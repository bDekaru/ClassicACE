using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        private static List<List<uint>> TreasureEncounters = null;

        public static void InitializeTreasureMaps()
        {
            TreasureEncounters = new List<List<uint>>();

            var classNames = DatabaseManager.World.GetAllWeenieClassNames();

            var t1classNames = classNames.Where(i => i.Value.StartsWith("t1-")).ToList();
            var t2classNames = classNames.Where(i => i.Value.StartsWith("t2-")).ToList();
            var t3classNames = classNames.Where(i => i.Value.StartsWith("t3-")).ToList();
            var t4classNames = classNames.Where(i => i.Value.StartsWith("t4-")).ToList();
            var t5classNames = classNames.Where(i => i.Value.StartsWith("t5-")).ToList();
            var t6classNames = classNames.Where(i => i.Value.StartsWith("t6-")).ToList();

            var t1 = new List<uint>();
            foreach (var entry in t1classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t1.Add(entry.Key);
            }

            var t2 = new List<uint>();
            foreach (var entry in t2classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t2.Add(entry.Key);
            }

            var t3 = new List<uint>();
            foreach (var entry in t3classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t3.Add(entry.Key);
            }

            var t4 = new List<uint>();
            foreach (var entry in t4classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t4.Add(entry.Key);
            }

            var t5 = new List<uint>();
            foreach (var entry in t5classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t5.Add(entry.Key);
            }

            var t6 = new List<uint>();
            foreach (var entry in t6classNames)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(entry.Key);
                if (weenie.GetProperty(ACE.Entity.Enum.Properties.PropertyInt.MaxGeneratedObjects).HasValue)
                    t6.Add(entry.Key);
            }

            TreasureEncounters.Add(t1);
            TreasureEncounters.Add(t2);
            TreasureEncounters.Add(t3);
            TreasureEncounters.Add(t4);
            TreasureEncounters.Add(t5);
            TreasureEncounters.Add(t6);
        }

        public static WorldObject TryCreateTreasureMap(Creature creature)
        {
            if (creature == null)
                return null;

            if (TreasureEncounters == null)
                InitializeTreasureMaps();

            var landblockTier = (int)Math.Floor((float)(creature.Tier ?? 1));
            var treatureTier = creature.RollTier();
            var treasureEncounterIndex = landblockTier - 1;

            if (treasureEncounterIndex < 0 || TreasureEncounters.Count < treasureEncounterIndex)
                return null;

            var possibleEncounterWcids = TreasureEncounters[treasureEncounterIndex];
            if (possibleEncounterWcids.Count == 0)
                return null;

            var rng = ThreadSafeRandom.Next(0, possibleEncounterWcids.Count() - 1);
            var encounterWcid = possibleEncounterWcids[rng];

            var possibleEncounters = DatabaseManager.World.GetEncountersByWcid(encounterWcid);
            if (possibleEncounters.Count == 0)
                return null;

            rng = ThreadSafeRandom.Next(0, possibleEncounters.Count() - 1);
            var encounter = possibleEncounters[rng];

            var xPos = Math.Clamp((encounter.CellX * 24.0f) + 12.0f, 0.5f, 191.5f);
            var yPos = Math.Clamp((encounter.CellY * 24.0f) + 12.0f, 0.5f, 191.5f);

            var pos = new Physics.Common.Position();
            pos.ObjCellID = (uint)(encounter.Landblock << 16) | 1;
            pos.Frame = new Physics.Animation.AFrame(new Vector3(xPos, yPos, 0), Quaternion.Identity);
            pos.adjust_to_outside();

            var sortCell = LScape.get_landcell(pos.ObjCellID) as SortCell;
            if (sortCell != null && sortCell.has_building())
                return null;

            var location = new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation);
            var coords = location.GetMapCoords();
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
            wo.Tier = treatureTier;
            wo.EWCoordinates = coords.Value.X;
            wo.NSCoordinates = coords.Value.Y;

            return wo;
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            var position = new Position((float)(NSCoordinates ?? 0), (float)(EWCoordinates ?? 0f));
            position.AdjustMapCoords();

            var distance = position.DistanceTo(player.Location);
            if (distance > 3600)
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
                if (distance > 3f || !DamageMod.HasValue)
                {
                    var animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(player.MotionTableId).GetAnimationLength(MotionCommand.Reading);
                    var actionChain = new ActionChain();
                    actionChain.AddAction(player, () => player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.ScanHorizon)));
                    actionChain.AddDelaySeconds(animTime);
                    actionChain.AddAction(player, () =>
                    {
                        var direction = player.Location.GetCardinalDirectionsTo(position);

                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The treasure map points {(direction == "" ? "at" : $"{direction} of")} your current location.", ChatMessageType.Broadcast));

                        if (distance <= 3 && !DamageMod.HasValue)
                        {
                            player.EnqueueBroadcast(new GameMessageUpdatePosition(player));
                            DamageMod = 1;
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance, MotionCommand.Cheer));
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

                    player.EnqueueBroadcast(new GameMessageUpdatePosition(player));

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
                            var level = Math.Min(player.Level ?? 1, Level ?? 1);
                            player.EarnXP(-level - 1000, XpType.Exploration, null, null, 0, null, ShareType.None, msg, PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5);

                            EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitLeather1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

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
                            var level = Math.Min(player.Level ?? 1, Level ?? 1);
                            player.EarnXP(-level - 1000, XpType.Exploration, null, null, 0, null, ShareType.None, "You unearth a treasure chest!", (PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5) * 3);

                            EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.HitLeather1, 1.0f));
                            player.EnqueueBroadcastMotion(new Motion(player.CurrentMotionState.Stance));

                            var tier = RollTier(Tier ?? 1);
                            var treasureChest = WorldObjectFactory.CreateNewWorldObject(TreasureChests[tier - 1]);

                            if (treasureChest == null)
                                return;

                            treasureChest.Location = player.Location.InFrontOf(1, false);

                            treasureChest.Tier = tier;

                            if (DefaultLocked)
                                treasureChest.IsLocked = true;

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
