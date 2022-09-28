using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class DaggerWcids_Aluvian_Sho
    {
        private static ChanceTable<WeenieClassName> T1_Chances;

        private static ChanceTable<WeenieClassName> T1_T3_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.knife,          0.16f ),
            ( WeenieClassName.knifeacid,      0.04f ),
            ( WeenieClassName.knifeelectric,  0.04f ),
            ( WeenieClassName.knifefire,      0.04f ),
            ( WeenieClassName.knifefrost,     0.04f ),
            ( WeenieClassName.dagger,         0.16f ),
            ( WeenieClassName.daggeracid,     0.04f ),
            ( WeenieClassName.daggerelectric, 0.04f ),
            ( WeenieClassName.daggerfire,     0.04f ),
            ( WeenieClassName.daggerfrost,    0.04f ),
            ( WeenieClassName.dirk,           0.16f ),
            ( WeenieClassName.dirkacid,       0.05f ),
            ( WeenieClassName.dirkelectric,   0.05f ),
            ( WeenieClassName.dirkfire,       0.05f ),
            ( WeenieClassName.dirkfrost,      0.05f ),
        };

        private static ChanceTable<WeenieClassName> T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.knife,          0.06f ),
            ( WeenieClassName.knifeacid,      0.01f ),
            ( WeenieClassName.knifeelectric,  0.01f ),
            ( WeenieClassName.knifefire,      0.01f ),
            ( WeenieClassName.knifefrost,     0.01f ),
            ( WeenieClassName.dagger,         0.06f ),
            ( WeenieClassName.daggeracid,     0.01f ),
            ( WeenieClassName.daggerelectric, 0.01f ),
            ( WeenieClassName.daggerfire,     0.01f ),
            ( WeenieClassName.daggerfrost,    0.01f ),
            ( WeenieClassName.dirk,           0.40f ),
            ( WeenieClassName.dirkacid,       0.10f ),
            ( WeenieClassName.dirkelectric,   0.10f ),
            ( WeenieClassName.dirkfire,       0.10f ),
            ( WeenieClassName.dirkfrost,      0.10f ),
        };

        private static ChanceTable<WeenieClassName> T5_T6_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.knife,          0.01f ),
            ( WeenieClassName.knifeacid,      0.01f ),
            ( WeenieClassName.knifeelectric,  0.01f ),
            ( WeenieClassName.knifefire,      0.01f ),
            ( WeenieClassName.knifefrost,     0.01f ),
            ( WeenieClassName.dagger,         0.01f ),
            ( WeenieClassName.daggeracid,     0.01f ),
            ( WeenieClassName.daggerelectric, 0.01f ),
            ( WeenieClassName.daggerfire,     0.01f ),
            ( WeenieClassName.daggerfrost,    0.01f ),
            ( WeenieClassName.dirk,           0.42f ),
            ( WeenieClassName.dirkacid,       0.12f ),
            ( WeenieClassName.dirkelectric,   0.12f ),
            ( WeenieClassName.dirkfire,       0.12f ),
            ( WeenieClassName.dirkfrost,      0.12f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> weaponTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T3_Chances,
            T1_T3_Chances,
            T1_T3_Chances,
            T4_Chances,
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
                case WeenieClassName.dirk:
                case WeenieClassName.dirkacid:
                case WeenieClassName.dirkelectric:
                case WeenieClassName.dirkfire:
                case WeenieClassName.dirkfrost:
                    weaponType = TreasureWeaponType.Dagger;
                    break;
                default:
                    weaponType = TreasureWeaponType.DaggerMS;
                    break;
            }

            return weapon;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static DaggerWcids_Aluvian_Sho()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,          3.0f ),
                    ( WeenieClassName.dagger,         3.0f ),
                    ( WeenieClassName.dirk,           0.5f ),
                };

                T1_T3_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,          4.0f ),
                    ( WeenieClassName.knifeacid,      1.0f ),
                    ( WeenieClassName.knifeelectric,  1.0f ),
                    ( WeenieClassName.knifefire,      1.0f ),
                    ( WeenieClassName.knifefrost,     1.0f ),

                    ( WeenieClassName.dagger,         4.0f ),
                    ( WeenieClassName.daggeracid,     1.0f ),
                    ( WeenieClassName.daggerelectric, 1.0f ),
                    ( WeenieClassName.daggerfire,     1.0f ),
                    ( WeenieClassName.daggerfrost,    1.0f ),

                    ( WeenieClassName.dirk,           4.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                T5_T6_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,          1.0f ),
                    ( WeenieClassName.knifeacid,      0.25f ),
                    ( WeenieClassName.knifeelectric,  0.25f ),
                    ( WeenieClassName.knifefire,      0.25f ),
                    ( WeenieClassName.knifefrost,     0.25f ),

                    ( WeenieClassName.dagger,         1.0f ),
                    ( WeenieClassName.daggeracid,     0.25f ),
                    ( WeenieClassName.daggerelectric, 0.25f ),
                    ( WeenieClassName.daggerfire,     0.25f ),
                    ( WeenieClassName.daggerfrost,    0.25f ),

                    ( WeenieClassName.dirk,           4.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                weaponTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T1_T3_Chances,
                    T1_T3_Chances,
                    T1_T3_Chances,
                    T5_T6_Chances,
                    T5_T6_Chances,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,          3.0f ),
                    ( WeenieClassName.dagger,         3.0f ),
                    ( WeenieClassName.dirk,           0.5f ),
                };

                T1_T3_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,         16.0f ),
                    ( WeenieClassName.knifeacid,      1.0f ),
                    ( WeenieClassName.knifeelectric,  1.0f ),
                    ( WeenieClassName.knifefire,      1.0f ),
                    ( WeenieClassName.knifefrost,     1.0f ),

                    ( WeenieClassName.dagger,        16.0f ),
                    ( WeenieClassName.daggeracid,     1.0f ),
                    ( WeenieClassName.daggerelectric, 1.0f ),
                    ( WeenieClassName.daggerfire,     1.0f ),
                    ( WeenieClassName.daggerfrost,    1.0f ),

                    ( WeenieClassName.dirk,          16.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                T5_T6_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.knife,           4.0f ),
                    ( WeenieClassName.knifeacid,      0.25f ),
                    ( WeenieClassName.knifeelectric,  0.25f ),
                    ( WeenieClassName.knifefire,      0.25f ),
                    ( WeenieClassName.knifefrost,     0.25f ),

                    ( WeenieClassName.dagger,          4.0f ),
                    ( WeenieClassName.daggeracid,     0.25f ),
                    ( WeenieClassName.daggerelectric, 0.25f ),
                    ( WeenieClassName.daggerfire,     0.25f ),
                    ( WeenieClassName.daggerfrost,    0.25f ),

                    ( WeenieClassName.dirk,          16.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                weaponTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T1_T3_Chances,
                    T1_T3_Chances,
                    T1_T3_Chances,
                    T5_T6_Chances,
                    T5_T6_Chances,
                };
            }

            foreach (var weaponTier in weaponTiers)
            {
                foreach (var entry in weaponTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Dagger);
            }
        }
    }
}
