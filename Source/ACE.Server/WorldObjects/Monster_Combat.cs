using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Database;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Monster combat general functions
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// The current attack target for the monster
        /// </summary>
        public WorldObject AttackTarget;

        private WorldObject _LastAttackTarget;
        public WorldObject LastAttackTarget
        {
            get
            {
                if (_LastAttackTarget != null && _LastAttackTarget.IsDestroyed)
                    _LastAttackTarget = null;

                return _LastAttackTarget;
            }
            set { _LastAttackTarget = value; }
        }

        /// <summary>
        /// A monster chooses 1 attack height
        /// </summary>
        public AttackHeight? AttackHeight;

        /// <summary>
        /// The next type of attack (melee/range/magic)
        /// </summary>
        public CombatType? CurrentAttackType;

        /// <summary>
        /// The maximum distance for the next attack
        /// </summary>
        public float MaxRange;

        /// <summary>
        ///  The time when monster started its last attack
        /// </summary>
        public double PrevAttackTime { get; set; }

        /// <summary>
        /// The time when monster can perform its next attack
        /// </summary>
        public double NextAttackTime { get; set; }

        /// <summary>
        /// The time when monster can perform its next magic attack
        /// </summary>
        public double NextSpellCastTime { get; set; }

        /// <summary>
        /// Returns true if monster is dead
        /// </summary>
        public bool IsDead => Health.Current <= 0;

        /// <summary>
        /// A list of possible attack heights for this monster,
        /// as determined by the combat maneuvers table
        /// </summary>
        private List<AttackHeight> _attackHeights;

        public List<AttackHeight> AttackHeights
        {
            get
            {
                if (CombatTable == null) return null;

                if (_attackHeights == null)
                    _attackHeights = CombatTable.CMT.Select(m => m.AttackHeight).Distinct().ToList();

                return _attackHeights;
            }
        }

        /// <summary>
        /// Selects a random attack height for the next attack
        /// </summary>
        public AttackHeight ChooseAttackHeight()
        {
            var rng = ThreadSafeRandom.Next(0, AttackHeights.Count - 1);
            return AttackHeights[rng];
        }

        public CombatType GetNextAttackType()
        {
            if (CombatTable == null)
                GetCombatTable();

            // if caster, roll for spellcasting chance
            if (HasKnownSpells && SpellCastReady() && TryRollSpell())
                return CombatType.Magic;
            else if (IsRanged)
                return CombatType.Missile;
            else
                return CombatType.Melee;
        }

        /// <summary>
        /// Reads the combat maneuvers table from the DAT file
        /// </summary>
        public void GetCombatTable()
        {
            if (CombatTableDID != null)
                CombatTable = DatManager.PortalDat.ReadFromDat<CombatManeuverTable>(CombatTableDID.Value);
        }

        /// <summary>
        /// Switch to attack stance
        /// </summary>
        public void DoAttackStance()
        {
            var combatMode = IsRanged ? CombatMode.Missile : CombatMode.Melee;

            var stanceTime = SetCombatMode(combatMode);

            var nextTime = Timers.RunningTime + stanceTime;

            if (NextMoveTime > Timers.RunningTime)
                NextMoveTime += stanceTime;
            else
                NextMoveTime = nextTime;

            if (NextAttackTime > Timers.RunningTime)
                NextAttackTime += stanceTime;
            else
                NextAttackTime = nextTime;

            if (IsRanged)
            {
                PrevAttackTime = NextAttackTime + MissileDelay - (AiUseMagicDelay ?? 3.0f);

                NextAttackTime += MissileDelay;
            }

            if (NeverAttack)
            {
                PrevAttackTime = NextAttackTime = double.MaxValue - (AiUseMagicDelay ?? 3.0f);
            }

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).DoAttackStance() - stanceTime: {stanceTime}, isAnimating: {IsAnimating}");

            PhysicsObj.StartTimer();
        }

        public float GetMaxRange()
        {
            // FIXME
            var it = 0;
            bool? isVisible = null;

            while (CurrentAttackType == CombatType.Magic)
            {
                // select a magic spell
                //CurrentSpell = GetRandomSpell();
                if (CurrentSpell.IsProjectile)
                {
                    if (isVisible == null)
                        isVisible = IsDirectVisible(AttackTarget);

                    // ensure direct los
                    if (!isVisible.Value)
                    {
                        // reroll attack type
                        CurrentAttackType = GetNextAttackType();
                        it++;

                        // max iterations to melee?
                        if (it >= 10)
                        {
                            //log.Warn($"{Name} ({Guid}) reached max iterations");
                            CurrentAttackType = CombatType.Melee;

                            var powerupTime = (float)(PowerupTime ?? 1.0f);
                            var failDelay = ThreadSafeRandom.Next(powerupTime * 0.5f, powerupTime * 1.5f);

                            NextMoveTime = Timers.RunningTime + failDelay;
                        }
                        continue;
                    }
                }
                return GetSpellMaxRange();
            }

            if (CurrentAttackType == CombatType.Missile)
            {
                /*var weapon = GetEquippedWeapon();
                if (weapon == null) return MaxMissileRange;

                var maxRange = weapon.GetProperty(PropertyInt.WeaponRange) ?? MaxMissileRange;
                return Math.Min(maxRange, MaxMissileRange);     // in-game cap @ 80 yds.*/
                return GetMaxMissileRange();
            }
            else
                return MaxMeleeRange;   // distance_to_target?
        }

        public bool MoveReady()
        {
            if (Timers.RunningTime < NextMoveTime)
                return false;

            return !PhysicsObj.IsAnimating && !HasPendingMovement;
        }

        /// <summary>
        /// Returns TRUE if creature can perform its next attack
        /// </summary>
        /// <returns></returns>
        public bool AttackReady()
        {
            if (Timers.RunningTime < NextAttackTime)
                return false;

            return !PhysicsObj.IsAnimating || !HasPendingMovement;
        }

        /// <summary>
        /// Returns TRUE if creature can cast its next spell
        /// </summary>
        /// <returns></returns>
        public bool SpellCastReady()
        {
            return Timers.RunningTime > NextSpellCastTime;
        }

        private bool IsAttacking = false;
        private bool PendingEndAttack = false;

        /// <summary>
        /// Performs the current attack on the target
        /// </summary>
        private void Attack()
        {
            if (!MoveReady() || !AttackReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).Attack()");

            if (IsAttacking)
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsAttacking = true;

            var targetCreature = AttackTarget as Creature;
            if (!IsAttackRange() || CurrentAttackType == null || targetCreature == null || IsDead || targetCreature.IsDead)
            {
                EndAttack();
                return;
            }

            if (AiImmobile && CurrentAttackType == CombatType.Melee)
            {
                var targetDist = GetDistanceToTarget();
                if (targetDist > MaxRange)
                {
                    EndAttack();
                    return;
                }
            }

            FailedMovementCount = 0;
            FailedSightCount = 0;

            switch (CurrentAttackType)
            {
                case CombatType.Melee:
                    MeleeAttack();
                    break;
                case CombatType.Missile:
                    RangeAttack();
                    break;
                case CombatType.Magic:
                    MagicAttack();
                    break;
                default:
                    EndAttack();
                    return;
            }

            EmoteManager.OnAttack(AttackTarget);

            AttacksReceivedWithoutBeingAbleToCounter = 0;
        }

        private void EndAttack(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndAttack()");

            PendingEndAttack = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsAttacking = false;

            CurrentAttackType = null;
            MaxRange = 0.0f;
        }

        public DamageType GetDamageType(PropertiesBodyPart attackPart, CombatType? combatType = null)
        {
            var weapon = GetEquippedWeapon();

            if (weapon != null)
                return GetDamageType(false, combatType);
            else
            {
                var damageType = attackPart.DType;

                if (damageType.IsMultiDamage())
                    damageType = damageType.SelectDamageType();

                return damageType;
            }
        }

        /// <summary>
        /// Simplified monster take damage over time function, only called for DoTs currently
        /// </summary>
        public virtual void TakeDamageOverTime(float amount, DamageType damageType, bool suppressEffects = false)
        {
            if (IsDead) return;

            TakeDamage(null, damageType, amount);

            if (!suppressEffects)
            {
                // splatter effects
                var hitSound = new GameMessageSound(Guid, Sound.HitFlesh1, 0.5f);
                //var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + playerSource.GetSplatterHeight() + playerSource.GetSplatterDir(this));
                var splatter = new GameMessageScript(Guid, damageType == DamageType.Nether ? PlayScript.HealthDownVoid : PlayScript.DirtyFightingDamageOverTime);
                EnqueueBroadcast(hitSound, splatter);
            }

            if (Health.Current <= 0) return;

            if (!suppressEffects && amount >= Health.MaxValue * 0.25f)
            {
                var painSound = (Sound)Enum.Parse(typeof(Sound), "Wound" + ThreadSafeRandom.Next(1, 3), true);
                EnqueueBroadcast(new GameMessageSound(Guid, painSound, 1.0f));
            }
        }

        /// <summary>
        /// Notifies the damage over time (DoT) source player of the tick damage amount
        /// </summary>
        public void TakeDamageOverTime_NotifySource(Player source, DamageType damageType, float amount, bool aetheria = false)
        {
            if (!PropertyManager.GetBool("show_dot_messages").Item)
                return;

            var iAmount = (uint)Math.Round(amount);

            var notifyType = damageType == DamageType.Undef ? DamageType.Health : damageType;

            string verb = null, plural = null;
            var percent = amount / Health.MaxValue;
            Strings.GetAttackVerb(notifyType, percent, ref verb, ref plural);
            var damageTypeString = notifyType.GetName().ToLower();

            string msg = null;

            ChatMessageType type = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM ? ChatMessageType.x1B : ChatMessageType.Magic;

            var targetName = source == this ? "yourself" : Name;
            if (!aetheria)
            {
                if (notifyType == DamageType.Health || notifyType == DamageType.Slash || notifyType == DamageType.Pierce || notifyType == DamageType.Bludgeon)
                {
                    /*var skill = source.GetCreatureSkill(Skill.DirtyFighting);
                    var attack = skill.AdvancementClass == SkillAdvancementClass.Specialized ? "Bleeding Assault" : "Bleeding Blow";
                    msg = $"With {attack} you {verb} {iAmount} points of health from {Name}!";*/

                    verb = "bleed";
                }

                msg = $"You {verb} {targetName} for {iAmount} points of periodic {damageTypeString} damage!";
            }
            else
                msg = $"With Surge of Affliction you {verb} {iAmount} points of health from {targetName}!";
            source.SendMessage(msg, type);
        }

        /// <summary>
        /// Applies some amount of damage to this monster from source
        /// </summary>
        /// <param name="source">The attacker / source of damage</param>
        /// <param name="amount">The amount of damage rounded</param>
        public virtual uint TakeDamage(WorldObject source, DamageType damageType, float amount, bool crit = false)
        {
            var tryDamage = (int)Math.Round(amount);
            var damage = -UpdateVitalDelta(Health, -tryDamage);

            // TODO: update monster stamina?

            // source should only be null for combined DoT ticks from multiple sources
            if (source != null)
            {
                if (damage >= 0)
                    DamageHistory.Add(source, damageType, (uint)damage);
                else
                    DamageHistory.OnHeal((uint)-damage);
            }

            if (Health.Current <= 0)
            {
                OnDeath(DamageHistory.LastDamager, damageType, crit);

                Die();
            }
            return (uint)Math.Max(0, damage);
        }

        public void EmitSplatter(Creature target, float damage)
        {
            if (target.IsDead) return;

            target.EnqueueBroadcast(new GameMessageSound(target.Guid, Sound.HitFlesh1, 0.5f));
            if (damage >= target.Health.MaxValue * 0.25f)
            {
                var painSound = (Sound)Enum.Parse(typeof(Sound), "Wound" + ThreadSafeRandom.Next(1, 3), true);
                target.EnqueueBroadcast(new GameMessageSound(target.Guid, painSound, 1.0f));
            }

            if (damage > 0)
            {
                var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + GetSplatterHeight() + GetSplatterDir(target));
                target.EnqueueBroadcast(new GameMessageScript(target.Guid, splatter));
            }
        }

        public CombatStyle AiAllowedCombatStyle
        {
            get => (CombatStyle)(GetProperty(PropertyInt.AiAllowedCombatStyle) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.AiAllowedCombatStyle); else SetProperty(PropertyInt.AiAllowedCombatStyle, (int)value); }
        }

        private static readonly ConcurrentDictionary<uint, BodyPartTable> BPTableCache = new ConcurrentDictionary<uint, BodyPartTable>();


        public static BodyPartTable GetBodyParts(uint wcid)
        {
            // get cached body parts table
            if (!BPTableCache.TryGetValue(wcid, out var bpTable))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

                bpTable = new BodyPartTable(weenie);
                BPTableCache[wcid] = bpTable;
            }
            return bpTable;
        }

        private BodyPartTable ModifiedBodyPartTable = null;
        public BodyPartTable GetBodyParts()
        {
           if (IsModified) // If we're modified(our level has been altered) get our custom body parts instead of the generic ones.
            {
                if (ModifiedBodyPartTable == null)
                    ModifiedBodyPartTable = new BodyPartTable(Biota);
                return ModifiedBodyPartTable;
            }
            else
                return GetBodyParts(WeenieClassId);
        }
        public void ClearModifiedBodyPartTable()
        {
            ModifiedBodyPartTable = null;
        }

        /// <summary>
        /// Flag indicates if a monster will aggro, but not attack
        /// </summary>
        public bool NeverAttack
        {
            get => GetProperty(PropertyBool.NeverAttack) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.NeverAttack); else SetProperty(PropertyBool.NeverAttack, value); }
        }
    }
}
