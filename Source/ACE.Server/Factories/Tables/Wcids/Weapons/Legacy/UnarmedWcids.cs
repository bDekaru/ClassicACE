using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class UnarmedWcids
    {
        private static ChanceTable<WeenieClassName> UnarmedWcids_Aluvian_T1;
        private static ChanceTable<WeenieClassName> UnarmedWcids_Aluvian = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.cestus,         0.40f ),
            ( WeenieClassName.cestusacid,     0.15f ),
            ( WeenieClassName.cestuselectric, 0.15f ),
            ( WeenieClassName.cestusfire,     0.15f ),
            ( WeenieClassName.cestusfrost,    0.15f ),
        };

        private static ChanceTable<WeenieClassName> UnarmedWcids_Gharundim_T1;
        private static ChanceTable<WeenieClassName> UnarmedWcids_Gharundim = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.katar,         0.40f ),
            ( WeenieClassName.kataracid,     0.15f ),
            ( WeenieClassName.katarelectric, 0.15f ),
            ( WeenieClassName.katarfire,     0.15f ),
            ( WeenieClassName.katarfrost,    0.15f ),
        };

        private static ChanceTable<WeenieClassName> UnarmedWcids_Sho_T1;
        private static ChanceTable<WeenieClassName> UnarmedWcids_Sho = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.nekode,         0.40f ),
            ( WeenieClassName.nekodeacid,     0.15f ),
            ( WeenieClassName.nekodeelectric, 0.15f ),
            ( WeenieClassName.nekodefire,     0.15f ),
            ( WeenieClassName.nekodefrost,    0.15f ),
        };

        public static WeenieClassName Roll(TreasureHeritageGroup heritage, int tier)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
            {
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        return UnarmedWcids_Aluvian.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        return UnarmedWcids_Gharundim.Roll();

                    case TreasureHeritageGroup.Sho:
                        return UnarmedWcids_Sho.Roll();
                }
            }
            else
            {
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        if (tier > 1)
                            return UnarmedWcids_Aluvian.Roll();
                        return UnarmedWcids_Aluvian_T1.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            return UnarmedWcids_Gharundim.Roll();
                        return UnarmedWcids_Gharundim_T1.Roll();

                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            return UnarmedWcids_Sho.Roll();
                        return UnarmedWcids_Sho_T1.Roll();
                }
            }
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static UnarmedWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                UnarmedWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.cestus,         1.00f ),
                };

                UnarmedWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.cestus,         4.00f ),
                    ( WeenieClassName.cestusacid,     1.00f ),
                    ( WeenieClassName.cestuselectric, 1.00f ),
                    ( WeenieClassName.cestusfire,     1.00f ),
                    ( WeenieClassName.cestusfrost,    1.00f ),
                };

                UnarmedWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.katar,         1.00f ),
                };

                UnarmedWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.katar,         4.00f ),
                    ( WeenieClassName.kataracid,     1.00f ),
                    ( WeenieClassName.katarelectric, 1.00f ),
                    ( WeenieClassName.katarfire,     1.00f ),
                    ( WeenieClassName.katarfrost,    1.00f ),
                };

                UnarmedWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nekode,         1.00f ),
                };

                UnarmedWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nekode,         4.00f ),
                    ( WeenieClassName.nekodeacid,     1.00f ),
                    ( WeenieClassName.nekodeelectric, 1.00f ),
                    ( WeenieClassName.nekodefire,     1.00f ),
                    ( WeenieClassName.nekodefrost,    1.00f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                UnarmedWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.cestus,         1.00f ),
                };

                UnarmedWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.cestus,        16.0f ),
                    ( WeenieClassName.cestusacid,     1.0f ),
                    ( WeenieClassName.cestuselectric, 1.0f ),
                    ( WeenieClassName.cestusfire,     1.0f ),
                    ( WeenieClassName.cestusfrost,    1.0f ),
                };

                UnarmedWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.katar,         1.00f ),
                };

                UnarmedWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.katar,        16.0f ),
                    ( WeenieClassName.kataracid,     1.0f ),
                    ( WeenieClassName.katarelectric, 1.0f ),
                    ( WeenieClassName.katarfire,     1.0f ),
                    ( WeenieClassName.katarfrost,    1.0f ),
                };

                UnarmedWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nekode,         1.00f ),
                };

                UnarmedWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nekode,        16.0f ),
                    ( WeenieClassName.nekodeacid,     1.0f ),
                    ( WeenieClassName.nekodeelectric, 1.0f ),
                    ( WeenieClassName.nekodefire,     1.0f ),
                    ( WeenieClassName.nekodefrost,    1.0f ),
                };
            }

            foreach (var entry in UnarmedWcids_Aluvian_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);
            foreach (var entry in UnarmedWcids_Aluvian)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);

            foreach (var entry in UnarmedWcids_Gharundim_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);
            foreach (var entry in UnarmedWcids_Gharundim)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);

            foreach (var entry in UnarmedWcids_Sho_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);
            foreach (var entry in UnarmedWcids_Sho)
                _combined.TryAdd(entry.result, TreasureWeaponType.Unarmed);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
