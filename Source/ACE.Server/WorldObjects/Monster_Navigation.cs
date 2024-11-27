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
        public const int FailedMovementThreshold = 3;
        public const int FailedRoutingThreshold = 3;

        public int FailedSightCount;
        public const int FailedSightThreshold = 10;

        /// <summary>
        /// Starts the process of monster turning and moving towards target
        /// </summary>
        public void StartMovement()
        {
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - StartTurn, ranged={IsRanged}");

            if (MoveSpeed == 0.0f)
                UpdateMovementSpeed();

            // send network actions
            var targetDist = GetDistanceToTarget();
            var turnTo = (IsRanged && targetDist <= MaxRange) || (CurrentAttackType == CombatType.Magic && targetDist <= GetSpellMaxRange()) || AiImmobile;
            if (turnTo)
                TurnTo(AttackTarget);
            else
                MoveTo(AttackTarget);

            moveBit = false;
        }

        protected void OnMovementStarted()
        {
            IsMoving = true;

            LastMoveTime = Timers.RunningTime;
            //NextCancelTime = LastMoveTime + ThreadSafeRandom.Next(2, 4);
        }

        protected void OnMovementStopped()
        {
            IsMoving = false;

            PhysicsObj.CachedVelocity = Vector3.Zero;
            NextMoveTime = Timers.RunningTime + ThreadSafeRandom.Next(0, 0.5f);
        }

        public override void HandleMotionDone(uint motionID, bool success)
        {
            MotionCommand motion = (MotionCommand)motionID;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - HandleMotionDone({motion} {success})");

            if (motion == MotionCommand.Invalid)
                return;

            if (motion == DesiredEmote)
                PendingEndEmoting = true;
            else if (motion == CurrentAttackMotionCommand)
                PendingEndAttack = true;
        }

        /// <summary>
        /// Called when the MoveTo process has completed
        /// </summary>
        public override void OnMoveComplete(WeenieError status)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - OnMoveComplete({status})");

            //Console.WriteLine($"Pathfinding: OnMoveComplete - {status}");

            OnMovementStopped();

            if (IsWandering)
                PendingEndWandering = true;

            if(IsMovingToHome)
                PendingEndMoveToHome = true;

            if (status != WeenieError.None)
            {
                if (status != WeenieError.ObjectGone)
                {
                    FailedMovementCount++;

                    if (IsRouting)
                    {
                        if (FailedMovementCount >= FailedRoutingThreshold)
                            PendingEndRoute = true;
                        else
                            PendingRetryRoute = true;
                    }
                }
                return;
            }

            if(status == WeenieError.None || status == WeenieError.ObjectGone)
                FailedMovementCount = 0;

            if (IsRouting)
            {
                PendingContinueRoute = true;
                return;
            }
        }

        /// <summary>
        /// Returns TRUE if monster in range for current attack type
        /// </summary>
        public bool IsAttackRange()
        {
            var distanceToTarget = GetDistanceToTarget();

            if (distanceToTarget <= MaxRange || PhysicsObj.IsSticky)
                return true;
            return false;
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

        public void UpdatePosition(bool netSend = true, bool adminMove = false)
        {
            //stopwatch.Restart();
            //ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Monster_Navigation_UpdatePosition_PUO, stopwatch.Elapsed.TotalSeconds);
            UpdatePosition_SyncLocation();

            if (netSend)
                SendUpdatePosition(adminMove);

            if (DebugMove)
                //Console.WriteLine($"{Name} ({Guid}) - UpdatePosition (velocity: {PhysicsObj.CachedVelocity.Length()})");
                Console.WriteLine($"{Name} ({Guid}) - UpdatePosition: {Location.ToLOCString()}");

            //if (MonsterState == State.Awake && IsMoving && !HasPendingMovement && !PhysicsObj.IsSticky)
            //    OnMovementStopped();
        }

        /// <summary>
        /// Synchronizes the WorldObject Location with the Physics Location
        /// </summary>
        private void UpdatePosition_SyncLocation()
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

        private void DebugDistance()
        {
            if (AttackTarget == null) return;

            var dist = GetDistanceToTarget();
            var angle = GetAngle(AttackTarget);
            //Console.WriteLine("Dist: " + dist);
            //Console.WriteLine("Angle: " + angle);
        }

        public void UpdateMovementSpeed()
        {
            var moveSpeed = MotionTable.GetRunSpeed(MotionTableId);
            if (moveSpeed == 0)
                moveSpeed = 2.5f;
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
                TryMoveToHome();
        }

        private bool IsMoveToHomePending = false;
        private bool IsMovingToHome = false;
        private bool PendingEndMoveToHome = false;

        public void TryMoveToHome()
        {
            //Console.WriteLine("Pathfinding: TryMoveToHome");

            if (IsMovingToHome)
                return;

            IsEmotePending = false;
            IsWanderingPending = false;
            IsRouteStartPending = false;

            if (IsEmoting)
                EndEmoting();

            if (IsWandering)
                EndWandering();

            if (IsRouting)
                EndRoute();

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsMoveToHomePending = true;
        }

        public void MoveToHome()
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.MoveToHome()");

            if (IsMovingToHome)
                return;

            if (!MoveReady())
                return;

            //Console.WriteLine("Pathfinding: MoveToHome");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsMoveToHomePending = false;
            IsMovingToHome = true;

            var prevAttackTarget = AttackTarget;

            MonsterState = State.Return;
            AttackTarget = null;

            var home = GetPosition(PositionType.Home);

            if (Location.Equals(home))
            {
                EndMoveToHome();
                return;
            }

            //NextCancelTime = Timers.RunningTime + 5.0f;

            MoveTo(home);

            EmoteManager.OnHomeSick(prevAttackTarget);
        }

        private void EndMoveToHome(bool forced = true)
        {
            //Console.WriteLine("Pathfinding: EndMoveToHome");

            if (!forced && !MoveReady())
                return;

            PendingEndMoveToHome = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsMoveToHomePending = false;
            IsMovingToHome = false;

            if (IsAwake)
                Sleep();
        }

        public void UpdateMoveSpeed()
        {
            var previousMoveSpeed = MoveSpeed;
            UpdateMovementSpeed();

            if (IsMoving && previousMoveSpeed != MoveSpeed)
                CancelMoveTo(WeenieError.ObjectGone);
        }

        public void CancelMoveTo(WeenieError error = WeenieError.ActionCancelled)
        {
            //Console.WriteLine($"{Name}.CancelMoveTo()");

            PhysicsObj.MovementManager.MoveToManager.CancelMoveTo(error);

            EnqueueBroadcastMotion(new Motion(CurrentMotionState.Stance, MotionCommand.Ready), null, true);

            OnMovementStopped();
        }

        public void ForceHome()
        {
            var homePos = GetPosition(PositionType.Home);

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}) - ForceHome({homePos.ToLOCString()})");

            //Console.WriteLine("Pathfinding: ForceHome");

            var setPos = new SetPosition();
            setPos.Pos = new Physics.Common.Position(homePos);
            setPos.Flags = SetPositionFlags.Teleport;

            PhysicsObj.SetPosition(setPos);

            UpdatePosition(true, true);
        }

        public void FindNewHome(float directionMinAngle, float directionMaxAngle, float distance)
        {
            var offsetPosition = new Position(Location);
            offsetPosition.Rotation = new Quaternion(0, 0, offsetPosition.RotationZ, offsetPosition.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, (float)ThreadSafeRandom.Next(directionMinAngle.ToRadians(), directionMaxAngle.ToRadians()));
            offsetPosition = offsetPosition.InFrontOf(distance);
            offsetPosition.SetLandblock();

            Home = offsetPosition;
        }

        private Position WanderTarget = null;
        private double LastWanderTime = 0;
        private bool IsWanderingPending = false;
        private bool IsWandering = false;
        private const double MaxWanderFrequency = 5;
        private const double WanderChance = 0.5;
        private bool PendingEndWandering = false;

        public void TryWandering(float directionMinAngle, float directionMaxAngle, float distance)
        {
            //Console.WriteLine("Pathfinding: TryWandering");

            if (IsWandering)
                return;

            var offsetPosition = new Position(Location);
            offsetPosition.Rotation = new Quaternion(0, 0, offsetPosition.RotationZ, offsetPosition.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, (float)ThreadSafeRandom.Next(directionMinAngle.ToRadians(), directionMaxAngle.ToRadians()));
            offsetPosition = offsetPosition.InFrontOf(distance);
            offsetPosition.SetLandblock();

            WanderTarget = offsetPosition;
            IsWanderingPending = true;
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

            //Console.WriteLine("Pathfinding: Wander");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            FailedMovementCount = 0;
            FailedSightCount = 0;

            LastWanderTime = Time.GetUnixTime();
            IsWanderingPending = false;
            IsWandering = true;

            MoveTo(WanderTarget, 1.0f, false);
        }

        private void EndWandering(bool forced = true)
        {
            //Console.WriteLine("Pathfinding: EndWandering");

            if (!forced && !MoveReady())
                return;

            PendingEndWandering = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsWanderingPending = false;
            IsWandering = false;
            WanderTarget = null;
        }

        private MotionCommand DesiredEmote = MotionCommand.Invalid;
        private double LastEmoteTime = 0;
        private bool IsEmotePending = false;
        private bool IsEmoting = false;
        private bool PendingEndEmoting = false;
        private const double MaxEmoteFrequency = 30;
        private const double EmoteChance = 0.5;

        public void TryEmoting(MotionCommand motion = MotionCommand.Invalid)
        {
            //Console.WriteLine("Pathfinding: TryEmoting");

            if (IsEmoting)
                return;

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

            //Console.WriteLine("Pathfinding: Emote");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            LastEmoteTime = Time.GetUnixTime();
            IsEmotePending = false;
            IsEmoting = true;

            var combatStance = GetCombatStance();
            var actionChain = new ActionChain();
            EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, DesiredEmote), null, true);
            EnqueueBroadcastMotion(new Motion(combatStance, MotionCommand.Ready), null, true);
        }

        private void EndEmoting(bool forced = true)
        {
            //Console.WriteLine("Pathfinding: EndEmoting");

            if (!forced && !MoveReady())
                return;

            PendingEndEmoting = false;

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
        private bool IsRouting = false;
        private bool LastRouteStartAttemptWasNullRoute = false;
        private bool IsRouteStartPending = false;
        private bool PendingEndRoute = false;
        private bool PendingRetryRoute = false;
        private bool PendingContinueRoute = false;

        public void TryRoute(List<Position> route = null)
        {
            if (!PathfindingEnabled || !Location.Indoors || AttackTarget == null || IsRouting)
                return;

            //Console.WriteLine("Pathfinding: TryRoute");

            IsRouteStartPending = true;

            RouteAttackTarget = AttackTarget;
            RoutePositionTarget = null;
            CurrentRoute = route;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
        }

        private void StartRoute()
        {
            if (IsRouting)
                return;

            LastRouteTime = Time.GetUnixTime();

            if (!MoveReady())
                return;

            //Console.WriteLine("Pathfinding: StartRoute");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            FailedMovementCount = 0;
            FailedSightCount = 0;
            IsRouteStartPending = false;
            IsRouting = true;

            if (CurrentRoute == null && (Location.Cell & 0xFFFF0000) == (AttackTarget.Location.Cell & 0xFFFF0000)) // The pathfinder currently only supports pathing between locations in the same landblock.
                CurrentRoute = Pathfinder.FindRoute(Location, AttackTarget.Location);

            if (CurrentRoute == null || CurrentRoute.Count == 0)
            {
                LastRouteStartAttemptWasNullRoute = true;
                EndRoute();
            }
            else
            {
                RouteAttackTarget = AttackTarget;
                RoutePositionTarget = null;
                CurrentRouteIndex = 0;
                LastRouteStartAttemptWasNullRoute = false;
            }

            ContinueRoute();
        }

        private void ContinueRoute(bool retry = false)
        {
            //Console.WriteLine("Pathfinding: ContinueRoute");

            PendingContinueRoute = false;

            if (AttackTarget == null || AttackTarget != RouteAttackTarget || CurrentRoute == null)
            {
                EndRoute();
                return;
            }

            if (!retry)
            {
                if (CurrentRoute != null && CurrentRouteIndex >= CurrentRoute.Count)
                {
                    EndRoute();
                    return;
                }

                if (GetDistanceToTarget() >= MaxChaseRange)
                {
                    EndRoute();
                    FindNextTarget();
                    return;
                }

                if (CurrentRouteIndex == 0 && CurrentRoute.Count > 1 && Location.DistanceTo(CurrentRoute[0]) < 1.0f)
                    CurrentRouteIndex++;

                RoutePositionTarget = CurrentRoute[CurrentRouteIndex];
                CurrentRouteIndex++;
            }

            if (Location.DistanceTo(RoutePositionTarget) < 1.0f)
            {
                ContinueRoute();
                return;
            }

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            MoveTo(RoutePositionTarget, 1.0f, false);
        }

        private void RetryRoute()
        {
            //Console.WriteLine("Pathfinding: RetryRoute");

            PendingRetryRoute = false;
            ContinueRoute(true);
        }

        private void EndRoute(bool forced = true)
        {
            //Console.WriteLine("Pathfinding: EndRoute");

            if (!forced && !MoveReady())
                return;

            PendingEndRoute = false;
            PendingContinueRoute = false;
            PendingRetryRoute = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsRouteStartPending = false;
            IsRouting = false;
            RoutePositionTarget = null;
            RouteAttackTarget = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
        }

        public bool HasPendingMovement
        {
            get => PhysicsObj.MovementManager?.MoveToManager?.PendingActions?.Count > 0;
        }

        public bool HasPendingAnimations
        {
            get => PhysicsObj.PartArray?.MotionTableManager?.PendingAnimations?.Count > 0;
        }

        
    }
}
