using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SwordWcids_Sho
    {
        private static ChanceTable<WeenieClassName> T1_Chances;

        private static ChanceTable<WeenieClassName> T1_T2_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.swordrapier,      0.10f ),
            ( WeenieClassName.yaoji,            0.12f ),
            ( WeenieClassName.yaojiacid,        0.03f ),
            ( WeenieClassName.yaojielectric,    0.03f ),
            ( WeenieClassName.yaojifire,        0.03f ),
            ( WeenieClassName.yaojifrost,       0.03f ),
            ( WeenieClassName.scimitar,         0.10f ),
            ( WeenieClassName.scimitaracid,     0.03f ),
            ( WeenieClassName.scimitarelectric, 0.03f ),
            ( WeenieClassName.scimitarfire,     0.03f ),
            ( WeenieClassName.scimitarfrost,    0.03f ),
            ( WeenieClassName.ken,              0.10f ),
            ( WeenieClassName.kenacid,          0.03f ),
            ( WeenieClassName.kenelectric,      0.03f ),
            ( WeenieClassName.kenfire,          0.03f ),
            ( WeenieClassName.kenfrost,         0.03f ),
            ( WeenieClassName.tachi,            0.10f ),
            ( WeenieClassName.tachiacid,        0.03f ),
            ( WeenieClassName.tachielectric,    0.03f ),
            ( WeenieClassName.tachifire,        0.03f ),
            ( WeenieClassName.tachifrost,       0.03f ),
        };

        private static ChanceTable<WeenieClassName> T3_T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.swordrapier,      0.06f ),
            ( WeenieClassName.yaoji,            0.06f ),
            ( WeenieClassName.yaojiacid,        0.01f ),
            ( WeenieClassName.yaojielectric,    0.01f ),
            ( WeenieClassName.yaojifire,        0.01f ),
            ( WeenieClassName.yaojifrost,       0.01f ),
            ( WeenieClassName.scimitar,         0.12f ),
            ( WeenieClassName.scimitaracid,     0.04f ),
            ( WeenieClassName.scimitarelectric, 0.04f ),
            ( WeenieClassName.scimitarfire,     0.04f ),
            ( WeenieClassName.scimitarfrost,    0.04f ),
            ( WeenieClassName.ken,              0.12f ),
            ( WeenieClassName.kenacid,          0.04f ),
            ( WeenieClassName.kenelectric,      0.04f ),
            ( WeenieClassName.kenfire,          0.04f ),
            ( WeenieClassName.kenfrost,         0.04f ),
            ( WeenieClassName.tachi,            0.12f ),
            ( WeenieClassName.tachiacid,        0.04f ),
            ( WeenieClassName.tachielectric,    0.04f ),
            ( WeenieClassName.tachifire,        0.04f ),
            ( WeenieClassName.tachifrost,       0.04f ),
        };

        private static ChanceTable<WeenieClassName> T5_T6_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.swordrapier,      0.01f ),
            ( WeenieClassName.yaoji,            0.01f ),
            ( WeenieClassName.yaojiacid,        0.01f ),
            ( WeenieClassName.yaojielectric,    0.01f ),
            ( WeenieClassName.yaojifire,        0.01f ),
            ( WeenieClassName.yaojifrost,       0.01f ),
            ( WeenieClassName.scimitar,         0.14f ),
            ( WeenieClassName.scimitaracid,     0.04f ),
            ( WeenieClassName.scimitarelectric, 0.04f ),
            ( WeenieClassName.scimitarfire,     0.04f ),
            ( WeenieClassName.scimitarfrost,    0.04f ),
            ( WeenieClassName.ken,              0.14f ),
            ( WeenieClassName.kenacid,          0.04f ),
            ( WeenieClassName.kenelectric,      0.04f ),
            ( WeenieClassName.kenfire,          0.04f ),
            ( WeenieClassName.kenfrost,         0.04f ),
            ( WeenieClassName.tachi,            0.14f ),
            ( WeenieClassName.tachiacid,        0.05f ),
            ( WeenieClassName.tachielectric,    0.05f ),
            ( WeenieClassName.tachifire,        0.05f ),
            ( WeenieClassName.tachifrost,       0.05f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> weaponTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T2_Chances,
            T1_T2_Chances,
            T3_T4_Chances,
            T3_T4_Chances,
            T5_T6_Chances,
            T5_T6_Chances,
        };

        public static WeenieClassName Roll(int tier, out TreasureWeaponType weaponType)
        {
            // todo: add unique profiles for t7 / t8?
            tier = Math.Clamp(tier, 1, 6);

            var weapon = weaponTiers[tier - 1].Roll();

            switch (weapon)
            {
                case WeenieClassName.swordrapier:
                case WeenieClassName.yaoji:
                case WeenieClassName.yaojiacid:
                case WeenieClassName.yaojielectric:
                case WeenieClassName.yaojifire:
                case WeenieClassName.yaojifrost:
                    weaponType = TreasureWeaponType.SwordMS;
                    break;
                case WeenieClassName.ace40760_nodachi:
                case WeenieClassName.ace40761_acidnodachi:
                case WeenieClassName.ace40762_lightningnodachi:
                case WeenieClassName.ace40763_flamingnodachi:
                case WeenieClassName.ace40764_frostnodachi:
                    weaponType = TreasureWeaponType.TwoHandedSword;
                    break;
                default:
                    weaponType = TreasureWeaponType.Sword;
                    break;
            }

            return weapon;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static SwordWcids_Sho()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yaoji,                3.0f ),
                    ( WeenieClassName.scimitar,             0.5f ),
                    ( WeenieClassName.ken,                  0.5f ),
                    ( WeenieClassName.tachi,                0.5f ),

                    ( WeenieClassName.swordrapier,          0.25f ),
                };

                T1_T2_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yaoji,             4.0f ),
                    ( WeenieClassName.yaojiacid,         1.0f ),
                    ( WeenieClassName.yaojielectric,     1.0f ),
                    ( WeenieClassName.yaojifire,         1.0f ),
                    ( WeenieClassName.yaojifrost,        1.0f ),

                    ( WeenieClassName.scimitar,          4.0f ),
                    ( WeenieClassName.scimitaracid,      1.0f ),
                    ( WeenieClassName.scimitarelectric,  1.0f ),
                    ( WeenieClassName.scimitarfire,      1.0f ),
                    ( WeenieClassName.scimitarfrost,     1.0f ),

                    ( WeenieClassName.ken,               4.0f ),
                    ( WeenieClassName.kenacid,           1.0f ),
                    ( WeenieClassName.kenelectric,       1.0f ),
                    ( WeenieClassName.kenfire,           1.0f ),
                    ( WeenieClassName.kenfrost,          1.0f ),

                    ( WeenieClassName.tachi,             4.0f ),
                    ( WeenieClassName.tachiacid,         1.0f ),
                    ( WeenieClassName.tachielectric,     1.0f ),
                    ( WeenieClassName.tachifire,         1.0f ),
                    ( WeenieClassName.tachifrost,        1.0f ),

                    ( WeenieClassName.swordrapier,      2.0f ),
                };

                weaponTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yaoji,                3.00f ),
                    ( WeenieClassName.scimitar,             0.5f ),
                    ( WeenieClassName.ken,                  0.5f ),
                    ( WeenieClassName.tachi,                0.5f ),

                    ( WeenieClassName.swordrapier,          0.5f ),
                    ( WeenieClassName.ace40760_nodachi,     0.5f ),
                };

                T1_T2_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yaoji,            16.0f ),
                    ( WeenieClassName.yaojiacid,         1.0f ),
                    ( WeenieClassName.yaojielectric,     1.0f ),
                    ( WeenieClassName.yaojifire,         1.0f ),
                    ( WeenieClassName.yaojifrost,        1.0f ),

                    ( WeenieClassName.scimitar,         16.0f ),
                    ( WeenieClassName.scimitaracid,      1.0f ),
                    ( WeenieClassName.scimitarelectric,  1.0f ),
                    ( WeenieClassName.scimitarfire,      1.0f ),
                    ( WeenieClassName.scimitarfrost,     1.0f ),

                    ( WeenieClassName.ken,        16.0f ),
                    ( WeenieClassName.kenacid,     1.0f ),
                    ( WeenieClassName.kenelectric, 1.0f ),
                    ( WeenieClassName.kenfire,     1.0f ),
                    ( WeenieClassName.kenfrost,    1.0f ),

                    ( WeenieClassName.tachi,          16.0f ),
                    ( WeenieClassName.tachiacid,       1.0f ),
                    ( WeenieClassName.tachielectric,   1.0f ),
                    ( WeenieClassName.tachifire,       1.0f ),
                    ( WeenieClassName.tachifrost,      1.0f ),

                    ( WeenieClassName.swordrapier,     16.0f ),

                    ( WeenieClassName.ace40760_nodachi,         16.0f ),
                    ( WeenieClassName.ace40761_acidnodachi,      1.0f ),
                    ( WeenieClassName.ace40762_lightningnodachi, 1.0f ),
                    ( WeenieClassName.ace40763_flamingnodachi,   1.0f ),
                    ( WeenieClassName.ace40764_frostnodachi,     1.0f ),
                };

                weaponTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                    T1_T2_Chances,
                };
            }

            foreach (var weaponTier in weaponTiers)
            {
                foreach (var entry in weaponTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Sword);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
