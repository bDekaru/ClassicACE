using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
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
        /// Creates and optionally mutates a new MissileWeapon
        /// </summary>
        public static WorldObject CreateMissileWeapon(TreasureDeath profile, bool isMagical, MissileWeaponSkill weaponSkill = MissileWeaponSkill.Undef, bool mutate = true)
        {
            int wcid;

            if (weaponSkill == MissileWeaponSkill.Undef || weaponSkill == MissileWeaponSkill.MissileWeapons)
                weaponSkill = (MissileWeaponSkill)ThreadSafeRandom.Next(2, 4);

            int wieldDifficulty = RollWieldDifficulty(profile.Tier, TreasureWeaponType.MissileWeapon);

            var heritage = HeritageChance.Roll(profile.UnknownChances, new TreasureRoll());

            switch (weaponSkill)
            {
                default:
                case MissileWeaponSkill.Bow:
                    switch (heritage)
                    {
                        default:
                        case TreasureHeritageGroup.Aluvian:
                            wcid = (int)BowWcids_Aluvian.Roll(profile.Tier, out _);
                            break;
                        case TreasureHeritageGroup.Gharundim:
                            wcid = (int)BowWcids_Gharundim.Roll(profile.Tier, out _);
                            break;
                        case TreasureHeritageGroup.Sho:
                            wcid = (int)BowWcids_Sho.Roll(profile.Tier, out _);
                            break;
                    }
                    break;
                case MissileWeaponSkill.Crossbow:
                    wcid = (int)CrossbowWcids.Roll(profile.Tier, out _);
                    break;
                case MissileWeaponSkill.ThrownWeapon:
                    wcid = (int)AtlatlWcids.Roll(profile.Tier, out _);
                    break;
            }

            WorldObject wo = WorldObjectFactory.CreateNewWorldObject((uint)wcid);

            if (wo != null && mutate)
                MutateMissileWeapon(wo, profile, isMagical, wieldDifficulty);
            
            return wo;
        }

        private static void MutateMissileWeapon(WorldObject wo, TreasureDeath profile, bool isMagical, int? wieldDifficulty = null, TreasureRoll roll = null)
        {
            // new method / mutation scripts
            var isElemental = wo.W_DamageType != DamageType.Undef;

            var scriptName = GetMissileScript(roll.WeaponType, isElemental);

            // mutate DamageMod / ElementalDamageBonus / WieldRequirements
            var mutationFilter = MutationCache.GetMutation(scriptName);

            mutationFilter.TryMutate(wo, profile.Tier, profile.LootQualityMod);

            // mutate WeaponDefense
            mutationFilter = MutationCache.GetMutation("MissileWeapons.weapon_defense.txt");

            mutationFilter.TryMutate(wo, profile.Tier, profile.LootQualityMod);

            // weapon speed
            if (wo.WeaponTime != null)
            {
                var weaponSpeedMod = RollWeaponSpeedMod(profile);
                wo.WeaponTime = (int)(wo.WeaponTime * weaponSpeedMod);
            }

            if (profile.LootQualityMod >= 0)
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

            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR) // Infiltration data has these in the weapon_defense.txt files
            {
                // missile / magic defense
                wo.WeaponMissileDefense = MissileMagicDefense.Roll(profile.Tier);
                wo.WeaponMagicDefense = MissileMagicDefense.Roll(profile.Tier);
            }

            // spells
            if (!isMagical)
            {
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
                wo.ManaRate = null;
            }
            else
                AssignMagic(wo, profile, roll);

            // item value
            //if (wo.HasMutateFilter(MutateFilter.Value))   // fixme: data
                MutateValue(wo, profile.Tier, roll);

            // long description
            wo.LongDesc = GetLongDesc(wo);
        }

        private static string GetMissileScript(TreasureWeaponType weaponType, bool isElemental = false)
        {
            var elementalStr = isElemental ? "elemental" : "non_elemental";

            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                string ruleset = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? "Infiltration" : "CustomDM";
                return $"MissileWeapons.{ruleset}." + weaponType.GetScriptName() + "_" + elementalStr + ".txt";
            }
            else
                return "MissileWeapons." + weaponType.GetScriptName() + "_" + elementalStr + ".txt";
        }
    }
}
