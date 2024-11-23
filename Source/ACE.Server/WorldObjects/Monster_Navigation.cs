using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Pathfinding;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// Return to home if target distance exceeds this range
        /// </summary>
        public const float MaxChaseRange = 96.0f;
        public const float MaxChaseRangeSq = MaxChaseRange * MaxChaseRange;

        /// <summary>
        /// Determines if a monster is within melee range of target
        /// </summary>
        //public const float MaxMeleeRange = 1.5f;
        public const float MaxMeleeRange = 0.75f;
        //public const float MaxMeleeRange = 1.5f + 0.6f + 0.1f;    // max melee range + distance from + buffer

        /// <summary>
        /// The maximum range for a monster missile attack
        /// </summary>
        //public const float MaxMissileRange = 80.0f;
        //public const float MaxMissileRange = 40.0f;   // for testing

        /// <summary>
        /// The distance per second from running animation
        /// </summary>
        public float MoveSpeed;

        /// <summary>
        /// The run skill via MovementSystem GetRunRate()
        /// </summary>
        public float RunRate;

        /// <summary>
        /// Flag indicates monster is turning towards target
        /// </summary>
        public bool IsTurning = false;

        /// <summary>
        /// Flag indicates monster is moving towards target
        /// </summary>
        public bool IsMoving = false;

        /// <summary>
        /// The last time a movement tick was processed
        /// </summary>
        public double LastMoveTime;

        public bool DebugMove;

        public double NextMoveTime;
        public double NextCancelTime;

        public int FailedMovementCount;
        public const int FailedMovementThreshold = 2;

        /// <summary>
        /// Starts the process of monster turning towards target
        /// </summary>
        public void StartTurn()
        {
            //if (Timers.RunningTime < NextMoveTime)
            //return;
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - StartTurn, ranged={IsRanged}");

            if (MoveSpeed == 0.0f)
                GetMovementSpeed();

            //Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - starting turn");

            IsTurning = true;

            // send network actions
            var targetDist = GetDistanceToTarget();
            var turnTo = (IsRanged && targetDist <= MaxRange) || (CurrentAttack == CombatType.Magic && targetDist <= GetSpellMaxRange()) || AiImmobile;
            if (turnTo)
                TurnTo(AttackTarget);
            else
                MoveTo(AttackTarget, RunRate);

            // need turning listener?
            IsTurning = false;
            IsMoving = true;
            LastMoveTime = Timers.RunningTime;
            NextCancelTime = LastMoveTime + ThreadSafeRandom.Next(2, 4);
            moveBit = false;

            var mvp = GetMovementParameters();
            if (turnTo)
                PhysicsObj.TurnToObject(AttackTarget.PhysicsObj, mvp);
            else
                PhysicsObj.MoveToObject(AttackTarget.PhysicsObj, mvp);

            // prevent initial snap
            PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;
        }

        /// <summary>
        /// Called when the TurnTo process has completed
        /// </summary>
        public void OnTurnComplete()
        {
            var dir = Vector3.Normalize(AttackTarget.Location.ToGlobal() - Location.ToGlobal());
            Location.Rotate(dir);

            IsTurning = false;

            if (!IsRanged)
                StartMove();
        }

        /// <summary>
        /// Starts the process of monster moving towards target
        /// </summary>
        public void StartMove()
        {
            LastMoveTime = Timers.RunningTime;
            IsMoving = true;
        }

        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - OnMoveComplete({status})");

            if (IsWandering)
                EndWandering();

            if (status != WeenieError.None)
            {
                FailedMovementCount++;
                if (IsRouting)
                {
                    if (FailedMovementCount > FailedMovementThreshold)
                        EndRoute();
                    else
                        RetryRoute();
                }
                return;
            }
            else
                FailedMovementCount = 0;

            if (IsRouting)
            {
                ContinueRoute();
                return;
            }

            if (AiImmobile && CurrentAttack == CombatType.Melee)
            {
                var targetDist = GetDistanceToTarget();
                if (targetDist > MaxRange)
                    ResetAttack();
            }

            if (MonsterState == State.Return)
                Sleep();

            PhysicsObj.CachedVelocity = Vector3.Zero;
            IsMoving = false;
        }

        /// <summary>
        /// Estimates the time it will take the monster to turn and move towards target
        /// </summary>
        public float EstimateTargetTime()
        {
            return EstimateTurnTo() + EstimateMoveTo();
        }

        /// <summary>
        /// Estimates the time it will take the monster to turn towards target
        /// </summary>
        public float EstimateTurnTo()
        {
            return GetRotateDelay(AttackTarget);
        }

        /// <summary>
        /// Estimates the time it will take the monster to move towards target
        /// </summary>
        public float EstimateMoveTo()
        {
            return GetDistanceToTarget() / MoveSpeed;
        }

        /// <summary>
        /// Returns TRUE if monster is within target melee range
        /// </summary>
        public bool IsMeleeRange()
        {
            return GetDistanceToTarget() <= MaxMeleeRange;
        }

        /// <summary>
        /// Returns TRUE if monster in range for current attack type
        /// </summary>
        public bool IsAttackRange()
        {
            return GetDistanceToTarget() <= MaxRange;
        }

        /// <summary>
        /// Gets the distance to target, with radius excluded
        /// </summary>
        public float GetDistanceToTarget()
        {
            if (AttackTarget == null)
                return float.MaxValue;

            //var matchIndoors = Location.Indoors == AttackTarget.Location.Indoors;
            //var targetPos = matchIndoors ? AttackTarget.Location.ToGlobal() : AttackTarget.Location.Pos;
            //var sourcePos = matchIndoors ? Location.ToGlobal() : Location.Pos;

            //var dist = (targetPos - sourcePos).Length();
            //var radialDist = dist - (AttackTarget.PhysicsObj.GetRadius() + PhysicsObj.GetRadius());

            // always use spheres?
            var cylDist = (float)Physics.Common.Position.CylinderDistance(PhysicsObj.GetRadius(), PhysicsObj.GetHeight(), PhysicsObj.Position,
                AttackTarget.PhysicsObj.GetRadius(), AttackTarget.PhysicsObj.GetHeight(), AttackTarget.PhysicsObj.Position);

            if (DebugMove)
                Console.WriteLine($"{Name}.DistanceToTarget: {cylDist}");

            //return radialDist;
            return cylDist;
        }

        /// <summary>
        /// Returns the destination position the monster is attempting to move to
        /// to perform a melee attack
        /// </summary>
        public Vector3 GetDestination()
        {
            var dir = Vector3.Normalize(Location.ToGlobal() - AttackTarget.Location.ToGlobal());
            return AttackTarget.Location.Pos + dir * (AttackTarget.PhysicsObj.GetRadius() + PhysicsObj.GetRadius());
        }

        /// <summary>
        /// Primary movement handler, determines if target in range
        /// </summary>
        public void Movement()
        {
            //if (!IsRanged)
                UpdatePosition();

            if (MonsterState == State.Awake && GetDistanceToTarget() >= MaxChaseRange)
            {
                CancelMoveTo();
                FindNextTarget();
                return;
            }

            if (PhysicsObj.MovementManager.MoveToManager.FailProgressCount > 0 && Timers.RunningTime > NextCancelTime)
                CancelMoveTo();
        }

        public void UpdatePosition(bool netsend = true)
        {
            //stopwatch.Restart();
            PhysicsObj.update_object();
            //ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Monster_Navigation_UpdatePosition_PUO, stopwatch.Elapsed.TotalSeconds);
            UpdatePosition_SyncLocation();

            if (netsend)
                SendUpdatePosition();

            if (DebugMove)
                //Console.WriteLine($"{Name} ({Guid}) - UpdatePosition (velocity: {PhysicsObj.CachedVelocity.Length()})");
                Console.WriteLine($"{Name} ({Guid}) - UpdatePosition: {Location.ToLOCString()}");

            if (MonsterState == State.Return && PhysicsObj.MovementManager.MoveToManager.PendingActions.Count == 0)
                Sleep();

            if (MonsterState == State.Awake && IsMoving && PhysicsObj.MovementManager.MoveToManager.PendingActions.Count == 0)
                IsMoving = false;
        }

        /// <summary>
        /// Synchronizes the WorldObject Location with the Physics Location
        /// </summary>
        public void UpdatePosition_SyncLocation()
        {
            // was the position successfully moved to?
            // use the physics position as the source-of-truth?
            var newPos = PhysicsObj.Position;

            if (Location.LandblockId.Raw != newPos.ObjCellID)
            {
                var prevBlockCell = Location.LandblockId.Raw;
                var prevBlock = Location.LandblockId.Raw >> 16;
                var prevCell = Location.LandblockId.Raw & 0xFFFF;

                var newBlockCell = newPos.ObjCellID;
                var newBlock = newPos.ObjCellID >> 16;
                var newCell = newPos.ObjCellID & 0xFFFF;

                Location.LandblockId = new LandblockId(newPos.ObjCellID);

                if (prevBlock != newBlock)
                {
                    LandblockManager.RelocateObjectForPhysics(this, true);
                    //Console.WriteLine($"Relocating {Name} from {prevBlockCell:X8} to {newBlockCell:X8}");
                    //Console.WriteLine("Old position: " + Location.Pos);
                    //Console.WriteLine("New position: " + newPos.Frame.Origin);
                }
                //else
                    //Console.WriteLine("Moving " + Name + " to " + Location.LandblockId.Raw.ToString("X8"));
            }

            // skip ObjCellID check when updating from physics
            // TODO: update to newer version of ACE.Entity.Position
            Location.PositionX = newPos.Frame.Origin.X;
            Location.PositionY = newPos.Frame.Origin.Y;
            Location.PositionZ = newPos.Frame.Origin.Z;

            Location.Rotation = newPos.Frame.Orientation;

            if (DebugMove)
                DebugDistance();
        }

        public void DebugDistance()
        {
            if (AttackTarget == null) return;

            var dist = GetDistanceToTarget();
            var angle = GetAngle(AttackTarget);
            //Console.WriteLine("Dist: " + dist);
            //Console.WriteLine("Angle: " + angle);
        }

        public void GetMovementSpeed()
        {
            var moveSpeed = MotionTable.GetRunSpeed(MotionTableId);
            if (moveSpeed == 0) moveSpeed = 2.5f;
            var scale = ObjScale ?? 1.0f;

            RunRate = GetRunRate();

            MoveSpeed = moveSpeed * RunRate * scale;

            //Console.WriteLine(Name + " - Run: " + runSkill + " - RunRate: " + RunRate + " - Move: " + MoveSpeed + " - Scale: " + scale);
        }

        /// <summary>
        /// Returns the RunRate that is sent to the client as myRunRate
        /// </summary>
        public float GetRunRate()
        {
            var burden = 0.0f;

            // assuming burden only applies to players...
            if (this is Player player)
            {
                var strength = Strength.Current;

                var capacity = EncumbranceSystem.EncumbranceCapacity((int)strength, player.AugmentationIncreasedCarryingCapacity);
                burden = EncumbranceSystem.GetBurden(capacity, EncumbranceVal ?? 0);

                // TODO: find this exact formula in client
                // technically this would be based on when the player releases / presses the movement key after stamina > 0
                if (player.IsExhausted)
                    burden = 3.0f;
            }

            var runSkill = GetCreatureSkill(Skill.Run).Current;
            var runRate = MovementSystem.GetRunRate(burden, (int)runSkill, ACE.Common.ConfigManager.Config.Server.WorldRuleset == ACE.Common.Ruleset.CustomDM ? 1.5f : 1.0f);

            return (float)runRate;
        }

        /// <summary>
        /// Sets the corpse to the final position
        /// </summary>
        public void SetFinalPosition()
        {
            if (AttackTarget == null) return;

            var playerDir = AttackTarget.Location.GetCurrentDir();
            Location.Pos = AttackTarget.Location.Pos + playerDir * (AttackTarget.PhysicsObj.GetRadius() + PhysicsObj.GetRadius());
            SendUpdatePosition();
        }

        /// <summary>
        /// Returns TRUE if monster is facing towards the target
        /// </summary>
        public bool IsFacing(WorldObject target)
        {
            if (target?.Location == null) return false;

            var angle = GetAngle(target);
            var dist = Math.Max(0, GetDistanceToTarget());

            // rotation accuracy?
            var threshold = 5.0f;

            var minDist = 10.0f;

            if (dist < minDist)
                threshold += (minDist - dist) * 1.5f;

            if (DebugMove)
                Console.WriteLine($"{Name}.IsFacing({target.Name}): Angle={angle}, Dist={dist}, Threshold={threshold}, {angle < threshold}");

            return angle < threshold;
        }

        public MovementParameters GetMovementParameters()
        {
            var mvp = new MovementParameters();

            // set non-default params for monster movement
            mvp.Flags &= ~MovementParamFlags.CanWalk;

            var turnTo = IsRanged || (CurrentAttack == CombatType.Magic && GetDistanceToTarget() <= GetSpellMaxRange()) || AiImmobile;

            if (!turnTo)
                mvp.Flags |= MovementParamFlags.FailWalk | MovementParamFlags.UseFinalHeading | MovementParamFlags.Sticky | MovementParamFlags.MoveAway;

            return mvp;
        }

        /// <summary>
        /// The maximum distance a monster can travel outside of its home position
        /// </summary>
        public double? HomeRadius
        {
            get => GetProperty(PropertyFloat.HomeRadius);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.HomeRadius); else SetProperty(PropertyFloat.HomeRadius, value.Value); }
        }

        public static float DefaultHomeRadius = 192.0f;
        public static float DefaultHomeRadiusSq = DefaultHomeRadius * DefaultHomeRadius;

        private float? homeRadiusSq;

        public float HomeRadiusSq
        {
            get
            {
                if (homeRadiusSq == null)
                {
                    var homeRadius = HomeRadius ?? DefaultHomeRadius;
                    homeRadiusSq = (float)(homeRadius * homeRadius);
                }
                return homeRadiusSq.Value;
            }
        }

        public void CheckMissHome()
        {
            if (MonsterState == State.Return)
                return;

            var homePosition = GetPosition(PositionType.Home);
            //var matchIndoors = Location.Indoors == homePosition.Indoors;

            //var globalPos = matchIndoors ? Location.ToGlobal() : Location.Pos;
            //var globalHomePos = matchIndoors ? homePosition.ToGlobal() : homePosition.Pos;
            var globalPos = Location.ToGlobal();
            var globalHomePos = homePosition.ToGlobal();

            var homeDistSq = Vector3.DistanceSquared(globalHomePos, globalPos);

            if (homeDistSq > HomeRadiusSq)
                MoveToHome();
        }

        public void MoveToHome()
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.MoveToHome()");

            var prevAttackTarget = AttackTarget;

            MonsterState = State.Return;
            AttackTarget = null;

            var home = GetPosition(PositionType.Home);

            if (Location.Equals(home))
            {
                Sleep();
                return;
            }

            NextCancelTime = Timers.RunningTime + 5.0f;

            MoveTo(home, RunRate, false, 1.0f);

            var homePos = new Physics.Common.Position(home);

            var mvp = GetMovementParameters();
            mvp.DistanceToObject = 0.6f;
            mvp.DesiredHeading = homePos.Frame.get_heading();

            PhysicsObj.MoveToPosition(homePos, mvp);
            IsMoving = true;

            EmoteManager.OnHomeSick(prevAttackTarget);
        }

        public void UpdateMoveSpeed()
        {
            var previousMoveSpeed = MoveSpeed;
            GetMovementSpeed();

            if (IsMoving && previousMoveSpeed != MoveSpeed)
            {
                PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
                PhysicsObj.MovementManager.MoveToManager.FailProgressCount = 0;

                if (AttackTarget != null)
                    MoveTo(AttackTarget, RunRate);
                else
                    MoveToHome();
            }
        }

        public void CancelMoveTo()
        {
            //Console.WriteLine($"{Name}.CancelMoveTo()");

            PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
            PhysicsObj.MovementManager.MoveToManager.FailProgressCount = 0;

            if (MonsterState == State.Return)
                ForceHome();

            EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready));

            IsMoving = false;
            NextMoveTime = Timers.RunningTime + 1.0f;

            ResetAttack();

            FindNextTarget();
        }

        public void ForceHome()
        {
            var homePos = GetPosition(PositionType.Home);

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - ForceHome({homePos.ToLOCString()})");

            var setPos = new SetPosition();
            setPos.Pos = new Physics.Common.Position(homePos);
            setPos.Flags = SetPositionFlags.Teleport;

            PhysicsObj.SetPosition(setPos);

            UpdatePosition_SyncLocation();

            SendUpdatePosition(true);

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(0.5f);
            actionChain.AddAction(this, Sleep);
            actionChain.EnqueueChain();
        }

        public void FindNewHome(float directionMinAngle, float directionMaxAngle, float distance)
        {
            var offsetPosition = new Position(Location);
            offsetPosition.Rotation = new Quaternion(0, 0, offsetPosition.RotationZ, offsetPosition.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, (float)ThreadSafeRandom.Next(directionMinAngle.ToRadians(), directionMaxAngle.ToRadians()));
            offsetPosition = offsetPosition.InFrontOf(distance);
            offsetPosition.SetLandblock();

            Home = offsetPosition;
        }

        public bool HasPendingActions
        {
            get => SwitchWeaponsPending || IsWanderingPending || IsEmotePending || IsRoutePending;
        }

        public bool IsPerformingAction
        {
            get => IsWandering || IsEmoting || IsRouting;
        }

        private Position WanderTarget = null;
        private double LastWanderTime = 0;
        private bool IsWanderingPending = false;
        private bool IsWandering = false;
        private const double MaxWanderFrequency = 10;
        private const double WanderChance = 0.5;

        public void TryWandering(float directionMinAngle, float directionMaxAngle, float distance)
        {
            var offsetPosition = new Position(Location);
            offsetPosition.Rotation = new Quaternion(0, 0, offsetPosition.RotationZ, offsetPosition.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, (float)ThreadSafeRandom.Next(directionMinAngle.ToRadians(), directionMaxAngle.ToRadians()));
            offsetPosition = offsetPosition.InFrontOf(distance);
            offsetPosition.SetLandblock();

            WanderTarget = offsetPosition;
            IsWanderingPending = true;
            LastAttemptWasNullRoute = false;

            if (LastEmoteTime + MaxEmoteFrequency < Time.GetUnixTime() && EmoteChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                TryEmoting();
        }

        private void Wander()
        {
            if (AttackTarget == null || WanderTarget == null)
            {
                EndWandering();
                return;
            }

            if (IsWandering)
                return;

            if (!MoveReady())
                return;

            if (LastWanderTime + MaxWanderFrequency < Time.GetUnixTime() && WanderChance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                // Cancel any remaining moves that might be pending.
                PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(WeenieError.ActionCancelled);
                PhysicsObj.MovementManager.MoveToManager.FailProgressCount = 0;

                LastWanderTime = Time.GetUnixTime();
                IsWanderingPending = false;
                IsWandering = true;

                MoveTo(WanderTarget, RunRate, true, 1.0f, null, false);
            }
            else
                EndWandering();
        }

        private void EndWandering()
        {
            IsWanderingPending = false;
            IsWandering = false;
            WanderTarget = null;
            FailedMovementCount = 0;

            TryRoute();
        }

        private MotionCommand DesiredEmote = MotionCommand.Invalid;
        private double LastEmoteTime = 0;
        private bool IsEmotePending = false;
        private bool IsEmoting = false;
        private const double MaxEmoteFrequency = 30;
        private const double EmoteChance = 0.5;

        public void TryEmoting(MotionCommand motion = MotionCommand.Invalid)
        {
            if (motion == MotionCommand.Invalid)
            {
                if (IdleMotionsList == null)
                    BuildIdleMotionsList();

                if (IdleMotionsList.Count() > 0)
                    DesiredEmote = IdleMotionsList.ElementAt(ThreadSafeRandom.Next(0, IdleMotionsList.Count() - 1));
                else
                    return;
            }
            else
                DesiredEmote = motion;
            IsEmotePending = true;
        }

        private void Emote()
        {
            if (AttackTarget == null || DesiredEmote == MotionCommand.Invalid)
            {
                EndEmoting();
                return;
            }

            if (IsEmoting)
                return;

            if (!MoveReady())
                return;

            LastEmoteTime = Time.GetUnixTime();
            IsEmotePending = false;
            IsEmoting = true;

            var combatStance = GetCombatStance();
            var actionChain = new ActionChain();
            EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, DesiredEmote), null, true);
            EnqueueBroadcastMotion(new Motion(combatStance, MotionCommand.Ready), null, true);

            EndEmoting();
        }

        private void EndEmoting()
        {
            IsEmotePending = false;
            IsEmoting = false;
            DesiredEmote = MotionCommand.Invalid;
        }

        private bool PathfindingEnabled = false;
        private double LastRouteTime = 0;
        private WorldObject RouteAttackTarget = null;
        private Position RoutePositionTarget;
        private List<Position> CurrentRoute;
        private int CurrentRouteIndex;
        private bool IsRoutePending = false;
        private bool IsRouting = false;
        private bool LastAttemptWasNullRoute = false;

        public void TryRoute(List<Position> route = null)
        {
            if (!PathfindingEnabled || !Location.Indoors || AttackTarget == null)
                return;

            if (route == null)
                route = Pathfinder.FindRoute(Location, AttackTarget.Location);

            if (route == null || route.Count == 0)
                LastAttemptWasNullRoute = true;
            else
            {
                RouteAttackTarget = AttackTarget;
                RoutePositionTarget = null;
                CurrentRoute = route;
                CurrentRouteIndex = 0;
                LastAttemptWasNullRoute = false;

                IsRoutePending = true;
            }
        }

        private void StartRoute()
        {
            LastRouteTime = Time.GetUnixTime();

            if (!MoveReady())
                return;

            FailedMovementCount = 0;
            IsRoutePending = false;
            IsRouting = true;

            ContinueRoute();
        }

        private void ContinueRoute(bool retry = false)
        {
            if (AttackTarget == null || AttackTarget != RouteAttackTarget || CurrentRoute == null || (CurrentRoute != null && CurrentRouteIndex >= CurrentRoute.Count))
            {
                EndRoute();
                return;
            }

            if (!retry)
            {
                RoutePositionTarget = CurrentRoute[CurrentRouteIndex];
                CurrentRouteIndex++;
            }

            if (Location.DistanceTo(RoutePositionTarget) < 1.0f)
            {
                ContinueRoute();
                return;
            }

            MoveTo(RoutePositionTarget, RunRate, true, 1.0f, null, false);
        }

        private void RetryRoute()
        {
            ContinueRoute(true);
        }

        private void EndRoute()
        {
            IsRoutePending = false;
            IsRouting = false;
            RoutePositionTarget = null;
            RouteAttackTarget = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;
            LastAttemptWasNullRoute = false;
            FailedMovementCount = 0;
        }
    }
}
