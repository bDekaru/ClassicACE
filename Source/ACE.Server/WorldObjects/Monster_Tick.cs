using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Pathfinding;
using System;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        protected const double monsterTickInterval = 0.2;

        public double NextMonsterTickTime;

        public double NextFailureCountersDecayTime;

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

            DoTHotTick(currentUnixTime);

            NextMonsterTickTime = currentUnixTime + ThreadSafeRandom.Next((float)monsterTickInterval * 0.5f, (float)monsterTickInterval * 1.5f); // Add some randomization here to keep creatures from acting in perfect synch.

            bool interruptTick = false;
            if (StunnedUntilTimestamp != 0)
            {
                if (StunnedUntilTimestamp >= currentUnixTime)
                {
                    if (NextStunEffectTimestamp <= currentUnixTime)
                    {
                        EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SplatterUpLeftBack), new GameMessageScript(Guid, PlayScript.SplatterUpRightBack), new GameMessageScript(Guid, PlayScript.SplatterUpLeftFront), new GameMessageScript(Guid, PlayScript.SplatterUpRightFront));
                        NextStunEffectTimestamp = currentUnixTime + StunEffectFrequency;
                    }
                    interruptTick = true;
                }
                else
                    StunnedUntilTimestamp = 0;
            }

            if (IsMesmerized)
            {
                if (NextMesmerizeEffectTimestamp <= currentUnixTime)
                {
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SplatterMidLeftBack), new GameMessageScript(Guid, PlayScript.SplatterMidRightBack), new GameMessageScript(Guid, PlayScript.SplatterMidLeftFront), new GameMessageScript(Guid, PlayScript.SplatterMidRightFront));
                    NextMesmerizeEffectTimestamp = currentUnixTime + MesmerizeEffectFrequency;
                }
                interruptTick = true;
            }
            else if(MesmerizeCooldown)
            {
                if (NextMesmerizeEffectTimestamp <= currentUnixTime)
                {
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SplatterMidRightBack));
                    NextMesmerizeEffectTimestamp = currentUnixTime + MesmerizeEffectFrequency;
                }
            }

            if (IsSnared)
            {
                if (NextSnareEffectTimestamp <= currentUnixTime)
                {
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SplatterLowLeftBack), new GameMessageScript(Guid, PlayScript.SplatterLowRightBack), new GameMessageScript(Guid, PlayScript.SplatterLowLeftFront), new GameMessageScript(Guid, PlayScript.SplatterLowRightFront));
                    NextSnareEffectTimestamp = currentUnixTime + SnareEffectFrequency;
                }
            }
            if (SnareCooldown)
            {
                if (NextSnareEffectTimestamp <= currentUnixTime)
                {
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SplatterLowRightBack));
                    NextSnareEffectTimestamp = currentUnixTime + SnareEffectFrequency;
                }
            }

            if (interruptTick)
                return;

            if (!IsAwake)
            {
                if (MonsterState == State.Return)
                    MonsterState = State.Idle;

                if (IsFactionMob || HasFoeType)
                    FactionMob_CheckMonsters();

                return;
            }
            else if (!IsDead)
            {
                if (NextFailureCountersDecayTime < currentUnixTime)
                {
                    if (FailedMovementCount > 0)
                        FailedMovementCount--;

                    if (PhysicsObj?.MovementManager?.MoveToManager?.FailProgressCount > 0)
                        PhysicsObj.MovementManager.MoveToManager.FailProgressCount--;

                    if (MonsterProjectile_OnCollideEnvironment_Counter > 0)
                        MonsterProjectile_OnCollideEnvironment_Counter--;

                    NextFailureCountersDecayTime = currentUnixTime + 10;
                }

                if (PhysicsObj?.MovementManager?.MoveToManager?.FailProgressCount >= 5)
                    CancelMoveTo(WeenieError.ActionCancelled);

                UpdatePosition(!PhysicsObj.IsSticky);
            }
            else
                return;

            var combatPet = this as CombatPet;

            var creatureTarget = AttackTarget as Creature;
            var playerTarget = AttackTarget as Player;

            if (playerTarget != null && playerTarget.IsSneaking)
            {
                if (IsDirectVisible(playerTarget) && PlayerKillerStatus != PlayerKillerStatus.RubberGlue && PlayerKillerStatus != PlayerKillerStatus.Protected)
                    playerTarget.EndSneaking($"{Name} can still see you! You stop sneaking!");
            }

            if (IsAttacking)
            {
                if (PendingEndAttack)
                {
                    EndAttack(false);
                    if (PendingEndAttack)
                        return;
                }
            }

            if (EmoteManager.IsBusy)
                return;

            if (IsMoveToHomePending)
                MoveToHome();
            if (IsMovingToHome || IsMoveToHomePending)
            {
                if (PendingEndMoveToHome)
                    EndMoveToHome();
                return;
            }

            if (AwakeJustToGrantPassage)
            {
                if (IsGrantPassagePending)
                    GrantPassage();
                if (IsGrantingPassage || IsGrantPassagePending)
                {
                    if (PendingEndGrantPassage)
                    {
                        EndGrantPassage(false);
                        if (PendingEndGrantPassage)
                            return;
                    }
                    else
                        return;
                }
                return;
            }

            CheckMissHome();    // tickrate?

            if (IsMoveToHomePending)
                return;

            HandleFindTarget();

            if (IsMoveToHomePending)
                return;

            if (AttackTarget == null)
            {
                TryMoveToHome();
                return;
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

            var distanceToTarget = GetDistanceToTarget();
            if (MonsterState == State.Awake && distanceToTarget >= MaxChaseRange)
            {
                if (HasPendingMovement)
                    CancelMoveTo(WeenieError.ObjectGone);
                FindNextTarget();
                return;
            }

            if (IsSwitchWeaponsPending)
                SwitchWeapons();
            if (IsSwitchingWeapons || IsSwitchWeaponsPending)
                return;

            // select a new weapon if missile launcher is out of ammo
            var weapon = GetEquippedWeapon();
            /*if (weapon != null && weapon.IsAmmoLauncher)
            {
                var ammo = GetEquippedAmmo();
                if (ammo == null)
                    SwitchToMeleeAttack();
            }*/

            if (weapon == null && CurrentAttackType != null && CurrentAttackType == CombatType.Missile)
            {
                EquipInventoryItems(true, false, true, false);
                DoAttackStance();
                CurrentAttackType = null;
            }

            // decide current type of attack
            if (CurrentAttackType == null)
            {
                CurrentAttackType = GetNextAttackType();
                if (CurrentAttackType != CombatType.Missile || !MissileCombatMeleeRangeMode)
                    MaxRange = GetMaxRange();
                else
                    MaxRange = MaxMeleeRange;

                if (AiIncapableOfAnyMotion && CurrentAttackType != CombatType.Magic)
                    CurrentAttackType = null;

                //if (CurrentAttack == AttackType.Magic)
                //MaxRange = MaxMeleeRange;   // FIXME: server position sync
            }

            var isMeleeVisible = true;
            var isDirectiVisible = true;
            var isInSight = true;
            var isMelee = CurrentAttackType == CombatType.Melee;
            var inRange = distanceToTarget < MaxRange;
            var inRangeToStop = distanceToTarget < MaxRange * 0.8f;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && !AiIncapableOfAnyMotion)
            {
                PathfindingEnabled = Pathfinder.PathfindingEnabled;

                isMeleeVisible = IsMeleeVisible(AttackTarget, true);
                isDirectiVisible = IsDirectVisible(AttackTarget, true);

                if (isMelee)
                    isInSight = isMeleeVisible;
                else
                    isInSight = isDirectiVisible;

                if ((isMelee && isInSight) || (!isMelee && isInSight && inRange))
                {
                    FailedSightCount = 0;

                    if (IsEmotePending || IsRouteStartPending || IsEmoting || IsRouting)
                    {
                        // If we can see our target abort everything and go for it.
                        if (DebugMove)
                            Console.WriteLine($"{Name} ({Guid}) Target in Sight!");

                        IsEmotePending = false;
                        IsWanderingPending = false;
                        IsRouteStartPending = false;

                        // Figure out a way to cancel motions so they will actually stop mid-play client-side. MotionCommand.Dead does it but I haven't been able to figure out why.
                        //if (IsEmoting)
                        //    PendingEndEmoting = true;

                        if (IsWandering)
                            PendingEndWandering = true;

                        if (IsRouting)
                            PendingEndRoute = true;
                    }
                    else if (!isMelee && isInSight && inRangeToStop && IsMoving && !IsTurning && !IsWandering) // Not necessary for melee as the range is close enough to the target that we never have to cancel the moveTo.
                    {
                        if (DebugMove)
                            Console.WriteLine($"{Name} ({Guid}) Target in Range!");

                        if (HasPendingMovement)
                            CancelMoveTo(WeenieError.ObjectGone);
                    }
                }
                else if (!isMelee && isInSight && !inRange)
                {
                    if (DebugMove)
                        Console.WriteLine($"{Name} ({Guid}) Target Out of Range!");
                }
                else if (!IsRouting && !IsWandering && !IsEmoting && !IsSwitchingWeapons && !IsAttacking)
                {
                    FailedSightCount++;
                    if (FailedSightCount >= FailedSightThreshold && HasPendingMovement)
                    {
                        if (DebugMove)
                            Console.WriteLine($"{Name} ({Guid}) Target Lost!");

                        if (HasPendingMovement)
                            CancelMoveTo(WeenieError.ObjectGone);
                    }
                }

                if (IsEmotePending)
                    Emote();
                if (IsEmoting || IsEmotePending)
                {
                    if (IsEmoting && !HasPendingAnimations && (ExpectedEmoteEndTime + 5) < currentUnixTime)
                    {
                        // In rare cases it seems OnMotionDone for an emote never gets triggered. This is a failsafe that gets the creature moving again.
                        log.Warn($"[Monster_Tick] 0x{Guid.Full:X8} {Name}: Fixed stuck IsEmoting.");
                        EndEmoting();
                    }

                    if (PendingEndEmoting)
                    {
                        EndEmoting(false);
                        if (PendingEndEmoting)
                            return;
                    }
                    else
                        return;
                }

                if (IsGrantPassagePending)
                    GrantPassage();
                if (IsGrantingPassage || IsGrantPassagePending)
                {
                    if (PendingEndGrantPassage)
                    {
                        EndGrantPassage(false);
                        if (PendingEndGrantPassage)
                            return;
                    }
                    else
                        return;
                }

                if (IsWanderingPending)
                    Wander();
                if (IsWandering || IsWanderingPending)
                {
                    if (PendingEndWandering)
                    {
                        EndWandering(false);
                        if (PendingEndWandering)
                            return;
                    }
                    else
                        return;
                }

                if (IsRouteStartPending)
                {
                    StartRoute();
                    if (IsRouteStartPending)
                        return;
                }
                if (IsRouting)
                {
                    if (PendingEndRoute)
                        EndRoute(false);
                    else if (PendingRetryRoute)
                        RetryRoute();
                    else if (PendingContinueRoute)
                        ContinueRoute();

                    if (IsRouting)
                        return;
                }

                if (NextNoCounterResetTime <= currentUnixTime)
                {
                    AttacksReceivedWithoutBeingAbleToCounter = 0;
                    NextNoCounterResetTime = double.MaxValue;
                }

                var distanceCovered = PreviousTickPosition?.SquaredDistanceTo(Location);
                PreviousTickPosition = new Position(Location);

                if (IsMoving && distanceCovered > 0.2)
                    AttacksReceivedWithoutBeingAbleToCounter = 0;

                if (AttackTarget != null && !Location.Indoors)
                {
                    var heightDifference = Math.Abs(Location.PositionZ - AttackTarget.Location.PositionZ);

                    if (heightDifference > 2.0 && AttacksReceivedWithoutBeingAbleToCounter > 2)
                    {
                        AttacksReceivedWithoutBeingAbleToCounter = 0;

                        if (HasRangedWeapon && CurrentAttackType == CombatType.Melee && !IsSwitchWeaponsPending && LastWeaponSwitchTime + 5 < currentUnixTime)
                        {
                            TrySwitchToMissileAttack();
                            return;
                        }
                        else
                        {
                            FindNewHome(100, 260, 100);
                            TryMoveToHome();
                            return;
                        }
                    }
                }
            }

            if (CurrentAttackType != null)
            {
                if (CurrentAttackType != CombatType.Missile || Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                {
                    var canStick = PhysicsObj.IsSticky && CurrentAttackType == CombatType.Melee && isMeleeVisible;

                    if (!canStick && distanceToTarget > MaxRange || (!AiOmnidirectional && !IsFacing(AttackTarget) && !IsSelfCast()))
                    {
                        bool failedThresholds = FailedMovementCount >= FailedMovementThreshold || FailedSightCount >= FailedSightThreshold;

                        if (!IsMoving && !failedThresholds && !AiIncapableOfAnyMotion)
                            StartMovement();
                        else
                        {
                            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                            {
                                if (failedThresholds)
                                {
                                    FailedMovementCount = 0;
                                    FailedSightCount = 0;

                                    var currentTarget = AttackTarget;
                                    FindNextTarget();

                                    if (currentTarget == AttackTarget)
                                    {
                                        if (HasRangedWeapon && CurrentAttackType == CombatType.Melee && LastWeaponSwitchTime + MaxSwitchWeaponFrequency < currentUnixTime && isDirectiVisible)
                                            TrySwitchToMissileAttack();
                                        else
                                        {
                                            if (LastEmoteTime + MaxEmoteFrequency < currentUnixTime && EmoteChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                                TryEmoting();

                                            if (LastWanderTime + MaxWanderFrequency < currentUnixTime && WanderChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                            {
                                                if (PathfindingEnabled && Indoors && !LastRouteStartAttemptWasNullRoute)
                                                    TryWandering(160, 200, 5);
                                                else
                                                    TryWandering(100, 260, 7);
                                            }

                                            if (PathfindingEnabled && Location.Indoors)
                                                TryRoute();
                                        }
                                    }
                                }
                                else if (HasRangedWeapon && CurrentAttackType == CombatType.Melee && distanceToTarget > 20 && LastWeaponSwitchTime + MaxSwitchWeaponFrequency < currentUnixTime && isDirectiVisible)
                                    TrySwitchToMissileAttack();
                            }
                        }
                    }
                    else
                        Attack();
                }
                else
                {
                    if (IsMoving)
                        return;

                    if (!IsFacing(AttackTarget))
                        StartMovement();
                    else if (distanceToTarget <= MaxRange)
                        Attack();
                    else
                    {
                        // monster switches to melee combat immediately,
                        // if target is beyond max range?

                        // should ranged mobs only get CurrentTargets within MaxRange?
                        //Console.WriteLine($"{Name}.MissileAttack({AttackTarget.Name}): targetDist={targetDist}, MaxRange={MaxRange}, switching to melee");
                        TrySwitchToMeleeAttack();
                    }
                }
            }

            // pets drawing aggro
            if (combatPet != null)
                combatPet.PetCheckMonsters();
        }

        private double StunnedUntilTimestamp;
        private double NextStunEffectTimestamp;
        private static double StunEffectFrequency = 0.5;

        public void StunFor(double seconds)
        {
            IsEmotePending = false;
            IsWanderingPending = false;
            IsRouteStartPending = false;

            if (IsEmoting)
                EndEmoting();

            if (IsWandering)
                EndWandering();

            if (IsRouting)
                EndRoute();

            if (IsAttacking)
                EndAttack();

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            StunnedUntilTimestamp = Time.GetFutureUnixTime(seconds);
            NextStunEffectTimestamp = 0;
        }

        private double NextSnareEffectTimestamp;
        private static double SnareEffectFrequency = 0.5;

        public bool IsSnared { get; private set; }
        public bool SnareCooldown { get; set; }
        public void Ensnare()
        {
            IsEmotePending = false;
            IsWanderingPending = false;
            IsRouteStartPending = false;

            if (IsEmoting)
                EndEmoting();

            if (IsWandering)
                EndWandering();

            if (IsRouting)
                EndRoute();

            if (IsAttacking)
                EndAttack();

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsSnared = true;
            NextSnareEffectTimestamp = 0;
        }

        public void RemoveSnare()
        {
            if (IsSnared)
            {
                IsEmotePending = false;
                IsWanderingPending = false;
                IsRouteStartPending = false;

                if (IsEmoting)
                    EndEmoting();

                if (IsWandering)
                    EndWandering();

                if (IsRouting)
                    EndRoute();

                if (IsAttacking)
                    EndAttack();

                if (HasPendingMovement)
                    CancelMoveTo(WeenieError.ObjectGone);

                IsSnared = false;
            }
        }

        private double NextMesmerizeEffectTimestamp;
        private static double MesmerizeEffectFrequency = 0.5;

        public bool IsMesmerized { get; private set; }
        public bool MesmerizeCooldown { get; set; }

        public void Mesmerize()
        {
            IsEmotePending = false;
            IsWanderingPending = false;
            IsRouteStartPending = false;

            if (IsEmoting)
                EndEmoting();

            if (IsWandering)
                EndWandering();

            if (IsRouting)
                EndRoute();

            if (IsAttacking)
                EndAttack();

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsMesmerized = true;
            NextMesmerizeEffectTimestamp = 0;
        }

        public void RemoveMesmerize()
        {
            if(IsMesmerized)
                IsMesmerized = false;
        }
    }
}
