using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using log4net;
using System;
using System.Collections.Generic;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class ScrollWcids
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly List<SpellId> creatureSpells = new List<SpellId>()
        {
            // creature buffs / debuffs
            SpellId.StrengthSelf1,
            SpellId.StrengthOther1,
            SpellId.WeaknessOther1,
            SpellId.EnduranceSelf1,
            SpellId.EnduranceOther1,
            SpellId.FrailtyOther1,
            SpellId.CoordinationSelf1,
            SpellId.CoordinationOther1,
            SpellId.ClumsinessOther1,
            SpellId.QuicknessSelf1,
            SpellId.QuicknessOther1,
            SpellId.SlownessOther1,
            SpellId.FocusSelf1,
            SpellId.FocusOther1,
            SpellId.BafflementOther1,
            SpellId.WillpowerSelf1,
            SpellId.WillpowerOther1,
            SpellId.FeeblemindOther1,

            SpellId.InvulnerabilitySelf1,
            SpellId.InvulnerabilityOther1,
            SpellId.VulnerabilityOther1,
            SpellId.ImpregnabilitySelf1,
            SpellId.ImpregnabilityOther1,
            SpellId.DefenselessnessOther1,
            SpellId.MagicResistanceSelf1,
            SpellId.MagicResistanceOther1,
            SpellId.MagicYieldOther1,

            SpellId.HeavyWeaponsMasterySelf1,
            SpellId.HeavyWeaponsMasteryOther1,
            SpellId.HeavyWeaponsIneptitudeOther1,
            SpellId.LightWeaponsMasterySelf1,
            SpellId.LightWeaponsMasteryOther1,
            SpellId.LightWeaponsIneptitudeOther1,
            SpellId.FinesseWeaponsMasterySelf1,
            SpellId.FinesseWeaponsMasteryOther1,
            SpellId.FinesseWeaponsIneptitudeOther1,
            SpellId.MissileWeaponsMasterySelf1,
            SpellId.MissileWeaponsMasteryOther1,
            SpellId.MissileWeaponsIneptitudeOther1,
            SpellId.TwoHandedMasterySelf1,
            SpellId.TwoHandedMasteryOther1,
            SpellId.TwoHandedIneptitude1,

            SpellId.CreatureEnchantmentMasterySelf1,
            SpellId.CreatureEnchantmentMasteryOther1,
            SpellId.CreatureEnchantmentIneptitudeOther1,
            SpellId.ItemEnchantmentMasterySelf1,
            SpellId.ItemEnchantmentMasteryOther1,
            SpellId.ItemEnchantmentIneptitudeOther1,
            SpellId.LifeMagicMasterySelf1,
            SpellId.LifeMagicMasteryOther1,
            SpellId.LifeMagicIneptitudeOther1,
            SpellId.WarMagicMasterySelf1,
            SpellId.WarMagicMasteryOther1,
            SpellId.WarMagicIneptitudeOther1,
            SpellId.VoidMagicMasterySelf1,
            SpellId.VoidMagicMasteryOther1,
            SpellId.VoidMagicIneptitudeOther1,
            SpellId.SummoningMasterySelf1,
            SpellId.SummoningMasteryOther1,
            SpellId.SummoningIneptitudeOther1,

            SpellId.ArcaneEnlightenmentSelf1,
            SpellId.ArcaneEnlightenmentOther1,
            SpellId.ArcaneBenightednessOther1,
            SpellId.DeceptionMasterySelf1,
            SpellId.DeceptionMasteryOther1,
            SpellId.DeceptionIneptitudeOther1,
            SpellId.HealingMasterySelf1,
            SpellId.HealingMasteryOther1,
            SpellId.HealingIneptitudeOther1,
            SpellId.LockpickMasterySelf1,
            SpellId.LockpickMasteryOther1,
            SpellId.LockpickIneptitudeOther1,
            SpellId.JumpingMasterySelf1,
            SpellId.JumpingMasteryOther1,
            SpellId.JumpingIneptitudeOther1,
            SpellId.ManaMasterySelf1,
            SpellId.ManaMasteryOther1,
            SpellId.ManaIneptitudeOther1,
            SpellId.SprintSelf1,
            SpellId.SprintOther1,
            SpellId.LeadenFeetOther1,

            SpellId.AlchemyMasterySelf1,
            SpellId.AlchemyMasteryOther1,
            SpellId.AlchemyIneptitudeOther1,
            SpellId.CookingMasterySelf1,
            SpellId.CookingMasteryOther1,
            SpellId.CookingIneptitudeOther1,
            SpellId.FletchingMasterySelf1,
            SpellId.FletchingMasteryOther1,
            SpellId.FletchingIneptitudeOther1,

            SpellId.DualWieldMasterySelf1,
            SpellId.DualWieldMasteryOther1,
            SpellId.DualWieldIneptitudeOther1,
            SpellId.DirtyFightingMasterySelf1,
            SpellId.DirtyFightingMasteryOther1,
            SpellId.DirtyFightingIneptitudeOther1,
            SpellId.RecklessnessMasterySelf1,
            SpellId.RecklessnessMasteryOther1,
            SpellId.RecklessnessIneptitudeOther1,
            SpellId.SneakAttackMasterySelf1,
            SpellId.SneakAttackMasteryOther1,
            SpellId.SneakAttackIneptitudeOther1,

            SpellId.ShieldMasterySelf1,
            SpellId.ShieldMasteryOther1,
            SpellId.ShieldIneptitudeOther1,

            SpellId.LeadershipMasterySelf1,
            SpellId.LeadershipMasteryOther1,
            SpellId.LeadershipIneptitudeOther1,
            SpellId.FealtySelf1,
            SpellId.FealtyOther1,
            SpellId.FaithlessnessOther1,

            SpellId.ArcanumSalvagingSelf1,
            SpellId.ArcanumSalvagingOther1,
            // no salvaging lowering spells, apparently?
            SpellId.ArmorExpertiseSelf1,
            SpellId.ArmorExpertiseOther1,
            SpellId.ArmorIgnoranceOther1,
            SpellId.ItemExpertiseSelf1,
            SpellId.ItemExpertiseOther1,
            SpellId.ItemIgnoranceOther1,
            SpellId.MagicItemExpertiseSelf1,
            SpellId.MagicItemExpertiseOther1,
            SpellId.MagicItemIgnoranceOther1,
            SpellId.WeaponExpertiseSelf1,
            SpellId.WeaponExpertiseOther1,
            SpellId.WeaponIgnoranceOther1,

            SpellId.MonsterAttunementSelf1,
            SpellId.MonsterAttunementOther1,
            SpellId.MonsterUnfamiliarityOther1,
            SpellId.PersonAttunementSelf1,
            SpellId.PersonAttunementOther1,
            SpellId.PersonUnfamiliarityOther1,

            SpellId.DispelCreatureBadSelf1,
            SpellId.DispelCreatureBadOther1,
        };

        private static readonly List<SpellId> lifeSpells = new List<SpellId>()
        { 
            // life spells
            SpellId.HealSelf1,
            SpellId.HealOther1,
            SpellId.HarmOther1,

            SpellId.RevitalizeSelf1,
            SpellId.RevitalizeOther1,
            SpellId.EnfeebleOther1,

            //SpellId.ManaBoostSelf1,       // these 2 mana boost spells exist,
            //SpellId.ManaBoostOther1,      // but they weren't learnable by the player in retail
            SpellId.ManaDrainOther1,

            SpellId.RegenerationSelf1,
            SpellId.RegenerationOther1,
            SpellId.FesterOther1,

            SpellId.RejuvenationSelf1,
            SpellId.RejuvenationOther1,
            SpellId.ExhaustionOther1,

            SpellId.ManaRenewalSelf1,
            SpellId.ManaRenewalOther1,
            SpellId.ManaDepletionOther1,

            SpellId.HealthToStaminaSelf1,
            SpellId.HealthToManaSelf1,
            SpellId.StaminaToHealthSelf1,
            SpellId.StaminaToManaSelf1,
            SpellId.ManaToHealthSelf1,
            SpellId.ManaToStaminaSelf1,

            SpellId.InfuseHealth1,
            SpellId.InfuseStamina1,
            SpellId.InfuseMana1,

            SpellId.DrainHealth1,
            SpellId.DrainStamina1,
            SpellId.DrainMana1,

            SpellId.ArmorSelf1,
            SpellId.ArmorOther1,
            SpellId.ImperilOther1,

            SpellId.BladeProtectionSelf1,
            SpellId.BladeProtectionOther1,
            SpellId.BladeVulnerabilityOther1,

            SpellId.PiercingProtectionSelf1,
            SpellId.PiercingProtectionOther1,
            SpellId.PiercingVulnerabilityOther1,

            SpellId.BludgeonProtectionSelf1,
            SpellId.BludgeonProtectionOther1,
            SpellId.BludgeonVulnerabilityOther1,

            SpellId.FireProtectionSelf1,
            SpellId.FireProtectionOther1,
            SpellId.FireVulnerabilityOther1,

            SpellId.ColdProtectionSelf1,
            SpellId.ColdProtectionOther1,
            SpellId.ColdVulnerabilityOther1,

            SpellId.AcidProtectionSelf1,
            SpellId.AcidProtectionOther1,
            SpellId.AcidVulnerabilityOther1,

            SpellId.LightningProtectionSelf1,
            SpellId.LightningProtectionOther1,
            SpellId.LightningVulnerabilityOther1,

            SpellId.HealthBolt1,
            SpellId.StaminaBolt1,
            SpellId.ManaBolt1,

            SpellId.DispelLifeBadSelf1,
            SpellId.DispelLifeBadOther1,
        };

        private static readonly List<SpellId> itemSpells = new List<SpellId>()
        {
            SpellId.Impenetrability1,
            SpellId.Brittlemail1,
            SpellId.BladeBane1,
            SpellId.BladeLure1,
            SpellId.PiercingBane1,
            SpellId.PiercingLure1,
            SpellId.BludgeonBane1,
            SpellId.BludgeonLure1,
            SpellId.FlameBane1,
            SpellId.FlameLure1,
            SpellId.FrostBane1,
            SpellId.FrostLure1,
            SpellId.AcidBane1,
            SpellId.AcidLure1,
            SpellId.LightningBane1,
            SpellId.LightningLure1,

            SpellId.BloodDrinkerSelf1,
            SpellId.BloodDrinkerOther1,
            SpellId.BloodLoather,

            SpellId.HeartSeekerSelf1,
            SpellId.HeartSeekerOther1,
            SpellId.TurnBlade1,

            SpellId.DefenderSelf1,
            SpellId.DefenderOther1,
            SpellId.LureBlade1,

            SpellId.SwiftKillerSelf1,
            SpellId.SwiftKillerOther1,
            SpellId.LeadenWeapon1,

            SpellId.HermeticLinkSelf1,
            SpellId.HermeticLinkOther1,
            SpellId.HermeticVoid1,

            SpellId.SpiritDrinkerSelf1,
            SpellId.SpiritDrinkerOther1,
            SpellId.SpiritLoather1,

            SpellId.StrengthenLock1,
            SpellId.WeakenLock1,

            SpellId.DispelItemBadOther1,
        };

        private static readonly List<SpellId> warSpells = new List<SpellId>()
        {
            SpellId.FlameBolt1,
            SpellId.FrostBolt1,
            SpellId.AcidStream1,
            SpellId.ShockWave1,
            SpellId.LightningBolt1,
            SpellId.ForceBolt1,
            SpellId.WhirlingBlade1,

            SpellId.AcidStreak1,
            SpellId.FlameStreak1,
            SpellId.ForceStreak1,
            SpellId.FrostStreak1,
            SpellId.LightningStreak1,
            SpellId.ShockwaveStreak1,
            SpellId.WhirlingBladeStreak1,

            SpellId.AcidArc1,
            SpellId.ForceArc1,
            SpellId.FrostArc1,
            SpellId.LightningArc1,
            SpellId.FlameArc1,
            SpellId.ShockArc1,
            SpellId.BladeArc1,

            SpellId.AcidBlast3,
            SpellId.ShockBlast3,
            SpellId.FrostBlast3,
            SpellId.LightningBlast3,
            SpellId.FlameBlast3,
            SpellId.ForceBlast3,
            SpellId.BladeBlast3,

            SpellId.AcidVolley3,
            SpellId.BludgeoningVolley3,
            SpellId.FrostVolley3,
            SpellId.LightningVolley3,
            SpellId.FlameVolley3,
            SpellId.ForceVolley3,
            SpellId.BladeVolley3,
        };

        private static readonly List<SpellId> voidSpells = new List<SpellId>()
        {
            SpellId.NetherBolt1,
            SpellId.NetherStreak1,
            SpellId.NetherArc1,
            SpellId.Corrosion1,
            SpellId.Corruption1,
            SpellId.CurseDestructionOther1,
            SpellId.CurseFestering1,
            SpellId.CurseWeakness1,
        };

        private static readonly List<SpellId> miscSpells = new List<SpellId>()
        {
        };

        private static readonly List<SpellId> allSpells;

        static ScrollWcids()
        {
            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                miscSpells = new List<SpellId>()
                {
                    SpellId.EmpowerSpell1,
                    SpellId.QuickcastSpell1,
                    SpellId.EnchainSpell1,
                    SpellId.ExtendSpell1,
                    SpellId.MaximizeSpell1,
                    SpellId.DelaySpell1,
                    SpellId.Blink1,
                };

                lifeSpells = new List<SpellId>()
                { 
                    // life spells
                    SpellId.HealSelf1,
                    SpellId.HealOther1,
                    SpellId.HarmOther1,

                    SpellId.RevitalizeSelf1,
                    SpellId.RevitalizeOther1,
                    SpellId.EnfeebleOther1,

                    //SpellId.ManaBoostSelf1,       // these 2 mana boost spells exist,
                    //SpellId.ManaBoostOther1,      // but they weren't learnable by the player in retail
                    SpellId.ManaDrainOther1,

                    SpellId.FesterOther1,

                    SpellId.ExhaustionOther1,

                    SpellId.ManaDepletionOther1,

                    SpellId.HealthToStaminaSelf1,
                    SpellId.HealthToManaSelf1,
                    SpellId.StaminaToHealthSelf1,
                    SpellId.StaminaToManaSelf1,
                    SpellId.ManaToHealthSelf1,
                    SpellId.ManaToStaminaSelf1,

                    SpellId.InfuseHealth1,
                    SpellId.InfuseStamina1,
                    SpellId.InfuseMana1,

                    SpellId.DrainHealth1,
                    SpellId.DrainStamina1,
                    SpellId.DrainMana1,

                    SpellId.HealthBolt1,
                    SpellId.StaminaBolt1,
                    SpellId.ManaBolt1,

                    SpellId.DispelLifeBadSelf1,
                    SpellId.DispelLifeBadOther1,

                    SpellId.Resurrect1,
                    SpellId.FellowHeal1,
                    SpellId.Ensnare,
                    SpellId.Mesmerize,
                    SpellId.AblativeArmorSelf1,
                    SpellId.AblativeArmorOther1
                };
            }
            else if (ConfigManager.Config.Server.WorldRuleset <= Ruleset.Infiltration)
            {
                creatureSpells.Add(SpellId.SpearMasterySelf1);
                creatureSpells.Add(SpellId.SpearMasteryOther1);
                creatureSpells.Add(SpellId.SpearIneptitudeOther1);

                creatureSpells.Add(SpellId.StaffMasterySelf1);
                creatureSpells.Add(SpellId.StaffMasteryOther1);
                creatureSpells.Add(SpellId.StaffIneptitudeOther1);

                creatureSpells.Add(SpellId.MaceMasterySelf1);
                creatureSpells.Add(SpellId.MaceMasteryOther1);
                creatureSpells.Add(SpellId.MaceIneptitudeOther1);

                creatureSpells.Add(SpellId.CrossbowMasterySelf1);
                creatureSpells.Add(SpellId.CrossbowMasteryOther1);
                creatureSpells.Add(SpellId.CrossbowIneptitudeOther1);

                creatureSpells.Add(SpellId.ThrownWeaponMasterySelf1);
                creatureSpells.Add(SpellId.ThrownWeaponMasteryOther1);
                creatureSpells.Add(SpellId.ThrownWeaponIneptitudeOther1);

                creatureSpells.Add(SpellId.UnarmedCombatMasterySelf1);
                creatureSpells.Add(SpellId.UnarmedCombatMasteryOther1);
                creatureSpells.Add(SpellId.UnarmedCombatIneptitudeOther1);

                creatureSpells.Remove(SpellId.TwoHandedMasterySelf1);
                creatureSpells.Remove(SpellId.TwoHandedMasteryOther1);
                creatureSpells.Remove(SpellId.TwoHandedIneptitude1);

                creatureSpells.Remove(SpellId.VoidMagicMasterySelf1);
                creatureSpells.Remove(SpellId.VoidMagicMasteryOther1);
                creatureSpells.Remove(SpellId.VoidMagicIneptitudeOther1);

                creatureSpells.Remove(SpellId.SummoningMasterySelf1);
                creatureSpells.Remove(SpellId.SummoningMasteryOther1);
                creatureSpells.Remove(SpellId.SummoningIneptitudeOther1);

                creatureSpells.Remove(SpellId.DualWieldMasterySelf1);
                creatureSpells.Remove(SpellId.DualWieldMasteryOther1);
                creatureSpells.Remove(SpellId.DualWieldIneptitudeOther1);

                creatureSpells.Remove(SpellId.DirtyFightingMasterySelf1);
                creatureSpells.Remove(SpellId.DirtyFightingMasteryOther1);
                creatureSpells.Remove(SpellId.DirtyFightingIneptitudeOther1);

                creatureSpells.Remove(SpellId.RecklessnessMasterySelf1);
                creatureSpells.Remove(SpellId.RecklessnessMasteryOther1);
                creatureSpells.Remove(SpellId.RecklessnessIneptitudeOther1);

                creatureSpells.Remove(SpellId.SneakAttackMasterySelf1);
                creatureSpells.Remove(SpellId.SneakAttackMasteryOther1);
                creatureSpells.Remove(SpellId.SneakAttackIneptitudeOther1);

                creatureSpells.Remove(SpellId.ShieldMasterySelf1);
                creatureSpells.Remove(SpellId.ShieldMasteryOther1);
                creatureSpells.Remove(SpellId.ShieldIneptitudeOther1);

                itemSpells.Remove(SpellId.BloodDrinkerOther1);
                itemSpells.Remove(SpellId.HeartSeekerOther1);
                itemSpells.Remove(SpellId.SwiftKillerOther1);
                itemSpells.Remove(SpellId.DefenderOther1);
                itemSpells.Remove(SpellId.HermeticLinkOther1);

                itemSpells.Remove(SpellId.SpiritDrinkerSelf1);
                itemSpells.Remove(SpellId.SpiritDrinkerOther1);
                itemSpells.Remove(SpellId.SpiritLoather1);
            }

            allSpells = new List<SpellId>();
            allSpells.AddRange(lifeSpells);
            allSpells.AddRange(warSpells);
            allSpells.AddRange(miscSpells);

            if (ConfigManager.Config.Server.WorldRuleset != Ruleset.CustomDM)
            {
                allSpells.AddRange(creatureSpells);
                allSpells.AddRange(itemSpells);
            }

            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
                allSpells.AddRange(voidSpells);
        }

        public static WeenieClassName Roll(TreasureDeath profile)
        {
            var spellLevel = ScrollLevelChance.Roll(profile);

            var spellId = SpellId.Undef;
            do
            {
                var rng = ThreadSafeRandom.Next(0, allSpells.Count - 1);

                spellId = allSpells[rng];

                spellId = SpellLevelProgression.GetSpellAtLevel(spellId, spellLevel);
            }
            while (spellId == SpellId.Undef); // Simple way of handling spells that start at level 3 (blasts, volleys)

            var weenie = DatabaseManager.World.GetScrollWeenie((uint)spellId);

            if (weenie == null)
            {
                log.DebugFormat("CreateRandomScroll for tier {0} and spellID of {1} returned null from the database.", profile.Tier, spellId);
                return WeenieClassName.undef;
            }

            return (WeenieClassName)weenie.WeenieClassId;
        }
    }
}
