using System;

using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects.Entity
{
    public class CreatureSkill
    {
        private readonly Creature creature;

        public readonly Skill Skill;

        // The underlying database record
        public readonly PropertiesSkill PropertiesSkill;

        public CreatureSkill(Creature creature, Skill skill, PropertiesSkill propertiesSkill)
        {
            this.creature = creature;
            Skill = skill;
            this.PropertiesSkill = propertiesSkill;
        }

        /// <summary>
        /// A bonus from character creation: +5 for trained, +10 for specialized
        /// </summary>
        public uint InitLevel
        {
            get => PropertiesSkill.InitLevel;
            set => PropertiesSkill.InitLevel = value;
        }

        public SkillAdvancementClass AdvancementClass
        {
            get => PropertiesSkill.SAC;
            set
            {
                if (PropertiesSkill.SAC != value)
                    creature.ChangesDetected = true;

                PropertiesSkill.SAC = value;
            }
        }

        public bool IsUsable
        {
            get
            {
                if (AdvancementClass == SkillAdvancementClass.Trained || AdvancementClass == SkillAdvancementClass.Specialized)
                    return true;

                if (AdvancementClass == SkillAdvancementClass.Untrained)
                {
                    DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)Skill, out var skillTableRecord);

                    if (skillTableRecord?.MinLevel == 1)
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The amount of experience put into this skill,
        /// from raising directly and earned through use
        /// </summary>
        public uint ExperienceSpent
        {
            get => PropertiesSkill.PP;
            set
            {
                if (PropertiesSkill.PP != value)
                    creature.ChangesDetected = true;

                PropertiesSkill.PP = value;
            }
        }

        /// <summary>
        /// Returns the amount of skill experience remaining
        /// until max rank is reached
        /// </summary>
        public uint ExperienceLeft
        {
            get
            {
                var skillXPTable = Player.GetSkillXPTable(AdvancementClass);
                if (skillXPTable == null)
                    return 0;

                // a player can actually have negative experience remaining,
                // if they had a Trained skill maxed, and then specialized it in skill temple afterwards.

                // (confirmed this is how it was in retail)

                var remainingXP = (long)skillXPTable[skillXPTable.Count - 1] - ExperienceSpent;

                return (uint)Math.Max(0, remainingXP);
            }
        }

        /// <summary>
        /// The number of levels a skill has been raised,
        /// derived from ExperienceSpent
        /// </summary>
        public ushort Ranks
        {
            get => PropertiesSkill.LevelFromPP;
            set
            {
                if (PropertiesSkill.LevelFromPP != value)
                    creature.ChangesDetected = true;

                PropertiesSkill.LevelFromPP = value;

                if (!IsSecondary && creature != null && creature.Skills != null)
                {
                    foreach (var entry in creature.Skills)
                    {
                        if (entry.Value.SecondaryTo == Skill)
                            entry.Value.UpdateSecondarySkill(value, PropertiesSkill.InitLevel);
                    }
                }
            }
        }

        /// <summary>
        /// Returns TRUE if this skill has been raised the maximum # of times
        /// </summary>
        public bool IsMaxRank
        {
            get
            {
                var skillXPTable = Player.GetSkillXPTable(AdvancementClass);
                if (skillXPTable == null)
                    return false;

                return Ranks >= (skillXPTable.Count - 1);
            }
        }

        public uint Base
        {
            get
            {
                uint total = 0;

                if (IsUsable)
                    total = AttributeFormula.GetFormula(creature, Skill, false);

                total += InitLevel + Ranks;

                if (creature is Player player)
                    total += GetAugBonus_Base(player);
                else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && Skill == Skill.ArcaneLore) // Custom Arcane Lore formula.
                    total += ((uint)creature.Level / 2) + 10;

                return total;
            }
        }

        public uint Current
        {
            get
            {
                uint total = 0;

                if (IsUsable)
                    total = AttributeFormula.GetFormula(creature, Skill);

                total += InitLevel + Ranks;

                var player = creature as Player;

                // base gets scaled by vitae
                if (player != null)
                    total += GetAugBonus_Base(player);
                else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && Skill == Skill.ArcaneLore) // Custom Arcane Lore formula.
                    total += ((uint)creature.Level / 2) + 10;

                // apply multiplicative enchantments
                var multiplier = creature.EnchantmentManager.GetSkillMod_Multiplier(Skill);

                var fTotal = total * multiplier;

                if (player != null)
                {
                    var vitae = player.Vitae;

                    if (vitae != 1.0f)
                        fTotal *= vitae;

                    // everything beyond this point does not get scaled by vitae
                    fTotal += GetAugBonus_Current(player);
                }

                var additives = creature.EnchantmentManager.GetSkillMod_Additives(Skill);

                var iTotal = (fTotal + additives).Round();

                iTotal = Math.Max(iTotal, 0);   // skill level cannot be debuffed below 0

                return (uint)iTotal;
            }
        }

        public bool IsSecondary
        {
            get => Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && PropertiesSkill.SecondaryTo != 0;
        }

        public Skill SecondaryTo
        {
            get => PropertiesSkill.SecondaryTo;
            set
            {
                PropertiesSkill.SecondaryTo = value;
                UpdateSecondarySkill();
            }
        }

        public void UpdateSecondarySkill()
        {
            if (SecondaryTo == 0 || creature == null || !DatManager.PortalDat.SkillTable.SkillBaseHash.ContainsKey((uint)SecondaryTo))
            {
                UpdateSecondarySkill(0, 0);
                return;
            }

            var primary = creature.GetCreatureSkill(SecondaryTo, false);
            if (primary == null)
                return;

            UpdateSecondarySkill(primary.Ranks, primary.InitLevel);
        }

        private void UpdateSecondarySkill(ushort primaryRanks, uint primaryInitLevel)
        {
            if (Skill == Skill.None)
                return;

            var oldValue = Ranks;

            if (SecondaryTo == 0 || creature == null)
            {
                Ranks = 0;
                if (AdvancementClass == SkillAdvancementClass.Specialized)
                    InitLevel = 10;
                else if (AdvancementClass == SkillAdvancementClass.Trained)
                    InitLevel = 5;
                else
                    InitLevel = 0;
            }
            else
            {
                InitLevel = primaryInitLevel;

                if (AdvancementClass == SkillAdvancementClass.Specialized)
                    Ranks = (ushort)(primaryRanks > 5 ? primaryRanks - 5 : 0);
                else
                    Ranks = (ushort)(primaryRanks > 10 ? primaryRanks - 10 : 0);
            }

            if (Ranks != oldValue && creature is Player player)
            {
                if(Ranks != 0)
                    ExperienceSpent = (uint)player.GetXPBetweenSkillLevels(AdvancementClass, 0, Ranks);
                else
                    ExperienceSpent = 0;

                player.ChangesDetected = true;

                // Delay sending theses message for one tick so they appear after the original skill message.
                if (SecondaryTo != 0)
                {
                    var sendMessageChain = new ActionChain();
                    sendMessageChain.AddDelayForOneTick();

                    sendMessageChain.AddAction(player, () =>
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, this));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your base {Skill.ToSentence()} secondary skill is now {Base}!", ChatMessageType.Advancement));
                    });

                    sendMessageChain.EnqueueChain();
                }
                else
                {
                    var sendMessageChain = new ActionChain();
                    sendMessageChain.AddDelayForOneTick();

                    sendMessageChain.AddAction(player, () =>
                    {
                        player.Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(player, this));
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your base {Skill.ToSentence()} skill is now {Base}!", ChatMessageType.Advancement));
                    });

                    sendMessageChain.EnqueueChain();
                }
            }
        }

        public uint GetAugBonus_Base(Player player)
        {
            // TODO: verify which of these are base, and which are current
            uint total = 0;

            if (player.LumAugAllSkills != 0)
                total += (uint)player.LumAugAllSkills;

            if (player.AugmentationSkilledMelee > 0 && Player.MeleeSkills.Contains(Skill))
                total += (uint)(player.AugmentationSkilledMelee * 10);
            else if (player.AugmentationSkilledMissile > 0 && Player.MissileSkills.Contains(Skill))
                total += (uint)(player.AugmentationSkilledMissile * 10);
            else if (player.AugmentationSkilledMagic > 0 && Player.MagicSkills.Contains(Skill))
                total += (uint)(player.AugmentationSkilledMagic * 10);

            //switch (Skill)
            //{
            //    case Skill.ArmorTinkering:
            //    case Skill.ItemTinkering:
            //    case Skill.MagicItemTinkering:
            //    case Skill.WeaponTinkering:
            //    case Skill.Salvaging:

            //        if (player.LumAugSkilledCraft != 0)
            //            total += (uint)player.LumAugSkilledCraft;
            //        break;
            //}

            if (AdvancementClass >= SkillAdvancementClass.Trained && player.Enlightenment != 0)
                total += (uint)player.Enlightenment;

            return total;
        }

        public uint GetAugBonus_Current(Player player)
        {
            // TODO: verify which of these are base, and which are current
            uint total = 0;

            if (player.AugmentationJackOfAllTrades != 0)
                total += (uint)(player.AugmentationJackOfAllTrades * 5);

            if (AdvancementClass == SkillAdvancementClass.Specialized && player.LumAugSkilledSpec != 0)
                total += (uint)player.LumAugSkilledSpec * 2;

            return total;
        }
    }
}
