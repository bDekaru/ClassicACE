using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
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

        public static void BroadcastSpellExtraction(Player player, string spellName, WorldObject target, double chance = 1.0f, bool success = true)
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

            if (!player.VerifyGameplayMode(source, target))
            {
                player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"These items cannot be used, incompatible gameplay mode!"));
                player.SendUseDoneEvent();
                return;
            }

            if (!RecipeManager.VerifyUse(player, source, target, true))
            {
                if(!confirmed)
                    player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                else
                    player.SendTransientError("Either you or one of the items involved does not pass the requirements for this craft interaction.");
                return;
            }

            if (source.SpellDID.HasValue) // Transfer Scroll
            {
                var data = InjectSpell(target, (SpellId)source.SpellDID, true, confirmed);
                switch (data.Result)
                {
                    case InjectSpellResult.YouDoNotPassCraftingRequirements:
                        player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                        return;
                    case InjectSpellResult.TargetCannotContainSpell:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {data.Target.NameWithMaterial} cannot contain {data.SpellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.TargetAlreadyContainsSpell:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {data.Target.NameWithMaterial} already contains {data.SpellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.TargetContainsStrongerSpell:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {data.Target.NameWithMaterial} already contains {data.SpellOnItem.Name}, which is stronger than {data.SpellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.TargetContainsEquivalentSpell:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {data.Target.NameWithMaterial} already contains {data.SpellOnItem.Name}, which is equivalent to {data.SpellToAdd.Name}.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.TargetCannotContainMoreSpells:
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {data.Target.NameWithMaterial} cannot contain any more spells.", ChatMessageType.Craft));
                        player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.RequiresConfirmation:
                        if (!player.ConfirmationManager.EnqueueSend(new Confirmation_CraftInteration(player.Guid, source.Guid, target.Guid), data.ConfirmationMessage))
                            player.SendUseDoneEvent(WeenieError.ConfirmationInProgress);
                        else
                            player.SendUseDoneEvent();
                        return;
                    case InjectSpellResult.ReadyToProceed:
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
                            if (!RecipeManager.VerifyUse(player, source, target, true))
                            {
                                // No longer valid, abort
                                player.SendTransientError("Either you or one of the items involved does not pass the requirements for this craft interaction.");
                                return;
                            }

                            ContinueInjectSpell(data);

                            player.EnqueueBroadcast(new GameMessageUpdateObject(target));

                            player.TryConsumeFromInventoryWithNetworking(source); // Consume the scroll.
                            BroadcastSpellTransfer(player, data.SpellToAdd.Name, target);
                        });

                        player.EnqueueMotion(actionChain, MotionCommand.Ready);

                        actionChain.AddAction(player, () =>
                        {
                            player.IsBusy = false;
                        });

                        actionChain.EnqueueChain();

                        player.NextUseTime = DateTime.UtcNow.AddSeconds(animTime);
                        break;
                }
            }
            else // Extraction Scroll
            {
                if (target.Workmanship == null)
                {
                    player.SendUseDoneEvent(WeenieError.YouDoNotPassCraftingRequirements);
                    return;
                }

                if (target.Retained == true)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"The {target.NameWithMaterial} is Retained!.", ChatMessageType.Craft));
                    player.SendUseDoneEvent();
                    return;
                }
                int spellCount = 0;
                var allSpells = target.Biota.GetKnownSpellsIds(target.BiotaDatabaseLock).Select(i => (SpellId)i).ToList();
                if (target.ProcSpell != null && target.ProcSpell != 0)
                    allSpells.Add((SpellId)target.ProcSpell);
                else if (target.ItemType == ItemType.Gem)
                    allSpells.Add((SpellId)target.SpellDID);

                RemoveTinkerSpellsFromList(target.TinkerLog, allSpells);

                var spells = new List<SpellId>();
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

                var showDialog = player.GetCharacterOption(CharacterOption.UseCraftingChanceOfSuccessDialog);
                if (showDialog && !confirmed)
                {
                    string msg;
                    if (spellCount == 1)
                    {
                        var spell = new Spell(spells[0]);
                        msg = $"Extracting {spell.Name} from {target.NameWithMaterial}.\nIt will be destroyed in the process.\n\n";
                    }
                    else
                        msg = $"Extracting a random spell from {target.NameWithMaterial}.\nIt will be destroyed in the process.\n\n";
                    if (!player.ConfirmationManager.EnqueueSend(new Confirmation_CraftInteration(player.Guid, source.Guid, target.Guid), msg))
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
                    if (!RecipeManager.VerifyUse(player, source, target, true))
                    {
                        // No longer valid, abort
                        player.SendTransientError("Either you or one of the items involved does not pass the requirements for this craft interaction.");
                        return;
                    }

                    var spellName = "a spell";
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

                    BroadcastSpellExtraction(player, spellName, target);
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

        public static void RemoveTinkerSpellsFromList(string tinkerLog, List<SpellId> spellIdList)
        {
            if (tinkerLog == null)
                return;

            var tinkers = tinkerLog.Split(",");
            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Carnelian).ToString()))
                spellIdList.Remove(SpellId.CANTRIPSTRENGTH1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Bloodstone).ToString()))
                spellIdList.Remove(SpellId.CANTRIPENDURANCE1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.SmokeyQuartz).ToString()))
                spellIdList.Remove(SpellId.CANTRIPCOORDINATION1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.RoseQuartz).ToString()))
                spellIdList.Remove(SpellId.CANTRIPQUICKNESS1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Agate).ToString()))
                spellIdList.Remove(SpellId.CANTRIPFOCUS1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.LapisLazuli).ToString()))
                spellIdList.Remove(SpellId.CANTRIPWILLPOWER1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.RedJade).ToString()))
                spellIdList.Remove(SpellId.CANTRIPHEALTHGAIN1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Citrine).ToString()))
                spellIdList.Remove(SpellId.CANTRIPSTAMINAGAIN1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.LavenderJade).ToString()))
                spellIdList.Remove(SpellId.CANTRIPMANAGAIN1);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Hematite).ToString()))
                spellIdList.Remove(SpellId.WarriorsVitality);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Malachite).ToString()))
                spellIdList.Remove(SpellId.WarriorsVigor);

            if (tinkers.Contains(((uint)ACE.Entity.Enum.MaterialType.Azurite).ToString()))
                spellIdList.Remove(SpellId.WizardsIntellect);
        }

        public enum InjectSpellResult
        {
            Success,
            RequiresConfirmation,
            ReadyToProceed,
            YouDoNotPassCraftingRequirements,
            TargetCannotContainSpell,
            TargetAlreadyContainsSpell,
            TargetContainsStrongerSpell,
            TargetContainsEquivalentSpell,
            TargetCannotContainMoreSpells,
        }

        public struct InjectSpellData
        {
            public InjectSpellResult Result;
            public WorldObject Target;
            public Spell SpellToAdd;
            public Spell SpellToReplace;
            public Spell SpellOnItem;
            public bool IsProc;
            public bool IsGem;
            public List<SpellId> AllSpells;
            public List<SpellId> LifeCreatureEnchantments;
            public List<SpellId> Cantrips;
            public string ConfirmationMessage;
        }

        public static InjectSpellData InjectSpell(WorldObject target, SpellId spellToAddId, bool requireConfirmation = false, bool confirmed = false)
        {
            InjectSpellData data = new InjectSpellData();
            data.Target = target;

            if (data.Target.Workmanship == null && data.Target.ExtraSpellsMaxOverride == null)
            {
                data.Result = InjectSpellResult.YouDoNotPassCraftingRequirements;
                return data;
            }

            var spellToAddlevel1Id = SpellLevelProgression.GetLevel1SpellId(spellToAddId);
            data.SpellToAdd = new Spell(spellToAddId);

            data.IsProc = false;
            if (spellToAddlevel1Id != SpellId.Undef && (MeleeSpells.meleeProcs.FirstOrDefault(x => x.result == spellToAddlevel1Id) != default((SpellId, float)) || MissileSpells.missileProcs.FirstOrDefault(x => x.result == spellToAddlevel1Id) != default((SpellId, float))))
            {
                data.IsProc = true;

                if (data.Target.ItemType != ItemType.MeleeWeapon && data.Target.ItemType != ItemType.MissileWeapon)
                {
                    data.Result = InjectSpellResult.TargetCannotContainSpell;
                    return data;
                }
            }

            data.IsGem = false;
            if (data.SpellToAdd.School == MagicSchool.ItemEnchantment && data.Target.ResistMagic >= 9999)
            {
                data.Result = InjectSpellResult.TargetCannotContainSpell;
                return data;
            }
            else if (data.Target.ItemType == ItemType.Gem)
            {
                data.IsGem = true;
                if (data.SpellToAdd.IsCantrip || data.SpellToAdd.School == MagicSchool.ItemEnchantment)
                {
                    data.Result = InjectSpellResult.TargetCannotContainSpell;
                    return data;
                }
            }
            else if (data.Target.ItemType == ItemType.MeleeWeapon || data.Target.ItemType == ItemType.MissileWeapon || data.Target.ItemType == ItemType.Caster)
            {
                if (data.SpellToAdd.IsImpenBaneType && spellToAddlevel1Id != SpellId.Brittlemail1)
                {
                    data.Result = InjectSpellResult.TargetCannotContainSpell;
                    return data;
                }
            }
            else if (data.SpellToAdd.IsWeaponTargetType)
            {
                data.Result = InjectSpellResult.TargetCannotContainSpell;
                return data;
            }

            var spellsOnItem = data.Target.Biota.GetKnownSpellsIds(data.Target.BiotaDatabaseLock).Select(i => (SpellId)i).ToList();

            if (data.Target.SpellDID.HasValue && data.Target.SpellDID != 0)
                spellsOnItem.Add((SpellId)data.Target.SpellDID);

            if (data.Target.ProcSpell.HasValue && data.Target.ProcSpell != 0)
                spellsOnItem.Add((SpellId)data.Target.ProcSpell);

            data.AllSpells = new List<SpellId>();
            data.LifeCreatureEnchantments = new List<SpellId>();
            data.Cantrips = new List<SpellId>();

            data.AllSpells.Add(spellToAddId);
            if (!data.IsProc)
            {
                if (data.SpellToAdd.IsCantrip)
                    data.Cantrips.Add(spellToAddId);
                else if (data.SpellToAdd.School == MagicSchool.CreatureEnchantment || data.SpellToAdd.School == MagicSchool.LifeMagic)
                    data.LifeCreatureEnchantments.Add(spellToAddId);
            }

            var extraSpells = data.Target.ExtraSpellsList != null ? data.Target.ExtraSpellsList.Split(",").ToList() : new List<string>();

            Spell spellToReplace = null;
            if (data.IsGem && data.Target.SpellDID != null)
                spellToReplace = new Spell(data.Target.SpellDID ?? 0);
            else if (data.IsProc && data.Target.ProcSpell != null)
                spellToReplace = new Spell(data.Target.ProcSpell ?? 0);

            foreach (var spellOnItemId in spellsOnItem)
            {
                data.SpellOnItem = new Spell(spellOnItemId);

                // For items that have a base difficulty override we will only calculate new arcane lore requirements based on the extra spells so filter them here.
                if (!data.Target.BaseItemDifficultyOverride.HasValue || extraSpells.Contains(spellOnItemId.ToString()))
                {
                    data.AllSpells.Add(spellOnItemId);
                    if ((!data.Target.SpellDID.HasValue || data.Target.SpellDID != (uint)spellOnItemId) && (!data.Target.ProcSpell.HasValue || data.Target.ProcSpell != (uint)spellOnItemId))
                    {
                        if (data.SpellOnItem.IsCantrip)
                            data.Cantrips.Add(spellOnItemId);
                        else if (data.SpellOnItem.School == MagicSchool.CreatureEnchantment || data.SpellOnItem.School == MagicSchool.LifeMagic)
                            data.LifeCreatureEnchantments.Add(spellOnItemId);
                    }
                }

                if (spellOnItemId == spellToAddId)
                {
                    data.Result = InjectSpellResult.TargetAlreadyContainsSpell;
                    return data;
                }
                else if (spellToReplace == null && data.SpellOnItem.Category == data.SpellToAdd.Category)
                {
                    if (data.SpellOnItem.Power > data.SpellToAdd.Power)
                    {
                        data.Result = InjectSpellResult.TargetContainsStrongerSpell;
                        return data;
                    }
                    else if (data.SpellOnItem.Power == data.SpellToAdd.Power)
                    {
                        data.Result = InjectSpellResult.TargetContainsEquivalentSpell;
                        return data;
                    }
                    else
                        spellToReplace = data.SpellOnItem;
                }
            }

            if (spellToReplace != null)
            {
                data.AllSpells.Remove((SpellId)spellToReplace.Id);
                data.LifeCreatureEnchantments.Remove((SpellId)spellToReplace.Id);
                data.Cantrips.Remove((SpellId)spellToReplace.Id);
                data.SpellToReplace = spellToReplace;
            }

            RemoveTinkerSpellsFromList(data.Target.TinkerLog, data.AllSpells);
            RemoveTinkerSpellsFromList(data.Target.TinkerLog, data.LifeCreatureEnchantments);
            RemoveTinkerSpellsFromList(data.Target.TinkerLog, data.Cantrips);

            if (spellToReplace == null && (data.Target.ExtraSpellsCount ?? 0) >= data.Target.GetMaxExtraSpellsCount())
            {
                data.Result = InjectSpellResult.TargetCannotContainMoreSpells;
                return data;
            }

            if (requireConfirmation)
            {
                if (!confirmed)
                {
                    var extraMessage = "";
                    if (spellToReplace != null)
                        extraMessage = $"\nThis will replace {spellToReplace.Name}!\n";

                    LootGenerationFactory.CalculateSpellcraft(data.Target, data.AllSpells, false, out var minSpellcraft, out var maxSpellcraft, out var rolledSpellCraft);
                    LootGenerationFactory.CalculateArcaneLore(data.Target, data.AllSpells, data.LifeCreatureEnchantments, data.Cantrips, minSpellcraft, maxSpellcraft, rolledSpellCraft, false, out var minArcane, out var maxArcane, out _);
                    var estimateMessage = minArcane != maxArcane ? $"The new Arcane Lore requirement will be between {minArcane} and {maxArcane}." : $"The new Arcane Lore requirement will be {minArcane}.";

                    data.ConfirmationMessage = $"Transferring {data.SpellToAdd.Name} to {data.Target.NameWithMaterial}.\n{(extraMessage.Length > 0 ? extraMessage : "")}\n{estimateMessage}\n\n";
                    data.Result = InjectSpellResult.RequiresConfirmation;
                    return data;
                }
                else
                {
                    data.Result = InjectSpellResult.ReadyToProceed;
                    return data;
                }
            }
            else
                return ContinueInjectSpell(data);
        }

        private static InjectSpellData ContinueInjectSpell(InjectSpellData data)
        {
            if (data.IsProc)
            {
                HandleExtraSpellList(data.Target, (SpellId)data.SpellToAdd.Id, (SpellId)(data.Target.ProcSpell ?? 0));

                data.Target.ProcSpellRate = 0.15f;
                data.Target.ProcSpell = data.SpellToAdd.Id;
                data.Target.ProcSpellSelfTargeted = data.SpellToAdd.IsSelfTargeted;
            }
            else if (data.IsGem)
            {
                HandleExtraSpellList(data.Target, (SpellId)data.SpellToAdd.Id, (SpellId)(data.Target.SpellDID ?? 0));

                data.Target.SpellDID = data.SpellToAdd.Id;
            }
            else
            {
                if (data.SpellToReplace != null)
                {
                    HandleExtraSpellList(data.Target, (SpellId)data.SpellToAdd.Id, (SpellId)data.SpellToReplace.Id);
                    data.Target.Biota.TryRemoveKnownSpell((int)data.SpellToReplace.Id, data.Target.BiotaDatabaseLock);
                }
                else
                    HandleExtraSpellList(data.Target, (SpellId)data.SpellToAdd.Id);
                data.Target.Biota.GetOrAddKnownSpell((int)data.SpellToAdd.Id, data.Target.BiotaDatabaseLock, out _);
            }

            var newMaxBaseMana = LootGenerationFactory.GetMaxBaseMana(data.Target);
            var newManaRate = LootGenerationFactory.CalculateManaRate(newMaxBaseMana);
            var newMaxMana = (int)data.SpellToAdd.BaseMana * 15;

            if (data.Target.TinkerLog != null)
            {
                var tinkers = data.Target.TinkerLog.Split(",");

                var appliedMoonstoneCount = tinkers.Count(s => s == "31");
                for (int i = 0; i < appliedMoonstoneCount; i++)
                {
                    var currentMana = newMaxMana;

                    newMaxMana = currentMana * 2;
                    if (newMaxMana - currentMana < 500)
                        newMaxMana = currentMana + 500;
                }

                var appliedSilverCount = tinkers.Count(s => s == "63");
                for (int i = 0; i < appliedSilverCount; i++)
                {
                    newManaRate *= 2.0f;
                }

                var appliedPyrealCount = tinkers.Count(s => s == "62");
                for (int i = 0; i < appliedPyrealCount; i++)
                {
                    newManaRate *= 0.5f;
                }
            }

            if (data.IsGem)
            {
                data.Target.ItemUseable = Usable.Contained;

                if (!data.Target.MaxStructure.HasValue)
                {
                    data.Target.MaxStructure = LootGenerationFactory.RollItemMaxStructure(data.Target);
                    data.Target.Structure = data.Target.MaxStructure;
                }

                data.Target.Structure = data.Target.MaxStructure;
                var baseWeenie = DatabaseManager.World.GetCachedWeenie(data.Target.WeenieClassId);
                if (baseWeenie != null)
                {
                    data.Target.Name = baseWeenie.GetName(); // Reset to base name before rebuilding suffix.
                    data.Target.LongDesc = LootGenerationFactory.GetLongDesc(data.Target);
                    data.Target.Name = data.Target.LongDesc;
                }
            }
            else
            {
                if (newMaxMana > (data.Target.ItemMaxMana ?? 0))
                {
                    data.Target.ItemMaxMana = newMaxMana;
                    data.Target.ItemCurMana = Math.Clamp(data.Target.ItemCurMana ?? 0, 0, data.Target.ItemMaxMana ?? 0);
                    data.Target.ManaRate = newManaRate;
                }                    

                data.Target.LongDesc = LootGenerationFactory.GetLongDesc(data.Target);
            }

            if (data.SpellToReplace == null || (data.IsProc && data.Target.ProcSpell == null))
                data.Target.ExtraSpellsCount = (data.Target.ExtraSpellsCount ?? 0) + 1;

            LootGenerationFactory.CalculateSpellcraft(data.Target, data.AllSpells, true, out var minSpellcraft, out var maxSpellcraft, out var rolledSpellCraft);
            LootGenerationFactory.CalculateArcaneLore(data.Target, data.AllSpells, data.LifeCreatureEnchantments, data.Cantrips, minSpellcraft, maxSpellcraft, rolledSpellCraft, true, out _, out _, out _);

            if (!data.Target.UiEffects.HasValue) // Elemental effects take precendence over magical as it is more important to know the element of a weapon than if it has spells.
                data.Target.UiEffects = ACE.Entity.Enum.UiEffects.Magical;

            data.Result = InjectSpellResult.Success;
            return data;
        }

        private static void HandleExtraSpellList(WorldObject target, SpellId newSpellId, SpellId replacementForSpellId = 0)
        {
            var spellList = new List<uint>();

            if (target.ExtraSpellsList != null)
            {
                var entries = target.ExtraSpellsList.Split(',');
                foreach (var entry in entries)
                {
                    if (uint.TryParse(entry, out var value))
                        spellList.Add(value);
                    else
                    {
                        log.Error($"HandleExtraSpellList() - Could not parse spellId \"{entry}\" in {target.Name}({target.Guid}) ExtraSpellList");
                        continue;
                    }
                }
            }

            if (replacementForSpellId != 0)
                spellList.Remove((uint)replacementForSpellId);
            spellList.Add((uint)newSpellId);

            if (spellList.Count > 0)
                target.ExtraSpellsList = string.Join(",", spellList);
            else
                target.ExtraSpellsList = null;
        }
    }
}
