using System;
using System.Collections.Generic;
using System.Linq;
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

                PerformAppraisal(player, target, false);
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

            if (!player.VerifyGameplayMode(source, target))
            {
                player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"These items cannot be used, incompatible gameplay mode!"));
                return WeenieError.YouDoNotPassCraftingRequirements;
            }

            // ensure both source and target are in player's inventory
            if (player.FindObject(source.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            if (player.FindObject(target.Guid.Full, Player.SearchLocations.MyInventory) == null)
                return WeenieError.YouDoNotPassCraftingRequirements;

            if (source.WeenieClassId != MagnifyingGlassWeenieId)
                return WeenieError.YouDoNotPassCraftingRequirements;

            //player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You can't use the {source.Name} on {target.Name} because the sigil is already visible."));

            return WeenieError.None;
        }

        public static void PerformAppraisal(Player player, WorldObject target, bool fromVendorSell = false)
        {
            if (target is Container container)
            {
                CreatureSkill appraisalSkill = player.GetCreatureSkill(Skill.Appraise);
                if (appraisalSkill.AdvancementClass < SkillAdvancementClass.Trained)
                {
                    if (!fromVendorSell)
                    {
                        player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You must have {appraisalSkill.Skill.ToSentence()} trained to appraise that."));
                        player.SendUseDoneEvent();
                    }
                    return;
                }

                var itemsNeedingAppraisal = container.Inventory.Values.Where(k => k.OriginalValue.HasValue && k.OriginalValue != k.Value && !k.Retained).ToList();
                if(itemsNeedingAppraisal.Count == 0)
                {
                    if (!fromVendorSell)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("There's nothing needing appraisal in this container.", ChatMessageType.Broadcast));
                        player.SendUseDoneEvent();
                    }
                    return;
                }

                var successCount = 0;
                foreach (var item in itemsNeedingAppraisal)
                {
                    var currValue = item.Value ?? 0;
                    var trueValue = item.OriginalValue ?? currValue;

                    var diff = 25 + (uint)Math.Sqrt(Math.Min(trueValue, 250000));
                    var chance = SkillCheck.GetSkillChance(appraisalSkill.Current, diff);

                    if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
                    {
                        Proficiency.OnSuccessUse(player, appraisalSkill, diff);

                        if (currValue != trueValue)
                        {
                            successCount++;
                            item.SetProperty(PropertyInt.Value, trueValue);
                            if (!fromVendorSell)
                            {
                                //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(item, PropertyInt.Value, trueValue));
                                player.EnqueueBroadcast(new GameMessageUpdateObject(item)); // The line above won't update the sell value at vendors until a relog, so for now update everything instead.
                            }

                            item.SaveBiotaToDatabase();
                        }
                    }
                }

                if (!fromVendorSell)
                {
                    if (successCount == itemsNeedingAppraisal.Count)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You successfully appraise {successCount} items in the container. All contents are now appraised.", ChatMessageType.Broadcast));
                    else if (successCount == 0)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to appraise any items in the container. There are still {itemsNeedingAppraisal.Count - successCount} unappraised item(s) in this container.", ChatMessageType.Broadcast));
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You successfully appraise {successCount} items in the container. There are still {itemsNeedingAppraisal.Count - successCount} unappraised item(s) in this container.", ChatMessageType.Broadcast));
                }
            }
            else
            {
                if (target.Retained)
                {
                    if (!fromVendorSell)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("Retained items cannot be appraised.", ChatMessageType.Broadcast));
                        player.SendUseDoneEvent();
                    }
                    return;
                }

                if (!target.OriginalValue.HasValue)
                {
                    if (!fromVendorSell)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("You can't appraise that.", ChatMessageType.Broadcast));
                        player.SendUseDoneEvent();
                    }
                    return;
                }

                CreatureSkill appraisalSkill = player.GetCreatureSkill(Skill.Appraise);
                if (appraisalSkill.AdvancementClass < SkillAdvancementClass.Trained)
                {
                    if (!fromVendorSell)
                    {
                        player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You must have {appraisalSkill.Skill.ToSentence()} trained to appraise that."));
                        player.SendUseDoneEvent();
                    }
                    return;
                }

                var currValue = target.Value ?? 0;
                var trueValue = target.OriginalValue ?? currValue;

                var diff = 25 + (uint)Math.Sqrt(Math.Min(trueValue, 250000));
                var chance = SkillCheck.GetSkillChance(appraisalSkill.Current, diff);

                if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
                {
                    Proficiency.OnSuccessUse(player, appraisalSkill, diff);

                    if (!fromVendorSell)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You appraise the {target.NameWithMaterial} to be worth {trueValue} Pyreals.", ChatMessageType.Broadcast));
                    if (currValue != trueValue)
                    {
                        target.SetProperty(PropertyInt.Value, trueValue);
                        if (!fromVendorSell)
                        {
                            //player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(target, PropertyInt.Value, trueValue));
                            player.EnqueueBroadcast(new GameMessageUpdateObject(target)); // The line above won't update the sell value at vendors until a relog, so for now update everything instead.
                        }

                        target.SaveBiotaToDatabase();
                    }
                }
                else if(!fromVendorSell)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to appraise the value of the {target.NameWithMaterial}.", ChatMessageType.Broadcast));
            }

            if (!fromVendorSell)
                player.SendUseDoneEvent();
        }

        public static bool IsMagnifyingGlass(WorldObject wo)
        {
            return wo.WeenieClassId == MagnifyingGlassWeenieId;
        }

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
    }
}
