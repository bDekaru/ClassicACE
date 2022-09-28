using System.Collections.Generic;

using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class AllegianceRankChance
    {
        private static ChanceTable<int> T1_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.85f ),
            ( 2, 0.15f ),
        };

        private static ChanceTable<int> T2_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.18f ),
            ( 2, 0.58f ),
            ( 3, 0.18f ),
            ( 4, 0.06f ),
        };

        private static ChanceTable<int> T3_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.07f ),
            ( 2, 0.18f ),
            ( 3, 0.46f ),
            ( 4, 0.18f ),
            ( 5, 0.08f ),
            ( 6, 0.03f ),
        };

        private static ChanceTable<int> T4_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.040f ),
            ( 2, 0.080f ),
            ( 3, 0.180f ),
            ( 4, 0.380f ),
            ( 5, 0.180f ),
            ( 6, 0.080f ),
            ( 7, 0.040f ),
            ( 8, 0.020f ),
        };

        private static ChanceTable<int> T5_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.050f ),
            ( 2, 0.090f ),
            ( 3, 0.170f ),
            ( 4, 0.330f ),
            ( 5, 0.170f ),
            ( 6, 0.090f ),
            ( 7, 0.050f ),
            ( 8, 0.028f ),
            ( 9, 0.015f ),
           ( 10, 0.007f ),
        };

        private static ChanceTable<int> T6_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.050f ),
            ( 2, 0.090f ),
            ( 3, 0.170f ),
            ( 4, 0.330f ),
            ( 5, 0.170f ),
            ( 6, 0.090f ),
            ( 7, 0.050f ),
            ( 8, 0.028f ),
            ( 9, 0.015f ),
           ( 10, 0.007f ),
        };

        private static ChanceTable<int> T7_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.050f ),
            ( 2, 0.090f ),
            ( 3, 0.170f ),
            ( 4, 0.330f ),
            ( 5, 0.170f ),
            ( 6, 0.090f ),
            ( 7, 0.050f ),
            ( 8, 0.028f ),
            ( 9, 0.015f ),
           ( 10, 0.007f ),
        };

        private static ChanceTable<int> T8_AllegianceRankChances = new ChanceTable<int>()
        {
            ( 1, 0.050f ),
            ( 2, 0.090f ),
            ( 3, 0.170f ),
            ( 4, 0.330f ),
            ( 5, 0.170f ),
            ( 6, 0.090f ),
            ( 7, 0.050f ),
            ( 8, 0.028f ),
            ( 9, 0.015f ),
           ( 10, 0.007f ),
        };

        private static readonly List<ChanceTable<int>> AllegianceRankChances = new List<ChanceTable<int>>()
        {
            T1_AllegianceRankChances,
            T2_AllegianceRankChances,
            T3_AllegianceRankChances,
            T4_AllegianceRankChances,
            T5_AllegianceRankChances,
            T6_AllegianceRankChances,
            T7_AllegianceRankChances,
            T8_AllegianceRankChances
        };

        /// <summary>
        /// Rolls for a allegiance rank requirement for a tier
        /// </summary>
        public static int Roll(int tier)
        {
            return AllegianceRankChances[tier - 1].Roll();
        }
    }
}
