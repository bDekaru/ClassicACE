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
        /// often shared between sources. MaxLevel is a portal's, which use_portal_max_level_requirement can turn off;
        /// NpcMaxLevel is an NPC's level test, which always applies.
        /// </summary>
        private class TravelSource
        {
            public IReadOnlyList<Position> From;
            public Position To;
            public int MinLevel, MaxLevel, NpcMaxLevel;
            public TravelKind Kind;
            public bool Anywhere;
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
            var seen = new HashSet<(GraphSpot, GraphSpot, int, int, TravelKind, bool)>();

            // the encounter spot lists are shared by many sources and long: each is placed once
            var placedLists = new Dictionary<IReadOnlyList<Position>, List<GraphSpot>>(ReferenceEqualityComparer.Instance);
            var anywhere = new List<GraphSpot> { default };

            foreach (var source in sources)
            {
                var from = source.Anywhere ? anywhere : null;
                if (from == null && !placedLists.TryGetValue(source.From, out from))
                    placedLists[source.From] = from = source.From.Select(p => GetGraphSpot(summaries, interiors, p)).Distinct().ToList();

                var maxLevel = maxLevelApplies ? source.MaxLevel : 0;
                if (source.NpcMaxLevel > 0 && (maxLevel == 0 || source.NpcMaxLevel < maxLevel))
                    maxLevel = source.NpcMaxLevel;
                var to = GetGraphSpot(summaries, interiors, source.To);
                foreach (var spot in from)
                    if (seen.Add((spot, to, source.MinLevel, maxLevel, source.Kind, source.Anywhere)))
                        links.Add(new TravelLink { From = spot, To = to, MinLevel = source.MinLevel, MaxLevel = maxLevel, Kind = source.Kind, Anywhere = source.Anywhere, Source = source.Source });
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
            ILookup<uint, WeeniePropertiesEmote> ownerEmotes;
            HashSet<uint> coins;
            ILookup<uint, uint> stockingVendors;
            Dictionary<uint, uint> summonGems;

            using (var context = new WorldDbContext())
            {
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                var weenieTypes = database.GetAllWeenieTypes(context);
                portals = weenieTypes.Where(t => t.Value == (int)WeenieType.Portal).Select(t => t.Key).ToHashSet();
                coins = weenieTypes.Where(t => t.Value == (int)WeenieType.Coin).Select(t => t.Key).ToHashSet();
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

                // the emote owners' emote sets, which judge their travel, and the vendors of the items they ask for:
                // vendors stock through their shop list or their generator profiles
                var owners = emoteTravel.Select(x => x.emote.ObjectId).Distinct().ToList();
                ownerEmotes = context.WeeniePropertiesEmote.Include(e => e.WeeniePropertiesEmoteAction).Where(e => owners.Contains(e.ObjectId)).ToList().ToLookup(e => e.ObjectId);
                var asked = ownerEmotes.SelectMany(g => g).SelectMany(e => e.WeeniePropertiesEmoteAction.Where(a => a.Type == (uint)EmoteType.InqOwnsItems).Select(a => a.WeenieClassId).Append(e.WeenieClassId))
                    .Where(w => w != null).Select(w => w.Value).ToHashSet();

                // the gems that summon their linked portal, by gem, and their vendors too
                var linked = context.WeeniePropertiesDID.Where(d => d.Type == (ushort)PropertyDataId.LinkedPortalOne).ToList()
                    .Where(d => weenieTypes.GetValueOrDefault(d.ObjectId) == (int)WeenieType.Gem).ToList();
                var linkedIds = linked.Select(d => d.ObjectId).ToList();
                var gemSpells = context.WeeniePropertiesDID.Where(d => d.Type == (ushort)PropertyDataId.Spell && linkedIds.Contains(d.ObjectId)).ToDictionary(d => d.ObjectId, d => d.Value);
                summonGems = linked.Where(d => gemSpells.TryGetValue(d.ObjectId, out var spell) && spells.TryGetValue(spell, out var s) && s.Link == 1).ToDictionary(d => d.ObjectId, d => d.Value);
                asked.UnionWith(summonGems.Keys);
                var askedList = asked.ToList();
                stockingVendors = context.WeeniePropertiesCreateList.Where(c => c.DestinationType == (sbyte)DestinationType.Shop && askedList.Contains(c.WeenieClassId)).Select(c => new { c.WeenieClassId, c.ObjectId }).ToList()
                    .Select(c => (item: c.WeenieClassId, vendor: c.ObjectId))
                    .Concat(profiles.Where(p => asked.Contains(p.WeenieClassId)).Select(p => (item: p.WeenieClassId, vendor: p.ObjectId)))
                    .Where(s => weenieTypes.GetValueOrDefault(s.vendor) == (int)WeenieType.Vendor)
                    .Distinct().ToLookup(s => s.item, s => s.vendor);

                // the portals, emote owners and vendors, and the generators that spawn them
                var targets = portals.Concat(owners).Concat(stockingVendors.SelectMany(g => g)).ToHashSet();
                var wanted = new HashSet<uint>(targets);
                var spawners = targets.ToList();
                for (var depth = 0; depth < MaxGeneratorDepth && spawners.Count > 0; depth++)
                    spawners = spawners.SelectMany(w => profilesBySpawn[w]).Select(p => p.ObjectId).Where(wanted.Add).ToList();
                var wantedList = wanted.ToList();
                placed = context.LandblockInstance.Where(i => wantedList.Contains(i.WeenieClassId)).ToList().ToLookup(i => i.WeenieClassId);

                // which encounters spawn each portal, emote owner or vendor, through their generators' child generators
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

            // an event counts as on when it is on now or an emote starts it
            bool IsEventOpen(string name)
            {
                name = EventManager.GetEventName(name);
                return !QuestGeneratorEvents.Contains(name) && (EventManager.IsEventStarted(name, null, null) || emoteStartedEvents.Contains(name));
            }

            // a generator tied to an event spawns only while the event is on
            bool IsEventGated(uint generator)
            {
                var name = database.GetCachedWeenie(generator)?.GetProperty(PropertyString.GeneratorEvent);
                return !string.IsNullOrEmpty(name) && EventManager.IsEventAvailable(name) && !IsEventOpen(name);
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

            // coins and what a placed vendor sells
            var fees = new Dictionary<uint, bool>();
            bool IsFee(uint wcid)
            {
                if (!fees.TryGetValue(wcid, out var fee))
                    fees[wcid] = fee = coins.Contains(wcid) || stockingVendors[wcid].Any(v => GetPlaces(v, 0).Count > 0);
                return fee;
            }

            // whether the reference player at a level gets the inquiry or Goto action to run an emote set of this
            // category; null where it never does
            Func<int, bool> GetPassCondition(WeeniePropertiesEmoteAction action, EmoteCategory category)
            {
                var success = category is EmoteCategory.TestSuccess or EmoteCategory.QuestSuccess or EmoteCategory.EventSuccess;
                bool InRange(int value) => value >= (action.Min ?? int.MinValue) && value <= (action.Max ?? int.MaxValue);
                Func<int, bool> Always(bool passes) => passes ? level => true : null;

                switch ((EmoteType)action.Type)
                {
                    case EmoteType.Goto:
                        return Always(category == EmoteCategory.GotoSet);
                    // QuestSuccess needs the flag with its timer running
                    case EmoteType.InqQuest:
                    case EmoteType.InqMyQuest:
                        return Always(category == EmoteCategory.QuestFailure);
                    case EmoteType.InqFellowQuest:
                        return Always(category == EmoteCategory.QuestNoFellow);
                    case EmoteType.InqIntStat when category is EmoteCategory.TestSuccess or EmoteCategory.TestFailure:
                        if (action.Stat == (int)PropertyInt.Level)
                            return level => InRange(level) == success;
                        return Always(action.Stat == (int)PropertyInt.PlayerKillerStatus && InRange((int)PlayerKillerStatus.NPK) == success);
                    case EmoteType.InqYesNo:
                        return Always(category is EmoteCategory.TestSuccess or EmoteCategory.TestFailure);
                    case EmoteType.InqOwnsItems:
                        return Always(category == EmoteCategory.TestFailure || (category == EmoteCategory.TestSuccess && action.WeenieClassId != null && IsFee(action.WeenieClassId.Value)));
                    case EmoteType.InqEvent:
                        return Always(category == EmoteCategory.EventSuccess ? IsEventOpen(action.Message) : category == EmoteCategory.EventFailure && !EventManager.IsEventStarted(action.Message, null, null));
                    default:
                        return null;
                }
            }

            // the levels (index 1 to WorldGraph.MaxLevel) at which the reference player gets an emote set to run, through
            // the emote sets that run it: no quest flags, no fellowship, NPK, carrying only fees
            bool[] GetRunLevels(WeeniePropertiesEmote set, HashSet<uint> visiting)
            {
                var levels = new bool[WorldGraph.MaxLevel + 1];
                var category = (EmoteCategory)set.Category;
                switch (category)
                {
                    case EmoteCategory.Use:
                    case EmoteCategory.Activation:
                        Array.Fill(levels, true, 1, WorldGraph.MaxLevel);
                        return levels;
                    case EmoteCategory.Give:
                    case EmoteCategory.Refuse:
                        if (set.WeenieClassId != null && IsFee(set.WeenieClassId.Value))
                            Array.Fill(levels, true, 1, WorldGraph.MaxLevel);
                        return levels;
                    case EmoteCategory.QuestSuccess:
                    case EmoteCategory.QuestFailure:
                    case EmoteCategory.QuestNoFellow:
                    case EmoteCategory.TestSuccess:
                    case EmoteCategory.TestFailure:
                    case EmoteCategory.EventSuccess:
                    case EmoteCategory.EventFailure:
                    case EmoteCategory.GotoSet:
                        break;
                    default:
                        return levels;
                }
                if (set.Quest == null || !visiting.Add(set.Id))
                    return levels;

                foreach (var parent in ownerEmotes[set.ObjectId])
                {
                    foreach (var action in parent.WeeniePropertiesEmoteAction.Where(a => set.Quest.Equals(a.Message, StringComparison.OrdinalIgnoreCase)))
                    {
                        var passes = GetPassCondition(action, category);
                        if (passes == null)
                            continue;
                        var parentLevels = GetRunLevels(parent, visiting);
                        for (var level = 1; level <= WorldGraph.MaxLevel; level++)
                            levels[level] |= parentLevels[level] && passes(level);
                    }
                }
                visiting.Remove(set.Id);
                return levels;
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

            // a summon gem a placed vendor sells is anyone's (gems trade, so the vendor's reach doesn't matter); one that
            // asks for a skill adds nothing. The gem casts the summon itself, which skips the portal's NoSummon: most town
            // gem portals have it, against player ties.
            foreach (var (gem, portal) in summonGems)
            {
                var gemWeenie = database.GetCachedWeenie(gem);
                if (!IsFee(gem) || gemWeenie == null
                    || gemWeenie.GetProperty(PropertyInt.UseRequiresSkill) != null || gemWeenie.GetProperty(PropertyDataId.ItemSkillLimit) != null)
                    continue;

                foreach (var source in GetPortalSources(portal, Array.Empty<Position>(), $" from gem {gem} {GetName(gem)}"))
                {
                    source.MinLevel = Math.Max(source.MinLevel, gemWeenie.GetProperty(PropertyInt.UseRequiresLevel) ?? 0);
                    source.Anywhere = true;
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
                var levels = category == EmoteCategory.Death ? null : GetRunLevels(emote, new HashSet<uint>());
                foreach (var source in travel)
                {
                    if (category == EmoteCategory.Death)
                    {
                        source.Kind = TravelKind.Quest;
                        source.Source = $"kill {owner} {GetName(owner)}: {source.Source}";
                        sources.Add(source);
                        continue;
                    }

                    source.Source = $"{category} emote of {owner} {GetName(owner)}" + (emote.WeenieClassId != null ? $" for {emote.WeenieClassId} {GetName(emote.WeenieClassId.Value)}" : "")
                        + (emote.Quest != null ? $" ({emote.Quest})" : "") + $": {source.Source}";

                    // a summoned portal that asks for a quest stays Quest whatever the emote set asks
                    if (source.Kind == TravelKind.Open)
                    {
                        // one open link per run of levels the emote set runs at
                        for (var start = 1; start <= WorldGraph.MaxLevel; start++)
                        {
                            if (!levels[start] || levels[start - 1])
                                continue;
                            var end = start;
                            while (end < WorldGraph.MaxLevel && levels[end + 1])
                                end++;
                            var minLevel = Math.Max(source.MinLevel, start == 1 ? 0 : start);
                            if (minLevel > end)
                                continue;
                            sources.Add(new TravelSource
                            {
                                From = source.From,
                                To = source.To,
                                MinLevel = minLevel,
                                MaxLevel = source.MaxLevel,
                                NpcMaxLevel = end == WorldGraph.MaxLevel ? 0 : end,
                                Kind = TravelKind.Open,
                                Source = source.Source
                            });
                        }
                    }

                    // the levels it doesn't run at, for the report
                    if (source.Kind == TravelKind.Quest || levels.Skip(1).Contains(false))
                    {
                        source.Kind = TravelKind.Quest;
                        sources.Add(source);
                    }
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
