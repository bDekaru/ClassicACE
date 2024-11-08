using System;
using System.Collections.Generic;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class AmmoWcids
    {
        private static ChanceTable<WeenieClassName> T1_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static ChanceTable<WeenieClassName> T2_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static ChanceTable<WeenieClassName> T3_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static ChanceTable<WeenieClassName> T4_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static ChanceTable<WeenieClassName> T5_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static ChanceTable<WeenieClassName> T6_T8_Chances = new ChanceTable<WeenieClassName>()
        {
        };

        private static readonly List<ChanceTable<WeenieClassName>> ammoTiers = new List<ChanceTable<WeenieClassName>>()
        {
            T1_Chances,
            T2_Chances,
            T3_Chances,
            T4_Chances,
            T5_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
            T6_T8_Chances,
        };

        static AmmoWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                T1_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.longsticks,             2.0f ),
                    ( WeenieClassName.shortsticks,            2.0f ),

                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowhead,              4.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),
                };

                T2_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {

                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowhead,              1.0f ),
                    ( WeenieClassName.arrowheadblunt,         1.0f ),
                    ( WeenieClassName.arrowheadbroad,         1.0f ),
                    ( WeenieClassName.arrowheadfrogcrotch,    1.0f ),
                    ( WeenieClassName.arrowheadarmorpiercing, 1.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),

                    ( WeenieClassName.oilacid,                0.5f ),
                    ( WeenieClassName.fireoil,                0.5f ),
                    ( WeenieClassName.oilfrost,               0.5f ),
                    ( WeenieClassName.oillightning,           0.5f ),
                };

                T3_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {

                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowheadgreater,              1.0f ),
                    ( WeenieClassName.arrowheadgreaterblunt,         1.0f ),
                    ( WeenieClassName.arrowheadgreaterbroad,         1.0f ),
                    ( WeenieClassName.arrowheadgreaterfrogcrotch,    1.0f ),
                    ( WeenieClassName.arrowheadgreaterarmorpiercing, 1.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),

                    ( WeenieClassName.oilacid,                0.5f ),
                    ( WeenieClassName.fireoil,                0.5f ),
                    ( WeenieClassName.oilfrost,               0.5f ),
                    ( WeenieClassName.oillightning,           0.5f ),
                };

                T4_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {

                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowheadgreater,              1.0f ),
                    ( WeenieClassName.arrowheadgreaterblunt,         1.0f ),
                    ( WeenieClassName.arrowheadgreaterbroad,         1.0f ),
                    ( WeenieClassName.arrowheadgreaterfrogcrotch,    1.0f ),
                    ( WeenieClassName.arrowheadgreaterarmorpiercing, 1.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),

                    ( WeenieClassName.oilacid,                0.5f ),
                    ( WeenieClassName.fireoil,                0.5f ),
                    ( WeenieClassName.oilfrost,               0.5f ),
                    ( WeenieClassName.oillightning,           0.5f ),
                };

                T5_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowheaddeadly,              1.0f ),
                    ( WeenieClassName.arrowheaddeadlyblunt,         1.0f ),
                    ( WeenieClassName.arrowheaddeadlybroad,         1.0f ),
                    ( WeenieClassName.arrowheaddeadlyfrogcrotch,    1.0f ),
                    ( WeenieClassName.arrowheaddeadlyarmorpiercing, 1.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),

                    ( WeenieClassName.oilacid,                0.5f ),
                    ( WeenieClassName.fireoil,                0.5f ),
                    ( WeenieClassName.oilfrost,               0.5f ),
                    ( WeenieClassName.oillightning,           0.5f ),
                };

                T6_T8_Chances = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.healthtonic,        1.0f ),
                    ( WeenieClassName.manatonic,          1.0f ),
                    ( WeenieClassName.staminatonic,       1.0f ),
                    ( WeenieClassName.healthphiltre,      3.0f ),
                    ( WeenieClassName.manaphiltre,        3.0f ),
                    ( WeenieClassName.staminaphiltre,     3.0f ),

                    ( WeenieClassName.arrowshaft,             1.0f ),
                    ( WeenieClassName.quarrelshaft,           1.0f ),
                    ( WeenieClassName.atlatldartshaft,        0.5f ),

                    ( WeenieClassName.arrowheaddeadly,              1.0f ),
                    ( WeenieClassName.arrowheaddeadlyblunt,         1.0f ),
                    ( WeenieClassName.arrowheaddeadlybroad,         1.0f ),
                    ( WeenieClassName.arrowheaddeadlyfrogcrotch,    1.0f ),
                    ( WeenieClassName.arrowheaddeadlyarmorpiercing, 1.0f ),

                    ( WeenieClassName.aquaincanta,            0.5f ),
                    ( WeenieClassName.neutralbalm,            0.5f ),

                    ( WeenieClassName.dart,                   1.0f ),
                    ( WeenieClassName.axethrowing,            1.0f ),
                    ( WeenieClassName.clubthrowing,           1.0f ),
                    ( WeenieClassName.daggerthrowing,         1.0f ),
                    ( WeenieClassName.javelin,                1.0f ),
                    ( WeenieClassName.shuriken,               1.0f ),
                    ( WeenieClassName.djarid,                 1.0f ),

                    ( WeenieClassName.oilacid,                0.5f ),
                    ( WeenieClassName.fireoil,                0.5f ),
                    ( WeenieClassName.oilfrost,               0.5f ),
                    ( WeenieClassName.oillightning,           0.5f ),
                };

                ammoTiers = new List<ChanceTable<WeenieClassName>>()
                {
                    T1_Chances,
                    T2_Chances,
                    T3_Chances,
                    T4_Chances,
                    T5_Chances,
                    T6_T8_Chances,
                    T6_T8_Chances,
                    T6_T8_Chances,
                };
            }
        }

        public static WeenieClassName Roll(TreasureDeath profile)
        {
            // todo: verify t7 / t8 chances
            var table = ammoTiers[profile.Tier - 1];

            // quality mod?
            return table.Roll(profile.LootQualityMod);
        }
    }
}
