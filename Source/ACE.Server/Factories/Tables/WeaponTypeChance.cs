using System;
using System.Collections.Generic;
using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

namespace ACE.Server.Factories.Tables
{
    public static class WeaponTypeChance
    {
        private static ChanceTable<TreasureWeaponType> T1_T4_Chances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 84%
            // missile: 12%
            // caster: 4%
            ( TreasureWeaponType.Sword,    0.12f ),
            ( TreasureWeaponType.Mace,     0.12f ),
            ( TreasureWeaponType.Axe,      0.12f ),
            ( TreasureWeaponType.Spear,    0.12f ),
            ( TreasureWeaponType.Unarmed,  0.12f ),
            ( TreasureWeaponType.Staff,    0.12f ),
            ( TreasureWeaponType.Dagger,   0.12f ),
            ( TreasureWeaponType.Bow,      0.04f ),
            ( TreasureWeaponType.Crossbow, 0.04f ),
            ( TreasureWeaponType.Atlatl,   0.04f ),
            ( TreasureWeaponType.Caster,   0.04f ),
        };

        private static ChanceTable<TreasureWeaponType> T5_T6_Chances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 63%
            // missile: 27%
            // caster: 10%
            ( TreasureWeaponType.Sword,    0.09f ),
            ( TreasureWeaponType.Mace,     0.09f ),
            ( TreasureWeaponType.Axe,      0.09f ),
            ( TreasureWeaponType.Spear,    0.09f ),
            ( TreasureWeaponType.Unarmed,  0.09f ),
            ( TreasureWeaponType.Staff,    0.09f ),
            ( TreasureWeaponType.Dagger,   0.09f ),
            ( TreasureWeaponType.Bow,      0.09f ),
            ( TreasureWeaponType.Crossbow, 0.09f ),
            ( TreasureWeaponType.Atlatl,   0.09f ),
            ( TreasureWeaponType.Caster,   0.10f ),
        };

        // from magloot corpse logs
        // it appears they might have gotten rid of the tier chances
        private static ChanceTable<TreasureWeaponType> RetailChances = new ChanceTable<TreasureWeaponType>()
        {
            // melee: 63%
            // missile: 20%
            // two-handed: 10%
            // caster: 7%
            ( TreasureWeaponType.Sword,           0.09f ),
            ( TreasureWeaponType.Mace,            0.09f ),
            ( TreasureWeaponType.Axe,             0.09f ),
            ( TreasureWeaponType.Spear,           0.09f ),
            ( TreasureWeaponType.Unarmed,         0.09f ),
            ( TreasureWeaponType.Staff,           0.09f ),
            ( TreasureWeaponType.Dagger,          0.09f ),
            ( TreasureWeaponType.Bow,             0.07f ),
            ( TreasureWeaponType.Crossbow,        0.07f ),
            ( TreasureWeaponType.Atlatl,          0.06f ),
            ( TreasureWeaponType.Caster,          0.07f ),
            ( TreasureWeaponType.TwoHandedWeapon, 0.10f ),      // see TreasureWeaponType for an explanation of why this is here,
                                                                // and not deeper in WeaponWcids.cs
        };

        private static ChanceTable<TreasureWeaponType> MeleeChances = new ChanceTable<TreasureWeaponType>()
        {
            ( TreasureWeaponType.Sword,           1.25f ),
            ( TreasureWeaponType.Mace,            1.25f ),
            ( TreasureWeaponType.Axe,             1.25f ),
            ( TreasureWeaponType.Spear,           1.25f ),
            ( TreasureWeaponType.Unarmed,         1.25f ),
            ( TreasureWeaponType.Staff,           1.25f ),
            ( TreasureWeaponType.Dagger,          1.25f ),
            ( TreasureWeaponType.TwoHandedWeapon, 1.25f ),      // see TreasureWeaponType for an explanation of why this is here,
                                                                // and not deeper in WeaponWcids.cs
        };

        private static ChanceTable<TreasureWeaponType> MissileChances = new ChanceTable<TreasureWeaponType>()
        {
            ( TreasureWeaponType.Bow,             0.34f ),
            ( TreasureWeaponType.Crossbow,        0.33f ),
            ( TreasureWeaponType.Atlatl,          0.33f ),
        };

        private static readonly List<ChanceTable<TreasureWeaponType>> weaponTiers = new List<ChanceTable<TreasureWeaponType>>()
        {
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T1_T4_Chances,
            T5_T6_Chances,
            T5_T6_Chances,
        };

        static WeaponTypeChance()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                RetailChances = new ChanceTable<TreasureWeaponType>()
                {
                    // melee: 63%
                    // missile: 27%
                    // caster: 10%
                    ( TreasureWeaponType.Sword,    0.09f ),
                    ( TreasureWeaponType.Mace,     0.09f ),
                    ( TreasureWeaponType.Axe,      0.09f ),
                    ( TreasureWeaponType.Spear,    0.09f ),
                    ( TreasureWeaponType.Unarmed,  0.09f ),
                    ( TreasureWeaponType.Staff,    0.09f ),
                    ( TreasureWeaponType.Dagger,   0.09f ),
                    ( TreasureWeaponType.Bow,      0.09f ),
                    ( TreasureWeaponType.Crossbow, 0.09f ),
                    ( TreasureWeaponType.Atlatl,   0.09f ),
                    ( TreasureWeaponType.Caster,   0.10f ),
                };

                MeleeChances = new ChanceTable<TreasureWeaponType>(ChanceTableType.Weight)
                {
                    ( TreasureWeaponType.Sword,           1.0f ),
                    ( TreasureWeaponType.Mace,            1.0f ),
                    ( TreasureWeaponType.Axe,             1.0f ),
                    ( TreasureWeaponType.Spear,           1.0f ),
                    ( TreasureWeaponType.Unarmed,         1.0f ),
                    ( TreasureWeaponType.Staff,           1.0f ),
                    ( TreasureWeaponType.Dagger,          1.0f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                RetailChances = new ChanceTable<TreasureWeaponType>(ChanceTableType.Weight)
                {
                    ( TreasureWeaponType.Sword,    2.0f ),
                    ( TreasureWeaponType.Unarmed,  2.0f ),
                    ( TreasureWeaponType.Dagger,   2.0f ),

                    ( TreasureWeaponType.Mace,     1.0f ),
                    ( TreasureWeaponType.Axe,      1.0f ),
                    ( TreasureWeaponType.Spear,    1.0f ),
                    ( TreasureWeaponType.Staff,    1.0f ),

                    ( TreasureWeaponType.Atlatl,   1.0f ),
                    ( TreasureWeaponType.Bow,      1.0f ),
                    ( TreasureWeaponType.Crossbow, 1.0f ),

                    ( TreasureWeaponType.Caster,   1.5f ),
                };

                MeleeChances = new ChanceTable<TreasureWeaponType>(ChanceTableType.Weight)
                {
                    ( TreasureWeaponType.Sword,           2.0f ),
                    ( TreasureWeaponType.Unarmed,         2.0f ),
                    ( TreasureWeaponType.Dagger,          2.0f ),

                    ( TreasureWeaponType.Mace,            1.0f ),
                    ( TreasureWeaponType.Axe,             1.0f ),
                    ( TreasureWeaponType.Spear,           1.0f ),
                    ( TreasureWeaponType.Staff,           1.0f ),
                };
            }
        }

        public static TreasureWeaponType Roll(int tier, TreasureWeaponType filterToType = TreasureWeaponType.Undef)
        {
            // todo: add unique profiles for t7 / t8?
            //tier = Math.Clamp(tier, 1, 6);

            //return weaponTiers[tier - 1].Roll();

            switch(filterToType)
            {
                case TreasureWeaponType.MeleeWeapon:
                    return MeleeChances.Roll();
                case TreasureWeaponType.MissileWeapon:
                    return MissileChances.Roll();
                default:
                    return RetailChances.Roll();
            }            
        }
    }
}
