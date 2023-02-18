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
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly List<SpellId> spells = new List<SpellId>()
        {
            SpellId.ArmorOther1,
            SpellId.AcidProtectionOther1,
            SpellId.BludgeonProtectionOther1,
            SpellId.ColdProtectionOther1,
            SpellId.LightningProtectionOther1,
            SpellId.FireProtectionOther1,
            SpellId.BladeProtectionOther1,
            SpellId.PiercingProtectionOther1,

            SpellId.WillpowerOther1,
            SpellId.FocusOther1,

            SpellId.ManaMasteryOther1,
            SpellId.RejuvenationOther1,
            SpellId.RegenerationOther1,
            SpellId.ManaRenewalOther1,
            SpellId.MagicResistanceOther1,
            SpellId.ImpregnabilityOther1,
            SpellId.InvulnerabilityOther1,
            SpellId.MagicResistanceOther1,
            SpellId.MonsterAttunementOther1,
            SpellId.WarMagicMasteryOther1,
            SpellId.LifeMagicMasteryOther1,
            SpellId.AlchemyMasteryOther1,
        };

        private static readonly int NumTiers = 8;

        // original api
        public static readonly SpellId[][] Table = new SpellId[spells.Count][];

        static ClothArmorSpells()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                clothArmorSpells = new List<(SpellId, float)>()
                {
                    ( SpellId.ArmorOther1,                  1.00f ),
                    ( SpellId.AcidProtectionOther1,         0.15f ),
                    ( SpellId.BludgeonProtectionOther1,     0.15f ),
                    ( SpellId.ColdProtectionOther1,         0.15f ),
                    ( SpellId.LightningProtectionOther1,    0.15f ),
                    ( SpellId.FireProtectionOther1,         0.15f ),
                    ( SpellId.BladeProtectionOther1,        0.15f ),
                    ( SpellId.PiercingProtectionOther1,     0.15f ),
                };
            }

            // takes ~0.3ms
            BuildSpells();
        }

        private static void BuildSpells()
        {
            for (var i = 0; i < spells.Count; i++)
                Table[i] = new SpellId[NumTiers];

            for (var i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];

                var spellLevels = SpellLevelProgression.GetSpellLevels(spell);

                if (spellLevels == null)
                {
                    log.Error($"ClothArmorSpells - couldn't find {spell}");
                    continue;
                }

                if (spellLevels.Count != NumTiers)
                {
                    log.Error($"ClothArmorSpells - expected {NumTiers} levels for {spell}, found {spellLevels.Count}");
                    continue;
                }

                for (var j = 0; j < NumTiers; j++)
                    Table[i][j] = spellLevels[j];
            }
        }

        // alt

        // this table also applies to clothing w/ AL

        private static readonly List<(SpellId spellId, float chance)> clothArmorSpells = new List<(SpellId, float)>()
        {
        };

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
