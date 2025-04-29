using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using System;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void MutateArmor(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureArmorType armorType, TreasureRoll roll = null)
        {
            // material type
            var materialType = GetMaterialType(wo, profile.Tier);
            if (materialType > 0)
                wo.MaterialType = materialType;

            // item color
            MutateColor(wo);

            // gem count / gem material
            if (wo.GemCode != null)
                wo.GemCount = GemCountChance.Roll(wo.GemCode.Value, profile.Tier);
            else
                wo.GemCount = ThreadSafeRandom.Next(1, 6);

            wo.GemType = RollGemType(profile.Tier);

            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier, profile.LootQualityMod);

            // burden
            if (wo.HasMutateFilter(MutateFilter.EncumbranceVal))  // fixme: data
                MutateBurden(wo, profile, false);

            if (profile.Tier > 6 && !wo.HasArmorLevel())
            {
                // normally this is handled in the mutation script for armor
                // for clothing, just calling the generic method here
                RollWieldLevelReq_T7_T8(wo, profile);
            }

            AssignArmorLevel(wo, profile, roll, armorType);

            if (wo.HasMutateFilter(MutateFilter.ArmorModVsType))
                MutateArmorModVsType(wo, profile);

            if (isMagical)
            {
                AssignMagic(wo, profile, roll);
            }
            else
            {
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
            }

            if (profile.Tier > 6 && armorType != TreasureArmorType.Society)
                TryRollEquipmentSet(wo, profile, roll);

            if (roll != null && profile.Tier == 8)
                TryMutateGearRating(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))   // fixme: data
                MutateValue(wo, profile.Tier, roll);

            wo.LongDesc = GetLongDesc(wo);
        }

        private static bool AssignArmorLevel(WorldObject wo, TreasureDeath profile, TreasureRoll roll, TreasureArmorType armorType)
        {
            // retail was only divied up into a few different mutation scripts here
            // anything with ArmorLevel ran these mutation scripts
            // anything that covered extremities (head / hand / foot wear) started with a slightly higher base AL,
            // but otherwise used the same mutation as anything that covered non-extremities
            // shields also had their own mutation script

            // only exceptions found: covenant armor, olthoi armor, metal cap

            if (!roll.HasArmorLevel(wo))
                return false;

            var scriptName = GetMutationScript_ArmorLevel(wo, roll);

            if (scriptName == null)
            {
                log.Error($"AssignArmorLevel({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - unknown item type");
                return false;
            }

            // persist original values for society armor
            var wieldRequirements = wo.WieldRequirements;
            var wieldSkillType = wo.WieldSkillType;
            var wieldDifficulty = wo.WieldDifficulty;

            //Console.WriteLine($"Mutating {wo.Name} with {scriptName}");

            var mutationFilter = MutationCache.GetMutation(scriptName);

            var success = mutationFilter.TryMutate(wo, profile.Tier);

            if (armorType == TreasureArmorType.Society)
            {
                wo.WieldRequirements = wieldRequirements;
                wo.WieldSkillType = wieldSkillType;
                wo.WieldDifficulty = wieldDifficulty;
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                ReplaceArmorLevelRequirements(wo, profile.Tier);

            return success;
        }

        public static void ReplaceArmorLevelRequirements(WorldObject wo, int tier)
        {
            if (wo.ArmorLevel.HasValue && wo.ArmorLevel > 0)
            {
                var hasLevelRequirement = false;
                if (wo.WieldRequirements == WieldRequirement.Level)
                    hasLevelRequirement = true;
                else if (wo.WieldRequirements2 == WieldRequirement.Level)
                    hasLevelRequirement = true;
                else if (wo.WieldRequirements3 == WieldRequirement.Level)
                    hasLevelRequirement = true;
                else if (wo.WieldRequirements4 == WieldRequirement.Level)
                    hasLevelRequirement = true;

                var hasArmorOrManaRequirement = false;
                if ((wo.WieldRequirements == WieldRequirement.RawSkill && wo.WieldSkillType == (int)Skill.Armor) || (wo.WieldRequirements == WieldRequirement.RawSecondaryAttrib && wo.WieldSkillType == (int)PropertyAttribute2nd.MaxMana))
                    hasArmorOrManaRequirement = true;
                else if ((wo.WieldRequirements2 == WieldRequirement.RawSkill && wo.WieldSkillType2 == (int)Skill.Armor) || (wo.WieldRequirements2 == WieldRequirement.RawSecondaryAttrib && wo.WieldSkillType2 == (int)PropertyAttribute2nd.MaxMana))
                    hasArmorOrManaRequirement = true;
                else if ((wo.WieldRequirements3 == WieldRequirement.RawSkill && wo.WieldSkillType3 == (int)Skill.Armor) || (wo.WieldRequirements3 == WieldRequirement.RawSecondaryAttrib && wo.WieldSkillType3 == (int)PropertyAttribute2nd.MaxMana))
                    hasArmorOrManaRequirement = true;
                else if ((wo.WieldRequirements4 == WieldRequirement.RawSkill && wo.WieldSkillType4 == (int)Skill.Armor) || (wo.WieldRequirements4 == WieldRequirement.RawSecondaryAttrib && wo.WieldSkillType4 == (int)PropertyAttribute2nd.MaxMana))
                    hasArmorOrManaRequirement = true;

                if (hasLevelRequirement && !hasArmorOrManaRequirement)
                {
                    var newWieldRequirements = WieldRequirement.Invalid;
                    var newWieldSkillType = 0;
                    var newWieldDifficulty = 0;

                    if (wo.IsClothArmor)
                    {
                        newWieldRequirements = WieldRequirement.RawSecondaryAttrib;
                        newWieldSkillType = (int)PropertyAttribute2nd.MaxMana;
                        newWieldDifficulty = ((tier - 1) * 30) + (int)Math.Round((float)wo.ArmorLevel * 0.75f) + 70;
                    }
                    else if(wo.IsShield)
                    {
                        newWieldRequirements = WieldRequirement.RawSkill;
                        newWieldSkillType = (int)Skill.Shield;
                        newWieldDifficulty = ((tier - 1) * 15) + (int)Math.Round((float)wo.ArmorLevel * 0.75f) + 100;
                    }
                    else
                    {
                        newWieldRequirements = WieldRequirement.RawSkill;
                        newWieldSkillType = (int)Skill.Armor;
                        newWieldDifficulty = ((tier - 1) * 15) + (int)Math.Round((float)wo.ArmorLevel * 0.75f) + 40;
                    }

                    if (wo.WieldRequirements == WieldRequirement.Level)
                    {
                        wo.WieldRequirements = newWieldRequirements;
                        wo.WieldSkillType = newWieldSkillType;
                        wo.WieldDifficulty = newWieldDifficulty;
                    }
                    else if (wo.WieldRequirements2 == WieldRequirement.Level)
                    {
                        wo.WieldRequirements2 = newWieldRequirements;
                        wo.WieldSkillType2 = newWieldSkillType;
                        wo.WieldDifficulty2 = newWieldDifficulty;
                    }
                    else if (wo.WieldRequirements3 == WieldRequirement.Level)
                    {
                        wo.WieldRequirements3 = newWieldRequirements;
                        wo.WieldSkillType3 = newWieldSkillType;
                        wo.WieldDifficulty3 = newWieldDifficulty;
                    }
                    else if (wo.WieldRequirements4 == WieldRequirement.Level)
                    {
                        wo.WieldRequirements4 = newWieldRequirements;
                        wo.WieldSkillType4 = newWieldSkillType;
                        wo.WieldDifficulty4 = newWieldDifficulty;
                    }
                }
            }
        }

        private static string GetMutationScript_ArmorLevel(WorldObject wo, TreasureRoll roll)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                string ruleset = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? "Infiltration" : "CustomDM";
                if (roll.ArmorType == TreasureArmorType.Covenant)
                {
                    if (wo.IsShield)
                        return $"ArmorLevel.{ruleset}.covenant_shield_level.txt";
                    return $"ArmorLevel.{ruleset}.covenant_armor_level.txt";
                }
                else
                {
                    if (wo.IsShield)
                        return $"ArmorLevel.{ruleset}.shield_level.txt";
                    else if(roll.IsClothArmor)
                        return $"ArmorLevel.{ruleset}.cloth_armor_level.txt";
                    return $"ArmorLevel.{ruleset}.armor_level.txt";
                }
            }
            else
            {
                switch (roll.ArmorType)
                {
                    case TreasureArmorType.Covenant:

                        if (wo.IsShield)
                            return "ArmorLevel.covenant_shield.txt";
                        else
                            return "ArmorLevel.covenant_armor.txt";

                    case TreasureArmorType.Olthoi:

                        if (wo.IsShield)
                            return "ArmorLevel.olthoi_shield.txt";
                        else
                            return "ArmorLevel.olthoi_armor.txt";
                }

                if (wo.IsShield)
                    return "ArmorLevel.shield_level.txt";

                var coverage = wo.ClothingPriority ?? 0;

                if ((coverage & (CoverageMask)CoverageMaskHelper.Extremities) != 0)
                    return "ArmorLevel.armor_level_extremity.txt";
                else if ((coverage & (CoverageMask)CoverageMaskHelper.Outerwear) != 0)
                    return "ArmorLevel.armor_level_non_extremity.txt";
                else
                    return null;
            }
        }

        private static bool TryRollEquipmentSet(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (roll == null)
            {
                if (!PropertyManager.GetBool("equipmentsetid_enabled").Item)
                    return false;

                if (profile.Tier < 6 || !wo.HasArmorLevel())
                    return false;

                if (wo.ClothingPriority == null || (wo.ClothingPriority & (CoverageMask)CoverageMaskHelper.Outerwear) == 0)
                    return false;

                var dropRate = PropertyManager.GetDouble("equipmentsetid_drop_rate").Item;
                var dropRateMod = 1.0 / dropRate;

                var lootQualityMod = 1.0f;
                if (PropertyManager.GetBool("loot_quality_mod").Item)
                    lootQualityMod = 1.0f - profile.LootQualityMod;

                // initial base 10% chance to add a random EquipmentSet, which can be adjusted via equipmentsetid_drop_rate
                var rng = ThreadSafeRandom.Next(1, (int)(100 * dropRateMod * lootQualityMod));
                if (rng > 10) return false;

                wo.EquipmentSetId = (EquipmentSet)ThreadSafeRandom.Next((int)EquipmentSet.Soldiers, (int)EquipmentSet.Lightningproof);
            }
            else
            {
                wo.EquipmentSetId = EquipmentSetChance.Roll(wo, profile, roll);
            }

            if (wo.EquipmentSetId != null && PropertyManager.GetBool("equipmentsetid_name_decoration").Item)
            {
                var equipSetId = wo.EquipmentSetId;

                var equipSetName = equipSetId.ToString();

                if (equipSetId >= EquipmentSet.Soldiers && equipSetId <= EquipmentSet.Crafters)
                    equipSetName = equipSetName.TrimEnd('s') + "'s";

                wo.Name = $"{equipSetName} {wo.Name}";
            }
            return true;
        }

        private static void MutateCloak(WorldObject wo, TreasureDeath profile, TreasureRoll roll = null)
        {
            wo.ItemMaxLevel = CloakChance.Roll_ItemMaxLevel(profile);

            // wield difficulty, based on ItemMaxLevel
            switch (wo.ItemMaxLevel)
            {
                case 1:
                    wo.WieldDifficulty = 30;
                    break;
                case 2:
                    wo.WieldDifficulty = 60;
                    break;
                case 3:
                    wo.WieldDifficulty = 90;
                    break;
                case 4:
                    wo.WieldDifficulty = 120;
                    break;
                case 5:
                    wo.WieldDifficulty = 150;
                    break;
            }

            wo.IconOverlayId = IconOverlay_ItemMaxLevel[wo.ItemMaxLevel.Value - 1];

            // equipment set
            wo.EquipmentSetId = CloakChance.RollEquipmentSet();

            // proc spell
            var surgeSpell = CloakChance.RollProcSpell();

            if (surgeSpell != SpellId.Undef)
            {
                wo.ProcSpell = (uint)surgeSpell;

                // Cloaked In Skill is the only self-targeted spell
                if (wo.ProcSpell == (uint)SpellId.CloakAllSkill)
                    wo.ProcSpellSelfTargeted = true;
                else
                    wo.ProcSpellSelfTargeted = false;

                wo.CloakWeaveProc = 1;
            }
            else
            {
                // Damage Reduction proc
                wo.CloakWeaveProc = 2;
            }

            // material type
            wo.MaterialType = GetMaterialType(wo, profile.Tier);

            // workmanship
            wo.Workmanship = WorkmanshipChance.Roll(profile.Tier, profile.LootQualityMod);

            if (roll != null && profile.Tier == 8)
                TryMutateGearRating(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))
                MutateValue(wo, profile.Tier, roll);
        }

        private static void MutateValue_Armor(WorldObject wo)
        {
            var bulkMod = wo.BulkMod ?? 1.0f;
            var sizeMod = wo.SizeMod ?? 1.0f;

            var armorLevel = wo.ArmorLevel ?? 0;

            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
            {
                // from the py16 mutation scripts
                //wo.Value += (int)(armorLevel * armorLevel / 10.0f * bulkMod * sizeMod);

                // still probably not how retail did it
                // modified for armor values to match closer to retail pcaps
                var minRng = (float)Math.Min(bulkMod, sizeMod);
                var maxRng = (float)Math.Max(bulkMod, sizeMod);

                var rng = ThreadSafeRandom.Next(minRng, maxRng);

                wo.Value += (int)(armorLevel * armorLevel / 10.0f * rng);
            }
            else
            {
                wo.Value += (int)(armorLevel * bulkMod * sizeMod);
            }
        }

        private static void MutateArmorModVsType(WorldObject wo, TreasureDeath profile)
        {
            // for the PropertyInt.MutateFilters found in py16 data,
            // items either had all of these, or none of these

            // only the elemental types could mutate
            TryMutateArmorModVsType(wo, profile, PropertyFloat.ArmorModVsFire);
            TryMutateArmorModVsType(wo, profile, PropertyFloat.ArmorModVsCold);
            TryMutateArmorModVsType(wo, profile, PropertyFloat.ArmorModVsAcid);
            TryMutateArmorModVsType(wo, profile, PropertyFloat.ArmorModVsElectric);
        }

        private static bool TryMutateArmorModVsType(WorldObject wo, TreasureDeath profile, PropertyFloat prop)
        {
            var armorModVsType = wo.GetProperty(prop);

            if (armorModVsType == null)
                return false;

            // perform the initial roll to determine if this ArmorModVsType will mutate
            var mutate = ArmorModVsTypeChance.Roll(profile.Tier);

            if (!mutate)
                return false;

            // get quality level 1-5 for tier
            var qualityLevel = ArmorModVsTypeChance.RollQualityLevel(profile);

            // add in rng
            // for t6+ / max quality level 5, the highest bonus found in eor data was ~0.9
            var rng = ThreadSafeRandom.Next(-0.05f, 0.15f);

            var bonusRL = qualityLevel * 0.15f + rng;

            //Console.WriteLine($"Boosting {wo.Name}.{prop} by {bonusRL}");

            armorModVsType += bonusRL;

            // ensure between -2.0 / 2.0?
            armorModVsType = Math.Clamp(armorModVsType.Value, -2.0f, 2.0f);

            wo.SetProperty(prop, armorModVsType.Value);

            return true;
        }

        private static bool TryMutateGearRating(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (profile.Tier != 8)
                return false;

            // shields don't have gear ratings
            if (wo.IsShield) return false;

            var gearRating = GearRatingChance.Roll(wo, profile, roll);

            if (gearRating == 0)
                return false;

            //Console.WriteLine($"TryMutateGearRating({wo.Name}, {profile.TreasureType}, {roll.ItemType}): rolled gear rating {gearRating}");

            var rng = ThreadSafeRandom.Next(0, 1);

            if (roll.HasArmorLevel(wo))
            {
                // clothing w/ al, and crowns would be included in this group
                if (rng == 0)
                    wo.GearCritDamage = gearRating;
                else
                    wo.GearCritDamageResist = gearRating;
            }
            else if (roll.IsClothing || roll.IsCloak)
            {
                if (rng == 0)
                    wo.GearDamage = gearRating;
                else
                    wo.GearDamageResist = gearRating;
            }
            else if (roll.IsJewelry)
            {
                if (rng == 0)
                    wo.GearHealingBoost = gearRating;
                else
                    wo.GearMaxHealth = gearRating;
            }
            else
            {
                log.Error($"TryMutateGearRating({wo.Name}, {profile.TreasureType}, {roll.ItemType}): unknown item type");
                return false;
            }

            // ensure wield requirement is level 180?
            if (roll.ArmorType != TreasureArmorType.Society)
                SetWieldLevelReq(wo, 180);

            return true;
        }

        private static void SetWieldLevelReq(WorldObject wo, int level)
        {
            if (wo.WieldRequirements == WieldRequirement.Invalid)
            {
                wo.WieldRequirements = WieldRequirement.Level;
                wo.WieldSkillType = (int)Skill.Axe;  // set from examples in pcap data
                wo.WieldDifficulty = level;
            }
            else if (wo.WieldRequirements == WieldRequirement.Level)
            {
                if (wo.WieldDifficulty < level)
                    wo.WieldDifficulty = level;
            }
            else
            {
                // this can either be empty, or in the case of covenant / olthoi armor,
                // it could already contain a level requirement of 180, or possibly 150 in tier 8

                // we want to set this level requirement to 180, in all cases

                // magloot logs indicated that even if covenant / olthoi armor was not upgraded to 180 in its mutation script,
                // a gear rating could still drop on it, and would "upgrade" the 150 to a 180

                wo.WieldRequirements2 = WieldRequirement.Level;
                wo.WieldSkillType2 = (int)Skill.Axe;  // set from examples in pcap data
                wo.WieldDifficulty2 = level;
            }
        }
    }
}
