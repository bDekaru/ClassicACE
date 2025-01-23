using System.Collections.Generic;

using log4net;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables
{
    public static class WandSpells
    {
        private static readonly List<(SpellId spellId, float chance)> wandSpells = new List<(SpellId, float)>()
        {
            ( SpellId.DefenderSelf1,      0.25f ),
            ( SpellId.HermeticLinkSelf1,  1.0f ),
            ( SpellId.SpiritDrinkerSelf1, 0.25f ),      // retail appears to have had a flat 25% chance for Spirit Drinker for all casters,
                                                        // regardless if they had a DamageType
        };

        static WandSpells()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Ruleset.Infiltration)
            {
                wandSpells = new List<(SpellId, float)>()
                {
                    ( SpellId.DefenderSelf1,      0.25f ),
                    ( SpellId.HermeticLinkSelf1,  0.50f ),
                };
            }
        }

        public static List<SpellId> Roll(WorldObject wo, TreasureDeath treasureDeath)
        {
            var spells = new List<SpellId>();

            foreach (var spell in wandSpells)
            {
                // retail didn't have this logic, but...
                if (spell.spellId == SpellId.SpiritDrinkerSelf1 && wo.W_DamageType == DamageType.Undef)
                    continue;

                var rng = ThreadSafeRandom.NextInterval(treasureDeath.LootQualityMod);

                if (rng < spell.chance)
                    spells.Add(spell.spellId);
            }
            return spells;
        }
    }
}
