using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Database.Models.World;

namespace ACE.Server.Factories.Tables
{
    public static class ScrollLevelChance
    {
        private static ChanceTable<int> T1_ScrollLevelChances = new ChanceTable<int>()
        {
            ( 1, 0.25f ),
            ( 2, 0.50f ),
            ( 3, 0.25f )
        };

        private static ChanceTable<int> T2_ScrollLevelChances = new ChanceTable<int>()
        {
            ( 3, 0.25f ),
            ( 4, 0.50f ),
            ( 5, 0.25f )
        };

        private static ChanceTable<int> T3_ScrollLevelChances = new ChanceTable<int>()
        {
            ( 5, 0.25f ),
            ( 6, 0.50f ),
            ( 7, 0.25f )
        };

        private static ChanceTable<int> T4_ScrollLevelChances = new ChanceTable<int>()
        {
            ( 6, 0.50f ),
            ( 7, 0.50f )
        };

        private static ChanceTable<int> T5_ScrollLevelChances;

        private static ChanceTable<int> T6_ScrollLevelChances;

        private static ChanceTable<int> T5_T8_ScrollLevelChances = new ChanceTable<int>()
        {
            ( 7, 1.00f )
        };

        private static readonly List<ChanceTable<int>> scrollLevelChances = new List<ChanceTable<int>>()
        {
            T1_ScrollLevelChances,
            T2_ScrollLevelChances,
            T3_ScrollLevelChances,
            T4_ScrollLevelChances,
            T5_T8_ScrollLevelChances,
            T5_T8_ScrollLevelChances,
            T5_T8_ScrollLevelChances,
            T5_T8_ScrollLevelChances,
        };

        public static int Roll(TreasureDeath profile)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration && profile.TreasureType == 338) // Steel Chest
                return 7;

            var table = scrollLevelChances[profile.Tier - 1];

            return table.Roll(profile.LootQualityMod);
        }

        static ScrollLevelChance()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 1, 0.40f ),
                    ( 2, 0.58f ),
                    ( 3, 0.02f ),
                };

                T2_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 3, 0.58f ),
                    ( 4, 0.38f ),
                    ( 5, 0.04f ),
                };

                T3_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.65f ),
                    ( 5, 0.30f ),
                    ( 6, 0.05f ),
                };

                T4_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.10f ),
                    ( 5, 0.80f ),
                    ( 6, 0.10f ),
                };

                T5_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 5, 0.20f ),
                    ( 6, 0.80f ),
                };

                T6_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 6, 1.00f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                scrollLevelChances = new List<ChanceTable<int>>()
                {
                    T1_ScrollLevelChances,
                    T2_ScrollLevelChances,
                    T3_ScrollLevelChances,
                    T4_ScrollLevelChances,
                    T5_ScrollLevelChances,
                    T6_ScrollLevelChances,
                    T6_ScrollLevelChances,
                    T6_ScrollLevelChances,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 1, 0.88f ),
                    ( 2, 0.10f ),
                    ( 3, 0.02f ),
                };

                T2_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 2, 0.90f ),
                    ( 3, 0.10f ),
                };

                T3_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 3, 0.90f ),
                    ( 4, 0.10f ),
                };

                T4_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.90f ),
                    ( 5, 0.10f ),
                };

                T5_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 5, 0.90f ),
                    ( 6, 0.10f ),
                };

                T6_ScrollLevelChances = new ChanceTable<int>()
                {
                    ( 6, 1.00f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                scrollLevelChances = new List<ChanceTable<int>>()
                {
                    T1_ScrollLevelChances,
                    T2_ScrollLevelChances,
                    T3_ScrollLevelChances,
                    T4_ScrollLevelChances,
                    T5_ScrollLevelChances,
                    T6_ScrollLevelChances,
                    T6_ScrollLevelChances,
                    T6_ScrollLevelChances,
                };
            }
        }
    }
}
