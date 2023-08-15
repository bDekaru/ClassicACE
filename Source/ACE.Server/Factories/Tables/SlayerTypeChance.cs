using System.Collections.Generic;

using ACE.Server.Factories.Entity;
using ACE.Database.Models.World;
using ACE.Entity.Enum;

namespace ACE.Server.Factories.Tables
{
    public static class SlayerTypeChance
    {
        public static float GetSlayerChanceForTier(int tier)
        {
            switch (tier)
            {
                case 1:
                default:
                    return 0.01f;
                case 2:
                    return 0.01f;
                case 3:
                    return 0.01f;
                case 4:
                    return 0.01f;
                case 5:
                    return 0.01f;
                case 6:
                    return 0.01f;
                case 7:
                    return 0.01f;
                case 8:
                    return 0.01f;
            }
        }

        private static ChanceTable<CreatureType> T1_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
        };

        private static ChanceTable<CreatureType> T2_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
        };

        private static ChanceTable<CreatureType> T3_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
            ( CreatureType.Margul, 1.0f ),
            ( CreatureType.Burun, 1.0f )
        };

        private static ChanceTable<CreatureType> T4_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
            ( CreatureType.Margul, 1.0f ),
            ( CreatureType.Burun, 1.0f )
        };

        private static ChanceTable<CreatureType> T5_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
            ( CreatureType.Margul, 1.0f ),
            ( CreatureType.Burun, 1.0f )
        };

        private static ChanceTable<CreatureType> T6_T8_SlayerTypeChances = new ChanceTable<CreatureType>(ChanceTableType.Weight)
        {
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Olthoi, 1.0f ),
            ( CreatureType.Banderling, 1.0f ),
            ( CreatureType.Drudge, 1.0f ),
            ( CreatureType.Mosswart, 1.0f ),
            ( CreatureType.Lugian, 1.0f ),
            ( CreatureType.Tumerok, 1.0f ),
            ( CreatureType.Mite, 1.0f ),
            ( CreatureType.Tusker, 1.0f ),
            ( CreatureType.PhyntosWasp, 1.0f ),
            ( CreatureType.Rat, 1.0f ),
            ( CreatureType.Cow, 1.0f ),
            ( CreatureType.Golem, 1.0f ),
            ( CreatureType.Undead, 1.0f ),
            ( CreatureType.Gromnie, 1.0f ),
            ( CreatureType.Reedshark, 1.0f ),
            ( CreatureType.Armoredillo, 1.0f ),
            ( CreatureType.Virindi, 1.0f ),
            ( CreatureType.Wisp, 1.0f ),
            ( CreatureType.Shadow, 1.0f ),
            ( CreatureType.Mattekar, 1.0f ),
            ( CreatureType.Sclavus, 1.0f ),
            ( CreatureType.ShallowsShark, 1.0f ),
            ( CreatureType.Monouga, 1.0f ),
            ( CreatureType.Zefir, 1.0f ),
            ( CreatureType.Human, 1.0f ),
            ( CreatureType.Shreth, 1.0f ),
            ( CreatureType.Chittick, 1.0f ),
            ( CreatureType.Moarsman, 1.0f ),
            ( CreatureType.Slithis, 1.0f ),
            ( CreatureType.Grievver, 1.0f ),
            ( CreatureType.Niffis, 1.0f ),
            ( CreatureType.Ursuin, 1.0f ),
            ( CreatureType.Crystal, 1.0f ),
            ( CreatureType.Idol, 1.0f ),
            ( CreatureType.Carenzi, 1.0f ),
            ( CreatureType.Siraluun, 1.0f ),
            ( CreatureType.Simulacrum, 1.0f ),
            ( CreatureType.Elemental, 1.0f ),
            ( CreatureType.Margul, 1.0f ),
            ( CreatureType.Burun, 1.0f )
        };

        private static readonly List<ChanceTable<CreatureType>> slayerTypeChance = new List<ChanceTable<CreatureType>>()
        {
            T1_SlayerTypeChances,
            T2_SlayerTypeChances,
            T3_SlayerTypeChances,
            T4_SlayerTypeChances,
            T5_SlayerTypeChances,
            T6_T8_SlayerTypeChances,
            T6_T8_SlayerTypeChances,
            T6_T8_SlayerTypeChances,
        };

        public static CreatureType Roll(TreasureDeath profile)
        {
            var table = slayerTypeChance[profile.Tier - 1];

            return table.Roll(profile.LootQualityMod);
        }
    }
}
