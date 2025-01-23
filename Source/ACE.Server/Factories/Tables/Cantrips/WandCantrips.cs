using System.Collections.Generic;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;
using ACE.Common;

namespace ACE.Server.Factories.Tables
{
    public static class WandCantrips
    {
        // retail appears to have been bugged here for WarMagicAptitude and VoidMagicAptitude
        // orbs: war magic aptitude
        // non-elemental casters: void magic aptitude
        // war elemental casters: void magic aptitude
        // void elemental casters: war magic aptitude

        private static ChanceTable<SpellId> casterCantrips = new ChanceTable<SpellId>()
        {
            ( SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, 0.05f ),
            ( SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,     0.05f ),
            ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           0.05f ),
            ( SpellId.CANTRIPWARMAGICAPTITUDE1,            0.05f ),     // gets mutated properly into VoidMagicAptitude for nether casters

            ( SpellId.CANTRIPDEFENDER1,                    0.05f ),
            ( SpellId.CantripHermeticLink1,                0.05f ),
            ( SpellId.CantripSpiritThirst1,                0.05f ),

            ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.05f ),
            ( SpellId.CANTRIPWILLPOWER1,                   0.05f ),

            ( SpellId.CANTRIPSTRENGTH1,                    0.04f ),
            ( SpellId.CANTRIPENDURANCE1,                   0.04f ),
            ( SpellId.CANTRIPFOCUS1,                       0.04f ),

            ( SpellId.CANTRIPARMOR1,                       0.03f ),
            ( SpellId.CANTRIPACIDWARD1,                    0.01f ),
            ( SpellId.CANTRIPBLUDGEONINGWARD1,             0.01f ),
            ( SpellId.CANTRIPFLAMEWARD1,                   0.01f ),
            ( SpellId.CANTRIPFROSTWARD1,                   0.01f ),
            ( SpellId.CANTRIPPIERCINGWARD1,                0.01f ),
            ( SpellId.CANTRIPSLASHINGWARD1,                0.01f ),
            ( SpellId.CANTRIPSTORMWARD1,                   0.01f ),

            ( SpellId.CANTRIPIMPREGNABILITY1,              0.03f ),
            ( SpellId.CANTRIPINVULNERABILITY1,             0.03f ),
            ( SpellId.CANTRIPMAGICRESISTANCE1,             0.03f ),

            // no alchemy prowess in eor
            ( SpellId.CANTRIPARCANEPROWESS1,               0.03f ),
            ( SpellId.CANTRIPARMOREXPERTISE1,              0.01f ),
            ( SpellId.CANTRIPCOOKINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.01f ),
            ( SpellId.CANTRIPFEALTY1,                      0.01f ),
            // no fletching prowess in eor
            ( SpellId.CANTRIPHEALINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPITEMEXPERTISE1,               0.01f ),
            ( SpellId.CANTRIPJUMPINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPLEADERSHIP1,                  0.01f ),
            ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.01f ),
            ( SpellId.CANTRIPMAGICITEMEXPERTISE1,          0.01f ),
            ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.01f ),
            ( SpellId.CANTRIPPERSONATTUNEMENT1,            0.01f ),
            ( SpellId.CantripSalvaging1,                   0.01f ),
            ( SpellId.CANTRIPSPRINT1,                      0.01f ),
            ( SpellId.CANTRIPWEAPONEXPERTISE1,             0.01f ),

            ( SpellId.CANTRIPCOORDINATION1,                0.01f ),
            ( SpellId.CANTRIPQUICKNESS1,                   0.01f ),

            ( SpellId.CantripSneakAttackProwess1,          0.02f ),
            ( SpellId.CantripSummoningProwess1,            0.02f ),
        };

        static WandCantrips()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.Infiltration)
            {
                casterCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1,    5.0f ),
                    ( SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,        5.0f ),
                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,              5.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,               5.0f ),

                    ( SpellId.CANTRIPDEFENDER1,                       5.0f ),
                    ( SpellId.CantripHermeticLink1,                   5.0f ),

                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,          5.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                      5.0f ),

                    ( SpellId.CANTRIPSTRENGTH1,                       4.0f ),
                    ( SpellId.CANTRIPENDURANCE1,                      4.0f ),
                    ( SpellId.CANTRIPFOCUS1,                          4.0f ),

                    ( SpellId.CANTRIPARMOR1,                          3.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                       1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                      1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                      1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                      1.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,                 3.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,                3.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,                3.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,              1.0f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,                  3.0f ),
                    ( SpellId.CANTRIPARMOREXPERTISE1,                 1.0f ),
                    ( SpellId.CANTRIPCOOKINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPDECEPTIONPROWESS1,               1.0f ),
                    ( SpellId.CANTRIPFEALTY1,                         1.0f ),
                    ( SpellId.CANTRIPFLETCHINGPROWESS1,               1.0f ),
                    ( SpellId.CANTRIPHEALINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPITEMEXPERTISE1,                  1.0f ),
                    ( SpellId.CANTRIPJUMPINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPLEADERSHIP1,                     1.0f ),
                    ( SpellId.CANTRIPLOCKPICKPROWESS1,                1.0f ),
                    ( SpellId.CANTRIPMAGICITEMEXPERTISE1,             1.0f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,              1.0f ),
                    ( SpellId.CANTRIPPERSONATTUNEMENT1,               1.0f ),
                    ( SpellId.CantripSalvaging1,                      1.0f ),
                    ( SpellId.CANTRIPSPRINT1,                         1.0f ),
                    ( SpellId.CANTRIPWEAPONEXPERTISE1,                1.0f ),

                    ( SpellId.CANTRIPCOORDINATION1,                   1.0f ),
                    ( SpellId.CANTRIPQUICKNESS1,                      1.0f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                casterCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,              5.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,               5.0f ),

                    ( SpellId.CANTRIPDEFENDER1,                       5.0f ),
                    ( SpellId.CantripHermeticLink1,                   5.0f ),

                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,          5.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                      5.0f ),

                    ( SpellId.CANTRIPSTRENGTH1,                       4.0f ),
                    ( SpellId.CANTRIPENDURANCE1,                      4.0f ),
                    ( SpellId.CANTRIPFOCUS1,                          4.0f ),

                    ( SpellId.CANTRIPARMOR1,                          3.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                       1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                      1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                      1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                   1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                      1.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,                 3.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,                3.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,                3.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,              1.0f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,                  3.0f ),
                    ( SpellId.CANTRIPCOOKINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPDECEPTIONPROWESS1,               1.0f ),
                    ( SpellId.CANTRIPFEALTY1,                         1.0f ),
                    ( SpellId.CANTRIPFLETCHINGPROWESS1,               1.0f ),
                    ( SpellId.CANTRIPHEALINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPJUMPINGPROWESS1,                 1.0f ),
                    ( SpellId.CANTRIPLEADERSHIP1,                     1.0f ),
                    ( SpellId.CANTRIPLOCKPICKPROWESS1,                1.0f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,              1.0f ),
                    ( SpellId.CantripSalvaging1,                      1.0f ),
                    ( SpellId.CANTRIPSPRINT1,                         1.0f ),

                    ( SpellId.CANTRIPCOORDINATION1,                   1.0f ),
                    ( SpellId.CANTRIPQUICKNESS1,                      1.0f ),
                };
            }
        }

        public static SpellId Roll()
        {
            return casterCantrips.Roll();
        }

        public static List<SpellId> GetSpellIdList()
        {
            var spellIds = new List<SpellId>();
            foreach (var entry in casterCantrips)
            {
                spellIds.Add(entry.result);
            }
            return spellIds;
        }
    }
}
