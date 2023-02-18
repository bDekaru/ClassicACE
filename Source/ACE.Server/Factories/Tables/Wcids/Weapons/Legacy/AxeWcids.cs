using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class AxeWcids
    {
        private static ChanceTable<WeenieClassName> AxeWcids_Aluvian_T1;

        private static ChanceTable<WeenieClassName> AxeWcids_Aluvian = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.axehand,           0.16f ),
            ( WeenieClassName.axehandacid,       0.04f ),
            ( WeenieClassName.axehandelectric,   0.04f ),
            ( WeenieClassName.axehandfire,       0.04f ),
            ( WeenieClassName.axehandfrost,      0.04f ),
            ( WeenieClassName.axebattle,         0.16f ),
            ( WeenieClassName.axebattleacid,     0.04f ),
            ( WeenieClassName.axebattleelectric, 0.04f ),
            ( WeenieClassName.axebattlefire,     0.04f ),
            ( WeenieClassName.axebattlefrost,    0.04f ),
            ( WeenieClassName.warhammer,         0.16f ),
            ( WeenieClassName.warhammeracid,     0.05f ),
            ( WeenieClassName.warhammerelectric, 0.05f ),
            ( WeenieClassName.warhammerfire,     0.05f ),
            ( WeenieClassName.warhammerfrost,    0.05f ),
        };

        private static ChanceTable<WeenieClassName> AxeWcids_Gharundim_T1;

        private static ChanceTable<WeenieClassName> AxeWcids_Gharundim = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.tungi,             0.16f ),
            ( WeenieClassName.tungiacid,         0.04f ),
            ( WeenieClassName.tungielectric,     0.04f ),
            ( WeenieClassName.tungifire,         0.04f ),
            ( WeenieClassName.tungifrost,        0.04f ),
            ( WeenieClassName.silifi,            0.16f ),
            ( WeenieClassName.silifiacid,        0.04f ),
            ( WeenieClassName.silifielectric,    0.04f ),
            ( WeenieClassName.silififire,        0.04f ),
            ( WeenieClassName.silififrost,       0.04f ),
            ( WeenieClassName.warhammer,         0.16f ),
            ( WeenieClassName.warhammeracid,     0.05f ),
            ( WeenieClassName.warhammerelectric, 0.05f ),
            ( WeenieClassName.warhammerfire,     0.05f ),
            ( WeenieClassName.warhammerfrost,    0.05f ),
        };

        private static ChanceTable<WeenieClassName> AxeWcids_Sho_T1;

        private static ChanceTable<WeenieClassName> AxeWcids_Sho = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shouono,           0.16f ),
            ( WeenieClassName.shouonoacid,       0.04f ),
            ( WeenieClassName.shouonoelectric,   0.04f ),
            ( WeenieClassName.shouonofire,       0.04f ),
            ( WeenieClassName.shouonofrost,      0.04f ),
            ( WeenieClassName.ono,               0.16f ),
            ( WeenieClassName.onoacid,           0.04f ),
            ( WeenieClassName.onoelectric,       0.04f ),
            ( WeenieClassName.onofire,           0.04f ),
            ( WeenieClassName.onofrost,          0.04f ),
            ( WeenieClassName.warhammer,         0.16f ),
            ( WeenieClassName.warhammeracid,     0.05f ),
            ( WeenieClassName.warhammerelectric, 0.05f ),
            ( WeenieClassName.warhammerfire,     0.05f ),
            ( WeenieClassName.warhammerfrost,    0.05f ),
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
                            weapon = AxeWcids_Aluvian.Roll();
                        else
                            weapon = AxeWcids_Aluvian_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            weapon = AxeWcids_Gharundim.Roll();
                        else
                            weapon = AxeWcids_Gharundim_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            weapon = AxeWcids_Sho.Roll();
                        else
                            weapon = AxeWcids_Sho_T1.Roll();
                        break;
                }

                switch (weapon)
                {
                    case WeenieClassName.ace41052_greataxe:
                    case WeenieClassName.ace41053_acidgreataxe:
                    case WeenieClassName.ace41054_lightninggreataxe:
                    case WeenieClassName.ace41055_flaminggreataxe:
                    case WeenieClassName.ace41056_frostgreataxe:
                        weaponType = TreasureWeaponType.TwoHandedAxe;
                        break;
                    default:
                        weaponType = TreasureWeaponType.Axe;
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
                            return AxeWcids_Aluvian.Roll();
                        return AxeWcids_Aluvian_T1.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            return AxeWcids_Gharundim.Roll();
                        return AxeWcids_Gharundim_T1.Roll();

                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            return AxeWcids_Sho.Roll();
                        return AxeWcids_Sho_T1.Roll();
                }
            }
            else
            {
                weaponType = TreasureWeaponType.Axe;
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        return AxeWcids_Aluvian.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        return AxeWcids_Gharundim.Roll();

                    case TreasureHeritageGroup.Sho:
                        return AxeWcids_Sho.Roll();
                }
            }
            weaponType = TreasureWeaponType.Undef;
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static AxeWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                AxeWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.axehand,           3.0f ),
                    ( WeenieClassName.axebattle,         0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                };

                AxeWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.axehand,           4.0f ),
                    ( WeenieClassName.axehandacid,       1.0f ),
                    ( WeenieClassName.axehandelectric,   1.0f ),
                    ( WeenieClassName.axehandfire,       1.0f ),
                    ( WeenieClassName.axehandfrost,      1.0f ),

                    ( WeenieClassName.axebattle,         4.0f ),
                    ( WeenieClassName.axebattleacid,     1.0f ),
                    ( WeenieClassName.axebattleelectric, 1.0f ),
                    ( WeenieClassName.axebattlefire,     1.0f ),
                    ( WeenieClassName.axebattlefrost,    1.0f ),

                    ( WeenieClassName.warhammer,         3.0f ),
                    ( WeenieClassName.warhammeracid,     0.75f ),
                    ( WeenieClassName.warhammerelectric, 0.75f ),
                    ( WeenieClassName.warhammerfire,     0.75f ),
                    ( WeenieClassName.warhammerfrost,    0.75f ),
                };

                AxeWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.tungi,             3.0f ),
                    ( WeenieClassName.silifi,            0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                };

                AxeWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.tungi,             4.0f ),
                    ( WeenieClassName.tungiacid,         1.0f ),
                    ( WeenieClassName.tungielectric,     1.0f ),
                    ( WeenieClassName.tungifire,         1.0f ),
                    ( WeenieClassName.tungifrost,        1.0f ),

                    ( WeenieClassName.silifi,            4.0f ),
                    ( WeenieClassName.silifiacid,        1.0f ),
                    ( WeenieClassName.silifielectric,    1.0f ),
                    ( WeenieClassName.silififire,        1.0f ),
                    ( WeenieClassName.silififrost,       1.0f ),

                    ( WeenieClassName.warhammer,         3.0f ),
                    ( WeenieClassName.warhammeracid,     0.75f ),
                    ( WeenieClassName.warhammerelectric, 0.75f ),
                    ( WeenieClassName.warhammerfire,     0.75f ),
                    ( WeenieClassName.warhammerfrost,    0.75f ),
                };

                AxeWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shouono,           3.0f ),
                    ( WeenieClassName.ono,               0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                };

                AxeWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shouono,           4.0f ),
                    ( WeenieClassName.shouonoacid,       1.0f ),
                    ( WeenieClassName.shouonoelectric,   1.0f ),
                    ( WeenieClassName.shouonofire,       1.0f ),
                    ( WeenieClassName.shouonofrost,      1.0f ),

                    ( WeenieClassName.ono,               4.0f ),
                    ( WeenieClassName.onoacid,           1.0f ),
                    ( WeenieClassName.onoelectric,       1.0f ),
                    ( WeenieClassName.onofire,           1.0f ),
                    ( WeenieClassName.onofrost,          1.0f ),

                    ( WeenieClassName.warhammer,         3.0f ),
                    ( WeenieClassName.warhammeracid,     0.75f ),
                    ( WeenieClassName.warhammerelectric, 0.75f ),
                    ( WeenieClassName.warhammerfire,     0.75f ),
                    ( WeenieClassName.warhammerfrost,    0.75f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                AxeWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.axehand,           3.0f ),
                    ( WeenieClassName.axebattle,         0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                    ( WeenieClassName.ace41052_greataxe, 0.5f ),
                };

                AxeWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.axehand,          16.0f ),
                    ( WeenieClassName.axehandacid,       1.0f ),
                    ( WeenieClassName.axehandelectric,   1.0f ),
                    ( WeenieClassName.axehandfire,       1.0f ),
                    ( WeenieClassName.axehandfrost,      1.0f ),

                    ( WeenieClassName.axebattle,        16.0f ),
                    ( WeenieClassName.axebattleacid,     1.0f ),
                    ( WeenieClassName.axebattleelectric, 1.0f ),
                    ( WeenieClassName.axebattlefire,     1.0f ),
                    ( WeenieClassName.axebattlefrost,    1.0f ),

                    ( WeenieClassName.warhammer,        16.0f ),
                    ( WeenieClassName.warhammeracid,     1.0f ),
                    ( WeenieClassName.warhammerelectric, 1.0f ),
                    ( WeenieClassName.warhammerfire,     1.0f ),
                    ( WeenieClassName.warhammerfrost,    1.0f ),

                    ( WeenieClassName.ace41052_greataxe,         16.0f ),
                    ( WeenieClassName.ace41053_acidgreataxe,      1.0f ),
                    ( WeenieClassName.ace41054_lightninggreataxe, 1.0f ),
                    ( WeenieClassName.ace41055_flaminggreataxe,   1.0f ),
                    ( WeenieClassName.ace41056_frostgreataxe,     1.0f ),
                };

                AxeWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.tungi,             3.0f ),
                    ( WeenieClassName.silifi,            0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                    ( WeenieClassName.ace41052_greataxe, 0.5f ),
                };

                AxeWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.tungi,            16.0f ),
                    ( WeenieClassName.tungiacid,         1.0f ),
                    ( WeenieClassName.tungielectric,     1.0f ),
                    ( WeenieClassName.tungifire,         1.0f ),
                    ( WeenieClassName.tungifrost,        1.0f ),

                    ( WeenieClassName.silifi,           16.0f ),
                    ( WeenieClassName.silifiacid,        1.0f ),
                    ( WeenieClassName.silifielectric,    1.0f ),
                    ( WeenieClassName.silififire,        1.0f ),
                    ( WeenieClassName.silififrost,       1.0f ),

                    ( WeenieClassName.warhammer,        16.0f ),
                    ( WeenieClassName.warhammeracid,     1.0f ),
                    ( WeenieClassName.warhammerelectric, 1.0f ),
                    ( WeenieClassName.warhammerfire,     1.0f ),
                    ( WeenieClassName.warhammerfrost,    1.0f ),

                    ( WeenieClassName.ace41052_greataxe,         16.0f ),
                    ( WeenieClassName.ace41053_acidgreataxe,      1.0f ),
                    ( WeenieClassName.ace41054_lightninggreataxe, 1.0f ),
                    ( WeenieClassName.ace41055_flaminggreataxe,   1.0f ),
                    ( WeenieClassName.ace41056_frostgreataxe,     1.0f ),
                };


                AxeWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shouono,           3.0f ),
                    ( WeenieClassName.ono,               0.5f ),
                    ( WeenieClassName.warhammer,         0.5f ),
                    ( WeenieClassName.ace41052_greataxe, 0.5f ),
                };

                AxeWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shouono,          16.0f ),
                    ( WeenieClassName.shouonoacid,       1.0f ),
                    ( WeenieClassName.shouonoelectric,   1.0f ),
                    ( WeenieClassName.shouonofire,       1.0f ),
                    ( WeenieClassName.shouonofrost,      1.0f ),

                    ( WeenieClassName.ono,              16.0f ),
                    ( WeenieClassName.onoacid,           1.0f ),
                    ( WeenieClassName.onoelectric,       1.0f ),
                    ( WeenieClassName.onofire,           1.0f ),
                    ( WeenieClassName.onofrost,          1.0f ),

                    ( WeenieClassName.warhammer,        16.0f ),
                    ( WeenieClassName.warhammeracid,     1.0f ),
                    ( WeenieClassName.warhammerelectric, 1.0f ),
                    ( WeenieClassName.warhammerfire,     1.0f ),
                    ( WeenieClassName.warhammerfrost,    1.0f ),

                    ( WeenieClassName.ace41052_greataxe,         16.0f ),
                    ( WeenieClassName.ace41053_acidgreataxe,      1.0f ),
                    ( WeenieClassName.ace41054_lightninggreataxe, 1.0f ),
                    ( WeenieClassName.ace41055_flaminggreataxe,   1.0f ),
                    ( WeenieClassName.ace41056_frostgreataxe,     1.0f ),
                };
            }

            foreach (var entry in AxeWcids_Aluvian_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);
            foreach (var entry in AxeWcids_Aluvian)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);

            foreach (var entry in AxeWcids_Gharundim_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);
            foreach (var entry in AxeWcids_Gharundim)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);

            foreach (var entry in AxeWcids_Sho_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);
            foreach (var entry in AxeWcids_Sho)
                _combined.TryAdd(entry.result, TreasureWeaponType.Axe);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
