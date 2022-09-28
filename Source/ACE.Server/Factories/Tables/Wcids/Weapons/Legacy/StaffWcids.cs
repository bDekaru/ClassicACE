using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class StaffWcids
    {
        private static ChanceTable<WeenieClassName> StaffWcids_Aluvian_T1;

        private static ChanceTable<WeenieClassName> StaffWcids_Aluvian = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.quarterstaffnew,         0.40f ),
            ( WeenieClassName.quarterstaffacidnew,     0.15f ),
            ( WeenieClassName.quarterstaffelectricnew, 0.15f ),
            ( WeenieClassName.quarterstaffflamenew,    0.15f ),
            ( WeenieClassName.quarterstafffrostnew,    0.15f ),
        };

        private static ChanceTable<WeenieClassName> StaffWcids_Gharundim_T1;

        private static ChanceTable<WeenieClassName> StaffWcids_Gharundim = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.nabutnew,         0.40f ),
            ( WeenieClassName.nabutacidnew,     0.15f ),
            ( WeenieClassName.nabutelectricnew, 0.15f ),
            ( WeenieClassName.nabutfirenew,     0.15f ),
            ( WeenieClassName.nabutfrostnew,    0.15f ),
        };

        private static ChanceTable<WeenieClassName> StaffWcids_Sho_T1;

        private static ChanceTable<WeenieClassName> StaffWcids_Sho = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.jonew,         0.40f ),
            ( WeenieClassName.joacidnew,     0.15f ),
            ( WeenieClassName.joelectricnew, 0.15f ),
            ( WeenieClassName.jofirenew,     0.15f ),
            ( WeenieClassName.jofrostnew,    0.15f ),
        };

        public static WeenieClassName Roll(TreasureHeritageGroup heritage, int tier)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
            {
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        return StaffWcids_Aluvian.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        return StaffWcids_Gharundim.Roll();

                    case TreasureHeritageGroup.Sho:
                        return StaffWcids_Sho.Roll();
                }
            }
            else
            {
                switch (heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        if (tier > 1)
                            return StaffWcids_Aluvian.Roll();
                        return StaffWcids_Aluvian_T1.Roll();

                    case TreasureHeritageGroup.Gharundim:
                        if (tier > 1)
                            return StaffWcids_Gharundim.Roll();
                        return StaffWcids_Gharundim_T1.Roll();

                    case TreasureHeritageGroup.Sho:
                        if (tier > 1)
                            return StaffWcids_Sho.Roll();
                        return StaffWcids_Sho_T1.Roll();
                }
            }
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static StaffWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                StaffWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.quarterstaffnew,          1.0f ),
                };

                StaffWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.quarterstaffnew,         4.0f ),
                    ( WeenieClassName.quarterstaffacidnew,     1.0f ),
                    ( WeenieClassName.quarterstaffelectricnew, 1.0f ),
                    ( WeenieClassName.quarterstaffflamenew,    1.0f ),
                    ( WeenieClassName.quarterstafffrostnew,    1.0f ),
                };

                StaffWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nabutnew,               1.0f ),
                };

                StaffWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nabutnew,         4.0f ),
                    ( WeenieClassName.nabutacidnew,     1.0f ),
                    ( WeenieClassName.nabutelectricnew, 1.0f ),
                    ( WeenieClassName.nabutfirenew,     1.0f ),
                    ( WeenieClassName.nabutfrostnew,    1.0f ),
                };

                StaffWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jonew,               1.0f ),
                };

                StaffWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jonew,         4.0f ),
                    ( WeenieClassName.joacidnew,     1.0f ),
                    ( WeenieClassName.joelectricnew, 1.0f ),
                    ( WeenieClassName.jofirenew,     1.0f ),
                    ( WeenieClassName.jofrostnew,    1.0f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                StaffWcids_Aluvian_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.quarterstaff,             1.0f ),
                    ( WeenieClassName.quarterstaffnew,          1.0f ),
                };

                StaffWcids_Aluvian = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.quarterstaff,           16.0f ),
                    ( WeenieClassName.quarterstaffacid,        1.0f ),
                    ( WeenieClassName.quarterstaffelectric,    1.0f ),
                    ( WeenieClassName.quarterstaffflame,       1.0f ),
                    ( WeenieClassName.quarterstafffrost,       1.0f ),

                    ( WeenieClassName.quarterstaffnew,        16.0f ),
                    ( WeenieClassName.quarterstaffacidnew,     1.0f ),
                    ( WeenieClassName.quarterstaffelectricnew, 1.0f ),
                    ( WeenieClassName.quarterstaffflamenew,    1.0f ),
                    ( WeenieClassName.quarterstafffrostnew,    1.0f ),
                };

                StaffWcids_Gharundim_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nabut,                  1.0f ),
                    ( WeenieClassName.nabutnew,               1.0f ),
                };

                StaffWcids_Gharundim = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.nabut,           16.0f ),
                    ( WeenieClassName.nabutacid,        1.0f ),
                    ( WeenieClassName.nabutelectric,    1.0f ),
                    ( WeenieClassName.nabutfire,        1.0f ),
                    ( WeenieClassName.nabutfrost,       1.0f ),

                    ( WeenieClassName.nabutnew,        16.0f ),
                    ( WeenieClassName.nabutacidnew,     1.0f ),
                    ( WeenieClassName.nabutelectricnew, 1.0f ),
                    ( WeenieClassName.nabutfirenew,     1.0f ),
                    ( WeenieClassName.nabutfrostnew,    1.0f ),
                };

                StaffWcids_Sho_T1 = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jo,                  1.0f ),
                    ( WeenieClassName.jonew,               1.0f ),
                };

                StaffWcids_Sho = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.jo,           16.0f ),
                    ( WeenieClassName.joacid,        1.0f ),
                    ( WeenieClassName.joelectric,    1.0f ),
                    ( WeenieClassName.jofire,        1.0f ),
                    ( WeenieClassName.jofrost,       1.0f ),

                    ( WeenieClassName.jonew,        16.0f ),
                    ( WeenieClassName.joacidnew,     1.0f ),
                    ( WeenieClassName.joelectricnew, 1.0f ),
                    ( WeenieClassName.jofirenew,     1.0f ),
                    ( WeenieClassName.jofrostnew,    1.0f ),
                };
            }

            foreach (var entry in StaffWcids_Aluvian_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);
            foreach (var entry in StaffWcids_Aluvian)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);

            foreach (var entry in StaffWcids_Gharundim_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);
            foreach (var entry in StaffWcids_Gharundim)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);

            foreach (var entry in StaffWcids_Sho_T1)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);
            foreach (var entry in StaffWcids_Sho)
                _combined.TryAdd(entry.result, TreasureWeaponType.Staff);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
