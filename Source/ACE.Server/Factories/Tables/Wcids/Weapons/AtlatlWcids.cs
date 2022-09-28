using System;
using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class AtlatlWcids
    {
        private static ChanceTable<WeenieClassName> T1_Chances;

        private static ChanceTable<WeenieClassName> T1_T4_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.atlatl,      0.50f ),
            ( WeenieClassName.atlatlroyal, 0.50f ),
        };

        private static ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.atlatl,                     0.25f ),
            ( WeenieClassName.atlatlroyal,                0.26f ),
            ( WeenieClassName.atlatlslashing,             0.035f ),
            ( WeenieClassName.atlatlpiercing,             0.035f ),
            ( WeenieClassName.atlatlblunt,                0.035f ),
            ( WeenieClassName.atlatlacid,                 0.035f ),
            ( WeenieClassName.atlatlfire,                 0.035f ),
            ( WeenieClassName.atlatlfrost,                0.035f ),
            ( WeenieClassName.atlatlelectric,             0.035f ),
            ( WeenieClassName.ace31812_slashingslingshot, 0.035f ),
            ( WeenieClassName.ace31818_piercingslingshot, 0.035f ),
            ( WeenieClassName.ace31814_bluntslingshot,    0.035f ),
            ( WeenieClassName.ace31813_acidslingshot,     0.035f ),
            ( WeenieClassName.ace31816_fireslingshot,     0.035f ),
            ( WeenieClassName.ace31817_frostslingshot,    0.035f ),
            ( WeenieClassName.ace31815_electricslingshot, 0.035f ),
        };

        private static ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.atlatlslashing,             0.075f ),
            ( WeenieClassName.atlatlpiercing,             0.075f ),
            ( WeenieClassName.atlatlblunt,                0.07f ),
            ( WeenieClassName.atlatlacid,                 0.07f ),
            ( WeenieClassName.atlatlfire,                 0.07f ),
            ( WeenieClassName.atlatlfrost,                0.07f ),
            ( WeenieClassName.atlatlelectric,             0.07f ),
            ( WeenieClassName.ace31812_slashingslingshot, 0.075f ),
            ( WeenieClassName.ace31818_piercingslingshot, 0.075f ),
            ( WeenieClassName.ace31814_bluntslingshot,    0.07f ),
            ( WeenieClassName.ace31813_acidslingshot,     0.07f ),
            ( WeenieClassName.ace31816_fireslingshot,     0.07f ),
            ( WeenieClassName.ace31817_frostslingshot,    0.07f ),
            ( WeenieClassName.ace31815_electricslingshot, 0.07f ),
        };

        private static readonly List<ChanceTable<WeenieClassName>> atlatlTiers = new List<ChanceTable<WeenieClassName>>()
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
            var roll = atlatlTiers[tier - 1].Roll();

            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                switch (roll) // Modify weapon type so we get correct mutations.
                {
                    case WeenieClassName.dart:
                    case WeenieClassName.dartacid:
                    case WeenieClassName.dartflame:
                    case WeenieClassName.dartfrost:
                    case WeenieClassName.dartelectric:
                    case WeenieClassName.axethrowing:
                    case WeenieClassName.axethrowingacid:
                    case WeenieClassName.axethrowingfire:
                    case WeenieClassName.axethrowingfrost:
                    case WeenieClassName.axethrowingelectric:
                    case WeenieClassName.clubthrowing:
                    case WeenieClassName.clubthrowingacid:
                    case WeenieClassName.clubthrowingfire:
                    case WeenieClassName.clubthrowingfrost:
                    case WeenieClassName.clubthrowingelectric:
                    case WeenieClassName.daggerthrowing:
                    case WeenieClassName.daggerthrowingacid:
                    case WeenieClassName.daggerthrowingfire:
                    case WeenieClassName.daggerthrowingfrost:
                    case WeenieClassName.daggerthrowingelectric:
                    case WeenieClassName.javelin:
                    case WeenieClassName.javelinacid:
                    case WeenieClassName.javelinfire:
                    case WeenieClassName.javelinfrost:
                    case WeenieClassName.javelinelectric:
                    case WeenieClassName.shuriken:
                    case WeenieClassName.shurikenacid:
                    case WeenieClassName.shurikenfire:
                    case WeenieClassName.shurikenfrost:
                    case WeenieClassName.shurikenelectric:
                    case WeenieClassName.djarid:
                    case WeenieClassName.djaridacid:
                    case WeenieClassName.djaridfire:
                    case WeenieClassName.djaridfrost:
                    case WeenieClassName.djaridelectric:
                        weaponType = TreasureWeaponType.Thrown;
                        break;
                    case WeenieClassName.atlatl:
                        weaponType = TreasureWeaponType.AtlatlRegular;
                        break;
                    default:
                        weaponType = TreasureWeaponType.Atlatl;
                        break;
                }
            }
            else
                weaponType = TreasureWeaponType.Atlatl;

            return roll;
        }
   
        private static readonly Dictionary<WeenieClassName, TreasureWeaponType> _combined = new Dictionary<WeenieClassName, TreasureWeaponType>();

        static AtlatlWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatl,      3.0f ),
                    ( WeenieClassName.atlatlroyal, 1.0f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatl,                     4.0f ),
                    ( WeenieClassName.atlatlroyal,                4.0f ),

                    ( WeenieClassName.atlatlslashing,             1.0f ),
                    ( WeenieClassName.atlatlpiercing,             1.0f ),
                    ( WeenieClassName.atlatlblunt,                1.0f ),
                    ( WeenieClassName.atlatlacid,                 1.0f ),
                    ( WeenieClassName.atlatlfire,                 1.0f ),
                    ( WeenieClassName.atlatlfrost,                1.0f ),
                    ( WeenieClassName.atlatlelectric,             1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatlslashing,             1.0f ),
                    ( WeenieClassName.atlatlpiercing,             1.0f ),
                    ( WeenieClassName.atlatlblunt,                1.0f ),
                    ( WeenieClassName.atlatlacid,                 1.0f ),
                    ( WeenieClassName.atlatlfire,                 1.0f ),
                    ( WeenieClassName.atlatlfrost,                1.0f ),
                    ( WeenieClassName.atlatlelectric,             1.0f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                atlatlTiers = new List<ChanceTable<WeenieClassName>>()
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
                    ( WeenieClassName.atlatl,      3.0f ),
                    ( WeenieClassName.atlatlroyal, 1.0f ),

                    ( WeenieClassName.dart, 3.0f ),
                    ( WeenieClassName.axethrowing, 1.0f ),
                    ( WeenieClassName.clubthrowing, 1.0f ),
                    ( WeenieClassName.daggerthrowing, 1.0f ),
                    ( WeenieClassName.javelin, 1.0f ),
                    ( WeenieClassName.shuriken, 1.0f ),
                    ( WeenieClassName.djarid, 1.0f ),
                };

                T1_T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatl,                   16.0f ),
                    ( WeenieClassName.atlatlroyal,              16.0f ),

                    ( WeenieClassName.dart,                     16.0f ),
                    ( WeenieClassName.dartacid,                  1.0f ),
                    ( WeenieClassName.dartflame,                 1.0f ),
                    ( WeenieClassName.dartfrost,                 1.0f ),
                    ( WeenieClassName.dartelectric,              1.0f ),

                    ( WeenieClassName.axethrowing,              16.0f ),
                    ( WeenieClassName.axethrowingacid,           1.0f ),
                    ( WeenieClassName.axethrowingfire,           1.0f ),
                    ( WeenieClassName.axethrowingfrost,          1.0f ),
                    ( WeenieClassName.axethrowingelectric,       1.0f ),

                    ( WeenieClassName.clubthrowing,             16.0f ),
                    ( WeenieClassName.clubthrowingacid,          1.0f ),
                    ( WeenieClassName.clubthrowingfire,          1.0f ),
                    ( WeenieClassName.clubthrowingfrost,         1.0f ),
                    ( WeenieClassName.clubthrowingelectric,      1.0f ),

                    ( WeenieClassName.daggerthrowing,           16.0f ),
                    ( WeenieClassName.daggerthrowingacid,        1.0f ),
                    ( WeenieClassName.daggerthrowingfire,        1.0f ),
                    ( WeenieClassName.daggerthrowingfrost,       1.0f ),
                    ( WeenieClassName.daggerthrowingelectric,    1.0f ),

                    ( WeenieClassName.javelin,                  16.0f ),
                    ( WeenieClassName.javelinacid,               1.0f ),
                    ( WeenieClassName.javelinfire,               1.0f ),
                    ( WeenieClassName.javelinfrost,              1.0f ),
                    ( WeenieClassName.javelinelectric,           1.0f ),

                    ( WeenieClassName.shuriken,                 16.0f ),
                    ( WeenieClassName.shurikenacid,              1.0f ),
                    ( WeenieClassName.shurikenfire,              1.0f ),
                    ( WeenieClassName.shurikenfrost,             1.0f ),
                    ( WeenieClassName.shurikenelectric,          1.0f ),

                    ( WeenieClassName.djarid,                   16.0f ),
                    ( WeenieClassName.djaridacid,                1.0f ),
                    ( WeenieClassName.djaridfire,                1.0f ),
                    ( WeenieClassName.djaridfrost,               1.0f ),
                    ( WeenieClassName.djaridelectric,            1.0f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatl,                    16.0f ),
                    ( WeenieClassName.atlatlroyal,               16.0f ),
                    ( WeenieClassName.atlatlslashing,             1.0f ),
                    ( WeenieClassName.atlatlpiercing,             1.0f ),
                    ( WeenieClassName.atlatlblunt,                1.0f ),
                    ( WeenieClassName.atlatlacid,                 1.0f ),
                    ( WeenieClassName.atlatlfire,                 1.0f ),
                    ( WeenieClassName.atlatlfrost,                1.0f ),
                    ( WeenieClassName.atlatlelectric,             1.0f ),

                    ( WeenieClassName.dart,                       16.0f ),
                    ( WeenieClassName.dartacid,                    1.0f ),
                    ( WeenieClassName.dartflame,                   1.0f ),
                    ( WeenieClassName.dartfrost,                   1.0f ),
                    ( WeenieClassName.dartelectric,                1.0f ),

                    ( WeenieClassName.axethrowing,                16.0f ),
                    ( WeenieClassName.axethrowingacid,             1.0f ),
                    ( WeenieClassName.axethrowingfire,             1.0f ),
                    ( WeenieClassName.axethrowingfrost,            1.0f ),
                    ( WeenieClassName.axethrowingelectric,         1.0f ),

                    ( WeenieClassName.clubthrowing,               16.0f ),
                    ( WeenieClassName.clubthrowingacid,            1.0f ),
                    ( WeenieClassName.clubthrowingfire,            1.0f ),
                    ( WeenieClassName.clubthrowingfrost,           1.0f ),
                    ( WeenieClassName.clubthrowingelectric,        1.0f ),

                    ( WeenieClassName.daggerthrowing,             16.0f ),
                    ( WeenieClassName.daggerthrowingacid,          1.0f ),
                    ( WeenieClassName.daggerthrowingfire,          1.0f ),
                    ( WeenieClassName.daggerthrowingfrost,         1.0f ),
                    ( WeenieClassName.daggerthrowingelectric,      1.0f ),

                    ( WeenieClassName.javelin,                    16.0f ),
                    ( WeenieClassName.javelinacid,                 1.0f ),
                    ( WeenieClassName.javelinfire,                 1.0f ),
                    ( WeenieClassName.javelinfrost,                1.0f ),
                    ( WeenieClassName.javelinelectric,             1.0f ),

                    ( WeenieClassName.shuriken,                   16.0f ),
                    ( WeenieClassName.shurikenacid,                1.0f ),
                    ( WeenieClassName.shurikenfire,                1.0f ),
                    ( WeenieClassName.shurikenfrost,               1.0f ),
                    ( WeenieClassName.shurikenelectric,            1.0f ),

                    ( WeenieClassName.djarid,                     16.0f ),
                    ( WeenieClassName.djaridacid,                  1.0f ),
                    ( WeenieClassName.djaridfire,                  1.0f ),
                    ( WeenieClassName.djaridfrost,                 1.0f ),
                    ( WeenieClassName.djaridelectric,              1.0f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.atlatlslashing,             1.0f ),
                    ( WeenieClassName.atlatlpiercing,             1.0f ),
                    ( WeenieClassName.atlatlblunt,                1.0f ),
                    ( WeenieClassName.atlatlacid,                 1.0f ),
                    ( WeenieClassName.atlatlfire,                 1.0f ),
                    ( WeenieClassName.atlatlfrost,                1.0f ),
                    ( WeenieClassName.atlatlelectric,             1.0f ),

                    ( WeenieClassName.dart,                       1.0f ),
                    ( WeenieClassName.dartacid,                   1.0f ),
                    ( WeenieClassName.dartflame,                  1.0f ),
                    ( WeenieClassName.dartfrost,                  1.0f ),
                    ( WeenieClassName.dartelectric,               1.0f ),

                    ( WeenieClassName.axethrowing,                1.0f ),
                    ( WeenieClassName.axethrowingacid,            1.0f ),
                    ( WeenieClassName.axethrowingfire,            1.0f ),
                    ( WeenieClassName.axethrowingfrost,           1.0f ),
                    ( WeenieClassName.axethrowingelectric,        1.0f ),

                    ( WeenieClassName.clubthrowing,               1.0f ),
                    ( WeenieClassName.clubthrowingacid,           1.0f ),
                    ( WeenieClassName.clubthrowingfire,           1.0f ),
                    ( WeenieClassName.clubthrowingfrost,          1.0f ),
                    ( WeenieClassName.clubthrowingelectric,       1.0f ),

                    ( WeenieClassName.daggerthrowing,             1.0f ),
                    ( WeenieClassName.daggerthrowingacid,         1.0f ),
                    ( WeenieClassName.daggerthrowingfire,         1.0f ),
                    ( WeenieClassName.daggerthrowingfrost,        1.0f ),
                    ( WeenieClassName.daggerthrowingelectric,     1.0f ),

                    ( WeenieClassName.javelin,                    1.0f ),
                    ( WeenieClassName.javelinacid,                1.0f ),
                    ( WeenieClassName.javelinfire,                1.0f ),
                    ( WeenieClassName.javelinfrost,               1.0f ),
                    ( WeenieClassName.javelinelectric,            1.0f ),

                    ( WeenieClassName.shuriken,                   1.0f ),
                    ( WeenieClassName.shurikenacid,               1.0f ),
                    ( WeenieClassName.shurikenfire,               1.0f ),
                    ( WeenieClassName.shurikenfrost,              1.0f ),
                    ( WeenieClassName.shurikenelectric,           1.0f ),

                    ( WeenieClassName.djarid,                     1.0f ),
                    ( WeenieClassName.djaridacid,                 1.0f ),
                    ( WeenieClassName.djaridfire,                 1.0f ),
                    ( WeenieClassName.djaridfrost,                1.0f ),
                    ( WeenieClassName.djaridelectric,             1.0f ),
                };

                // we have to refresh this list or it will still contain the previous values.
                atlatlTiers = new List<ChanceTable<WeenieClassName>>()
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
            
	   		foreach (var atlatlTier in atlatlTiers)
            {
                foreach (var entry in atlatlTier)
                    _combined.TryAdd(entry.result, TreasureWeaponType.Atlatl);
            }
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureWeaponType weaponType)
        {
            return _combined.TryGetValue(wcid, out weaponType);
        }
    }
}
