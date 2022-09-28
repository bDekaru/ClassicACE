using System.Collections.Generic;

using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    /// <summary>
    /// Defines which spells can be found on item types
    /// </summary>
    public static class SpellSelectionTable
    {
        // thanks to Sapphire Knight and Butterflygolem for helping to figure this part out!

        // gems
        private static ChanceTable<SpellId> spellSelectionGroup1 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthSelf1,            0.06f ),
            ( SpellId.EnduranceSelf1,           0.06f ),
            ( SpellId.CoordinationSelf1,        0.06f ),
            ( SpellId.QuicknessSelf1,           0.06f ),
            ( SpellId.FocusSelf1,               0.06f ),
            ( SpellId.WillpowerSelf1,           0.06f ),
            ( SpellId.RegenerationSelf1,        0.11f ),
            ( SpellId.RejuvenationSelf1,        0.11f ),
            ( SpellId.ManaRenewalSelf1,         0.11f ),
            ( SpellId.AcidProtectionSelf1,      0.03f ),
            ( SpellId.BludgeonProtectionSelf1,  0.03f ),
            ( SpellId.ColdProtectionSelf1,      0.03f ),
            ( SpellId.LightningProtectionSelf1, 0.03f ),
            ( SpellId.FireProtectionSelf1,      0.03f ),
            ( SpellId.BladeProtectionSelf1,     0.03f ),
            ( SpellId.PiercingProtectionSelf1,  0.03f ),
            ( SpellId.ArmorSelf1,               0.10f ),
        };

        // jewelry
        private static ChanceTable<SpellId> spellSelectionGroup2 = new ChanceTable<SpellId>()
        {
            ( SpellId.MagicResistanceSelf1,     0.08f ),
            ( SpellId.ArmorSelf1,               0.05f ),
            ( SpellId.AcidProtectionSelf1,      0.05f ),
            ( SpellId.BludgeonProtectionSelf1,  0.05f ),
            ( SpellId.ColdProtectionSelf1,      0.05f ),
            ( SpellId.LightningProtectionSelf1, 0.05f ),
            ( SpellId.FireProtectionSelf1,      0.05f ),
            ( SpellId.BladeProtectionSelf1,     0.05f ),
            ( SpellId.PiercingProtectionSelf1,  0.05f ),
            ( SpellId.StrengthSelf1,            0.04f ),
            ( SpellId.EnduranceSelf1,           0.04f ),
            ( SpellId.CoordinationSelf1,        0.04f ),
            ( SpellId.QuicknessSelf1,           0.04f ),
            ( SpellId.FocusSelf1,               0.04f ),
            ( SpellId.WillpowerSelf1,           0.04f ),
            ( SpellId.ManaRenewalSelf1,         0.04f ),
            ( SpellId.ManaMasterySelf1,         0.04f ),
            ( SpellId.RegenerationSelf1,        0.03f ),
            ( SpellId.RejuvenationSelf1,        0.03f ),
            ( SpellId.ItemExpertiseSelf1,       0.03f ),
            ( SpellId.ArmorExpertiseSelf1,      0.02f ),
            ( SpellId.ArcaneEnlightenmentSelf1, 0.02f ),
            ( SpellId.DeceptionMasterySelf1,    0.01f ),
            ( SpellId.FealtySelf1,              0.01f ),
            ( SpellId.MonsterAttunementSelf1,   0.01f ),
            ( SpellId.PersonAttunementSelf1,    0.01f ),
            ( SpellId.ArcanumSalvagingSelf1,    0.01f ),
            ( SpellId.MagicItemExpertiseSelf1,  0.01f ),
            ( SpellId.WeaponExpertiseSelf1,     0.01f ),
        };

        // crowns
        private static ChanceTable<SpellId> spellSelectionGroup3 = new ChanceTable<SpellId>()
        {
            ( SpellId.LeadershipMasterySelf1,   0.10f ),
            ( SpellId.ImpregnabilitySelf1,      0.10f ),
            ( SpellId.InvulnerabilitySelf1,     0.10f ),
            ( SpellId.MagicResistanceSelf1,     0.10f ),
            ( SpellId.FocusSelf1,               0.05f ),
            ( SpellId.WillpowerSelf1,           0.05f ),
            ( SpellId.ArmorSelf1,               0.05f ),
            ( SpellId.RegenerationSelf1,        0.05f ),
            ( SpellId.RejuvenationSelf1,        0.05f ),
            ( SpellId.ManaRenewalSelf1,         0.05f ),
            ( SpellId.ManaMasterySelf1,         0.05f ),
            ( SpellId.ArcaneEnlightenmentSelf1, 0.05f ),
            ( SpellId.HealingMasterySelf1,      0.05f ),
            ( SpellId.DeceptionMasterySelf1,    0.05f ),
            ( SpellId.MonsterAttunementSelf1,   0.05f ),
            ( SpellId.PersonAttunementSelf1,    0.05f ),
        };

        // orbs
        private static ChanceTable<SpellId> spellSelectionGroup4 = new ChanceTable<SpellId>()
        {
            ( SpellId.LifeMagicMasterySelf1,           0.20f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.15f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.10f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.10f ),
            ( SpellId.FocusSelf1,                      0.09f ),
            ( SpellId.WillpowerSelf1,                  0.09f ),
            ( SpellId.WarMagicMasterySelf1,            0.09f ),
            ( SpellId.SneakAttackMasterySelf1,         0.09f ),
            ( SpellId.ManaMasterySelf1,                0.09f ),
        };

        // wands, staffs, sceptres, batons
        private static ChanceTable<SpellId> spellSelectionGroup5 = new ChanceTable<SpellId>()
        {
            ( SpellId.WarMagicMasterySelf1,            0.25f ),
            ( SpellId.WillpowerSelf1,                  0.15f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.10f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.10f ),
            ( SpellId.LifeMagicMasterySelf1,           0.08f ),
            ( SpellId.FocusSelf1,                      0.08f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.08f ),
            ( SpellId.ManaMasterySelf1,                0.08f ),
            ( SpellId.SneakAttackMasterySelf1,         0.08f ),
        };

        // one-handed melee weapons
        private static ChanceTable<SpellId> spellSelectionGroup6 = new ChanceTable<SpellId>()
        {
            ( SpellId.QuicknessSelf1,            0.25f ),
            ( SpellId.StrengthSelf1,             0.15f ),
            ( SpellId.EnduranceSelf1,            0.15f ),
            ( SpellId.CoordinationSelf1,         0.15f ),
            ( SpellId.DualWieldMasterySelf1,     0.10f ),
            ( SpellId.DirtyFightingMasterySelf1, 0.10f ),
            ( SpellId.SneakAttackMasterySelf1,   0.10f ),
        };

        // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
        private static ChanceTable<SpellId> spellSelectionGroup7 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthSelf1,         0.25f ),
            ( SpellId.EnduranceSelf1,        0.25f ),
            ( SpellId.MagicResistanceSelf1,  0.15f ),
            ( SpellId.RejuvenationSelf1,     0.10f ),
            ( SpellId.RegenerationSelf1,     0.10f ),
            ( SpellId.SummoningMasterySelf1, 0.10f ),
            ( SpellId.FealtySelf1,           0.05f ),
        };

        // shields
        private static ChanceTable<SpellId> spellSelectionGroup8 = new ChanceTable<SpellId>()
        {
            ( SpellId.ImpregnabilitySelf1,  0.15f ),
            ( SpellId.InvulnerabilitySelf1, 0.15f ),
            ( SpellId.FealtySelf1,          0.15f ),
            ( SpellId.RejuvenationSelf1,    0.15f ),
            ( SpellId.StrengthSelf1,        0.10f ),
            ( SpellId.EnduranceSelf1,       0.10f ),
            ( SpellId.MagicResistanceSelf1, 0.10f ),
            ( SpellId.ShieldMasterySelf1,   0.10f ),
        };

        // gauntlets
        private static ChanceTable<SpellId> spellSelectionGroup9 = new ChanceTable<SpellId>()
        {
            ( SpellId.CoordinationSelf1,          0.22f ),
            ( SpellId.ShieldMasterySelf1,         0.12f ),
            ( SpellId.HeavyWeaponsMasterySelf1,   0.11f ),
            ( SpellId.LightWeaponsMasterySelf1,   0.11f ),
            ( SpellId.FinesseWeaponsMasterySelf1, 0.11f ),
            ( SpellId.MissileWeaponsMasterySelf1, 0.11f ),
            ( SpellId.TwoHandedMasterySelf1,      0.11f ),
            ( SpellId.HealingMasterySelf1,        0.11f ),
        };

        // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
        private static ChanceTable<SpellId> spellSelectionGroup10 = new ChanceTable<SpellId>()
        {
            ( SpellId.MagicResistanceSelf1,      0.15f ),
            ( SpellId.ImpregnabilitySelf1,       0.10f ),
            ( SpellId.InvulnerabilitySelf1,      0.10f ),
            ( SpellId.ArmorExpertiseSelf1,       0.05f ),
            ( SpellId.ItemExpertiseSelf1,        0.05f ),
            ( SpellId.WeaponExpertiseSelf1,      0.05f ),
            ( SpellId.MonsterAttunementSelf1,    0.05f ),
            ( SpellId.HealingMasterySelf1,       0.05f ),
            ( SpellId.RegenerationSelf1,         0.05f ),
            ( SpellId.RejuvenationSelf1,         0.05f ),
            ( SpellId.ManaRenewalSelf1,          0.05f ),
            ( SpellId.DualWieldMasterySelf1,     0.05f ),
            ( SpellId.DirtyFightingMasterySelf1, 0.05f ),
            ( SpellId.RecklessnessMasterySelf1,  0.05f ),
            ( SpellId.SneakAttackMasterySelf1,   0.05f ),
            ( SpellId.FealtySelf1,               0.05f ),
        };

        // boots, chiran sandals, sollerets
        private static ChanceTable<SpellId> spellSelectionGroup11 = new ChanceTable<SpellId>()
        {
            ( SpellId.QuicknessSelf1,             0.23f ),
            ( SpellId.HeavyWeaponsMasterySelf1,   0.10f ),
            ( SpellId.FinesseWeaponsMasterySelf1, 0.10f ),
            ( SpellId.MissileWeaponsMasterySelf1, 0.10f ),
            ( SpellId.HealingMasterySelf1,        0.10f ),
            ( SpellId.LightWeaponsMasterySelf1,   0.09f ),
            ( SpellId.TwoHandedMasterySelf1,      0.09f ),
            ( SpellId.CoordinationSelf1,          0.09f ),
            ( SpellId.JumpingMasterySelf1,        0.05f ),
            ( SpellId.SprintSelf1,                0.05f ),
        };

        // breeches, jerkins, shirts, pants, tunics, doublets, trousers, pantaloons
        private static ChanceTable<SpellId> spellSelectionGroup12 = new ChanceTable<SpellId>()
        {
            ( SpellId.ArmorSelf1,               0.30f ),
            ( SpellId.AcidProtectionSelf1,      0.10f ),
            ( SpellId.BludgeonProtectionSelf1,  0.10f ),
            ( SpellId.ColdProtectionSelf1,      0.10f ),
            ( SpellId.LightningProtectionSelf1, 0.10f ),
            ( SpellId.FireProtectionSelf1,      0.10f ),
            ( SpellId.BladeProtectionSelf1,     0.10f ),
            ( SpellId.PiercingProtectionSelf1,  0.10f ),
        };

        // caps, qafiyas, turbans, fezs, berets
        private static ChanceTable<SpellId> spellSelectionGroup13 = new ChanceTable<SpellId>()
        {
            ( SpellId.FocusSelf1,                      0.04f ),
            ( SpellId.WillpowerSelf1,                  0.04f ),
            ( SpellId.RejuvenationSelf1,               0.04f ),
            ( SpellId.RegenerationSelf1,               0.04f ),
            ( SpellId.ArmorSelf1,                      0.03f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.03f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.03f ),
            ( SpellId.LifeMagicMasterySelf1,           0.03f ),
            ( SpellId.WarMagicMasterySelf1,            0.03f ),
            ( SpellId.VoidMagicMasterySelf1,           0.03f ),
            ( SpellId.DualWieldMasterySelf1,           0.03f ),
            ( SpellId.DirtyFightingMasterySelf1,       0.03f ),
            ( SpellId.RecklessnessMasterySelf1,        0.03f ),
            ( SpellId.SneakAttackMasterySelf1,         0.03f ),
            ( SpellId.MagicResistanceSelf1,            0.03f ),
            ( SpellId.ManaRenewalSelf1,                0.03f ),
            ( SpellId.AlchemyMasterySelf1,             0.03f ),
            ( SpellId.CookingMasterySelf1,             0.03f ),
            ( SpellId.FletchingMasterySelf1,           0.03f ),
            ( SpellId.HealingMasterySelf1,             0.03f ),
            ( SpellId.LockpickMasterySelf1,            0.03f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.03f ),
            ( SpellId.DeceptionMasterySelf1,           0.03f ),
            ( SpellId.FealtySelf1,                     0.03f ),
            ( SpellId.ManaMasterySelf1,                0.03f ),
            ( SpellId.ArcanumSalvagingSelf1,           0.03f ),
            ( SpellId.ArmorExpertiseSelf1,             0.03f ),
            ( SpellId.MagicItemExpertiseSelf1,         0.03f ),
            ( SpellId.ItemExpertiseSelf1,              0.03f ),
            ( SpellId.WeaponExpertiseSelf1,            0.03f ),
            ( SpellId.MonsterAttunementSelf1,          0.03f ),
            ( SpellId.PersonAttunementSelf1,           0.03f ),
        };

        // cloth gloves (1 entry?)
        private static ChanceTable<SpellId> spellSelectionGroup14 = new ChanceTable<SpellId>()
        {
            ( SpellId.CoordinationSelf1,               0.04f ),
            ( SpellId.QuicknessSelf1,                  0.04f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.04f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.04f ),
            ( SpellId.LifeMagicMasterySelf1,           0.04f ),
            ( SpellId.WarMagicMasterySelf1,            0.04f ),
            ( SpellId.VoidMagicMasterySelf1,           0.04f ),
            ( SpellId.ManaMasterySelf1,                0.04f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.04f ),
            ( SpellId.ArcanumSalvagingSelf1,           0.04f ),
            ( SpellId.ArmorExpertiseSelf1,             0.04f ),
            ( SpellId.ItemExpertiseSelf1,              0.04f ),
            ( SpellId.MagicItemExpertiseSelf1,         0.04f ),
            ( SpellId.WeaponExpertiseSelf1,            0.04f ),
            ( SpellId.HeavyWeaponsMasterySelf1,        0.04f ),
            ( SpellId.LightWeaponsMasterySelf1,        0.04f ),
            ( SpellId.FinesseWeaponsMasterySelf1,      0.04f ),
            ( SpellId.MissileWeaponsMasterySelf1,      0.04f ),
            ( SpellId.TwoHandedMasterySelf1,           0.04f ),
            ( SpellId.ShieldMasterySelf1,              0.04f ),
            ( SpellId.AlchemyMasterySelf1,             0.04f ),
            ( SpellId.CookingMasterySelf1,             0.04f ),
            ( SpellId.FletchingMasterySelf1,           0.04f ),
            ( SpellId.HealingMasterySelf1,             0.04f ),
            ( SpellId.LockpickMasterySelf1,            0.04f ),
        };

        // greaves, leggings, tassets, leather pants
        private static ChanceTable<SpellId> spellSelectionGroup15 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthSelf1,         0.25f ),
            ( SpellId.QuicknessSelf1,        0.25f ),
            ( SpellId.SummoningMasterySelf1, 0.20f ),
            ( SpellId.JumpingMasterySelf1,   0.10f ),
            ( SpellId.SprintSelf1,           0.10f ),
            ( SpellId.EnduranceSelf1,        0.10f ),
        };

        // dinnerware
        private static ChanceTable<SpellId> spellSelectionGroup16 = new ChanceTable<SpellId>()
        {
            ( SpellId.AlchemyMasterySelf1,     0.09f ),
            ( SpellId.CookingMasterySelf1,     0.09f ),
            ( SpellId.FletchingMasterySelf1,   0.09f ),
            ( SpellId.LockpickMasterySelf1,    0.08f ),
            ( SpellId.ArcanumSalvagingSelf1,   0.08f ),
            ( SpellId.ArmorExpertiseSelf1,     0.08f ),
            ( SpellId.ItemExpertiseSelf1,      0.08f ),
            ( SpellId.MagicItemExpertiseSelf1, 0.08f ),
            ( SpellId.WeaponExpertiseSelf1,    0.08f ),
            ( SpellId.WillpowerSelf1,          0.05f ),
            ( SpellId.StrengthSelf1,           0.04f ),
            ( SpellId.EnduranceSelf1,          0.04f ),
            ( SpellId.CoordinationSelf1,       0.04f ),
            ( SpellId.QuicknessSelf1,          0.04f ),
            ( SpellId.FocusSelf1,              0.04f ),
        };

        // added

        // missile weapons, two-handed weapons
        private static ChanceTable<SpellId> spellSelectionGroup17 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthSelf1,             0.16f ),
            ( SpellId.EnduranceSelf1,            0.15f ),
            ( SpellId.CoordinationSelf1,         0.15f ),
            ( SpellId.QuicknessSelf1,            0.15f ),
            ( SpellId.DirtyFightingMasterySelf1, 0.13f ),
            ( SpellId.RecklessnessMasterySelf1,  0.13f ),
            ( SpellId.SneakAttackMasterySelf1,   0.13f ),
        };

        // shoes, loafers, slippers, sandals
        private static ChanceTable<SpellId> spellSelectionGroup18 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthSelf1,              0.06f ),
            ( SpellId.QuicknessSelf1,             0.06f ),
            ( SpellId.ImpregnabilitySelf1,        0.06f ),
            ( SpellId.InvulnerabilitySelf1,       0.06f ),
            ( SpellId.MagicResistanceSelf1,       0.06f ),
            ( SpellId.ArcaneEnlightenmentSelf1,   0.06f ),
            ( SpellId.ManaMasterySelf1,           0.06f ),
            ( SpellId.HealingMasterySelf1,        0.06f ),
            ( SpellId.JumpingMasterySelf1,        0.06f ),
            ( SpellId.SprintSelf1,                0.06f ),
            ( SpellId.HeavyWeaponsMasterySelf1,   0.06f ),
            ( SpellId.LightWeaponsMasterySelf1,   0.06f ),
            ( SpellId.FinesseWeaponsMasterySelf1, 0.06f ),
            ( SpellId.MissileWeaponsMasterySelf1, 0.06f ),
            ( SpellId.TwoHandedMasteryOther1,     0.06f ),
            ( SpellId.EnduranceSelf1,             0.05f ),
            ( SpellId.CoordinationSelf1,          0.05f ),
        };

        // nether caster
        private static ChanceTable<SpellId> spellSelectionGroup19 = new ChanceTable<SpellId>()
        {
            ( SpellId.VoidMagicMasterySelf1,           0.25f ),
            ( SpellId.WillpowerSelf1,                  0.15f ),
            ( SpellId.ManaMasterySelf1,                0.10f ),
            ( SpellId.LifeMagicMasterySelf1,           0.10f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.10f ),
            ( SpellId.FocusSelf1,                      0.09f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.09f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.06f ),
            ( SpellId.SneakAttackMasterySelf1,         0.06f ),
        };

        // leather cap (1 entry?)
        private static ChanceTable<SpellId> spellSelectionGroup20 = new ChanceTable<SpellId>()
        {
            ( SpellId.RecklessnessMasterySelf1,        0.075f ),
            ( SpellId.LockpickMasterySelf1,            0.075f ),
            ( SpellId.CookingMasterySelf1,             0.05f ),
            ( SpellId.FletchingMasterySelf1,           0.05f ),
            ( SpellId.ItemEnchantmentMasterySelf1,     0.05f ),
            ( SpellId.CreatureEnchantmentMasterySelf1, 0.04f ),
            ( SpellId.FealtySelf1,                     0.04f ),
            ( SpellId.ManaMasterySelf1,                0.04f ),
            ( SpellId.SneakAttackMasterySelf1,         0.04f ),
            ( SpellId.WillpowerSelf1,                  0.04f ),
            ( SpellId.ItemExpertiseSelf1,              0.03f ),
            ( SpellId.PersonAttunementSelf1,           0.03f ),
            ( SpellId.RegenerationSelf1,               0.03f ),
            ( SpellId.VoidMagicMasterySelf1,           0.03f ),
            ( SpellId.WarMagicMasterySelf1,            0.03f ),
            ( SpellId.WeaponExpertiseSelf1,            0.03f ),
            ( SpellId.AlchemyMasterySelf1,             0.025f ),
            ( SpellId.ArcaneEnlightenmentSelf1,        0.025f ),
            ( SpellId.ArcanumSalvagingSelf1,           0.025f ),
            ( SpellId.DeceptionMasterySelf1,           0.025f ),
            ( SpellId.DualWieldMasterySelf1,           0.025f ),
            ( SpellId.MonsterAttunementSelf1,          0.025f ),
            ( SpellId.ArmorExpertiseSelf1,             0.02f ),
            ( SpellId.DirtyFightingMasterySelf1,       0.02f ),
            ( SpellId.FocusSelf1,                      0.02f ),
            ( SpellId.HealingMasterySelf1,             0.02f ),
            ( SpellId.MagicItemExpertiseSelf1,         0.02f ),
            ( SpellId.MagicResistanceSelf1,            0.02f ),
            ( SpellId.ManaRenewalSelf1,                0.02f ),
            ( SpellId.RejuvenationSelf1,               0.02f ),
            ( SpellId.LifeMagicMasterySelf1,           0.01f ),
        };

        /// <summary>
        /// Key is (PropertyInt.TsysMutationData >> 24) - 1
        /// </summary>
        private static readonly List<ChanceTable<SpellId>> spellSelectionGroup = new List<ChanceTable<SpellId>>()
        {
            spellSelectionGroup1,
            spellSelectionGroup2,
            spellSelectionGroup3,
            spellSelectionGroup4,
            spellSelectionGroup5,
            spellSelectionGroup6,
            spellSelectionGroup7,
            spellSelectionGroup8,
            spellSelectionGroup9,
            spellSelectionGroup10,
            spellSelectionGroup11,
            spellSelectionGroup12,
            spellSelectionGroup13,
            spellSelectionGroup14,
            spellSelectionGroup15,
            spellSelectionGroup16,
            spellSelectionGroup17,
            spellSelectionGroup18,
            spellSelectionGroup19,
            spellSelectionGroup20,
        };

        static SpellSelectionTable()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                spellSelectionGroup1 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthSelf1,               5.0f ),
                    ( SpellId.EnduranceSelf1,              5.0f ),
                    ( SpellId.CoordinationSelf1,           5.0f ),
                    ( SpellId.QuicknessSelf1,              5.0f ),
                    ( SpellId.FocusSelf1,                  5.0f ),
                    ( SpellId.WillpowerSelf1,              5.0f ),

                    ( SpellId.ArcaneEnlightenmentSelf1,    3.0f ),

                    ( SpellId.MagicResistanceSelf1,        2.0f ),
                    ( SpellId.ImpregnabilitySelf1,         2.0f ),
                    ( SpellId.InvulnerabilitySelf1,        2.0f ),
                    ( SpellId.DeceptionMasterySelf1,       2.0f ),
                    ( SpellId.MonsterAttunementSelf1,      2.0f ),
                    ( SpellId.JumpingMasterySelf1,         2.0f ),
                    ( SpellId.SprintSelf1,                 2.0f ),
                    ( SpellId.HealingMasterySelf1,         2.0f ),
                    ( SpellId.LeadershipMasterySelf1,      2.0f ),
                    ( SpellId.FealtySelf1,                 2.0f ),

                    ( SpellId.AlchemyMasterySelf1,         1.0f ),
                    ( SpellId.CookingMasterySelf1,         1.0f ),
                    ( SpellId.FletchingMasterySelf1,       1.0f ),
                    ( SpellId.LockpickMasterySelf1,        1.0f ),
                    ( SpellId.ItemExpertiseSelf1,          1.0f ),
                    ( SpellId.ArmorExpertiseSelf1,         1.0f ),
                    ( SpellId.MagicItemExpertiseSelf1,     1.0f ),
                    ( SpellId.WeaponExpertiseSelf1,        1.0f ),
                    ( SpellId.ArcanumSalvagingSelf1,       1.0f ),

                    ( SpellId.LightWeaponsMasterySelf1,    0.5f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1,  0.5f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,            0.5f ),
                    ( SpellId.SpearMasterySelf1,           0.5f ),
                    ( SpellId.StaffMasterySelf1,           0.5f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,    0.5f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,   0.5f ),
                    ( SpellId.MissileWeaponsMasterySelf1,  0.5f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,        0.5f ),
                    ( SpellId.ThrownWeaponMasterySelf1,    0.5f ),
                    ( SpellId.ShieldMasterySelf1,          0.5f ),
                    ( SpellId.DualWieldMasterySelf1,       0.5f ),
                    ( SpellId.WarMagicMasterySelf1,        0.5f ),
                    ( SpellId.LifeMagicMasterySelf1,       0.5f ),
                };

                // jewelry
                spellSelectionGroup2 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.ArcaneEnlightenmentSelf1, 5.0f ),
                    ( SpellId.MagicResistanceSelf1,     5.0f ),
                    ( SpellId.ArmorSelf1,               5.0f ),
                    ( SpellId.AcidProtectionSelf1,      5.0f ),
                    ( SpellId.BludgeonProtectionSelf1,  5.0f ),
                    ( SpellId.ColdProtectionSelf1,      5.0f ),
                    ( SpellId.LightningProtectionSelf1, 5.0f ),
                    ( SpellId.FireProtectionSelf1,      5.0f ),
                    ( SpellId.BladeProtectionSelf1,     5.0f ),
                    ( SpellId.PiercingProtectionSelf1,  5.0f ),
                    ( SpellId.StrengthSelf1,            4.0f ),
                    ( SpellId.EnduranceSelf1,           4.0f ),
                    ( SpellId.CoordinationSelf1,        4.0f ),
                    ( SpellId.QuicknessSelf1,           4.0f ),
                    ( SpellId.FocusSelf1,               4.0f ),
                    ( SpellId.WillpowerSelf1,           4.0f ),
                    ( SpellId.ManaRenewalSelf1,         4.0f ),
                    ( SpellId.ManaMasterySelf1,         4.0f ),
                    ( SpellId.RegenerationSelf1,        3.0f ),
                    ( SpellId.RejuvenationSelf1,        3.0f ),
                    ( SpellId.ItemExpertiseSelf1,       3.0f ),
                    ( SpellId.ArmorExpertiseSelf1,      2.0f ),
                    ( SpellId.DeceptionMasterySelf1,    1.0f ),
                    ( SpellId.FealtySelf1,              1.0f ),
                    ( SpellId.MonsterAttunementSelf1,   1.0f ),
                    ( SpellId.ArcanumSalvagingSelf1,    1.0f ),
                    ( SpellId.MagicItemExpertiseSelf1,  1.0f ),
                    ( SpellId.WeaponExpertiseSelf1,     1.0f ),
                };

                // crowns
                spellSelectionGroup3 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LeadershipMasterySelf1,    2.0f),
                    ( SpellId.ImpregnabilitySelf1,       2.0f),
                    ( SpellId.InvulnerabilitySelf1,      2.0f),
                    ( SpellId.MagicResistanceSelf1,      2.0f),
                    ( SpellId.FocusSelf1,                1.0f),
                    ( SpellId.WillpowerSelf1,            1.0f),
                    ( SpellId.ArmorSelf1,                1.0f),
                    ( SpellId.RegenerationSelf1,         1.0f),
                    ( SpellId.RejuvenationSelf1,         1.0f),
                    ( SpellId.ManaRenewalSelf1,          1.0f),
                    ( SpellId.ManaMasterySelf1,          1.0f),
                    ( SpellId.ArcaneEnlightenmentSelf1,  1.0f),
                    ( SpellId.HealingMasterySelf1,       1.0f),
                    ( SpellId.DeceptionMasterySelf1,     1.0f),
                    ( SpellId.MonsterAttunementSelf1,    1.0f),
                };

                // orbs
                spellSelectionGroup4 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LifeMagicMasterySelf1,           2.0f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        1.5f ),
                    ( SpellId.FocusSelf1,                      1.0f ),
                    ( SpellId.WillpowerSelf1,                  1.0f ),
                    ( SpellId.WarMagicMasterySelf1,            1.0f ),
                    ( SpellId.ManaMasterySelf1,                1.0f ),
                };

                // wands, staffs, sceptres, batons
                spellSelectionGroup5 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.WarMagicMasterySelf1,            2.0f ),
                    ( SpellId.WillpowerSelf1,                  1.5f ),
                    ( SpellId.LifeMagicMasterySelf1,           1.0f ),
                    ( SpellId.FocusSelf1,                      1.0f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        1.0f ),
                    ( SpellId.ManaMasterySelf1,                1.0f ),
                };

                // one-handed melee weapons
                spellSelectionGroup6 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessSelf1,            0.225f ),
                    ( SpellId.StrengthSelf1,             0.225f ),
                    ( SpellId.EnduranceSelf1,            0.225f ),
                    ( SpellId.CoordinationSelf1,         0.225f ),
                    ( SpellId.DualWieldMasterySelf1,     0.100f ),
                };

                // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
                spellSelectionGroup7 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,         0.30f ),
                    ( SpellId.EnduranceSelf1,        0.30f ),

                    ( SpellId.MagicResistanceSelf1,  0.15f ),

                    ( SpellId.RejuvenationSelf1,     0.10f ),
                    ( SpellId.RegenerationSelf1,     0.10f ),

                    ( SpellId.FealtySelf1,           0.05f ),
                };

                // shields
                spellSelectionGroup8 = new ChanceTable<SpellId>()
                {
                    ( SpellId.ImpregnabilitySelf1,  0.15f ),
                    ( SpellId.InvulnerabilitySelf1, 0.15f ),

                    ( SpellId.StrengthSelf1,        0.10f ),
                    ( SpellId.EnduranceSelf1,       0.10f ),

                    ( SpellId.RejuvenationSelf1,    0.15f ),

                    ( SpellId.FealtySelf1,          0.15f ),
                    ( SpellId.MagicResistanceSelf1, 0.10f ),

                    ( SpellId.ShieldMasterySelf1,   0.10f ),
                };

                // gauntlets
                spellSelectionGroup9 = new ChanceTable<SpellId>()
                {
                    ( SpellId.CoordinationSelf1,          0.13f ),

                    ( SpellId.HealingMasterySelf1,        0.10f ),

                    ( SpellId.LightWeaponsMasterySelf1,   0.07f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1, 0.07f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,           0.07f ),
                    ( SpellId.SpearMasterySelf1,          0.07f ),
                    ( SpellId.StaffMasterySelf1,          0.07f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,   0.07f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,  0.07f ),
                    ( SpellId.MissileWeaponsMasterySelf1, 0.07f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,       0.07f ),
                    ( SpellId.ThrownWeaponMasterySelf1,   0.07f ),
                    ( SpellId.ShieldMasterySelf1,         0.07f ),
                };

                // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
                spellSelectionGroup10 = new ChanceTable<SpellId>()
                {
                    ( SpellId.MagicResistanceSelf1,      0.16f ),
                    ( SpellId.ImpregnabilitySelf1,       0.12f ),
                    ( SpellId.InvulnerabilitySelf1,      0.12f ),

                    ( SpellId.ArmorExpertiseSelf1,       0.06f ),
                    ( SpellId.ItemExpertiseSelf1,        0.06f ),
                    ( SpellId.WeaponExpertiseSelf1,      0.06f ),
                    ( SpellId.MonsterAttunementSelf1,    0.06f ),
                    ( SpellId.HealingMasterySelf1,       0.06f ),
                    ( SpellId.RegenerationSelf1,         0.06f ),
                    ( SpellId.RejuvenationSelf1,         0.06f ),
                    ( SpellId.ManaRenewalSelf1,          0.06f ),
                    ( SpellId.FealtySelf1,               0.06f ),

                    ( SpellId.DualWieldMasterySelf1,     0.06f ),
                };

                // boots, chiran sandals, sollerets
                spellSelectionGroup11 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessSelf1,             0.20f ),
                    ( SpellId.CoordinationSelf1,          0.10f ),

                    ( SpellId.HealingMasterySelf1,        0.10f ),

                    ( SpellId.JumpingMasterySelf1,        0.05f ),
                    ( SpellId.SprintSelf1,                0.05f ),

                    ( SpellId.LightWeaponsMasterySelf1,   0.05f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1, 0.05f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,           0.05f ),
                    ( SpellId.SpearMasterySelf1,          0.05f ),
                    ( SpellId.StaffMasterySelf1,          0.05f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,   0.05f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,  0.05f ),
                    ( SpellId.MissileWeaponsMasterySelf1, 0.05f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,       0.05f ),
                    ( SpellId.ThrownWeaponMasterySelf1,   0.05f ),
                };

                // caps, qafiyas, turbans, fezs, berets
                spellSelectionGroup13 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.FocusSelf1,                      1.0f ),
                    ( SpellId.WillpowerSelf1,                  1.0f ),
                    ( SpellId.RejuvenationSelf1,               1.0f ),
                    ( SpellId.RegenerationSelf1,               1.0f ),

                    ( SpellId.ArmorSelf1,                      0.9f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.9f ),
                    ( SpellId.WarMagicMasterySelf1,            0.9f ),
                    ( SpellId.MagicResistanceSelf1,            0.9f ),
                    ( SpellId.ManaRenewalSelf1,                0.9f ),
                    ( SpellId.HealingMasterySelf1,             0.9f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.9f ),
                    ( SpellId.FealtySelf1,                     0.9f ),
                    ( SpellId.ManaMasterySelf1,                0.9f ),

                    ( SpellId.AlchemyMasterySelf1,             0.8f ),
                    ( SpellId.CookingMasterySelf1,             0.8f ),
                    ( SpellId.FletchingMasterySelf1,           0.8f ),
                    ( SpellId.LockpickMasterySelf1,            0.8f ),
                    ( SpellId.DeceptionMasterySelf1,           0.8f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.8f ),
                    ( SpellId.ArmorExpertiseSelf1,             0.8f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.8f ),
                    ( SpellId.ItemExpertiseSelf1,              0.8f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.8f ),
                    ( SpellId.MonsterAttunementSelf1,          0.8f ),
                };

                // cloth gloves (1 entry?)
                spellSelectionGroup14 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CoordinationSelf1,               1.0f ),
                    ( SpellId.QuicknessSelf1,                  1.0f ),

                    ( SpellId.AlchemyMasterySelf1,             0.9f ),
                    ( SpellId.CookingMasterySelf1,             0.9f ),
                    ( SpellId.FletchingMasterySelf1,           0.9f ),
                    ( SpellId.HealingMasterySelf1,             0.9f ),
                    ( SpellId.LockpickMasterySelf1,            0.9f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.9f ),
                    ( SpellId.WarMagicMasterySelf1,            0.9f ),
                    ( SpellId.ManaMasterySelf1,                0.9f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.9f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.9f ),

                    ( SpellId.ArmorExpertiseSelf1,             0.8f ),
                    ( SpellId.ItemExpertiseSelf1,              0.8f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.8f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.8f ),
                };

                // greaves, leggings, tassets, leather pants
                spellSelectionGroup15 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,         0.29f ),
                    ( SpellId.QuicknessSelf1,        0.29f ),
                    ( SpellId.JumpingMasterySelf1,   0.14f ),
                    ( SpellId.SprintSelf1,           0.14f ),
                    ( SpellId.EnduranceSelf1,        0.14f ),
                };

                // missile weapons, two-handed weapons
                spellSelectionGroup17 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,             0.25f ),
                    ( SpellId.EnduranceSelf1,            0.25f ),
                    ( SpellId.CoordinationSelf1,         0.25f ),
                    ( SpellId.QuicknessSelf1,            0.25f ),
                };

                // shoes, loafers, slippers, sandals
                spellSelectionGroup18 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthSelf1,              1.0f ),
                    ( SpellId.QuicknessSelf1,             1.0f ),
                    ( SpellId.EnduranceSelf1,             1.0f ),
                    ( SpellId.CoordinationSelf1,          1.0f ),

                    ( SpellId.ImpregnabilitySelf1,        0.5f ),
                    ( SpellId.InvulnerabilitySelf1,       0.5f ),
                    ( SpellId.MagicResistanceSelf1,       0.5f ),

                    ( SpellId.ArcaneEnlightenmentSelf1,   0.5f ),
                    ( SpellId.ManaMasterySelf1,           0.5f ),
                    ( SpellId.HealingMasterySelf1,        0.5f ),

                    ( SpellId.JumpingMasterySelf1,        0.5f ),
                    ( SpellId.SprintSelf1,                0.5f ),
                };

                // nether caster - should never be used with Infiltration data but here for completioness.
                spellSelectionGroup19 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.WarMagicMasterySelf1,            1.0f ),

                    ( SpellId.WillpowerSelf1,                  0.8f ),

                    ( SpellId.ManaMasterySelf1,                0.4f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.4f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.4f ),
                    ( SpellId.FocusSelf1,                      0.4f ),
                };

                // leather cap (1 entry?)
                spellSelectionGroup20 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LockpickMasterySelf1,            1.0f ),

                    ( SpellId.CookingMasterySelf1,             0.8f ),
                    ( SpellId.FletchingMasterySelf1,           0.8f ),
                    ( SpellId.AlchemyMasterySelf1,             0.8f ),

                    ( SpellId.ItemExpertiseSelf1,              0.7f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.7f ),
                    ( SpellId.ArmorExpertiseSelf1,             0.7f ),

                    ( SpellId.WarMagicMasterySelf1,            0.6f ),
                    ( SpellId.FealtySelf1,                     0.6f ),
                    ( SpellId.ManaMasterySelf1,                0.6f ),

                    ( SpellId.WillpowerSelf1,                  0.5f ),
                    ( SpellId.FocusSelf1,                      0.5f ),
                    ( SpellId.RegenerationSelf1,               0.5f ),

                    ( SpellId.ArcaneEnlightenmentSelf1,        0.4f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.4f ),
                    ( SpellId.DeceptionMasterySelf1,           0.4f ),
                    ( SpellId.MonsterAttunementSelf1,          0.4f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.4f ),
                    ( SpellId.HealingMasterySelf1,             0.4f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.4f ),
                    ( SpellId.MagicResistanceSelf1,            0.4f ),
                    ( SpellId.ManaRenewalSelf1,                0.4f ),
                    ( SpellId.RejuvenationSelf1,               0.4f ),
                };

                spellSelectionGroup = new List<ChanceTable<SpellId>>()
                {
                    spellSelectionGroup1,
                    spellSelectionGroup2,
                    spellSelectionGroup3,
                    spellSelectionGroup4,
                    spellSelectionGroup5,
                    spellSelectionGroup6,
                    spellSelectionGroup7,
                    spellSelectionGroup8,
                    spellSelectionGroup9,
                    spellSelectionGroup10,
                    spellSelectionGroup11,
                    spellSelectionGroup12,
                    spellSelectionGroup13,
                    spellSelectionGroup14,
                    spellSelectionGroup15,
                    spellSelectionGroup16,
                    spellSelectionGroup17,
                    spellSelectionGroup18,
                    spellSelectionGroup19,
                    spellSelectionGroup20,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                // orbs
                spellSelectionGroup4 = new ChanceTable<SpellId>()
                {
                    ( SpellId.LifeMagicMasterySelf1,           0.20f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.16f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.12f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.12f ),
                    ( SpellId.FocusSelf1,                      0.10f ),
                    ( SpellId.WillpowerSelf1,                  0.10f ),
                    ( SpellId.WarMagicMasterySelf1,            0.10f ),
                    ( SpellId.ManaMasterySelf1,                0.10f ),
                };

                // wands, staffs, sceptres, batons
                spellSelectionGroup5 = new ChanceTable<SpellId>()
                {
                    ( SpellId.WarMagicMasterySelf1,            0.26f ),
                    ( SpellId.WillpowerSelf1,                  0.16f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.11f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.11f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.09f ),
                    ( SpellId.FocusSelf1,                      0.09f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.09f ),
                    ( SpellId.ManaMasterySelf1,                0.09f ),
                };

                // one-handed melee weapons
                spellSelectionGroup6 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessSelf1,            0.325f ),
                    ( SpellId.StrengthSelf1,             0.225f ),
                    ( SpellId.EnduranceSelf1,            0.225f ),
                    ( SpellId.CoordinationSelf1,         0.225f ),
                };

                // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
                spellSelectionGroup7 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,         0.30f ),
                    ( SpellId.EnduranceSelf1,        0.30f ),
                    ( SpellId.MagicResistanceSelf1,  0.15f ),
                    ( SpellId.RejuvenationSelf1,     0.10f ),
                    ( SpellId.RegenerationSelf1,     0.10f ),
                    ( SpellId.FealtySelf1,           0.05f ),
                };

                // shields
                spellSelectionGroup8 = new ChanceTable<SpellId>()
                {
                    ( SpellId.ImpregnabilitySelf1,  0.17f ),
                    ( SpellId.InvulnerabilitySelf1, 0.17f ),
                    ( SpellId.FealtySelf1,          0.17f ),
                    ( SpellId.RejuvenationSelf1,    0.16f ),
                    ( SpellId.StrengthSelf1,        0.11f ),
                    ( SpellId.EnduranceSelf1,       0.11f ),
                    ( SpellId.MagicResistanceSelf1, 0.11f ),
                };

                // gauntlets
                spellSelectionGroup9 = new ChanceTable<SpellId>()
                {
                    ( SpellId.CoordinationSelf1,          0.20f ),
                    ( SpellId.HealingMasterySelf1,        0.10f ),
                    ( SpellId.LightWeaponsMasterySelf1,   0.07f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1, 0.07f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,           0.07f ),
                    ( SpellId.SpearMasterySelf1,          0.07f ),
                    ( SpellId.StaffMasterySelf1,          0.07f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,   0.07f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,  0.07f ),
                    ( SpellId.MissileWeaponsMasterySelf1, 0.07f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,       0.07f ),
                    ( SpellId.ThrownWeaponMasterySelf1,   0.07f ),
                };

                // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
                spellSelectionGroup10 = new ChanceTable<SpellId>()
                {
                    ( SpellId.MagicResistanceSelf1,      0.15f ),
                    ( SpellId.ImpregnabilitySelf1,       0.11f ),
                    ( SpellId.InvulnerabilitySelf1,      0.11f ),

                    ( SpellId.ArmorExpertiseSelf1,       0.07f ),
                    ( SpellId.ItemExpertiseSelf1,        0.07f ),
                    ( SpellId.WeaponExpertiseSelf1,      0.07f ),
                    ( SpellId.MonsterAttunementSelf1,    0.07f ),
                    ( SpellId.HealingMasterySelf1,       0.07f ),
                    ( SpellId.RegenerationSelf1,         0.07f ),
                    ( SpellId.RejuvenationSelf1,         0.07f ),
                    ( SpellId.ManaRenewalSelf1,          0.07f ),
                    ( SpellId.FealtySelf1,               0.07f ),
                };

                // boots, chiran sandals, sollerets
                spellSelectionGroup11 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessSelf1,             0.20f ),
                    ( SpellId.HealingMasterySelf1,        0.10f ),
                    ( SpellId.CoordinationSelf1,          0.10f ),
                    ( SpellId.JumpingMasterySelf1,        0.05f ),
                    ( SpellId.SprintSelf1,                0.05f ),
                    ( SpellId.LightWeaponsMasterySelf1,   0.05f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1, 0.05f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,           0.05f ),
                    ( SpellId.SpearMasterySelf1,          0.05f ),
                    ( SpellId.StaffMasterySelf1,          0.05f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,   0.05f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,  0.05f ),
                    ( SpellId.MissileWeaponsMasterySelf1, 0.05f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,       0.05f ),
                    ( SpellId.ThrownWeaponMasterySelf1,   0.05f ),
                };

                // caps, qafiyas, turbans, fezs, berets
                spellSelectionGroup13 = new ChanceTable<SpellId>()
                {
                    ( SpellId.FocusSelf1,                      0.05f ),
                    ( SpellId.WillpowerSelf1,                  0.05f ),
                    ( SpellId.RejuvenationSelf1,               0.05f ),
                    ( SpellId.RegenerationSelf1,               0.05f ),
                    ( SpellId.ArmorSelf1,                      0.04f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.04f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.04f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.04f ),
                    ( SpellId.WarMagicMasterySelf1,            0.04f ),
                    ( SpellId.MagicResistanceSelf1,            0.04f ),
                    ( SpellId.ManaRenewalSelf1,                0.04f ),
                    ( SpellId.HealingMasterySelf1,             0.04f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.04f ),
                    ( SpellId.FealtySelf1,                     0.04f ),
                    ( SpellId.ManaMasterySelf1,                0.04f ),
                    ( SpellId.AlchemyMasterySelf1,             0.03f ),
                    ( SpellId.CookingMasterySelf1,             0.03f ),
                    ( SpellId.FletchingMasterySelf1,           0.03f ),
                    ( SpellId.LockpickMasterySelf1,            0.03f ),
                    ( SpellId.DeceptionMasterySelf1,           0.03f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.03f ),
                    ( SpellId.ArmorExpertiseSelf1,             0.03f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.03f ),
                    ( SpellId.ItemExpertiseSelf1,              0.03f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.03f ),
                    ( SpellId.MonsterAttunementSelf1,          0.03f ),
                    ( SpellId.PersonAttunementSelf1,           0.03f ),
                };

                // cloth gloves (1 entry?)
                spellSelectionGroup14 = new ChanceTable<SpellId>()
                {
                    ( SpellId.CoordinationSelf1,               0.05f ),
                    ( SpellId.QuicknessSelf1,                  0.05f ),
                    ( SpellId.AlchemyMasterySelf1,             0.04f ),
                    ( SpellId.CookingMasterySelf1,             0.04f ),
                    ( SpellId.FletchingMasterySelf1,           0.04f ),
                    ( SpellId.HealingMasterySelf1,             0.04f ),
                    ( SpellId.LockpickMasterySelf1,            0.04f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.04f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.04f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.04f ),
                    ( SpellId.WarMagicMasterySelf1,            0.04f ),
                    ( SpellId.ManaMasterySelf1,                0.04f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.04f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.04f ),
                    ( SpellId.ArmorExpertiseSelf1,             0.03f ),
                    ( SpellId.ItemExpertiseSelf1,              0.03f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.03f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.03f ),
                    ( SpellId.LightWeaponsMasterySelf1,        0.03f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1,      0.03f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,                0.03f ),
                    ( SpellId.SpearMasterySelf1,               0.03f ),
                    ( SpellId.StaffMasterySelf1,               0.03f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,        0.03f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,       0.03f ),
                    ( SpellId.MissileWeaponsMasterySelf1,      0.03f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,            0.03f ),
                    ( SpellId.ThrownWeaponMasterySelf1,        0.03f ),
                };

                // greaves, leggings, tassets, leather pants
                spellSelectionGroup15 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,         0.29f ),
                    ( SpellId.QuicknessSelf1,        0.29f ),
                    ( SpellId.JumpingMasterySelf1,   0.14f ),
                    ( SpellId.SprintSelf1,           0.14f ),
                    ( SpellId.EnduranceSelf1,        0.14f ),
                };

                // missile weapons, two-handed weapons
                spellSelectionGroup17 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,             0.25f ),
                    ( SpellId.EnduranceSelf1,            0.25f ),
                    ( SpellId.CoordinationSelf1,         0.25f ),
                    ( SpellId.QuicknessSelf1,            0.25f ),
                };

                // shoes, loafers, slippers, sandals
                spellSelectionGroup18 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthSelf1,              0.06f ),
                    ( SpellId.QuicknessSelf1,             0.06f ),
                    ( SpellId.EnduranceSelf1,             0.06f ),
                    ( SpellId.CoordinationSelf1,          0.06f ),
                    ( SpellId.ImpregnabilitySelf1,        0.05f ),
                    ( SpellId.InvulnerabilitySelf1,       0.05f ),
                    ( SpellId.MagicResistanceSelf1,       0.05f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,   0.05f ),
                    ( SpellId.ManaMasterySelf1,           0.04f ),
                    ( SpellId.HealingMasterySelf1,        0.04f ),
                    ( SpellId.JumpingMasterySelf1,        0.04f ),
                    ( SpellId.SprintSelf1,                0.04f ),
                    ( SpellId.LightWeaponsMasterySelf1,   0.04f ), // AxeMasterySelf1
                    ( SpellId.FinesseWeaponsMasterySelf1, 0.04f ), // DaggerMasterySelf1
                    ( SpellId.MaceMasterySelf1,           0.04f ),
                    ( SpellId.SpearMasterySelf1,          0.04f ),
                    ( SpellId.StaffMasterySelf1,          0.04f ),
                    ( SpellId.HeavyWeaponsMasterySelf1,   0.04f ), // SwordMasterySelf1
                    ( SpellId.UnarmedCombatMasterySelf1,  0.04f ),
                    ( SpellId.MissileWeaponsMasterySelf1, 0.04f ), // BowMasterySelf1
                    ( SpellId.CrossbowMasterySelf1,       0.04f ),
                    ( SpellId.ThrownWeaponMasterySelf1,   0.04f ),
                };

                // nether caster - should never be used with Infiltration data but here for completioness.
                spellSelectionGroup19 = new ChanceTable<SpellId>()
                {
                    ( SpellId.WarMagicMasterySelf1,            0.28f ),
                    ( SpellId.WillpowerSelf1,                  0.18f ),
                    ( SpellId.ManaMasterySelf1,                0.10f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.10f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.10f ),
                    ( SpellId.FocusSelf1,                      0.09f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.09f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.06f ),
                };

                // leather cap (1 entry?)
                spellSelectionGroup20 = new ChanceTable<SpellId>()
                {
                    ( SpellId.LockpickMasterySelf1,            0.08f ),
                    ( SpellId.CookingMasterySelf1,             0.06f ),
                    ( SpellId.FletchingMasterySelf1,           0.06f ),
                    ( SpellId.AlchemyMasterySelf1,             0.06f ),
                    ( SpellId.ItemEnchantmentMasterySelf1,     0.06f ),
                    ( SpellId.CreatureEnchantmentMasterySelf1, 0.05f ),
                    ( SpellId.ItemExpertiseSelf1,              0.05f ),
                    ( SpellId.PersonAttunementSelf1,           0.05f ),
                    ( SpellId.WeaponExpertiseSelf1,            0.05f ),
                    ( SpellId.ArmorExpertiseSelf1,             0.05f ),
                    ( SpellId.WarMagicMasterySelf1,            0.04f ),
                    ( SpellId.FealtySelf1,                     0.04f ),
                    ( SpellId.ManaMasterySelf1,                0.04f ),
                    ( SpellId.WillpowerSelf1,                  0.03f ),
                    ( SpellId.FocusSelf1,                      0.03f ),
                    ( SpellId.RegenerationSelf1,               0.03f ),
                    ( SpellId.ArcaneEnlightenmentSelf1,        0.025f ),
                    ( SpellId.ArcanumSalvagingSelf1,           0.025f ),
                    ( SpellId.DeceptionMasterySelf1,           0.025f ),
                    ( SpellId.MonsterAttunementSelf1,          0.025f ),
                    ( SpellId.LifeMagicMasterySelf1,           0.02f ),                
                    ( SpellId.HealingMasterySelf1,             0.02f ),
                    ( SpellId.MagicItemExpertiseSelf1,         0.02f ),
                    ( SpellId.MagicResistanceSelf1,            0.02f ),
                    ( SpellId.ManaRenewalSelf1,                0.02f ),
                    ( SpellId.RejuvenationSelf1,               0.02f ),
                };

                spellSelectionGroup = new List<ChanceTable<SpellId>>()
                {
                    spellSelectionGroup1,
                    spellSelectionGroup2,
                    spellSelectionGroup3,
                    spellSelectionGroup4,
                    spellSelectionGroup5,
                    spellSelectionGroup6,
                    spellSelectionGroup7,
                    spellSelectionGroup8,
                    spellSelectionGroup9,
                    spellSelectionGroup10,
                    spellSelectionGroup11,
                    spellSelectionGroup12,
                    spellSelectionGroup13,
                    spellSelectionGroup14,
                    spellSelectionGroup15,
                    spellSelectionGroup16,
                    spellSelectionGroup17,
                    spellSelectionGroup18,
                    spellSelectionGroup19,
                    spellSelectionGroup20,
                };
            }
        }

        /// <summary>
        /// Rolls for a creature / life spell for an item
        /// </summary>
        /// <param name="spellCode">the SpellCode from WorldObject</param>
        public static SpellId Roll(int spellCode)
        {
            return spellSelectionGroup[spellCode - 1].Roll();
        }
    }
}
