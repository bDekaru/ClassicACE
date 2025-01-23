using System.Collections.Generic;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class ClothArmorCantrips
    {
        private static ChanceTable<SpellId> clothArmorCantrips = new ChanceTable<SpellId>() { };

        static ClothArmorCantrips()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                clothArmorCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,          6.0f ),
                    ( SpellId.CANTRIPMANAGAIN1,                       6.0f ),

                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,              4.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,               4.0f ),

                    ( SpellId.CANTRIPFOCUS1,                          2.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                      2.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,                 1.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,                1.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,                1.0f ),

                    ( SpellId.CANTRIPIMPENETRABILITY1,                1.0f ),
                    ( SpellId.CANTRIPACIDBANE1,                       1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPFLAMEBANE1,                      1.0f ),
                    ( SpellId.CANTRIPFROSTBANE1,                      1.0f ),
                    ( SpellId.CANTRIPPIERCINGBANE1,                   1.0f ),
                    ( SpellId.CANTRIPSLASHINGBANE1,                   1.0f ),
                    ( SpellId.CANTRIPSTORMBANE1,                      1.0f ),

                    ( SpellId.CANTRIPARMOR1,                          1.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                       1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                      1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                      1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                      1.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,              0.5f ),
                    ( SpellId.CantripAwarenessAptitude1,              0.5f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,              0.5f ),
                };
            }
        }

        public static SpellId Roll()
        {
            return clothArmorCantrips.Roll();
        }

        public static List<SpellId> GetSpellIdList()
        {
            var spellIds = new List<SpellId>();
            foreach (var entry in clothArmorCantrips)
            {
                spellIds.Add(entry.result);
            }
            return spellIds;
        }
    }
}
