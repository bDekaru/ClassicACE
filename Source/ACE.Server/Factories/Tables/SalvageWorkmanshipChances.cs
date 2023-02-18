using System.Collections.Generic;

using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SalvageWorkmanshipChances
    {
        private static ChanceTable<float> T1_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.00f, 1.00f ),
            ( 1.25f, 0.50f ),
        };

        private static ChanceTable<float> T2_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.25f, 1.00f ),
            ( 1.50f, 0.50f ),
        };

        private static ChanceTable<float> T3_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.50f, 1.00f ),
            ( 1.75f, 0.50f ),
        };

        private static ChanceTable<float> T4_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 1.75f, 1.00f ),
            ( 2.00f, 0.50f ),
        };

        private static ChanceTable<float> T5_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 2.00f, 1.00f ),
            ( 2.50f, 0.50f ),
        };

        private static ChanceTable<float> T6_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 2.50f, 1.00f ),
            ( 3.00f, 0.50f ),
        };

        private static ChanceTable<float> T7_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 3.00f, 1.00f ),
            ( 4.00f, 0.50f ),
        };

        private static ChanceTable<float> T8_WorkmanshipChances = new ChanceTable<float>(ChanceTableType.Weight)
        {
            ( 5.00f, 1.00f ),
            ( 6.00f, 0.75f ),
            ( 7.00f, 0.75f ),
            ( 8.00f, 0.50f ),
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
