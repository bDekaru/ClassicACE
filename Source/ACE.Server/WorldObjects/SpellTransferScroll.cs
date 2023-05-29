using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    public class SpellTransferScroll : Stackable
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public SpellTransferScroll(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public SpellTransferScroll(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }
        public static void BroadcastSpellTransfer(Player player, string spellName, WorldObject target, double chance = 1.0f, bool success = true)
        {
            // send local broadcast
            if (success)
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} successfully transfers {spellName} to the {target.NameWithMaterial}.", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);
            else
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} fails to transfer {spellName} to the {target.NameWithMaterial}. The target is destroyed.", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);

            log.Debug($"[SpellTransfer] {player.Name} {(success ? "successfully transfers" : "fails to transfer")} {spellName} to the {target.NameWithMaterial}.{(!success ? " The target is destroyed." : "")} | Chance: {chance}");
        }

        public static void BroadcastSpellExtraction(Player player, string spellName, WorldObject target, double chance, bool success)
        {
            // send local broadcast
            if (success)
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} successfully extracts {spellName} from the {target.NameWithMaterial}. The target is destroyed.", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);
            else
                player.EnqueueBroadcast(new GameMessageSystemChat($"{player.Name} fails to extract {spellName} from the {target.NameWithMaterial}. The target is destroyed.", ChatMessageType.Craft), WorldObject.LocalBroadcastRange, ChatMessageType.Craft);

            log.Debug($"[SpellTransfer] {player.Name} {(success ? "successfully extracts" : "fails to extract")} {spellName} from the {target.NameWithMaterial}. The target is destroyed. | Chance: {chance}");
        }

        public override void HandleActionUseOnTarget(Player player, WorldObject target)
        {
            UseObjectOnTarget(player, this, target);
        }

        public static void UseObjectOnTarget(Player player, WorldObject source, WorldObject target, bool confirmed = false)
        {
            if (player.IsBusy)
            {
                player.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            if (!RecipeManager.VerifyUse(player, source, target, true) || (target.Workmanship == null && target.ExtraSpellsMaxOverride == null))
            {
                player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                return;
            }

            if (source.SpellDID.HasValue) // Transfer Scroll
            {
                var spellToAddId = (uint)source.SpellDID;
                var spellToAddlevel1Id = SpellLevelProgression.GetLevel1SpellId((SpellId)spellToAddId);
                Spell spellToAdd = new Spell(spellToAddId);

                var isProc = false;
                if (spellToAddlevel1Id != SpellId.Undef && (MeleeSpells.meleeProcs.FirstOrDefault(x => x.result == spellToAddlevel1Id) != default((SpellId, float)) || MissileSpells.missileProcs.FirstOrDefault(x => x.result == spellToAddlevel1Id) != default((SpellId, float))))
                {
                    isProc = true;

                    if(target.ItemType != ItemType.MeleeWeapon && target.ItemType != ItemType.MissileWeapon)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                }

                var isGem = false;
                if(target.ItemType == ItemType.Gem)
                {
                    if (!PropertyManager.GetBool("useable_gems").Item)
                    {
                        player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                        return;
                    }

                    isGem = true;
                    if(spellToAdd.IsCantrip)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                    else if (spellToAdd.School == MagicSchool.ItemEnchantment)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                }
                else if(target.ItemType == ItemType.MeleeWeapon || target.ItemType == ItemType.MissileWeapon || target.ItemType == ItemType.Caster)
                {
                    if (spellToAdd.IsImpenBaneType)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                }
                else
                {
                    if (spellToAdd.IsWeaponTargetType)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                }

                if (spellToAdd.School == MagicSchool.ItemEnchantment && target.ResistMagic >= 9999)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain {spellToAdd.Name}.", ChatMessageType.Craft));
                    player.SendUseDoneEvent();
                    return;
                }

                var spellsOnItem = target.Biota.GetKnownSpellsIds(target.BiotaDatabaseLock);

                if (target.ProcSpell != null && target.ProcSpell != 0)
                    spellsOnItem.Add((int)target.ProcSpell);

                var enchantments = new List<SpellId>();
                var cantrips = new List<SpellId>();
                if (spellToAdd.IsCantrip)
                    cantrips.Add((SpellId)spellToAddId);
                else if (spellToAdd.School == MagicSchool.CreatureEnchantment)
                    enchantments.Add((SpellId)spellToAddId);

                Spell spellToReplace = null;
                foreach (var spellOnItemId in spellsOnItem)
                {
                    Spell spellOnItem = new Spell(spellOnItemId);
                    if (spellOnItem.IsCantrip)
                        cantrips.Add((SpellId)spellOnItemId);
                    else if(spellOnItem.School == MagicSchool.CreatureEnchantment)
                        enchantments.Add((SpellId)spellOnItemId);

                    if (spellOnItemId == spellToAddId)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} already contains {spellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                    else if(spellOnItem.Category == spellToAdd.Category)
                    {
                        if (spellOnItem.Power > spellToAdd.Power)
                        {
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} already contains {spellOnItem.Name}, which is stronger than {spellToAdd.Name}.", ChatMessageType.Craft));
                            player.SendUseDoneEvent();
                            return;
                        }
                        else if (spellOnItem.Power == spellToAdd.Power)
                        {
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} already contains {spellOnItem.Name}, which is equivalent to {spellToAdd.Name}.", ChatMessageType.Craft));
                            player.SendUseDoneEvent();
                            return;
                        }
                        else
                            spellToReplace = spellOnItem;
                    }
                }

                if (!isGem && target.ProcSpell == null && spellToReplace == null)
                {
                    if ((target.ExtraSpellsCount ?? 0) >= target.GetMaxExtraSpellsCount())
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} cannot contain any more spells.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    }
                }

                if (!confirmed)
                {
                    var extraMessage = "";
                    if (isProc && target.ProcSpell != null)
                    {
                        var currentProc = new Spell(target.ProcSpell ?? 0);
                        extraMessage = $"\nThis will replace {currentProc.Name}!\n";
                    }
                    else if (isGem && target.SpellDID != null)
                    {
                        var currentGemSpell = new Spell(target.SpellDID ?? 0);
                        extraMessage = $"\nThis will replace {currentGemSpell.Name}!\n";
                    }
                    else if (spellToReplace != null)
                        extraMessage = $"\nThis will replace {spellToReplace.Name}!\n";

                    if (!player.ConfirmationManager.EnqueueSend(new Confirmation_CraftInteration(player.Guid, source.Guid, target.Guid), $"Transfering {spellToAdd.Name} to {target.NameWithMaterial}.\n{(extraMessage.Length > 0 ? extraMessage : "")}\n"))
                        player.SendUseDoneEvent(WeenieError.ConfirmationInProgress);
                    else
                        player.SendUseDoneEvent();
                    return;
                }

                var actionChain = new ActionChain();

                var animTime = 0.0f;

                player.IsBusy = true;

                if (player.CombatMode != CombatMode.NonCombat)
                {
                    var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                    actionChain.AddDelaySeconds(stanceTime);

                    animTime += stanceTime;
                }

                animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

                actionChain.AddAction(player, () =>
                {
                    if (isProc)
                    {
                        target.ProcSpellRate = 0.15f;
                        target.ProcSpell = spellToAddId;
                        target.ProcSpellSelfTargeted = spellToAdd.IsSelfTargeted;
                    }
                    else if (isGem)
                        target.SpellDID = spellToAddId;
                    else
                    {
                        if (spellToReplace != null)
                            target.Biota.TryRemoveKnownSpell((int)spellToReplace.Id, target.BiotaDatabaseLock);
                        target.Biota.GetOrAddKnownSpell((int)spellToAddId, target.BiotaDatabaseLock, out _);
                    }

                    var newMaxBaseMana = LootGenerationFactory.GetMaxBaseMana(target);
                    var newManaRate = LootGenerationFactory.CalculateManaRate(newMaxBaseMana);
                    var newMaxMana = (int)spellToAdd.BaseMana * 15;

                    if (target.TinkerLog != null)
                    {
                        var tinkers = target.TinkerLog.Split(",");
                        var appliedMoonstoneCount = tinkers.Count(s => s == "31");
                        newMaxMana += 500 * appliedMoonstoneCount;
                    }

                    if (isGem)
                    {
                        target.ItemUseable = Usable.Contained;
                        target.ItemManaCost = (int)spellToAdd.BaseMana;
                        var baseWeenie = DatabaseManager.World.GetCachedWeenie(target.WeenieClassId);
                        if (baseWeenie != null)
                        {
                            target.Name = baseWeenie.GetName(); // Reset to base name before rebuilding suffix.
                            target.LongDesc = LootGenerationFactory.GetLongDesc(target);
                            target.Name = target.LongDesc;
                        }
                    }
                    else if (newMaxMana > (target.ItemMaxMana ?? 0))
                    {
                        target.ManaRate = newManaRate;
                        target.LongDesc = LootGenerationFactory.GetLongDesc(target);
                    }

                    if (spellToReplace == null || (isProc && target.ProcSpell == null))
                        target.ExtraSpellsCount = (target.ExtraSpellsCount ?? 0) + 1;

                    target.ItemMaxMana = newMaxMana;
                    target.ItemCurMana = Math.Clamp(target.ItemCurMana ?? 0, 0, target.ItemMaxMana ?? 0);

                    var newRollDiff = LootGenerationFactory.RollEnchantmentDifficulty(enchantments);
                    newRollDiff += LootGenerationFactory.RollCantripDifficulty(cantrips);
                    UpdateArcaneLoreAndSpellCraft(target, newRollDiff);

                    if (!target.UiEffects.HasValue) // Elemental effects take precendence over magical as it is more important to know the element of a weapon than if it has spells.
                        target.UiEffects = ACE.Entity.Enum.UiEffects.Magical;

                    player.EnqueueBroadcast(new GameMessageUpdateObject(target));

                    player.TryConsumeFromInventoryWithNetworking(source); // Consume the scroll.
                    BroadcastSpellTransfer(player, spellToAdd.Name, target);
                });

                player.EnqueueMotion(actionChain, MotionCommand.Ready);

                actionChain.AddAction(player, () =>
                {
                    player.IsBusy = false;
                });

                actionChain.EnqueueChain();

                player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
            }
            else // Extraction Scroll
            {
                if (target.Retained == true)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} is Retained!.", ChatMessageType.Craft));
                    player.SendUseDoneEvent();
                    return;
                }
                int spellCount = 0;
                var allSpells = target.Biota.GetKnownSpellsIds(target.BiotaDatabaseLock);
                if (target.ProcSpell != null && target.ProcSpell != 0)
                    allSpells.Add((int)target.ProcSpell);
                else if (target.ItemType == ItemType.Gem)
                    allSpells.Add((int)target.SpellDID);

                var spells = new List<int>();
                if (source.Level.HasValue)
                {
                    foreach (var spellId in allSpells)
                    {
                        Spell spell = new Spell(spellId);
                        if(spell.IsCantrip)
                        {
                            if(spell.Formula.Level == 1 && (source.Level == 3 || source.Level == 10)) // Minor Cantrips
                                spells.Add(spellId);
                            else if (spell.Formula.Level > 1 && (source.Level == 6 || source.Level == 11)) // Other Cantrips
                                spells.Add(spellId);
                        }
                        else if (spell.Level == source.Level)
                            spells.Add(spellId);
                    }
                }
                else
                    spells = allSpells;

                spellCount = spells.Count;
                if (spellCount == 0)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} does not have any valid spells to extract.", ChatMessageType.Craft));
                    player.SendUseDoneEvent();
                    return;
                }

                var chance = Math.Clamp(0.25 + ((spellCount - 1) * 0.1), 0.25, 1.0);

                if (target.ItemType == ItemType.Gem && target.ItemUseable == Usable.No)
                    chance = 1; // Non-useable gems have 100% extraction chance.

                var percent = chance * 100;
                var showDialog = player.GetCharacterOption(CharacterOption.UseCraftingChanceOfSuccessDialog);
                if (showDialog && !confirmed)
                {
                    if (!player.ConfirmationManager.EnqueueSend(new Confirmation_CraftInteration(player.Guid, source.Guid, target.Guid), $"Extracting a spell from {target.NameWithMaterial}.\nIt will be destroyed in the process.\n\nYou determine that you have a {percent.Round()} percent chance to succeed.\n\n"))
                        player.SendUseDoneEvent(WeenieError.ConfirmationInProgress);
                    else
                        player.SendUseDoneEvent();


                    if (PropertyManager.GetBool("craft_exact_msg").Item)
                    {
                        var exactMsg = $"You have a {(float)percent} percent chance of extracting a spell from {target.NameWithMaterial}.";

                        player.Session.Network.EnqueueSend(new GameMessageSystemChat(exactMsg, ChatMessageType.Craft));
                    }
                    return;
                }

                var actionChain = new ActionChain();

                var animTime = 0.0f;

                player.IsBusy = true;

                if (player.CombatMode != CombatMode.NonCombat)
                {
                    var stanceTime = player.SetCombatMode(CombatMode.NonCombat);
                    actionChain.AddDelaySeconds(stanceTime);

                    animTime += stanceTime;
                }

                animTime += player.EnqueueMotion(actionChain, MotionCommand.ClapHands);

                actionChain.AddAction(player, () =>
                {
                    var success = ThreadSafeRandom.Next(0.0f, 1.0f) < chance;
                    var spellName = "a spell";
                    if (success)
                    {
                        var spellToExtractRoll = ThreadSafeRandom.Next(0, spellCount - 1);
                        var spellToExtractId = spells[spellToExtractRoll];

                        if (player.TryConsumeFromInventoryWithNetworking(source, 1)) // Consume the scroll
                        {
                            Spell spell = new Spell(spellToExtractId);
                            spellName = spell.Name;

                            var newScroll = WorldObjectFactory.CreateNewWorldObject(50130); // Spell Transfer Scroll
                            newScroll.SpellDID = (uint)spellToExtractId;
                            newScroll.Name += spellName;
                            if (player.TryCreateInInventoryWithNetworking(newScroll)) // Create the transfer scroll
                                player.TryConsumeFromInventoryWithNetworking(target); // Destroy the item
                            else
                                newScroll.Destroy(); // Clean up on creation failure
                        }
                    }
                    else
                    {
                        player.TryConsumeFromInventoryWithNetworking(source, 1); // Consume the scroll
                        player.TryConsumeFromInventoryWithNetworking(target); // Destroy the item
                    }

                    BroadcastSpellExtraction(player, spellName, target, chance, success);
                });

                player.EnqueueMotion(actionChain, MotionCommand.Ready);

                actionChain.AddAction(player, () =>
                {
                    if (!showDialog)
                        player.SendUseDoneEvent();

                    player.IsBusy = false;
                });

                actionChain.EnqueueChain();

                player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
            }
        }

        private static void UpdateArcaneLoreAndSpellCraft(WorldObject wo, float newRollDiff)
        {
            var newSpellcraft = LootGenerationFactory.RollSpellcraft(wo);

            var itemSkillLevelFactor = 0.0f;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 10.0f;
            }
            else
            {
                if (wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 2.0f;
            }

            var fArcane = newSpellcraft - itemSkillLevelFactor;

            if (wo.ItemAllegianceRankLimit > 0)
                fArcane -= (float)wo.ItemAllegianceRankLimit * 10.0f;

            if (wo.HeritageGroup != 0)
                fArcane -= fArcane * 0.2f;

            if (fArcane < 0)
                fArcane = 0;

            wo.ItemDifficulty = (int)Math.Floor(fArcane + newRollDiff);
            wo.ItemSpellcraft = newSpellcraft;
        }
    }
}
