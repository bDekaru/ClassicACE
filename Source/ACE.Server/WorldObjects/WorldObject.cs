using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;

using log4net;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Util;
using ACE.Server.WorldObjects.Managers;

using Landblock = ACE.Server.Entity.Landblock;
using Position = ACE.Entity.Position;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Base Object for Game World
    /// </summary>
    public abstract partial class WorldObject : IActor
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// If this object was created from a weenie (and not a database biota), this is the source.
        /// You should never manipulate these values. You should only reference these values in extreme cases.
        /// </summary>
        public Weenie Weenie { get; }

        /// <summary>
        /// This is object property overrides that should have come from the shard db (or init to defaults of object is new to this instance).
        /// You should not manipulate these values directly. To manipulate this use the exposed SetProperty and RemoveProperty functions instead.
        /// </summary>
        public Biota Biota { get; }

        /// <summary>
        /// This is just a wrapper around Biota.Id
        /// </summary>
        public ObjectGuid Guid { get; }

        public PhysicsObj PhysicsObj { get; protected set; }

        public ObjectDescriptionFlag ObjectDescriptionFlags { get; protected set; }

        public SequenceManager Sequences { get; } = new SequenceManager();

        public virtual float ListeningRadius { get; protected set; } = 5f;

        /// <summary>
        /// Should only be adjusted by Landblock -- default is null
        /// </summary>
        public Landblock CurrentLandblock
        {
            get => currentLandblock;

            internal set
            {
                var previousLandblock = currentLandblock;
                currentLandblock = value;

                if (previousLandblock != currentLandblock)
                {
                    if (previousLandblock != null)
                        OnLeaveLandblock(previousLandblock);

                    if (currentLandblock != null)
                        OnEnterLandblock(currentLandblock);
                }
            }
        }

        public virtual void OnEnterLandblock(Landblock landblock)
        {
        }

        public virtual void OnLeaveLandblock(Landblock Landblock)
        {
        }

        public bool IsBusy { get; set; }
        public bool IsShield { get => CombatUse != null && CombatUse == ACE.Entity.Enum.CombatUse.Shield; }
        // ValidLocations is bugged for some older two-handed weapons, still contains MeleeWeapon instead of TwoHanded?
        //public bool IsTwoHanded { get => CurrentWieldedLocation != null && CurrentWieldedLocation == EquipMask.TwoHanded; }
        public bool IsTwoHanded { get => DefaultCombatStyle != null && (DefaultCombatStyle == CombatStyle.TwoHanded ); }
        public bool IsBow { get => DefaultCombatStyle != null && (DefaultCombatStyle == CombatStyle.Bow || DefaultCombatStyle == CombatStyle.Crossbow); }
        public bool IsAtlatl { get => DefaultCombatStyle != null && DefaultCombatStyle == CombatStyle.Atlatl; }
        public bool IsAmmoLauncher { get => IsBow || IsAtlatl; }
        public bool IsThrownWeapon { get => DefaultCombatStyle != null && DefaultCombatStyle == CombatStyle.ThrownWeapon; }
        public bool IsRanged { get => IsAmmoLauncher || IsThrownWeapon; }
        public bool IsCaster { get => DefaultCombatStyle != null && (DefaultCombatStyle == CombatStyle.Magic); }
        public bool IsClothArmor { get => ItemType == ItemType.Clothing && ClothingPriority.HasValue && (ClothingPriority.Value.HasFlag(CoverageMask.OuterwearChest) || ClothingPriority.Value.HasFlag(CoverageMask.Feet) || ClothingPriority.Value.HasFlag(CoverageMask.Hands) || ClothingPriority.Value.HasFlag(CoverageMask.Head)); } // Robes, Dresses, Cloth Caps, Cloth Gloves and Cloth Shoes.
        public bool IsRobe { get => ItemType == ItemType.Clothing && ClothingPriority.HasValue && ClothingPriority.Value.HasFlag(CoverageMask.OuterwearChest); } // Robes and Dresses

        public bool IsCreature
        {
            get
            {
                return WeenieType == WeenieType.Creature || WeenieType == WeenieType.Cow ||
                       WeenieType == WeenieType.Sentinel || WeenieType == WeenieType.Admin ||
                       WeenieType == WeenieType.Vendor ||
                       WeenieType == WeenieType.CombatPet || WeenieType == WeenieType.Pet;
            }
        }

        public EmoteManager EmoteManager;
        public EnchantmentManagerWithCaching EnchantmentManager;

        // todo: move these to a base projectile class
        public WorldObject ProjectileSource { get; set; }
        public WorldObject ProjectileTarget { get; set; }

        public WorldObject ProjectileLauncher { get; set; }
        public WorldObject ProjectileAmmo { get; set; }

        public bool HitMsg;     // FIXME: find a better way to do this for projectiles

        public WorldObject Wielder;

        public WorldObject() { }

        /// <summary>
        /// A new biota will be created taking all of its values from weenie.
        /// </summary>
        protected WorldObject(Weenie weenie, ObjectGuid guid)
        {
            Weenie = weenie;
            Biota = ACE.Entity.Adapter.WeenieConverter.ConvertToBiota(weenie, guid.Full, false, true);
            Guid = guid;

            InitializePropertyDictionaries();
            SetEphemeralValues();
            InitializeGenerator();
            InitializeHeartbeats();

            CreationTimestamp = (int)Time.GetUnixTime();

            if (!Guid.IsPlayer())
            {
                var overrideGameplayMode = GetProperty(PropertyInt.GameplayMode);
                if (overrideGameplayMode.HasValue)
                    GameplayMode = (GameplayModes)overrideGameplayMode.Value;
                else
                    GameplayMode = GameplayModes.InitialMode;
            }
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// Any properties tagged as Ephemeral will be removed from the biota.
        /// </summary>
        protected WorldObject(Biota biota)
        {
            Biota = biota;
            Guid = new ObjectGuid(Biota.Id);

            biotaOriginatedFromDatabase = true;

            InitializePropertyDictionaries();
            SetEphemeralValues();
            InitializeGenerator();
            InitializeHeartbeats();
        }

        public bool BumpVelocity { get; set; }

        /// <summary>
        /// Initializes a new default physics object
        /// </summary>
        public virtual void InitPhysicsObj()
        {
            //Console.WriteLine($"InitPhysicsObj({Name} - {Guid})");

            var defaultState = CalculatedPhysicsState();

            if (!(this is Creature))
            {
                var isDynamic = Static == null || !Static.Value;
                var setupTableId = SetupTableId;

                // TODO: REMOVE ME?
                // Temporary workaround fix to account for ace spawn placement issues with certain hooked objects.
                if (this is Hook)
                {
                    var hookWeenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                    setupTableId = hookWeenie.GetProperty(PropertyDataId.Setup) ?? SetupTableId;
                }
                // TODO: REMOVE ME?

                PhysicsObj = PhysicsObj.makeObject(setupTableId, Guid.Full, isDynamic);
            }
            else
            {
                PhysicsObj = new PhysicsObj();
                PhysicsObj.makeAnimObject(SetupTableId, true);
            }

            PhysicsObj.set_object_guid(Guid);

            PhysicsObj.set_weenie_obj(new WeenieObject(this));

            PhysicsObj.SetMotionTableID(MotionTableId);

            PhysicsObj.SetScaleStatic(ObjScale ?? 1.0f);

            PhysicsObj.State = defaultState;

            //if (creature != null) AllowEdgeSlide = true;

            if (BumpVelocity)
                PhysicsObj.Velocity = new Vector3(0, 0, 0.5f);
        }

        public bool AddPhysicsObj()
        {
            if (PhysicsObj.CurCell != null)
                return false;

            AdjustDungeon(Location);

            // exclude linkspots from spawning
            if (WeenieClassId == 10762) return true;

            var cell = LScape.get_landcell(Location.Cell);
            if (cell == null)
            {
                PhysicsObj.DestroyObject();
                PhysicsObj = null;
                return false;
            }

            PhysicsObj.Position.ObjCellID = cell.ID;

            var location = new Physics.Common.Position();
            location.ObjCellID = cell.ID;
            location.Frame.Origin = Location.Pos;
            location.Frame.Orientation = Location.Rotation;

            var success = PhysicsObj.enter_world(location);

            if (!success || PhysicsObj.CurCell == null)
            {
                PhysicsObj.DestroyObject();
                PhysicsObj = null;
                //Console.WriteLine($"AddPhysicsObj: failure: {Name} @ {cell.ID.ToString("X8")} - {Location.Pos} - {Location.Rotation} - SetupID: {SetupTableId.ToString("X8")}, MTableID: {MotionTableId.ToString("X8")}");
                return false;
            }

            //Console.WriteLine($"AddPhysicsObj: success: {Name} ({Guid})");
            SyncLocation();

            SetPosition(PositionType.Home, new Position(Location));

            return true;
        }

        public void SyncLocation()
        {
            Location.LandblockId = new LandblockId(PhysicsObj.Position.ObjCellID);

            // skip ObjCellID check when updating from physics
            // TODO: update to newer version of ACE.Entity.Position
            Location.PositionX = PhysicsObj.Position.Frame.Origin.X;
            Location.PositionY = PhysicsObj.Position.Frame.Origin.Y;
            Location.PositionZ = PhysicsObj.Position.Frame.Origin.Z;

            Location.Rotation = PhysicsObj.Position.Frame.Orientation;
        }

        private void InitializePropertyDictionaries()
        {
            if (Biota.PropertiesEnchantmentRegistry == null)
                Biota.PropertiesEnchantmentRegistry = new Collection<PropertiesEnchantmentRegistry>();
        }

        private void SetEphemeralValues()
        { 
            ObjectDescriptionFlags = ObjectDescriptionFlag.Attackable;

            EmoteManager = new EmoteManager(this);
            EnchantmentManager = new EnchantmentManagerWithCaching(this);

            if (Placement == null)
                Placement = ACE.Entity.Enum.Placement.Resting;

            if (MotionTableId != 0)
                CurrentMotionState = new Motion(MotionStance.Invalid);

            AwareList = null;
            NextAwarenessCheck = 0;
        }

        /// <summary>
        /// This will be true when teleporting
        /// </summary>
        public bool Teleporting { get; set; } = false;

        public bool HasGiveOrRefuseEmoteForItem(WorldObject item, out PropertiesEmote emote)
        {
            // NPC refuses this item, with a custom response
            var refuseItem = EmoteManager.GetEmoteSet(EmoteCategory.Refuse, null, null, item.WeenieClassId);
            if (refuseItem != null)
            {
                emote = refuseItem;
                return true;
            }            

            // NPC accepts this item
            var giveItem = EmoteManager.GetEmoteSet(EmoteCategory.Give, null, null, item.WeenieClassId);
            if (giveItem != null)
            {
                emote = giveItem;
                return true;
            }

            emote = null;
            return false;
        }

        /// <summary>
        /// Returns TRUE if this object has wo in VisibleTargets list
        /// </summary>
        public bool IsVisibleTarget(WorldObject wo)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            // note: VisibleTargets is only maintained for monsters and combat pets
            return PhysicsObj.ObjMaint.VisibleTargetsContainsKey(wo.PhysicsObj.ID);
        }

        //public static PhysicsObj SightObj = PhysicsObj.makeObject(0x02000124, 0, false, true);     // arrow

        /// <summary>
        /// Returns TRUE if this object has direct line-of-sight visibility to input object
        /// </summary>
        public bool IsDirectVisible(WorldObject wo, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var SightObj = PhysicsObj.makeObject(0x02000124, 0, false, true);

            SightObj.State |= PhysicsState.Missile;

            var startPos = new Physics.Common.Position(PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(wo.PhysicsObj.Position);

            if (PhysicsObj.GetBlockDist(startPos, targetPos) > 1)
                return false;

            // set to eye level
            startPos.Frame.Origin.Z += PhysicsObj.GetHeight() - SightObj.GetHeight();
            targetPos.Frame.Origin.Z += wo.PhysicsObj.GetHeight() - SightObj.GetHeight();

            var dir = Vector3.Normalize(targetPos.Frame.Origin - startPos.Frame.Origin);
            var radsum = PhysicsObj.GetPhysicsRadius() + SightObj.GetPhysicsRadius();
            startPos.Frame.Origin += dir * radsum;

            SightObj.CurCell = PhysicsObj.CurCell;
            SightObj.ProjectileTarget = wo.PhysicsObj;

            if (ethereal)
                SightObj.set_ethereal(true, false);

            // perform line of sight test
            var transition = SightObj.transition(startPos, targetPos, false);

            SightObj.DestroyObject();

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == wo.PhysicsObj.ID) != null;
            return isVisible;
        }

        public bool IsDirectVisible(Position pos, bool ethereal = false)
        {
            if (PhysicsObj == null)
                return false;

            var SightObj = PhysicsObj.makeObject(0x02000124, 0, false, true);

            SightObj.State |= PhysicsState.Missile;

            var startPos = new Physics.Common.Position(PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(pos);

            if (PhysicsObj.GetBlockDist(startPos, targetPos) > 1)
                return false;

            // set to eye level
            startPos.Frame.Origin.Z += PhysicsObj.GetHeight() - SightObj.GetHeight();
            targetPos.Frame.Origin.Z += SightObj.GetHeight();

            var dir = Vector3.Normalize(targetPos.Frame.Origin - startPos.Frame.Origin);
            var radsum = PhysicsObj.GetPhysicsRadius() + SightObj.GetPhysicsRadius();
            startPos.Frame.Origin += dir * radsum;

            SightObj.CurCell = PhysicsObj.CurCell;
            SightObj.ProjectileTarget = PhysicsObj;

            if (ethereal)
                SightObj.set_ethereal(true, false);

            // perform line of sight test
            var transition = SightObj.transition(targetPos, startPos, false);

            SightObj.DestroyObject();

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == PhysicsObj.ID) != null;
            return isVisible;
        }

        public bool IsDirectVisible(WorldObject wo, float width, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var offset = width / 2;

            if (!IsDirectVisibleWidthInternal(wo, offset, ethereal))
                return false;
            if (!IsDirectVisibleWidthInternal(wo, -offset, ethereal))
                return false;

            return true;
        }

        private bool IsDirectVisibleWidthInternal(WorldObject wo, float offset, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var SightObj = PhysicsObj.makeObject(0x02000124, 0, false, true);

            SightObj.State |= PhysicsState.Missile;

            var startPos = new Physics.Common.Position(PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(wo.PhysicsObj.Position);

            startPos.add_offset(new Vector3(0, offset, 0));
            targetPos.add_offset(new Vector3(0, offset, 0));

            if (PhysicsObj.GetBlockDist(startPos, targetPos) > 1)
                return false;

            // set to eye level
            startPos.Frame.Origin.Z += PhysicsObj.GetHeight() - SightObj.GetHeight();
            targetPos.Frame.Origin.Z += wo.PhysicsObj.GetHeight() - SightObj.GetHeight();

            var dir = Vector3.Normalize(targetPos.Frame.Origin - startPos.Frame.Origin);
            var radsum = PhysicsObj.GetPhysicsRadius() + SightObj.GetPhysicsRadius();
            startPos.Frame.Origin += dir * radsum;

            SightObj.CurCell = PhysicsObj.CurCell;
            SightObj.ProjectileTarget = wo.PhysicsObj;

            if (ethereal)
                SightObj.set_ethereal(true, false);

            // perform line of sight test
            var transition = SightObj.transition(startPos, targetPos, false);

            SightObj.DestroyObject();

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == wo.PhysicsObj.ID) != null;
            return isVisible;
        }

        public bool IsMeleeVisible(WorldObject wo, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var startPos = new Physics.Common.Position(PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(wo.PhysicsObj.Position);

            PhysicsObj.ProjectileTarget = wo.PhysicsObj;

            bool isEthereal = PhysicsObj.State.HasFlag(PhysicsState.Ethereal);
            if (ethereal && !isEthereal)
                PhysicsObj.set_ethereal(true, false);

            // perform line of sight test
            var transition = PhysicsObj.transition(startPos, targetPos, false);

            if (ethereal && !isEthereal)
                PhysicsObj.set_ethereal(false, false);

            PhysicsObj.ProjectileTarget = null;

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == wo.PhysicsObj.ID) != null;
            return isVisible;
        }

        public bool IsMeleeVisible(WorldObject wo, float width, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var offset = width / 2;

            if (!IsMeleeVisibleWidthInternal(wo, offset, ethereal))
                return false;
            if (!IsMeleeVisibleWidthInternal(wo, -offset, ethereal))
                return false;

            return true;
        }

        private bool IsMeleeVisibleWidthInternal(WorldObject wo, float offset, bool ethereal = false)
        {
            if (PhysicsObj == null || wo.PhysicsObj == null)
                return false;

            var startPos = new Physics.Common.Position(PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(wo.PhysicsObj.Position);

            startPos.add_offset(new Vector3(0, offset, 0));
            targetPos.add_offset(new Vector3(0, offset, 0));

            PhysicsObj.ProjectileTarget = wo.PhysicsObj;

            bool isEthereal = PhysicsObj.State.HasFlag(PhysicsState.Ethereal);
            if (ethereal && !isEthereal)
                PhysicsObj.set_ethereal(true, false);

            // perform line of sight test
            var transition = PhysicsObj.transition(startPos, targetPos, false);

            if (ethereal && !isEthereal)
                PhysicsObj.set_ethereal(false, false);

            PhysicsObj.ProjectileTarget = null;

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == wo.PhysicsObj.ID) != null;
            return isVisible;
        }

        public bool IsProjectileVisible(WorldObject proj)
        {
            if (!(this is Creature) || (Ethereal ?? false))
                return true;

            if (PhysicsObj == null || proj.PhysicsObj == null)
                return false;

            var startPos = new Physics.Common.Position(proj.PhysicsObj.Position);
            var targetPos = new Physics.Common.Position(PhysicsObj.Position);

            // set to eye level
            targetPos.Frame.Origin.Z += PhysicsObj.GetHeight() - proj.PhysicsObj.GetHeight();

            var prevTarget = proj.PhysicsObj.ProjectileTarget;
            proj.PhysicsObj.ProjectileTarget = PhysicsObj;

            // perform line of sight test
            var transition = proj.PhysicsObj.transition(startPos, targetPos, false);

            proj.PhysicsObj.ProjectileTarget = prevTarget;

            if (transition == null) return false;

            // check if target object was reached
            var isVisible = transition.CollisionInfo.CollideObject.FirstOrDefault(c => c.ID == PhysicsObj.ID) != null;
            return isVisible;
        }



        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************

        public MoveToState LastMoveToState { get; set; }

        public Position RequestedLocation { get; set; }

        /// <summary>
        /// Flag indicates if RequestedLocation should be broadcast to other players
        /// - For AutoPos packets, this is set to TRUE
        /// - For MoveToState packets, this is set to FALSE
        /// </summary>
        public bool RequestedLocationBroadcast { get; set; }

        ////// Logical Game Data
        public ContainerType ContainerType
        {
            get
            {
                if (WeenieType == WeenieType.Container)
                    return ContainerType.Container;
                else if (RequiresPackSlot)
                    return ContainerType.Foci;
                else
                    return ContainerType.NonContainer;
            }
        }

        public string DebugOutputString(WorldObject obj)
        {
            var sb = new StringBuilder();

            sb.AppendLine("ACE Debug Output:");
            sb.AppendLine("ACE Class File: " + GetType().Name + ".cs");
            sb.AppendLine("Guid: " + obj.Guid.Full + " (0x" + obj.Guid.Full.ToString("X") + ")");

            sb.AppendLine("----- Private Fields -----");
            foreach (var prop in obj.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).OrderBy(field => field.Name))
            {
                if (prop.GetValue(obj) == null)
                    continue;

                sb.AppendLine($"{prop.Name.Replace("<", "").Replace(">k__BackingField", "")} = {prop.GetValue(obj)}");
            }

            sb.AppendLine("----- Public Properties -----");
            foreach (var prop in obj.GetType().GetProperties().OrderBy(property => property.Name))
            {
                if (prop.GetValue(obj, null) == null)
                    continue;

                switch (prop.Name.ToLower())
                {
                    case "guid":
                        sb.AppendLine($"{prop.Name} = {obj.Guid.Full} (GuidType.{obj.Guid.Type.ToString()})");
                        break;
                    case "descriptionflags":
                        sb.AppendLine($"{prop.Name} = {ObjectDescriptionFlags.ToString()}" + " (" + (uint)ObjectDescriptionFlags + ")");
                        break;
                    case "weenieflags":
                        var weenieFlags = CalculateWeenieHeaderFlag();
                        sb.AppendLine($"{prop.Name} = {weenieFlags.ToString()}" + " (" + (uint)weenieFlags + ")");
                        break;
                    case "weenieflags2":
                        var weenieFlags2 = CalculateWeenieHeaderFlag2();
                        sb.AppendLine($"{prop.Name} = {weenieFlags2.ToString()}" + " (" + (uint)weenieFlags2 + ")");
                        break;
                    case "itemtype":
                        sb.AppendLine($"{prop.Name} = {obj.ItemType.ToString()}" + " (" + (uint)obj.ItemType + ")");
                        break;
                    case "creaturetype":
                        sb.AppendLine($"{prop.Name} = {obj.CreatureType.ToString()}" + " (" + (uint)obj.CreatureType + ")");
                        break;
                    case "containertype":
                        sb.AppendLine($"{prop.Name} = {obj.ContainerType.ToString()}" + " (" + (uint)obj.ContainerType + ")");
                        break;
                    case "usable":
                        sb.AppendLine($"{prop.Name} = {obj.ItemUseable.ToString()}" + " (" + (uint)obj.ItemUseable + ")");
                        break;
                    case "radarbehavior":
                        sb.AppendLine($"{prop.Name} = {obj.RadarBehavior.ToString()}" + " (" + (uint)obj.RadarBehavior + ")");
                        break;
                    case "physicsdescriptionflag":
                        var physicsDescriptionFlag = CalculatedPhysicsDescriptionFlag();
                        sb.AppendLine($"{prop.Name} = {physicsDescriptionFlag.ToString()}" + " (" + (uint)physicsDescriptionFlag + ")");
                        break;
                    case "physicsstate":
                        var physicsState = PhysicsObj.State;
                        sb.AppendLine($"{prop.Name} = {physicsState.ToString()}" + " (" + (uint)physicsState + ")");
                        break;
                    //case "propertiesspellid":
                    //    foreach (var item in obj.PropertiesSpellId)
                    //    {
                    //        sb.AppendLine($"PropertySpellId.{Enum.GetName(typeof(Spell), item.SpellId)} ({item.SpellId})");
                    //    }
                    //    break;
                    case "validlocations":
                        sb.AppendLine($"{prop.Name} = {obj.ValidLocations}" + " (" + (uint)obj.ValidLocations + ")");
                        break;
                    case "currentwieldedlocation":
                        sb.AppendLine($"{prop.Name} = {obj.CurrentWieldedLocation}" + " (" + (uint)obj.CurrentWieldedLocation + ")");
                        break;
                    case "priority":
                        sb.AppendLine($"{prop.Name} = {obj.ClothingPriority}" + " (" + (uint)obj.ClothingPriority + ")");
                        break;
                    case "radarcolor":
                        sb.AppendLine($"{prop.Name} = {obj.RadarColor}" + " (" + (uint)obj.RadarColor + ")");
                        break;
                    case "location":
                        sb.AppendLine($"{prop.Name} = {obj.Location.ToLOCString()}");
                        break;
                    case "destination":
                        sb.AppendLine($"{prop.Name} = {obj.Destination.ToLOCString()}");
                        break;
                    case "instantiation":
                        sb.AppendLine($"{prop.Name} = {obj.Instantiation.ToLOCString()}");
                        break;
                    case "sanctuary":
                        sb.AppendLine($"{prop.Name} = {obj.Sanctuary.ToLOCString()}");
                        break;
                    case "home":
                        sb.AppendLine($"{prop.Name} = {obj.Home.ToLOCString()}");
                        break;
                    case "portalsummonloc":
                        sb.AppendLine($"{prop.Name} = {obj.PortalSummonLoc.ToLOCString()}");
                        break;
                    case "houseboot":
                        sb.AppendLine($"{prop.Name} = {obj.HouseBoot.ToLOCString()}");
                        break;
                    case "lastoutsidedeath":
                        sb.AppendLine($"{prop.Name} = {obj.LastOutsideDeath.ToLOCString()}");
                        break;
                    case "linkedlifestone":
                        sb.AppendLine($"{prop.Name} = {obj.LinkedLifestone.ToLOCString()}");
                        break;                    
                    case "channelsactive":
                        sb.AppendLine($"{prop.Name} = {(Channel)obj.GetProperty(PropertyInt.ChannelsActive)}" + " (" + (uint)obj.GetProperty(PropertyInt.ChannelsActive) + ")");
                        break;
                    case "channelsallowed":
                        sb.AppendLine($"{prop.Name} = {(Channel)obj.GetProperty(PropertyInt.ChannelsAllowed)}" + " (" + (uint)obj.GetProperty(PropertyInt.ChannelsAllowed) + ")");
                        break;
                    case "playerkillerstatus":
                        sb.AppendLine($"{prop.Name} = {obj.PlayerKillerStatus}" + " (" + (uint)obj.PlayerKillerStatus + ")");
                        break;
                    default:
                        sb.AppendLine($"{prop.Name} = {prop.GetValue(obj, null)}");
                        break;
                }
            }

            sb.AppendLine("----- Property Dictionaries -----");

            foreach (var item in obj.GetAllPropertyBools())
                sb.AppendLine($"PropertyBool.{Enum.GetName(typeof(PropertyBool), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyDataId())
                sb.AppendLine($"PropertyDataId.{Enum.GetName(typeof(PropertyDataId), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyFloat())
                sb.AppendLine($"PropertyFloat.{Enum.GetName(typeof(PropertyFloat), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyInstanceId())
                sb.AppendLine($"PropertyInstanceId.{Enum.GetName(typeof(PropertyInstanceId), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyInt())
                sb.AppendLine($"PropertyInt.{Enum.GetName(typeof(PropertyInt), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyInt64())
                sb.AppendLine($"PropertyInt64.{Enum.GetName(typeof(PropertyInt64), item.Key)} ({(int)item.Key}) = {item.Value}");
            foreach (var item in obj.GetAllPropertyString())
                sb.AppendLine($"PropertyString.{Enum.GetName(typeof(PropertyString), item.Key)} ({(int)item.Key}) = {item.Value}");

            sb.AppendLine("\n");

            return sb.ToString().Replace("\r", "");
        }

        public void QueryHealth(Session examiner)
        {
            float healthPercentage = 1f;

            if (this is Creature creature)
                healthPercentage = (float)creature.Health.Current / creature.Health.MaxValue;

            var updateHealth = new GameEventUpdateHealth(examiner, Guid.Full, healthPercentage);
            examiner.Network.EnqueueSend(updateHealth);
        }

        public void QueryItemMana(Session examiner)
        {
            float manaPercentage = 1f;
            uint success = 0;

            if (ItemCurMana != null && ItemMaxMana != null)
            {
                manaPercentage = (float)ItemCurMana / (float)ItemMaxMana;
                success = 1;
            }

            if (success == 0) // according to retail PCAPs, if success = 0, mana = 0.
                manaPercentage = 0;

            var updateMana = new GameEventQueryItemManaResponse(examiner, Guid.Full, manaPercentage, success);
            examiner.Network.EnqueueSend(updateMana);
        }


        public void EnqueueBroadcastPhysicsState()
        {
            if (PhysicsObj != null)
            {
                if (!Visibility)
                    EnqueueBroadcast(new GameMessageSetState(this, PhysicsObj.State));
                else
                {
                    if (this is Player player && player.CloakStatus == CloakStatus.On)
                    {
                        var ps = PhysicsObj.State;
                        ps &= ~PhysicsState.Cloaked;
                        ps &= ~PhysicsState.NoDraw;
                        player.Session.Network.EnqueueSend(new GameMessageSetState(this, PhysicsObj.State));
                        EnqueueBroadcast(false, new GameMessageSetState(this, ps));
                    }
                    else
                        EnqueueBroadcast(new GameMessageSetState(this, PhysicsObj.State));
                }
            }
        }

        public void EnqueueBroadcastUpdateObject()
        {
            EnqueueBroadcast(new GameMessageUpdateObject(this));
        }

        public virtual void OnCollideObject(WorldObject target)
        {
            // thrown weapons
            if (ProjectileTarget == null) return;

            ProjectileCollisionHelper.OnCollideObject(this, target);
        }

        public virtual void OnCollideObjectEnd(WorldObject target)
        {
            // empty base
        }

        public virtual void OnCollideEnvironment()
        {
            // thrown weapons
            if (ProjectileTarget == null) return;

            ProjectileCollisionHelper.OnCollideEnvironment(this);
        }

        public void ApplyVisualEffects(PlayScript effect, float speed = 1)
        {
            if (CurrentLandblock != null)
                PlayParticleEffect(effect, Guid, speed);
        }

        // plays particle effect like spell casting or bleed etc..
        public void PlayParticleEffect(PlayScript effectId, ObjectGuid targetId, float speed = 1)
        {
            EnqueueBroadcast(new GameMessageScript(targetId, effectId, speed));
        }

        public void ApplySoundEffects(Sound sound, float volume = 1)
        {
            if (CurrentLandblock != null)
                PlaySoundEffect(sound, Guid, volume);
        }

        public void PlaySoundEffect(Sound soundId, ObjectGuid targetId, float volume = 1)
        {
            EnqueueBroadcast(new GameMessageSound(targetId, soundId, volume));
        }

        public virtual void OnGeneration(WorldObject generator)
        {
            //Console.WriteLine($"{Name}.OnGeneration()");

            EmoteManager.OnGeneration();
        }

        public virtual void BeforeEnterWorld()
        {
            ExtraItemChecks();
        }

        public virtual bool EnterWorld()
        {
            if (Location == null)
                return false;

            if (!LandblockManager.AddObject(this))
                return false;

            if (SuppressGenerateEffect != true)
                ApplyVisualEffects(PlayScript.Create);

            if (Generator != null)
                OnGeneration(Generator);

            //Console.WriteLine($"{Name}.EnterWorld()");

            return true;
        }

        // todo: This should really be an extension method for Position, or a static method within Position or even AdjustPos
        public static void AdjustDungeon(Position pos)
        {
            AdjustDungeonPos(pos);
            AdjustDungeonCells(pos);
        }

        // todo: This should really be an extension method for Position, or a static method within Position or even AdjustPos
        public static bool AdjustDungeonCells(Position pos)
        {
            if (pos == null) return false;

            var landblock = LScape.get_landblock(pos.Cell);
            if (landblock == null || !landblock.HasDungeon) return false;

            var dungeonID = pos.Cell >> 16;

            var adjustCell = AdjustCell.Get(dungeonID);
            var cellID = adjustCell.GetCell(pos.Pos);

            if (cellID != null && pos.Cell != cellID.Value)
            {
                pos.LandblockId = new LandblockId(cellID.Value);
                return true;
            }
            return false;
        }

        // todo: This should really be an extension method for Position, or a static method within Position, or even AdjustPos
        public static bool AdjustDungeonPos(Position pos)
        {
            if (pos == null) return false;

            var landblock = LScape.get_landblock(pos.Cell);
            if (landblock == null || !landblock.HasDungeon) return false;

            var dungeonID = pos.Cell >> 16;

            var adjusted = AdjustPos.Adjust(dungeonID, pos);
            return adjusted;
        }

        /// <summary>
        /// Returns a strike message based on damage type and severity
        /// </summary>
        public virtual string GetAttackMessage(Creature creature, DamageType damageType, uint amount)
        {
            var percent = (float)amount / creature.Health.Base;
            string verb = null, plural = null;
            Strings.GetAttackVerb(damageType, percent, ref verb, ref plural);
            var type = damageType.GetName().ToLower();
            return $"You {verb} {creature.Name} for {amount} points of {type} damage!";
        }

        public Dictionary<PropertyInt, int?> GetProperties(WorldObject wo)
        {
            var props = new Dictionary<PropertyInt, int?>();

            var fields = Enum.GetValues(typeof(PropertyInt)).Cast<PropertyInt>();
            foreach (var field in fields)
            {
                var prop = wo.GetProperty(field);
                props.Add(field, prop);
            }
            return props;
        }

        /// <summary>
        /// Returns the base damage for a weapon
        /// </summary>
        public virtual BaseDamage GetBaseDamage()
        {
            var maxDamage = GetProperty(PropertyInt.Damage) ?? 0;
            var variance = GetProperty(PropertyFloat.DamageVariance) ?? 0;

            return new BaseDamage(maxDamage, (float)variance);
        }

        /// <summary>
        /// Returns the modified damage for a weapon,
        /// with the wielder enchantments taken into account
        /// </summary>
        public BaseDamageMod GetDamageMod(Creature wielder, WorldObject weapon = null)
        {
            var baseDamage = GetBaseDamage();

            if (weapon == null)
                weapon = wielder.GetEquippedWeapon();

            var baseDamageMod = new BaseDamageMod(baseDamage, wielder, weapon);

            return baseDamageMod;
        }

        public bool IsDestroyed { get; private set; }

        /// <summary>
        /// If this is a container or a creature, all of the inventory and/or equipped objects will also be destroyed.<para />
        /// An object should only be destroyed once.
        /// </summary>
        public void Destroy(bool raiseNotifyOfDestructionEvent = true, bool fromLandblockUnload = false)
        {
            if (IsDestroyed)
            {
                //log.WarnFormat("Item 0x{0:X8}:{1} called destroy more than once.", Guid.Full, Name);
                return;
            }

            IsDestroyed = true;

            ReleasedTimestamp = Time.GetUnixTime();

            if (this is Container container)
            {
                foreach (var item in container.Inventory.Values)
                    item.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);
            }

            if (this is Creature creature)
            {
                foreach (var item in creature.EquippedObjects.Values)
                    item.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);

                foreach (var objInfo in creature.DeployedObjects)
                {
                    var obj = objInfo.TryGetWorldObject();
                    if (obj != null)
                    {
                        obj.Generator = null;
                        if (obj is Container contObj)
                            contObj.StartContainerDecay();
                    }
                }
            }

            if (this is Pet pet)
            {
                if (pet.P_PetOwner?.CurrentActivePet == this)
                    pet.P_PetOwner.CurrentActivePet = null;

                if (pet.P_PetDevice?.Pet == Guid.Full)
                    pet.P_PetDevice.Pet = null;
            }

            if (this is Vendor vendor)
            {
                foreach (var wo in vendor.DefaultItemsForSale.Values)
                    wo.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);

                foreach (var wo in vendor.UniqueItemsForSale.Values)
                    wo.Destroy(raiseNotifyOfDestructionEvent, fromLandblockUnload);
            }

            if (!fromLandblockUnload)
            {
                if (this is House house && house.SlumLord != null && !house.SlumLord.IsDestroyed)
                    HouseManager.DoHandleHouseRemoval(Guid.Full);

                if (this is SlumLord slumlord && slumlord.House != null && !slumlord.House.IsDestroyed)
                    HouseManager.DoHandleHouseRemoval(slumlord.House.Guid.Full);
            }

            if (raiseNotifyOfDestructionEvent)
                NotifyOfEvent(RegenerationType.Destruction);

            if (IsGenerator)
            {
                if (fromLandblockUnload)
                    ProcessGeneratorDestructionDirective(GeneratorDestruct.Destroy, fromLandblockUnload);
                else
                    OnGeneratorDestroy();
            }

            CurrentLandblock?.RemoveWorldObject(Guid);

            RemoveBiotaFromDatabase();

            if (Guid.IsDynamic())
                GuidManager.RecycleDynamicGuid(Guid);
        }

        public void FadeOutAndDestroy(bool raiseNotifyOfDestructionEvent = true)
        {
            EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.Destroy));

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(1.0f);
            actionChain.AddAction(this, () => Destroy(raiseNotifyOfDestructionEvent));
            actionChain.EnqueueChain();
        }

        public string GetPluralName()
        {
            var pluralName = PluralName;

            if (pluralName == null)
                pluralName = Name.Pluralize();

            return pluralName;
        }

        /// <summary>
        /// Returns TRUE if this object has non-cyclic animations in progress
        /// </summary>
        public bool IsAnimating { get => PhysicsObj != null && PhysicsObj.IsAnimating; }

        /// <summary>
        /// Executes a motion/animation for this object
        /// adds to the physics animation system, and broadcasts to nearby players
        /// </summary>
        /// <returns>The amount it takes to execute the motion</returns>
        public float ExecuteMotion(Motion motion, bool sendClient = true, float? maxRange = null, bool persist = false)
        {
            var motionCommand = motion.MotionState.ForwardCommand;

            if (motionCommand == MotionCommand.Ready)
                motionCommand = (MotionCommand)motion.Stance;

            // run motion command on server through physics animation system
            if (PhysicsObj != null && motionCommand != MotionCommand.Ready)
            {
                var motionInterp = PhysicsObj.get_minterp();

                var rawState = new Physics.Animation.RawMotionState();
                rawState.ForwardCommand = 0;    // always 0? must be this for monster sleep animations (skeletons, golems)
                                                // else the monster will immediately wake back up..
                rawState.CurrentHoldKey = HoldKey.Run;
                rawState.CurrentStyle = (uint)motionCommand;

                if (!PhysicsObj.IsMovingOrAnimating)
                    //PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime - PhysicsGlobals.MinQuantum;
                    PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

                motionInterp.RawState = rawState;
                motionInterp.apply_raw_movement(true, true);
            }

            if (persist && PropertyManager.GetBool("persist_movement").Item)
                motion.Persist(CurrentMotionState);

            // hardcoded ready?
            var animLength = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, CurrentMotionState.MotionState.ForwardCommand, motionCommand);
            CurrentMotionState = motion;

            // broadcast to nearby players
            if (sendClient)
                EnqueueBroadcastMotion(motion, maxRange, false);

            return animLength;
        }

        public float ExecuteMotionPersist(Motion motion, bool sendClient = true, float? maxRange = null)
        {
            return ExecuteMotion(motion, sendClient, maxRange, true);
        }

        public void SetStance(MotionStance stance, bool broadcast = true)
        {
            var motion = new Motion(stance);

            if (PropertyManager.GetBool("persist_movement").Item)
                motion.Persist(CurrentMotionState);

            CurrentMotionState = motion;

            if (broadcast)
                EnqueueBroadcastMotion(CurrentMotionState);
        }

        /// <summary>
        /// Returns the relative direction of this creature in relation to target
        /// expressed as a quadrant: Front/Back, Left/Right
        /// </summary>
        public Quadrant GetRelativeDir(WorldObject target)
        {
            var sourcePos = new Vector3(Location.PositionX, Location.PositionY, 0);
            var targetPos = new Vector3(target.Location.PositionX, target.Location.PositionY, 0);
            var targetDir = new AFrame(target.Location.Pos, target.Location.Rotation).get_vector_heading();

            targetDir.Z = 0;
            targetDir = Vector3.Normalize(targetDir);

            var sourceToTarget = Vector3.Normalize(sourcePos - targetPos);

            var dir = Vector3.Dot(sourceToTarget, targetDir);
            var angle = Vector3.Cross(sourceToTarget, targetDir);

            var quadrant = angle.Z <= 0 ? Quadrant.Left : Quadrant.Right;

            quadrant |= dir >= 0 ? Quadrant.Front : Quadrant.Back;

            return quadrant;
        }

        /// <summary>
        /// Returns TRUE if this WorldObject is a generic linkspot
        /// Linkspots are used for things like Houses,
        /// where the portal destination should be populated at runtime.
        /// </summary>
        public bool IsLinkSpot => WeenieType == WeenieType.Generic && WeenieClassName.Equals("portaldestination");

        public const float LocalBroadcastRange = 96.0f;
        public const float LocalBroadcastRangeSq = LocalBroadcastRange * LocalBroadcastRange;

        public SetPosition ScatterPos { get; set; }

        public DestinationType DestinationType;
        private Landblock currentLandblock;

        public Skill ConvertToMoASkill(Skill skill)
        {
            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                switch (skill)
                {
                    case Skill.Mace: return Skill.Axe;
                    case Skill.Staff: return Skill.Spear;
                    case Skill.Crossbow: return Skill.Bow;
                    default: return skill;
                }
            }
            else if (ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                return skill;
            else
            {
                if (this is Player player)
                {
                    if (SkillExtensions.RetiredMelee.Contains(skill))
                        return player.GetHighestMeleeSkill();
                    if (SkillExtensions.RetiredMissile.Contains(skill))
                        return Skill.MissileWeapons;
                }

                return skill;
            }
        }

        public void GetCurrentMotionState(out MotionStance currentStance, out MotionCommand currentMotion)
        {
            currentStance = MotionStance.Invalid;
            currentMotion = MotionCommand.Ready;

            if (CurrentMotionState != null)
            {
                currentStance = CurrentMotionState.Stance;

                if (CurrentMotionState.MotionState != null)
                    currentMotion = CurrentMotionState.MotionState.ForwardCommand;
            }
        }

        public virtual void OnMotionDone(uint motionID, bool success)
        {
            // empty base
        }

        public virtual void OnMoveComplete(WeenieError status)
        {
            // empty base
        }

        public bool IsTradeNote => ItemType == ItemType.PromissoryNote;

        public virtual bool IsAttunedOrContainsAttuned => Attuned >= AttunedStatus.Attuned;

        public virtual bool IsStickyAttunedOrContainsStickyAttuned => Attuned >= AttunedStatus.Sticky;

        public virtual bool IsUniqueOrContainsUnique => Unique != null;

        public virtual List<WorldObject> GetUniqueObjects()
        {
            if (Unique == null)
                return new List<WorldObject>();
            else
                return new List<WorldObject>() { this };
        }

        public bool HasArmorLevel()
        {
            return ArmorLevel > 0 || IsClothArmor;
        }

        public virtual bool IsBeingTradedOrContainsItemBeingTraded(HashSet<ObjectGuid> guidList) => guidList.Contains(Guid);

        public bool IsSocietyArmor => WieldSkillType >= (int)PropertyInt.SocietyRankCelhan && WieldSkillType <= (int)PropertyInt.SocietyRankRadblo;

        public int StructureUnitValue
        {
            get
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);
                var weenieValue = weenie?.GetValue() ?? 0;
                var weenieMaxStructure = weenie?.GetMaxStructure() ?? 1;

                var structureUnitValue = weenieValue / weenieMaxStructure;

                return Math.Max(0, structureUnitValue);
            }
        }

        public bool CanHaveExtraSpells
        {
            get
            {
                if (ExtraSpellsMaxOverride.HasValue && ExtraSpellsMaxOverride > 0)
                    return true;
                else
                    return ItemWorkmanship > 0 && (ItemType & (ItemType.WeaponOrCaster | ItemType.Vestements | ItemType.Jewelry)) != 0;
            }
        }

        public int MaxExtraSpellsCount
        {
            get
            {
                if (ExtraSpellsMaxOverride.HasValue)
                    return Math.Max(ExtraSpellsMaxOverride.Value, 0);

                var baseSlots = (int)Math.Floor((ItemWorkmanship ?? 0) / 2f);
                if (IsRobe)
                    return baseSlots == 0 ? 1 : (baseSlots * 2);
                return baseSlots;
            }
        }

        public bool CanBeTinkered
        {
            get
            {
                if (TinkerMaxCountOverride.HasValue && TinkerMaxCountOverride > 0)
                    return true;
                else if (ItemWorkmanship > 0 && (ItemType & (ItemType.WeaponOrCaster | ItemType.Vestements | ItemType.Jewelry)) != 0)
                    return true;
                else
                    return false;
            }
        }

        public int MaxTinkerCount
        {
            get
            {
                if (TinkerMaxCountOverride.HasValue)
                    return Math.Max(TinkerMaxCountOverride.Value, 0);
                else if (ItemWorkmanship > 0 && (ItemType & (ItemType.WeaponOrCaster | ItemType.Vestements | ItemType.Jewelry)) != 0)
                {
                    var workmanship = ItemWorkmanship ?? 0;
                    var maxTinkerCount = (int)Math.Floor(workmanship / 3.1f) + 1;
                    if (HasArmorLevel())
                        maxTinkerCount += Math.Min(workmanship - 1, 2);
                    return maxTinkerCount;
                }
                else
                    return 0;
            }
        }

        public int MinSalvageQualityForTinkering
        {
            get
            {
                return (int)Math.Floor((TinkerWorkmanshipOverride ?? ItemWorkmanship ?? 0) / 3.5f) * 4;
            }
        }

        public bool HasSpells
        {
            get
            {
                return SpellDID.HasValue || ProcSpell.HasValue || (Biota != null && Biota.PropertiesSpellBook != null && Biota.PropertiesSpellBook.Count > 0);
            }
        }
        public double GetHighestTierAroundObject(float maxDistance)
        {
            double? maxTier = null;

            if (CurrentLandblock == null)
                return 0;

            var landblockId = CurrentLandblock.Id.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
            foreach (var instance in instances)
            {
                Position instancePos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                if (Location.DistanceTo(instancePos) < maxDistance)
                {
                    var weenie = DatabaseManager.World.GetCachedWeenie(instance.WeenieClassId);

                    if (weenie == null)
                        continue;

                    if (weenie.WeenieType == WeenieType.Creature)
                    {
                        var playerKillerStatus = (PlayerKillerStatus?)weenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                        var npcLooksLikeObject = weenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                        if (playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                        {
                            var level = weenie.GetProperty(PropertyInt.Level) ?? 1;
                            var tier = Creature.CalculateExtendedTier(level);
                            if (tier > (maxTier ?? 0))
                                maxTier = tier;
                        }
                    }
                    else
                    {
                        var deathTreasureId = weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                        if (deathTreasureId != 0)
                        {
                            var deathTreasure = LootGenerationFactory.GetTweakedDeathTreasureProfile(deathTreasureId, this);
                            if (deathTreasure.Tier > (maxTier ?? 0))
                                maxTier = deathTreasure.Tier;
                        }
                    }
                }
            }

            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);
            foreach (var encounter in encounters)
            {
                var xPos = Math.Clamp((encounter.CellX * 24.0f) + 12.0f, 0.5f, 191.5f);
                var yPos = Math.Clamp((encounter.CellY * 24.0f) + 12.0f, 0.5f, 191.5f);

                var pos = new Physics.Common.Position();
                pos.ObjCellID = (uint)(CurrentLandblock.Id.Landblock << 16) | 1;
                pos.Frame = new Physics.Animation.AFrame(new Vector3(xPos, yPos, 0), Quaternion.Identity);
                pos.adjust_to_outside();
                pos.Frame.Origin.Z = CurrentLandblock.PhysicsLandblock.GetZ(pos.Frame.Origin);

                Position encounterPos = new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation);
                if (Location.DistanceTo(encounterPos) < maxDistance)
                {
                    var weenie = DatabaseManager.World.GetCachedWeenie(encounter.WeenieClassId);

                    if (weenie == null)
                        continue;

                    foreach (var generatorEntry in weenie.PropertiesGenerator)
                    {
                        var generatedWeenie = DatabaseManager.World.GetCachedWeenie(generatorEntry.WeenieClassId);

                        if (generatedWeenie == null)
                            continue;

                        if (generatedWeenie.WeenieType == WeenieType.Creature)
                        {
                            var level = generatedWeenie.GetProperty(PropertyInt.Level) ?? 1;
                            var tier = Creature.CalculateExtendedTier(level);
                            if (tier > (maxTier ?? 0))
                            {
                                maxTier = tier;
                                break;
                            }
                        }
                        else
                        {
                            var deathTreasureId = generatedWeenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                            if (deathTreasureId != 0)
                            {
                                var deathTreasure = LootGenerationFactory.GetTweakedDeathTreasureProfile(deathTreasureId, this);
                                if (deathTreasure.Tier > (maxTier ?? 0))
                                {
                                    maxTier = deathTreasure.Tier;
                                    break;
                                }
                            }
                        }
                    }

                    if (maxTier.HasValue)
                        break;
                }
            }

            return maxTier ?? 0;
        }

        public bool VerifyGameplayMode(WorldObject item1 = null, WorldObject item2 = null)
        {
            if (item1 != null)
            {
                if (GameplayMode == GameplayModes.Limbo && item1.GameplayMode != GameplayModes.Limbo)
                    return false;
                else if (GameplayMode > item1.GameplayMode || (GameplayMode == GameplayModes.SoloSelfFound && item1.IsHardcore))
                    return false;
                else if (item1.GameplayModeExtraIdentifier != 0 && GameplayModeExtraIdentifier != item1.GameplayModeExtraIdentifier)
                    return false;
            }
            if (item2 != null)
            {
                if (GameplayMode == GameplayModes.Limbo && item2.GameplayMode != GameplayModes.Limbo)
                    return false;
                else if (GameplayMode > item2.GameplayMode || (GameplayMode == GameplayModes.SoloSelfFound && item2.IsHardcore))
                    return false;
                else if (item2.GameplayModeExtraIdentifier != 0 && GameplayModeExtraIdentifier != item2.GameplayModeExtraIdentifier)
                    return false;
            }
            return true;
        }

        public void UpdateGameplayMode(Container owner)
        {
            if (owner == null ||( owner.GameplayMode == GameplayModes.Limbo && GameplayMode != GameplayModes.InitialMode))
                return;

            if (GameplayMode > owner.GameplayMode && (owner.GameplayMode != GameplayModes.SoloSelfFound || GameplayMode == GameplayModes.InitialMode))
            {
                GameplayMode = owner.GameplayMode;
                GameplayModeExtraIdentifier = owner.GameplayModeExtraIdentifier;
                GameplayModeIdentifierString = owner.GameplayModeIdentifierString;
            }
        }

        public uint GetGameplayModeIconOverlayId(GameplayModes gameplayMode)
        {
            switch (gameplayMode)
            {
                default:
                case GameplayModes.Regular: return 0;
                case GameplayModes.InitialMode: return 0x06020017;
                case GameplayModes.HardcoreNPK: return 0x06020012;
                case GameplayModes.HardcorePK: return 0x06020011;
                case GameplayModes.SoloSelfFound: return 0x06020013;
            }
        }

        public bool IsGameplayOverlay(uint overlayId)
        {
            if (WeenieType == WeenieType.Creature && !Guid.IsPlayer())
                return false;

            switch (overlayId)
            {
                default:
                    return false;
                case 0:
                case 0x06020011:
                case 0x06020012:
                case 0x06020013:
                case 0x06020014:
                case 0x06020015:
                case 0x06020016:
                case 0x06020017:
                    return true;
            }
        }

        public int RollTier()
        {
            return RollTier(Tier ?? 1, this is Creature creature ? creature.GetCreatureVital(PropertyAttribute2nd.Health).MaxValue : 0);
        }

        public static int RollTier(double extendedTier, uint maxHealth = 0)
        {
            var extendedTierClamped = Math.Clamp(extendedTier, 1, 6);

            var tierLevelUpChance = extendedTierClamped % 1;
            var tierLevelUpRoll = ThreadSafeRandom.NextInterval(0);

            int tier;
            if (tierLevelUpRoll < tierLevelUpChance || maxHealth >= 1500)
                tier = (int)Math.Ceiling(extendedTierClamped);
            else
                tier = (int)Math.Floor(extendedTierClamped);

            return tier;
        }

        public bool InDungeon
        {
            get
            {
                if (CurrentLandblock == null || Location == null)
                    return false;

                return CurrentLandblock.IsDungeon || (CurrentLandblock.HasDungeon && Location.Indoors);
            }
        }

        public bool Indoors
        {
            get
            {
                if (CurrentLandblock == null || Location == null)
                    return false;

                return Location.Indoors;
            }
        }

        public bool Underground
        {
            get
            {
                if (CurrentLandblock == null || Location == null)
                    return false;

                var terrainZ = Location.GetTerrainZ();
                return Location.PositionZ + Height < terrainZ;
            }
        }

        private int EstimateItemTierFromRequirements(WieldRequirement wieldRequirements, int? wieldSkillType, int? wieldDifficulty)
        {
            var requirementEstimatedTier = 1;
            if (wieldDifficulty != null)
            {
                if (wieldRequirements == WieldRequirement.Level)
                    requirementEstimatedTier = (int)Creature.CalculateExtendedTier(wieldDifficulty ?? 0);
                else if (wieldRequirements == WieldRequirement.RawSkill)
                {
                    if (wieldSkillType == (int)Skill.Axe || wieldSkillType == (int)Skill.Dagger || wieldSkillType == (int)Skill.Spear || wieldSkillType == (int)Skill.Sword || wieldSkillType == (int)Skill.ThrownWeapon || wieldSkillType == (int)Skill.UnarmedCombat)
                    {
                        if (wieldDifficulty < 250)
                            requirementEstimatedTier = 1;
                        else if (wieldDifficulty < 300)
                            requirementEstimatedTier = 2;
                        else if (wieldDifficulty < 325)
                            requirementEstimatedTier = 3;
                        else if (wieldDifficulty < 350)
                            requirementEstimatedTier = 4;
                        else if (wieldDifficulty < 370)
                            requirementEstimatedTier = 5;
                        else
                            requirementEstimatedTier = 6;
                    }
                    else if (wieldSkillType == (int)Skill.Bow)
                    {
                        if (wieldDifficulty < 250)
                            requirementEstimatedTier = 1;
                        else if (wieldDifficulty < 270)
                            requirementEstimatedTier = 2;
                        else if (wieldDifficulty < 290)
                            requirementEstimatedTier = 3;
                        else if (wieldDifficulty < 315)
                            requirementEstimatedTier = 4;
                        else if (wieldDifficulty < 335)
                            requirementEstimatedTier = 5;
                        else
                            requirementEstimatedTier = 6;
                    }
                    else if (wieldSkillType == (int)Skill.WarMagic || wieldSkillType == (int)Skill.LifeMagic)
                    {
                        if (wieldDifficulty < 225)
                            requirementEstimatedTier = 1;
                        else if (wieldDifficulty < 245)
                            requirementEstimatedTier = 2;
                        else if (wieldDifficulty < 265)
                            requirementEstimatedTier = 3;
                        else if (wieldDifficulty < 290)
                            requirementEstimatedTier = 4;
                        else if (wieldDifficulty < 310)
                            requirementEstimatedTier = 5;
                        else
                            requirementEstimatedTier = 6;
                    }
                    else
                    {
                        if (wieldDifficulty < 180)
                            requirementEstimatedTier = 1;
                        else if (wieldDifficulty < 230)
                            requirementEstimatedTier = 2;
                        else if (wieldDifficulty < 255)
                            requirementEstimatedTier = 3;
                        else if (wieldDifficulty < 280)
                            requirementEstimatedTier = 4;
                        else if (wieldDifficulty < 300)
                            requirementEstimatedTier = 5;
                        else
                            requirementEstimatedTier = 6;
                    }
                }
            }

            return requirementEstimatedTier;
        }

        private int EstimateItemTier()
        {
            var estimatedTier = 1;
            var requirementEstimatedTier = 1;
            var arcaneEstimatedTier = 1;

            if (WieldRequirements != WieldRequirement.Invalid)
                requirementEstimatedTier = EstimateItemTierFromRequirements(WieldRequirements, WieldSkillType, WieldDifficulty);
            if (WieldRequirements2 != WieldRequirement.Invalid)
                requirementEstimatedTier = Math.Max(requirementEstimatedTier, EstimateItemTierFromRequirements(WieldRequirements2, WieldSkillType2, WieldDifficulty2));
            if (WieldRequirements3 != WieldRequirement.Invalid)
                requirementEstimatedTier = Math.Max(requirementEstimatedTier, EstimateItemTierFromRequirements(WieldRequirements3, WieldSkillType3, WieldDifficulty3));
            if (WieldRequirements4 != WieldRequirement.Invalid)
                requirementEstimatedTier = Math.Max(requirementEstimatedTier, EstimateItemTierFromRequirements(WieldRequirements4, WieldSkillType4, WieldDifficulty4));

            if (ItemSkillLimit.HasValue && ItemSkillLevelLimit.HasValue)
                requirementEstimatedTier = Math.Max(requirementEstimatedTier, EstimateItemTierFromRequirements(WieldRequirement.RawSkill, (int)ItemSkillLimit, ItemSkillLevelLimit));

            if (ItemDifficulty.HasValue)
            {
                if (ItemDifficulty <= 30)
                    arcaneEstimatedTier = 1;
                else if (ItemDifficulty <= 90)
                    arcaneEstimatedTier = 2;
                else if (ItemDifficulty <= 150)
                    arcaneEstimatedTier = 3;
                else if (ItemDifficulty <= 185)
                    arcaneEstimatedTier = 4;
                else if (ItemDifficulty <= 220)
                    arcaneEstimatedTier = 5;
                else
                    arcaneEstimatedTier = 6;
            }

            estimatedTier = Math.Max(requirementEstimatedTier, arcaneEstimatedTier);

            return estimatedTier;
        }

        public void ExtraItemChecks()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (this is Creature creature)
                {
                    // Skip checking subcontainers here as they will be checked independently when their Container.OnInitialInventoryLoadCompleted() triggers.

                    foreach (var equippedItem in creature.EquippedObjects.Values)
                    {
                        if (!(equippedItem is Container)) 
                            equippedItem.ExtraItemChecks();
                    }

                    foreach (var containedItem in creature.Inventory.Values)
                    {
                        if(!(containedItem is Container))
                            containedItem.ExtraItemChecks();
                    }
                    return;
                }

                var currentVersion = 1;

                // The following code makes sure the item fits into CustomDM's ruleset as not all database entries have been updated.
                if (Version == null || Version < currentVersion)
                {
                    // These changes are applied even to items owned by monsters, do not update item version here as that would prevent the full changes from being applied later.

                    // Convert weapon skills to merged ones
                    if (WieldSkillType.HasValue)
                        WieldSkillType = (int)ConvertToMoASkill((Skill)WieldSkillType);
                    if (WieldSkillType2.HasValue)
                        WieldSkillType2 = (int)ConvertToMoASkill((Skill)WieldSkillType2);
                    if (WieldSkillType3.HasValue)
                        WieldSkillType3 = (int)ConvertToMoASkill((Skill)WieldSkillType3);
                    if (WieldSkillType4.HasValue)
                        WieldSkillType4 = (int)ConvertToMoASkill((Skill)WieldSkillType4);
                }

                var owner = Wielder ?? Container;
                bool ownerIsMonster = owner != null && owner is Creature creatureOwner && !creatureOwner.Guid.IsPlayer() && (creatureOwner.Attackable || creatureOwner.TargetingTactic != TargetingTactic.None);
                if (ownerIsMonster)
                    return;

                if (owner is Container containerOwner)
                    UpdateGameplayMode(containerOwner);

                if (this is Container container)
                {
                    foreach (var containedItem in container.Inventory.Values)
                    {
                        containedItem.ExtraItemChecks();
                    }
                }

                if (Version == null || Version < currentVersion) // Monsters can keep unmodified items for now due to balance reasons.
                {
                    Version = currentVersion; // Bring item version up to current.

                    if (ItemWorkmanship == null && (ItemType & (ItemType.WeaponOrCaster | ItemType.Vestements | ItemType.Jewelry)) != 0 && WeenieType != WeenieType.Missile && WeenieType != WeenieType.Ammunition)
                    {
                        var estimatedTier = EstimateItemTier();

                        // Add default ExtraSpellsMaxOverride value to quest items.
                        if (ExtraSpellsMaxOverride == null && ResistMagic == null)
                        {
                            switch (estimatedTier)
                            {
                                default:
                                case 1: ExtraSpellsMaxOverride = 1; break;
                                case 2: ExtraSpellsMaxOverride = 2; break;
                                case 3: ExtraSpellsMaxOverride = 2; break;
                                case 4: ExtraSpellsMaxOverride = 2; break;
                                case 5: ExtraSpellsMaxOverride = 3; break;
                                case 6: ExtraSpellsMaxOverride = 3; break;
                            }

                            if (IsRobe)
                                ExtraSpellsMaxOverride *= 2;

                            BaseItemDifficultyOverride = ItemDifficulty;
                            BaseSpellcraftOverride = ItemSpellcraft;
                        }

                        // Add default TinkerMaxCountOverride value to quest items.
                        if (TinkerMaxCountOverride == null)
                        {
                            switch (estimatedTier)
                            {
                                default:
                                case 1: TinkerWorkmanshipOverride = 1; break;
                                case 2: TinkerWorkmanshipOverride = 4; break;
                                case 3: TinkerWorkmanshipOverride = 5; break;
                                case 4: TinkerWorkmanshipOverride = 6; break;
                                case 5: TinkerWorkmanshipOverride = 8; break;
                                case 6: TinkerWorkmanshipOverride = 10; break;
                            }

                            TinkerMaxCountOverride = 2;
                        }

                        // Remove invalid properties from items accessible by players, keep them on monster's items.
                        if (CriticalMultiplier.HasValue)
                        {
                            log.Warn($"Removed invalid CriticalMultiplier {CriticalMultiplier:0.00} from {Name}.");
                            CriticalMultiplier = null;
                        }

                        if (CriticalFrequency.HasValue)
                        {
                            log.Warn($"Removed invalid CriticalFrequency {CriticalFrequency:0.00} from {Name}.");
                            CriticalFrequency = null;
                        }

                        if (IgnoreArmor.HasValue)
                        {
                            log.Warn($"Removed invalid IgnoreArmor {IgnoreArmor:0.00} from {Name}.");
                            IgnoreArmor = null;
                        }

                        if (IgnoreShield.HasValue)
                        {
                            log.Warn($"Removed invalid IgnoreShield {IgnoreShield:0.00} from {Name}.");
                            IgnoreShield = null;
                        }

                        if (ResistanceModifier.HasValue)
                        {
                            log.Warn($"Removed invalid ResistanceModifier {ResistanceModifier:0.00} from {Name}.");
                            ResistanceModifier = null;
                        }
                        if (ResistanceModifierType.HasValue)
                        {
                            log.Warn($"Removed invalid ResistanceModifierType {ResistanceModifierType} from {Name}.");
                            ResistanceModifierType = null;
                        }
                    }

                    // Remove invalid spells from items accessible by players, keep the spells on monster's items.
                    if (SpellDID.HasValue)
                    {
                        if (SpellsToReplace.TryGetValue((SpellId)SpellDID, out var replacementId))
                        {
                            if (replacementId < 0)
                            {
                                var originalSpellId = (SpellId)SpellDID;
                                Spell originalSpell = new Spell(originalSpellId);

                                int level = Math.Clamp(Math.Abs(replacementId), 1, 8);

                                SpellId spellLevel1Id = SpellId.Undef;
                                if (this is Caster)
                                    spellLevel1Id = CasterSlotSpells.PseudoRandomRoll(this, (int)WeenieClassId);
                                else if (this is Gem)
                                    spellLevel1Id = SpellSelectionTable.PseudoRandomRoll(1, (int)WeenieClassId);

                                if (spellLevel1Id != SpellId.Undef)
                                {
                                    var spellId = SpellLevelProgression.GetSpellAtLevel(spellLevel1Id, level);

                                    SpellDID = (uint)spellId;

                                    log.Warn($"Replaced invalid spell {originalSpellId} with {spellId} as a DID spell on {Name}.");
                                }
                                else
                                    log.Warn($"Failed to replace invalid spell {originalSpellId} as a DID spell on {Name}. Unhandled item type.");
                            }
                            else if (replacementId > 0)
                            {
                                var originalSpellId = (SpellId)SpellDID;

                                SpellDID = (uint)replacementId;

                                log.Warn($"Replaced invalid spell {originalSpellId} with {(SpellId)replacementId} as a DID spell on {Name}.");
                            }
                            else
                            {
                                var originalSpellId = (SpellId)SpellDID;

                                RemoveProperty(PropertyDataId.Spell);

                                log.Warn($"Removed invalid spell {originalSpellId} as a DID spell on {Name}.");
                            }
                        }
                    }

                    if (ProcSpell.HasValue)
                    {
                        if (SpellsToReplace.TryGetValue((SpellId)ProcSpell, out var replacementId))
                        {
                            if (replacementId < 0)
                            {
                                var originalSpellId = (SpellId)ProcSpell;

                                int level = Math.Clamp(Math.Abs(replacementId), 1, 8);

                                SpellId procSpellLevel1Id = SpellId.Undef;
                                if (this is MeleeWeapon)
                                    procSpellLevel1Id = MeleeSpells.PseudoRandomRollProc((int)WeenieClassId);
                                else if (this is MissileLauncher || this is Missile)
                                    procSpellLevel1Id = MissileSpells.PseudoRandomRollProc((int)WeenieClassId);

                                if (procSpellLevel1Id != SpellId.Undef)
                                {
                                    var procSpellId = SpellLevelProgression.GetSpellAtLevel(procSpellLevel1Id, level);

                                    Spell spell = new Spell(procSpellId);
                                    ProcSpellRate = 0.15f;
                                    ProcSpell = (uint)procSpellId;
                                    ProcSpellSelfTargeted = spell.IsSelfTargeted;

                                    log.Warn($"Replaced invalid spell {originalSpellId} with {procSpellId} as a proc on {Name}.");
                                }
                                else
                                    log.Warn($"Failed to replace invalid spell {originalSpellId} as a proc spell on {Name}. Unhandled item type.");
                            }
                            else if (replacementId > 0)
                            {
                                var originalSpellId = (SpellId)ProcSpell;

                                Spell spell = new Spell(replacementId);

                                ProcSpellRate = 0.15f;
                                ProcSpell = (uint)replacementId;
                                ProcSpellSelfTargeted = spell.IsSelfTargeted;

                                log.Warn($"Replaced invalid spell {originalSpellId} with {(SpellId)replacementId} as a proc on {Name}.");
                            }
                            else
                            {
                                var originalSpellId = (SpellId)ProcSpell;

                                RemoveProperty(PropertyFloat.ProcSpellRate);
                                RemoveProperty(PropertyDataId.ProcSpell);
                                RemoveProperty(PropertyBool.ProcSpellSelfTargeted);

                                log.Warn($"Removed invalid spell {originalSpellId} as a proc on {Name}.");
                            }
                        }
                    }

                    var list = Biota.GetKnownSpellsIds(BiotaDatabaseLock);
                    foreach (var entry in list)
                    {
                        if (SpellsToReplace.TryGetValue((SpellId)entry, out var replacementId))
                        {
                            if (Biota.TryRemoveKnownSpell(entry, BiotaDatabaseLock))
                            {
                                if (replacementId < 0)
                                {
                                    log.Warn($"Failed to replace invalid spell {(SpellId)entry} as a proc spell on {Name}. Unhandled item type.");
                                }
                                else if (replacementId > 0)
                                {
                                    Biota.GetOrAddKnownSpell(replacementId, BiotaDatabaseLock, out _);
                                    log.Warn($"Replaced invalid spell {(SpellId)entry} with {(SpellId)replacementId} on {Name}.");
                                }
                                else
                                    log.Warn($"Removed invalid spell {(SpellId)entry} from {Name}.");
                            }
                        }
                    }
                }
            }
        }

        private Dictionary<SpellId, int> SpellsToReplace = new Dictionary<SpellId, int>()
        {
            // -1 means replace with a pseudorandom(based on wcid) level 1 proc and so on.
            // 0 means remove, positive values mean the spellId of the replacement spell.
            { SpellId.BloodDrinkerSelf1, 0 },
            { SpellId.BloodDrinkerSelf2, 0 },
            { SpellId.BloodDrinkerSelf3, 0 },
            { SpellId.BloodDrinkerSelf4, 0 },
            { SpellId.BloodDrinkerSelf5, 0 },
            { SpellId.BloodDrinkerSelf6, 0 },
            { SpellId.BloodDrinkerSelf7, 0 },
            { SpellId.BloodDrinkerSelf8, 0 },
            { SpellId.LightbringersWay, 0 },

            { SpellId.Discipline, 0 },
            { SpellId.WoundTwister, 0 },
            { SpellId.MurderousThirst, 0 },

            { SpellId.BloodDrinkerOther1, 0 },
            { SpellId.BloodDrinkerOther2, 0 },
            { SpellId.BloodDrinkerOther3, 0 },
            { SpellId.BloodDrinkerOther4, 0 },
            { SpellId.BloodDrinkerOther5, 0 },
            { SpellId.BloodDrinkerOther6, 0 },
            { SpellId.BloodDrinkerOther7, 0 },
            { SpellId.BloodDrinkerOther8, 0 },

            { SpellId.SwiftKillerSelf1, 0 },
            { SpellId.SwiftKillerSelf2, 0 },
            { SpellId.SwiftKillerSelf3, 0 },
            { SpellId.SwiftKillerSelf4, 0 },
            { SpellId.SwiftKillerSelf5, 0 },
            { SpellId.SwiftKillerSelf6, 0 },
            { SpellId.SwiftKillerSelf7, 0 },
            { SpellId.SwiftKillerSelf8, 0 },

            { SpellId.Alacrity, 0 },
            { SpellId.SpeedHunter, 0 },

            { SpellId.SwiftKillerOther1, 0 },
            { SpellId.SwiftKillerOther2, 0 },
            { SpellId.SwiftKillerOther3, 0 },
            { SpellId.SwiftKillerOther4, 0 },
            { SpellId.SwiftKillerOther5, 0 },
            { SpellId.SwiftKillerOther6, 0 },
            { SpellId.SwiftKillerOther7, 0 },
            { SpellId.SwiftKillerOther8, 0 },

            //{ SpellId.HeartSeekerSelf1, 0 },
            //{ SpellId.HeartSeekerSelf2, 0 },
            //{ SpellId.HeartSeekerSelf3, 0 },
            //{ SpellId.HeartSeekerSelf4, 0 },
            //{ SpellId.HeartSeekerSelf5, 0 },
            //{ SpellId.HeartSeekerSelf6, 0 },
            //{ SpellId.HeartSeekerSelf7, 0 },
            //{ SpellId.HeartSeekerSelf8, 0 },

            //{ SpellId.HeartSeekerOther1, 0 },
            //{ SpellId.HeartSeekerOther2, 0 },
            //{ SpellId.HeartSeekerOther3, 0 },
            //{ SpellId.HeartSeekerOther4, 0 },
            //{ SpellId.HeartSeekerOther5, 0 },
            //{ SpellId.HeartSeekerOther6, 0 },
            //{ SpellId.HeartSeekerOther7, 0 },
            //{ SpellId.HeartSeekerOther8, 0 },

            //{ SpellId.DefenderSelf1, 0 },
            //{ SpellId.DefenderSelf2, 0 },
            //{ SpellId.DefenderSelf3, 0 },
            //{ SpellId.DefenderSelf4, 0 },
            //{ SpellId.DefenderSelf5, 0 },
            //{ SpellId.DefenderSelf6, 0 },
            //{ SpellId.DefenderSelf7, 0 },
            //{ SpellId.DefenderSelf8, 0 },

            //{ SpellId.DefenderOther1, 0 },
            //{ SpellId.DefenderOther2, 0 },
            //{ SpellId.DefenderOther3, 0 },
            //{ SpellId.DefenderOther4, 0 },
            //{ SpellId.DefenderOther5, 0 },
            //{ SpellId.DefenderOther6, 0 },
            //{ SpellId.DefenderOther7, 0 },
            //{ SpellId.DefenderOther8, 0 },

            { SpellId.SpiritDrinkerSelf1, 0 },
            { SpellId.SpiritDrinkerSelf2, 0 },
            { SpellId.SpiritDrinkerSelf3, 0 },
            { SpellId.SpiritDrinkerSelf4, 0 },
            { SpellId.SpiritDrinkerSelf5, 0 },
            { SpellId.SpiritDrinkerSelf6, 0 },
            { SpellId.SpiritDrinkerSelf7, 0 },
            { SpellId.SpiritDrinkerSelf8, 0 },

            { SpellId.SpiritDrinkerOther1, 0 },
            { SpellId.SpiritDrinkerOther2, 0 },
            { SpellId.SpiritDrinkerOther3, 0 },
            { SpellId.SpiritDrinkerOther4, 0 },
            { SpellId.SpiritDrinkerOther5, 0 },
            { SpellId.SpiritDrinkerOther6, 0 },
            { SpellId.SpiritDrinkerOther7, 0 },
            { SpellId.SpiritDrinkerOther8, 0 },

            { SpellId.Impenetrability1, 0 },
            { SpellId.Impenetrability2, 0 },
            { SpellId.Impenetrability3, 0 },
            { SpellId.Impenetrability4, 0 },
            { SpellId.Impenetrability5, 0 },
            { SpellId.Impenetrability6, 0 },
            { SpellId.Impenetrability7, 0 },
            { SpellId.Impenetrability8, 0 },

            { SpellId.AerfallesWard, 0 },
            { SpellId.LesserSkinFiazhat, 0 },
            { SpellId.MinorSkinFiazhat, 0 },
            { SpellId.SkinFiazhat, 0 },

            { SpellId.ItemEnchantmentMasterySelf1, 0 },
            { SpellId.ItemEnchantmentMasterySelf2, 0 },
            { SpellId.ItemEnchantmentMasterySelf3, 0 },
            { SpellId.ItemEnchantmentMasterySelf4, 0 },
            { SpellId.ItemEnchantmentMasterySelf5, 0 },
            { SpellId.ItemEnchantmentMasterySelf6, 0 },
            { SpellId.ItemEnchantmentMasterySelf7, 0 },
            { SpellId.ItemEnchantmentMasterySelf8, 0 },

            { SpellId.ItemEnchantmentMasteryOther1, 0 },
            { SpellId.ItemEnchantmentMasteryOther2, 0 },
            { SpellId.ItemEnchantmentMasteryOther3, 0 },
            { SpellId.ItemEnchantmentMasteryOther4, 0 },
            { SpellId.ItemEnchantmentMasteryOther5, 0 },
            { SpellId.ItemEnchantmentMasteryOther6, 0 },
            { SpellId.ItemEnchantmentMasteryOther7, 0 },
            { SpellId.ItemEnchantmentMasteryOther8, 0 },

            { SpellId.CreatureEnchantmentMasterySelf1, 0 },
            { SpellId.CreatureEnchantmentMasterySelf2, 0 },
            { SpellId.CreatureEnchantmentMasterySelf3, 0 },
            { SpellId.CreatureEnchantmentMasterySelf4, 0 },
            { SpellId.CreatureEnchantmentMasterySelf5, 0 },
            { SpellId.CreatureEnchantmentMasterySelf6, 0 },
            { SpellId.CreatureEnchantmentMasterySelf7, 0 },
            { SpellId.CreatureEnchantmentMasterySelf8, 0 },

            { SpellId.CreatureEnchantmentMasteryOther1, 0 },
            { SpellId.CreatureEnchantmentMasteryOther2, 0 },
            { SpellId.CreatureEnchantmentMasteryOther3, 0 },
            { SpellId.CreatureEnchantmentMasteryOther4, 0 },
            { SpellId.CreatureEnchantmentMasteryOther5, 0 },
            { SpellId.CreatureEnchantmentMasteryOther6, 0 },
            { SpellId.CreatureEnchantmentMasteryOther7, 0 },
            { SpellId.CreatureEnchantmentMasteryOther8, 0 },

            { SpellId.ArmorSelf1, 0 },
            { SpellId.ArmorSelf2, 0 },
            { SpellId.ArmorSelf3, 0 },
            { SpellId.ArmorSelf4, 0 },
            { SpellId.ArmorSelf5, 0 },
            { SpellId.ArmorSelf6, 0 },
            { SpellId.ArmorSelf7, 0 },
            { SpellId.ArmorSelf8, 0 },

            { SpellId.ArmorOther1, (int)SpellId.ArmorMasteryOther1 },
            { SpellId.ArmorOther2, (int)SpellId.ArmorMasteryOther2 },
            { SpellId.ArmorOther3, (int)SpellId.ArmorMasteryOther3 },
            { SpellId.ArmorOther4, (int)SpellId.ArmorMasteryOther4 },
            { SpellId.ArmorOther5, (int)SpellId.ArmorMasteryOther5 },
            { SpellId.ArmorOther6, (int)SpellId.ArmorMasteryOther6 },
            { SpellId.ArmorOther7, (int)SpellId.ArmorMasteryOther7 },
            { SpellId.ArmorOther8, (int)SpellId.ArmorMasteryOther8 },

            { SpellId.ImperilOther1, 0 },
            { SpellId.ImperilOther2, 0 },
            { SpellId.ImperilOther3, 0 },
            { SpellId.ImperilOther4, 0 },
            { SpellId.ImperilOther5, 0 },
            { SpellId.ImperilOther6, 0 },
            { SpellId.ImperilOther7, 0 },
            { SpellId.ImperilOther8, 0 },

            { SpellId.ImperilSelf1, 0 },
            { SpellId.ImperilSelf2, 0 },
            { SpellId.ImperilSelf3, 0 },
            { SpellId.ImperilSelf4, 0 },
            { SpellId.ImperilSelf5, 0 },
            { SpellId.ImperilSelf6, 0 },
            { SpellId.ImperilSelf7, 0 },
            { SpellId.ImperilSelf8, 0 },

            { SpellId.ForceArmor, 0 },
            { SpellId.PanoplyQueenslayer, 0 },
            { SpellId.TuskerHideLesser, 0 },
            { SpellId.TuskerHide, 0 },
            { SpellId.LesserMistsBur, 0 },
            { SpellId.MinorMistsBur, 0 },
            { SpellId.MistsBur, 0 },
            { SpellId.ArmorSelfAegisGoldenFlame, 0 },
            { SpellId.KukuurHide, 0 },
            { SpellId.DrudgeArmor, 0 },
            { SpellId.FrozenArmor, 0 },
            { SpellId.ArmorProdigalHarbinger, 0 },
            { SpellId.BaelzharonArmorOther, 0 },
        };
    }
}
