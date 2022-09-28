using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity.Mutations
{
    public class Mutation
    {
        public List<float> Chances = new List<float>();

        public List<MutationOutcome> Outcomes = new List<MutationOutcome>();

        public bool TryMutate(WorldObject wo, int tier, double rng, float qualityMod = 0.0f)
        {
            // if at least 6 tiers are defined,
            // if we are rolling for a higher tier,
            // fall back on highest tier?
            if (Chances.Count >= 6 && tier > Chances.Count)
                tier = Chances.Count;

            if (tier < 1 || tier > Chances.Count)
                return false;

            // does it pass the roll to mutate for the tier?
            if (rng >= Chances[tier - 1])
                return false;

            // roll again to select the mutations
            if (qualityMod >= 0)
                rng = ThreadSafeRandom.Next(qualityMod, 1.0f);
            else
                rng = ThreadSafeRandom.Next(0.0f, Math.Max(1.0f + qualityMod, 0.0f));

            var mutated = false;
            foreach (var outcome in Outcomes)
                mutated |= outcome.TryMutate(wo, rng);

            return mutated;
        }
    }
}
