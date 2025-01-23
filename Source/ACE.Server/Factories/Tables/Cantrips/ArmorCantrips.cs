using System.Collections.Generic;

using log4net;

using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class ArmorCantrips
    {
        private static ChanceTable<SpellId> armorCantrips = new ChanceTable<SpellId>()
        {
            ( SpellId.CANTRIPSTRENGTH1,                    0.02f ),
            ( SpellId.CANTRIPENDURANCE1,                   0.02f ),
            ( SpellId.CANTRIPCOORDINATION1,                0.02f ),
            ( SpellId.CANTRIPQUICKNESS1,                   0.02f ),
            ( SpellId.CANTRIPFOCUS1,                       0.02f ),
            ( SpellId.CANTRIPWILLPOWER1,                   0.02f ),

            ( SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1,        0.02f ),
            ( SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1,        0.02f ),
            ( SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,      0.02f ),
            ( SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,      0.02f ),
            ( SpellId.CANTRIPTWOHANDEDAPTITUDE1,           0.02f ),

            ( SpellId.CANTRIPIMPREGNABILITY1,              0.02f ),
            ( SpellId.CANTRIPINVULNERABILITY1,             0.02f ),
            ( SpellId.CANTRIPMAGICRESISTANCE1,             0.02f ),

            ( SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, 0.02f ),
            ( SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,     0.02f ),
            ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           0.02f ),
            ( SpellId.CANTRIPWARMAGICAPTITUDE1,            0.02f ),
            ( SpellId.CantripVoidMagicAptitude1,           0.02f ),

            ( SpellId.CANTRIPIMPENETRABILITY1,             0.02f ),
            ( SpellId.CANTRIPACIDBANE1,                    0.02f ),
            ( SpellId.CANTRIPBLUDGEONINGBANE1,             0.02f ),
            ( SpellId.CANTRIPFLAMEBANE1,                   0.02f ),
            ( SpellId.CANTRIPFROSTBANE1,                   0.02f ),
            ( SpellId.CANTRIPPIERCINGBANE1,                0.02f ),
            ( SpellId.CANTRIPSLASHINGBANE1,                0.02f ),
            ( SpellId.CANTRIPSTORMBANE1,                   0.02f ),

            ( SpellId.CANTRIPARMOR1,                       0.02f ),
            ( SpellId.CANTRIPACIDWARD1,                    0.02f ),
            ( SpellId.CANTRIPBLUDGEONINGWARD1,             0.02f ),
            ( SpellId.CANTRIPFLAMEWARD1,                   0.02f ),
            ( SpellId.CANTRIPFROSTWARD1,                   0.02f ),
            ( SpellId.CANTRIPPIERCINGWARD1,                0.02f ),
            ( SpellId.CANTRIPSLASHINGWARD1,                0.02f ),
            ( SpellId.CANTRIPSTORMWARD1,                   0.02f ),

            ( SpellId.CANTRIPALCHEMICALPROWESS1,           0.01f ),
            ( SpellId.CANTRIPARCANEPROWESS1,               0.01f ),
            ( SpellId.CANTRIPARMOREXPERTISE1,              0.01f ),
            ( SpellId.CANTRIPCOOKINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.01f ),
            ( SpellId.CANTRIPFEALTY1,                      0.01f ),
            ( SpellId.CANTRIPFLETCHINGPROWESS1,            0.01f ),
            ( SpellId.CANTRIPHEALINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPITEMEXPERTISE1,               0.01f ),
            ( SpellId.CANTRIPJUMPINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPLEADERSHIP1,                  0.01f ),
            ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.01f ),
            ( SpellId.CANTRIPMAGICITEMEXPERTISE1,          0.01f ),
            ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.01f ),
            ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.005f ),
            ( SpellId.CANTRIPPERSONATTUNEMENT1,            0.005f ),
            ( SpellId.CantripSalvaging1,                   0.01f ),
            ( SpellId.CANTRIPSPRINT1,                      0.01f ),
            ( SpellId.CANTRIPWEAPONEXPERTISE1,             0.01f ),

            ( SpellId.CantripDirtyFightingProwess1,        0.02f ),
            ( SpellId.CantripDualWieldAptitude1,           0.02f ),
            ( SpellId.CantripRecklessnessProwess1,         0.02f ),
            ( SpellId.CantripShieldAptitude1,              0.02f ),
            ( SpellId.CantripSneakAttackProwess1,          0.02f ),
            ( SpellId.CantripSummoningProwess1,            0.02f ),
        };

        private static ChanceTable<SpellId> shieldCantrips = new ChanceTable<SpellId>()
        {
            ( SpellId.CANTRIPSTRENGTH1,                    0.02f ),
            ( SpellId.CANTRIPENDURANCE1,                   0.02f ),
            ( SpellId.CANTRIPCOORDINATION1,                0.02f ),
            ( SpellId.CANTRIPQUICKNESS1,                   0.02f ),
            ( SpellId.CANTRIPFOCUS1,                       0.02f ),
            ( SpellId.CANTRIPWILLPOWER1,                   0.02f ),

            ( SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1,        0.02f ),
            ( SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1,        0.02f ),
            ( SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,      0.02f ),
            ( SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,      0.02f ),
            ( SpellId.CANTRIPTWOHANDEDAPTITUDE1,           0.02f ),

            ( SpellId.CANTRIPIMPREGNABILITY1,              0.02f ),
            ( SpellId.CANTRIPINVULNERABILITY1,             0.02f ),
            ( SpellId.CANTRIPMAGICRESISTANCE1,             0.02f ),

            ( SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, 0.02f ),
            ( SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,     0.02f ),
            ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           0.02f ),
            ( SpellId.CANTRIPWARMAGICAPTITUDE1,            0.02f ),
            ( SpellId.CantripVoidMagicAptitude1,           0.02f ),

            ( SpellId.CANTRIPIMPENETRABILITY1,             0.02f ),
            ( SpellId.CANTRIPACIDBANE1,                    0.02f ),
            ( SpellId.CANTRIPBLUDGEONINGBANE1,             0.02f ),
            ( SpellId.CANTRIPFLAMEBANE1,                   0.02f ),
            ( SpellId.CANTRIPFROSTBANE1,                   0.02f ),
            ( SpellId.CANTRIPPIERCINGBANE1,                0.02f ),
            ( SpellId.CANTRIPSLASHINGBANE1,                0.02f ),
            ( SpellId.CANTRIPSTORMBANE1,                   0.02f ),

            ( SpellId.CANTRIPARMOR1,                       0.02f ),
            ( SpellId.CANTRIPACIDWARD1,                    0.02f ),
            ( SpellId.CANTRIPBLUDGEONINGWARD1,             0.02f ),
            ( SpellId.CANTRIPFLAMEWARD1,                   0.02f ),
            ( SpellId.CANTRIPFROSTWARD1,                   0.02f ),
            ( SpellId.CANTRIPPIERCINGWARD1,                0.02f ),
            ( SpellId.CANTRIPSLASHINGWARD1,                0.02f ),
            ( SpellId.CANTRIPSTORMWARD1,                   0.02f ),

            ( SpellId.CANTRIPALCHEMICALPROWESS1,           0.01f ),
            ( SpellId.CANTRIPARCANEPROWESS1,               0.01f ),
            ( SpellId.CANTRIPARMOREXPERTISE1,              0.01f ),
            ( SpellId.CANTRIPCOOKINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.01f ),
            ( SpellId.CANTRIPFEALTY1,                      0.01f ),
            ( SpellId.CANTRIPFLETCHINGPROWESS1,            0.01f ),
            ( SpellId.CANTRIPHEALINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPITEMEXPERTISE1,               0.01f ),
            ( SpellId.CANTRIPJUMPINGPROWESS1,              0.01f ),
            ( SpellId.CANTRIPLEADERSHIP1,                  0.01f ),
            ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.01f ),
            ( SpellId.CANTRIPMAGICITEMEXPERTISE1,          0.01f ),
            ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.01f ),
            ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.005f ),
            ( SpellId.CANTRIPPERSONATTUNEMENT1,            0.005f ),
            ( SpellId.CantripSalvaging1,                   0.01f ),
            ( SpellId.CANTRIPSPRINT1,                      0.01f ),
            ( SpellId.CANTRIPWEAPONEXPERTISE1,             0.01f ),

            ( SpellId.CantripDirtyFightingProwess1,        0.02f ),
            ( SpellId.CantripDualWieldAptitude1,           0.02f ),
            ( SpellId.CantripRecklessnessProwess1,         0.02f ),
            ( SpellId.CantripShieldAptitude1,              0.02f ),
            ( SpellId.CantripSneakAttackProwess1,          0.02f ),
            ( SpellId.CantripSummoningProwess1,            0.02f ),
        };

        static ArmorCantrips()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                armorCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPSTRENGTH1,                    1.0f ),
                    ( SpellId.CANTRIPENDURANCE1,                   1.0f ),
                    ( SpellId.CANTRIPCOORDINATION1,                1.0f ),
                    ( SpellId.CANTRIPQUICKNESS1,                   1.0f ),
                    ( SpellId.CANTRIPFOCUS1,                       1.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                   1.0f ),

                    ( SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1,        1.0f ), // CANTRIPAXEAPTITUDE1
                    ( SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPDAGGERAPTITUDE1
                    ( SpellId.CANTRIPMACEAPTITUDE1,                1.0f ),
                    ( SpellId.CANTRIPSPEARAPTITUDE1,               1.0f ),
                    ( SpellId.CANTRIPSTAFFAPTITUDE1,               1.0f ),
                    ( SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1,        1.0f ), // CANTRIPSWORDAPTITUDE1
                    ( SpellId.CANTRIPUNARMEDAPTITUDE1,             1.0f ),
                    ( SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPBOWAPTITUDE1
                    ( SpellId.CANTRIPCROSSBOWAPTITUDE1,            1.0f ),
                    ( SpellId.CANTRIPTHROWNAPTITUDE1,              1.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,              1.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,             1.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,             1.0f ),

                    ( SpellId.CANTRIPCREATUREENCHANTMENTAPTITUDE1, 1.0f ),
                    ( SpellId.CANTRIPITEMENCHANTMENTAPTITUDE1,     1.0f ),
                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           1.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,            1.0f ),

                    ( SpellId.CANTRIPIMPENETRABILITY1,             1.0f ),
                    ( SpellId.CANTRIPACIDBANE1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGBANE1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEBANE1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTBANE1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSTORMBANE1,                   1.0f ),

                    ( SpellId.CANTRIPARMOR1,                       1.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                   1.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,           0.5f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,               0.5f ),
                    ( SpellId.CANTRIPARMOREXPERTISE1,              0.5f ),
                    ( SpellId.CANTRIPCOOKINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPFEALTY1,                      0.5f ),
                    ( SpellId.CANTRIPFLETCHINGPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPHEALINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPITEMEXPERTISE1,               0.5f ),
                    ( SpellId.CANTRIPJUMPINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPLEADERSHIP1,                  0.5f ),
                    ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.5f ),
                    ( SpellId.CANTRIPMAGICITEMEXPERTISE1,          0.5f ),
                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.5f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.25f ),
                    ( SpellId.CANTRIPPERSONATTUNEMENT1,            0.25f ),
                    ( SpellId.CantripSalvaging1,                   0.5f ),
                    ( SpellId.CANTRIPSPRINT1,                      0.5f ),
                    ( SpellId.CANTRIPWEAPONEXPERTISE1,             0.5f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                armorCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPSTRENGTH1,                    1.0f ),
                    ( SpellId.CANTRIPENDURANCE1,                   1.0f ),
                    ( SpellId.CANTRIPCOORDINATION1,                1.0f ),
                    ( SpellId.CANTRIPQUICKNESS1,                   1.0f ),
                    ( SpellId.CANTRIPFOCUS1,                       1.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                   1.0f ),

                    ( SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1,        1.0f ), // CANTRIPAXEAPTITUDE1
                    ( SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPDAGGERAPTITUDE1
                    //( SpellId.CANTRIPMACEAPTITUDE1,                1.0f ),
                    ( SpellId.CANTRIPSPEARAPTITUDE1,               1.0f ),
                    //( SpellId.CANTRIPSTAFFAPTITUDE1,               1.0f ),
                    ( SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1,        1.0f ), // CANTRIPSWORDAPTITUDE1
                    ( SpellId.CANTRIPUNARMEDAPTITUDE1,             1.0f ),
                    ( SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPBOWAPTITUDE1
                    //( SpellId.CANTRIPCROSSBOWAPTITUDE1,            1.0f ),
                    ( SpellId.CANTRIPTHROWNAPTITUDE1,              1.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,              1.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,             1.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,             1.0f ),

                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           1.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,            1.0f ),

                    ( SpellId.CANTRIPIMPENETRABILITY1,             1.0f ),
                    ( SpellId.CANTRIPACIDBANE1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGBANE1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEBANE1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTBANE1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSTORMBANE1,                   1.0f ),

                    ( SpellId.CANTRIPARMOR1,                       1.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                   1.0f ),

                    ( SpellId.CantripShieldAptitude1,              1.0f ),
                    ( SpellId.CantripArmorAptitude1,               1.0f ),
                    ( SpellId.CantripAwarenessAptitude1,           1.0f ),
                    ( SpellId.CantripAppraiseAptitude1,            1.0f ),
                    ( SpellId.CantripSneakingAptitude1,            1.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,           0.5f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,               0.5f ),
                    ( SpellId.CANTRIPCOOKINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPFEALTY1,                      0.5f ),
                    ( SpellId.CANTRIPFLETCHINGPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPHEALINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPJUMPINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPLEADERSHIP1,                  0.5f ),
                    ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.5f ),
                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.5f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.5f ),
                    ( SpellId.CantripSalvaging1,                   0.5f ),
                    ( SpellId.CANTRIPSPRINT1,                      0.5f ),
                };

                shieldCantrips = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CANTRIPSTRENGTH1,                    1.0f ),
                    ( SpellId.CANTRIPENDURANCE1,                   1.0f ),
                    ( SpellId.CANTRIPCOORDINATION1,                1.0f ),
                    ( SpellId.CANTRIPQUICKNESS1,                   1.0f ),
                    ( SpellId.CANTRIPFOCUS1,                       1.0f ),
                    ( SpellId.CANTRIPWILLPOWER1,                   1.0f ),

                    ( SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1,        1.0f ), // CANTRIPAXEAPTITUDE1
                    ( SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPDAGGERAPTITUDE1
                    //( SpellId.CANTRIPMACEAPTITUDE1,                1.0f ),
                    ( SpellId.CANTRIPSPEARAPTITUDE1,               1.0f ),
                    //( SpellId.CANTRIPSTAFFAPTITUDE1,               1.0f ),
                    ( SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1,        1.0f ), // CANTRIPSWORDAPTITUDE1
                    ( SpellId.CANTRIPUNARMEDAPTITUDE1,             1.0f ),
                    ( SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1,      1.0f ), // CANTRIPBOWAPTITUDE1
                    //( SpellId.CANTRIPCROSSBOWAPTITUDE1,            1.0f ),
                    ( SpellId.CANTRIPTHROWNAPTITUDE1,              1.0f ),

                    ( SpellId.CANTRIPIMPREGNABILITY1,              1.0f ),
                    ( SpellId.CANTRIPINVULNERABILITY1,             1.0f ),
                    ( SpellId.CANTRIPMAGICRESISTANCE1,             1.0f ),

                    ( SpellId.CANTRIPLIFEMAGICAPTITUDE1,           1.0f ),
                    ( SpellId.CANTRIPWARMAGICAPTITUDE1,            1.0f ),

                    ( SpellId.CantripHeartBlocker1,                1.0f ),
                    ( SpellId.CANTRIPIMPENETRABILITY1,             1.0f ),
                    ( SpellId.CANTRIPACIDBANE1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGBANE1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEBANE1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTBANE1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGBANE1,                1.0f ),
                    ( SpellId.CANTRIPSTORMBANE1,                   1.0f ),

                    ( SpellId.CANTRIPARMOR1,                       1.0f ),
                    ( SpellId.CANTRIPACIDWARD1,                    1.0f ),
                    ( SpellId.CANTRIPBLUDGEONINGWARD1,             1.0f ),
                    ( SpellId.CANTRIPFLAMEWARD1,                   1.0f ),
                    ( SpellId.CANTRIPFROSTWARD1,                   1.0f ),
                    ( SpellId.CANTRIPPIERCINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSLASHINGWARD1,                1.0f ),
                    ( SpellId.CANTRIPSTORMWARD1,                   1.0f ),

                    ( SpellId.CantripShieldAptitude1,              1.0f ),
                    ( SpellId.CantripArmorAptitude1,               1.0f ),
                    ( SpellId.CantripAwarenessAptitude1,           1.0f ),
                    ( SpellId.CantripAppraiseAptitude1,            1.0f ),
                    ( SpellId.CantripSneakingAptitude1,            1.0f ),

                    ( SpellId.CANTRIPALCHEMICALPROWESS1,           0.5f ),
                    ( SpellId.CANTRIPARCANEPROWESS1,               0.5f ),
                    ( SpellId.CANTRIPCOOKINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPDECEPTIONPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPFEALTY1,                      0.5f ),
                    ( SpellId.CANTRIPFLETCHINGPROWESS1,            0.5f ),
                    ( SpellId.CANTRIPHEALINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPJUMPINGPROWESS1,              0.5f ),
                    ( SpellId.CANTRIPLEADERSHIP1,                  0.5f ),
                    ( SpellId.CANTRIPLOCKPICKPROWESS1,             0.5f ),
                    ( SpellId.CANTRIPMANACONVERSIONPROWESS1,       0.5f ),
                    ( SpellId.CANTRIPMONSTERATTUNEMENT1,           0.5f ),
                    ( SpellId.CantripSalvaging1,                   0.5f ),
                    ( SpellId.CANTRIPSPRINT1,                      0.5f ),
                };
            }
        }

        public static SpellId Roll(bool isShield)
        {
            if(isShield)
                return shieldCantrips.Roll();
            else
                return armorCantrips.Roll();
        }

        public static List<SpellId> GetSpellIdList(bool isShield)
        {
            var spellIds = new List<SpellId>();
            if (isShield)
            {
                foreach (var entry in shieldCantrips)
                {
                    spellIds.Add(entry.result);
                }
            }
            else
            {
                foreach (var entry in armorCantrips)
                {
                    spellIds.Add(entry.result);
                }
            }
            return spellIds;
        }
    }
}
