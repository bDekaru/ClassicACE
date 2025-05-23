using System;
using System.Collections.Generic;

using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;

namespace ACE.Server.WorldObjects
{
    public class SlumLord : Container
    {
        /// <summary>
        /// The house this slumlord is linked to
        /// </summary>
        public House House { get => ParentLink as House; }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public SlumLord(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public SlumLord(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            ItemCapacity = 120;
        }

        public bool HouseRequiresMonarch
        {
            get => GetProperty(PropertyBool.HouseRequiresMonarch) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.HouseRequiresMonarch); else SetProperty(PropertyBool.HouseRequiresMonarch, value); }
        }

        public int? AllegianceMinLevel
        {
            get => GetProperty(PropertyInt.AllegianceMinLevel);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.AllegianceMinLevel); else SetProperty(PropertyInt.AllegianceMinLevel, value.Value); }
        }

        public override void ActOnUse(WorldObject worldObject)
        {
            //Console.WriteLine($"SlumLord.ActOnUse({worldObject.Name})");

            var player = worldObject as Player;
            if (player == null) return;

            if (House != null)
            {
                if (House.HouseStatus == HouseStatus.Disabled)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"This {(House.HouseType == HouseType.Undef ? "house" : Name.ToString().ToLower())} is {(House.HouseStatus == HouseStatus.Disabled ? "not " : "")}available for purchase.", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This house is not properly configured. Please report this issue.", ChatMessageType.Broadcast));
                return;
            }

            // sent house profile
            var houseProfile = GetHouseProfile();

            player.Session.Network.EnqueueSend(new GameEventHouseProfile(player.Session, Guid, houseProfile));
        }

        public HouseProfile GetHouseProfile()
        {
            var houseProfile = new HouseProfile();

            houseProfile.DwellingID = HouseId.Value;

            if (House != null)
            {
                houseProfile.Type = House.HouseType;

                if (House.HouseStatus == HouseStatus.Disabled)
                    houseProfile.Bitmask &= ~HouseBitfield.Active;

                if (House.HouseStatus == HouseStatus.InActive)
                    houseProfile.MaintenanceFree = true;
            }

            if (HouseRequiresMonarch)
                houseProfile.Bitmask |= HouseBitfield.RequiresMonarch;

            if (MinLevel != null)
                houseProfile.MinLevel = MinLevel.Value;

            if (AllegianceMinLevel != null)
                houseProfile.MinAllegRank = AllegianceMinLevel.Value;

            if (HouseOwner != null)
            {
                var ownerId = HouseOwner.Value;
                var owner = PlayerManager.FindByGuid(ownerId);

                houseProfile.OwnerID = new ObjectGuid(ownerId);
                houseProfile.OwnerName = owner?.Name;
            }

            houseProfile.SetBuyItems(GetBuyItems());
            houseProfile.SetRentItems(GetRentItems());
            houseProfile.SetPaidItems(this);

            return houseProfile;
        }

        /// <summary>
        /// Returns the list of items required to purchase this dwelling
        /// </summary>
        public List<WorldObject> GetBuyItems()
        {
            var buyList = GetCreateListForSlumLord(DestinationType.HouseBuy);

            buyList.ForEach(item =>
            {
                if (House != null && item.StackSize.HasValue)
                    item.StackSize = (int)Math.Round(item.StackSize.Value * House.GetPriceMultiplier());
                item.Destroy(false);
            });

            return buyList;
        }

        /// <summary>
        /// Returns the list of items required to rent this dwelling
        /// </summary>
        public List<WorldObject> GetRentItems()
        {
            var rentList = GetCreateListForSlumLord(DestinationType.HouseRent);

            rentList.ForEach(item =>
            {
                if (House != null && item.StackSize.HasValue)
                    item.StackSize = (int)Math.Round(item.StackSize.Value * House.GetPriceMultiplier());
                item.Destroy(false);
            });

            return rentList;
        }

        /// <summary>
        /// Returns TRUE if rent is already paid for current maintenance period
        /// </summary>
        public bool IsRentPaid()
        {
            if (House != null && House.HouseStatus == HouseStatus.InActive)
                return true;

            var houseProfile = GetHouseProfile();

            foreach (var rentItem in houseProfile.Rent)
            {
                if (rentItem.Paid < rentItem.Num)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns TRUE if this player has the minimum requirements to purchase / rent this house
        /// </summary>
        public bool HasRequirements(Player player)
        {
            if (!PropertyManager.GetBool("house_purchase_requirements").Item)
                return true;

            if (AllegianceMinLevel == null)
                return true;

            var allegianceMinLevel = PropertyManager.GetLong("mansion_min_rank", -1).Item;
            if (allegianceMinLevel == -1)
                allegianceMinLevel = AllegianceMinLevel.Value;

            if (allegianceMinLevel > 0 && (player.Allegiance == null || player.AllegianceNode.Rank < allegianceMinLevel))
            {
                Console.WriteLine($"{Name}.HasRequirements({player.Name}) - allegiance rank {player.AllegianceNode?.Rank ?? 0} < {allegianceMinLevel}");
                return false;
            }
            return true;
        }

        public int GetAllegianceMinLevel()
        {
            if (AllegianceMinLevel == null)
                return 0;

            var allegianceMinLevel = PropertyManager.GetLong("mansion_min_rank", -1).Item;
            if (allegianceMinLevel == -1)
                allegianceMinLevel = AllegianceMinLevel.Value;

            return (int)allegianceMinLevel;
        }

        protected override void OnInitialInventoryLoadCompleted()
        {
            base.OnInitialInventoryLoadCompleted();

            HouseManager.OnInitialInventoryLoadCompleted(this);

            DetermineTier();
        }

        public void DetermineTier()
        {
            if (House != null && House.IsCustomHouse)
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);

                var minLevel = weenie.GetProperty(PropertyInt.MinLevel) ?? 0;
                if (minLevel != 0)
                    MinLevel = Math.Min((int)Player.GetMaxLevel(), (int)Math.Round(minLevel + House.GeteAdditionalLevelRequirement()));

                var allegianceMinLevel = weenie.GetProperty(PropertyInt.AllegianceMinLevel) ?? 0;
                if (allegianceMinLevel != 0)
                    AllegianceMinLevel = Math.Min(10, (int)Math.Round(allegianceMinLevel + House.GetAdditionalAllegianceLevelRequirement()));
            }
        }

        public void On()
        {
            var on = new Motion(MotionStance.Invalid, MotionCommand.On);

            SetAndBroadcastMotion(on);
        }

        public void Off()
        {
            var off = new Motion(MotionStance.Invalid, MotionCommand.Off);

            if (CurrentLandblock != null)
                SetAndBroadcastMotion(off);
        }

        private void SetAndBroadcastMotion(Motion motion)
        {
            CurrentMotionState = motion;
            EnqueueBroadcastMotion(motion);
        }

        public void SetAndBroadcastName(string houseOwnerName = null)
        {
            if (string.IsNullOrWhiteSpace(houseOwnerName))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(WeenieClassId);

                if (weenie != null)
                    Name = weenie.GetProperty(PropertyString.Name);
                else
                    Name = House.HouseType.ToString();
            }
            else
                Name = $"{houseOwnerName}'s {Name}";

            if (CurrentLandblock != null)
            {
                //EnqueueBroadcast(new GameMessagePublicUpdatePropertyString(this, PropertyString.Name, Name)); // This does not cause the client to update the object's name.

                var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
                var delay = motionTable.GetAnimationLength(MotionCommand.On);

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(delay); // Add a delay here so we do not skip the on/off animation.
                actionChain.AddAction(this, () => EnqueueBroadcast(new GameMessageUpdateObject(this)));
                actionChain.EnqueueChain();
            }

        }

        /// <summary>
        /// This event is raised when HouseManager removes item for rent
        /// </summary>
        protected override void OnRemoveItem(WorldObject removedItem)
        {
            //Console.WriteLine("Slumlord.OnRemoveItem()");

            // Here we explicitly remove the payment from the database to avoid storing unneeded objects and free guid.
            if (!removedItem.IsDestroyed)
                removedItem.Destroy();
        }
    }
}
