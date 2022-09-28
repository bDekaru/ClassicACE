using System;
using System.Collections.Generic;
using System.Linq;
using log4net;

using ACE.Common;

namespace ACE.Server.Factories.Entity
{
    public enum ChanceTableType
    {
        Chance,
        Weight
    }

    public class ChanceTable<T> : List<(T result, float chance)>
    {
        private bool verified;
        private ChanceTableType TableType;
        private float TotalWeight = 1.0f;
        private static readonly decimal threshold = 0.0000001M;

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ChanceTable(ChanceTableType tableType = ChanceTableType.Chance)
        {
            TableType = tableType;
        }
        private static int CompareByInverseChance((T result, float chance) x, (T result, float chance) y)
        {
            return -x.chance.CompareTo(y.chance);
        }

        private void VerifyTable()
        {
            Sort(CompareByInverseChance); // Sort this list to make sure the smallest chances are at the end of the list so qualityMod can work properly.

            if (TableType == ChanceTableType.Weight)
            {
                TotalWeight = 0.0f;
                foreach (var entry in this)
                {
                    TotalWeight += entry.chance;
                }
            }
            else
            {
                var total = 0.0M;

                foreach (var entry in this)
                    total += (decimal)(entry.chance);

                if (Math.Abs(1.0M - total) > threshold)
                    log.Error($"Chance table adds up to {total}, expected 1.0: {string.Join(", ", this)}");
            }

            verified = true;
        }

        public T PseudoRandomRoll(int seed)
        {
            if (!verified)
                VerifyTable();

            var total = 0.0f;

            Random random = new Random(seed);
            var rng = random.NextDouble();

            foreach (var entry in this)
            {
                total += entry.chance / TotalWeight;

                if (rng < total)
                    return entry.result;
            }

            //Console.WriteLine($"Rolled {rng}, everything >= {total}");

            return this.Last(i => i.chance > 0).result;
        }

        public T Roll(float qualityMod = 0.0f, bool invertedQualityMod = false)
        {
            if (!verified)
                VerifyTable();

            var total = 0.0f;

            double rng;
            if (invertedQualityMod)
            {
                if (qualityMod >= 0)
                    rng = ThreadSafeRandom.Next(0.0f, 1.0f - qualityMod);
                else
                    rng = ThreadSafeRandom.Next(-qualityMod, 1.0f);
            }
            else
            {
                if (qualityMod >= 0)
                    rng = ThreadSafeRandom.Next(qualityMod, 1.0f);
                else
                    rng = ThreadSafeRandom.Next(0.0f, Math.Max(1.0f + qualityMod, 0.0f));
            }

            foreach (var entry in this)
            {
                total += entry.chance / TotalWeight;

                if (rng < total)
                    return entry.result;
            }

            //Console.WriteLine($"Rolled {rng}, everything >= {total}");

            return this.Last(i => i.chance > 0).result;
        }
    }
}
