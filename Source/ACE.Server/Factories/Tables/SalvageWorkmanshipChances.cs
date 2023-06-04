using System.Collections.Generic;

using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SalvageWorkmanshipChances
    {
        private static ChanceTable<float> T1_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.00f, 1.00f ),
            ( 4.00f, 0.30f ),
        };

        private static ChanceTable<float> T2_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.00f, 0.30f ),
            ( 4.00f, 1.00f ),
        };

        private static ChanceTable<float> T3_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.00f, 0.15f ),
            ( 4.00f, 1.00f ),
            ( 8.00f, 0.15f ),
        };

        private static ChanceTable<float> T4_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.00f, 0.15f ),
            ( 4.00f, 0.75f ),
            ( 8.00f, 0.40f ),
        };

        private static ChanceTable<float> T5_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 4.00f, 0.80f ),
            ( 8.00f, 0.50f ),
        };

        private static ChanceTable<float> T6_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 4.00f, 1.00f ),
            ( 8.00f, 1.00f ),
        };

        private static ChanceTable<float> T7_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 4.00f, 0.50f ),
            ( 8.00f, 1.00f ),
        };

        private static ChanceTable<float> T8_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 8.00f, 1.00f ),
        };

        private static readonly List<ChanceTable<float>> WorkmanshipTiers = new List<ChanceTable<float>>()
        {
            T1_WorkmanshipChances,
            T2_WorkmanshipChances,
            T3_WorkmanshipChances,
            T4_WorkmanshipChances,
            T5_WorkmanshipChances,
            T6_WorkmanshipChances,
            T7_WorkmanshipChances,
            T8_WorkmanshipChances
        };
        public static float Roll(int tier)
        {
            return WorkmanshipTiers[tier - 1].Roll();
        }
    }
}
