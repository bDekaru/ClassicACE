using System.Collections.Generic;

using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class SpellLevelChance
    {
        /*
            1: 1-3
            2: 3-5
            3: 4-6
            4: 5-6
            5: 6-7 (should be 5-7)
            6: 6-7
            7: 7-8 (should be 6-8)
            8: 7-8 (should be 6-8)
        */

        private static ChanceTable<int> T1_SpellLevelChances = new ChanceTable<int>()
        {
            // 15/60/25?
            ( 1, 0.25f ),
            ( 2, 0.50f ),
            ( 3, 0.25f ),
        };

        private static ChanceTable<int> T2_SpellLevelChances = new ChanceTable<int>()
        {
            ( 3, 0.25f ),
            ( 4, 0.50f ),
            ( 5, 0.25f ),
        };

        private static ChanceTable<int> T3_SpellLevelChances = new ChanceTable<int>()
        {
            ( 4, 0.25f ),
            ( 5, 0.50f ),
            ( 6, 0.25f ),
        };

        private static ChanceTable<int> T4_SpellLevelChances = new ChanceTable<int>()
        {
            ( 5, 0.75f ),
            ( 6, 0.25f ),
        };

        private static ChanceTable<int> T5_SpellLevelChances = new ChanceTable<int>()
        {
            ( 5, 0.30f ),
            ( 6, 0.50f ),
            ( 7, 0.20f ),
        };

        private static ChanceTable<int> T6_SpellLevelChances = new ChanceTable<int>()
        {
            ( 6, 0.60f ),
            ( 7, 0.40f ),
        };

        private static ChanceTable<int> T7_SpellLevelChances = new ChanceTable<int>()
        {
            ( 6, 0.25f ),
            ( 7, 0.50f ),
            ( 8, 0.25f ),
        };

        private static ChanceTable<int> T8_SpellLevelChances = new ChanceTable<int>()
        {
            ( 6, 0.15f ),
            ( 7, 0.50f ),
            ( 8, 0.35f ),
        };

        private static readonly List<ChanceTable<int>> spellLevelChances = new List<ChanceTable<int>>()
        {
            T1_SpellLevelChances,
            T2_SpellLevelChances,
            T3_SpellLevelChances,
            T4_SpellLevelChances,
            T5_SpellLevelChances,
            T6_SpellLevelChances,
            T7_SpellLevelChances,
            T8_SpellLevelChances
        };

        static SpellLevelChance()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 1, 0.40f ),
                    ( 2, 0.58f ),
                    ( 3, 0.02f ),
                };

                T2_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 3, 0.58f ),
                    ( 4, 0.38f ),
                    ( 5, 0.04f ),
                };

                T3_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.65f ),
                    ( 5, 0.30f ),
                    ( 6, 0.05f ),
                };

                T4_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.10f ),
                    ( 5, 0.80f ),
                    ( 6, 0.10f ),
                };

                T5_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 5, 0.75f ),
                    ( 6, 0.25f ),
                };

                T6_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 5, 0.20f ),
                    ( 6, 0.80f ),
                };

                T7_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 6, 0.95f ),
                    ( 7, 0.05f ),
                };

                T8_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 6, 0.75f ),
                    ( 7, 0.25f ),
                };

                spellLevelChances = new List<ChanceTable<int>>()
                {
                    T1_SpellLevelChances,
                    T2_SpellLevelChances,
                    T3_SpellLevelChances,
                    T4_SpellLevelChances,
                    T5_SpellLevelChances,
                    T6_SpellLevelChances,
                    T7_SpellLevelChances,
                    T8_SpellLevelChances
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 1, 0.95f ),
                    ( 2, 0.05f ),
                };

                T2_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 2, 0.95f ),
                    ( 3, 0.05f ),
                };

                T3_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 3, 0.95f ),
                    ( 4, 0.05f ),
                };

                T4_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 4, 0.95f ),
                    ( 5, 0.05f ),
                };

                T5_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 5, 0.95f ),
                    ( 6, 0.05f ),
                };

                T6_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 6, 0.95f ),
                    ( 7, 0.05f ),
                };

                T7_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 6, 0.90f ),
                    ( 7, 0.10f ),
                };

                T8_SpellLevelChances = new ChanceTable<int>()
                {
                    ( 6, 0.75f ),
                    ( 7, 0.25f ),
                };

                spellLevelChances = new List<ChanceTable<int>>()
                {
                    T1_SpellLevelChances,
                    T2_SpellLevelChances,
                    T3_SpellLevelChances,
                    T4_SpellLevelChances,
                    T5_SpellLevelChances,
                    T6_SpellLevelChances,
                    T7_SpellLevelChances,
                    T8_SpellLevelChances
                };
            }
        }

        /// <summary>
        /// Rolls for a spell level for a tier
        /// </summary>
        public static int Roll(int tier)
        {
            return spellLevelChances[tier - 1].Roll();
        }
    }
}
