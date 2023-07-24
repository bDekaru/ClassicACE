using ACE.Entity;
using ACE.Entity.Enum;
using System;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        protected const double monsterTickInterval = 0.2;

        public double NextMonsterTickTime;

        private bool firstUpdate = true;

        private int AttacksReceivedWithoutBeingAbleToCounter = 0;
        private double NextNoCounterResetTime = double.MaxValue;
        private static double NoCounterInterval = 60;
        private Position PreviousTickPosition = new Position();

        /// <summary>
        /// Primary dispatch for monster think
        /// </summary>
        public void Monster_Tick(double currentUnixTime)
        {
            if (IsChessPiece && this is GamePiece gamePiece)
            {
                // faster than vtable?
                gamePiece.Tick(currentUnixTime);
                return;
            }

            if (IsPassivePet && this is Pet pet)
            {
                pet.Tick(currentUnixTime);
                return;
            }

            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            if (!IsAwake)
            {
                if (MonsterState == State.Return)
                    MonsterState = State.Idle;

                if (IsFactionMob || HasFoeType)
                    FactionMob_CheckMonsters();

                return;
            }

            if (IsDead) return;

            if (EmoteManager.IsBusy) return;

            HandleFindTarget();

            CheckMissHome();    // tickrate?

            if (AttackTarget == null && MonsterState != State.Return)
            {
                Sleep();
                return;
            }

            if (MonsterState == State.Return)
            {
                Movement();
                return;
            }

            var combatPet = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;
            var playerTarget = AttackTarget as Player;

            if (playerTarget != null && playerTarget.IsSneaking)
            {
                if (IsDirectVisible(playerTarget))
                    playerTarget.EndSneaking($"{Name} can still see you! You stop sneaking!");
            }

            if (creatureTarget != null && (creatureTarget.IsDead || (combatPet == null && !IsVisibleTarget(creatureTarget))) || (playerTarget != null && playerTarget.IsSneaking))
            {
                FindNextTarget();
                return;
            }

            if (firstUpdate)
            {
                if (CurrentMotionState.Stance == MotionStance.NonCombat)
                    DoAttackStance();

                if (IsAnimating)
                {
                    //PhysicsObj.ShowPendingMotions();
                    PhysicsObj.update_object();
                    return;
                }

                firstUpdate = false;
            }

            // select a new weapon if missile launcher is out of ammo
            var weapon = GetEquippedWeapon();
            /*if (weapon != null && weapon.IsAmmoLauncher)
            {
                var ammo = GetEquippedAmmo();
                if (ammo == null)
                    SwitchToMeleeAttack();
            }*/

            if (weapon == null && CurrentAttack != null && CurrentAttack == CombatType.Missile)
            {
                EquipInventoryItems(true, false, true, false);
                DoAttackStance();
                CurrentAttack = null;
            }

            // decide current type of attack
            if (CurrentAttack == null)
            {
                CurrentAttack = GetNextAttackType();
                if (CurrentAttack != CombatType.Missile || !MissileCombatMeleeRangeMode)
                    MaxRange = GetMaxRange();
                else
                    MaxRange = MaxMeleeRange;

                //if (CurrentAttack == AttackType.Magic)
                //MaxRange = MaxMeleeRange;   // FIXME: server position sync
            }

            if (PhysicsObj.IsSticky)
                UpdatePosition(false);

            // get distance to target
            var targetDist = GetDistanceToTarget();
            //Console.WriteLine($"{Name} ({Guid}) - Dist: {targetDist}");

            if (PathfindingPending)
                return;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (NextNoCounterResetTime <= currentUnixTime)
                {
                    AttacksReceivedWithoutBeingAbleToCounter = 0;
                    NextNoCounterResetTime = double.MaxValue;
                }

                var distanceCovered = PreviousTickPosition?.SquaredDistanceTo(Location);
                PreviousTickPosition = new Position(Location);

                if (IsTurning || (IsMoving && distanceCovered > 0.2))
                    AttacksReceivedWithoutBeingAbleToCounter = 0;

                if (AttackTarget != null && !Location.Indoors)
                {
                    var heightDifference = Math.Abs(Location.PositionZ - AttackTarget.Location.PositionZ);

                    if (heightDifference > 2.0 && AttacksReceivedWithoutBeingAbleToCounter > 2)
                    {
                        AttacksReceivedWithoutBeingAbleToCounter = 0;

                        if (HasRangedWeapon && CurrentAttack == CombatType.Melee && !SwitchWeaponsPending && LastWeaponSwitchTime + 5 < currentUnixTime)
                        {
                            TrySwitchToMissileAttack();
                            return;
                        }
                        else
                        {
                            FindNewHome(100, 260, 100);
                            MoveToHome();
                            return;
                        }
                    }
                }
            }

            if (CurrentAttack != CombatType.Missile || Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (targetDist > MaxRange || (!IsFacing(AttackTarget) && !IsSelfCast()))
                {
                    // turn / move towards
                    if (!IsTurning && !IsMoving)
                        StartTurn();
                    else
                    {
                        if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                        {
                            if (HasRangedWeapon && CurrentAttack == CombatType.Melee && (targetDist > 20 || PhysicsObj.MovementManager.MoveToManager.FailProgressCount > 10) && !SwitchWeaponsPending && LastWeaponSwitchTime + 5 < currentUnixTime)
                                TrySwitchToMissileAttack();
                            else if (PhysicsObj.MovementManager.MoveToManager.FailProgressCount > 20 && !PathfindingPending && LastPathfindTime + 5 < currentUnixTime)
                                TryPathfind(100, 260, 3);
                            else
                                Movement();
                        }
                        else
                            Movement();
                    }
                }
                else
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
            }
            else
            {
                if (IsTurning || IsMoving)
                {
                    Movement();
                    return;
                }

                if (!IsFacing(AttackTarget))
                {
                    StartTurn();
                }
                else if (targetDist <= MaxRange)
                {
                    // perform attack
                    if (AttackReady())
                        Attack();
                }
                else
                {
                    // monster switches to melee combat immediately,
                    // if target is beyond max range?

                    // should ranged mobs only get CurrentTargets within MaxRange?
                    //Console.WriteLine($"{Name}.MissileAttack({AttackTarget.Name}): targetDist={targetDist}, MaxRange={MaxRange}, switching to melee");
                    TrySwitchToMeleeAttack();
                }
            }

            // pets drawing aggro
            if (combatPet != null)
                combatPet.PetCheckMonsters();
        }
    }
}
