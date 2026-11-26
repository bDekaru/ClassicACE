using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Pathfinding;
using ACE.Server.Pathfinding.Reachability;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Encounter = ACE.Database.Models.World.Encounter;
using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    public partial class TreasureMap : GenericObject
    {
        public TreasureMap(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
        }

        public TreasureMap(Biota biota) : base(biota)
        {
        }

        private List<uint> TreasureChests = new List<uint>()
        {
            50207,
            50208,
            50209,
            50210,
            50211,
            50212,
        };

        private static readonly string[] TreasureEncounterPrefixes = { "t0-startertown", "t1-", "t2-", "t3-", "t4-", "t5-", "t6-" };

        /// <summary>
        /// The encounter generators of each landblock tier (index), by class name prefix. Monsters die on several threads at once.
        /// </summary>
        private static readonly Lazy<List<List<uint>>> TreasureEncounters = new Lazy<List<List<uint>>>(GetTreasureEncounters, LazyThreadSafetyMode.PublicationOnly);

        private static readonly ConcurrentDictionary<uint, List<Encounter>> EncountersByWcid = new ConcurrentDictionary<uint, List<Encounter>>();

        private static List<List<uint>> GetTreasureEncounters()
        {
            var classNames = DatabaseManager.World.GetAllWeenieClassNames();

            return TreasureEncounterPrefixes.Select(prefix => classNames
                .Where(i => i.Value.StartsWith(prefix) && DatabaseManager.World.GetCachedWeenie(i.Key)?.GetProperty(PropertyInt.MaxGeneratedObjects) != null)
                .Select(i => i.Key)
                .ToList()).ToList();
        }

        public static WorldObject TryCreateTreasureMap(Weenie creatureWeenie)
        {
            if (creatureWeenie.WeenieType != WeenieType.Creature)
                return null;

            var creature = WorldObjectFactory.CreateNewWorldObject(creatureWeenie) as Creature;

            if (creature == null)
                return null;

            var treasure = TryCreateTreasureMap(creature);
            creature.Destroy();

            return treasure;
        }
        public static WorldObject TryCreateTreasureMap(Creature creature)
        {
            if (creature == null)
                return null;

            int landblockTier;
            int treasureTier;
            if (creature.Level <= 10)
            {
                landblockTier = 0;
                treasureTier = 1;
            }
            else
            {
                landblockTier = (int)Math.Floor(Math.Clamp(creature.Tier ?? 1, 1, 6));
                treasureTier = creature.RollTier();
            }

            var possibleEncounterWcids = TreasureEncounters.Value[landblockTier];
            if (possibleEncounterWcids.Count == 0 || WaitsForReachability)
                return null;

            Vector2? coords = null;
            for (int retries = 0; retries < 10 && coords == null; retries++)
            {
                var encounterWcid = possibleEncounterWcids[ThreadSafeRandom.Next(0, possibleEncounterWcids.Count - 1)];
                coords = RollEncounter(EncountersByWcid.GetOrAdd(encounterWcid, DatabaseManager.World.GetEncountersByWcid), creature.Level);
            }

            if (coords == null)
                return null;

            var wo = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.treasureMap);
            if (wo == null)
                return null;

            if (0.25 > ThreadSafeRandom.Next(0, 1.0f))
                wo.DefaultLocked = true;

            wo.Name = $"{creature.Name}'s Treasure Map";
            wo.LongDesc = $"{wo.LongDesc}\n\nThe map was found in the corpse of a level {creature.Level} {creature.Name}.{(wo.DefaultLocked ? "\n\nThe map indicates that the treasure chest is locked." : "")}";
            wo.Level = creature.Level;
            wo.Tier = treasureTier;
            wo.EWCoordinates = coords.Value.X;
            wo.NSCoordinates = coords.Value.Y;

            return wo;
        }

        /// <summary>
        /// With the startup reachability bake on (see Program), no map drops until its graph is in use, even past a
        /// bake that ended without one (until a restart), so no spot lands where a player can't walk to.
        /// </summary>
        private static bool WaitsForReachability => ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM && PropertyManager.GetBool("reachability_bake").Item && !Pathfinder.HasReachabilityGraph;

        /// <summary>
        /// A spot of one of the encounters that a player of the level (any level if null) can get to.
        /// </summary>
        public static Vector2? RollEncounter(List<Encounter> possibleEncounters, int? level)
        {
            if (possibleEncounters == null || possibleEncounters.Count == 0)
                return null;

            var rng = ThreadSafeRandom.Next(0, possibleEncounters.Count() - 1);
            var encounter = possibleEncounters[rng];

            var xPos = Math.Clamp((encounter.CellX * 24.0f) + 12.0f, 0.5f, 191.5f);
            var yPos = Math.Clamp((encounter.CellY * 24.0f) + 12.0f, 0.5f, 191.5f);

            var pos = new Physics.Common.Position();
            pos.ObjCellID = (uint)(encounter.Landblock << 16) | 1;
            pos.Frame = new Physics.Animation.AFrame(new Vector3(xPos, yPos, 0), Quaternion.Identity);
            pos.adjust_to_outside();

            var coords = pos.ACEPosition().GetMapCoords();
            if (coords == null)
                return null;

            // checked where the map points, as ActOnUse reads it back: a neighboring cell of the encounter's
            var spot = new Position(coords.Value.Y, coords.Value.X);
            spot.AdjustMapCoords();

            var sortCell = LScape.get_landcell(spot.Cell) as SortCell;
            if (sortCell == null || sortCell.has_building() || sortCell.WaterType == LandDefs.WaterType.EntirelyWater || !spot.IsWalkable())
                return null;

            // Unknown too: a landblock not baked yet, or one that failed. OneWay is fine: the way back can be a recall.
            if (Pathfinder.HasReachabilityGraph && Pathfinder.GetReachability(spot, level) is not (ReachabilityLabel.Walkable or ReachabilityLabel.OneWay))
                return null;

            return coords;
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            player.SyncLocation();
            player.EnqueueBroadcast(new GameMessageUpdatePosition(player));

            var position = new Position((float)(NSCoordinates ?? 0), (float)(EWCoordinates ?? 0f));
            position.AdjustMapCoords();

            var distance = Math.Abs(position.GetLargestOffset(player.Location));
            if (distance > 5000)
            {
                var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Reading);
                var actionChain = new ActionChain();
                actionChain.AddAction(player, () => player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Reading)));
                actionChain.AddDelaySeconds(animTime + 1);
                actionChain.AddAction(player, () =>
                {
                    string directions;
                    string name;
                    var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)position.Landblock).FirstOrDefault();
                    if (entryLandblock != null)
                    {
                        name = entryLandblock.Name;
                        if (entryLandblock.MicroRegion != "")
                            directions = $"{entryLandblock.Directions} {entryLandblock.Reference} in {entryLandblock.MicroRegion}";
                        else if (entryLandblock.MacroRegion != "" && entryLandblock.MacroRegion != "Dereth")
                            directions = $"{entryLandblock.Directions} {entryLandblock.Reference} in {entryLandblock.MacroRegion}";
                        else
                            directions = $"{entryLandblock.Directions} {entryLandblock.Reference}";
                    }
                    else
                    {
                        name = $"an unknown location({position.Landblock})";
                        directions = "";
                    }

                    var useName = false;
                    var prefix = " the";
                    if (name != "")
                    {
                        if (directions.StartsWith($" in {name}"))
                        {
                            useName = false;
                            prefix = "";
                            directions = directions.Substring(3, directions.Length - 3);
                        }
                        else
                        {
                            prefix = " ";
                            useName = true;
                        }
                    }

                    var message = $"The treasure map points to{prefix}{(useName ? name : "")}{directions}.";
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Broadcast));

                    player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                });
                actionChain.EnqueueChain();
            }
            else
            {
                if (distance > 2 || !DamageMod.HasValue)
                {
                    var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.ScanHorizon);
                    var actionChain = new ActionChain();
                    actionChain.AddAction(player, () => player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.ScanHorizon)));
                    actionChain.AddDelaySeconds(animTime);
                    actionChain.AddAction(player, () =>
                    {
                        player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

                        var direction = player.Location.GetCardinalDirectionsTo(position);

                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The treasure map points {(direction == "" ? "at" : $"{direction} of")} your current location.", ChatMessageType.Broadcast));

                        if (distance <= 2 && !DamageMod.HasValue)
                        {
                            DamageMod = 1;
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Cheer));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));
                        }
                        else
                            DamageMod = null;
                    });
                    actionChain.EnqueueChain();
                }
                else
                {
                    if (player.attacksReceivedPerSecond > 0)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot dig for treasure while in combat!", ChatMessageType.Broadcast));
                        return;
                    }

                    if (!Damage.HasValue)
                        Damage = 0;

                    if (Damage < 7)
                    {
                        string msg;
                        if (Damage == 0)
                            msg = "You start to dig for treasure!";
                        else
                            msg = "You continue to dig for treasure!";

                        Damage++;

                        var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Pickup);
                        var actionChain = new ActionChain();
                        actionChain.AddAction(player, () => player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Pickup)));
                        actionChain.AddDelaySeconds(animTime);
                        actionChain.AddAction(player, () =>
                        {
                            player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitLeather1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

                            var level = Math.Min(player.Level ?? 1, Level ?? 1);
                            player.EarnXP(-level - 1000, XpType.Exploration, null, null, 0, null, ShareType.Fellowship, msg, PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5);

                            var visibleCreatures = player.PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();
                            foreach (var creature in visibleCreatures)
                            {
                                if (!creature.IsDead && !creature.IsAwake)
                                    player.AlertMonster(creature);
                            }
                        });
                        actionChain.EnqueueChain();
                    }
                    else
                    {
                        var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Pickup);
                        var actionChain = new ActionChain();
                        actionChain.AddAction(player, () => player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Pickup)));
                        actionChain.AddDelaySeconds(animTime);
                        actionChain.AddAction(player, () =>
                        {
                            player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitPlate1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

                            var level = Math.Min(player.Level ?? 1, Level ?? 1);
                            player.EarnXP(-level - 1000, XpType.Exploration, null, null, 0, null, ShareType.Fellowship, "You unearth a treasure chest!", (PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5) * 3);

                            var tier = RollTier(Tier ?? 1);
                            var treasureChest = WorldObjectFactory.CreateNewWorldObject(TreasureChests[tier - 1]);

                            if (treasureChest == null)
                                return;

                            treasureChest.Location = player.Location.InFrontOf(1, false);

                            treasureChest.Tier = tier;

                            if (DefaultLocked)
                                treasureChest.IsLocked = true;

                            treasureChest.Generator = player;

                            if (treasureChest.EnterWorld())
                            {
                                if (!player.TryConsumeFromInventoryWithNetworking(this, 1))
                                {
                                    if (treasureChest != null)
                                        treasureChest.Destroy();
                                }
                            }
                            else if (treasureChest != null)
                                treasureChest.Destroy();
                        });
                        actionChain.EnqueueChain();
                    }
                }
            }
        }
    }
}
