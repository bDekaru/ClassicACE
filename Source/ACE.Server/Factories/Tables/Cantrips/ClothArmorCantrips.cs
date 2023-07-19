using System.Collections.Generic;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class ClothArmorCantrips
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly List<SpellId> spells = new List<SpellId>()
        {
        };

        private static readonly int NumLevels = 4;

        // original api
        public static readonly SpellId[][] Table = new SpellId[spells.Count][];

        static ClothArmorCantrips()
        {
            // takes ~0.3ms
            BuildSpells();

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                clothArmorCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                clothArmorCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPARMOR1,                          3.0f ),

                    ( SpellId.CANTRIPFOCUS1,                          2.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                      2.0f ),

                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,          2.0f ),
                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,              2.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,               2.0f ),

                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,              1.0f ),
                    ( SpellId.CANTRIPIMPREGNABILITY1,                 1.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,                1.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,                1.0f ),

                    ( SpellId.CANTRIPACIDWARD1,                       1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                      1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                      1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                      1.0f ),

                    ( SpellId.CantripAwarenessAptitude1,              1.0f ),
                    ( SpellId.CantripAppraiseAptitude1,               1.0f ),
                    ( SpellId.CantripSneakingAptitude1,               1.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,                  0.5f ),
                };
            }
        }

        private static void BuildSpells()
        {
            for (var i = 0; i < spells.Count; i++)
                Table[i] = new SpellId[NumLevels];

            for (var i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];

                var spellLevels = SpellLevelProgression.GetSpellLevels(spell);

                if (spellLevels == null)
                {
                    log.Error($"ClothArmorCantrips - couldn't find {spell}");
                    continue;
                }

                if (spellLevels.Count != NumLevels)
                {
                    log.Error($"ClothArmorCantrips - expected {NumLevels} levels for {spell}, found {spellLevels.Count}");
                    continue;
                }

                for (var j = 0; j < NumLevels; j++)
                    Table[i][j] = spellLevels[j];
            }
        }

        private static ChanceTable<SpellId> clothArmorCantrips = new ChanceTable<SpellId>()
        {
        };

        public static SpellId Roll()
        {
            return clothArmorCantrips.Roll();
        }
    }
}
