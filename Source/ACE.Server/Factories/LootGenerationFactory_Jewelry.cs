using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void MutateJewelry(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
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
                wo.GemCount = ThreadSafeRandom.Next(1, 5);

            wo.GemType = RollGemType(profile.Tier);

            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier, profile.LootQualityMod);

            // wield level requirement for t7+
            if (profile.Tier > 6)
                RollWieldLevelReq_T7_T8(wo, profile);

            // assign magic
            if (isMagical)
                AssignMagic(wo, profile, roll);
            else
            {
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
                wo.ManaRate = null;
            }

            // gear rating (t8)
            if (roll != null && profile.Tier == 8)
                TryMutateGearRating(wo, profile, roll);

            // item value
            //  if (wo.HasMutateFilter(MutateFilter.Value))     // fixme: data
                MutateValue(wo, profile.Tier, roll);

            wo.LongDesc = GetLongDesc(wo);
        }
    }
}
