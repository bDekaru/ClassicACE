using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Reachability;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading;
using Position = ACE.Entity.Position;
using Spell = ACE.Database.Models.World.Spell;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The travel links of the reachability graph, from the world database: portals placed or spawned by placed
    /// generators, portals summoned by killing a creature, portals random encounters spawn, and NPCs that send a player
    /// somewhere or summon a portal for one.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Generator events whose portal is part of a quest although the portal asks for none: Qin Xikit's Hidden Crown
        /// opens for twenty minutes once the four heat fonts are powered and the Font Guardian is spoken to.
        /// </summary>
        private static readonly HashSet<string> QuestGeneratorEvents = new HashSet<string> { "EventFontBrowerkStopgap" };

        /// <summary>
        /// The deepest generator chain followed from a placed object or an encounter: its generator, their generators
        /// and so on.
        /// </summary>
        private const int MaxGeneratorDepth = 4;

        /// <summary>
        /// Emote categories whose sending is a monster's spell in a fight, not travel a player chooses.
        /// </summary>
        private static readonly HashSet<EmoteCategory> CombatEmoteCategories = new HashSet<EmoteCategory> { EmoteCategory.NewEnemy, EmoteCategory.ReceiveCritical, EmoteCategory.ResistSpell };

        /// <summary>
        /// Travel as the database gives it, before it is placed on a graph. From are the places it starts from, a list
        /// often shared between sources.
        /// </summary>
        private class TravelSource
        {
            public IReadOnlyList<Position> From;
            public Position To;
            public int MinLevel, MaxLevel;
            public TravelKind Kind;
            public string Source;
        }

        /// <summary>
        /// The travel sources, from the database on first use; a failed load is tried again on the next use.
        /// </summary>
        private static readonly Lazy<List<TravelSource>> TravelSources = new Lazy<List<TravelSource>>(() =>
        {
            var watch = Stopwatch.StartNew();
            var sources = LoadTravelSources();
            log.Info($"Pathfinder: {sources.Count} travel sources for reachability loaded in {watch.Elapsed.TotalSeconds:F1} s");
            return sources;
        }, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// The travel sources placed on the summaries' pieces: one link per distinct start, end, levels and kind. None
        /// while the database can't be read.
        /// </summary>
        private static List<TravelLink> GetTravelLinks(TileSummary[] summaries, InteriorSummary[] interiors)
        {
            List<TravelSource> sources;
            try
            {
                sources = TravelSources.Value;
            }
            catch (Exception e)
            {
                log.Error($"Pathfinder: loading the travel links for reachability failed, the graph has none until the next build: {e}");
                return new List<TravelLink>();
            }

            var maxLevelApplies = PropertyManager.GetBool("use_portal_max_level_requirement").Item;
            var links = new List<TravelLink>();
            var seen = new HashSet<(GraphSpot, GraphSpot, int, int, TravelKind)>();

            // the encounter spot lists are shared by many sources and long: each is placed once
            var placedLists = new Dictionary<IReadOnlyList<Position>, List<GraphSpot>>(ReferenceEqualityComparer.Instance);

            foreach (var source in sources)
            {
                if (!placedLists.TryGetValue(source.From, out var from))
                    placedLists[source.From] = from = source.From.Select(p => GetGraphSpot(summaries, interiors, p)).Distinct().ToList();

                var maxLevel = maxLevelApplies ? source.MaxLevel : 0;
                var to = GetGraphSpot(summaries, interiors, source.To);
                foreach (var spot in from)
                    if (seen.Add((spot, to, source.MinLevel, maxLevel, source.Kind)))
                        links.Add(new TravelLink { From = spot, To = to, MinLevel = source.MinLevel, MaxLevel = maxLevel, Kind = source.Kind, Source = source.Source });
            }
            return links;
        }

        private static List<TravelSource> LoadTravelSources()
        {
            var database = DatabaseManager.World;
            var rotateOffsets = PropertyManager.GetBool("use_generator_rotation_offset").Item;

            HashSet<uint> portals;
            List<WeeniePropertiesGenerator> profiles;
            Dictionary<uint, Spell> spells;
            List<(WeeniePropertiesEmote emote, WeeniePropertiesEmoteAction action)> emoteTravel;
            ILookup<uint, WeeniePropertiesGenerator> profilesBySpawn;
            ILookup<uint, LandblockInstance> placed;
            Dictionary<uint, HashSet<uint>> encounterRoots;
            Dictionary<uint, List<Position>> rootSpots;
            HashSet<string> emoteStartedEvents;

            using (var context = new WorldDbContext())
            {
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                portals =database.GetAllWeenieTypes(context).Where(t => t.Value == (int)WeenieType.Portal).Select(t => t.Key).ToHashSet();
                profiles = context.WeeniePropertiesGenerator.ToList();
                profilesBySpawn = profiles.ToLookup(p => p.WeenieClassId);

                // the spells that send to a place or summon one of the caster's linked portals
                spells = context.Spell.Where(s => s.PositionObjCellId != null || (s.Link != null && s.Link != 0)).ToDictionary(s => s.Id);
                var spellIds = spells.Keys.Select(id => (int?)(int)id).ToList();
                emoteTravel = context.WeeniePropertiesEmoteAction
                    .Where(a => ((a.Type == (uint)EmoteType.CastSpell || a.Type == (uint)EmoteType.CastSpellInstant) && spellIds.Contains(a.SpellId)) || (a.Type == (uint)EmoteType.TeleportTarget && a.ObjCellId > 0))
                    .Join(context.WeeniePropertiesEmote, a => a.EmoteId, e => e.Id, (a, e) => new { a, e })
                    .AsEnumerable()
                    .Where(x => !CombatEmoteCategories.Contains((EmoteCategory)x.e.Category))
                    .Select(x => (x.e, x.a))
                    .ToList();

                emoteStartedEvents = context.WeeniePropertiesEmoteAction.Where(a => a.Type == (uint)EmoteType.StartEvent && a.Message != null).Select(a => a.Message).ToList()
                    .Select(EventManager.GetEventName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // the portals and emote owners, and the generators that spawn them
                var targets = portals.Concat(emoteTravel.Select(x => x.emote.ObjectId)).ToHashSet();
                var wanted = new HashSet<uint>(targets);
                var spawners = targets.ToList();
                for (var depth = 0; depth < MaxGeneratorDepth && spawners.Count > 0; depth++)
                    spawners = spawners.SelectMany(w => profilesBySpawn[w]).Select(p => p.ObjectId).Where(wanted.Add).ToList();
                var wantedList = wanted.ToList();
                placed = context.LandblockInstance.Where(i => wantedList.Contains(i.WeenieClassId)).ToList().ToLookup(i => i.WeenieClassId);

                // which encounters spawn each portal or emote owner, through their generators' child generators
                var profilesOf = profiles.ToLookup(p => p.ObjectId);
                encounterRoots = new Dictionary<uint, HashSet<uint>>();
                foreach (var root in context.Encounter.Select(e => e.WeenieClassId).Distinct().ToList())
                {
                    var visited = new HashSet<uint> { root };
                    var frontier = new List<uint> { root };
                    // depth 0 is the encounter's own weenie, which can be a portal or an NPC itself
                    for (var depth = 0; depth <= MaxGeneratorDepth && frontier.Count > 0; depth++)
                    {
                        foreach (var wcid in frontier.Where(targets.Contains))
                        {
                            if (!encounterRoots.TryGetValue(wcid, out var roots))
                                encounterRoots[wcid] = roots = new HashSet<uint>();
                            roots.Add(root);
                        }
                        frontier = frontier.SelectMany(g => profilesOf[g]).Select(p => p.WeenieClassId).Where(visited.Add).ToList();
                    }
                }

                // an encounter spawns at the middle of its terrain cell
                var neededRoots = encounterRoots.Values.SelectMany(r => r).Distinct().ToList();
                rootSpots = context.Encounter.Where(e => neededRoots.Contains(e.WeenieClassId)).ToList()
                    .GroupBy(e => e.WeenieClassId)
                    .ToDictionary(g => g.Key, g => g.Select(e => new Position((uint)(e.Landblock << 16 | e.CellX * 8 + e.CellY + 1), e.CellX * 24 + 12f, e.CellY * 24 + 12f, 0f, 0f, 0f, 0f, 1f)).ToList());
            }

            string GetName(uint wcid) => database.GetCachedWeenie(wcid)?.GetProperty(PropertyString.Name) ?? "?";
            Position ToPosition(LandblockInstance i) => new Position(i.ObjCellId, i.OriginX, i.OriginY, i.OriginZ, i.AnglesX, i.AnglesY, i.AnglesZ, i.AnglesW);

            // where an object is placed or spawned by placed generators, through up to MaxGeneratorDepth generators
            List<Position> GetPlaces(uint wcid, int depth)
            {
                var places = placed[wcid].Select(ToPosition).ToList();
                if (depth < MaxGeneratorDepth)
                    foreach (var profile in profilesBySpawn[wcid])
                        places.AddRange(GetSpawnSpots(profile, depth + 1));
                return places;
            }
            List<Position> GetSpawnSpots(WeeniePropertiesGenerator profile, int depth) => GetPlaces(profile.ObjectId, depth).Select(g => GetSpawnSpot(g, profile, rotateOffsets)).ToList();

            // a generator tied to an event spawns only while the event is on: open if it is on now or an emote starts it
            bool IsEventGated(uint generator)
            {
                var name = database.GetCachedWeenie(generator)?.GetProperty(PropertyString.GeneratorEvent);
                if (string.IsNullOrEmpty(name) || !EventManager.IsEventAvailable(name))
                    return false;
                name = EventManager.GetEventName(name);
                return QuestGeneratorEvents.Contains(name) || (!EventManager.IsEventStarted(name, null, null) && !emoteStartedEvents.Contains(name));
            }

            var encounterSpots = new Dictionary<uint, IReadOnlyList<Position>>();
            IReadOnlyList<Position> GetEncounterSpots(uint wcid)
            {
                if (!encounterSpots.TryGetValue(wcid, out var spots))
                    encounterSpots[wcid] = spots = (encounterRoots.GetValueOrDefault(wcid) ?? new HashSet<uint>()).SelectMany(r => rootSpots.GetValueOrDefault(r) ?? new List<Position>()).ToList();
                return spots;
            }

            // how the reference player (regular gameplay mode, NPK, not Olthoi, no vitae) may take a portal, as
            // Portal.GetRestriction and CheckUseRequirements judge it: none if not at all. A portal with a relative
            // destination leads somewhere else from each place, as Portal.EnterWorld sets it.
            List<TravelSource> GetPortalSources(uint wcid, IReadOnlyList<Position> from, string where)
            {
                var portal = database.GetCachedWeenie(wcid);
                if (portal == null)
                    return new List<TravelSource>();

                var restrictions = (PortalBitmask)(portal.GetProperty(PropertyInt.PortalBitmask) ?? (int)PortalBitmask.Unrestricted);
                if (restrictions == PortalBitmask.Undef || restrictions.HasFlag(PortalBitmask.NoNPK) || restrictions.HasFlag(PortalBitmask.OnlyOlthoiPCs)
                    || (portal.GetProperty(PropertyBool.AdvocateQuest) ?? false) || (portal.GetProperty(PropertyInt64.GameplayModeExtraIdentifier) ?? 0) != 0)
                    return new List<TravelSource>();

                TravelSource Make(IReadOnlyList<Position> starts, Position to) => new TravelSource
                {
                    From = starts,
                    To = to,
                    MinLevel = portal.GetProperty(PropertyInt.MinLevel) ?? 0,
                    MaxLevel = portal.GetProperty(PropertyInt.MaxLevel) ?? 0,
                    Kind = portal.GetProperty(PropertyString.QuestRestriction) == null ? TravelKind.Open : TravelKind.Quest,
                    Source = $"portal {wcid} {GetName(wcid)}{where}"
                };

                var destination = portal.GetPosition(PositionType.Destination);
                if (destination != null)
                    return new List<TravelSource> { Make(from, destination) };

                var relative = portal.GetPosition(PositionType.RelativeDestination);
                if (relative == null)
                    return new List<TravelSource>();

                return from.Select(start =>
                {
                    var to = new Position(start);
                    to.Pos += relative.Pos;
                    to.LandblockId = new LandblockId(to.GetCell());
                    return Make(new[] { start }, to);
                }).ToList();
            }

            var sources = new List<TravelSource>();

            foreach (var group in placed.Where(g => portals.Contains(g.Key)))
                sources.AddRange(GetPortalSources(group.Key, group.Select(ToPosition).ToList(), ""));

            foreach (var profile in profiles.Where(p => portals.Contains(p.WeenieClassId)))
            {
                var spots = GetSpawnSpots(profile, 1);
                if (spots.Count == 0)
                    continue;

                var gated = IsEventGated(profile.ObjectId);
                foreach (var source in GetPortalSources(profile.WeenieClassId, spots, $" from generator {profile.ObjectId}"))
                {
                    if (gated)
                        source.Kind = TravelKind.Quest;
                    sources.Add(source);
                }
            }

            // most portals random encounters spawn lead where a fixed one does; those that don't need the luck of
            // finding one
            var fixedLandblocks = sources.Select(s => s.To.Cell >> 16).ToHashSet();
            foreach (var wcid in encounterRoots.Keys.Where(portals.Contains))
            {
                foreach (var source in GetPortalSources(wcid, GetEncounterSpots(wcid), " from encounters").Where(s => !fixedLandblocks.Contains(s.To.Cell >> 16)))
                {
                    source.Kind = TravelKind.Quest;
                    sources.Add(source);
                }
            }

            var ownerPlaces = new Dictionary<uint, IReadOnlyList<Position>>();
            foreach (var (emote, action) in emoteTravel)
            {
                var owner = emote.ObjectId;
                if (!ownerPlaces.TryGetValue(owner, out var from))
                    ownerPlaces[owner] = from = GetPlaces(owner, 0).Concat(GetEncounterSpots(owner)).ToList();
                if (from.Count == 0)
                    continue;

                List<TravelSource> travel;
                if (action.Type == (uint)EmoteType.TeleportTarget)
                    travel = new List<TravelSource> { new TravelSource { From = from, To = new Position(action.ObjCellId.Value, action.OriginX ?? 0, action.OriginY ?? 0, action.OriginZ ?? 0, 0f, 0f, 0f, 1f), Source = "teleport" } };
                else
                {
                    var spell = spells[(uint)action.SpellId.Value];
                    if (spell.PositionObjCellId != null)
                        travel = new List<TravelSource> { new TravelSource { From = from, To = new Position(spell.PositionObjCellId.Value, spell.PositionOriginX ?? 0, spell.PositionOriginY ?? 0, spell.PositionOriginZ ?? 0, 0f, 0f, 0f, 1f), Source = $"spell {spell.Id}" } };
                    else
                    {
                        var portal = database.GetCachedWeenie(owner)?.GetProperty(spell.Link == 1 ? PropertyDataId.LinkedPortalOne : PropertyDataId.LinkedPortalTwo);
                        travel = portal == null ? new List<TravelSource>() : GetPortalSources(portal.Value, from, "");
                    }
                }

                var category = (EmoteCategory)emote.Category;
                foreach (var source in travel)
                {
                    if (category == EmoteCategory.Death)
                    {
                        source.Kind = TravelKind.Quest;
                        source.Source = $"kill {owner} {GetName(owner)}: {source.Source}";
                    }
                    else
                    {
                        source.Kind = TravelKind.Unjudged;
                        source.Source = $"{category} emote of {owner} {GetName(owner)}" + (emote.WeenieClassId != null ? $" for {emote.WeenieClassId} {GetName(emote.WeenieClassId.Value)}" : "")
                            + (emote.Quest != null ? $" ({emote.Quest})" : "") + $": {source.Source}";
                    }
                    sources.Add(source);
                }
            }

            return sources;
        }

        /// <summary>
        /// Where a generator profile spawns, as GeneratorProfile.Spawn_Specific places it; anything else spawns at or
        /// around the generator.
        /// </summary>
        private static Position GetSpawnSpot(Position generator, WeeniePropertiesGenerator profile, bool rotateOffsets)
        {
            if (!((RegenLocationType)profile.WhereCreate).HasFlag(RegenLocationType.Specific))
                return generator;

            if ((profile.ObjCellId ?? 0) > 0)
                return new Position(profile.ObjCellId.Value, profile.OriginX ?? 0, profile.OriginY ?? 0, profile.OriginZ ?? 0, 0f, 0f, 0f, 1f);

            var offset = new Vector3(profile.OriginX ?? 0, profile.OriginY ?? 0, profile.OriginZ ?? 0);
            if (rotateOffsets)
                offset = Vector3.Transform(offset, generator.Rotation);
            return new Position(generator.Cell, generator.PositionX + offset.X, generator.PositionY + offset.Y, generator.PositionZ + offset.Z, 0f, 0f, 0f, 1f);
        }
    }
}
