using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class BowWcids_Aluvian
    {
        private static ChanceTable<WeenieClassName> T1_Chances = new ChanceTable<WeenieClassName>();

        private static ChanceTable<WeenieClassName> T1_T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bowshort, 0.50f ),
            ( WeenieClassName.bowlong,  0.25f ),
            ( WeenieClassName.bowwar,   0.25f )
        };

        private static ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bowshort,                     0.25f ),
            ( WeenieClassName.bowlong,                      0.13f ),
            ( WeenieClassName.bowwar,                       0.13f ),
            ( WeenieClassName.bowslashing,                  0.035f ),
            ( WeenieClassName.bowpiercing,                  0.035f ),
            ( WeenieClassName.bowblunt,                     0.035f ),
            ( WeenieClassName.bowacid,                      0.035f ),
            ( WeenieClassName.bowfire,                      0.035f ),
            ( WeenieClassName.bowfrost,                     0.035f ),
            ( WeenieClassName.bowelectric,                  0.035f ),
            ( WeenieClassName.ace31798_slashingcompoundbow, 0.035f ),
            ( WeenieClassName.ace31804_piercingcompoundbow, 0.035f ),
            ( WeenieClassName.ace31800_bluntcompoundbow,    0.035f ),
            ( WeenieClassName.ace31799_acidcompoundbow,     0.035f ),
            ( WeenieClassName.ace31802_firecompoundbow,     0.035f ),
            ( WeenieClassName.ace31803_frostcompoundbow,    0.035f ),
            ( WeenieClassName.ace31801_electriccompoundbow, 0.035f ),
        };

        private static ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bowslashing,                  0.075f ),
            ( WeenieClassName.bowpiercing,                  0.075f ),
            ( WeenieClassName.bowblunt,                     0.07f ),
            ( WeenieClassName.bowacid,                      0.07f ),
            ( WeenieClassName.bowfire,                      0.07f ),
            ( WeenieClassName.bowfrost,                     0.07f ),
            ( WeenieClassName.bowelectric,                  0.07f ),
            ( WeenieClassName.ace31798_slashingcompoundbow, 0.075f ),
            ( WeenieClassName.ace31804_piercingcompoundbow, 0.075f ),
            ( WeenieClassName.ace31800_bluntcompoundbow,    0.07f ),
            ( WeenieClassName.ace31799_acidcompoundbow,     0.07f ),
            ( WeenieClassName.ace31802_firecompoundbow,     0.07f ),
            ( WeenieClassName.ace31803_frostcompoundbow,    0.07f ),
            ( WeenieClassName.ace31801_electriccompoundbow, 0.07f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> bowTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T5_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
        };
        
        public static WeenieClassName Roll(int tier, out TreasureWeaponType weaponType)
        {
            var roll = bowTiers[tier - 1].Roll();

            if (roll == WeenieClassName.bowshort && Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                weaponType = TreasureWeaponType.BowShort; // Modify weapon type so we get correct mutations.
            else
                weaponType = TreasureWeaponType.Bow;

            return roll;
        }
        
        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();
        
        static BowWcids_Aluvian()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowshort,   3.0f ),
                    ( WeenieClassName.bowlong,    1.0f )
                };

                T1_T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowshort, 1.0f ),
                    ( WeenieClassName.bowlong,  1.0f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowlong,                      4.0f ),

                    ( WeenieClassName.bowslashing,                  1.0f ),
                    ( WeenieClassName.bowpiercing,                  1.0f ),
                    ( WeenieClassName.bowblunt,                     1.0f ),
                    ( WeenieClassName.bowacid,                      1.0f ),
                    ( WeenieClassName.bowfire,                      1.0f ),
                    ( WeenieClassName.bowfrost,                     1.0f ),
                    ( WeenieClassName.bowelectric,                  1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowslashing,                  1.0f ),
                    ( WeenieClassName.bowpiercing,                  1.0f ),
                    ( WeenieClassName.bowblunt,                     1.0f ),
                    ( WeenieClassName.bowacid,                      1.0f ),
                    ( WeenieClassName.bowfire,                      1.0f ),
                    ( WeenieClassName.bowfrost,                     1.0f ),
                    ( WeenieClassName.bowelectric,                  1.0f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                bowTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T1_T4_Chances,
                    T1_T4_Chances,
                    T1_T4_Chances,
                    T5_Chances,
                    T6_T8_Chances,
                    T6_T8_Chances,
                    T6_T8_Chances,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowshort,   3.0f ),
                    ( WeenieClassName.bowlong,    1.0f )
                };

                T1_T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowshort, 1.0f ),
                    ( WeenieClassName.bowlong,  1.0f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowlong,                     16.0f ),

                    ( WeenieClassName.bowslashing,                  1.0f ),
                    ( WeenieClassName.bowpiercing,                  1.0f ),
                    ( WeenieClassName.bowblunt,                     1.0f ),
                    ( WeenieClassName.bowacid,                      1.0f ),
                    ( WeenieClassName.bowfire,                      1.0f ),
                    ( WeenieClassName.bowfrost,                     1.0f ),
                    ( WeenieClassName.bowelectric,                  1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.bowslashing,                  1.0f ),
                    ( WeenieClassName.bowpiercing,                  1.0f ),
                    ( WeenieClassName.bowblunt,                     1.0f ),
                    ( WeenieClassName.bowacid,                      1.0f ),
                    ( WeenieClassName.bowfire,                      1.0f ),
                    ( WeenieClassName.bowfrost,                     1.0f ),
                    ( WeenieClassName.bowelectric,                  1.0f ),
                };
                
                // we have to refresh this list or it will still contain the previous values.
                bowTiers = new List<ChanceTable<WeenieClassName>>()
		        {
                    T1_Chances,
		            T1_T4_Chances,
		            T1_T4_Chances,
		            T1_T4_Chances,
		            T5_Chances,
		            T6_T8_Chances,
		            T6_T8_Chances,
		            T6_T8_Chances,
		        };
            }
            
			foreach (var bowTier in bowTiers)
            {
                foreach (var entry in bowTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Bow);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
