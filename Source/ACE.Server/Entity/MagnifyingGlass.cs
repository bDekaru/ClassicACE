using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;
using log4net;

namespace ACE.Server.Entity
{
    public class MagnifyingGlass : CraftTool
    {
        public const uint MagnifyingGlassWeenieId = 50077;

        static MagnifyingGlass()
        {
        }

        public MagnifyingGlass(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
        }

        public MagnifyingGlass(Biota biota) : base(biota)
        {
        }

        public static void UseObjectOnTarget(Player player, WorldObject source, WorldObject target)
        {
            //Console.WriteLine($"Aetheria.UseObjectOnTarget({player.Name}, {source.Name}, {target.Name})");

            if (player.IsBusy)
            {
                player.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            // verify use requirements
            var useError = VerifyUseRequirements(player, source, target);
            if (useError != WeenieError.None)
            {
                player.SendUseDoneEvent(useError);
                return;
            }

            var animTime = 0.0f;

            var actionChain = new ActionChain();

            player.IsBusy = true;

            // handle switching to peace mode
            if (player.CombatMode != CombatMode.NonCombat)
            {
                var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                actionChain.AddDelaySeconds(stanceTime);

                animTime += stanceTime;
            }

            // perform clapping motion
            animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

            actionChain.AddAction(player, () =>
            {
                // re-verify
                var useError = VerifyUseRequirements(player, source, target);
                if (useError != WeenieError.None)
                {
                    player.SendUseDoneEvent(useError);
                    return;
                }

                PerformAppraisal(player, source, target);
            });

            player.EnqueueMotion(actionChain, MotionCommand.Ready);

            actionChain.AddAction(player, () => player.IsBusy = false);

            actionChain.EnqueueChain();

            player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
        }

        public static WeenieError VerifyUseRequirements(Player player, WorldObject source, WorldObject target)
        {
            if (source == target)
            {
                player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You can't use the {source.Name} on itself."));
                return WeenieError.YouDoNotPassCraftingRequirements;
            }

            // ensure both source and target are in player's inventory
            if (player.FindObject(source.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            if (source.WeenieClassId != MagnifyingGlassWeenieId || !target.OriginalValue.HasValue || target.OriginalValue == 0)
                return WeenieError.YouDoNotPassCraftingRequirements;

            //player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You can't use the {source.Name} on {target.Name} because the sigil is already visible."));

            return WeenieError.None;
        }

        public static void PerformAppraisal(Player player, WorldObject source, WorldObject target)
        {
            if(!target.OriginalValue.HasValue)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("You can't appraise that.", ChatMessageType.Broadcast));
                player.SendUseDoneEvent();
                return;
            }

            CreatureSkill appraisalSkill = player.GetCreatureSkill(Skill.Appraise);
            if (appraisalSkill.AdvancementClass < SkillAdvancementClass.Trained)
            {
                player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You must have {appraisalSkill.Skill.ToSentence()} trained to appraise that."));
                player.SendUseDoneEvent();
                return;
            }

            var currValue = target.Value ?? 0;
            var trueValue = target.OriginalValue ?? currValue;

            var diff = 25 + (uint)Math.Sqrt(Math.Min(trueValue, 250000));
            var chance = SkillCheck.GetSkillChance(appraisalSkill.Current, diff);

            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                Proficiency.OnSuccessUse(player, appraisalSkill, diff);

                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You appraise the {target.NameWithMaterial} to be worth {trueValue} Pyreals.", ChatMessageType.Broadcast));
                if (currValue != trueValue)
                {
                    target.SetProperty(PropertyInt.Value, trueValue);
                    player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(target, PropertyInt.Value, trueValue));

                    target.SaveBiotaToDatabase();
                }
            }
            else
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to appraise the value of the {target.NameWithMaterial}.", ChatMessageType.Broadcast));

            player.SendUseDoneEvent();
        }

        public static bool IsMagnifyingGlass(WorldObject wo)
        {
            return wo.WeenieClassId == MagnifyingGlassWeenieId;
        }

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
