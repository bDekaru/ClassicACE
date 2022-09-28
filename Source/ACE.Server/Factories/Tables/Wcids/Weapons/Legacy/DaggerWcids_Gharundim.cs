using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class DaggerWcids_Gharundim
    {
        private static ChanceTable<WeenieClassName> T1_Chances;

        private static ChanceTable<WeenieClassName> T1_T3_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.jambiya,         0.16f ),
            ( WeenieClassName.jambiyaacid,     0.04f ),
            ( WeenieClassName.jambiyaelectric, 0.04f ),
            ( WeenieClassName.jambiyafire,     0.04f ),
            ( WeenieClassName.jambiyafrost,    0.04f ),
            ( WeenieClassName.khanjar,         0.16f ),
            ( WeenieClassName.khanjaracid,     0.04f ),
            ( WeenieClassName.khanjarelectric, 0.04f ),
            ( WeenieClassName.khanjarfire,     0.04f ),
            ( WeenieClassName.khanjarfrost,    0.04f ),
            ( WeenieClassName.dirk,            0.16f ),
            ( WeenieClassName.dirkacid,        0.05f ),
            ( WeenieClassName.dirkelectric,    0.05f ),
            ( WeenieClassName.dirkfire,        0.05f ),
            ( WeenieClassName.dirkfrost,       0.05f ),
        };

        private static ChanceTable<WeenieClassName> T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.jambiya,         0.06f ),
            ( WeenieClassName.jambiyaacid,     0.01f ),
            ( WeenieClassName.jambiyaelectric, 0.01f ),
            ( WeenieClassName.jambiyafire,     0.01f ),
            ( WeenieClassName.jambiyafrost,    0.01f ),
            ( WeenieClassName.khanjar,         0.06f ),
            ( WeenieClassName.khanjaracid,     0.01f ),
            ( WeenieClassName.khanjarelectric, 0.01f ),
            ( WeenieClassName.khanjarfire,     0.01f ),
            ( WeenieClassName.khanjarfrost,    0.01f ),
            ( WeenieClassName.dirk,            0.40f ),
            ( WeenieClassName.dirkacid,        0.10f ),
            ( WeenieClassName.dirkelectric,    0.10f ),
            ( WeenieClassName.dirkfire,        0.10f ),
            ( WeenieClassName.dirkfrost,       0.10f ),
        };

        private static ChanceTable<WeenieClassName> T5_T6_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.jambiya,         0.01f ),
            ( WeenieClassName.jambiyaacid,     0.01f ),
            ( WeenieClassName.jambiyaelectric, 0.01f ),
            ( WeenieClassName.jambiyafire,     0.01f ),
            ( WeenieClassName.jambiyafrost,    0.01f ),
            ( WeenieClassName.khanjar,         0.01f ),
            ( WeenieClassName.khanjaracid,     0.01f ),
            ( WeenieClassName.khanjarelectric, 0.01f ),
            ( WeenieClassName.khanjarfire,     0.01f ),
            ( WeenieClassName.khanjarfrost,    0.01f ),
            ( WeenieClassName.dirk,            0.42f ),
            ( WeenieClassName.dirkacid,        0.12f ),
            ( WeenieClassName.dirkelectric,    0.12f ),
            ( WeenieClassName.dirkfire,        0.12f ),
            ( WeenieClassName.dirkfrost,       0.12f ),
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

        static DaggerWcids_Gharundim()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jambiya,        3.0f ),
                    ( WeenieClassName.khanjar,        3.0f ),
                    ( WeenieClassName.dirk,           0.5f ),
                };

                T1_T3_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jambiya,          4.0f ),
                    ( WeenieClassName.jambiyaacid,      1.0f ),
                    ( WeenieClassName.jambiyaelectric,  1.0f ),
                    ( WeenieClassName.jambiyafire,      1.0f ),
                    ( WeenieClassName.jambiyafrost,     1.0f ),

                    ( WeenieClassName.khanjar,         4.0f ),
                    ( WeenieClassName.khanjaracid,     1.0f ),
                    ( WeenieClassName.khanjarelectric, 1.0f ),
                    ( WeenieClassName.khanjarfire,     1.0f ),
                    ( WeenieClassName.khanjarfrost,    1.0f ),

                    ( WeenieClassName.dirk,           4.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                T5_T6_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jambiya,          1.0f ),
                    ( WeenieClassName.jambiyaacid,      0.25f ),
                    ( WeenieClassName.jambiyaelectric,  0.25f ),
                    ( WeenieClassName.jambiyafire,      0.25f ),
                    ( WeenieClassName.jambiyafrost,     0.25f ),

                    ( WeenieClassName.khanjar,         1.0f ),
                    ( WeenieClassName.khanjaracid,     0.25f ),
                    ( WeenieClassName.khanjarelectric, 0.25f ),
                    ( WeenieClassName.khanjarfire,     0.25f ),
                    ( WeenieClassName.khanjarfrost,    0.25f ),

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
                    ( WeenieClassName.jambiya,        3.0f ),
                    ( WeenieClassName.khanjar,        3.0f ),
                    ( WeenieClassName.dirk,           0.5f ),
                };

                T1_T3_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jambiya,         16.0f ),
                    ( WeenieClassName.jambiyaacid,      1.0f ),
                    ( WeenieClassName.jambiyaelectric,  1.0f ),
                    ( WeenieClassName.jambiyafire,      1.0f ),
                    ( WeenieClassName.jambiyafrost,     1.0f ),

                    ( WeenieClassName.khanjar,        16.0f ),
                    ( WeenieClassName.khanjaracid,     1.0f ),
                    ( WeenieClassName.khanjarelectric, 1.0f ),
                    ( WeenieClassName.khanjarfire,     1.0f ),
                    ( WeenieClassName.khanjarfrost,    1.0f ),

                    ( WeenieClassName.dirk,          16.0f ),
                    ( WeenieClassName.dirkacid,       1.0f ),
                    ( WeenieClassName.dirkelectric,   1.0f ),
                    ( WeenieClassName.dirkfire,       1.0f ),
                    ( WeenieClassName.dirkfrost,      1.0f ),
                };

                T5_T6_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jambiya,           4.0f ),
                    ( WeenieClassName.jambiyaacid,      0.25f ),
                    ( WeenieClassName.jambiyaelectric,  0.25f ),
                    ( WeenieClassName.jambiyafire,      0.25f ),
                    ( WeenieClassName.jambiyafrost,     0.25f ),

                    ( WeenieClassName.khanjar,          4.0f ),
                    ( WeenieClassName.khanjaracid,     0.25f ),
                    ( WeenieClassName.khanjarelectric, 0.25f ),
                    ( WeenieClassName.khanjarfire,     0.25f ),
                    ( WeenieClassName.khanjarfrost,    0.25f ),

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

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
