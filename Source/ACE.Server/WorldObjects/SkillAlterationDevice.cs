using System;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Command.Handlers;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public class SkillAlterationDevice : WorldObject
    {
        public enum SkillAlterationType
        {
            Undef               = 0,
            Specialize          = 1,
            Lower               = 2,
            SetPrimary          = 3,
            SetSecondary        = 4,
            ResetLeyLineSeed    = 5,
        }

        public SkillAlterationType TypeOfAlteration
        {
            get => (SkillAlterationType)(GetProperty(PropertyInt.TypeOfAlteration) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.TypeOfAlteration); else SetProperty(PropertyInt.TypeOfAlteration, (int)value); }
        }

        public Skill SkillToBeAltered
        {
            get => (Skill)(GetProperty(PropertyInt.SkillToBeAltered) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.SkillToBeAltered); else SetProperty(PropertyInt.SkillToBeAltered, (int)value); }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public SkillAlterationDevice(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public SkillAlterationDevice(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        public override void ActOnUse(WorldObject activator)
        {
            ActOnUse(activator, false);
        }

        public void ActOnUse(WorldObject activator, bool confirmed)
        {
            if (!(activator is Player player))
                return;

            CreatureSkill skill = null;
            SkillBase skillBase = null;

            // verify skill
            skill = player.GetCreatureSkill(SkillToBeAltered);

            if (TypeOfAlteration != SkillAlterationType.ResetLeyLineSeed)
            {

                if (skill == null || !DatManager.PortalDat.SkillTable.SkillBaseHash.ContainsKey((uint)skill.Skill))
                {
                    player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouFailToAlterSkill));
                    return;
                }

                // get skill training / specialization costs
                skillBase = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)skill.Skill];
            }

            if (!VerifyRequirements(player, skill, skillBase))
                return;

            if (!confirmed)
            {
                var msg = "This action will ";
                switch (TypeOfAlteration)
                {
                    case SkillAlterationType.Specialize:
                        msg += $"specialize your {skill.Skill.ToSentence()} skill and cost {skillBase.UpgradeCostFromTrainedToSpecialized} credits.";
                        break;
                    case SkillAlterationType.Lower:
                        msg += $"lower your {skill.Skill.ToSentence()} skill from {(skill.AdvancementClass == SkillAdvancementClass.Specialized ? "specialized to trained" : "trained to untrained")} and refund the skill credits and experience invested in this skill.";
                        break;
                    case SkillAlterationType.SetPrimary:
                        msg += $"set your {skill.Skill.ToSentence()} skill as a primary skill.";
                        break;
                    case SkillAlterationType.SetSecondary:
                        msg += $"set your {skill.Skill.ToSentence()} skill as a secondary of {GetHighestValidPrimarySkill(player).Skill.ToSentence()} and refund the experience invested in this skill.";
                        break;
                    case SkillAlterationType.ResetLeyLineSeed:
                        msg += $"reset your ley line alignment so menhir rings will attune different spells.";
                        break;
                }

                if (!player.ConfirmationManager.EnqueueSend(new Confirmation_AlterSkill(player.Guid, Guid), msg))
                    player.SendWeenieError(WeenieError.ConfirmationInProgress);

                return;
            }

            AlterSkill(player, skill, skillBase);
        }

        private CreatureSkill GetHighestValidPrimarySkill(Player player)
        {
            CreatureSkill highestSkill;

            var axe = player.GetCreatureSkill(Skill.Axe);
            var dagger = player.GetCreatureSkill(Skill.Dagger);
            //var mace = player.GetCreatureSkill(Skill.Mace);
            var spear = player.GetCreatureSkill(Skill.Spear);
            //var staff = player.GetCreatureSkill(Skill.Staff);
            var sword = player.GetCreatureSkill(Skill.Sword);
            var unarmed = player.GetCreatureSkill(Skill.UnarmedCombat);
            var bow = player.GetCreatureSkill(Skill.Bow);
            //var crossbow = player.GetCreatureSkill(Skill.Crossbow);
            var thrown = player.GetCreatureSkill(Skill.ThrownWeapon);

            highestSkill = axe;
            if (dagger.Current > highestSkill.Current)
                highestSkill = dagger;
            //if (mace.Current > highestSkill.Current)
            //    highestSkill = mace;
            if (spear.Current > highestSkill.Current)
                highestSkill = spear;
            //if (staff.Current > highestSkill.Current)
            //    highestSkill = staff;
            if (sword.Current > highestSkill.Current)
                highestSkill = sword;
            if (unarmed.Current > highestSkill.Current)
                highestSkill = unarmed;
            if (bow.Current > highestSkill.Current)
                highestSkill = bow;
            //if (crossbow.Current > highestSkill.Current)
            //    highestSkill = crossbow;
            if (thrown.Current > highestSkill.Current)
                highestSkill = thrown;

            return highestSkill;
        }

        public bool VerifyRequirements(Player player, CreatureSkill skill, SkillBase skillBase)
        {
            switch (TypeOfAlteration)
            {
                // Gem of Enlightenment
                case SkillAlterationType.Specialize:

                    // ensure skill is trained
                    if (skill.AdvancementClass != SkillAdvancementClass.Trained)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.Your_SkillMustBeTrained, skill.Skill.ToSentence()));
                        return false;
                    }

                    // ensure player has enough available skill credits
                    if (player.AvailableSkillCredits < skillBase.UpgradeCostFromTrainedToSpecialized)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.NotEnoughSkillCreditsToSpecialize, skill.Skill.ToSentence()));
                        return false;
                    }

                    // ensure player won't exceed limit of 70 specialized credits after operation
                    var specializedCost = skillBase.SpecializedCost;

                    if (DatManager.PortalDat.CharGen.HeritageGroups.TryGetValue((uint)player.Heritage, out var heritageGroup))
                    {
                        // check for adjusted costs of Specialization due to player's heritage (e.g. Arcane Lore)
                        var heritageAdjustedCost = heritageGroup.Skills.FirstOrDefault(i => i.SkillNum == (int)skill.Skill);

                        if (heritageAdjustedCost != null)
                            specializedCost = heritageAdjustedCost.PrimaryCost;
                    }

                    if (GetTotalSpecializedCredits(player) + specializedCost > 70)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.TooManyCreditsInSpecializedSkills, skill.Skill.ToSentence()));
                        return false;
                    }
                    break;

                // Gem of Forgetfulness
                case SkillAlterationType.Lower:

                    // ensure skill is trained or specialized
                    if (skill.AdvancementClass < SkillAdvancementClass.Trained)
                    {
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.Your_SkillIsAlreadyUntrained, skill.Skill.ToSentence()));
                        return false;
                    }

                    // Check for equipped items that have requirements in the skill we're lowering
                    if (CheckWieldedItems(player))
                    {
                        // Items are wielded which might be affected by a lowering operation
                        player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.CannotLowerSkillWhileWieldingItem, skill.Skill.ToSentence()));
                        return false;
                    }

                    break;

                case SkillAlterationType.SetPrimary:

                    // ensure skill is trained or specialized
                    if (skill.AdvancementClass < SkillAdvancementClass.Trained)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill must be trained or specialized in order to be altered in this way!", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    if(!skill.IsSecondary)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill is already set as a primary skill!", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    break;

                case SkillAlterationType.SetSecondary:

                    // ensure skill is trained or specialized
                    if (skill.AdvancementClass < SkillAdvancementClass.Trained)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill must be trained or specialized in order to be altered in this way!", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    // ensure not already secondary
                    if (skill.IsSecondary)
                    {
                        var currentPrimarySkill = player.GetCreatureSkill(skill.SecondaryTo);
                        if (currentPrimarySkill != null)
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill is already a secondary skill of {currentPrimarySkill.Skill.ToSentence()} skill!", ChatMessageType.WorldBroadcast));
                        else
                            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill is already a secondary skill!", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    // ensure primary skill is trained or specialized
                    var primarySkill = GetHighestValidPrimarySkill(player);
                    if (primarySkill.AdvancementClass < SkillAdvancementClass.Trained)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {primarySkill.Skill.ToSentence()} skill must be trained or specialized in order to be altered in this way!", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    // ensure not same skill
                    if (primarySkill.Skill == skill.Skill)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your highest primary skill cannot be set as a secondary.", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    // ensure no secondaries
                    bool hasSecondary = false;
                    foreach (var entry in player.Skills)
                    {
                        if (entry.Value.SecondaryTo == skill.Skill)
                        {
                            hasSecondary = true;
                            break;
                        }
                    }
                    if (hasSecondary)
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Cannot set a skill with secondaries as a secondary.", ChatMessageType.WorldBroadcast));
                        return false;
                    }

                    break;

            }
            return true;
        }

        public void AlterSkill(Player player, CreatureSkill skill, SkillBase skillBase)
        {
            switch (TypeOfAlteration)
            {
                // Gem of Enlightenment
                case SkillAlterationType.Specialize:

                    if (player.SpecializeSkill(skill.Skill, skillBase.UpgradeCostFromTrainedToSpecialized, false))
                    {
                        var updateSkill = new GameMessagePrivateUpdateSkill(player, skill);
                        var availableSkillCredits = new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0);
                        var msg = new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.YouHaveSucceededSpecializing_Skill, skill.Skill.ToSentence());

                        player.Session.Network.EnqueueSend(updateSkill, availableSkillCredits, msg);

                        player.TryConsumeFromInventoryWithNetworking(this, 1);
                    }
                    break;

                // Gem of Forgetfulness
                case SkillAlterationType.Lower:

                    // specialized => trained
                    if (skill.AdvancementClass == SkillAdvancementClass.Specialized)
                    {
                        var specializedViaAugmentation = player.IsSkillSpecializedViaAugmentation(skill.Skill, out var playerHasAugmentation) && playerHasAugmentation;

                        if (player.UnspecializeSkill(skill.Skill, skillBase.UpgradeCostFromTrainedToSpecialized))
                        {
                            var updateSkill = new GameMessagePrivateUpdateSkill(player, skill);
                            var availableSkillCredits = new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0);
                            var msg = specializedViaAugmentation ? WeenieErrorWithString.YouSucceededRecoveringXPFromSkill_AugmentationNotUntrainable : WeenieErrorWithString.YouHaveSucceededUnspecializing_Skill;
                            var message = new GameEventWeenieErrorWithString(player.Session, msg, skill.Skill.ToSentence());

                            player.Session.Network.EnqueueSend(updateSkill, availableSkillCredits, message);

                            player.TryConsumeFromInventoryWithNetworking(this, 1);
                        }
                    }

                    // trained => untrained
                    // in the case of skills which can't be untrained,
                    // keep trained, but recover the xp spent
                    else if (skill.AdvancementClass == SkillAdvancementClass.Trained)
                    {
                        var untrainable = Player.IsSkillUntrainable(skill.Skill, player.HeritageGroup);

                        if (player.UntrainSkill(skill.Skill, skillBase.TrainedCost))
                        {
                            var updateSkill = new GameMessagePrivateUpdateSkill(player, skill);
                            var availableSkillCredits = new GameMessagePrivateUpdatePropertyInt(player, PropertyInt.AvailableSkillCredits, player.AvailableSkillCredits ?? 0);
                            var msg = untrainable ? WeenieErrorWithString.YouHaveSucceededUntraining_Skill : WeenieErrorWithString.CannotUntrain_SkillButRecoveredXP;
                            var message = new GameEventWeenieErrorWithString(player.Session, msg, skill.Skill.ToSentence());

                            player.Session.Network.EnqueueSend(updateSkill, availableSkillCredits, message);

                            player.TryConsumeFromInventoryWithNetworking(this, 1);
                        }
                    }
                    break;

                case SkillAlterationType.SetPrimary:

                    skill.SecondaryTo = Skill.None;
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill is now set as a primary skill!", ChatMessageType.WorldBroadcast));
                    player.TryConsumeFromInventoryWithNetworking(this, 1);
                    break;

                case SkillAlterationType.SetSecondary:

                    var primarySkill = GetHighestValidPrimarySkill(player);
                    if (skill.Ranks != 0)
                    {
                        player.RefundXP(skill.ExperienceSpent);
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You have been refunded {skill.ExperienceSpent:N0} experience.", ChatMessageType.WorldBroadcast));
                    }
                    skill.SecondaryTo = primarySkill.Skill;
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {skill.Skill.ToSentence()} skill is now set as secondary of {primarySkill.Skill.ToSentence()} skill!", ChatMessageType.WorldBroadcast));
                    player.TryConsumeFromInventoryWithNetworking(this, 1);
                    break;

                case SkillAlterationType.ResetLeyLineSeed:

                    player.LeyLineSeed = 0;
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your ley line attunement has changed!", ChatMessageType.WorldBroadcast));
                    player.TryConsumeFromInventoryWithNetworking(this, 1);
                    break;

            }
        }

        /// <summary>
        /// Calculates and returns the current total number of specialized credits
        /// </summary>
        private int GetTotalSpecializedCredits(Player player)
        {
            var specializedCreditsTotal = 0;

            foreach (var kvp in player.Skills)
            {
                if (!DatManager.PortalDat.SkillTable.SkillBaseHash.ContainsKey((uint)kvp.Key))
                    continue;

                if (kvp.Value.AdvancementClass == SkillAdvancementClass.Specialized)
                {
                    switch (kvp.Key)
                    {
                        // exclude None/Undef skill
                        case Skill.None:

                        // exclude aug specs
                        case Skill.ArmorTinkering:
                        case Skill.ItemTinkering:
                        case Skill.MagicItemTinkering:
                        case Skill.WeaponTinkering:
                        case Skill.Salvaging:
                            continue;
                    }

                    var skill = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)kvp.Key];

                    var specializedCost = skill.SpecializedCost;

                    if (DatManager.PortalDat.CharGen.HeritageGroups.TryGetValue((uint)player.Heritage, out var heritageGroup))
                    {
                        // check for adjusted costs of Specialization due to player's heritage (e.g. Arcane Lore)
                        var heritageAdjustedCost = heritageGroup.Skills.FirstOrDefault(i => i.SkillNum == (int)kvp.Key);

                        if (heritageAdjustedCost != null)
                            specializedCost = heritageAdjustedCost.PrimaryCost;
                    }
                    specializedCreditsTotal += specializedCost;
                }
            }

            return specializedCreditsTotal;
        }

        /// <summary>
        /// Checks wielded items and their requirements to see if they'd be violated by an impending skill lowering operation
        /// </summary>
        private bool CheckWieldedItems(Player player)
        {
            foreach (var equippedItem in player.EquippedObjects.Values)
            {
                if (CheckWieldRequirement(player, equippedItem.WieldRequirements, equippedItem.WieldSkillType) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements2, equippedItem.WieldSkillType2) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements3, equippedItem.WieldSkillType3) ||
                    CheckWieldRequirement(player, equippedItem.WieldRequirements4, equippedItem.WieldSkillType4))
                {
                    return true;
                }
            }
            return false;
        }

        private bool CheckWieldRequirement(Player player, WieldRequirement itemWieldReq, int? wieldSkillType)
        {
            if (itemWieldReq != WieldRequirement.RawSkill && itemWieldReq != WieldRequirement.Skill)
                return false;

            return player.ConvertToMoASkill((Skill)(wieldSkillType ?? 0)) == SkillToBeAltered;
        }
    }
}
