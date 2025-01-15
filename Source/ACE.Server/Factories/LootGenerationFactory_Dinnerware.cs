using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void MutateDinnerware(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
        {
            // dinnerware did not have its Damage / DamageVariance / WeaponSpeed mutated

            // material type
            wo.MaterialType = GetMaterialType(wo, profile.Tier);

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

            // "Empty Flask" was the only dinnerware that never received spells
            if (isMagical && wo.WeenieClassId != (uint)WeenieClassName.flasksimple)
                AssignMagic(wo, profile, roll);

            // item value
            if (wo.HasMutateFilter(MutateFilter.Value))
                MutateValue(wo, profile.Tier, roll);

            // long desc
            wo.LongDesc = GetLongDesc(wo);
        }
    }
}

