using System;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Network.GameEvent.Events;

namespace ACE.Server.WorldObjects
{
    public class Lockpick : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Lockpick(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Lockpick(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.Lockpick;
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            if (!player.VerifyGameplayMode(this))
            {
                player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"This item cannot be used, invalid gameplay mode!"));
                player.SendUseDoneEvent(WeenieError.YouCannotUseThatItem);
                return;
            }

            if (player.IsOlthoiPlayer)
            {
                player.SendUseDoneEvent(WeenieError.OlthoiCannotInteractWithThat);
                return;
            }

            if (target is PressurePlate trap)
                trap.AttemptDisarm(player, this);
            else
                UnlockerHelper.UseUnlocker(player, this, target);
        }
    }
}
