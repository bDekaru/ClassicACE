using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class MaceWcids
    {
        private static ChanceTable<WeenieClassName> MaceWcids_Aluvian_T1;

        private static ChanceTable<WeenieClassName> MaceWcids_Aluvian = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.club,                0.13f ),
            ( WeenieClassName.clubacid,            0.03f ),
            ( WeenieClassName.clubelectric,        0.03f ),
            ( WeenieClassName.clubfire,            0.03f ),
            ( WeenieClassName.clubfrost,           0.03f ),
            ( WeenieClassName.mace,                0.13f ),
            ( WeenieClassName.maceacid,            0.03f ),
            ( WeenieClassName.maceelectric,        0.03f ),
            ( WeenieClassName.macefire,            0.03f ),
            ( WeenieClassName.macefrost,           0.03f ),
            ( WeenieClassName.morningstar,         0.13f ),
            ( WeenieClassName.morningstaracid,     0.03f ),
            ( WeenieClassName.morningstarelectric, 0.03f ),
            ( WeenieClassName.morningstarfire,     0.03f ),
            ( WeenieClassName.morningstarfrost,    0.03f ),
            ( WeenieClassName.clubspiked,          0.13f ),
            ( WeenieClassName.clubspikedacid,      0.03f ),
            ( WeenieClassName.clubspikedelectric,  0.03f ),
            ( WeenieClassName.clubspikedfire,      0.03f ),
            ( WeenieClassName.clubspikedfrost,     0.03f ),
        };

        private static ChanceTable<WeenieClassName> MaceWcids_Gharundim_T1;

        private static ChanceTable<WeenieClassName> MaceWcids_Gharundim = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.kasrullah,           0.13f ),
            ( WeenieClassName.kasrullahacid,       0.03f ),
            ( WeenieClassName.kasrullahelectric,   0.03f ),
            ( WeenieClassName.kasrullahfire,       0.03f ),
            ( WeenieClassName.kasrullahfrost,      0.03f ),
            ( WeenieClassName.dabus,               0.13f ),
            ( WeenieClassName.dabusacid,           0.03f ),
            ( WeenieClassName.dabuselectric,       0.03f ),
            ( WeenieClassName.dabusfire,           0.03f ),
            ( WeenieClassName.dabusfrost,          0.03f ),
            ( WeenieClassName.morningstar,         0.13f ),
            ( WeenieClassName.morningstaracid,     0.03f ),
            ( WeenieClassName.morningstarelectric, 0.03f ),
            ( WeenieClassName.morningstarfire,     0.03f ),
            ( WeenieClassName.morningstarfrost,    0.03f ),
            ( WeenieClassName.clubspiked,          0.13f ),
            ( WeenieClassName.clubspikedacid,      0.03f ),
            ( WeenieClassName.clubspikedelectric,  0.03f ),
            ( WeenieClassName.clubspikedfire,      0.03f ),
            ( WeenieClassName.clubspikedfrost,     0.03f ),
        };

        private static ChanceTable<WeenieClassName> MaceWcids_Sho_T1;

        private static ChanceTable<WeenieClassName> MaceWcids_Sho = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.jitte,               0.13f ),
            ( WeenieClassName.jitteacid,           0.03f ),
            ( WeenieClassName.jitteelectric,       0.03f ),
            ( WeenieClassName.jittefire,           0.03f ),
            ( WeenieClassName.jittefrost,          0.03f ),
            ( WeenieClassName.tofun,               0.13f ),
            ( WeenieClassName.tofunacid,           0.03f ),
            ( WeenieClassName.tofunelectric,       0.03f ),
            ( WeenieClassName.tofunfire,           0.03f ),
            ( WeenieClassName.tofunfrost,          0.03f ),
            ( WeenieClassName.morningstar,         0.13f ),
            ( WeenieClassName.morningstaracid,     0.03f ),
            ( WeenieClassName.morningstarelectric, 0.03f ),
            ( WeenieClassName.morningstarfire,     0.03f ),
            ( WeenieClassName.morningstarfrost,    0.03f ),
            ( WeenieClassName.clubspiked,          0.13f ),
            ( WeenieClassName.clubspikedacid,      0.03f ),
            ( WeenieClassName.clubspikedelectric,  0.03f ),
            ( WeenieClassName.clubspikedfire,      0.03f ),
            ( WeenieClassName.clubspikedfrost,     0.03f ),
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
                            weapon = MaceWcids_Aluvian.Roll();
                        else
                            weapon = MaceWcids_Aluvian_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            weapon = MaceWcids_Gharundim.Roll();
                        else
                            weapon = MaceWcids_Gharundim_T1.Roll();
                        break;
                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            weapon = MaceWcids_Sho.Roll();
                        else
                            weapon = MaceWcids_Sho_T1.Roll();
                        break;
                }

                switch (weapon)
                {
                    case WeenieClassName.ace41057_greatstarmace:
                    case WeenieClassName.ace41058_acidgreatstarmace:
                    case WeenieClassName.ace41059_lightninggreatstarmace:
                    case WeenieClassName.ace41060_flaminggreatstarmace:
                    case WeenieClassName.ace41061_frostgreatstarmace:
                    case WeenieClassName.ace41062_khandahandledmace:
                    case WeenieClassName.ace41063_acidkhandahandledmace:
                    case WeenieClassName.ace41064_lightningkhandahandledmace:
                    case WeenieClassName.ace41065_flamingkhandahandledmace:
                    case WeenieClassName.ace41066_frostkhandahandledmace:
                    case WeenieClassName.ace40635_tetsubo:
                    case WeenieClassName.ace40636_acidtetsubo:
                    case WeenieClassName.ace40637_lightningtetsubo:
                    case WeenieClassName.ace40638_flamingtetsubo:
                    case WeenieClassName.ace40639_frosttetsubo:
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
                            return MaceWcids_Aluvian.Roll();
                        return MaceWcids_Aluvian_T1.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            return MaceWcids_Gharundim.Roll();
                        return MaceWcids_Gharundim_T1.Roll();

                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            return MaceWcids_Sho.Roll();
                        return MaceWcids_Sho_T1.Roll();
                }
            }
            else
            {
                weaponType = TreasureWeaponType.Axe;
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        return MaceWcids_Aluvian.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        return MaceWcids_Gharundim.Roll();

                    case TreasureHeritageGroup.Sho:
                        return MaceWcids_Sho.Roll();
                }
            }
            weaponType = TreasureWeaponType.Undef;
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();
        static MaceWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                MaceWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.club,             1.0f ),
                    ( WeenieClassName.clubspiked,       1.0f ),

                    ( WeenieClassName.mace,             0.25f ),
                    ( WeenieClassName.morningstar,      0.25f ),
                };

                MaceWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.club,                4.0f ),
                    ( WeenieClassName.clubacid,            1.0f ),
                    ( WeenieClassName.clubelectric,        1.0f ),
                    ( WeenieClassName.clubfire,            1.0f ),
                    ( WeenieClassName.clubfrost,           1.0f ),

                    ( WeenieClassName.mace,                4.0f ),
                    ( WeenieClassName.maceacid,            1.0f ),
                    ( WeenieClassName.maceelectric,        1.0f ),
                    ( WeenieClassName.macefire,            1.0f ),
                    ( WeenieClassName.macefrost,           1.0f ),

                    ( WeenieClassName.morningstar,         3.0f ),
                    ( WeenieClassName.morningstaracid,     0.75f ),
                    ( WeenieClassName.morningstarelectric, 0.75f ),
                    ( WeenieClassName.morningstarfire,     0.75f ),
                    ( WeenieClassName.morningstarfrost,    0.75f ),

                    ( WeenieClassName.clubspiked,          3.0f ),
                    ( WeenieClassName.clubspikedacid,      0.75f ),
                    ( WeenieClassName.clubspikedelectric,  0.75f ),
                    ( WeenieClassName.clubspikedfire,      0.75f ),
                    ( WeenieClassName.clubspikedfrost,     0.75f ),
                };

                MaceWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.kasrullah,        1.0f ),
                    ( WeenieClassName.clubspiked,       1.0f ),

                    ( WeenieClassName.dabus,            0.25f ),
                    ( WeenieClassName.morningstar,      0.25f ),
                };

                MaceWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.kasrullah,           4.0f ),
                    ( WeenieClassName.kasrullahacid,       1.0f ),
                    ( WeenieClassName.kasrullahelectric,   1.0f ),
                    ( WeenieClassName.kasrullahfire,       1.0f ),
                    ( WeenieClassName.kasrullahfrost,      1.0f ),

                    ( WeenieClassName.dabus,               4.0f ),
                    ( WeenieClassName.dabusacid,           1.0f ),
                    ( WeenieClassName.dabuselectric,       1.0f ),
                    ( WeenieClassName.dabusfire,           1.0f ),
                    ( WeenieClassName.dabusfrost,          1.0f ),

                    ( WeenieClassName.morningstar,         3.0f ),
                    ( WeenieClassName.morningstaracid,     0.75f ),
                    ( WeenieClassName.morningstarelectric, 0.75f ),
                    ( WeenieClassName.morningstarfire,     0.75f ),
                    ( WeenieClassName.morningstarfrost,    0.75f ),

                    ( WeenieClassName.clubspiked,          3.0f ),
                    ( WeenieClassName.clubspikedacid,      0.75f ),
                    ( WeenieClassName.clubspikedelectric,  0.75f ),
                    ( WeenieClassName.clubspikedfire,      0.75f ),
                    ( WeenieClassName.clubspikedfrost,     0.75f ),
                };

                MaceWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jitte,            1.0f ),
                    ( WeenieClassName.clubspiked,       1.0f ),

                    ( WeenieClassName.tofun,            0.25f ),
                    ( WeenieClassName.morningstar,      0.25f ),
                };

                MaceWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jitte,               4.0f ),
                    ( WeenieClassName.jitteacid,           1.0f ),
                    ( WeenieClassName.jitteelectric,       1.0f ),
                    ( WeenieClassName.jittefire,           1.0f ),
                    ( WeenieClassName.jittefrost,          1.0f ),

                    ( WeenieClassName.tofun,               4.0f ),
                    ( WeenieClassName.tofunacid,           1.0f ),
                    ( WeenieClassName.tofunelectric,       1.0f ),
                    ( WeenieClassName.tofunfire,           1.0f ),
                    ( WeenieClassName.tofunfrost,          1.0f ),

                    ( WeenieClassName.morningstar,         3.0f ),
                    ( WeenieClassName.morningstaracid,     0.75f ),
                    ( WeenieClassName.morningstarelectric, 0.75f ),
                    ( WeenieClassName.morningstarfire,     0.75f ),
                    ( WeenieClassName.morningstarfrost,    0.75f ),

                    ( WeenieClassName.clubspiked,          3.0f ),
                    ( WeenieClassName.clubspikedacid,      0.75f ),
                    ( WeenieClassName.clubspikedelectric,  0.75f ),
                    ( WeenieClassName.clubspikedfire,      0.75f ),
                    ( WeenieClassName.clubspikedfrost,     0.75f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                MaceWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.club,             3.0f ),
                    ( WeenieClassName.clubspiked,       3.0f ),

                    ( WeenieClassName.mace,             0.50f ),
                    ( WeenieClassName.morningstar,      0.50f ),

                    ( WeenieClassName.ace41057_greatstarmace,     0.50f ),
                };

                MaceWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.club,               16.0f ),
                    ( WeenieClassName.clubacid,            1.0f ),
                    ( WeenieClassName.clubelectric,        1.0f ),
                    ( WeenieClassName.clubfire,            1.0f ),
                    ( WeenieClassName.clubfrost,           1.0f ),

                    ( WeenieClassName.mace,               16.0f ),
                    ( WeenieClassName.maceacid,            1.0f ),
                    ( WeenieClassName.maceelectric,        1.0f ),
                    ( WeenieClassName.macefire,            1.0f ),
                    ( WeenieClassName.macefrost,           1.0f ),

                    ( WeenieClassName.morningstar,        16.0f ),
                    ( WeenieClassName.morningstaracid,     1.0f ),
                    ( WeenieClassName.morningstarelectric, 1.0f ),
                    ( WeenieClassName.morningstarfire,     1.0f ),
                    ( WeenieClassName.morningstarfrost,    1.0f ),

                    ( WeenieClassName.clubspiked,         16.0f ),
                    ( WeenieClassName.clubspikedacid,      1.0f ),
                    ( WeenieClassName.clubspikedelectric,  1.0f ),
                    ( WeenieClassName.clubspikedfire,      1.0f ),
                    ( WeenieClassName.clubspikedfrost,     1.0f ),

                    ( WeenieClassName.ace41057_greatstarmace,         16.0f ),
                    ( WeenieClassName.ace41058_acidgreatstarmace,      1.0f ),
                    ( WeenieClassName.ace41059_lightninggreatstarmace, 1.0f ),
                    ( WeenieClassName.ace41060_flaminggreatstarmace,   1.0f ),
                    ( WeenieClassName.ace41061_frostgreatstarmace,     1.0f ),
                };

                MaceWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.kasrullah,        3.0f ),
                    ( WeenieClassName.clubspiked,       3.0f ),

                    ( WeenieClassName.dabus,            0.50f ),
                    ( WeenieClassName.morningstar,      0.50f ),

                    ( WeenieClassName.ace41062_khandahandledmace, 0.50f ),
                };

                MaceWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.kasrullah,          16.0f ),
                    ( WeenieClassName.kasrullahacid,       1.0f ),
                    ( WeenieClassName.kasrullahelectric,   1.0f ),
                    ( WeenieClassName.kasrullahfire,       1.0f ),
                    ( WeenieClassName.kasrullahfrost,      1.0f ),

                    ( WeenieClassName.dabus,              16.0f ),
                    ( WeenieClassName.dabusacid,           1.0f ),
                    ( WeenieClassName.dabuselectric,       1.0f ),
                    ( WeenieClassName.dabusfire,           1.0f ),
                    ( WeenieClassName.dabusfrost,          1.0f ),

                    ( WeenieClassName.morningstar,        16.0f ),
                    ( WeenieClassName.morningstaracid,     1.0f ),
                    ( WeenieClassName.morningstarelectric, 1.0f ),
                    ( WeenieClassName.morningstarfire,     1.0f ),
                    ( WeenieClassName.morningstarfrost,    1.0f ),

                    ( WeenieClassName.clubspiked,         16.0f ),
                    ( WeenieClassName.clubspikedacid,      1.0f ),
                    ( WeenieClassName.clubspikedelectric,  1.0f ),
                    ( WeenieClassName.clubspikedfire,      1.0f ),
                    ( WeenieClassName.clubspikedfrost,     1.0f ),

                    ( WeenieClassName.ace41062_khandahandledmace,         16.0f ),
                    ( WeenieClassName.ace41063_acidkhandahandledmace,      1.0f ),
                    ( WeenieClassName.ace41064_lightningkhandahandledmace, 1.0f ),
                    ( WeenieClassName.ace41065_flamingkhandahandledmace,   1.0f ),
                    ( WeenieClassName.ace41066_frostkhandahandledmace,     1.0f ),
                };

                MaceWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jitte,            3.0f ),
                    ( WeenieClassName.clubspiked,       3.0f ),

                    ( WeenieClassName.tofun,            0.50f ),
                    ( WeenieClassName.morningstar,      0.50f ),

                    ( WeenieClassName.ace40635_tetsubo,           0.50f ),
                };

                MaceWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jitte,              16.0f ),
                    ( WeenieClassName.jitteacid,           1.0f ),
                    ( WeenieClassName.jitteelectric,       1.0f ),
                    ( WeenieClassName.jittefire,           1.0f ),
                    ( WeenieClassName.jittefrost,          1.0f ),

                    ( WeenieClassName.tofun,              16.0f ),
                    ( WeenieClassName.tofunacid,           1.0f ),
                    ( WeenieClassName.tofunelectric,       1.0f ),
                    ( WeenieClassName.tofunfire,           1.0f ),
                    ( WeenieClassName.tofunfrost,          1.0f ),

                    ( WeenieClassName.morningstar,        16.0f ),
                    ( WeenieClassName.morningstaracid,     1.0f ),
                    ( WeenieClassName.morningstarelectric, 1.0f ),
                    ( WeenieClassName.morningstarfire,     1.0f ),
                    ( WeenieClassName.morningstarfrost,    1.0f ),

                    ( WeenieClassName.clubspiked,         16.0f ),
                    ( WeenieClassName.clubspikedacid,      1.0f ),
                    ( WeenieClassName.clubspikedelectric,  1.0f ),
                    ( WeenieClassName.clubspikedfire,      1.0f ),
                    ( WeenieClassName.clubspikedfrost,     1.0f ),

                    ( WeenieClassName.ace40635_tetsubo,         16.0f ),
                    ( WeenieClassName.ace40636_acidtetsubo,      1.0f ),
                    ( WeenieClassName.ace40637_lightningtetsubo, 1.0f ),
                    ( WeenieClassName.ace40638_flamingtetsubo,   1.0f ),
                    ( WeenieClassName.ace40639_frosttetsubo,     1.0f ),
                };
            }

            foreach (var entry in MaceWcids_Aluvian_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);
            foreach (var entry in MaceWcids_Aluvian)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);

            foreach (var entry in MaceWcids_Gharundim_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);
            foreach (var entry in MaceWcids_Gharundim)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);

            foreach (var entry in MaceWcids_Sho_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);
            foreach (var entry in MaceWcids_Sho)
                _combined.TryAdd(entry.result, TreasureWeaponType.Mace);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
