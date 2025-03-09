using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using System.Collections.Generic;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SpecialItemsWcids
    {
        private static ChanceTable<TreasureItemType_Orig> specialItemCategory = new ChanceTable<TreasureItemType_Orig>(ChanceTableType.Weight)
        {
            (TreasureItemType_Orig.Salvage,                 1.0f ),
            (TreasureItemType_Orig.SpecialItem_Unmutated,   1.0f ),
        };

        private static ChanceTable<WeenieClassName> specialItemsUnmutatedWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ((WeenieClassName)50128,      1.00f ), // Spell Extraction Scroll VI
            ((WeenieClassName)50129,      1.00f ), // Spell Extraction Scroll VII
            ((WeenieClassName)50140,      1.00f ), // Minor Cantrip Extraction Scroll
            ((WeenieClassName)50141,      1.00f ), // Major Cantrip Extraction Scroll
        };

        private static Dictionary<WeenieClassName, int> specialItemsUnmutatedAmount = new Dictionary<WeenieClassName, int>()
        {
            {(WeenieClassName)50128,      10 }, // Spell Extraction Scroll VI
            {(WeenieClassName)50129,      10 }, // Spell Extraction Scroll VII
            {(WeenieClassName)50140,       1 }, // Minor Cantrip Extraction Scroll
            {(WeenieClassName)50141,       1 }, // Major Cantrip Extraction Scroll
        };

        private static ChanceTable<WeenieClassName> specialItemsSalvageWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ( WeenieClassName.materialsteel,            1.00f ), // AL + 25% or 20

            ( WeenieClassName.materialiron,             1.00f ), // Weapon Damage + 4 for regular weapons and + 2 for multi-strike weapons
            ( WeenieClassName.materialmahogany,         1.00f ), // Missile Weapon Mod + 16%
            ( WeenieClassName.materialgreengarnet,      1.00f ), // Wand Damage + 4%

            ( WeenieClassName.materialsunstone,         1.00f ), // Armor Rending
            ( WeenieClassName.materialblackopal,        1.00f ), // Critical Strike
            ( WeenieClassName.materialfireopal,         1.00f ), // Critical Blow
            ( WeenieClassName.materialtigereye,         1.00f ), // Elemental Rending
        };

        public static WeenieClassName Roll(TreasureDeath profile, TreasureRoll treasureRoll)
        {
            treasureRoll.ItemType = specialItemCategory.Roll();
            switch (treasureRoll.ItemType)
            {
                case TreasureItemType_Orig.Salvage:
                    return specialItemsSalvageWcids.Roll(profile.LootQualityMod);
                default:
                case TreasureItemType_Orig.SpecialItem_Unmutated:
                    return specialItemsUnmutatedWcids.Roll(profile.LootQualityMod);
            }
        }

        public static int GetAmount(uint wcid)
        {
            if (specialItemsUnmutatedAmount.TryGetValue((WeenieClassName)wcid, out var amount))
                return amount;
            else
                return 1;
        }
    }
}
