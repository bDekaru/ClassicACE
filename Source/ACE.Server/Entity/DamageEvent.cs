using System;
using System.Collections.Generic;
using System.Linq;

using log4net;

using ACE.Common;
using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.Entity
{
    public class DamageEvent
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // factors:
        // - lifestone protection
        // - evade
        //   - offense mod (heart seeker)
        //      - accuracy mod (missile)
        //   - defense mod (defender)
        //      - stamina mod
        // - base damage / mod
        // - damage rating / mod
        //   - recklessness
        //   - sneak attack
        //   - heritage bonus
        // - damage resistance rating /mod
        // - power meter mod
        // - critical (chance % mod / critical damage mod)
        // - attribute mod
        // - armor / mod (base al, impen / bane, life armor / imperil)
        // - elemental damage bonus
        // - slayer mod
        // - resistance mod (natural, prot, vuln)
        //   - resistance cleaving
        // - shield mod
        // - rending mod

        public Creature Attacker;
        public Creature Defender;

        public CombatType CombatType;   // melee / missile / magic

        public WorldObject DamageSource;
        public DamageType DamageType;

        public WorldObject Weapon;      // the attacker's weapon. this can be different from DamageSource,
                                        // ie. for a missile attack, the missile would the DamageSource,
                                        // and the buffs would come from the Weapon

        public AttackType AttackType;   // slash / thrust / punch / kick / offhand / multistrike
        public AttackHeight AttackHeight;

        public bool LifestoneProtection;

        public float EvasionChance;
        public uint EffectiveAttackSkill;
        public uint EffectiveDefenseSkill;
        public float AccuracyMod;

        public float BlockChance;
        public uint EffectiveBlockSkill;

        public bool Evaded;
        public bool Blocked;

        public BaseDamageMod BaseDamageMod;
        public float BaseDamage { get; set; }

        public float AttributeMod;
        public float PowerMod;
        public float SlayerMod;

        public float DamageRatingBaseMod;
        public float RecklessnessMod;
        public float SneakAttackMod;
        public float HeritageMod;
        public float PkDamageMod;

        public float DamageRatingMod;

        public bool IsCritical;

        public float CriticalChance;
        public float CriticalDamageMod;

        public float CriticalDamageRatingMod;
        public float CriticalDamageResistanceRatingMod;

        public float DamageBeforeMitigation;

        public float ArmorMod;
        public float ResistanceMod;
        public float ShieldMod;
        public float WeaponResistanceMod;

        public float DamageResistanceRatingBaseMod;
        public float DamageResistanceRatingMod;
        public float PkDamageResistanceMod;

        public float DamageMitigated;
        public float DamageBlocked;

        // creature attacker
        public MotionCommand? AttackMotion;
        public AttackHook AttackHook;
        public KeyValuePair<CombatBodyPart, PropertiesBodyPart> AttackPart;      // the body part this monster is attacking with

        // creature defender
        public Quadrant Quadrant;

        public bool IgnoreMagicArmor =>  (Weapon?.IgnoreMagicArmor ?? false) || (Attacker?.IgnoreMagicArmor ?? false);      // ignores impen / banes

        public bool IgnoreMagicResist => (Weapon?.IgnoreMagicResist ?? false) || (Attacker?.IgnoreMagicResist ?? false);    // ignores life armor / prots

        public bool Overpower;


        // player defender
        public BodyPart BodyPart;
        public List<WorldObject> Armor;

        // creature defender
        public KeyValuePair<CombatBodyPart, PropertiesBodyPart> PropertiesBodyPart;
        public Creature_BodyPart CreaturePart;

        public float Damage;

        public bool GeneralFailure;

        public bool HasDamage => !Evaded && !LifestoneProtection;

        public bool CriticalDefended;

        public static HashSet<uint> AllowDamageTypeUndef = new HashSet<uint>()
        {
            22545,  // Obsidian Spines
            35191,  // Thunder Chicken
            38406,  // Blessed Moar
            38587,  // Ardent Moar
            38588,  // Blessed Moar
            38586,  // Verdant Moar
            40298,  // Ardent Moar
            40300,  // Blessed Moar
            40301,  // Verdant Moar
        };

        public static DamageEvent CalculateDamage(Creature attacker, Creature defender, WorldObject damageSource, MotionCommand? attackMotion = null, AttackHook attackHook = null)
        {
            var damageEvent = new DamageEvent();
            damageEvent.AttackMotion = attackMotion;
            damageEvent.AttackHook = attackHook;
            if (damageSource == null)
                damageSource = attacker;

            var damage = damageEvent.DoCalculateDamage(attacker, defender, damageSource);

            damageEvent.HandleLogging(attacker, defender);

            return damageEvent;
        }

        private float DoCalculateDamage(Creature attacker, Creature defender, WorldObject damageSource)
        {
            var playerAttacker = attacker as Player;
            var playerDefender = defender as Player;

            var pkBattle = playerAttacker != null && playerDefender != null;

            Attacker = attacker;
            Defender = defender;

            CombatType = damageSource.ProjectileSource == null ? CombatType.Melee : CombatType.Missile;

            DamageSource = damageSource;

            Weapon = damageSource.ProjectileSource == null ? attacker.GetEquippedMeleeWeapon() : (damageSource.ProjectileLauncher ?? damageSource.ProjectileAmmo);

            AttackType = attacker.AttackType;
            AttackHeight = attacker.AttackHeight ?? AttackHeight.Medium;

            var isAttackFromSneaking = false;
            if (playerAttacker != null)
            {
                isAttackFromSneaking = playerAttacker.IsAttackFromSneaking;
                playerAttacker.IsAttackFromSneaking = false;
            }

            // check lifestone protection
            if (playerDefender != null && playerDefender.UnderLifestoneProtection)
            {
                LifestoneProtection = true;
                playerDefender.HandleLifestoneProtection();
                return 0.0f;
            }

            if (defender.Invincible || defender.IsDead || defender.IsOnNoDamageLandblock)
                return 0.0f;

            // overpower
            if (attacker.Overpower != null)
                Overpower = Creature.GetOverpower(attacker, defender);

            // evasion chance
            if (!Overpower)
            {
                EvasionChance = GetEvadeChance(attacker, defender);
                if (attacker != defender && EvasionChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                {
                    Evaded = true;
                    return 0.0f;
                }
            }

            // get base damage
            if (playerAttacker != null)
                GetBaseDamage(playerAttacker);
            else
                GetBaseDamage(attacker, AttackMotion ?? MotionCommand.Invalid, AttackHook);

            if (DamageType == DamageType.Undef)
            {
                if ((attacker?.Guid.IsPlayer() ?? false) || (damageSource?.Guid.IsPlayer() ?? false))
                {
                    log.Error($"DamageEvent.DoCalculateDamage({attacker?.Name} ({attacker?.Guid}), {defender?.Name} ({defender?.Guid}), {damageSource?.Name} ({damageSource?.Guid})) - DamageType == DamageType.Undef");
                    GeneralFailure = true;
                }
            }

            if (GeneralFailure) return 0.0f;

            // get damage modifiers
            PowerMod = attacker.GetPowerMod(Weapon);
            AttributeMod = attacker.GetAttributeMod(Weapon);
            SlayerMod = WorldObject.GetWeaponCreatureSlayerModifier(Weapon, attacker, defender);

            // ratings
            DamageRatingBaseMod = Creature.GetPositiveRatingMod(attacker.GetDamageRating());
            RecklessnessMod = Creature.GetRecklessnessMod(attacker, defender);
            SneakAttackMod = attacker.GetSneakAttackMod(defender);
            HeritageMod = attacker.GetHeritageBonus(Weapon) ? 1.05f : 1.0f;

            TacticAndTechniqueType attackerTechniqueId = TacticAndTechniqueType.None;
            WorldObject attackerTechniqueTrinket;
            TacticAndTechniqueType defenderTechniqueId = TacticAndTechniqueType.None;
            WorldObject defenderTechniqueTrinket;

            var extraDamageMod = 1.0f;

            if (playerAttacker != null && Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                attackerTechniqueTrinket = attacker.GetEquippedTrinket();
                if (attackerTechniqueTrinket != null)
                    attackerTechniqueId = (TacticAndTechniqueType)attackerTechniqueTrinket.TacticAndTechniqueId;

                defenderTechniqueTrinket = defender.GetEquippedTrinket();
                if (defenderTechniqueTrinket != null)
                    defenderTechniqueId = (TacticAndTechniqueType)defenderTechniqueTrinket.TacticAndTechniqueId;

                if (attackerTechniqueId == TacticAndTechniqueType.Reckless)
                {
                    if (CombatType == CombatType.Melee || attacker.GetDistance(defender) < 3) // Make sure we're close to each other.
                    {
                        CreatureSkill attackerMeleeDef = playerAttacker.GetCreatureSkill(Skill.MeleeDefense);
                        CreatureSkill defenderMeleeDef = defender.GetCreatureSkill(Skill.MeleeDefense);

                        var activationChance = SkillCheck.GetSkillChance(attackerMeleeDef.Current, (uint)(defenderMeleeDef.Current * 0.75f));
                        if (activationChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                            RecklessnessMod = 1.20f; // Extra damage dealt while attacking with the Reckless technique.
                    }
                }

                if (playerAttacker.AttackHeight == AttackHeight.High) // High height attacks give players an extra 10% damage bonus.
                    extraDamageMod += 0.10f;
            }

            DamageRatingMod = Creature.AdditiveCombine(DamageRatingBaseMod, RecklessnessMod, SneakAttackMod, HeritageMod, extraDamageMod);

            if (pkBattle)
            {
                PkDamageMod = Creature.GetPositiveRatingMod(attacker.GetPKDamageRating());
                DamageRatingMod = Creature.AdditiveCombine(DamageRatingMod, PkDamageMod);
            }

            // damage before mitigation
            DamageBeforeMitigation = BaseDamage * AttributeMod * PowerMod * SlayerMod * DamageRatingMod;

            var attackSkill = attacker.GetCreatureSkill(attacker.GetCurrentWeaponSkill());

            // critical hit?
            CriticalChance = WorldObject.GetWeaponCriticalChance(Weapon, attacker, attackSkill, defender, pkBattle);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (attacker == defender)
                    CriticalChance = 0.0f; // Self-damage never crits.
                else
                {
                    if (playerAttacker != null)
                    {
                        if (CombatType != CombatType.Magic)
                        {
                            // critical chance scales with power/accuracy bar
                            CriticalChance += playerAttacker.ScaleWithPowerAccuracyBar(CriticalChance);
                        }

                        if (Weapon != null && Weapon.IsTwoHanded)
                            CriticalChance += 0.05f + playerAttacker.ScaleWithPowerAccuracyBar(0.05f);

                        if (isAttackFromSneaking)
                        {
                            CriticalChance = 1.0f;
                            if (playerDefender == null)
                                SneakAttackMod = 3.0f;
                        }
                        else if (attackerTechniqueId == TacticAndTechniqueType.Opportunist)
                        {
                            CriticalChance += 0.10f + playerAttacker.ScaleWithPowerAccuracyBar(0.10f); // Extra critical chance while using the Opportunist technique.

                            var currentTime = Time.GetUnixTime();
                            var chance = 0.2f + playerAttacker.ScaleWithPowerAccuracyBar(0.2f);
                            if (attacker != defender && playerAttacker.NextTechniqueNegativeActivationTime <= currentTime && chance > ThreadSafeRandom.Next(0.0f, 1.0f))
                            {
                                // Chance of inflicting self damage while using the Opportunist technique.
                                var modifiedInterval = Player.TechniqueNegativeActivationInterval;
                                if (Weapon != null && Weapon.IsTwoHanded)
                                    modifiedInterval /= 2;
                                playerAttacker.NextTechniqueNegativeActivationTime = currentTime + modifiedInterval;
                                playerAttacker.DamageTarget(playerAttacker, damageSource);
                            }
                        }
                    }

                    if (playerDefender != null)
                    {
                        if (defenderTechniqueId == TacticAndTechniqueType.Riposte)
                            CriticalChance += 0.10f; // Extra chance of receiving critical hits while using the Riposte technique.
                    }
                }
            }

            // https://asheron.fandom.com/wiki/Announcements_-_2002/08_-_Atonement
            // It should be noted that any time a character is logging off, PK or not, all physical attacks against them become automatically critical.
            // (Note that spells do not share this behavior.) We hope this will stress the need to log off in a safe place.

            if (playerDefender != null && (playerDefender.IsLoggingOut || playerDefender.PKLogout) && (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM || (!playerDefender.IsPK && !playerDefender.IsPKL)))
                CriticalChance = 1.0f;

            if (CriticalChance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                if (playerDefender != null && playerDefender.AugmentationCriticalDefense > 0)
                {
                    var criticalDefenseMod = playerAttacker != null ? 0.05f : 0.25f;
                    var criticalDefenseChance = playerDefender.AugmentationCriticalDefense * criticalDefenseMod;

                    if (criticalDefenseChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                        CriticalDefended = true;
                }

                if (!CriticalDefended)
                {
                    IsCritical = true;

                    // verify: CriticalMultiplier only applied to the additional crit damage,
                    // whereas CD/CDR applied to the total damage (base damage + additional crit damage)
                    CriticalDamageMod = 1.0f + WorldObject.GetWeaponCritDamageMod(Weapon, attacker, attackSkill, defender, pkBattle);

                    CriticalDamageRatingMod = Creature.GetPositiveRatingMod(attacker.GetCritDamageRating());

                    // recklessness excluded from crits
                    RecklessnessMod = 1.0f;
                    DamageRatingMod = Creature.AdditiveCombine(DamageRatingBaseMod, CriticalDamageRatingMod, SneakAttackMod, HeritageMod, extraDamageMod);

                    if (pkBattle)
                        DamageRatingMod = Creature.AdditiveCombine(DamageRatingMod, PkDamageMod);

                    DamageBeforeMitigation = BaseDamageMod.MaxDamage * AttributeMod * PowerMod * SlayerMod * DamageRatingMod * CriticalDamageMod;
                }
            }

            // armor rending and cleaving
            var armorRendingMod = 1.0f;
            if (Weapon != null && Weapon.HasImbuedEffect(ImbuedEffectType.ArmorRending))
                armorRendingMod = WorldObject.GetArmorRendingMod(attackSkill, pkBattle);

            var armorCleavingMod = attacker.GetArmorCleavingMod(Weapon, attackSkill, pkBattle);

            var ignoreArmorMod = Math.Min(armorRendingMod, armorCleavingMod);

            // get body part / armor pieces / armor modifier
            if (playerDefender != null)
            {
                // select random body part @ current attack height
                GetBodyPart(AttackHeight);

                // get player armor pieces
                Armor = attacker.GetArmorLayers(playerDefender, BodyPart);

                // get armor modifiers
                ArmorMod = attacker.GetArmorMod(playerDefender, DamageType, Armor, Weapon, ignoreArmorMod, pkBattle);
            }
            else
            {
                // determine height quadrant
                Quadrant = GetQuadrant(Defender, Attacker, AttackHeight, DamageSource);

                // select random body part @ current attack height
                GetBodyPart(Defender, Quadrant);
                if (Evaded)
                    return 0.0f;

                Armor = CreaturePart.GetArmorLayers(PropertiesBodyPart.Key);

                // get target armor
                ArmorMod = CreaturePart.GetArmorMod(DamageType, Armor, Attacker, Weapon, ignoreArmorMod);
            }

            if (Weapon != null && Weapon.HasImbuedEffect(ImbuedEffectType.IgnoreAllArmor))
                ArmorMod = 1.0f;

            // get resistance modifiers
            WeaponResistanceMod = WorldObject.GetWeaponResistanceModifier(Weapon, attacker, attackSkill, DamageType);

            if (playerDefender != null)
            {
                ResistanceMod = playerDefender.GetResistanceMod(DamageType, Attacker, Weapon, WeaponResistanceMod);
            }
            else
            {
                var resistanceType = Creature.GetResistanceType(DamageType);
                ResistanceMod = (float)Math.Max(0.0f, defender.GetResistanceMod(resistanceType, Attacker, Weapon, WeaponResistanceMod));
            }

            // damage resistance rating
            DamageResistanceRatingMod = DamageResistanceRatingBaseMod = defender.GetDamageResistRatingMod(CombatType);

            if (IsCritical)
            {
                CriticalDamageResistanceRatingMod = Creature.GetNegativeRatingMod(defender.GetCritDamageResistRating());

                DamageResistanceRatingMod = Creature.AdditiveCombine(DamageResistanceRatingBaseMod, CriticalDamageResistanceRatingMod);
            }

            if (pkBattle)
            {
                PkDamageResistanceMod = Creature.GetNegativeRatingMod(defender.GetPKDamageResistRating());

                DamageResistanceRatingMod = Creature.AdditiveCombine(DamageResistanceRatingMod, PkDamageResistanceMod);
            }

            // get shield modifier
            ShieldMod = defender.GetShieldMod(attacker, DamageType, Weapon, pkBattle, out var shield);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && shield != null)
            {
                BlockChance = GetBlockChance(attacker, defender, shield, attackSkill);

                if (attacker != defender && BlockChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                    Blocked = true;
                else
                {
                    Blocked = false;
                    ShieldMod = 2.0f;
                }
            }

            var damageBeforeShieldMod = DamageBeforeMitigation * ArmorMod * ResistanceMod * DamageResistanceRatingMod;

            // calculate final output damage
            Damage = damageBeforeShieldMod * ShieldMod;

            if (attacker == defender)
                Damage *= 1.33f; // Self-damage does extra damage.

            if (pkBattle)
            {
                try
                {
                    float pvpMod = (float)PropertyManager.GetDouble("pvp_dmg_mod").Item;

                    switch (attackSkill.Skill)
                    {
                        case Skill.LightWeapons:
                        case Skill.Axe:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_axe").Item;
                            break;
                        case Skill.HeavyWeapons:
                        case Skill.Sword:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_sword").Item;
                            break;
                        case Skill.Mace:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_mace").Item;
                            break;
                        case Skill.Spear:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_spear").Item;
                            break;
                        case Skill.Staff:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_staff").Item;
                            break;
                        case Skill.UnarmedCombat:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_unarmed").Item;
                            break;
                        case Skill.FinesseWeapons:
                        case Skill.Dagger:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_dagger").Item;
                            break;
                        case Skill.MissileWeapons:
                        case Skill.Bow:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_bow").Item;
                            break;
                        case Skill.Crossbow:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_crossbow").Item;
                            break;
                        case Skill.ThrownWeapon:
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_thrown").Item;
                            break;
                    }

                    if (Weapon != null)
                    {
                        if (Weapon.IgnoreMagicArmor && Weapon.IgnoreMagicResist)
                        {
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_hollow").Item;
                            switch (attackSkill.Skill)
                            {
                                case Skill.LightWeapons:
                                case Skill.Axe:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_axe_hollow").Item;
                                    break;
                                case Skill.HeavyWeapons:
                                case Skill.Sword:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_sword_hollow").Item;
                                    break;
                                case Skill.Mace:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_mace_hollow").Item;
                                    break;
                                case Skill.Spear:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_spear_hollow").Item;
                                    break;
                                case Skill.Staff:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_staff_hollow").Item;
                                    break;
                                case Skill.UnarmedCombat:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_unarmed_hollow").Item;
                                    break;
                                case Skill.FinesseWeapons:
                                case Skill.Dagger:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_dagger_hollow").Item;
                                    break;
                                case Skill.MissileWeapons:
                                case Skill.Bow:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_bow_hollow").Item;
                                    break;
                                case Skill.Crossbow:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_crossbow_hollow").Item;
                                    break;
                                case Skill.ThrownWeapon:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_thrown_hollow").Item;
                                    break;

                            }
                        }
                        else if (Weapon.HasImbuedEffect(ImbuedEffectType.IgnoreAllArmor))
                        {
                            pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_phantom").Item;
                            switch (attackSkill.Skill)
                            {
                                case Skill.LightWeapons:
                                case Skill.Axe:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_axe_phantom").Item;
                                    break;
                                case Skill.HeavyWeapons:
                                case Skill.Sword:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_sword_phantom").Item;
                                    break;
                                case Skill.Mace:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_mace_phantom").Item;
                                    break;
                                case Skill.Spear:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_spear_phantom").Item;
                                    break;
                                case Skill.Staff:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_staff_phantom").Item;
                                    break;
                                case Skill.UnarmedCombat:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_unarmed_phantom").Item;
                                    break;
                                case Skill.FinesseWeapons:
                                case Skill.Dagger:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_dagger_phantom").Item;
                                    break;
                                case Skill.MissileWeapons:
                                case Skill.Bow:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_bow_phantom").Item;
                                    break;
                                case Skill.Crossbow:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_crossbow_phantom").Item;
                                    break;
                                case Skill.ThrownWeapon:
                                    pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_thrown_phantom").Item;
                                    break;
                            }
                        }
                    }

                    if (SneakAttackMod > 1.0)
                        pvpMod *= (float)PropertyManager.GetDouble("pvp_dmg_mod_sneak").Item;

                    Damage = Damage * pvpMod;
                }
                catch (Exception ex)
                {
                    log.Error($"Failed applying server configured pvp mods. Ex: {ex}");
                }
            }

            DamageMitigated = DamageBeforeMitigation - Damage;
            if(Blocked)
                DamageBlocked = damageBeforeShieldMod - Damage;

            return Damage;
        }

        public Quadrant GetQuadrant(Creature defender, Creature attacker, AttackHeight attackHeight, WorldObject damageSource)
        {
            var quadrant = attackHeight.ToQuadrant();

            var wo = damageSource.CurrentLandblock != null ? damageSource : attacker;

            quadrant |= wo.GetRelativeDir(defender);

            return quadrant;
        }

        /// <summary>
        /// Returns the chance for creature to avoid monster attack
        /// </summary>
        public float GetEvadeChance(Creature attacker, Creature defender)
        {
            Player playerAttacker = attacker as Player;
            Player playerDefender = defender as Player;
            bool isPvP = playerAttacker != null && playerDefender != null;

            AccuracyMod = attacker.GetAccuracyMod(Weapon);

            EffectiveAttackSkill = attacker.GetEffectiveAttackSkill();

            //var attackType = attacker.GetCombatType();

            EffectiveDefenseSkill = defender.GetEffectiveDefenseSkill(CombatType, isPvP);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (playerAttacker != null)
                {
                    if (playerAttacker.AttackHeight == AttackHeight.Medium) // Medium height attacks gives players 10% extra attack skill.
                        EffectiveAttackSkill = (uint)Math.Round(EffectiveAttackSkill * 1.10f);
                }

                if (playerDefender != null)
                {
                    var defenderTechnique = playerDefender.GetEquippedTrinket();
                    if (defenderTechnique != null && defenderTechnique.TacticAndTechniqueId == (int)TacticAndTechniqueType.Reckless)
                        return 0.0f; // No evasion while using Reckless technique.

                    if (playerDefender != null && playerDefender.AttackHeight == AttackHeight.Low) // While using low height attacks players get an extra defence skill bonus.
                        EffectiveDefenseSkill = (uint)Math.Round(EffectiveDefenseSkill * 1.10f);
                }

                // Evasion penalty for receiving too many attacks per second.
                if (defender.attacksReceivedPerSecond > 0.0f && Defender.AttackTarget != attacker) // But we still have full evasion chance against our attack target.
                    EffectiveDefenseSkill = (uint)Math.Round(EffectiveDefenseSkill * (1.0f - Math.Min(1.0f, defender.attacksReceivedPerSecond / 20.0f)));
            }

            var evadeChance = 1.0f - SkillCheck.GetSkillChance(EffectiveAttackSkill, EffectiveDefenseSkill);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && playerDefender != null)
                evadeChance = Math.Min(evadeChance, 0.95f + ((CombatType == CombatType.Missile ? playerDefender.CachedMissileDefenseCapBonus : playerDefender.CachedMeleeDefenseCapBonus)) * 0.01);


            return (float)evadeChance;
        }

        public float GetBlockChance(Creature attacker, Creature defender, WorldObject shield, CreatureSkill attackSkill)
        {
            if (defender == null || defender.IsExhausted)
                return 0.0f;

            var playerAttacker = attacker as Player;
            var playerDefender = defender as Player;
            var isPvP = playerAttacker != null && playerDefender != null;
            if (playerDefender == null)
                return 1.0f; // Creatures with shields always block.
            else
            {
                var shieldSkill = defender.GetCreatureSkill(Skill.Shield);
                if (shieldSkill.AdvancementClass > SkillAdvancementClass.Untrained)
                {
                    var shieldDefenseMod = (float)(shield.ShieldDefense ?? 1) + shield.EnchantmentManager.GetShieldDefenseMod();

                    if (shield.IsEnchantable)
                        shieldDefenseMod += defender.EnchantmentManager.GetAttackMod();

                    EffectiveBlockSkill = (uint)Math.Round(shieldSkill.Current * shieldDefenseMod);
                }
                else
                    EffectiveBlockSkill = 0;

                var combatTypeMod = CombatType == CombatType.Missile ? 2.0f : 1.333f;
                EffectiveBlockSkill = (uint)(EffectiveBlockSkill * combatTypeMod);

                var blockChance = 5.0f - SkillCheck.GetSkillChance(attackSkill.Current, EffectiveBlockSkill);

                if (CombatType == CombatType.Missile)
                    blockChance += blockChance * shield.GetShieldMissileBlockBonus();

                if (isPvP)
                    blockChance *= (float)PropertyManager.GetDouble("pvp_dmg_mod_shield_block_chance").Item;

                return (float)blockChance;
            }
        }

        /// <summary>
        /// Returns the base damage for a player attacker
        /// </summary>
        public void GetBaseDamage(Player attacker)
        {
            if (DamageSource.ItemType == ItemType.MissileWeapon)
            {
                DamageType = DamageSource.W_DamageType;

                // handle prismatic arrows
                if (DamageType == DamageType.Base)
                {
                    if (Weapon != null && Weapon.W_DamageType != DamageType.Undef)
                        DamageType = Weapon.W_DamageType;
                    else
                        DamageType = DamageType.Pierce;
                }
            }
            else
                DamageType = attacker.GetDamageType(false, CombatType.Melee);

            // TODO: combat maneuvers for player?
            BaseDamageMod = attacker.GetBaseDamageMod(DamageSource);

            BaseDamageMod.BaseDamage.MaxDamage += attacker.GetUnarmedSkillDamageBonus();

            // some quest bows can have built-in damage bonus
            if (Weapon?.WeenieType == WeenieType.MissileLauncher)
                BaseDamageMod.DamageBonus += Weapon.Damage ?? 0;

            if (DamageSource.ItemType == ItemType.MissileWeapon)
                BaseDamageMod.ElementalBonus = WorldObject.GetMissileElementalDamageBonus(Weapon, attacker, DamageType);

            BaseDamage = (float)ThreadSafeRandom.Next(BaseDamageMod.MinDamage, BaseDamageMod.MaxDamage);
        }

        /// <summary>
        /// Returns the base damage for a non-player attacker
        /// </summary>
        public void GetBaseDamage(Creature attacker, MotionCommand motionCommand, AttackHook attackHook)
        {
            AttackPart = attacker.GetAttackPart(motionCommand, attackHook);
            if (AttackPart.Value == null)
            {
                GeneralFailure = true;
                return;
            }

            BaseDamageMod = attacker.GetBaseDamage(AttackPart.Value);

            BaseDamageMod.BaseDamage.MaxDamage += attacker.GetUnarmedSkillDamageBonus();

            BaseDamage = (float)ThreadSafeRandom.Next(BaseDamageMod.MinDamage, BaseDamageMod.MaxDamage);

            DamageType = attacker.GetDamageType(AttackPart.Value, CombatType);
        }

        /// <summary>
        /// Returns a body part for a player defender
        /// </summary>
        public void GetBodyPart(AttackHeight attackHeight)
        {
            // select random body part @ current attack height
            BodyPart = BodyParts.GetBodyPart(attackHeight);
        }

        public static readonly Quadrant LeftRight = Quadrant.Left | Quadrant.Right;
        public static readonly Quadrant FrontBack = Quadrant.Front | Quadrant.Back;

        /// <summary>
        /// Returns a body part for a creature defender
        /// </summary>
        public void GetBodyPart(Creature defender, Quadrant quadrant)
        {
            // get cached body parts table
            var bodyParts = !defender.IsModified ? Creature.GetBodyParts(defender.WeenieClassId) : Creature.GetBodyParts(defender); // If we're modified(our level has been altered) get our custom body parts instead of the generic one.

            // rng roll for body part
            var bodyPart = bodyParts.RollBodyPart(quadrant);

            if (bodyPart == CombatBodyPart.Undefined)
            {
                log.Debug($"DamageEvent.GetBodyPart({defender?.Name} ({defender?.Guid}) ) - couldn't find body part for wcid {defender.WeenieClassId}, Quadrant {quadrant}");
                Evaded = true;
                return;
            }

            //Console.WriteLine($"AttackHeight: {AttackHeight}, Quadrant: {quadrant & FrontBack}{quadrant & LeftRight}, AttackPart: {bodyPart}");

            defender.Biota.PropertiesBodyPart.TryGetValue(bodyPart, out var value);
            PropertiesBodyPart = new KeyValuePair<CombatBodyPart, PropertiesBodyPart>(bodyPart, value);

            // select random body part @ current attack height
            /*BiotaPropertiesBodyPart = BodyParts.GetBodyPart(defender, attackHeight);

            if (BiotaPropertiesBodyPart == null)
            {
                Evaded = true;
                return;
            }*/

            CreaturePart = new Creature_BodyPart(defender, PropertiesBodyPart);
        }

        public void ShowInfo(Creature creature)
        {
            var targetInfo = PlayerManager.GetOnlinePlayer(creature.DebugDamageTarget);
            if (targetInfo == null)
            {
                creature.DebugDamage = Creature.DebugDamageType.None;
                return;
            }

            // setup
            var info = $"Attacker: {Attacker.Name} ({Attacker.Guid})\n";
            info += $"Defender: {Defender.Name} ({Defender.Guid})\n";

            info += $"CombatType: {CombatType}\n";

            info += $"DamageSource: {DamageSource.Name} ({DamageSource.Guid})\n";
            info += $"DamageType: {DamageType}\n";

            var weaponName = Weapon != null ? $"{Weapon.Name} ({Weapon.Guid})" : "None\n";
            info += $"Weapon: {weaponName}\n";

            info += $"AttackType: {AttackType}\n";
            info += $"AttackHeight: {AttackHeight}\n";

            // lifestone protection
            if (LifestoneProtection)
                info += $"LifestoneProtection: {LifestoneProtection}\n";

            // evade
            if (AccuracyMod != 0.0f && AccuracyMod != 1.0f)
                info += $"AccuracyMod: {AccuracyMod}\n";

            info += $"EffectiveAttackSkill: {EffectiveAttackSkill}\n";
            info += $"EffectiveDefenseSkill: {EffectiveDefenseSkill}\n";

            if (Attacker.Overpower != null)
                info += $"Overpower: {Overpower} ({Creature.GetOverpowerChance(Attacker, Defender)})\n";

            info += $"EvasionChance: {EvasionChance}\n";
            info += $"Evaded: {Evaded}\n";

            if (!(Attacker is Player))
            {
                if (AttackMotion != null)
                    info += $"AttackMotion: {AttackMotion}\n";
                if (AttackPart.Value != null)
                    info += $"AttackPart: {AttackPart.Key}\n";
            }

            // base damage
            if (BaseDamageMod != null)
                info += $"BaseDamageRange: {BaseDamageMod.Range}\n";


            info += $"BaseDamage: {BaseDamage}\n";

            // damage modifiers
            info += $"AttributeMod: {AttributeMod}\n";

            if (PowerMod != 0.0f && PowerMod != 1.0f)
                info += $"PowerMod: {PowerMod}\n";

            if (SlayerMod != 0.0f && SlayerMod != 1.0f)
                info += $"SlayerMod: {SlayerMod}\n";

            if (BaseDamageMod != null)
            {
                if (BaseDamageMod.DamageBonus != 0)
                    info += $"DamageBonus: {BaseDamageMod.DamageBonus}\n";

                if (BaseDamageMod.DamageMod != 0.0f && BaseDamageMod.DamageMod != 1.0f)
                    info += $"DamageMod: {BaseDamageMod.DamageMod}\n";

                if (BaseDamageMod.ElementalBonus != 0)
                    info += $"ElementalDamageBonus: {BaseDamageMod.ElementalBonus}\n";
            }

            // critical hit
            info += $"CriticalChance: {CriticalChance}\n";
            info += $"CriticalHit: {IsCritical}\n";

            if (CriticalDefended)
                info += $"CriticalDefended: {CriticalDefended}\n";

            if (CriticalDamageMod != 0.0f && CriticalDamageMod != 1.0f)
                info += $"CriticalDamageMod: {CriticalDamageMod}\n";

            if (CriticalDamageRatingMod != 0.0f && CriticalDamageRatingMod != 1.0f)
                info += $"CriticalDamageRatingMod: {CriticalDamageRatingMod}\n";

            // damage ratings
            if (DamageRatingBaseMod != 0.0f && DamageRatingBaseMod != 1.0f)
                info += $"DamageRatingBaseMod: {DamageRatingBaseMod}\n";

            if (HeritageMod != 0.0f && HeritageMod != 1.0f)
                info += $"HeritageMod: {HeritageMod}\n";

            if (RecklessnessMod != 0.0f && RecklessnessMod != 1.0f)
                info += $"RecklessnessMod: {RecklessnessMod}\n";

            if (SneakAttackMod != 0.0f && SneakAttackMod != 1.0f)
                info += $"SneakAttackMod: {SneakAttackMod}\n";

            if (PkDamageMod != 0.0f && PkDamageMod != 1.0f)
                info += $"PkDamageMod: {PkDamageMod}\n";

            if (DamageRatingMod != 0.0f && DamageRatingMod != 1.0f)
                info += $"DamageRatingMod: {DamageRatingMod}\n";

            if (BodyPart != 0)
            {
                // player body part
                info += $"BodyPart: {BodyPart}\n";
            }
            if (Armor != null && Armor.Count > 0)
            {
                info += $"Armors: {string.Join(", ", Armor.Select(i => i.Name))}\n";
            }

            if (CreaturePart != null)
            {
                // creature body part
                info += $"BodyPart: {PropertiesBodyPart.Key}\n";
                info += $"BaseArmor: {CreaturePart.Biota.Value.BaseArmor}\n";
            }

            // damage mitigation
            if (ArmorMod != 0.0f && ArmorMod != 1.0f)
                info += $"ArmorMod: {ArmorMod}\n";

            if (ResistanceMod != 0.0f && ResistanceMod != 1.0f)
                info += $"ResistanceMod: {ResistanceMod}\n";

            if (ShieldMod != 0.0f && ShieldMod != 1.5f)
                info += $"ShieldMod: {ShieldMod}\n";

            if (WeaponResistanceMod != 0.0f && WeaponResistanceMod != 1.0f)
                info += $"WeaponResistanceMod: {WeaponResistanceMod}\n";

            if (DamageResistanceRatingBaseMod != 0.0f && DamageResistanceRatingBaseMod != 1.0f)
                info += $"DamageResistanceRatingBaseMod: {DamageResistanceRatingBaseMod}\n";

            if (CriticalDamageResistanceRatingMod != 0.0f && CriticalDamageResistanceRatingMod != 1.0f)
                info += $"CriticalDamageResistanceRatingMod: {CriticalDamageResistanceRatingMod}\n";

            if (PkDamageResistanceMod != 0.0f && PkDamageResistanceMod != 1.0f)
                info += $"PkDamageResistanceMod: {PkDamageResistanceMod}\n";

            if (DamageResistanceRatingMod != 0.0f && DamageResistanceRatingMod != 1.0f)
                info += $"DamageResistanceRatingMod: {DamageResistanceRatingMod}\n";

            if (IgnoreMagicArmor)
                info += $"IgnoreMagicArmor: {IgnoreMagicArmor}\n";
            if (IgnoreMagicResist)
                info += $"IgnoreMagicResist: {IgnoreMagicResist}\n";

            // final damage
            info += $"DamageBeforeMitigation: {DamageBeforeMitigation}\n";
            info += $"DamageMitigated: {DamageMitigated}\n";
            info += $"Damage: {Damage}\n";

            info += $"Block Chance: {BlockChance}\n";
            info += $"Blocked: {Blocked}\n";
            info += $"Damage Blocked: {DamageBlocked}\n";

            info += "----";

            targetInfo.Session.Network.EnqueueSend(new GameMessageSystemChat(info, ChatMessageType.Broadcast));
        }

        public void HandleLogging(Creature attacker, Creature defender)
        {
            if (attacker != null && (attacker.DebugDamage & Creature.DebugDamageType.Attacker) != 0)
            {
                ShowInfo(attacker);
            }
            if (defender != null && (defender.DebugDamage & Creature.DebugDamageType.Defender) != 0)
            {
                ShowInfo(defender);
            }
        }

        public AttackConditions AttackConditions
        {
            get
            {
                var attackConditions = new AttackConditions();

                if (CriticalDefended)
                    attackConditions |= AttackConditions.CriticalProtectionAugmentation;
                if (RecklessnessMod > 1.0f)
                    attackConditions |= AttackConditions.Recklessness;
                if (SneakAttackMod > 1.0f)
                    attackConditions |= AttackConditions.SneakAttack;
                if (Overpower)
                    attackConditions |= AttackConditions.Overpower;

                return attackConditions;
            }
        }
    }
}
