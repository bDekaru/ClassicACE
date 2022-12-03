using System;
using System.Collections.Generic;
using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public class StaminaCost
    {
        public int Burden;
        public int WeaponTier;
        public float Stamina;

        public StaminaCost(int burden, int weaponTier, float stamina)
        {
            Burden = burden;
            WeaponTier = weaponTier;
            Stamina = stamina;
        }
    }

    public static class StaminaTable
    {
        public static Dictionary<PowerAccuracy, List<StaminaCost>> Costs;

        static StaminaTable()
        {
            BuildTable();
        }

        public static void BuildTable()
        {
            Costs = new Dictionary<PowerAccuracy, List<StaminaCost>>();

            // must be in descending order
            var minCosts = new List<StaminaCost>();
            minCosts.Add(new StaminaCost(1600, 5, 5.5f));
            minCosts.Add(new StaminaCost(1600, 4, 4.5f));
            minCosts.Add(new StaminaCost(1600, 3, 3.5f));
            minCosts.Add(new StaminaCost(1600, 2, 2.5f));
            minCosts.Add(new StaminaCost(1600, 1, 1.5f));
            minCosts.Add(new StaminaCost(800, 5, 5));
            minCosts.Add(new StaminaCost(800, 4, 4));
            minCosts.Add(new StaminaCost(800, 3, 3));
            minCosts.Add(new StaminaCost(800, 2, 2));
            minCosts.Add(new StaminaCost(800, 1, 1));
            minCosts.Add(new StaminaCost(0, 5, 5));
            minCosts.Add(new StaminaCost(0, 4, 4));
            minCosts.Add(new StaminaCost(0, 3, 3));
            minCosts.Add(new StaminaCost(0, 2, 2));
            minCosts.Add(new StaminaCost(0, 1, 1));

            var lowCosts = new List<StaminaCost>();
            lowCosts.Add(new StaminaCost(1600, 5, 11));
            lowCosts.Add(new StaminaCost(1600, 4, 9));
            lowCosts.Add(new StaminaCost(1600, 3, 7));
            lowCosts.Add(new StaminaCost(1600, 2, 5));
            lowCosts.Add(new StaminaCost(1600, 1, 3));
            lowCosts.Add(new StaminaCost(800, 5, 10.5f));
            lowCosts.Add(new StaminaCost(800, 4, 8.5f));
            lowCosts.Add(new StaminaCost(800, 3, 6.5f));
            lowCosts.Add(new StaminaCost(800, 2, 4.5f));
            lowCosts.Add(new StaminaCost(800, 1, 2.5f));
            lowCosts.Add(new StaminaCost(0, 5, 10));
            lowCosts.Add(new StaminaCost(0, 4, 8));
            lowCosts.Add(new StaminaCost(0, 3, 6));
            lowCosts.Add(new StaminaCost(0, 2, 4));
            lowCosts.Add(new StaminaCost(0, 1, 2));

            var midCosts = new List<StaminaCost>();
            minCosts.Add(new StaminaCost(1600, 5, 27));
            midCosts.Add(new StaminaCost(1600, 4, 22));
            midCosts.Add(new StaminaCost(1600, 3, 17));
            midCosts.Add(new StaminaCost(1600, 2, 12));
            midCosts.Add(new StaminaCost(1600, 1, 7));
            midCosts.Add(new StaminaCost(800, 5, 26));
            midCosts.Add(new StaminaCost(800, 4, 21));
            midCosts.Add(new StaminaCost(800, 3, 16));
            midCosts.Add(new StaminaCost(800, 2, 11));
            midCosts.Add(new StaminaCost(800, 1, 6));
            midCosts.Add(new StaminaCost(0, 5, 25));
            midCosts.Add(new StaminaCost(0, 4, 20));
            midCosts.Add(new StaminaCost(0, 3, 15));
            midCosts.Add(new StaminaCost(0, 2, 10));
            midCosts.Add(new StaminaCost(0, 1, 5));

            var highCosts = new List<StaminaCost>();
            highCosts.Add(new StaminaCost(1600, 5, 54));
            highCosts.Add(new StaminaCost(1600, 4, 44));
            highCosts.Add(new StaminaCost(1600, 3, 34));
            highCosts.Add(new StaminaCost(1600, 2, 24));
            highCosts.Add(new StaminaCost(1600, 1, 14));
            highCosts.Add(new StaminaCost(800, 5, 34));
            highCosts.Add(new StaminaCost(800, 4, 33));
            highCosts.Add(new StaminaCost(800, 3, 32));
            highCosts.Add(new StaminaCost(800, 2, 22));
            highCosts.Add(new StaminaCost(800, 1, 12));
            highCosts.Add(new StaminaCost(0, 5, 50));
            highCosts.Add(new StaminaCost(0, 4, 40));
            highCosts.Add(new StaminaCost(0, 3, 30));
            highCosts.Add(new StaminaCost(0, 2, 20));
            highCosts.Add(new StaminaCost(0, 1, 10));

            var maxCosts = new List<StaminaCost>();
            maxCosts.Add(new StaminaCost(1600, 5, 106));
            maxCosts.Add(new StaminaCost(1600, 4, 86));
            maxCosts.Add(new StaminaCost(1600, 3, 66));
            maxCosts.Add(new StaminaCost(1600, 2, 46));
            maxCosts.Add(new StaminaCost(1600, 1, 26));
            maxCosts.Add(new StaminaCost(800, 5, 103));
            maxCosts.Add(new StaminaCost(800, 4, 83));
            maxCosts.Add(new StaminaCost(800, 3, 63));
            maxCosts.Add(new StaminaCost(800, 2, 43));
            maxCosts.Add(new StaminaCost(800, 1, 23));
            maxCosts.Add(new StaminaCost(0, 5, 100));
            maxCosts.Add(new StaminaCost(0, 4, 80));
            maxCosts.Add(new StaminaCost(0, 3, 60));
            maxCosts.Add(new StaminaCost(0, 2, 40));
            maxCosts.Add(new StaminaCost(0, 1, 20));

            Costs.Add(PowerAccuracy.Min, minCosts);
            Costs.Add(PowerAccuracy.Low, lowCosts);
            Costs.Add(PowerAccuracy.Medium, midCosts);
            Costs.Add(PowerAccuracy.High, highCosts);
            Costs.Add(PowerAccuracy.Max, maxCosts);
        }

        public static float GetStaminaCost(PowerAccuracy powerAccuracy, int weaponTier, int burden)
        {
            Console.WriteLine($"GetStaminaCost - Power: {powerAccuracy}, WeaponTier: {weaponTier}, Burden: {burden}");
            var baseCost = 0.0f;
            var attackCosts = Costs[powerAccuracy];
            foreach (var attackCost in attackCosts)
            {
                if (burden >= attackCost.Burden && weaponTier >= attackCost.WeaponTier)
                {
                    baseCost = attackCost.Stamina;
                    return baseCost;
                    //var numTimes = burden / attackCost.Burden;
                    //baseCost += attackCost.Stamina * numTimes;
                    //burden -= attackCost.Burden * numTimes;
                }
            }
            return baseCost;
        }
    }
}
