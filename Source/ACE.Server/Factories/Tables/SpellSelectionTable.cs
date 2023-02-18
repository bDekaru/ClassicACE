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
            ( SpellId.StrengthOther1,            0.06f ),
            ( SpellId.EnduranceOther1,           0.06f ),
            ( SpellId.CoordinationOther1,        0.06f ),
            ( SpellId.QuicknessOther1,           0.06f ),
            ( SpellId.FocusOther1,               0.06f ),
            ( SpellId.WillpowerOther1,           0.06f ),
            ( SpellId.RegenerationOther1,        0.11f ),
            ( SpellId.RejuvenationOther1,        0.11f ),
            ( SpellId.ManaRenewalOther1,         0.11f ),
            ( SpellId.AcidProtectionOther1,      0.03f ),
            ( SpellId.BludgeonProtectionOther1,  0.03f ),
            ( SpellId.ColdProtectionOther1,      0.03f ),
            ( SpellId.LightningProtectionOther1, 0.03f ),
            ( SpellId.FireProtectionOther1,      0.03f ),
            ( SpellId.BladeProtectionOther1,     0.03f ),
            ( SpellId.PiercingProtectionOther1,  0.03f ),
            ( SpellId.ArmorOther1,               0.10f ),
        };

        // jewelry
        private static ChanceTable<SpellId> spellSelectionGroup2 = new ChanceTable<SpellId>()
        {
            ( SpellId.MagicResistanceOther1,     0.08f ),
            ( SpellId.ArmorOther1,               0.05f ),
            ( SpellId.AcidProtectionOther1,      0.05f ),
            ( SpellId.BludgeonProtectionOther1,  0.05f ),
            ( SpellId.ColdProtectionOther1,      0.05f ),
            ( SpellId.LightningProtectionOther1, 0.05f ),
            ( SpellId.FireProtectionOther1,      0.05f ),
            ( SpellId.BladeProtectionOther1,     0.05f ),
            ( SpellId.PiercingProtectionOther1,  0.05f ),
            ( SpellId.StrengthOther1,            0.04f ),
            ( SpellId.EnduranceOther1,           0.04f ),
            ( SpellId.CoordinationOther1,        0.04f ),
            ( SpellId.QuicknessOther1,           0.04f ),
            ( SpellId.FocusOther1,               0.04f ),
            ( SpellId.WillpowerOther1,           0.04f ),
            ( SpellId.ManaRenewalOther1,         0.04f ),
            ( SpellId.ManaMasteryOther1,         0.04f ),
            ( SpellId.RegenerationOther1,        0.03f ),
            ( SpellId.RejuvenationOther1,        0.03f ),
            ( SpellId.ItemExpertiseOther1,       0.03f ),
            ( SpellId.ArmorExpertiseOther1,      0.02f ),
            ( SpellId.ArcaneEnlightenmentOther1, 0.02f ),
            ( SpellId.DeceptionMasteryOther1,    0.01f ),
            ( SpellId.FealtyOther1,              0.01f ),
            ( SpellId.MonsterAttunementOther1,   0.01f ),
            ( SpellId.PersonAttunementOther1,    0.01f ),
            ( SpellId.ArcanumSalvagingOther1,    0.01f ),
            ( SpellId.MagicItemExpertiseOther1,  0.01f ),
            ( SpellId.WeaponExpertiseOther1,     0.01f ),
        };

        // crowns
        private static ChanceTable<SpellId> spellSelectionGroup3 = new ChanceTable<SpellId>()
        {
            ( SpellId.LeadershipMasteryOther1,   0.10f ),
            ( SpellId.ImpregnabilityOther1,      0.10f ),
            ( SpellId.InvulnerabilityOther1,     0.10f ),
            ( SpellId.MagicResistanceOther1,     0.10f ),
            ( SpellId.FocusOther1,               0.05f ),
            ( SpellId.WillpowerOther1,           0.05f ),
            ( SpellId.ArmorOther1,               0.05f ),
            ( SpellId.RegenerationOther1,        0.05f ),
            ( SpellId.RejuvenationOther1,        0.05f ),
            ( SpellId.ManaRenewalOther1,         0.05f ),
            ( SpellId.ManaMasteryOther1,         0.05f ),
            ( SpellId.ArcaneEnlightenmentOther1, 0.05f ),
            ( SpellId.HealingMasteryOther1,      0.05f ),
            ( SpellId.DeceptionMasteryOther1,    0.05f ),
            ( SpellId.MonsterAttunementOther1,   0.05f ),
            ( SpellId.PersonAttunementOther1,    0.05f ),
        };

        // orbs
        private static ChanceTable<SpellId> spellSelectionGroup4 = new ChanceTable<SpellId>()
        {
            ( SpellId.LifeMagicMasteryOther1,           0.20f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.15f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.10f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.10f ),
            ( SpellId.FocusOther1,                      0.09f ),
            ( SpellId.WillpowerOther1,                  0.09f ),
            ( SpellId.WarMagicMasteryOther1,            0.09f ),
            ( SpellId.SneakAttackMasteryOther1,         0.09f ),
            ( SpellId.ManaMasteryOther1,                0.09f ),
        };

        // wands, staffs, sceptres, batons
        private static ChanceTable<SpellId> spellSelectionGroup5 = new ChanceTable<SpellId>()
        {
            ( SpellId.WarMagicMasteryOther1,            0.25f ),
            ( SpellId.WillpowerOther1,                  0.15f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.10f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.10f ),
            ( SpellId.LifeMagicMasteryOther1,           0.08f ),
            ( SpellId.FocusOther1,                      0.08f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.08f ),
            ( SpellId.ManaMasteryOther1,                0.08f ),
            ( SpellId.SneakAttackMasteryOther1,         0.08f ),
        };

        // one-handed melee weapons
        private static ChanceTable<SpellId> spellSelectionGroup6 = new ChanceTable<SpellId>()
        {
            ( SpellId.QuicknessOther1,            0.25f ),
            ( SpellId.StrengthOther1,             0.15f ),
            ( SpellId.EnduranceOther1,            0.15f ),
            ( SpellId.CoordinationOther1,         0.15f ),
            ( SpellId.DualWieldMasteryOther1,     0.10f ),
            ( SpellId.DirtyFightingMasteryOther1, 0.10f ),
            ( SpellId.SneakAttackMasteryOther1,   0.10f ),
        };

        // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
        private static ChanceTable<SpellId> spellSelectionGroup7 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthOther1,         0.25f ),
            ( SpellId.EnduranceOther1,        0.25f ),
            ( SpellId.MagicResistanceOther1,  0.15f ),
            ( SpellId.RejuvenationOther1,     0.10f ),
            ( SpellId.RegenerationOther1,     0.10f ),
            ( SpellId.SummoningMasteryOther1, 0.10f ),
            ( SpellId.FealtyOther1,           0.05f ),
        };

        // shields
        private static ChanceTable<SpellId> spellSelectionGroup8 = new ChanceTable<SpellId>()
        {
            ( SpellId.ImpregnabilityOther1,  0.15f ),
            ( SpellId.InvulnerabilityOther1, 0.15f ),
            ( SpellId.FealtyOther1,          0.15f ),
            ( SpellId.RejuvenationOther1,    0.15f ),
            ( SpellId.StrengthOther1,        0.10f ),
            ( SpellId.EnduranceOther1,       0.10f ),
            ( SpellId.MagicResistanceOther1, 0.10f ),
            ( SpellId.ShieldMasteryOther1,   0.10f ),
        };

        // gauntlets
        private static ChanceTable<SpellId> spellSelectionGroup9 = new ChanceTable<SpellId>()
        {
            ( SpellId.CoordinationOther1,          0.22f ),
            ( SpellId.ShieldMasteryOther1,         0.12f ),
            ( SpellId.HeavyWeaponsMasteryOther1,   0.11f ),
            ( SpellId.LightWeaponsMasteryOther1,   0.11f ),
            ( SpellId.FinesseWeaponsMasteryOther1, 0.11f ),
            ( SpellId.MissileWeaponsMasteryOther1, 0.11f ),
            ( SpellId.TwoHandedMasteryOther1,      0.11f ),
            ( SpellId.HealingMasteryOther1,        0.11f ),
        };

        // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
        private static ChanceTable<SpellId> spellSelectionGroup10 = new ChanceTable<SpellId>()
        {
            ( SpellId.MagicResistanceOther1,      0.15f ),
            ( SpellId.ImpregnabilityOther1,       0.10f ),
            ( SpellId.InvulnerabilityOther1,      0.10f ),
            ( SpellId.ArmorExpertiseOther1,       0.05f ),
            ( SpellId.ItemExpertiseOther1,        0.05f ),
            ( SpellId.WeaponExpertiseOther1,      0.05f ),
            ( SpellId.MonsterAttunementOther1,    0.05f ),
            ( SpellId.HealingMasteryOther1,       0.05f ),
            ( SpellId.RegenerationOther1,         0.05f ),
            ( SpellId.RejuvenationOther1,         0.05f ),
            ( SpellId.ManaRenewalOther1,          0.05f ),
            ( SpellId.DualWieldMasteryOther1,     0.05f ),
            ( SpellId.DirtyFightingMasteryOther1, 0.05f ),
            ( SpellId.RecklessnessMasteryOther1,  0.05f ),
            ( SpellId.SneakAttackMasteryOther1,   0.05f ),
            ( SpellId.FealtyOther1,               0.05f ),
        };

        // boots, chiran sandals, sollerets
        private static ChanceTable<SpellId> spellSelectionGroup11 = new ChanceTable<SpellId>()
        {
            ( SpellId.QuicknessOther1,             0.23f ),
            ( SpellId.HeavyWeaponsMasteryOther1,   0.10f ),
            ( SpellId.FinesseWeaponsMasteryOther1, 0.10f ),
            ( SpellId.MissileWeaponsMasteryOther1, 0.10f ),
            ( SpellId.HealingMasteryOther1,        0.10f ),
            ( SpellId.LightWeaponsMasteryOther1,   0.09f ),
            ( SpellId.TwoHandedMasteryOther1,      0.09f ),
            ( SpellId.CoordinationOther1,          0.09f ),
            ( SpellId.JumpingMasteryOther1,        0.05f ),
            ( SpellId.SprintOther1,                0.05f ),
        };

        // breeches, jerkins, shirts, pants, tunics, doublets, trousers, pantaloons
        private static ChanceTable<SpellId> spellSelectionGroup12 = new ChanceTable<SpellId>()
        {
            ( SpellId.ArmorOther1,               0.30f ),
            ( SpellId.AcidProtectionOther1,      0.10f ),
            ( SpellId.BludgeonProtectionOther1,  0.10f ),
            ( SpellId.ColdProtectionOther1,      0.10f ),
            ( SpellId.LightningProtectionOther1, 0.10f ),
            ( SpellId.FireProtectionOther1,      0.10f ),
            ( SpellId.BladeProtectionOther1,     0.10f ),
            ( SpellId.PiercingProtectionOther1,  0.10f ),
        };

        // caps, qafiyas, turbans, fezs, berets
        private static ChanceTable<SpellId> spellSelectionGroup13 = new ChanceTable<SpellId>()
        {
            ( SpellId.FocusOther1,                      0.04f ),
            ( SpellId.WillpowerOther1,                  0.04f ),
            ( SpellId.RejuvenationOther1,               0.04f ),
            ( SpellId.RegenerationOther1,               0.04f ),
            ( SpellId.ArmorOther1,                      0.03f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.03f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.03f ),
            ( SpellId.LifeMagicMasteryOther1,           0.03f ),
            ( SpellId.WarMagicMasteryOther1,            0.03f ),
            ( SpellId.VoidMagicMasteryOther1,           0.03f ),
            ( SpellId.DualWieldMasteryOther1,           0.03f ),
            ( SpellId.DirtyFightingMasteryOther1,       0.03f ),
            ( SpellId.RecklessnessMasteryOther1,        0.03f ),
            ( SpellId.SneakAttackMasteryOther1,         0.03f ),
            ( SpellId.MagicResistanceOther1,            0.03f ),
            ( SpellId.ManaRenewalOther1,                0.03f ),
            ( SpellId.AlchemyMasteryOther1,             0.03f ),
            ( SpellId.CookingMasteryOther1,             0.03f ),
            ( SpellId.FletchingMasteryOther1,           0.03f ),
            ( SpellId.HealingMasteryOther1,             0.03f ),
            ( SpellId.LockpickMasteryOther1,            0.03f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.03f ),
            ( SpellId.DeceptionMasteryOther1,           0.03f ),
            ( SpellId.FealtyOther1,                     0.03f ),
            ( SpellId.ManaMasteryOther1,                0.03f ),
            ( SpellId.ArcanumSalvagingOther1,           0.03f ),
            ( SpellId.ArmorExpertiseOther1,             0.03f ),
            ( SpellId.MagicItemExpertiseOther1,         0.03f ),
            ( SpellId.ItemExpertiseOther1,              0.03f ),
            ( SpellId.WeaponExpertiseOther1,            0.03f ),
            ( SpellId.MonsterAttunementOther1,          0.03f ),
            ( SpellId.PersonAttunementOther1,           0.03f ),
        };

        // cloth gloves (1 entry?)
        private static ChanceTable<SpellId> spellSelectionGroup14 = new ChanceTable<SpellId>()
        {
            ( SpellId.CoordinationOther1,               0.04f ),
            ( SpellId.QuicknessOther1,                  0.04f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.04f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.04f ),
            ( SpellId.LifeMagicMasteryOther1,           0.04f ),
            ( SpellId.WarMagicMasteryOther1,            0.04f ),
            ( SpellId.VoidMagicMasteryOther1,           0.04f ),
            ( SpellId.ManaMasteryOther1,                0.04f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.04f ),
            ( SpellId.ArcanumSalvagingOther1,           0.04f ),
            ( SpellId.ArmorExpertiseOther1,             0.04f ),
            ( SpellId.ItemExpertiseOther1,              0.04f ),
            ( SpellId.MagicItemExpertiseOther1,         0.04f ),
            ( SpellId.WeaponExpertiseOther1,            0.04f ),
            ( SpellId.HeavyWeaponsMasteryOther1,        0.04f ),
            ( SpellId.LightWeaponsMasteryOther1,        0.04f ),
            ( SpellId.FinesseWeaponsMasteryOther1,      0.04f ),
            ( SpellId.MissileWeaponsMasteryOther1,      0.04f ),
            ( SpellId.TwoHandedMasteryOther1,           0.04f ),
            ( SpellId.ShieldMasteryOther1,              0.04f ),
            ( SpellId.AlchemyMasteryOther1,             0.04f ),
            ( SpellId.CookingMasteryOther1,             0.04f ),
            ( SpellId.FletchingMasteryOther1,           0.04f ),
            ( SpellId.HealingMasteryOther1,             0.04f ),
            ( SpellId.LockpickMasteryOther1,            0.04f ),
        };

        // greaves, leggings, tassets, leather pants
        private static ChanceTable<SpellId> spellSelectionGroup15 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthOther1,         0.25f ),
            ( SpellId.QuicknessOther1,        0.25f ),
            ( SpellId.SummoningMasteryOther1, 0.20f ),
            ( SpellId.JumpingMasteryOther1,   0.10f ),
            ( SpellId.SprintOther1,           0.10f ),
            ( SpellId.EnduranceOther1,        0.10f ),
        };

        // dinnerware
        private static ChanceTable<SpellId> spellSelectionGroup16 = new ChanceTable<SpellId>()
        {
            ( SpellId.AlchemyMasteryOther1,     0.09f ),
            ( SpellId.CookingMasteryOther1,     0.09f ),
            ( SpellId.FletchingMasteryOther1,   0.09f ),
            ( SpellId.LockpickMasteryOther1,    0.08f ),
            ( SpellId.ArcanumSalvagingOther1,   0.08f ),
            ( SpellId.ArmorExpertiseOther1,     0.08f ),
            ( SpellId.ItemExpertiseOther1,      0.08f ),
            ( SpellId.MagicItemExpertiseOther1, 0.08f ),
            ( SpellId.WeaponExpertiseOther1,    0.08f ),
            ( SpellId.WillpowerOther1,          0.05f ),
            ( SpellId.StrengthOther1,           0.04f ),
            ( SpellId.EnduranceOther1,          0.04f ),
            ( SpellId.CoordinationOther1,       0.04f ),
            ( SpellId.QuicknessOther1,          0.04f ),
            ( SpellId.FocusOther1,              0.04f ),
        };

        // added

        // missile weapons, two-handed weapons
        private static ChanceTable<SpellId> spellSelectionGroup17 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthOther1,             0.16f ),
            ( SpellId.EnduranceOther1,            0.15f ),
            ( SpellId.CoordinationOther1,         0.15f ),
            ( SpellId.QuicknessOther1,            0.15f ),
            ( SpellId.DirtyFightingMasteryOther1, 0.13f ),
            ( SpellId.RecklessnessMasteryOther1,  0.13f ),
            ( SpellId.SneakAttackMasteryOther1,   0.13f ),
        };

        // shoes, loafers, slippers, sandals
        private static ChanceTable<SpellId> spellSelectionGroup18 = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthOther1,              0.06f ),
            ( SpellId.QuicknessOther1,             0.06f ),
            ( SpellId.ImpregnabilityOther1,        0.06f ),
            ( SpellId.InvulnerabilityOther1,       0.06f ),
            ( SpellId.MagicResistanceOther1,       0.06f ),
            ( SpellId.ArcaneEnlightenmentOther1,   0.06f ),
            ( SpellId.ManaMasteryOther1,           0.06f ),
            ( SpellId.HealingMasteryOther1,        0.06f ),
            ( SpellId.JumpingMasteryOther1,        0.06f ),
            ( SpellId.SprintOther1,                0.06f ),
            ( SpellId.HeavyWeaponsMasteryOther1,   0.06f ),
            ( SpellId.LightWeaponsMasteryOther1,   0.06f ),
            ( SpellId.FinesseWeaponsMasteryOther1, 0.06f ),
            ( SpellId.MissileWeaponsMasteryOther1, 0.06f ),
            ( SpellId.TwoHandedMasteryOther1,      0.06f ),
            ( SpellId.EnduranceOther1,             0.05f ),
            ( SpellId.CoordinationOther1,          0.05f ),
        };

        // nether caster
        private static ChanceTable<SpellId> spellSelectionGroup19 = new ChanceTable<SpellId>()
        {
            ( SpellId.VoidMagicMasteryOther1,           0.25f ),
            ( SpellId.WillpowerOther1,                  0.15f ),
            ( SpellId.ManaMasteryOther1,                0.10f ),
            ( SpellId.LifeMagicMasteryOther1,           0.10f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.10f ),
            ( SpellId.FocusOther1,                      0.09f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.09f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.06f ),
            ( SpellId.SneakAttackMasteryOther1,         0.06f ),
        };

        // leather cap (1 entry?)
        private static ChanceTable<SpellId> spellSelectionGroup20 = new ChanceTable<SpellId>()
        {
            ( SpellId.RecklessnessMasteryOther1,        0.075f ),
            ( SpellId.LockpickMasteryOther1,            0.075f ),
            ( SpellId.CookingMasteryOther1,             0.05f ),
            ( SpellId.FletchingMasteryOther1,           0.05f ),
            ( SpellId.ItemEnchantmentMasteryOther1,     0.05f ),
            ( SpellId.CreatureEnchantmentMasteryOther1, 0.04f ),
            ( SpellId.FealtyOther1,                     0.04f ),
            ( SpellId.ManaMasteryOther1,                0.04f ),
            ( SpellId.SneakAttackMasteryOther1,         0.04f ),
            ( SpellId.WillpowerOther1,                  0.04f ),
            ( SpellId.ItemExpertiseOther1,              0.03f ),
            ( SpellId.PersonAttunementOther1,           0.03f ),
            ( SpellId.RegenerationOther1,               0.03f ),
            ( SpellId.VoidMagicMasteryOther1,           0.03f ),
            ( SpellId.WarMagicMasteryOther1,            0.03f ),
            ( SpellId.WeaponExpertiseOther1,            0.03f ),
            ( SpellId.AlchemyMasteryOther1,             0.025f ),
            ( SpellId.ArcaneEnlightenmentOther1,        0.025f ),
            ( SpellId.ArcanumSalvagingOther1,           0.025f ),
            ( SpellId.DeceptionMasteryOther1,           0.025f ),
            ( SpellId.DualWieldMasteryOther1,           0.025f ),
            ( SpellId.MonsterAttunementOther1,          0.025f ),
            ( SpellId.ArmorExpertiseOther1,             0.02f ),
            ( SpellId.DirtyFightingMasteryOther1,       0.02f ),
            ( SpellId.FocusOther1,                      0.02f ),
            ( SpellId.HealingMasteryOther1,             0.02f ),
            ( SpellId.MagicItemExpertiseOther1,         0.02f ),
            ( SpellId.MagicResistanceOther1,            0.02f ),
            ( SpellId.ManaRenewalOther1,                0.02f ),
            ( SpellId.RejuvenationOther1,               0.02f ),
            ( SpellId.LifeMagicMasteryOther1,           0.01f ),
        };

        private static ChanceTable<SpellId> spellSelectionGroup21 = new ChanceTable<SpellId>()
        {
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
                // gems
                spellSelectionGroup1 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthOther1,               5.0f ),
                    ( SpellId.EnduranceOther1,              5.0f ),
                    ( SpellId.CoordinationOther1,           5.0f ),
                    ( SpellId.QuicknessOther1,              5.0f ),
                    ( SpellId.FocusOther1,                  5.0f ),
                    ( SpellId.WillpowerOther1,              5.0f ),

                    ( SpellId.ArcaneEnlightenmentOther1,    3.0f ),

                    ( SpellId.MagicResistanceOther1,        2.0f ),
                    ( SpellId.ImpregnabilityOther1,         2.0f ),
                    ( SpellId.InvulnerabilityOther1,        2.0f ),
                    ( SpellId.DeceptionMasteryOther1,       2.0f ),
                    ( SpellId.MonsterAttunementOther1,      2.0f ),
                    ( SpellId.JumpingMasteryOther1,         2.0f ),
                    ( SpellId.SprintOther1,                 2.0f ),
                    ( SpellId.HealingMasteryOther1,         2.0f ),
                    ( SpellId.LeadershipMasteryOther1,      2.0f ),
                    ( SpellId.FealtyOther1,                 2.0f ),

                    ( SpellId.AlchemyMasteryOther1,         1.0f ),
                    ( SpellId.CookingMasteryOther1,         1.0f ),
                    ( SpellId.FletchingMasteryOther1,       1.0f ),
                    ( SpellId.LockpickMasteryOther1,        1.0f ),
                    ( SpellId.ArcanumSalvagingOther1,       1.0f ),

                    ( SpellId.ArmorMasteryOther1,           1.0f ),
                    ( SpellId.AwarenessMasteryOther1,       1.0f ),
                    ( SpellId.AppraiseMasteryOther1,        1.0f ),
                    ( SpellId.SneakingMasteryOther1,        1.0f ),

                    ( SpellId.LightWeaponsMasteryOther1,    0.5f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1,  0.5f ), // DaggerMasteryOther1
                    //( SpellId.MaceMasteryOther1,            0.5f ),
                    ( SpellId.SpearMasteryOther1,           0.5f ),
                    //( SpellId.StaffMasteryOther1,           0.5f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,    0.5f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,   0.5f ),
                    ( SpellId.MissileWeaponsMasteryOther1,  0.5f ), // BowMasteryOther1
                    //( SpellId.CrossbowMasteryOther1,        0.5f ),
                    ( SpellId.ThrownWeaponMasteryOther1,    0.5f ),
                    ( SpellId.ShieldMasteryOther1,          0.5f ),
                    ( SpellId.DualWieldMasteryOther1,       0.5f ),
                    ( SpellId.WarMagicMasteryOther1,        0.5f ),
                    ( SpellId.LifeMagicMasteryOther1,       0.5f ),
                };

                // jewelry
                spellSelectionGroup2 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.ArcaneEnlightenmentOther1, 5.0f ),
                    ( SpellId.MagicResistanceOther1,     5.0f ),
                    ( SpellId.ArmorOther1,               5.0f ),
                    ( SpellId.AcidProtectionOther1,      5.0f ),
                    ( SpellId.BludgeonProtectionOther1,  5.0f ),
                    ( SpellId.ColdProtectionOther1,      5.0f ),
                    ( SpellId.LightningProtectionOther1, 5.0f ),
                    ( SpellId.FireProtectionOther1,      5.0f ),
                    ( SpellId.BladeProtectionOther1,     5.0f ),
                    ( SpellId.PiercingProtectionOther1,  5.0f ),
                    ( SpellId.StrengthOther1,            4.0f ),
                    ( SpellId.EnduranceOther1,           4.0f ),
                    ( SpellId.CoordinationOther1,        4.0f ),
                    ( SpellId.QuicknessOther1,           4.0f ),
                    ( SpellId.FocusOther1,               4.0f ),
                    ( SpellId.WillpowerOther1,           4.0f ),
                    ( SpellId.ManaRenewalOther1,         4.0f ),
                    ( SpellId.ManaMasteryOther1,         4.0f ),
                    ( SpellId.RegenerationOther1,        3.0f ),
                    ( SpellId.RejuvenationOther1,        3.0f ),
                    ( SpellId.DeceptionMasteryOther1,    1.0f ),
                    ( SpellId.FealtyOther1,              1.0f ),
                    ( SpellId.MonsterAttunementOther1,   1.0f ),
                    ( SpellId.ArcanumSalvagingOther1,    1.0f ),
                    ( SpellId.ArmorMasteryOther1,        1.0f ),
                    ( SpellId.AwarenessMasteryOther1,    1.0f ),
                    ( SpellId.AppraiseMasteryOther1,     1.0f ),
                    ( SpellId.SneakingMasteryOther1,     1.0f ),
                };

                // crowns
                spellSelectionGroup3 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LeadershipMasteryOther1,    2.0f),
                    ( SpellId.ImpregnabilityOther1,       2.0f),
                    ( SpellId.InvulnerabilityOther1,      2.0f),
                    ( SpellId.MagicResistanceOther1,      2.0f),
                    ( SpellId.FocusOther1,                1.0f),
                    ( SpellId.WillpowerOther1,            1.0f),
                    ( SpellId.ArmorOther1,                1.0f),
                    ( SpellId.RegenerationOther1,         1.0f),
                    ( SpellId.RejuvenationOther1,         1.0f),
                    ( SpellId.ManaRenewalOther1,          1.0f),
                    ( SpellId.ManaMasteryOther1,          1.0f),
                    ( SpellId.ArcaneEnlightenmentOther1,  1.0f),
                    ( SpellId.HealingMasteryOther1,       1.0f),
                    ( SpellId.DeceptionMasteryOther1,     1.0f),
                    ( SpellId.MonsterAttunementOther1,    1.0f),
                };

                // orbs
                spellSelectionGroup4 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LifeMagicMasteryOther1,           2.0f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        1.5f ),
                    ( SpellId.FocusOther1,                      1.0f ),
                    ( SpellId.WillpowerOther1,                  1.0f ),
                    ( SpellId.WarMagicMasteryOther1,            1.0f ),
                    ( SpellId.ManaMasteryOther1,                1.0f ),
                };

                // wands, staffs, sceptres, batons
                spellSelectionGroup5 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.WarMagicMasteryOther1,            2.0f ),
                    ( SpellId.WillpowerOther1,                  1.5f ),
                    ( SpellId.LifeMagicMasteryOther1,           1.0f ),
                    ( SpellId.FocusOther1,                      1.0f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        1.0f ),
                    ( SpellId.ManaMasteryOther1,                1.0f ),
                };

                // one-handed melee weapons
                spellSelectionGroup6 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessOther1,            0.225f ),
                    ( SpellId.StrengthOther1,             0.225f ),
                    ( SpellId.EnduranceOther1,            0.225f ),
                    ( SpellId.CoordinationOther1,         0.225f ),
                    ( SpellId.DualWieldMasteryOther1,     0.100f ),
                };

                // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
                spellSelectionGroup7 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthOther1,         0.30f ),
                    ( SpellId.EnduranceOther1,        0.30f ),

                    ( SpellId.MagicResistanceOther1,  0.15f ),

                    ( SpellId.RejuvenationOther1,     0.10f ),
                    ( SpellId.RegenerationOther1,     0.10f ),

                    ( SpellId.FealtyOther1,           0.05f ),
                };

                // shields
                spellSelectionGroup8 = new ChanceTable<SpellId>()
                {
                    ( SpellId.ImpregnabilityOther1,  0.15f ),
                    ( SpellId.InvulnerabilityOther1, 0.15f ),

                    ( SpellId.StrengthOther1,        0.10f ),
                    ( SpellId.EnduranceOther1,       0.10f ),

                    ( SpellId.RejuvenationOther1,    0.15f ),

                    ( SpellId.FealtyOther1,          0.15f ),
                    ( SpellId.MagicResistanceOther1, 0.10f ),

                    ( SpellId.ShieldMasteryOther1,   0.10f ),
                };

                // gauntlets
                spellSelectionGroup9 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CoordinationOther1,          2.0f ),

                    ( SpellId.HealingMasteryOther1,        1.5f ),

                    ( SpellId.LightWeaponsMasteryOther1,      1.0f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1,    1.0f ), // DaggerMasteryOther1
                    //( SpellId.MaceMasteryOther1,              1.0f ),
                    ( SpellId.SpearMasteryOther1,             1.0f ),
                    //( SpellId.StaffMasteryOther1,             1.0f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,      1.0f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,     1.0f ),
                    ( SpellId.MissileWeaponsMasteryOther1,    1.0f ), // BowMasteryOther1
                    //( SpellId.CrossbowMasteryOther1,          1.0f ),
                    ( SpellId.ThrownWeaponMasteryOther1,      1.0f ),
                    ( SpellId.ShieldMasteryOther1,            1.0f ),
                };

                // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
                spellSelectionGroup10 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.MagicResistanceOther1,         2.5f ),
                    ( SpellId.ImpregnabilityOther1,          2.0f ),
                    ( SpellId.InvulnerabilityOther1,         2.0f ),

                    ( SpellId.MonsterAttunementOther1,       1.0f ),
                    ( SpellId.HealingMasteryOther1,          1.0f ),
                    ( SpellId.RegenerationOther1,            1.0f ),
                    ( SpellId.RejuvenationOther1,            1.0f ),
                    ( SpellId.ManaRenewalOther1,             1.0f ),
                    ( SpellId.FealtyOther1,                  1.0f ),

                    ( SpellId.DualWieldMasteryOther1,        1.0f ),
                    ( SpellId.ArmorMasteryOther1,            1.0f ),
                    ( SpellId.AwarenessMasteryOther1,        1.0f ),
                    ( SpellId.AppraiseMasteryOther1,         1.0f ),
                };

                // boots, chiran sandals, sollerets
                spellSelectionGroup11 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.QuicknessOther1,             8.0f ),
                    ( SpellId.CoordinationOther1,          4.0f ),

                    ( SpellId.HealingMasteryOther1,        4.0f ),

                    ( SpellId.JumpingMasteryOther1,        2.0f ),
                    ( SpellId.SprintOther1,                2.0f ),

                    ( SpellId.LightWeaponsMasteryOther1,   1.0f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1, 1.0f ), // DaggerMasteryOther1
                    //( SpellId.MaceMasteryOther1,           1.0f ),
                    ( SpellId.SpearMasteryOther1,          1.0f ),
                    //( SpellId.StaffMasteryOther1,          1.0f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,   1.0f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,  1.0f ),
                    ( SpellId.MissileWeaponsMasteryOther1, 1.0f ), // BowMasteryOther1
                    //( SpellId.CrossbowMasteryOther1,       1.0f ),
                    ( SpellId.ThrownWeaponMasteryOther1,   1.0f ),
                    ( SpellId.SneakingMasteryOther1,       1.0f ),
                };

                // caps, qafiyas, turbans, fezs, berets
                spellSelectionGroup13 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.FocusOther1,                      1.0f ),
                    ( SpellId.WillpowerOther1,                  1.0f ),
                    ( SpellId.RejuvenationOther1,               1.0f ),
                    ( SpellId.RegenerationOther1,               1.0f ),

                    ( SpellId.ArmorOther1,                      0.9f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.9f ),
                    ( SpellId.WarMagicMasteryOther1,            0.9f ),
                    ( SpellId.MagicResistanceOther1,            0.9f ),
                    ( SpellId.ManaRenewalOther1,                0.9f ),
                    ( SpellId.HealingMasteryOther1,             0.9f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.9f ),
                    ( SpellId.FealtyOther1,                     0.9f ),
                    ( SpellId.ManaMasteryOther1,                0.9f ),

                    ( SpellId.AlchemyMasteryOther1,             0.8f ),
                    ( SpellId.CookingMasteryOther1,             0.8f ),
                    ( SpellId.FletchingMasteryOther1,           0.8f ),
                    ( SpellId.LockpickMasteryOther1,            0.8f ),
                    ( SpellId.DeceptionMasteryOther1,           0.8f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.8f ),
                    ( SpellId.MonsterAttunementOther1,          0.8f ),
                };

                // cloth gloves (1 entry?)
                spellSelectionGroup14 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.CoordinationOther1,               1.0f ),
                    ( SpellId.QuicknessOther1,                  1.0f ),

                    ( SpellId.AlchemyMasteryOther1,             0.9f ),
                    ( SpellId.CookingMasteryOther1,             0.9f ),
                    ( SpellId.FletchingMasteryOther1,           0.9f ),
                    ( SpellId.HealingMasteryOther1,             0.9f ),
                    ( SpellId.LockpickMasteryOther1,            0.9f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.9f ),
                    ( SpellId.WarMagicMasteryOther1,            0.9f ),
                    ( SpellId.ManaMasteryOther1,                0.9f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.9f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.9f ),
                };

                // greaves, leggings, tassets, leather pants
                spellSelectionGroup15 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthOther1,         1.0f ),
                    ( SpellId.QuicknessOther1,        1.0f ),
                    ( SpellId.JumpingMasteryOther1,   0.5f ),
                    ( SpellId.SprintOther1,           0.5f ),
                    ( SpellId.EnduranceOther1,        0.5f ),
                };

                // dinnerware
                spellSelectionGroup16 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.AlchemyMasteryOther1,        2.0f ),
                    ( SpellId.CookingMasteryOther1,        2.0f ),
                    ( SpellId.FletchingMasteryOther1,      2.0f ),
                    ( SpellId.LockpickMasteryOther1,       2.0f ),
                    ( SpellId.ArcanumSalvagingOther1,      2.0f ),
                    ( SpellId.StrengthOther1,              1.0f ),
                    ( SpellId.EnduranceOther1,             1.0f ),
                    ( SpellId.CoordinationOther1,          1.0f ),
                    ( SpellId.QuicknessOther1,             1.0f ),
                    ( SpellId.FocusOther1,                 1.0f ),
                    ( SpellId.WillpowerOther1,             1.0f ),
                };

                // missile weapons, two-handed weapons
                spellSelectionGroup17 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthOther1,             1.0f ),
                    ( SpellId.EnduranceOther1,            1.0f ),
                    ( SpellId.CoordinationOther1,         1.0f ),
                    ( SpellId.QuicknessOther1,            1.0f ),
                };

                // shoes, loafers, slippers, sandals
                spellSelectionGroup18 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.StrengthOther1,              1.0f ),
                    ( SpellId.QuicknessOther1,             1.0f ),
                    ( SpellId.EnduranceOther1,             1.0f ),
                    ( SpellId.CoordinationOther1,          1.0f ),

                    ( SpellId.ImpregnabilityOther1,        0.5f ),
                    ( SpellId.InvulnerabilityOther1,       0.5f ),
                    ( SpellId.MagicResistanceOther1,       0.5f ),

                    ( SpellId.ArcaneEnlightenmentOther1,   0.5f ),
                    ( SpellId.ManaMasteryOther1,           0.5f ),
                    ( SpellId.HealingMasteryOther1,        0.5f ),

                    ( SpellId.JumpingMasteryOther1,        0.5f ),
                    ( SpellId.SprintOther1,                0.5f ),
                    ( SpellId.SneakingMasteryOther1,       0.5f ),
                };

                // nether caster - should never be used with Infiltration data but here for completioness.
                spellSelectionGroup19 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.WarMagicMasteryOther1,            1.0f ),

                    ( SpellId.WillpowerOther1,                  0.8f ),

                    ( SpellId.ManaMasteryOther1,                0.4f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.4f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.4f ),
                    ( SpellId.FocusOther1,                      0.4f ),
                };

                // leather cap (1 entry?)
                spellSelectionGroup20 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.LockpickMasteryOther1,            1.0f ),

                    ( SpellId.CookingMasteryOther1,             0.8f ),
                    ( SpellId.FletchingMasteryOther1,           0.8f ),
                    ( SpellId.AlchemyMasteryOther1,             0.8f ),

                    ( SpellId.WarMagicMasteryOther1,            0.6f ),
                    ( SpellId.FealtyOther1,                     0.6f ),
                    ( SpellId.ManaMasteryOther1,                0.6f ),

                    ( SpellId.WillpowerOther1,                  0.5f ),
                    ( SpellId.FocusOther1,                      0.5f ),
                    ( SpellId.RegenerationOther1,               0.5f ),

                    ( SpellId.ArcaneEnlightenmentOther1,        0.4f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.4f ),
                    ( SpellId.DeceptionMasteryOther1,           0.4f ),
                    ( SpellId.MonsterAttunementOther1,          0.4f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.4f ),
                    ( SpellId.HealingMasteryOther1,             0.4f ),
                    ( SpellId.MagicResistanceOther1,            0.4f ),
                    ( SpellId.ManaRenewalOther1,                0.4f ),
                    ( SpellId.RejuvenationOther1,               0.4f ),
                };

                // robes
                spellSelectionGroup21 = new ChanceTable<SpellId>(ChanceTableType.Weight)
                {
                    ( SpellId.WillpowerOther1,                  2.0f ),
                    ( SpellId.FocusOther1,                      2.0f ),
                    ( SpellId.ManaMasteryOther1,                2.0f ),
                    ( SpellId.RejuvenationOther1,               1.5f ),
                    ( SpellId.RegenerationOther1,               1.5f ),
                    ( SpellId.ManaRenewalOther1,                1.5f ),
                    ( SpellId.MagicResistanceOther1,            1.5f ),
                    ( SpellId.ImpregnabilityOther1,             1.5f ),
                    ( SpellId.InvulnerabilityOther1,            1.5f ),
                    ( SpellId.MagicResistanceOther1,            1.5f ),
                    ( SpellId.MonsterAttunementOther1,          1.0f ),
                    ( SpellId.WarMagicMasteryOther1,            1.0f ),
                    ( SpellId.LifeMagicMasteryOther1,           1.0f ),
                    ( SpellId.AlchemyMasteryOther1,             1.0f ),
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
                    spellSelectionGroup21,
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                // orbs
                spellSelectionGroup4 = new ChanceTable<SpellId>()
                {
                    ( SpellId.LifeMagicMasteryOther1,           0.20f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.16f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.12f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.12f ),
                    ( SpellId.FocusOther1,                      0.10f ),
                    ( SpellId.WillpowerOther1,                  0.10f ),
                    ( SpellId.WarMagicMasteryOther1,            0.10f ),
                    ( SpellId.ManaMasteryOther1,                0.10f ),
                };

                // wands, staffs, sceptres, batons
                spellSelectionGroup5 = new ChanceTable<SpellId>()
                {
                    ( SpellId.WarMagicMasteryOther1,            0.26f ),
                    ( SpellId.WillpowerOther1,                  0.16f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.11f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.11f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.09f ),
                    ( SpellId.FocusOther1,                      0.09f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.09f ),
                    ( SpellId.ManaMasteryOther1,                0.09f ),
                };

                // one-handed melee weapons
                spellSelectionGroup6 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessOther1,            0.325f ),
                    ( SpellId.StrengthOther1,             0.225f ),
                    ( SpellId.EnduranceOther1,            0.225f ),
                    ( SpellId.CoordinationOther1,         0.225f ),
                };

                // bracers, breastplates, coats, cuirasses, girths, hauberks, pauldrons, chest armor, sleeves
                spellSelectionGroup7 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthOther1,         0.30f ),
                    ( SpellId.EnduranceOther1,        0.30f ),
                    ( SpellId.MagicResistanceOther1,  0.15f ),
                    ( SpellId.RejuvenationOther1,     0.10f ),
                    ( SpellId.RegenerationOther1,     0.10f ),
                    ( SpellId.FealtyOther1,           0.05f ),
                };

                // shields
                spellSelectionGroup8 = new ChanceTable<SpellId>()
                {
                    ( SpellId.ImpregnabilityOther1,  0.17f ),
                    ( SpellId.InvulnerabilityOther1, 0.17f ),
                    ( SpellId.FealtyOther1,          0.17f ),
                    ( SpellId.RejuvenationOther1,    0.16f ),
                    ( SpellId.StrengthOther1,        0.11f ),
                    ( SpellId.EnduranceOther1,       0.11f ),
                    ( SpellId.MagicResistanceOther1, 0.11f ),
                };

                // gauntlets
                spellSelectionGroup9 = new ChanceTable<SpellId>()
                {
                    ( SpellId.CoordinationOther1,          0.20f ),
                    ( SpellId.HealingMasteryOther1,        0.10f ),
                    ( SpellId.LightWeaponsMasteryOther1,   0.07f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1, 0.07f ), // DaggerMasteryOther1
                    ( SpellId.MaceMasteryOther1,           0.07f ),
                    ( SpellId.SpearMasteryOther1,          0.07f ),
                    ( SpellId.StaffMasteryOther1,          0.07f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,   0.07f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,  0.07f ),
                    ( SpellId.MissileWeaponsMasteryOther1, 0.07f ), // BowMasteryOther1
                    ( SpellId.CrossbowMasteryOther1,       0.07f ),
                    ( SpellId.ThrownWeaponMasteryOther1,   0.07f ),
                };

                // helms, basinets, helmets, coifs, cowls, heaumes, kabutons
                spellSelectionGroup10 = new ChanceTable<SpellId>()
                {
                    ( SpellId.MagicResistanceOther1,      0.15f ),
                    ( SpellId.ImpregnabilityOther1,       0.11f ),
                    ( SpellId.InvulnerabilityOther1,      0.11f ),

                    ( SpellId.ArmorExpertiseOther1,       0.07f ),
                    ( SpellId.ItemExpertiseOther1,        0.07f ),
                    ( SpellId.WeaponExpertiseOther1,      0.07f ),
                    ( SpellId.MonsterAttunementOther1,    0.07f ),
                    ( SpellId.HealingMasteryOther1,       0.07f ),
                    ( SpellId.RegenerationOther1,         0.07f ),
                    ( SpellId.RejuvenationOther1,         0.07f ),
                    ( SpellId.ManaRenewalOther1,          0.07f ),
                    ( SpellId.FealtyOther1,               0.07f ),
                };

                // boots, chiran sandals, sollerets
                spellSelectionGroup11 = new ChanceTable<SpellId>()
                {
                    ( SpellId.QuicknessOther1,             0.20f ),
                    ( SpellId.HealingMasteryOther1,        0.10f ),
                    ( SpellId.CoordinationOther1,          0.10f ),
                    ( SpellId.JumpingMasteryOther1,        0.05f ),
                    ( SpellId.SprintOther1,                0.05f ),
                    ( SpellId.LightWeaponsMasteryOther1,   0.05f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1, 0.05f ), // DaggerMasteryOther1
                    ( SpellId.MaceMasteryOther1,           0.05f ),
                    ( SpellId.SpearMasteryOther1,          0.05f ),
                    ( SpellId.StaffMasteryOther1,          0.05f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,   0.05f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,  0.05f ),
                    ( SpellId.MissileWeaponsMasteryOther1, 0.05f ), // BowMasteryOther1
                    ( SpellId.CrossbowMasteryOther1,       0.05f ),
                    ( SpellId.ThrownWeaponMasteryOther1,   0.05f ),
                };

                // caps, qafiyas, turbans, fezs, berets
                spellSelectionGroup13 = new ChanceTable<SpellId>()
                {
                    ( SpellId.FocusOther1,                      0.05f ),
                    ( SpellId.WillpowerOther1,                  0.05f ),
                    ( SpellId.RejuvenationOther1,               0.05f ),
                    ( SpellId.RegenerationOther1,               0.05f ),
                    ( SpellId.ArmorOther1,                      0.04f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.04f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.04f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.04f ),
                    ( SpellId.WarMagicMasteryOther1,            0.04f ),
                    ( SpellId.MagicResistanceOther1,            0.04f ),
                    ( SpellId.ManaRenewalOther1,                0.04f ),
                    ( SpellId.HealingMasteryOther1,             0.04f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.04f ),
                    ( SpellId.FealtyOther1,                     0.04f ),
                    ( SpellId.ManaMasteryOther1,                0.04f ),
                    ( SpellId.AlchemyMasteryOther1,             0.03f ),
                    ( SpellId.CookingMasteryOther1,             0.03f ),
                    ( SpellId.FletchingMasteryOther1,           0.03f ),
                    ( SpellId.LockpickMasteryOther1,            0.03f ),
                    ( SpellId.DeceptionMasteryOther1,           0.03f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.03f ),
                    ( SpellId.ArmorExpertiseOther1,             0.03f ),
                    ( SpellId.MagicItemExpertiseOther1,         0.03f ),
                    ( SpellId.ItemExpertiseOther1,              0.03f ),
                    ( SpellId.WeaponExpertiseOther1,            0.03f ),
                    ( SpellId.MonsterAttunementOther1,          0.03f ),
                    ( SpellId.PersonAttunementOther1,           0.03f ),
                };

                // cloth gloves (1 entry?)
                spellSelectionGroup14 = new ChanceTable<SpellId>()
                {
                    ( SpellId.CoordinationOther1,               0.05f ),
                    ( SpellId.QuicknessOther1,                  0.05f ),
                    ( SpellId.AlchemyMasteryOther1,             0.04f ),
                    ( SpellId.CookingMasteryOther1,             0.04f ),
                    ( SpellId.FletchingMasteryOther1,           0.04f ),
                    ( SpellId.HealingMasteryOther1,             0.04f ),
                    ( SpellId.LockpickMasteryOther1,            0.04f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.04f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.04f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.04f ),
                    ( SpellId.WarMagicMasteryOther1,            0.04f ),
                    ( SpellId.ManaMasteryOther1,                0.04f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.04f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.04f ),
                    ( SpellId.ArmorExpertiseOther1,             0.03f ),
                    ( SpellId.ItemExpertiseOther1,              0.03f ),
                    ( SpellId.MagicItemExpertiseOther1,         0.03f ),
                    ( SpellId.WeaponExpertiseOther1,            0.03f ),
                    ( SpellId.LightWeaponsMasteryOther1,        0.03f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1,      0.03f ), // DaggerMasteryOther1
                    ( SpellId.MaceMasteryOther1,                0.03f ),
                    ( SpellId.SpearMasteryOther1,               0.03f ),
                    ( SpellId.StaffMasteryOther1,               0.03f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,        0.03f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,       0.03f ),
                    ( SpellId.MissileWeaponsMasteryOther1,      0.03f ), // BowMasteryOther1
                    ( SpellId.CrossbowMasteryOther1,            0.03f ),
                    ( SpellId.ThrownWeaponMasteryOther1,        0.03f ),
                };

                // greaves, leggings, tassets, leather pants
                spellSelectionGroup15 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthOther1,         0.29f ),
                    ( SpellId.QuicknessOther1,        0.29f ),
                    ( SpellId.JumpingMasteryOther1,   0.14f ),
                    ( SpellId.SprintOther1,           0.14f ),
                    ( SpellId.EnduranceOther1,        0.14f ),
                };

                // missile weapons, two-handed weapons
                spellSelectionGroup17 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthOther1,             0.25f ),
                    ( SpellId.EnduranceOther1,            0.25f ),
                    ( SpellId.CoordinationOther1,         0.25f ),
                    ( SpellId.QuicknessOther1,            0.25f ),
                };

                // shoes, loafers, slippers, sandals
                spellSelectionGroup18 = new ChanceTable<SpellId>()
                {
                    ( SpellId.StrengthOther1,              0.06f ),
                    ( SpellId.QuicknessOther1,             0.06f ),
                    ( SpellId.EnduranceOther1,             0.06f ),
                    ( SpellId.CoordinationOther1,          0.06f ),
                    ( SpellId.ImpregnabilityOther1,        0.05f ),
                    ( SpellId.InvulnerabilityOther1,       0.05f ),
                    ( SpellId.MagicResistanceOther1,       0.05f ),
                    ( SpellId.ArcaneEnlightenmentOther1,   0.05f ),
                    ( SpellId.ManaMasteryOther1,           0.04f ),
                    ( SpellId.HealingMasteryOther1,        0.04f ),
                    ( SpellId.JumpingMasteryOther1,        0.04f ),
                    ( SpellId.SprintOther1,                0.04f ),
                    ( SpellId.LightWeaponsMasteryOther1,   0.04f ), // AxeMasteryOther1
                    ( SpellId.FinesseWeaponsMasteryOther1, 0.04f ), // DaggerMasteryOther1
                    ( SpellId.MaceMasteryOther1,           0.04f ),
                    ( SpellId.SpearMasteryOther1,          0.04f ),
                    ( SpellId.StaffMasteryOther1,          0.04f ),
                    ( SpellId.HeavyWeaponsMasteryOther1,   0.04f ), // SwordMasteryOther1
                    ( SpellId.UnarmedCombatMasteryOther1,  0.04f ),
                    ( SpellId.MissileWeaponsMasteryOther1, 0.04f ), // BowMasteryOther1
                    ( SpellId.CrossbowMasteryOther1,       0.04f ),
                    ( SpellId.ThrownWeaponMasteryOther1,   0.04f ),
                };

                // nether caster - should never be used with Infiltration data but here for completioness.
                spellSelectionGroup19 = new ChanceTable<SpellId>()
                {
                    ( SpellId.WarMagicMasteryOther1,            0.28f ),
                    ( SpellId.WillpowerOther1,                  0.18f ),
                    ( SpellId.ManaMasteryOther1,                0.10f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.10f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.10f ),
                    ( SpellId.FocusOther1,                      0.09f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.09f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.06f ),
                };

                // leather cap (1 entry?)
                spellSelectionGroup20 = new ChanceTable<SpellId>()
                {
                    ( SpellId.LockpickMasteryOther1,            0.08f ),
                    ( SpellId.CookingMasteryOther1,             0.06f ),
                    ( SpellId.FletchingMasteryOther1,           0.06f ),
                    ( SpellId.AlchemyMasteryOther1,             0.06f ),
                    ( SpellId.ItemEnchantmentMasteryOther1,     0.06f ),
                    ( SpellId.CreatureEnchantmentMasteryOther1, 0.05f ),
                    ( SpellId.ItemExpertiseOther1,              0.05f ),
                    ( SpellId.PersonAttunementOther1,           0.05f ),
                    ( SpellId.WeaponExpertiseOther1,            0.05f ),
                    ( SpellId.ArmorExpertiseOther1,             0.05f ),
                    ( SpellId.WarMagicMasteryOther1,            0.04f ),
                    ( SpellId.FealtyOther1,                     0.04f ),
                    ( SpellId.ManaMasteryOther1,                0.04f ),
                    ( SpellId.WillpowerOther1,                  0.03f ),
                    ( SpellId.FocusOther1,                      0.03f ),
                    ( SpellId.RegenerationOther1,               0.03f ),
                    ( SpellId.ArcaneEnlightenmentOther1,        0.025f ),
                    ( SpellId.ArcanumSalvagingOther1,           0.025f ),
                    ( SpellId.DeceptionMasteryOther1,           0.025f ),
                    ( SpellId.MonsterAttunementOther1,          0.025f ),
                    ( SpellId.LifeMagicMasteryOther1,           0.02f ),                
                    ( SpellId.HealingMasteryOther1,             0.02f ),
                    ( SpellId.MagicItemExpertiseOther1,         0.02f ),
                    ( SpellId.MagicResistanceOther1,            0.02f ),
                    ( SpellId.ManaRenewalOther1,                0.02f ),
                    ( SpellId.RejuvenationOther1,               0.02f ),
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
