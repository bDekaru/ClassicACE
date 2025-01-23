using System.Collections.Generic;

using log4net;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables
{
    public static class ArmorSpells
    {
        // this table also applies to clothing w/ AL

        private static readonly List<(SpellId spellId, float chance)> armorSpells = new List<(SpellId, float)>()
        {
            ( SpellId.PiercingBane1,    0.15f ),
            ( SpellId.FlameBane1,       0.15f ),
            ( SpellId.FrostBane1,       0.15f ),
            ( SpellId.Impenetrability1, 1.00f ),
            ( SpellId.AcidBane1,        0.15f ),
            ( SpellId.BladeBane1,       0.15f ),
            ( SpellId.LightningBane1,   0.15f ),
            ( SpellId.BludgeonBane1,    0.15f ),
        };

        private static readonly List<(SpellId spellId, float chance)> shieldSpells = new List<(SpellId, float)>()
        {
            ( SpellId.PiercingBane1,    0.15f ),
            ( SpellId.FlameBane1,       0.15f ),
            ( SpellId.FrostBane1,       0.15f ),
            ( SpellId.Impenetrability1, 1.00f ),
            ( SpellId.AcidBane1,        0.15f ),
            ( SpellId.BladeBane1,       0.15f ),
            ( SpellId.LightningBane1,   0.15f ),
            ( SpellId.BludgeonBane1,    0.15f ),
        };

        static ArmorSpells()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                armorSpells = new List<(SpellId, float)>()
                {
                    ( SpellId.PiercingBane1,    0.15f ),
                    ( SpellId.FlameBane1,       0.15f ),
                    ( SpellId.FrostBane1,       0.15f ),
                    //( SpellId.Impenetrability1, 1.00f ),
                    ( SpellId.AcidBane1,        0.15f ),
                    ( SpellId.BladeBane1,       0.15f ),
                    ( SpellId.LightningBane1,   0.15f ),
                    ( SpellId.BludgeonBane1,    0.15f ),
                };

                shieldSpells = new List<(SpellId, float)>()
                {
                    ( SpellId.HeartBlocker1,    0.15f ),
                    ( SpellId.PiercingBane1,    0.15f ),
                    ( SpellId.FlameBane1,       0.15f ),
                    ( SpellId.FrostBane1,       0.15f ),
                    //( SpellId.Impenetrability1, 1.00f ),
                    ( SpellId.AcidBane1,        0.15f ),
                    ( SpellId.BladeBane1,       0.15f ),
                    ( SpellId.LightningBane1,   0.15f ),
                    ( SpellId.BludgeonBane1,    0.15f ),
                };

            }
        }

        public static List<SpellId> Roll(TreasureDeath treasureDeath, bool isShield)
        {
            // this roll also applies to clothing w/ AL!
            // ie., shirts and pants would never have item spells on them,
            // but cloth gloves would

            // thanks to Sapphire Knight and Butterflygolem for helping to figure this part out!

            var spells = new List<SpellId>();

            var possibleSpells = isShield ? shieldSpells : armorSpells;

            foreach (var spell in possibleSpells)
            {
                var rng = ThreadSafeRandom.NextInterval(treasureDeath.LootQualityMod);

                if (rng < spell.chance)
                    spells.Add(spell.spellId);
            }
            return spells;
        }
    }
}
