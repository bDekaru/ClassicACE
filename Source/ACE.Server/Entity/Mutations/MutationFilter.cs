using System;
using System.Collections.Generic;

using ACE.Common;
using ACE.Server.WorldObjects;

namespace ACE.Server.Entity.Mutations
{
    public class MutationFilter
    {
        public List<Mutation> Mutations = new List<Mutation>();

        // MutationFilter -> Mutation -> MutationOutcome -> EffectList -> Effect
        
        public bool TryMutate(WorldObject wo, int tier = 1, float qualityMod = 0.0f)
        {
            double rng;
            if(qualityMod >= 0)
                rng = ThreadSafeRandom.Next(qualityMod, 1.0f);
            else
                rng = ThreadSafeRandom.Next(0.0f, Math.Max(1.0f + qualityMod, 0.0f));

            var mutated = false;

            foreach (var mutation in Mutations)
                mutated |= mutation.TryMutate(wo, tier, rng, qualityMod);

            return mutated;
        }
    }
}
