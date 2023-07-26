using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Structure;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public enum DebugDamageType
        {
            None = 0x0,
            Attacker = 0x1,
            Defender = 0x2,
            All = Attacker | Defender
        };

        public DebugDamageType DebugDamage;

        public ObjectGuid DebugDamageTarget;

        /// <summary>
        /// The list of combat maneuvers performable by this creature
        /// </summary>
        public DatLoader.FileTypes.CombatManeuverTable CombatTable { get; set; }

        public CombatMode CombatMode { get; protected set; }

        public AttackType AttackType { get; set; }

        public DamageHistory DamageHistory { get; private set; }

        /// <summary>
        /// Handles queueing up multiple animation sequences between packets
        /// ie., when a player switches from bow to sword combat,
        /// the client will send an unwield item packet for the bow first,
        /// queueing up a switch to peace mode, and then unarmed combat mode.
        /// next the client will send a wield item packet for the sword,
        /// queueing up the switch from unarmed combat -> peace mode -> bow combat
        /// </summary>
        public double LastWeaponSwap;

        public float SetCombatMode(CombatMode combatMode)
        {
            return SetCombatMode(combatMode, out var _);
        }

        /// <summary>
        /// Switches a player or creature to a new combat stance
        /// </summary>
        public float SetCombatMode(CombatMode combatMode, out float queueTime, bool forceHandCombat = false, bool animOnly = false)
        {
            // check if combat stance actually needs switching
            var combatStance = forceHandCombat ? MotionStance.HandCombat : GetCombatStance();

            //Console.WriteLine($"{Name}.SetCombatMode({combatMode}), CombatStance: {combatStance}");

            if (combatMode != CombatMode.NonCombat && CurrentMotionState.Stance == combatStance)
            {
                queueTime = 0.0f;
                return 0.0f;
            }

            if (CombatMode == CombatMode.Missile)
                HideAmmo();

            if (!animOnly)
                CombatMode = combatMode;

            var animLength = 0.0f;

            switch (combatMode)
            {
                case CombatMode.NonCombat:
                    animLength = HandleSwitchToPeaceMode();
                    break;
                case CombatMode.Melee:
                    animLength = HandleSwitchToMeleeCombatMode(forceHandCombat);
                    break;
                case CombatMode.Magic:
                    animLength = HandleSwitchToMagicCombatMode();
                    break;
                case CombatMode.Missile:
                    animLength = HandleSwitchToMissileCombatMode();
                    break;
                default:
                    log.InfoFormat($"Unknown combat mode {CombatMode} for {Name}");
                    break;
            }

            queueTime = HandleStanceQueue(animLength);

            //Console.WriteLine($"SetCombatMode(): queueTime({queueTime}) + animLength({animLength})");
            return queueTime + animLength;
        }

        /// <summary>
        /// Switches a player or creature to non-combat mode
        /// </summary>
        public float HandleSwitchToPeaceMode()
        {
            var animLength = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Ready, MotionCommand.NonCombat);

            var motion = new Motion(MotionStance.NonCombat);
            ExecuteMotionPersist(motion);

            var player = this as Player;
            if (player != null)
            {
                player.stance = MotionStance.NonCombat;
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat));
            }

            //Console.WriteLine("HandleSwitchToPeaceMode() - animLength: " + animLength);
            return animLength;
        }

        /// <summary>
        /// Handles switching between combat stances:
        /// old style -> peace mode -> hand combat (weapon swap) -> peace mode -> new style
        /// </summary>
        public float SwitchCombatStyles()
        {
            if (CurrentMotionState.Stance == MotionStance.NonCombat || CurrentMotionState.Stance == MotionStance.Invalid || IsMonster)
                return 0.0f;

            var combatStance = GetCombatStance();

            float peace1 = 0.0f, unarmed = 0.0f, peace2 = 0.0f;

            // this is now handled as a proper 2-step process in HandleActionChangeCombatMode / NextUseTime

            // FIXME: just call generic method to switch to HandCombat first
            peace1 = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Ready, MotionCommand.NonCombat);
            /*if (CurrentMotionState.Stance != MotionStance.HandCombat && combatStance != MotionStance.HandCombat)
            {
                unarmed = MotionTable.GetAnimationLength(MotionTableId, MotionStance.NonCombat, MotionCommand.Ready, MotionCommand.HandCombat);
                peace2 = MotionTable.GetAnimationLength(MotionTableId, MotionStance.HandCombat, MotionCommand.Ready, MotionCommand.NonCombat);
            }*/

            SetStance(MotionStance.NonCombat, false);

            //Console.WriteLine($"SwitchCombatStyle() - animLength: {animLength}");
            //Console.WriteLine($"SwitchCombatStyle() - peace1({peace1}) + unarmed({unarmed}) + peace2({peace2})");
            var animLength = peace1 + unarmed + peace2;
            return animLength;
        }

        /// <summary>
        /// Switches a player or creature to melee attack stance
        /// </summary>
        public float HandleSwitchToMeleeCombatMode(bool forceHandCombat = false)
        {
            // get appropriate combat stance for currently wielded items
            var combatStance = forceHandCombat ? MotionStance.HandCombat : GetCombatStance();

            var animLength = SwitchCombatStyles();
            animLength += MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Ready, (MotionCommand)combatStance);

            var motion = new Motion(combatStance);
            ExecuteMotionPersist(motion);

            var player = this as Player;
            if (player != null)
            {
                player.HandleActionTradeSwitchToCombatMode(player.Session);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.Melee));
            }

            //Console.WriteLine("HandleSwitchToMeleeCombatMode() - animLength: " + animLength);
            return animLength;
        }

        /// <summary>
        /// Switches a player or creature to magic casting stance
        /// </summary>
        public float HandleSwitchToMagicCombatMode()
        {
            var wand = GetEquippedWand();
            if (wand == null) return 0.0f;

            var animLength = SwitchCombatStyles();
            animLength += MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, MotionCommand.Ready, MotionCommand.Magic);

            var motion = new Motion(MotionStance.Magic);
            ExecuteMotionPersist(motion);

            var player = this as Player;
            if (player != null)
            {
                player.HandleActionTradeSwitchToCombatMode(player.Session);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.Magic));
            }

            //Console.WriteLine("HandleSwitchToMagicCombatMode() - animLength: " + animLength);
            return animLength;
        }

        /// <summary>
        /// Switches a player or creature to a missile combat stance
        /// </summary>
        public float HandleSwitchToMissileCombatMode()
        {
            // get appropriate combat stance for currently wielded items
            var weapon = GetEquippedMissileWeapon();
            if (weapon == null) return 0.0f;

            var combatStance = GetCombatStance();

            var swapTime = SwitchCombatStyles();

            var motion = new Motion(combatStance);
            var stanceTime = ExecuteMotionPersist(motion);

            var ammo = GetEquippedAmmo();
            var reloadTime = 0.0f;
            if (ammo != null && weapon.IsAmmoLauncher)
            {
                // bug for bow-wielding skeletons starting from decomposed state:
                // sleep -> wakeup anim time must be passed in here
                var actionChain = new ActionChain();

                var currentTime = Time.GetUnixTime();
                var queueTime = 0.0f;
                if (currentTime < LastWeaponSwap)
                    queueTime += (float)(LastWeaponSwap - currentTime);

                actionChain.AddDelaySeconds(queueTime + swapTime + stanceTime);
                reloadTime = ReloadMissileAmmo(actionChain);
                actionChain.EnqueueChain();
            }

            var player = this as Player;
            if (player != null)
            {
                player.HandleActionTradeSwitchToCombatMode(player.Session);
                player.Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.Missile));
            }
            //Console.WriteLine("HandleSwitchToMissileCombatMode() - animLength: " + animLength);
            return swapTime + stanceTime + reloadTime;
        }

        /// <summary>
        /// Sends the message to hide the current equipped ammo
        /// </summary>
        public void HideAmmo()
        {
            var ammo = GetEquippedAmmo();
            if (ammo != null)
                EnqueueBroadcast(new GameMessagePickupEvent(ammo));
        }

        /// <summary>
        /// Returns the combat stance for the currently wielded items
        /// </summary>
        public MotionStance GetCombatStance()
        {
            var caster = GetEquippedWand();

            if (caster != null)
                return MotionStance.Magic;

            var weapon = GetEquippedWeapon(true);
            var dualWield = GetDualWieldWeapon();
            var shield = GetEquippedShield();

            var combatStance = MotionStance.HandCombat;

            if (weapon != null)
                combatStance = GetWeaponStance(weapon);

            if (dualWield != null)
                combatStance = MotionStance.DualWieldCombat;

            if (shield != null)
                combatStance = AddShieldStance(combatStance);

            return combatStance;
        }

        /// <summary>
        /// Translates the default combat style for a weapon
        /// into a combat motion stance
        /// </summary>
        public MotionStance GetWeaponStance(WorldObject weapon)
        {
            var combatStance = MotionStance.HandCombat;

            switch (weapon.DefaultCombatStyle)
            {
                case CombatStyle.Atlatl:
                    combatStance = MotionStance.AtlatlCombat;
                    break;
                case CombatStyle.Bow:
                    combatStance = MotionStance.BowCombat;
                    break;
                case CombatStyle.Crossbow:
                    combatStance = MotionStance.CrossbowCombat;
                    break;
                case CombatStyle.DualWield:
                    combatStance = MotionStance.DualWieldCombat;
                    break;
                case CombatStyle.Magic:
                    combatStance = MotionStance.Magic;
                    break;
                case CombatStyle.OneHanded:
                    combatStance = MotionStance.SwordCombat;
                    break;
                case CombatStyle.OneHandedAndShield:
                    combatStance = MotionStance.SwordShieldCombat;
                    break;
                case CombatStyle.Sling:
                    combatStance = MotionStance.SlingCombat;
                    break;
                case CombatStyle.ThrownShield:
                    combatStance = MotionStance.ThrownShieldCombat;
                    break;
                case CombatStyle.ThrownWeapon:
                    combatStance = MotionStance.ThrownWeaponCombat;
                    break;
                case CombatStyle.TwoHanded:
                    // MotionStance.TwoHandedStaffCombat doesn't appear to do anything
                    // Additionally, PropertyInt.WeaponType isn't always included, and the 2handed weapons that do appear to use WeaponType.TwoHanded
                    combatStance = MotionStance.TwoHandedSwordCombat;
                    break;
                case CombatStyle.Unarmed:
                    combatStance = MotionStance.HandCombat;
                    break;
                default:
                    Console.WriteLine($"{Name}.GetCombatStance() - {weapon.DefaultCombatStyle}");
                    break;
            }
            return combatStance;
        }

        /// <summary>
        /// Adds the shield stance to an existing combat stance
        /// </summary>
        public MotionStance AddShieldStance(MotionStance combatStance)
        {
            switch (combatStance)
            {
                case MotionStance.SwordCombat:
                    combatStance = MotionStance.SwordShieldCombat;
                    break;
                case MotionStance.ThrownWeaponCombat:
                    GetCombatTable();
                    if (CombatTable.Stances.ContainsKey(MotionStance.ThrownShieldCombat))
                        combatStance = MotionStance.ThrownShieldCombat;
                    else
                        combatStance = MotionStance.ThrownWeaponCombat;
                    break;
            }
            return combatStance;
        }

        /// <summary>
        /// Adds queued weapon swaps to the current animation time
        /// </summary>
        public float HandleStanceQueue(float animLength)
        {
            var currentTime = Time.GetUnixTime();
            if (currentTime >= LastWeaponSwap)
            {
                LastWeaponSwap = currentTime + animLength;
                return 0.0f;
            }
            else
            {
                LastWeaponSwap += animLength;
                return (float)(LastWeaponSwap - currentTime - animLength);
            }
        }

        Skill CachedHighestMeleeSkill = Skill.None;
        Skill CachedHighestMissileSkill = Skill.None;

        /// <summary>
        /// Returns the highest melee skill for the player
        /// (light / heavy / finesse)
        /// </summary>
        public Skill GetHighestMeleeSkill()
        {
            Entity.CreatureSkill maxMelee;
            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
            {
                var light = GetCreatureSkill(Skill.LightWeapons);
                var heavy = GetCreatureSkill(Skill.HeavyWeapons);
                var finesse = GetCreatureSkill(Skill.FinesseWeapons);

                maxMelee = light;
                if (heavy.Current > maxMelee.Current)
                    maxMelee = heavy;
                if (finesse.Current > maxMelee.Current)
                    maxMelee = finesse;
            }
            else
            {
                if (!(this is Player) && CachedHighestMeleeSkill != Skill.None)
                    return CachedHighestMeleeSkill;

                var axe = GetCreatureSkill(Skill.Axe);
                var dagger = GetCreatureSkill(Skill.Dagger);
                var mace = GetCreatureSkill(Skill.Mace);
                var spear = GetCreatureSkill(Skill.Spear);
                var staff = GetCreatureSkill(Skill.Staff);
                var sword = GetCreatureSkill(Skill.Sword);
                var unarmed = GetCreatureSkill(Skill.UnarmedCombat);

                maxMelee = axe;
                if (dagger.Current > maxMelee.Current)
                    maxMelee = dagger;
                if (mace.Current > maxMelee.Current)
                    maxMelee = mace;
                if (spear.Current > maxMelee.Current)
                    maxMelee = spear;
                if (staff.Current > maxMelee.Current)
                    maxMelee = staff;
                if (sword.Current > maxMelee.Current)
                    maxMelee = sword;
                if (unarmed.Current > maxMelee.Current)
                    maxMelee = unarmed;

                CachedHighestMeleeSkill = maxMelee.Skill;
            }

            return maxMelee.Skill;
        }

        public Skill GetHighestMissileSkill()
        {
            Entity.CreatureSkill maxMissile;
            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
                return Skill.MissileWeapons;
            else
            {
                if (!(this is Player) && CachedHighestMissileSkill != Skill.None)
                    return CachedHighestMissileSkill;

                var bow = GetCreatureSkill(Skill.Bow);
                var crossbow = GetCreatureSkill(Skill.Crossbow);
                var thrown = GetCreatureSkill(Skill.ThrownWeapon);

                maxMissile = bow;
                if (crossbow.Current > maxMissile.Current)
                    maxMissile = crossbow;
                if (thrown.Current > maxMissile.Current)
                    maxMissile = thrown;

                CachedHighestMissileSkill = maxMissile.Skill;
            }

            return maxMissile.Skill;
        }

        /// <summary>
        /// Returns the attack type for non-player creatures
        /// </summary>
        public virtual CombatType GetCombatType()
        {
            return CurrentAttack ?? CombatType.Melee;
        }

        /// <summary>
        /// Returns a value between 0.5-1.5 for non-bow attacks,
        /// depending on the power bar meter
        /// </summary>
        public virtual float GetPowerMod(WorldObject weapon)
        {
            // doesn't apply for non-player creatures?
            return 1.0f;
        }

        /// <summary>
        /// Returns a value between 0.6-1.6 for bow attacks,
        /// depending on the accuracy meter
        /// </summary>
        public virtual float GetAccuracyMod(WorldObject weapon)
        {
            // doesn't apply for non-player creatures?
            return 1.0f;
        }

        /// <summary>
        /// Returns the attribute damage bonus for a physical attack
        /// </summary>
        /// <param name="attackType">Uses strength for melee, coordination for missile</param>
        public float GetAttributeMod(WorldObject weapon)
        {
            // The damage done by melee weapons—such as swords, maces, daggers, spears, and so on—is now affected more by the strength of the combatant. Strong warriors will find that they do more damage per hit than before.
            // This does not affect missile or unarmed combat. Note that this applies to monsters as well, so be careful when facing monsters that wield weapons!
            // Asheron's Call Release Notes - 2000/02 - Shadows of the Past
            //if (IsHumanoid && GetCurrentWeaponSkill() == Skill.UnarmedCombat)
            //    return 1.0f;

            var isBow = weapon != null && weapon.IsBow;

            Entity.CreatureAttribute attribute;
            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                attribute = isBow || weapon?.WeaponSkill == Skill.Dagger || weapon?.WeaponSkill == Skill.Spear || weapon?.WeaponSkill == Skill.Staff ? Coordination : Strength;
            else if (ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                attribute = isBow || weapon?.WeaponSkill == Skill.Dagger ? Coordination : Strength;
            else
                attribute = isBow || weapon?.WeaponSkill == Skill.FinesseWeapons ? Coordination : Strength;

            Skill skill = GetCurrentWeaponSkill();
            if (isBow)
                skill = Skill.Bow; // Group up bows and crossbows while excluding thrown weapons.
            else if (skill == Skill.UnarmedCombat && !IsHumanoid)
                skill = Skill.None; // Non humanoids(creatures that aren't able to wield weapons) use unarmed combat but still have the regular weapon factor.

            return SkillFormula.GetAttributeMod((int)attribute.Current, skill);
        }
        public virtual int GetUnarmedSkillDamageBonus()
        {
            if (IsHumanoid && ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration && GetCurrentWeaponSkill() == Skill.UnarmedCombat) // Non humanoids(creatures that aren't able to wield weapons) do not get a damage bonus based on skill.
            {
                var skill = GetCreatureSkill(Skill.UnarmedCombat).Current;

                if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    return (int)skill / 10;
                else
                    return (int)skill / 20;
            }
            else
                return 0;
        }

        /// <summary>
        /// Returns the current attack skill for this monster,
        /// given their stance and wielded weapon
        /// </summary>
        public virtual Skill GetCurrentAttackSkill()
        {
            return GetCurrentWeaponSkill();
        }

        /// <summary>
        /// Returns the current weapon skill for non-player creatures
        /// </summary>
        public virtual Skill GetCurrentWeaponSkill()
        {
            var weapon = GetEquippedWeapon();

            var skill = weapon != null ? weapon.WeaponSkill : Skill.UnarmedCombat;

            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
            {
                var creatureSkill = GetCreatureSkill(skill);

                if (creatureSkill.InitLevel == 0)
                {
                    // convert to post-MoA skill
                    if (weapon != null && weapon.IsRanged)
                        skill = Skill.MissileWeapons;
                    else if (skill == Skill.Sword)
                        skill = Skill.HeavyWeapons;
                    else if (skill == Skill.Dagger)
                        skill = Skill.FinesseWeapons;
                    else
                        skill = Skill.LightWeapons;
                }
            }
            else
            {
                if (weapon != null && weapon.IsRanged)
                    skill = GetHighestMissileSkill();
                else
                    skill = GetHighestMeleeSkill();
            }

            //Console.WriteLine("Monster weapon skill: " + skill);

            return skill;
        }

        /// <summary>
        /// Returns the effective attack skill for a non-player creature,
        /// ie. with Heart Seeker bonus
        /// </summary>
        public virtual uint GetEffectiveAttackSkill()
        {
            var attackSkill = GetCreatureSkill(GetCurrentAttackSkill()).Current;

            // TODO: don't use for bow?
            // https://asheron.fandom.com/wiki/Developer_Chat_-_2002/09/23
            var offenseMod = GetWeaponOffenseModifier(this);

            // monsters don't use accuracy mod?

            return (uint)Math.Round(attackSkill * offenseMod);
        }

        /// <summary>
        /// Returns the effective defense skill for a player or creature,
        /// ie. with Defender bonus and imbues
        /// </summary>
        public uint GetEffectiveDefenseSkill(CombatType combatType, bool isPvP)
        {
            var defenseSkill = combatType == CombatType.Missile ? Skill.MissileDefense : Skill.MeleeDefense;
            var defenseMod = defenseSkill == Skill.MissileDefense ? GetWeaponMissileDefenseModifier(this) : GetWeaponMeleeDefenseModifier(this);
            var burdenMod = GetBurdenMod();

            var imbuedEffectType = defenseSkill == Skill.MissileDefense ? ImbuedEffectType.MissileDefense : ImbuedEffectType.MeleeDefense;
            var defenseImbues = GetDefenseImbues(imbuedEffectType);

            var stanceMod = this is Player player ? player.GetDefenseStanceMod() : 1.0f;

            //if (this is Player)
            //Console.WriteLine($"StanceMod: {stanceMod}");

            uint effectiveDefense;
            if (!isPvP && ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && this is Player)
                effectiveDefense = (uint)Math.Round(GetCreatureSkill(defenseSkill).Current * 1.25f * defenseMod * burdenMod * stanceMod + defenseImbues);
            else
                effectiveDefense = (uint)Math.Round(GetCreatureSkill(defenseSkill).Current * defenseMod * burdenMod * stanceMod + defenseImbues);

            if (IsExhausted) effectiveDefense = 0;

            return effectiveDefense;
        }


        private static double MinAttackSpeed = 0.5;
        private static double MaxAttackSpeed = 2.0;

        /// <summary>
        /// Returns the animation speed for an attack,
        /// based on the current quickness and weapon speed
        /// </summary>
        public float GetAnimSpeed()
        {
            var quickness = Quickness.Current;
            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && IsHumanoid && GetCurrentWeaponSkill() == Skill.UnarmedCombat)
                quickness = Focus.Current;
            var weaponSpeed = GetWeaponSpeed(this);

            var divisor = 1.0 - (quickness / 300.0) + (weaponSpeed / 150.0);
            if (divisor <= 0)
                return (float)MaxAttackSpeed;

            var animSpeed = (float)Math.Clamp((1.0 / divisor), MinAttackSpeed, MaxAttackSpeed);

            return animSpeed;
        }

        /// <summary>
        /// Called when a creature evades an attack
        /// </summary>
        public virtual void OnEvade(WorldObject attacker, CombatType attackType)
        {
            // http://asheron.wikia.com/wiki/Attributes

            // Endurance will also make it less likely that you use a point of stamina to successfully evade a missile or melee attack.
            // A player is required to have Melee Defense for melee attacks or Missile Defense for missile attacks trained or specialized
            // in order for this specific ability to work. This benefit is tied to Endurance only, and it caps out at around a 75% chance
            // to avoid losing a point of stamina per successful evasion.

            var defenseSkillType = attackType == CombatType.Missile ? Skill.MissileDefense : Skill.MeleeDefense;
            var defenseSkill = GetCreatureSkill(defenseSkillType);

            if (CombatMode != CombatMode.NonCombat)
            {
                if (defenseSkill.AdvancementClass >= SkillAdvancementClass.Trained)
                {
                    var enduranceBase = (int)Endurance.Base;

                    // TODO: find exact formula / where it caps out at 75%

                    // more literal / linear formula
                    //var noStaminaUseChance = (enduranceBase - 50) / 320.0f;

                    // gdle curve-based formula, caps at 300 instead of 290
                    var noStaminaUseChance = (enduranceBase * enduranceBase * 0.000005f) + (enduranceBase * 0.00124f) - 0.07f;

                    noStaminaUseChance = Math.Clamp(noStaminaUseChance, 0.0f, 0.75f);

                    //Console.WriteLine($"NoStaminaUseChance: {noStaminaUseChance}");

                    if (noStaminaUseChance <= ThreadSafeRandom.Next(0.0f, 1.0f))
                        UpdateVitalDelta(Stamina, -1);
                }
                else
                    UpdateVitalDelta(Stamina, -1);
            }
            else
            {
                // if the player is in non-combat mode, no stamina is consumed on evade
                // reference: https://youtu.be/uFoQVgmSggo?t=145
                // from the dm guide, page 147: "if you are not in Combat mode, you lose no Stamina when an attack is thrown at you"

                //UpdateVitalDelta(Stamina, -1);
            }
        }

        /// <summary>
        /// Called when a creature hits a target
        /// </summary>
        public virtual void OnDamageTarget(WorldObject target, CombatType attackType, bool critical)
        {
            // empty base for non-player creatures?
        }

        /// <summary>
        /// Called when a creature receives an attack, evaded or not
        /// </summary>
        public virtual void OnAttackReceived(WorldObject attacker, CombatType attackType, bool critical, bool avoided)
        {
            var attackerAsCreature = attacker as Creature;
            if (attackerAsCreature != null)
            {
                attackerAsCreature.TryCastAssessDebuff(this, attackType);

                if (!Guid.IsPlayer() && attacker == AttackTarget && (attackType == CombatType.Missile || attackType == CombatType.Magic))
                {
                    if (AttacksReceivedWithoutBeingAbleToCounter == 0)
                        NextNoCounterResetTime = Time.GetFutureUnixTime(NoCounterInterval);
                    AttacksReceivedWithoutBeingAbleToCounter++;
                }
            }

            numRecentAttacksReceived++;
        }

        /// <summary>
        /// Returns the current attack height as an enumerable string
        /// </summary>
        public string GetAttackHeight()
        {
            return AttackHeight?.GetString();
        }

        /// <summary>
        /// Returns the splatter height for the current attack height
        /// </summary>
        public string GetSplatterHeight()
        {
            if (AttackHeight == null) return "Mid";

            switch (AttackHeight.Value)
            {
                case ACE.Entity.Enum.AttackHeight.Low: return "Low";
                case ACE.Entity.Enum.AttackHeight.Medium: return "Mid";
                case ACE.Entity.Enum.AttackHeight.High: default: return "Up";
            }
        }

        /// <summary>
        /// Returns the splatter direction quadrant string
        /// </summary>
        public string GetSplatterDir(WorldObject target)
        {
            var quadrant = GetRelativeDir(target);

            var splatterDir = quadrant.HasFlag(Quadrant.Left) ? "Left" : "Right";
            splatterDir += quadrant.HasFlag(Quadrant.Front) ? "Front" : "Back";

            return splatterDir;
        }

        public double GetLifeResistance(DamageType damageType)
        {
            double resistance = 1.0;

            switch (damageType)
            {
                case DamageType.Slash:
                    resistance = ResistSlashMod;
                    break;

                case DamageType.Pierce:
                    resistance = ResistPierceMod;
                    break;

                case DamageType.Bludgeon:
                    resistance = ResistBludgeonMod;
                    break;

                case DamageType.Fire:
                    resistance = ResistFireMod;
                    break;

                case DamageType.Cold:
                    resistance = ResistColdMod;
                    break;

                case DamageType.Acid:
                    resistance = ResistAcidMod;
                    break;

                case DamageType.Electric:
                    resistance = ResistElectricMod;
                    break;

                case DamageType.Nether:
                    resistance = ResistNetherMod;
                    break;
            }

            return resistance;
        }

        /// <summary>
        /// Reduces a creatures's attack skill while exhausted
        /// </summary>
        public uint GetExhaustedSkill(uint attackSkill)
        {
            var halfSkill = (uint)Math.Round(attackSkill / 2.0f);

            uint maxPenalty = 50;
            var reducedSkill = attackSkill >= maxPenalty ? attackSkill - maxPenalty : 0;

            return Math.Max(reducedSkill, halfSkill);
        }

        /// <summary>
        /// Returns a divisor for the target height
        /// for aiming projectiles
        /// </summary>
        public virtual float GetAimHeight(WorldObject target)
        {
            return 2.0f;
        }

        /// <summary>
        /// Return the scalar damage absorbed by a shield
        /// </summary>
        public float GetShieldMod(Creature attacker, DamageType damageType, WorldObject weapon, bool isPvP, out WorldObject shield)
        {
            // ensure combat stance
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR && CombatMode == CombatMode.NonCombat)
            {
                shield = null;
                return 1.0f;
            }

            // does the player have a shield equipped?
            shield = GetEquippedShield();
            if (shield == null)
                return 1.0f;

            var player = this as Player;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (player != null && GetCreatureSkill(Skill.Shield).AdvancementClass < SkillAdvancementClass.Trained)
                    return 1.0f;

                // we cant block our own attacks
                if (attacker == this)
                    return 1.0f;
            }

            // phantom weapons ignore all armor and shields
            if (weapon != null && weapon.HasImbuedEffect(ImbuedEffectType.IgnoreAllArmor))
                return 1.0f;

            bool bypassShieldAngleCheck = false;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && (weapon == null || ((weapon.IgnoreShield ?? 0) == 0 && !weapon.IsTwoHanded)))
            {
                var techniqueTrinket = GetEquippedTrinket();
                if (techniqueTrinket != null && techniqueTrinket.TacticAndTechniqueId == (int)TacticAndTechniqueType.Defensive)
                    bypassShieldAngleCheck = true; // Shields cover all angles while using the Defensive technique.
            }

            if (!bypassShieldAngleCheck)
            {
                // is monster in front of player,
                // within shield effectiveness area?
                var effectiveAngle = 180.0f;
                var angle = GetAngle(attacker);
                if (Math.Abs(angle) > effectiveAngle / 2.0f)
                    return 1.0f;
            }

            // get base shield AL
            var baseSL = shield.GetProperty(PropertyInt.ArmorLevel) ?? 0.0f;

            // shield AL item enchantment additives:
            // impenetrability, brittlemail
            var ignoreMagicArmor = (weapon?.IgnoreMagicArmor ?? false) || (attacker?.IgnoreMagicArmor ?? false);

            var modSL = shield.EnchantmentManager.GetArmorMod();

            if (ignoreMagicArmor)
                modSL = attacker is Player ? (int)Math.Round(IgnoreMagicArmorScaled(modSL)) : 0;

            var effectiveSL = baseSL + modSL;

            // get shield RL against damage type
            var baseRL = GetResistance(shield, damageType);

            // shield RL item enchantment additives:
            // banes, lures
            var modRL = shield.EnchantmentManager.GetArmorModVsType(damageType);

            if (ignoreMagicArmor)
                modRL = attacker is Player ? IgnoreMagicArmorScaled(modRL) : 0.0f;

            var effectiveRL = (float)(baseRL + modRL);

            // resistance clamp
            effectiveRL = Math.Clamp(effectiveRL, -2.0f, 2.0f);

            // handle negative SL
            //if (effectiveSL < 0 && effectiveRL != 0)
            //effectiveRL = 1.0f / effectiveRL;

            var effectiveLevel = effectiveSL * effectiveRL;

            effectiveLevel = GetSkillModifiedShieldLevel(effectiveLevel);

            var ignoreShieldMod = attacker.GetIgnoreShieldMod(attacker, weapon, isPvP);
            //Console.WriteLine($"IgnoreShieldMod: {ignoreShieldMod}");

            effectiveLevel *= ignoreShieldMod;

            if (isPvP)
                effectiveLevel *= (float)PropertyManager.GetInterpolatedDouble(attacker.Level ?? 1, "pvp_dmg_mod_low_shield_level", "pvp_dmg_mod_high_shield_level", "pvp_dmg_mod_low_level", "pvp_dmg_mod_high_level");

            // SL is multiplied by existing AL
            var shieldMod = SkillFormula.CalcArmorMod(effectiveLevel);
            //Console.WriteLine("ShieldMod: " + shieldMod);
            return shieldMod;
        }

        public static double GetThrownWeaponMaxVelocity(WorldObject throwed)
        {
            Creature thrower = throwed.Wielder as Creature;

            if (thrower == null || throwed == null)
                return 0;

            return GetThrownWeaponMaxVelocity((int)thrower.Strength.Current, ((throwed.StackUnitEncumbrance ?? throwed.EncumbranceVal) ?? 1));
        }

        public static double GetEstimatedThrownWeaponMaxVelocity(WorldObject throwed)
        {
            if (throwed == null)
                return 0;

            return GetThrownWeaponMaxVelocity(100, ((throwed.StackUnitEncumbrance ?? throwed.EncumbranceVal) ?? 1));
        }

        public static double GetThrownWeaponMaxVelocity(int throwerStrength, int throwedEncumbrance)
        {
            return Math.Clamp(16 - 0.06 * throwerStrength + 0.0009 * Math.Pow(throwerStrength, 2) - (Math.Sqrt(throwedEncumbrance) - Math.Sqrt(5)), 5.45, 27.3); // Custom formula - Asheron's Call Strategies and Secrets has some info on the original formula on pages 150 and 151.
        }

        /// <summary>
        /// Returns the total applicable Recklessness modifier,
        /// taking into account both attacker and defender players
        /// </summary>
        public static float GetRecklessnessMod(Creature attacker, Creature defender)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                return 1.0f;

            var playerAttacker = attacker as Player;
            var playerDefender = defender as Player;

            var recklessnessMod = 1.0f;

            // multiplicative or additive?
            // defender is a negative Damage Reduction Rating
            // 20 DR combined with 20 DRR = 1.2 * 0.8333... = 1.0
            // 20 DR combined with -20 DRR = 1.2 * 1.2 = 1.44
            if (playerAttacker != null)
                recklessnessMod *= playerAttacker.GetRecklessnessMod();

            if (playerDefender != null)
                recklessnessMod *= playerDefender.GetRecklessnessMod();

            return recklessnessMod;
        }

        public float GetSneakAttackMod(WorldObject target)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
                return 1.0f;
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                // ensure creature target
                var creatureTarget = target as Creature;
                if (creatureTarget == null)
                    return 1.0f;

                var angle = creatureTarget.GetAngle(this);
                var behind = Math.Abs(angle) > 90.0f;

                var weapon = GetEquippedMeleeWeapon();
                var daggerBonus = 0.0f;
                if (weapon != null && weapon.WeaponSkill == Skill.Dagger)
                    daggerBonus = 0.1f;

                if (behind)
                    return 1.2f + daggerBonus; // 20% damage bonus
                else
                    return 1.0f;
            }
            else
            {
                // ensure trained
                var sneakAttack = GetCreatureSkill(Skill.SneakAttack);
                if (sneakAttack.AdvancementClass < SkillAdvancementClass.Trained)
                    return 1.0f;

                // ensure creature target
                var creatureTarget = target as Creature;
                if (creatureTarget == null)
                    return 1.0f;

                // Effects:
                // General Sneak Attack effects:
                //   - 100% chance to sneak attack from behind an opponent.
                //   - Deception trained: 10% chance to sneak attack from the front of an opponent
                //   - Deception specialized: 15% chance to sneak attack from the front of an opponent
                var angle = creatureTarget.GetAngle(this);
                var behind = Math.Abs(angle) > 90.0f;
                var chance = 0.0f;
                if (behind)
                {
                    chance = 1.0f;
                }
                else
                {
                    var deception = GetCreatureSkill(Skill.Deception);
                    if (deception.AdvancementClass == SkillAdvancementClass.Trained)
                        chance = 0.1f;
                    else if (deception.AdvancementClass == SkillAdvancementClass.Specialized)
                        chance = 0.15f;

                    // if Deception is below 306 skill, these chances are reduced proportionately.
                    // this is in addition to proprtional reduction if your Sneak Attack skill is below your attack skill.
                    var deceptionCap = 306;
                    if (deception.Current < deceptionCap)
                        chance *= Math.Min((float)deception.Current / deceptionCap, 1.0f);
                }
                //Console.WriteLine($"Sneak attack {(behind ? "behind" : "front")}, chance {Math.Round(chance * 100)}%");

                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
                if (rng >= chance)
                    return 1.0f;

                // Damage Rating:
                // Sneak Attack Trained:
                //   + 10 Damage Rating when Sneak Attack activates
                // Sneak Attack Specialized:
                //   + 20 Damage Rating when Sneak Attack activates
                var damageRating = sneakAttack.AdvancementClass == SkillAdvancementClass.Specialized ? 20.0f : 10.0f;

                // Sneak Attack works for melee, missile, and magic attacks.

                // if the Sneak Attack skill is lower than your attack skill (as determined by your equipped weapon)
                // then the damage rating is reduced proportionately. Because the damage rating caps at 10 for trained
                // and 20 for specialized, there is no reason to raise the skill above your attack skill
                var attackSkill = GetCreatureSkill(GetCurrentAttackSkill());
                if (sneakAttack.Current < attackSkill.Current)
                {
                    if (attackSkill.Current > 0)
                        damageRating *= (float)sneakAttack.Current / attackSkill.Current;
                    else
                        damageRating = 0;
                }

                // if the defender has Assess Person, they reduce the extra Sneak Attack damage Deception can add
                // from the front by up to 100%.
                // this percent is reduced proportionately if your buffed Assess Person skill is below the deception cap.
                // this reduction does not apply to attacks from behind.
                if (!behind)
                {
                    // compare to assess person or deception??
                    // wiki info is confusing here, it says 'your buffed Assess Person'
                    // which sounds like its scaling sourceAssess / targetAssess,
                    // but i think it should be targetAssess / deceptionCap?
                    var targetAssess = creatureTarget.GetCreatureSkill(Skill.AssessPerson).Current;

                    var deceptionCap = 306;
                    damageRating *= 1.0f - Math.Min((float)targetAssess / deceptionCap, 1.0f);
                }

                var sneakAttackMod = (100 + damageRating) / 100.0f;
                //Console.WriteLine("SneakAttackMod: " + sneakAttackMod);
                return sneakAttackMod;
            }
        }

        public void FightDirty(WorldObject target, WorldObject weapon)
        {
            // Skill description:
            // Your melee and missile attacks have a chance to weaken your opponent.
            // - Low attacks can reduce the defense skills of the opponent.
            // - Medium attacks can cause small amounts of bleeding damage.
            // - High attacks can reduce opponents' attack and healing skills

            // Effects:
            // Low: reduces the defense skills of the opponent by -10
            // Medium: bleed ticks for 60 damage per 20 seconds
            // High: reduces the attack skills of the opponent by -10, and
            //       the healing effects of the opponent by -15 rating
            //
            // these damage #s are doubled for dirty fighting specialized.

            // Notes:
            // - Dirty fighting works for melee and missile attacks.
            // - Has a 25% chance to activate on any melee of missile attack.
            //   - This activation is reduced proportionally if Dirty Fighting is lower
            //     than your active weapon skill as determined by your equipped weapon.
            // - All activate effects last 20 seconds.
            // - Although a specific effect won't stack with itself,
            //   you can stack all 3 effects on the opponent at the same time. This means
            //   when a skill activates at one attack height, you can move to another attack height
            //   to try to land an additional effect.
            // - Successfully landing a Dirty Fighting effect is mentioned in chat. Additionally,
            //   the medium height effect results in 'floating glyphs' around the target:

            //   "Dirty Fighting! <Player> delivers a Bleeding Assault to <target>!"
            //   "Dirty Fighting! <Player> delivers a Traumatic Assault to <target>!"

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                return;

            // dirty fighting skill must be at least trained
            var dirtySkill = GetCreatureSkill(Skill.DirtyFighting);
            if (dirtySkill.AdvancementClass < SkillAdvancementClass.Trained)
                return;

            // ensure creature target
            var creatureTarget = target as Creature;
            if (creatureTarget == null)
                return;

            var chance = 0.25f;

            var attackSkill = GetCreatureSkill(GetCurrentWeaponSkill());
            if (dirtySkill.Current < attackSkill.Current)
            {
                chance *= (float)dirtySkill.Current / attackSkill.Current;
            }

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (rng >= chance)
                return;

            switch (AttackHeight)
            {
                case ACE.Entity.Enum.AttackHeight.Low:
                    FightDirty_ApplyLowAttack(creatureTarget, weapon);
                    break;
                case ACE.Entity.Enum.AttackHeight.Medium:
                    FightDirty_ApplyMediumAttack(creatureTarget, weapon);
                    break;
                case ACE.Entity.Enum.AttackHeight.High:
                    FightDirty_ApplyHighAttack(creatureTarget, weapon);
                    break;
            }
        }

        /// <summary>
        /// Reduces the defense skills of the opponent by
        /// -10 if trained, or -20 if specialized
        /// </summary>
        public void FightDirty_ApplyLowAttack(Creature target, WorldObject weapon)
        {
            var spellID = GetCreatureSkill(Skill.DirtyFighting).AdvancementClass == SkillAdvancementClass.Specialized ?
                SpellId.DF_Specialized_DefenseDebuff : SpellId.DF_Trained_DefenseDebuff;

            var spell = new Spell(spellID);
            if (spell.NotFound) return;  // TODO: friendly message to install DF patch

            var addResult = target.EnchantmentManager.Add(spell, this, weapon);

            if (target is Player playerTarget)
                playerTarget.Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(playerTarget.Session, new Enchantment(playerTarget, addResult.Enchantment)));

            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.DirtyFightingDefenseDebuff));

            FightDirty_SendMessage(target, spell);
        }

        /// <summary>
        /// Applies bleed ticks for 60 damage per 20 seconds if trained,
        /// 120 damage per 20 seconds if specialized
        /// </summary>
        /// <returns></returns>
        public void FightDirty_ApplyMediumAttack(Creature target, WorldObject weapon)
        {
            var spellID = GetCreatureSkill(Skill.DirtyFighting).AdvancementClass == SkillAdvancementClass.Specialized ?
                SpellId.DF_Specialized_Bleed : SpellId.DF_Trained_Bleed;

            var spell = new Spell(spellID);
            if (spell.NotFound) return;  // TODO: friendly message to install DF patch

            var addResult = target.EnchantmentManager.Add(spell, this, weapon);

            if (target is Player playerTarget)
                playerTarget.Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(playerTarget.Session, new Enchantment(playerTarget, addResult.Enchantment)));

            // only send if not already applied?
            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.DirtyFightingDamageOverTime));

            FightDirty_SendMessage(target, spell);
        }

        /// <summary>
        /// Reduces the attack skills and healing rating for opponent
        /// by -10 if trained, or -20 if specialized
        /// </summary>
        public void FightDirty_ApplyHighAttack(Creature target, WorldObject weapon)
        {
            // attack debuff
            var spellID = GetCreatureSkill(Skill.DirtyFighting).AdvancementClass == SkillAdvancementClass.Specialized ?
                SpellId.DF_Specialized_AttackDebuff : SpellId.DF_Trained_AttackDebuff;

            var spell = new Spell(spellID);
            if (spell.NotFound) return;  // TODO: friendly message to install DF patch

            var addResult = target.EnchantmentManager.Add(spell, this, weapon);

            var playerTarget = target as Player;

            if (playerTarget != null)
                playerTarget.Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(playerTarget.Session, new Enchantment(playerTarget, addResult.Enchantment)));

            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.DirtyFightingAttackDebuff));

            FightDirty_SendMessage(target, spell);

            // healing resistance rating
            spellID = GetCreatureSkill(Skill.DirtyFighting).AdvancementClass == SkillAdvancementClass.Specialized ?
                SpellId.DF_Specialized_HealingDebuff : SpellId.DF_Trained_HealingDebuff;

            spell = new Spell(spellID);
            if (spell.NotFound) return;  // TODO: friendly message to install DF patch

            addResult = target.EnchantmentManager.Add(spell, this, weapon);

            if (playerTarget != null)
                playerTarget.Session.Network.EnqueueSend(new GameEventMagicUpdateEnchantment(playerTarget.Session, new Enchantment(playerTarget, addResult.Enchantment)));

            target.EnqueueBroadcast(new GameMessageScript(target.Guid, PlayScript.DirtyFightingHealDebuff));

            FightDirty_SendMessage(target, spell);
        }

        public void FightDirty_SendMessage(Creature target, Spell spell)
        {
            // Dirty Fighting! <Player> delivers a <sic> Unbalancing Blow to <target>!
            //var article = spellBase.Name.StartsWithVowel() ? "an" : "a";

            var msg = $"Dirty Fighting! {Name} delivers a {spell.Name} to {target.Name}!";

            var playerSource = this as Player;
            var playerTarget = target as Player;

            if (playerSource != null)
                playerSource.SendMessage(msg, ChatMessageType.Combat, this);
            if (playerTarget != null)
                playerTarget.SendMessage(msg, ChatMessageType.Combat, this);
        }

        private double NextAssessDebuffActivationTime = 0;
        private static double AssessDebuffActivationInterval = 10;
        public void TryCastAssessDebuff(Creature target, CombatType combatType)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (this == target)
                return; // We don't want to find vulnerabilities on ourselves!

            if (target.IsDead)
                return; // Target is already dead, abort!

            var currentTime = Time.GetUnixTime();

            if (NextAssessDebuffActivationTime > currentTime)
                return;

            var skill = GetCreatureSkill(Skill.AssessCreature);
            if (skill.AdvancementClass == SkillAdvancementClass.Untrained || skill.AdvancementClass == SkillAdvancementClass.Inactive)
                return;

            var sourceAsPlayer = this as Player;
            var targetAsPlayer = target as Player;

            var activationChance = 0.25f;
            if (sourceAsPlayer != null)
                activationChance += sourceAsPlayer.ScaleWithPowerAccuracyBar(0.25f);

            if (activationChance < ThreadSafeRandom.Next(0.0f, 1.0f))
                return;

            NextAssessDebuffActivationTime = currentTime + AssessDebuffActivationInterval;

            var defenseSkill = target.GetCreatureSkill(Skill.Deception);
            var effectiveDefenseSkill = defenseSkill.Current;

            if (targetAsPlayer != null)
                effectiveDefenseSkill *= 2;

            var avoidChance = 1.0f - SkillCheck.GetSkillChance(skill.Current, effectiveDefenseSkill);
            if (avoidChance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                if (sourceAsPlayer != null)
                    sourceAsPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{target.Name}'s deception stops you from finding a vulnerability!", ChatMessageType.Magic));

                if (targetAsPlayer != null)
                {
                    Proficiency.OnSuccessUse(targetAsPlayer, defenseSkill, skill.Current);
                    targetAsPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your deception stops {Name} from finding a vulnerability!", ChatMessageType.Magic));
                }

                return;
            }
            else if (sourceAsPlayer != null)
                Proficiency.OnSuccessUse(sourceAsPlayer, skill, defenseSkill.Current);

            string spellType;
            // 1/5 chance of the vulnerability being explicity of the type of attack that was used, otherwise random 1/3 for each type
            SpellId spellId;
            if (ThreadSafeRandom.Next(1, 5) != 5)
            {
                switch (combatType)
                {
                    default:
                    case CombatType.Melee:
                        spellId = SpellId.VulnerabilityOther1;
                        spellType = "melee";
                        break;
                    case CombatType.Missile:
                        spellId = SpellId.DefenselessnessOther1;
                        spellType = "missile";
                        break;
                    case CombatType.Magic:
                        spellId = SpellId.MagicYieldOther1;
                        spellType = "magic";
                        break;
                }
            }
            else
            {
                var spellRNG = ThreadSafeRandom.Next(1, 3);
                switch (spellRNG)
                {
                    default:
                    case 1:
                        spellId = SpellId.VulnerabilityOther1;
                        spellType = "melee";
                        break;
                    case 2:
                        spellId = SpellId.DefenselessnessOther1;
                        spellType = "missile";
                        break;
                    case 3:
                        spellId = SpellId.MagicYieldOther1;
                        spellType = "magic";
                        break;
                }
            }

            var spellLevels = SpellLevelProgression.GetSpellLevels(spellId);
            int maxUsableSpellLevel = Math.Min(spellLevels.Count, 5);

            if (spellLevels.Count == 0)
                return;

            int minSpellLevel = Math.Min(Math.Max(0, (int)Math.Floor(((float)skill.Current - 150) / 50.0)), maxUsableSpellLevel);
            int maxSpellLevel = Math.Max(0, Math.Min((int)Math.Floor(((float)skill.Current - 50) / 50.0), maxUsableSpellLevel));

            int spellLevel = ThreadSafeRandom.Next(minSpellLevel, maxSpellLevel);
            var spell = new Spell(spellLevels[spellLevel]);

            if (spell.NonComponentTargetType == ItemType.None)
                TryCastSpell(spell, null, this, null, false, false, false, false);
            else
                TryCastSpell(spell, target, this, null, false, false, false, false);

            string spellTypePrefix;
            switch (spellLevel + 1)
            {
                case 1: spellTypePrefix = "a minor"; break;
                default:
                case 2: spellTypePrefix = "a"; break;
                case 3: spellTypePrefix = "a moderate"; break;
                case 4: spellTypePrefix = "a severe"; break;
                case 5: spellTypePrefix = "a major"; break;
                case 6: spellTypePrefix = "a crippling"; break;
            }

            if (sourceAsPlayer != null)
                sourceAsPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your assess knowledge allows you to expose {spellTypePrefix} {spellType} vulnerability on {target.Name}!", ChatMessageType.Magic));
            if (targetAsPlayer != null)
                targetAsPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name}'s assess knowledge exposes {spellTypePrefix} {spellType} vulnerability on you!", ChatMessageType.Magic));
        }

        /// <summary>
        /// Returns TRUE if the creature receives a +5 DR bonus for this weapon type
        /// </summary>
        public virtual bool GetHeritageBonus(WorldObject weapon)
        {
            // only for players
            return false;
        }

        /// <summary>
        /// Returns a ResistanceType for a DamageType
        /// </summary>
        public static ResistanceType GetResistanceType(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Slash:
                    return ResistanceType.Slash;
                case DamageType.Pierce:
                    return ResistanceType.Pierce;
                case DamageType.Bludgeon:
                    return ResistanceType.Bludgeon;
                case DamageType.Fire:
                    return ResistanceType.Fire;
                case DamageType.Cold:
                    return ResistanceType.Cold;
                case DamageType.Acid:
                    return ResistanceType.Acid;
                case DamageType.Electric:
                    return ResistanceType.Electric;
                case DamageType.Nether:
                    return ResistanceType.Nether;
                case DamageType.Health:
                    return ResistanceType.HealthDrain;
                case DamageType.Stamina:
                    return ResistanceType.StaminaDrain;
                case DamageType.Mana:
                    return ResistanceType.ManaDrain;
                default:
                    return ResistanceType.Undef;
            }
        }

        public virtual bool CanDamage(Creature target)
        {
            if (target is Player)
            {
                // monster attacking player
                return true;    // other checks handled elsewhere
            }
            else
            {
                // monster attacking monster
                var sourcePet = this is CombatPet;
                var targetPet = target is CombatPet;

                if (sourcePet || targetPet)
                {
                    if (sourcePet && targetPet)     // combat pets can't damage other pets
                        return false;
                    else if (sourcePet && target.PlayerKillerStatus == PlayerKillerStatus.PK || targetPet && PlayerKillerStatus == PlayerKillerStatus.PK)   // combat pets can't damage pk-only creatures (ie. faction banners)
                        return false;
                    else
                        return true;
                }

                // faction mobs
                if (Faction1Bits != null || target.Faction1Bits != null)
                {
                    if (AllowFactionCombat(target))
                        return true;
                }

                // handle FoeType
                if (PotentialFoe(target))
                    return true;

                return false;
            }
        }

        public static Skill GetDefenseSkill(CombatType combatType)
        {
            switch (combatType)
            {
                case CombatType.Melee:
                    return Skill.MeleeDefense;
                case CombatType.Missile:
                    return Skill.MissileDefense;
                case CombatType.Magic:
                    return Skill.MagicDefense;
                default:
                    return Skill.None;
            }
        }

        /// <summary>
        /// If one of these fields is set, potential aggro from Player or CombatPet movement terminates immediately
        /// </summary>
        protected static readonly Tolerance PlayerCombatPet_MoveExclude = Tolerance.NoAttack | Tolerance.Appraise | Tolerance.Provoke | Tolerance.Retaliate | Tolerance.Monster;

        /// <summary>
        /// If one of these fields is set, potential aggro from other monster movement terminates immediately
        /// </summary>
        protected static readonly Tolerance Monster_MoveExclude = Tolerance.NoAttack | Tolerance.Appraise | Tolerance.Provoke | Tolerance.Retaliate;

        /// <summary>
        /// If one of these fields is set, potential aggro from Player or CombatPet attacks terminates immediately
        /// </summary>
        protected static readonly Tolerance PlayerCombatPet_RetaliateExclude = Tolerance.NoAttack | Tolerance.Monster;

        /// <summary>
        /// If one of these fields is set, potential aggro from monster alerts terminates immediately
        /// </summary>
        protected static readonly Tolerance AlertExclude = Tolerance.NoAttack | Tolerance.Provoke;

        /// <summary>
        /// Wakes up a monster if it can be alerted
        /// </summary>
        public bool AlertMonster(Creature monster)
        {
            // currently used for proximity checking exclusively:

            // Player_Monster.CheckMonsters() - player movement
            // Monster_Awareness.CheckTargets_Inner() - monster spawning in
            // Monster_Awareness.FactionMob_CheckMonsters() - faction mob scanning

            // non-attackable creatures do not get aggroed,
            // unless they have a TargetingTactic, such as the invisible archers in Oswald's Dirk Quest
            if (!monster.Attackable && monster.TargetingTactic == TargetingTactic.None)
                return false;

            // ensure monster is currently in idle state to wake up,
            // and it has no tolerance to players running nearby
            // TODO: investigate usage for tolerance
            var tolerance = this is Player ? PlayerCombatPet_MoveExclude : Monster_MoveExclude;

            if (monster.MonsterState != State.Idle || (monster.Tolerance & tolerance) != 0)
                return false;

            // for faction mobs, ensure alerter doesn't belong to same faction
            if (SameFaction(monster) && !PotentialFoe(monster))
                return false;

            // add to retaliate targets?

            //Console.WriteLine($"[{Timers.RunningTime}] - {monster.Name} ({monster.Guid}) - waking up");
            monster.AttackTarget = this;
            monster.WakeUp();

            return true;
        }

        /// <summary>
        /// Returns the damage type for the currently equipped weapon / ammo
        /// </summary>
        /// <param name="multiple">If true, returns all of the damage types for the weapon</param>
        public virtual DamageType GetDamageType(bool multiple = false, CombatType? combatType = null)
        {
            // old method, keeping intact for monsters
            var weapon = GetEquippedWeapon();
            var ammo = GetEquippedAmmo();

            if (weapon == null)
                return DamageType.Bludgeon;

            if (combatType == null)
                combatType = GetCombatType();

            var damageSource = combatType == CombatType.Melee || ammo == null || !weapon.IsAmmoLauncher ? weapon : ammo;

            var damageTypes = damageSource.W_DamageType;

            // returning multiple damage types
            if (multiple) return damageTypes;

            // get single damage type
            var motion = CurrentMotionState.MotionState.ForwardCommand.ToString();
            foreach (DamageType damageType in Enum.GetValues(typeof(DamageType)))
            {
                if ((damageTypes & damageType) != 0 && !damageType.IsMultiDamage())
                {
                    // handle multiple damage types
                    if (damageType == DamageType.Slash && motion.Contains("Thrust"))
                        continue;

                    return damageType;
                }
            }
            return damageTypes;
        }

        /// <summary>
        /// Flag indicates which overpower formula is used
        /// True  = Formula A / ratings method
        /// False = Formula B / critical defense method
        /// </summary>
        public static bool OverpowerMethod = false;

        public static bool GetOverpower(Creature attacker, Creature defender)
        {
            if (OverpowerMethod)
                return GetOverpower_Method_A(attacker, defender);
            else
                return GetOverpower_Method_B(attacker, defender);
        }

        public static bool GetOverpower_Method_A(Creature attacker, Creature defender)
        {
            // implemented similar to ratings
            if (attacker.Overpower == null)
                return false;

            var overpowerChance = attacker.Overpower.Value;
            if (defender.OverpowerResist != null)
                overpowerChance -= defender.OverpowerResist.Value;

            //Console.WriteLine($"Overpower chance: {GetOverpowerChance_Method_A(attacker, defender)}");

            if (overpowerChance <= 0)
                return false;

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

            return rng < overpowerChance * 0.01f;
        }

        public static bool GetOverpower_Method_B(Creature attacker, Creature defender)
        {
            // implemented similar to critical defense
            if (attacker.Overpower == null)
                return false;

            var overpowerChance = attacker.Overpower.Value;

            //Console.WriteLine($"Overpower chance: {GetOverpowerChance_Method_B(attacker, defender)}");

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

            if (rng >= overpowerChance * 0.01f)
                return false;

            if (defender.OverpowerResist == null)
                return true;

            var resistChance = defender.OverpowerResist.Value;

            rng = ThreadSafeRandom.Next(0.0f, 1.0f);

            return rng >= resistChance * 0.01f;
        }

        public static float GetOverpowerChance(Creature attacker, Creature defender)
        {
            if (OverpowerMethod)
                return GetOverpowerChance_Method_A(attacker, defender);
            else
                return GetOverpowerChance_Method_B(attacker, defender);
        }

        public static float GetOverpowerChance_Method_A(Creature attacker, Creature defender)
        {
            if (attacker.Overpower == null)
                return 0.0f;

            var overpowerChance = attacker.Overpower.Value;
            if (defender.OverpowerResist != null)
                overpowerChance -= defender.OverpowerResist.Value;

            if (overpowerChance <= 0)
                return 0.0f;

            return overpowerChance * 0.01f;
        }

        public static float GetOverpowerChance_Method_B(Creature attacker, Creature defender)
        {
            if (attacker.Overpower == null)
                return 0.0f;

            var overpowerChance = (attacker.Overpower ?? 0) * 0.01f;
            var overpowerResistChance = (defender.OverpowerResist ?? 0) * 0.01f;

            return overpowerChance * (1.0f - overpowerResistChance);
        }

        /// <summary>
        /// Returns the number of equipped items with a particular imbue type
        /// </summary>
        public int GetDefenseImbues(ImbuedEffectType imbuedEffectType)
        {
            return EquippedObjects.Values.Count(i => i.ImbuedEffect.HasFlag(imbuedEffectType));
        }

        /// <summary>
        /// Returns the cloak the creature has equipped,
        /// or 'null' if no cloak is equipped
        /// </summary>
        public WorldObject EquippedCloak => EquippedObjects.Values.FirstOrDefault(i => i.ValidLocations == EquipMask.Cloak);

        /// <summary>
        /// Returns TRUE if creature has cloak equipped
        /// </summary>
        public bool HasCloakEquipped => EquippedCloak != null;

        /// <summary>
        /// Called when a monster attacks another monster
        /// This should only happen between mobs of differing factions, or from FoeType
        /// </summary>
        public void MonsterOnAttackMonster(Creature monster)
        {
            /*Console.WriteLine($"{Name}.MonsterOnAttackMonster({monster.Name})");
            Console.WriteLine($"Attackable: {monster.Attackable}");
            Console.WriteLine($"Tolerance: {monster.Tolerance}");*/

            // when a faction mob attacks a regular mob, the regular mob will retaliate against the faction mob
            if (Faction1Bits != null && (monster.Faction1Bits == null || (Faction1Bits & monster.Faction1Bits) == 0))
                monster.AddRetaliateTarget(this);

            // when a monster with a FoeType attacks a foe, the foe will retaliate
            if (FoeType != null && (monster.FoeType == null || monster.FoeType != CreatureType))
                monster.AddRetaliateTarget(this);

            if (monster.MonsterState == State.Idle && !monster.Tolerance.HasFlag(Tolerance.NoAttack))
            {
                monster.AttackTarget = this;
                monster.WakeUp();
            }
        }

        /// <summary>
        /// Returns TRUE if creatures are both in the same faction
        /// </summary>
        public bool SameFaction(Creature creature)
        {
            return Faction1Bits != null && creature.Faction1Bits != null && (Faction1Bits & creature.Faction1Bits) != 0;
        }

        /// <summary>
        /// Returns TRUE is this creature has a FoeType that matches the input creature's CreatureType,
        /// or if the input creature has a FoeType that matches this creature's CreatureType
        /// </summary>
        public bool PotentialFoe(Creature creature)
        {
            return FoeType != null && FoeType == creature.CreatureType || creature.FoeType != null && creature.FoeType == CreatureType;
        }

        public bool AllowFactionCombat(Creature creature)
        {
            if (Faction1Bits == null && creature.Faction1Bits == null)
                return false;

            var factionSelf = Faction1Bits ?? FactionBits.None;
            var factionOther = creature.Faction1Bits ?? FactionBits.None;

            return (factionSelf & factionOther) == 0;
        }

        public void AddRetaliateTarget(WorldObject wo)
        {
            PhysicsObj.ObjMaint.AddRetaliateTarget(wo.PhysicsObj);
        }

        public bool HasRetaliateTarget(WorldObject wo)
        {
            if (wo != null)
                return PhysicsObj.ObjMaint.RetaliateTargetsContainsKey(wo.Guid.Full);
            else
                return false;
        }

        public void ClearRetaliateTargets()
        {
            PhysicsObj.ObjMaint.ClearRetaliateTargets();
        }

        /// <summary>
        /// Returns the total burden of items held in both hands
        /// (main hand and offhand)
        /// </summary>
        public int GetHeldItemBurden()
        {
            var mainhand = GetEquippedMainHand();
            var offhand = GetEquippedOffHand();

            int mainhandBurden;
            if ((mainhand?.MaxStackSize ?? 0) > 1) // Thrown weapons use the burden of a stack of 30 instead of entire current stack.
                mainhandBurden = (mainhand?.StackUnitEncumbrance ?? 0) * 30;
            else
                mainhandBurden = mainhand?.EncumbranceVal ?? 0;
            var offhandBurden = offhand?.EncumbranceVal ?? 0;

            return mainhandBurden + offhandBurden;
        }

        public float GetStaminaMod()
        {
            var endurance = (int)Endurance.Base;

            // more literal / linear formula
            var staminaMod = 1.0f - (endurance - 50) / 480.0f;

            // gdle curve-based formula, caps at 300 instead of 290
            //var staminaMod = (endurance * endurance * -0.000003175f) - (endurance * 0.0008889f) + 1.052f;

            staminaMod = Math.Clamp(staminaMod, 0.5f, 1.0f);

            // this is also specific to gdle,
            // additive luck which can send the base stamina way over 1.0
            /*var luck = ThreadSafeRandom.Next(0.0f, 1.0f);
            staminaMod += luck;*/

            return staminaMod;
        }

        /// <summary>
        /// Calculates the amount of stamina required to perform this attack
        /// </summary>
        public int GetAttackStamina(PowerAccuracy powerAccuracy)
        {
            // Stamina cost for melee and missile attacks is based on the total burden of what you are holding
            // in your hands (main hand and offhand), and your power/accuracy bar.

            // Attacking(Low power / accuracy bar)   1 point per 700 burden units
            //                                       1 point per 1200 burden units
            //                                       1.5 points per 1600 burden units
            // Attacking(Mid power / accuracy bar)   1 point per 700 burden units
            //                                       2 points per 1200 burden units
            //                                       3 points per 1600 burden units
            // Attacking(High power / accuracy bar)  2 point per 700 burden units
            //                                       4 points per 1200 burden units
            //                                       6 points per 1600 burden units

            // The higher a player's base Endurance, the less stamina one uses while attacking. This benefit is tied to Endurance only,
            // and caps out at 50% less stamina used per attack. Scaling is similar to other Endurance bonuses. Applies only to players.

            // When stamina drops to 0, your melee and missile defenses also drop to 0 and you will be incapable of attacking.
            // In addition, you will suffer a 50% penalty to your weapon skill. This applies to players and creatures.

            var burden = GetHeldItemBurden();

            var baseCost = StaminaTable.GetStaminaCost(powerAccuracy, burden);

            var staminaMod = GetStaminaMod();

            var staminaCost = Math.Max(baseCost * staminaMod, 1);

            //Console.WriteLine($"GetAttackStamina({powerAccuracy}) - burden: {burden}, baseCost: {baseCost}, staminaMod: {staminaMod}, staminaCost: {staminaCost}");

            return (int)Math.Round(staminaCost);
        }

        public bool IsOnNoDamageLandblock => Location != null ? NoDamage_Landblocks.Contains(Location.LandblockId.Landblock) : false;

        /// <summary>
        /// A list of landblocks where no damage can be done
        /// </summary>
        public static HashSet<ushort> NoDamage_Landblocks = new HashSet<ushort>()
        {
        };

        static Creature()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                NoDamage_Landblocks = new HashSet<ushort>()
                {
                    // Marketplace
                    0x016C,
                    // Apartments
                    0x7200,
                    0x7300,
                    0x7400,
                    0x7500,
                    0x7600,
                    0x7700,
                    0x7800,
                    0x7900,
                    0x7A00,
                    0x7B00,
                    0x7C00,
                    0x7D00,
                    0x7E00,
                    0x7F00,
                    0x8000,
                    0x8100,
                    0x8200,
                    0x8300,
                    0x8400,
                    0x8500,
                    0x8600,
                    0x8700,
                    0x8800,
                    0x8900,
                    0x8A00,
                    0x8B00,
                    0x8C00,
                    0x8D00,
                    0x8E00,
                    0x8F00,
                    0x9000,
                    0x9100,
                    0x9200,
                    0x9300,
                    0x9400,
                    0x9500,
                    0x9600,
                    0x9700,
                    0x9800,
                    0x9900,
                    0x5360,
                    0x5361,
                    0x5362,
                    0x5363,
                    0x5364,
                    0x5365,
                    0x5366,
                    0x5367,
                    0x5368,
                    0x5369
                };
            }
        }
    }
}
