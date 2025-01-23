using System.Collections.Generic;

using log4net;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables
{
    public static class ClothArmorSpells
    {
        private static readonly List<(SpellId spellId, float chance)> clothArmorSpells = new List<(SpellId, float)>() { };

        static ClothArmorSpells()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                clothArmorSpells = new List<(SpellId, float)>()
                {
                    ( SpellId.PiercingBane1,    0.3f ),
                    ( SpellId.FlameBane1,       0.3f ),
                    ( SpellId.FrostBane1,       0.3f ),
                    //( SpellId.Impenetrability1, 1.00f ),
                    ( SpellId.AcidBane1,        0.3f ),
                    ( SpellId.BladeBane1,       0.3f ),
                    ( SpellId.LightningBane1,   0.3f ),
                    ( SpellId.BludgeonBane1,    0.3f ),
                };
            }
        }

        public static List<SpellId> Roll(TreasureDeath treasureDeath)
        {
            var spells = new List<SpellId>();

            foreach (var spell in clothArmorSpells)
            {
                var rng = ThreadSafeRandom.NextInterval(treasureDeath.LootQualityMod);

                if (rng < spell.chance)
                    spells.Add(spell.spellId);
            }
            return spells;
        }
    }
}
