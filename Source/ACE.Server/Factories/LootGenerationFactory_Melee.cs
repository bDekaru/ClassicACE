using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Entity.Mutations;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        /// <summary>
        /// Creates and optionally mutates a new MeleeWeapon
        /// </summary>
        public static WorldObject CreateMeleeWeapon(TreasureDeath profile, bool isMagical, MeleeWeaponSkill weaponSkill = MeleeWeaponSkill.Undef, bool mutate = true)
        {
            var wcid = 0;

            if (weaponSkill == MeleeWeaponSkill.Undef)
                if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                    weaponSkill = (MeleeWeaponSkill)ThreadSafeRandom.Next(5, 11);
                else
                    weaponSkill = (MeleeWeaponSkill)ThreadSafeRandom.Next(1, 4);

            var heritage = HeritageChance.Roll(profile.UnknownChances, new TreasureRoll());

            switch (weaponSkill)
            {
                case MeleeWeaponSkill.HeavyWeapons:

                    wcid = (int)HeavyWeaponWcids.Roll(out _);
                    break;

                case MeleeWeaponSkill.LightWeapons:

                    wcid = (int)LightWeaponWcids.Roll(out _);
                    break;

                case MeleeWeaponSkill.FinesseWeapons:

                    wcid = (int)FinesseWeaponWcids.Roll(out _);
                    break;

                case MeleeWeaponSkill.TwoHandedCombat:

                    wcid = (int)TwoHandedWeaponWcids.Roll(out _);
                    break;

                case MeleeWeaponSkill.Axe:

                    wcid = (int)AxeWcids.Roll(heritage, profile.Tier, out _);
                    break;

                case MeleeWeaponSkill.Dagger:

                    switch (heritage)
                    {
                        default:
                            wcid = (int)DaggerWcids_Aluvian_Sho.Roll(profile.Tier, out _);
                            break;
                        case TreasureHeritageGroup.Gharundim:
                            wcid = (int)DaggerWcids_Gharundim.Roll(profile.Tier, out _);
                            break;
                    }
                    break;

                case MeleeWeaponSkill.Mace:

                    wcid = (int)MaceWcids.Roll(heritage, profile.Tier, out _);
                    break;

                case MeleeWeaponSkill.Spear:

                    wcid = (int)SpearWcids.Roll(heritage, profile.Tier, out _);
                    break;

                case MeleeWeaponSkill.Staff:

                    wcid = (int)StaffWcids.Roll(heritage, profile.Tier);
                    break;

                case MeleeWeaponSkill.Sword:

                    switch (heritage)
                    {
                        default:
                        case TreasureHeritageGroup.Aluvian:
                            wcid = (int)SwordWcids_Aluvian.Roll(profile.Tier, out _);
                            break;
                        case TreasureHeritageGroup.Gharundim:
                            wcid = (int)SwordWcids_Gharundim.Roll(profile.Tier, out _);
                            break;
                        case TreasureHeritageGroup.Sho:
                            wcid = (int)SwordWcids_Sho.Roll(profile.Tier, out _);
                            break;
                    }
                    break;

                case MeleeWeaponSkill.UnarmedCombat:

                    wcid = (int)UnarmedWcids.Roll(heritage, profile.Tier);
                    break;
            }

            var wo = WorldObjectFactory.CreateNewWorldObject((uint)wcid);

            if (wo != null && mutate)
            {
                if (!MutateMeleeWeapon(wo, profile, isMagical))
                {
                    log.Warn($"[LOOT] {wo.WeenieClassId} - {wo.Name} is not a MeleeWeapon");
                    return null;
                }
            }
            return wo;
        }

        private static bool MutateMeleeWeapon(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
        {
            if (!(wo is MeleeWeapon || wo.IsThrownWeapon))
                return false;

            // thanks to 4eyebiped for helping with the data analysis of magloot retail logs
            // that went into reversing these mutation scripts

            var weaponSkill = wo.WeaponSkill.ToMeleeWeaponSkill();

            // mutate Damage / WieldDifficulty / Variance
            var scriptName = GetDamageScript(weaponSkill, roll.WeaponType);

            var mutationFilter = MutationCache.GetMutation(scriptName);

            mutationFilter.TryMutate(wo, profile.Tier, profile.LootQualityMod);

            // mutate WeaponOffense / WeaponDefense
            scriptName = GetOffenseDefenseScript(weaponSkill, roll.WeaponType);

            mutationFilter = MutationCache.GetMutation(scriptName);

            mutationFilter.TryMutate(wo, profile.Tier, profile.LootQualityMod);

            // weapon speed
            if (wo.WeaponTime != null)
            {
                var weaponSpeedMod = RollWeaponSpeedMod(profile);
                wo.WeaponTime = (int)(wo.WeaponTime * weaponSpeedMod);
            }

            var allowSpecialProperties = true;
            if (profile is TreasureDeathExtended extendedProfile)
                allowSpecialProperties = extendedProfile.AllowSpecialProperties;

            if (profile.LootQualityMod >= 0 && allowSpecialProperties)
            {
                var counter = 0;
                if (counter < 2 && RollShieldCleaving(profile, wo))
                    counter++;
                if (counter < 2 && RollArmorCleaving(profile, wo))
                    counter++;
                if (counter < 2 && RollBitingStrike(profile, wo))
                    counter++;
                if (counter < 2 && RollCrushingBlow(profile, wo))
                    counter++;
                RollSlayer(profile, wo);
            }

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
                wo.GemCount = ThreadSafeRandom.Next(1, 5);

            wo.GemType = RollGemType(profile.Tier);

            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier, profile.LootQualityMod);

            // burden
            MutateBurden(wo, profile, true);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR) // Infiltration data has these in the offense_defense.txt files
            {
                // missile / magic defense
                wo.WeaponMissileDefense = MissileMagicDefense.Roll(profile.Tier);
                wo.WeaponMagicDefense = MissileMagicDefense.Roll(profile.Tier);
            }

            // spells
            if (!isMagical)
            {
                // clear base
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
            }
            else
                AssignMagic(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))   // fixme: data
                MutateValue(wo, profile.Tier, roll);

            // long description
            wo.LongDesc = GetLongDesc(wo);

            return true;
        }

        private static string GetDamageScript(MeleeWeaponSkill weaponSkill, TreasureWeaponType weaponType)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                string ruleset = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? "Infiltration" : "CustomDM";
                return $"MeleeWeapons.Damage_WieldDifficulty_DamageVariance.{ruleset}." + weaponType.GetScriptName() + ".txt";
            }
            else
                return "MeleeWeapons.Damage_WieldDifficulty_DamageVariance." + weaponSkill.GetScriptName_Combined() + "_" + weaponType.GetScriptName() + ".txt";
        }

        private static string GetOffenseDefenseScript(MeleeWeaponSkill weaponSkill, TreasureWeaponType weaponType)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                string ruleset = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? "Infiltration" : "CustomDM";
                return $"MeleeWeapons.WeaponOffense_WeaponDefense.{ruleset}." + weaponType.GetScriptShortName() + "_offense_defense.txt";
            }
            else
                return "MeleeWeapons.WeaponOffense_WeaponDefense." + weaponType.GetScriptShortName() + "_offense_defense.txt";
        }
    }
}
