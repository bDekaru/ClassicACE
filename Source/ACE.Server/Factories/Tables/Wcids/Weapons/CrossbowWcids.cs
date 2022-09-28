using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class CrossbowWcids
    {
        private static ChanceTable<WeenieClassName> T1_Chances;

        private static ChanceTable<WeenieClassName> T1_T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowlight,    0.50f ),
            ( WeenieClassName.crossbowheavy,    0.25f ),
            ( WeenieClassName.crossbowarbalest, 0.25f ),
        };

        private static ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowlight,                     0.25f ),
            ( WeenieClassName.crossbowheavy,                     0.13f ),
            ( WeenieClassName.crossbowarbalest,                  0.13f ),
            ( WeenieClassName.crossbowslashing,                  0.035f ),
            ( WeenieClassName.crossbowpiercing,                  0.035f ),
            ( WeenieClassName.crossbowblunt,                     0.035f ),
            ( WeenieClassName.crossbowacid,                      0.035f ),
            ( WeenieClassName.crossbowfire,                      0.035f ),
            ( WeenieClassName.crossbowfrost,                     0.035f ),
            ( WeenieClassName.crossbowelectric,                  0.035f ),
            ( WeenieClassName.ace31805_slashingcompoundcrossbow, 0.035f ),
            ( WeenieClassName.ace31811_piercingcompoundcrossbow, 0.035f ),
            ( WeenieClassName.ace31807_bluntcompoundcrossbow,    0.035f ),
            ( WeenieClassName.ace31806_acidcompoundcrossbow,     0.035f ),
            ( WeenieClassName.ace31809_firecompoundcrossbow,     0.035f ),
            ( WeenieClassName.ace31810_frostcompoundcrossbow,    0.035f ),
            ( WeenieClassName.ace31808_electriccompoundcrossbow, 0.035f ),
        };

        private static ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.crossbowslashing,                  0.075f ),
            ( WeenieClassName.crossbowpiercing,                  0.075f ),
            ( WeenieClassName.crossbowblunt,                     0.07f ),
            ( WeenieClassName.crossbowacid,                      0.07f ),
            ( WeenieClassName.crossbowfire,                      0.07f ),
            ( WeenieClassName.crossbowfrost,                     0.07f ),
            ( WeenieClassName.crossbowelectric,                  0.07f ),
            ( WeenieClassName.ace31805_slashingcompoundcrossbow, 0.075f ),
            ( WeenieClassName.ace31811_piercingcompoundcrossbow, 0.075f ),
            ( WeenieClassName.ace31807_bluntcompoundcrossbow,    0.07f ),
            ( WeenieClassName.ace31806_acidcompoundcrossbow,     0.07f ),
            ( WeenieClassName.ace31809_firecompoundcrossbow,     0.07f ),
            ( WeenieClassName.ace31810_frostcompoundcrossbow,    0.07f ),
            ( WeenieClassName.ace31808_electriccompoundcrossbow, 0.07f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> crossbowTiers = new List<ChanceTable<WeenieClassName>>()
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
            var roll = crossbowTiers[tier - 1].Roll();

            if (roll == WeenieClassName.crossbowlight && Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                weaponType = TreasureWeaponType.CrossbowLight; // Modify weapon type so we get correct mutations.
            else
                weaponType = TreasureWeaponType.Crossbow;

            return roll;
        }
        
		private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();
		
        static CrossbowWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowlight,    3.0f ),
                    ( WeenieClassName.crossbowheavy,    1.0f ),
                };

                T1_T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowlight,    1.00f ),
                    ( WeenieClassName.crossbowheavy,    1.00f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowlight,                     4.0f ),
                    ( WeenieClassName.crossbowheavy,                     4.0f ),

                    ( WeenieClassName.crossbowslashing,                  1.0f ),
                    ( WeenieClassName.crossbowpiercing,                  1.0f ),
                    ( WeenieClassName.crossbowblunt,                     1.0f ),
                    ( WeenieClassName.crossbowacid,                      1.0f ),
                    ( WeenieClassName.crossbowfire,                      1.0f ),
                    ( WeenieClassName.crossbowfrost,                     1.0f ),
                    ( WeenieClassName.crossbowelectric,                  1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowslashing,                  1.0f ),
                    ( WeenieClassName.crossbowpiercing,                  1.0f ),
                    ( WeenieClassName.crossbowblunt,                     1.0f ),
                    ( WeenieClassName.crossbowacid,                      1.0f ),
                    ( WeenieClassName.crossbowfire,                      1.0f ),
                    ( WeenieClassName.crossbowfrost,                     1.0f ),
                    ( WeenieClassName.crossbowelectric,                  1.0f ),
                };
                
                // we have to refresh this list or it will still contain the previous values.
                crossbowTiers = new List<ChanceTable<WeenieClassName>>()
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
                    ( WeenieClassName.crossbowlight,    3.0f ),
                    ( WeenieClassName.crossbowheavy,    1.0f ),
                };

                T1_T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowlight,    1.00f ),
                    ( WeenieClassName.crossbowheavy,    1.00f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowlight,                     16.0f ),
                    ( WeenieClassName.crossbowheavy,                     16.0f ),

                    ( WeenieClassName.crossbowslashing,                  1.0f ),
                    ( WeenieClassName.crossbowpiercing,                  1.0f ),
                    ( WeenieClassName.crossbowblunt,                     1.0f ),
                    ( WeenieClassName.crossbowacid,                      1.0f ),
                    ( WeenieClassName.crossbowfire,                      1.0f ),
                    ( WeenieClassName.crossbowfrost,                     1.0f ),
                    ( WeenieClassName.crossbowelectric,                  1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.crossbowslashing,                  1.0f ),
                    ( WeenieClassName.crossbowpiercing,                  1.0f ),
                    ( WeenieClassName.crossbowblunt,                     1.0f ),
                    ( WeenieClassName.crossbowacid,                      1.0f ),
                    ( WeenieClassName.crossbowfire,                      1.0f ),
                    ( WeenieClassName.crossbowfrost,                     1.0f ),
                    ( WeenieClassName.crossbowelectric,                  1.0f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                crossbowTiers = new List<ChanceTable<WeenieClassName>>()
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
            
            foreach (var crossbowTier in crossbowTiers)
            {
                foreach (var entry in crossbowTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Crossbow);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
