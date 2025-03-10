using System;
using System.Numerics;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    public sealed class HousePortal : Portal
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public House House => ParentLink as House;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public HousePortal(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public HousePortal(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        public override void SetLinkProperties(WorldObject wo)
        {
            if (House == null)
            {
                log.Warn($"[HOUSE] HousePortal.SetLinkProperties({(wo != null ? $"{wo.Name}:0x{wo.Guid}:{wo.WeenieClassId}" : "null")}): House is null for HousePortal 0x{Guid} at {Location.ToLOCString()}");
                return;
            }

            if (wo == null)
            {
                log.Warn($"[HOUSE] HousePortal.SetLinkProperties(null): WorldObject is null for HousePortal 0x{Guid} at {Location.ToLOCString()} | {(House != null ? $"House = {House.Name}:0x{House.Guid}:{House.WeenieClassId}" : "House is null")}");
                return;
            }

            // get properties from parent?
            wo.HouseId = House.HouseId;
            wo.HouseOwner = House.HouseOwner;
            wo.HouseInstance = House.HouseInstance;

            if (wo.IsLinkSpot)
            {
                var housePortals = House.GetHousePortals();
                if (housePortals.Count == 0)
                {
                    Console.WriteLine($"{Name}.SetLinkProperties({wo.Name}): found LinkSpot, but empty HousePortals");
                    return;
                }
                var i = housePortals[0];

                if (i.ObjCellId == Location.Cell)
                {
                    if (housePortals.Count > 1)
                        i = housePortals[1];
                    else
                    { // there are some houses that for some reason, don't have return locations, so we'll fake the entry with a reference to the root house portal location mimicking other database entries.
                        var rootHouse = House.RootHouse;
                        if (rootHouse == null)
                        {
                            log.Warn($"[HOUSE] HousePortal.SetLinkProperties({(wo != null ? $"{wo.Name}:0x{wo.Guid}:{wo.WeenieClassId}" : "null")}): House.RootHouse is null for HousePortal 0x{Guid} at {Location.ToLOCString()}");
                            return;
                        }
                        i = new Database.Models.World.HousePortal { ObjCellId = rootHouse.HousePortal.Location.Cell,
                                                                      OriginX = rootHouse.HousePortal.Location.PositionX,
                                                                      OriginY = rootHouse.HousePortal.Location.PositionY,
                                                                      OriginZ = rootHouse.HousePortal.Location.PositionZ,
                                                                      AnglesX = rootHouse.HousePortal.Location.RotationX,
                                                                      AnglesY = rootHouse.HousePortal.Location.RotationY,
                                                                      AnglesZ = rootHouse.HousePortal.Location.RotationZ,
                                                                      AnglesW = rootHouse.HousePortal.Location.RotationW };
                    }
                }

                var destination = new Position(i.ObjCellId, new Vector3(i.OriginX, i.OriginY, i.OriginZ), new Quaternion(i.AnglesX, i.AnglesY, i.AnglesZ, i.AnglesW));

                wo.SetPosition(PositionType.Destination, destination);

                // set portal destination directly?
                SetPosition(PositionType.Destination, destination);
            }
        }

        public override ActivationResult CheckUseRequirements(WorldObject activator, bool silent = false)
        {
            var rootHouse = House?.RootHouse;

            if (activator == null || rootHouse == null)
            {
                log.Warn($"HousePortal.CheckUseRequirements: 0x{Guid} - {Location.ToLOCString()}");
                log.Warn($"HousePortal.CheckUseRequirements: activator is null - {activator == null} | House is null - {House == null} | RootHouse is null - {rootHouse == null}");
                return new ActivationResult(false);
            }

            if (!(activator is Player player))
                return new ActivationResult(false);

            if (player.IsOlthoiPlayer)
                return silent ? new ActivationResult(false) : new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.OlthoiMayNotUsePortal));

            if (player.CurrentLandblock.IsDungeon && Destination.LandblockId != player.CurrentLandblock.Id)
                return new ActivationResult(true);   // allow escape to overworld always

            if (player.IgnorePortalRestrictions)
                return new ActivationResult(true);

            var houseOwner = rootHouse.HouseOwner;

            if (houseOwner == null)
                //return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.YouMustBeHouseGuestToUsePortal));
                return new ActivationResult(true);

            if (rootHouse.OpenToEveryone)
                return new ActivationResult(true);

            if (!rootHouse.HasPermission(player))
                return silent ? new ActivationResult(false) : new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.YouMustBeHouseGuestToUsePortal));

            return new ActivationResult(true);
        }

        /// <summary>
        /// House Portals are on Use activated, rather than collision based activation
        /// The actual portal process is wrapped to the base portal class ActOnUse, after ACL check are performed
        /// </summary>
        /// <param name="worldObject"></param>
        public override void ActOnUse(WorldObject worldObject)
        {
            // if house portal in dungeon,
            // set destination to outdoor house slumlord
            if (CurrentLandblock != null && CurrentLandblock.IsDungeon && Destination.LandblockId == CurrentLandblock.Id)
                SetPosition(PositionType.Destination, House.GetRecallDestination());

            base.ActOnUse(worldObject);
        }
    }
}
