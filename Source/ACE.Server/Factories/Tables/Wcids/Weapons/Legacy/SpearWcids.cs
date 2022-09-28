using System.Collections.Generic;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SpearWcids
    {
        private static ChanceTable<WeenieClassName> SpearWcids_Aluvian_T1;

        private static ChanceTable<WeenieClassName> SpearWcids_Aluvian = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.spear,              0.16f ),
            ( WeenieClassName.spearacid,          0.04f ),
            ( WeenieClassName.spearelectric,      0.04f ),
            ( WeenieClassName.spearflame,         0.04f ),
            ( WeenieClassName.spearfrost,         0.04f ),
            ( WeenieClassName.trident,            0.16f ),
            ( WeenieClassName.tridentacid,        0.04f ),
            ( WeenieClassName.tridentelectric,    0.04f ),
            ( WeenieClassName.tridentfire,        0.04f ),
            ( WeenieClassName.tridentfrost,       0.04f ),
            ( WeenieClassName.swordstaff,         0.16f ),
            ( WeenieClassName.swordstaffacid,     0.05f ),
            ( WeenieClassName.swordstaffelectric, 0.05f ),
            ( WeenieClassName.swordstafffire,     0.05f ),
            ( WeenieClassName.swordstafffrost,    0.05f ),
        };

        private static ChanceTable<WeenieClassName> SpearWcids_Gharundim_T1;

        private static ChanceTable<WeenieClassName> SpearWcids_Gharundim = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.budiaq,             0.16f ),
            ( WeenieClassName.budiaqacid,         0.04f ),
            ( WeenieClassName.budiaqelectric,     0.04f ),
            ( WeenieClassName.budiaqfire,         0.04f ),
            ( WeenieClassName.budiaqfrost,        0.04f ),
            ( WeenieClassName.trident,            0.16f ),
            ( WeenieClassName.tridentacid,        0.04f ),
            ( WeenieClassName.tridentelectric,    0.04f ),
            ( WeenieClassName.tridentfire,        0.04f ),
            ( WeenieClassName.tridentfrost,       0.04f ),
            ( WeenieClassName.swordstaff,         0.16f ),
            ( WeenieClassName.swordstaffacid,     0.05f ),
            ( WeenieClassName.swordstaffelectric, 0.05f ),
            ( WeenieClassName.swordstafffire,     0.05f ),
            ( WeenieClassName.swordstafffrost,    0.05f ),
        };

        private static ChanceTable<WeenieClassName> SpearWcids_Sho_T1 = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.yari,                0.34f ),
            ( WeenieClassName.trident,             0.33f ),
            ( WeenieClassName.swordstaff,          0.33f ),
        };

        private static ChanceTable<WeenieClassName> SpearWcids_Sho = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.yari,               0.16f ),
            ( WeenieClassName.yariacid,           0.04f ),
            ( WeenieClassName.yarielectric,       0.04f ),
            ( WeenieClassName.yarifire,           0.04f ),
            ( WeenieClassName.yarifrost,          0.04f ),
            ( WeenieClassName.trident,            0.16f ),
            ( WeenieClassName.tridentacid,        0.04f ),
            ( WeenieClassName.tridentelectric,    0.04f ),
            ( WeenieClassName.tridentfire,        0.04f ),
            ( WeenieClassName.tridentfrost,       0.04f ),
            ( WeenieClassName.swordstaff,         0.16f ),
            ( WeenieClassName.swordstaffacid,     0.05f ),
            ( WeenieClassName.swordstaffelectric, 0.05f ),
            ( WeenieClassName.swordstafffire,     0.05f ),
            ( WeenieClassName.swordstafffrost,    0.05f ),
        };

        public static WeenieClassName Roll(TreasureHeritageGroup heritage, int tier, out TreasureWeaponType weaponType)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                WeenieClassName weapon = WeenieClassName.undef;
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        if (tier > 1)
                            weapon = SpearWcids_Aluvian.Roll();
                        else
                            weapon = SpearWcids_Aluvian_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            weapon = SpearWcids_Gharundim.Roll();
                        else
                            weapon = SpearWcids_Gharundim_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            weapon = SpearWcids_Sho.Roll();
                        else
                            weapon = SpearWcids_Sho_T1.Roll();
                        break;
                }

                switch (weapon)
                {
                    case WeenieClassName.ace41046_pike:
                    case WeenieClassName.ace41047_acidpike:
                    case WeenieClassName.ace41048_lightningpike:
                    case WeenieClassName.ace41049_flamingpike:
                    case WeenieClassName.ace41050_frostpike:
                    case WeenieClassName.ace41036_assagai:
                    case WeenieClassName.ace41037_acidassagai:
                    case WeenieClassName.ace41038_lightningassagai:
                    case WeenieClassName.ace41039_flamingassagai:
                    case WeenieClassName.ace41040_frostassagai:
                    case WeenieClassName.ace41041_magariyari:
                    case WeenieClassName.ace41042_acidmagariyari:
                    case WeenieClassName.ace41043_lightningmagariyari:
                    case WeenieClassName.ace41044_flamingmagariyari:
                    case WeenieClassName.ace41045_frostmagariyari:
                        weaponType = TreasureWeaponType.TwoHandedMace;
                        break;
                    default:
                        weaponType = TreasureWeaponType.Mace;
                        break;
                }

                return weapon;
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                weaponType = TreasureWeaponType.Axe;
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        if (tier > 1)
                            return SpearWcids_Aluvian.Roll();
                        return SpearWcids_Aluvian_T1.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            return SpearWcids_Gharundim.Roll();
                        return SpearWcids_Gharundim_T1.Roll();

                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            return SpearWcids_Sho.Roll();
                        return SpearWcids_Sho_T1.Roll();
                }
            }
            else
            {
                weaponType = TreasureWeaponType.Axe;
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        return SpearWcids_Aluvian.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        return SpearWcids_Gharundim.Roll();

                    case TreasureHeritageGroup.Sho:
                        return SpearWcids_Sho.Roll();
                }
            }
            weaponType = TreasureWeaponType.Undef;
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static SpearWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                SpearWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.spear,               1.0f ),

                    ( WeenieClassName.trident,             0.25f ),
                    ( WeenieClassName.swordstaff,          0.25f ),
                };

                SpearWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.spear,              4.0f ),
                    ( WeenieClassName.spearacid,          1.0f ),
                    ( WeenieClassName.spearelectric,      1.0f ),
                    ( WeenieClassName.spearflame,         1.0f ),
                    ( WeenieClassName.spearfrost,         1.0f ),

                    ( WeenieClassName.trident,            4.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,         3.0f ),
                    ( WeenieClassName.swordstaffacid,     0.75f ),
                    ( WeenieClassName.swordstaffelectric, 0.75f ),
                    ( WeenieClassName.swordstafffire,     0.75f ),
                    ( WeenieClassName.swordstafffrost,    0.75f ),
                };

                SpearWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.budiaq,              1.00f ),

                    ( WeenieClassName.trident,             0.25f ),
                    ( WeenieClassName.swordstaff,          0.25f ),
                };

                SpearWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.budiaq,             4.0f ),
                    ( WeenieClassName.budiaqacid,         1.0f ),
                    ( WeenieClassName.budiaqelectric,     1.0f ),
                    ( WeenieClassName.budiaqfire,         1.0f ),
                    ( WeenieClassName.budiaqfrost,        1.0f ),

                    ( WeenieClassName.trident,            4.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,         3.0f ),
                    ( WeenieClassName.swordstaffacid,     0.75f ),
                    ( WeenieClassName.swordstaffelectric, 0.75f ),
                    ( WeenieClassName.swordstafffire,     0.75f ),
                    ( WeenieClassName.swordstafffrost,    0.75f ),
                };

                SpearWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yari,                1.0f ),

                    ( WeenieClassName.trident,             0.25f ),
                    ( WeenieClassName.swordstaff,          0.25f ),
                };

                SpearWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yari,               4.0f ),
                    ( WeenieClassName.yariacid,           1.0f ),
                    ( WeenieClassName.yarielectric,       1.0f ),
                    ( WeenieClassName.yarifire,           1.0f ),
                    ( WeenieClassName.yarifrost,          1.0f ),

                    ( WeenieClassName.trident,            4.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,         3.0f ),
                    ( WeenieClassName.swordstaffacid,     0.75f ),
                    ( WeenieClassName.swordstaffelectric, 0.75f ),
                    ( WeenieClassName.swordstafffire,     0.75f ),
                    ( WeenieClassName.swordstafffrost,    0.75f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                SpearWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.spear,               3.0f ),

                    ( WeenieClassName.trident,             0.50f ),
                    ( WeenieClassName.swordstaff,          0.50f ),

                    ( WeenieClassName.ace41046_pike,       0.50f ),
                };

                SpearWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.spear,             16.0f ),
                    ( WeenieClassName.spearacid,          1.0f ),
                    ( WeenieClassName.spearelectric,      1.0f ),
                    ( WeenieClassName.spearflame,         1.0f ),
                    ( WeenieClassName.spearfrost,         1.0f ),

                    ( WeenieClassName.trident,           16.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,        16.0f ),
                    ( WeenieClassName.swordstaffacid,     1.0f ),
                    ( WeenieClassName.swordstaffelectric, 1.0f ),
                    ( WeenieClassName.swordstafffire,     1.0f ),
                    ( WeenieClassName.swordstafffrost,    1.0f ),

                    ( WeenieClassName.ace41046_pike,         16.0f ),
                    ( WeenieClassName.ace41047_acidpike,      1.0f ),
                    ( WeenieClassName.ace41048_lightningpike, 1.0f ),
                    ( WeenieClassName.ace41049_flamingpike,   1.0f ),
                    ( WeenieClassName.ace41050_frostpike,     1.0f ),
                };

                SpearWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.budiaq,              3.00f ),

                    ( WeenieClassName.trident,             0.50f ),
                    ( WeenieClassName.swordstaff,          0.50f ),

                    ( WeenieClassName.ace41036_assagai,    0.50f ),
                };

                SpearWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.budiaq,            16.0f ),
                    ( WeenieClassName.budiaqacid,         1.0f ),
                    ( WeenieClassName.budiaqelectric,     1.0f ),
                    ( WeenieClassName.budiaqfire,         1.0f ),
                    ( WeenieClassName.budiaqfrost,        1.0f ),

                    ( WeenieClassName.trident,           16.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,        16.0f ),
                    ( WeenieClassName.swordstaffacid,     1.0f ),
                    ( WeenieClassName.swordstaffelectric, 1.0f ),
                    ( WeenieClassName.swordstafffire,     1.0f ),
                    ( WeenieClassName.swordstafffrost,    1.0f ),

                    ( WeenieClassName.ace41036_assagai,         16.0f ),
                    ( WeenieClassName.ace41037_acidassagai,      1.0f ),
                    ( WeenieClassName.ace41038_lightningassagai, 1.0f ),
                    ( WeenieClassName.ace41039_flamingassagai,   1.0f ),
                    ( WeenieClassName.ace41040_frostassagai,     1.0f ),
                };

                SpearWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yari,                3.00f ),

                    ( WeenieClassName.trident,             0.50f ),
                    ( WeenieClassName.swordstaff,          0.50f ),

                    ( WeenieClassName.ace41041_magariyari, 0.50f ),
                };

                SpearWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.yari,              16.0f ),
                    ( WeenieClassName.yariacid,           1.0f ),
                    ( WeenieClassName.yarielectric,       1.0f ),
                    ( WeenieClassName.yarifire,           1.0f ),
                    ( WeenieClassName.yarifrost,          1.0f ),

                    ( WeenieClassName.trident,           16.0f ),
                    ( WeenieClassName.tridentacid,        1.0f ),
                    ( WeenieClassName.tridentelectric,    1.0f ),
                    ( WeenieClassName.tridentfire,        1.0f ),
                    ( WeenieClassName.tridentfrost,       1.0f ),

                    ( WeenieClassName.swordstaff,        16.0f ),
                    ( WeenieClassName.swordstaffacid,     1.0f ),
                    ( WeenieClassName.swordstaffelectric, 1.0f ),
                    ( WeenieClassName.swordstafffire,     1.0f ),
                    ( WeenieClassName.swordstafffrost,    1.0f ),

                    ( WeenieClassName.ace41041_magariyari,         16.0f ),
                    ( WeenieClassName.ace41042_acidmagariyari,      1.0f ),
                    ( WeenieClassName.ace41043_lightningmagariyari, 1.0f ),
                    ( WeenieClassName.ace41044_flamingmagariyari,   1.0f ),
                    ( WeenieClassName.ace41045_frostmagariyari,     1.0f ),
                };
            }

            foreach (var entry in SpearWcids_Aluvian_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);
            foreach (var entry in SpearWcids_Aluvian)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);

            foreach (var entry in SpearWcids_Gharundim_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);
            foreach (var entry in SpearWcids_Gharundim)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);

            foreach (var entry in SpearWcids_Sho_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);
            foreach (var entry in SpearWcids_Sho)
                _combined.TryAdd(entry.result, TreasureWeaponType.Spear);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
