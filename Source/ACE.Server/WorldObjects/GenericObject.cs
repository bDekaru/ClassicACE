using System;
using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class GenericObject : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public GenericObject(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public GenericObject(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            //StackSize = null;
            //StackUnitEncumbrance = null;
            //StackUnitValue = null;
            //MaxStackSize = null;

            // Linkable Item Generator (linkitemgen2minutes) fix
            if (WeenieClassId == 4142)
            {
                MaxGeneratedObjects = 0;
                InitGeneratedObjects = 0;
            }
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            if (UseSound > 0)
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, UseSound));

            if(WeenieClassId == (uint)Factories.Enum.WeenieClassName.explorationMarker)
            {
                if (player.attacksReceivedPerSecond > 0)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot properly explore your surroundings while in combat!", ChatMessageType.Broadcast));
                    return;
                }

                short landblockId = (short)(CurrentLandblock.Id.Raw >> 16);
                if (player.Exploration1LandblockId == landblockId)
                {
                    if (player.Exploration1MarkerProgressTracker > 0)
                    {
                        player.Exploration1MarkerProgressTracker--;
                        var msg = $"{player.Exploration1MarkerProgressTracker:N0} marker{(player.Exploration1MarkerProgressTracker != 1 ? "s" : "")} remaining.";
                        player.EarnXP((-player.Level ?? -1) - 1000, XpType.Exploration, null, null, 0, null, ShareType.None, msg, PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5);

                        if (player.Exploration1MarkerProgressTracker == 0)
                        {
                            player.PlayParticleEffect(PlayScript.AugmentationUseOther, player.Guid);
                            if (player.Exploration1LandblockReached && player.Exploration1KillProgressTracker == 0)
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your exploration assignment is now fulfilled!", ChatMessageType.Broadcast));
                        }
                    }
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("You have already fulfilled the exploration marker requirements of your assignment.", ChatMessageType.Broadcast));
                }
                else if (player.Exploration2LandblockId == landblockId)
                {
                    if (player.Exploration2MarkerProgressTracker > 0)
                    {
                        player.Exploration2MarkerProgressTracker--;
                        var msg = $"{player.Exploration2MarkerProgressTracker:N0} marker{(player.Exploration2MarkerProgressTracker != 1 ? "s" : "")} remaining.";
                        player.EarnXP((-player.Level ?? -1) - 1000, XpType.Exploration, null, null, 0, null, ShareType.None, msg, PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5);

                        if (player.Exploration2MarkerProgressTracker == 0)
                        {
                            player.PlayParticleEffect(PlayScript.AugmentationUseOther, player.Guid);
                            if (player.Exploration2LandblockReached && player.Exploration2KillProgressTracker == 0)
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your exploration assignment is now fulfilled!", ChatMessageType.Broadcast));
                        }
                    }
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("You have already fulfilled the exploration marker requirements of your assignment.", ChatMessageType.Broadcast));
                }
                else if (player.Exploration3LandblockId == landblockId)
                {
                    if (player.Exploration3MarkerProgressTracker > 0)
                    {
                        player.Exploration3MarkerProgressTracker--;
                        var msg = $"{player.Exploration3MarkerProgressTracker:N0} marker{(player.Exploration3MarkerProgressTracker != 1 ? "s" : "")} remaining.";
                        player.EarnXP((-player.Level ?? -1) - 1000, XpType.Exploration, null, null, 0, null, ShareType.None, msg, PropertyManager.GetDouble("exploration_bonus_xp").Item + 0.5);

                        if (player.Exploration3MarkerProgressTracker == 0)
                        {
                            player.PlayParticleEffect(PlayScript.AugmentationUseOther, player.Guid);
                            if (player.Exploration3LandblockReached && player.Exploration3KillProgressTracker == 0)
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Your exploration assignment is now fulfilled!", ChatMessageType.Broadcast));
                        }
                    }
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("You have already fulfilled the exploration marker requirements of your assignment.", ChatMessageType.Broadcast));
                }
                else
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("You currently do not have any exploration assignments for this location.", ChatMessageType.Broadcast));

                // Antecipate next refresh if it is further than 60 seconds away.
                var nextRefresh = Time.GetFutureUnixTime(60);
                if (CurrentLandblock.NextExplorationMarkerRefresh > nextRefresh)
                    CurrentLandblock.NextExplorationMarkerRefresh = nextRefresh;

                Destroy();
            }
        }
    }
}
