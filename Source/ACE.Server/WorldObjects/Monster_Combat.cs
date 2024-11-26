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
        public double NextMagicAttackTime
        {
            get
            {
                // defaults to most common value found in py16 db
                var magicDelay = AiUseMagicDelay ?? 3.0f;

                return PrevAttackTime + magicDelay;
            }
        }

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

            if (!IsModified)
            {
                // if caster, roll for spellcasting chance
                if (HasKnownSpells && TryRollSpell())
                    return CombatType.Magic;
            }
            else
            {
                // If we're modified(our level has been altered) get our custom spellbook instead of the generic one.
                if (HasKnownSpellsModified && TryRollSpellModified())
                    return CombatType.Magic;
            }

            if (IsRanged)
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
                Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - DoAttackStance - stanceTime: {stanceTime}, isAnimating: {IsAnimating}");

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
                            var failDelay = ThreadSafeRandom.Next(0.0f, powerupTime);

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

            //PhysicsObj.update_object();
            //UpdatePosition_SyncLocation();

            return !PhysicsObj.IsAnimating && !HasPendingMovement;
        }

        /// <summary>
        /// Returns TRUE if creature can perform its next attack
        /// </summary>
        /// <returns></returns>
        public bool AttackReady()
        {
            var nextAttackTime = CurrentAttackType == CombatType.Magic ? NextMagicAttackTime : NextAttackTime;

            if (Timers.RunningTime < nextAttackTime || !IsAttackRange())
                return false;

            //PhysicsObj.update_object();
            //UpdatePosition_SyncLocation();

            return !PhysicsObj.IsAnimating;
        }


        private bool IsAttackPending = false;
        private bool IsAttacking = false;
        private bool PendingEndAttack = false;
        private Creature NextSwingAttackTarget = null;

        public void TryAttack()
        {
            //Console.WriteLine("Pathfinding: TryAttack");

            if (IsAttacking)
                return;

            NextSwingAttackTarget = AttackTarget as Creature;

            IsAttackPending = true;
        }

        /// <summary>
        /// Performs the current attack on the target
        /// </summary>
        private void Attack()
        {
            if (DebugMove)
                Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - Attack");

            if (IsAttacking)
                return;

            if (!MoveReady() || !AttackReady())
                return;

            //Console.WriteLine("Pathfinding: Attack");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsAttackPending = false;
            IsAttacking = true;

            if (CurrentAttackType == null)
            {
                EndAttack();
                return;
            }

            var targetCreature = AttackTarget as Creature;
            if (targetCreature == null || IsDead || targetCreature.IsDead)
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

            if(AttackTarget != NextSwingAttackTarget)
            {
                // We changed targets since we initiated our attack attempt.
                EndAttack();
                return;
            }

            FailedMovementCount = 0;

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
            //Console.WriteLine("Pathfinding: EndAttack");

            if (!forced && !MoveReady())
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsAttackPending = false;
            IsAttacking = false;

            NextSwingAttackTarget = null;
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
        public virtual void TakeDamageOverTime(float amount, DamageType damageType)
        {
            if (IsDead) return;

            TakeDamage(null, damageType, amount);

            // splatter effects
            var hitSound = new GameMessageSound(Guid, Sound.HitFlesh1, 0.5f);
            //var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + playerSource.GetSplatterHeight() + playerSource.GetSplatterDir(this));
            var splatter = new GameMessageScript(Guid, damageType == DamageType.Nether ? PlayScript.HealthDownVoid : PlayScript.DirtyFightingDamageOverTime);
            EnqueueBroadcast(hitSound, splatter);

            if (Health.Current <= 0) return;

            if (amount >= Health.MaxValue * 0.25f)
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

            string msg = null;

            var type = ChatMessageType.CombatSelf;

            if (damageType == DamageType.Nether)
            {
                msg = $"You {verb} {Name} for {iAmount} points of periodic nether damage!";
                type = ChatMessageType.Magic;
            }
            else if (damageType == DamageType.Fire)
            {
                msg = $"You {verb} {Name} for {iAmount} points of periodic fire damage!";
                type = ChatMessageType.Magic;
            }
            else if (aetheria)
            {
                msg = $"With Surge of Affliction you {verb} {iAmount} points of health from {Name}!";
                type = ChatMessageType.Magic;
            }
            else
            {
                /*var skill = source.GetCreatureSkill(Skill.DirtyFighting);
                var attack = skill.AdvancementClass == SkillAdvancementClass.Specialized ? "Bleeding Assault" : "Bleeding Blow";
                msg = $"With {attack} you {verb} {iAmount} points of health from {Name}!";*/

                msg = $"You bleed {Name} for {iAmount} points of periodic health damage!";
                type = ChatMessageType.CombatSelf;
            }
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
            var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + GetSplatterHeight() + GetSplatterDir(target));
            target.EnqueueBroadcast(new GameMessageScript(target.Guid, splatter));
        }

        public CombatStyle AiAllowedCombatStyle
        {
            get => (CombatStyle)(GetProperty(PropertyInt.AiAllowedCombatStyle) ?? 0);
            set { if (value == 0) RemoveProperty(PropertyInt.AiAllowedCombatStyle); else SetProperty(PropertyInt.AiAllowedCombatStyle, (int)value); }
        }

        private static readonly ConcurrentDictionary<uint, BodyPartTable> BPTableCache = new ConcurrentDictionary<uint, BodyPartTable>();

        public static BodyPartTable GetBodyParts(uint wcid)
        {
            if (!BPTableCache.TryGetValue(wcid, out var bpTable))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(wcid);

                bpTable = new BodyPartTable(weenie);
                BPTableCache[wcid] = bpTable;
            }
            return bpTable;
        }

        public static BodyPartTable GetBodyParts(Creature creature)
        {
            return new BodyPartTable(creature.Weenie);
        }

        /// <summary>
        /// Flag indicates if a monster will aggro, but not attack
        /// </summary>
        public bool NeverAttack
        {
            get => GetProperty(PropertyBool.NeverAttack) ?? false;
            set { if (!value) RemoveProperty(PropertyBool.NeverAttack); else SetProperty(PropertyBool.NeverAttack, value); }
        }

        private double StunnedUntilTimestamp;
        private double NextStunEffectTimestamp;
        private static double StunEffectFrequency = 0.5;

        public void StunFor(double seconds)
        {
            StunnedUntilTimestamp = Time.GetFutureUnixTime(seconds);
            NextStunEffectTimestamp = 0;
        }
    }
}
