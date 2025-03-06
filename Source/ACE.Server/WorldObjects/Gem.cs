using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;

namespace ACE.Server.WorldObjects
{
    public class Gem : Stackable
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Gem(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Gem(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item should be in the players possession.
        /// 
        /// The OnUse method for this class is to use a contract to add a tracked quest to our quest panel.
        /// This gives the player access to information about the quest such as starting and ending NPC locations,
        /// and shows our progress for kill tasks as well as any timing information such as when we can repeat the
        /// quest or how much longer we have to complete it in the case of at timed quest.   Og II
        /// </summary>
        public override void ActOnUse(WorldObject activator)
        {
            ActOnUse(activator, false);
        }

        public void ActOnUse(WorldObject activator, bool confirmed, WorldObject target = null)
        {
            if (!(activator is Player player))
                return;

            if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            {
                player.SendWeenieError(WeenieError.YoureTooBusy);
                return;
            }

            if (player.IsJumping)
            {
                player.SendWeenieError(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            if (!string.IsNullOrWhiteSpace(UseSendsSignal))
            {
                player.CurrentLandblock?.EmitSignal(player, UseSendsSignal);
                return;
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && SpellDID != null && player.InDungeon)
            {
                switch((SpellId)SpellDID)
                {
                    case SpellId.SummonPortal1:
                    case SpellId.SummonPortal2:
                    case SpellId.SummonPortal3:
                    case SpellId.SummonSecondPortal1:
                    case SpellId.SummonSecondPortal2:
                    case SpellId.SummonSecondPortal3:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat("You may not summon portals from this location.", ChatMessageType.Broadcast));
                        return;
                    default:
                        var spell = ((SpellId)SpellDID).ToString().ToLower();
                        if (spell.Contains("recall"))
                        {
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You may not recall from this location.", ChatMessageType.Broadcast));
                            return;
                        }
                        break;
                }
            }

            // handle rare gems
            if (RareId != null && player.GetCharacterOption(CharacterOption.ConfirmUseOfRareGems) && !confirmed)
            {
                var msg = $"Are you sure you want to use {Name}?";
                var confirm = new Confirmation_Custom(player.Guid, () => ActOnUse(activator, true, target));
                if (!player.ConfirmationManager.EnqueueSend(confirm, msg))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                var timeElapsed = currentTime - player.LastRareUsedTimestamp;

                if (timeElapsed < RareTimer)
                {
                    // TODO: get retail message
                    var remainTime = (int)Math.Ceiling(RareTimer - timeElapsed);
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You may use another timed rare in {remainTime}s", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (UseUserAnimation != MotionCommand.Invalid)
            {
                // some gems have UseUserAnimation and UseSound, similar to food
                // eg. 7559 - Condensed Dispel Potion

                // the animation is also weird, and differs from food, in that it is the full animation
                // instead of stopping at the 'eat/drink' point... so we pass 0.5 here?

                var animMod = (UseUserAnimation == MotionCommand.MimeDrink || UseUserAnimation == MotionCommand.MimeEat) ? 0.5f : 1.0f;

                player.ApplyConsumable(UseUserAnimation, () => UseGem(player, target), animMod);
            }
            else
                UseGem(player, target);
        }

        public void UseGem(Player player, WorldObject target)
        {
            if (player.IsDead) return;

            // verify item is still valid
            if (player.FindObject(Guid.Full, Player.SearchLocations.MyInventory) == null)
            {
                //player.SendWeenieError(WeenieError.ObjectGone);   // results in 'Unable to move object!' transient error
                player.SendTransientError($"Cannot find the {Name}");   // custom message
                return;
            }

            // trying to use a dispel potion while pk timer is active
            // send error message and cancel - do not consume item
            if (SpellDID != null)
            {
                var spell = new Spell(SpellDID.Value);

                if (spell.MetaSpellType == SpellType.Dispel && !VerifyDispelPKStatus(this, player))
                    return;
            }

            if (RareUsesTimer)
            {
                var currentTime = Time.GetUnixTime();

                player.LastRareUsedTimestamp = currentTime;

                // local broadcast usage
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} used the rare item {Name}", ChatMessageType.Broadcast));
            }

            if(MaxStructure.HasValue && MaxStructure > 0 && Structure == 0)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {NameWithMaterial} has no uses left!", ChatMessageType.Craft));
                return;
            }

            bool usesMana = false;
            if (SpellDID.HasValue)
            {
                var spell = new Spell((uint)SpellDID);

                usesMana = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && (ItemManaCost ?? 0) != 0;
                if (usesMana)
                {
                    int manaCost;

                    var manaConversion = player.GetCreatureSkill(Skill.ManaConversion);
                    if (manaConversion.AdvancementClass < SkillAdvancementClass.Trained)
                        manaCost = (int)ItemManaCost;
                    else
                        manaCost = (int)Player.GetManaCost((uint)ItemSpellcraft, (uint)ItemManaCost, manaConversion.Current, manaConversion.AdvancementClass);

                    if (ItemCurMana >= manaCost)
                    {
                        ItemCurMana -= manaCost;
                        player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.ItemCurMana, (int)ItemCurMana));
                    }
                    else
                    {
                        player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"The {NameWithMaterial} doesn't have enough mana!"));
                        return;
                    }
                }

                if (spell.MetaSpellType == SpellType.PortalSummon || spell.MetaSpellType == SpellType.PortalRecall)
                {
                    if (LinkedPortalOneDID != null || LinkedPortalTwoDID != null)
                        TryCastSpell(spell, player, this, tryResist: false); // if we're a summon portal gem with a predetermined destination summon that.
                    else
                    {
                        if (spell.Id == (uint)SpellId.LifestoneRecall1 && player.LinkedLifestone == null)
                        {
                            player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouMustLinkToLifestoneToRecall));
                            return;
                        }
                        else if ((spell.Id == (uint)SpellId.PortalTieRecall1 && player.LinkedPortalOneDID == null) ||
                                 (spell.Id == (uint)SpellId.PortalTieRecall2 && player.LinkedPortalTwoDID == null))
                        {
                            player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouMustLinkToPortalToRecall));
                            return;
                        }
                        else if (((spell.Id == (uint)SpellId.SummonPortal1 || spell.Id == (uint)SpellId.SummonPortal2 || spell.Id == (uint)SpellId.SummonPortal3) && player.LinkedPortalOneDID == null) ||
                                ((spell.Id == (uint)SpellId.SummonSecondPortal1 || spell.Id == (uint)SpellId.SummonSecondPortal2 || spell.Id == (uint)SpellId.SummonSecondPortal3) && player.LinkedPortalTwoDID == null))
                        {
                            player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouMustLinkToPortalToSummonIt));
                            return;
                        }
                        else
                            player.TryCastSpell(spell, player, this, tryResist: false);
                    }
                }
                else if (spell.MetaSpellType == SpellType.PortalLink)
                {
                    if (target.WeenieType != WeenieType.Portal)
                    {
                        player.SendChatMessage(this, "You cannot link that.", ChatMessageType.Magic);
                        return;
                    }

                    var targetPortal = target as Portal;

                    var summoned = targetPortal.OriginalPortal != null;

                    var targetDID = summoned ? targetPortal.OriginalPortal : targetPortal.WeenieClassId;

                    var tiePortal = GetPortal(targetDID.Value);

                    if (tiePortal == null)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouCannotLinkToThatPortal));
                        return;
                    }

                    var result = tiePortal.CheckUseRequirements(player);

                    if (!result.Success && result.Message != null)
                        player.Session.Network.EnqueueSend(result.Message);

                    if (tiePortal.NoTie || !result.Success)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouCannotLinkToThatPortal));
                        return;
                    }

                    player.TryCastSpell(spell, target, this, tryResist: false); // This spell cast must come from the player otherwise the link property will be set on the gem instead.
                }
                else if (spell.IsImpenBaneType || spell.IsItemRedirectableType)
                    TryCastItemEnchantment_WithRedirects(spell, player, this);
                else if (target != null)
                    TryCastSpell(spell, target, this, tryResist: false);
                else
                    TryCastSpell(spell, player, this, tryResist: false);
            }

            if (UseCreateContractId > 0)
            {
                if (!player.ContractManager.Add(UseCreateContractId.Value))
                    return;

                // this wasn't in retail, but the lack of feedback when using a contract gem just seems jarring so...
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name} accepted. Click on the quill icon in the lower right corner to open your contract tab to view your active contracts.", ChatMessageType.Broadcast));
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (TacticAndTechniqueId > 0)
                {
                    switch ((TacticAndTechniqueType)TacticAndTechniqueId)
                    {
                        case TacticAndTechniqueType.Taunt:
                            if (player.ToggleTauntSetting())
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You will now start attempting to taunt opponents into attacking you.", ChatMessageType.Broadcast));
                            else
                                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You will no longer attempt to taunt opponents.", ChatMessageType.Broadcast));
                            break;
                        case TacticAndTechniqueType.Sneak:
                            if (!player.IsSneaking)
                                player.BeginSneaking();
                            else
                                player.EndSneaking();
                            break;
                        case TacticAndTechniqueType.Misdirect:
                            player.Misdirect();
                            break;
                    }
                }
            }

            if (UseCreateItem > 0)
            {
                if (!HandleUseCreateItem(player))
                    return;
            }

            if (UseSound > 0)
                player.Session.Network.EnqueueSend(new GameMessageSound(player.Guid, UseSound));

            var unlimitedUses = GetProperty(PropertyBool.UnlimitedUse) ?? false;
            if (!unlimitedUses)
            {
                if (Structure.HasValue)
                {
                    if (Structure > 0)
                        Structure--;
                    else
                        Structure = 0;

                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, (int)Structure));
                }
                else if (!usesMana)
                    player.TryConsumeFromInventoryWithNetworking(this, 1);
            }

            player.EnchantmentManager.StartCooldown(this);
        }

        public bool HandleUseCreateItem(Player player)
        {
            var amount = UseCreateQuantity ?? 1;

            var itemsToReceive = new ItemsToReceive(player);

            itemsToReceive.Add(UseCreateItem.Value, amount);

            if (itemsToReceive.PlayerExceedsLimits)
            {
                if (itemsToReceive.PlayerExceedsAvailableBurden)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You are too encumbered to use that!"));
                else if (itemsToReceive.PlayerOutOfInventorySlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough pack space to use that!"));
                else if (itemsToReceive.PlayerOutOfContainerSlots)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You do not have enough container slots to use that!"));

                return false;
            }

            if (itemsToReceive.RequiredSlots > 0)
            {
                var remaining = amount;

                while (remaining > 0)
                {
                    var item = WorldObjectFactory.CreateNewWorldObject(UseCreateItem.Value);

                    if (item is Stackable)
                    {
                        var stackSize = Math.Min(remaining, item.MaxStackSize ?? 1);

                        item.SetStackSize(stackSize);
                        remaining -= stackSize;
                    }
                    else
                        remaining--;

                    player.TryCreateInInventoryWithNetworking(item);
                }
            }
            else
            {
                player.SendTransientError($"Unable to use {Name} at this time!");
                return false;
            }
            return true;
        }

        public int? RareId
        {
            get => GetProperty(PropertyInt.RareId);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.RareId); else SetProperty(PropertyInt.RareId, value.Value); }
        }

        public bool RareUsesTimer
        {
            get => GetProperty(PropertyBool.RareUsesTimer) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.RareUsesTimer); else SetProperty(PropertyBool.RareUsesTimer, value); }
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            // should tailoring kit / aetheria be subtyped?
            if (Tailoring.IsTailoringKit(WeenieClassId))
            {
                Tailoring.UseObjectOnTarget(player, this, target);
                return;
            }

            var result = CheckUseRequirements(player);
            var resultTarget = target.CheckUseRequirements(player);

            if (!result.Success)
            {
                if (result.Message != null)
                    player.Session.Network.EnqueueSend(result.Message);
                player.SendUseDoneEvent();
                return;
            }

            if (target != null)
            {
                if (!resultTarget.Success)
                {
                    if (resultTarget.Message != null)
                        player.Session.Network.EnqueueSend(resultTarget.Message);
                    player.SendUseDoneEvent();
                    return;
                }
            }

            if (RecipeManager.GetRecipe(player, this, target) != null) // if we have a recipe do that, otherwise redirect to ActOnUse with a target.
                base.HandleActionUseOnTarget(player, target);
            else
            {
                ActOnUse(player, false, target);
                player.SendUseDoneEvent();
            }
        }

        /// <summary>
        /// For Rares that use cooldown timers (RareUsesTimer),
        /// any other rares with RareUsesTimer may not be used for 3 minutes
        /// Note that if the player logs out, this cooldown timer continues to tick/expire (unlike enchantments)
        /// </summary>
        public static int RareTimer = 180;

        public string UseSendsSignal
        {
            get => GetProperty(PropertyString.UseSendsSignal);
            set { if (value == null) RemoveProperty(PropertyString.UseSendsSignal); else SetProperty(PropertyString.UseSendsSignal, value); }
        }

        public override void OnActivate(WorldObject activator)
        {
            if (ItemUseable == Usable.Contained && activator is Player player)
            {               
                var containedItem = player.FindObject(Guid.Full, Player.SearchLocations.MyInventory | Player.SearchLocations.MyEquippedItems);
                if (containedItem != null) // item is contained by player
                {
                    if (player.IsBusy || player.Teleporting || player.suicideInProgress)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YoureTooBusy));
                        return;
                    }

                    if (player.IsDead)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.Dead));
                        return;
                    }
                }
                else
                    return;
            }

            base.OnActivate(activator);
        }
    }
}
