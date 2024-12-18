using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static WorldObject CreateWeapon(TreasureDeath profile, bool isMagical)
        {
            int chance = ThreadSafeRandom.Next(1, 100);

            // Aligning drop ratio to better align with retail - HarliQ 11/11/19
            // Melee - 42%
            // Missile - 36%
            // Casters - 22%

            return chance switch
            {
                var rate when (rate < 43) => CreateMeleeWeapon(profile, isMagical),
                var rate when (rate > 42 && rate < 79) => CreateMissileWeapon(profile, isMagical),
                _ => CreateCaster(profile, isMagical),
            };
        }

        private static float RollWeaponSpeedMod(TreasureDeath treasureDeath)
        {
            var qualityLevel = QualityChance.Roll(treasureDeath);

            if (qualityLevel == 0)
                return 1.0f;    // no bonus

            var rng = (float)ThreadSafeRandom.Next(-0.025f, 0.025f);

            // min/max range: 67.5% - 100%
            var weaponSpeedMod = 1.0f - (qualityLevel * 0.025f + rng);

            //Console.WriteLine($"WeaponSpeedMod: {weaponSpeedMod}");

            return weaponSpeedMod;
        }

        private static float ApplyQualityModToExtraMutationChance(float chance, float lootQualityMod)
        {
            return chance * (1 + lootQualityMod);
        }

        private static bool RollCrushingBlow(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            var chance = ExtraMutationEffects.GetCrushingBlowChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                float amount;
                if (wo.IsCaster)
                    amount = 1.5f;
                else
                    amount = 2.0f;

                wo.CriticalMultiplier = amount;
                wo.IconOverlayId = 0x06005EBC;
                return true;
            }
            else
                return false;
        }

        private static bool RollBitingStrike(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            var chance = ExtraMutationEffects.GetBitingStrikeChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                float amount;
                if (wo.IsCaster)
                    amount = 0.10f;
                else
                    amount = 0.15f;
                wo.CriticalFrequency = amount;
                wo.IconOverlayId = 0x06005EBD;
                return true;
            }
            else
                return false;
        }

        private static bool RollSlayer(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            var chance = SlayerTypeChance.GetSlayerChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                wo.SlayerCreatureType = SlayerTypeChance.Roll(treasureDeath);
                wo.SlayerDamageBonus = 1.5f;
                wo.IconOverlayId = 0x06005EC0;
                return true;
            }
            return false;
        }

        private static bool RollArmorCleaving(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            var chance = ExtraMutationEffects.GetArmorCleavingChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                // 0.0 = ignore 100% of armor AL.
                wo.IgnoreArmor = 0.625f; // Equivalent to -75 at 200 AL armor.
                wo.IconOverlayId = 0x06005EBF;
                return true;
            }
            return false;
        }

        private static bool RollShieldCleaving(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            if (wo.IsTwoHanded)
                return false; // Two-handed weapons area always shield cleaving so this is unnecessary.

            var chance = ExtraMutationEffects.GetShieldCleavingChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                // 1.0 = ignore 100% of shield AL.
                wo.IgnoreShield = 0.50f; // Equivalent of Brittlemail V for 300 AL shields.
                wo.IconOverlayId = 0x06005EC2;
                wo.Name = $"{wo.Name} of Shield Cleaving";
                return true;
            }
            return false;
        }

        private static bool RollResistanceCleaving(TreasureDeath treasureDeath, WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            var chance = ExtraMutationEffects.GetResistanceCleavingChanceForTier(treasureDeath.Tier);
            chance = ApplyQualityModToExtraMutationChance(chance, treasureDeath.LootQualityMod);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                wo.ResistanceModifierType = DamageType.Elemental;
                wo.ResistanceModifier = 0.6f; // This is used as the multiplier in the rending formula.
                wo.IconOverlayId = 0x06005EC1;
                return true;
            }
            return false;
        }
    }
}
