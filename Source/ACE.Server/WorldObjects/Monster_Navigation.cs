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
        /// Flag indicates monster is moving and/or turning towards target
        /// </summary>
        public bool IsMoving = false;

        /// <summary>
        /// Flag indicates monster is turning *in place* towards target
        /// </summary>
        public bool IsTurning = false;

        /// <summary>
        /// The last time a movement tick was processed
        /// </summary>
        public double LastMoveTime;

        public bool DebugMove;

        public double NextMoveTime;

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
                Console.WriteLine($"{Name} ({Guid}).StartMovement({IsRanged})");

            if (MoveSpeed == 0.0f)
                UpdateMovementSpeed();

            // send network actions
            var targetDist = GetDistanceToTarget();
            var turnTo = (IsRanged && targetDist <= MaxRange) || (CurrentAttackType == CombatType.Magic && targetDist <= GetSpellMaxRange()) || AiImmobile;
            if (turnTo)
                TurnTo(AttackTarget);
            else
                MoveTo(AttackTarget, MaxMeleeRange - 0.05f);

            //moveBit = false;
        }

        protected void OnMovementStarted(bool isTurn = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).OnMovementStarted()");

            IsMoving = true;
            IsTurning = isTurn;

            LastMoveTime = Timers.RunningTime;
        }

        protected void OnMovementStopped()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).OnMovementStopped()");

            IsMoving = false;
            IsTurning = false;

            MonsterMovementLock.EnterWriteLock();
            try
            {
                LastMoveTo = null;
            }
            finally
            {
                MonsterMovementLock.ExitWriteLock();
            }

            //PhysicsObj.CachedVelocity = Vector3.Zero;
            NextMoveTime = Timers.RunningTime + ThreadSafeRandom.Next(0, 0.5f);
        }

        public override void OnMotionDone(uint motionID, bool success)
        {
            MotionCommand motion = (MotionCommand)motionID;

            //if (DebugMove)
            //    Console.WriteLine($"{Name} ({Guid}).OnMotionDone({motion} {success})");

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
            //if (DebugMove)
            //    Console.WriteLine($"{Name} ({Guid}).OnMoveComplete({status})");

            OnMovementStopped();

            // a route home goes on to its next waypoint (see UpdateMoveToHome)
            if (IsMovingToHome && !IsRouting)
                PendingEndMoveToHome = true;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (IsGrantingPassage)
                    PendingEndGrantPassage = true;

                if (IsWandering)
                    PendingEndWandering = true;

                switch (status)
                {
                    case WeenieError.ObjectGone:
                        FailedMovementCount = 0;
                        return;
                    case WeenieError.None:
                        FailedMovementCount = 0;

                        if (IsRouting)
                        {
                            PendingContinueRoute = true;
                            return;
                        }
                        return;
                    default:
                        FailedMovementCount++;

                        if (IsRouting)
                        {
                            if (FailedMovementCount >= FailedRoutingThreshold)
                                PendingEndRoute = true;
                            else
                                PendingRetryRoute = true;
                        }
                        return;
                }
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

            //if (DebugMove)
            //    Console.WriteLine($"{Name}.DistanceToTarget: {cylDist}");

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

            //if (DebugMove)
            //    //Console.WriteLine($"{Name} ({Guid}) - UpdatePosition (velocity: {PhysicsObj.CachedVelocity.Length()})");
            //    Console.WriteLine($"{Name} ({Guid}) - UpdatePosition: {Location.ToLOCString()}");

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

            //if (DebugMove)
            //    DebugDistance();
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

            //if (DebugMove)
            //    Console.WriteLine($"{Name}.IsFacing({target.Name}): Angle={angle}, Dist={dist}, Threshold={threshold}, {angle < threshold}");

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

            if (IsOutsideHomeRadius())
                TryMoveToHome();
        }

        /// <summary>
        /// True if this creature is further from home than its HomeRadius, so CheckMissHome sends it home.
        /// </summary>
        private bool IsOutsideHomeRadius()
        {
            var homePosition = GetPosition(PositionType.Home);
            //var matchIndoors = Location.Indoors == homePosition.Indoors;

            //var globalPos = matchIndoors ? Location.ToGlobal() : Location.Pos;
            //var globalHomePos = matchIndoors ? homePosition.ToGlobal() : homePosition.Pos;
            var globalPos = Location.ToGlobal();
            var globalHomePos = homePosition.ToGlobal();

            var homeDistSq = Vector3.DistanceSquared(globalHomePos, globalPos);

            return homeDistSq > HomeRadiusSq;
        }

        private bool IsMoveToHomePending = false;
        private bool IsMovingToHome = false;
        private bool PendingEndMoveToHome = false;

        /// <summary>
        /// When the current move home started (unix time).
        /// </summary>
        private double MoveToHomeStartTime = 0;

        /// <summary>
        /// With the CustomDM ruleset, a creature moving home that has been given an attack target (by being attacked on the
        /// way) turns to fight once it has been moving home this long, in seconds.
        /// </summary>
        private const double MinMoveToHomeTimeBeforeFighting = 10.0;

        public void TryMoveToHome()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryMoveToHome()");

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
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).MoveToHome()");

            if (IsMovingToHome)
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsMoveToHomePending = false;
            IsMovingToHome = true;
            MoveToHomeStartTime = Time.GetUnixTime();

            var prevAttackTarget = AttackTarget;

            MonsterState = State.Return;
            AttackTarget = null;

            var home = GetPosition(PositionType.Home);

            FailedMovementCount = 0;
            FailedSightCount = 0;

            // Follow a route home where there is one; the route steps only run with the CustomDM ruleset (see
            // OnMoveComplete). Without a route, head straight home.
            var routing = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && TryStartRouteTo(home);
            if (!routing)
                MoveToHomeDirectly();

            EmoteManager.OnHomeSick(prevAttackTarget);
        }

        /// <summary>
        /// Heads straight for home: the whole way when there is no route, or the last stretch after one (from where the
        /// route ended, which may be short of home if it got stuck). EndMoveToHome follows when this move completes.
        /// </summary>
        private void MoveToHomeDirectly()
        {
            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            // cancelling the previous move reports it completed, which must not end the move home
            PendingEndMoveToHome = false;

            MoveTo(GetPosition(PositionType.Home), 0.6f, useSpheres: false);
        }

        /// <summary>
        /// Runs the steps of moving home, from the monster tick: the route home while there is one, then the last stretch
        /// straight home, then the end of the move.
        /// </summary>
        private void UpdateMoveToHome()
        {
            if (ShouldStopMovingHomeToFight())
            {
                StopMovingHomeToFight();
                return;
            }

            if (IsRouting)
            {
                // A move cancelled from outside (e.g. UpdateMoveSpeed when a spell changes the move speed) completes
                // with ObjectGone, which leaves no step pending, and nothing else resumes a route home: head for the
                // waypoint it was interrupted on again.
                if (!PendingEndRoute && !PendingRetryRoute && !PendingContinueRoute && !HasPendingMovement && MoveReady())
                {
                    if (DebugMove)
                        Console.WriteLine($"{Name} ({Guid}).UpdateMoveToHome() - resuming the interrupted route home");

                    if (RoutePositionTarget != null && CurrentRouteIndex > 0)
                        CurrentRouteIndex--;
                    PendingContinueRoute = true;
                }

                if (PendingEndRoute)
                    EndRoute(false);
                else if (PendingRetryRoute)
                    RetryRoute();
                else if (PendingContinueRoute)
                    ContinueRoute();

                // the route is over: done, or given up after failing to move
                if (!IsRouting && IsMovingToHome)
                    MoveToHomeDirectly();

                return;
            }

            if (PendingEndMoveToHome)
                EndMoveToHome();
        }

        /// <summary>
        /// True if this creature should stop moving home to fight the attack target it was given on the way (being
        /// attacked by a player sets one): CustomDM only, after MinMoveToHomeTimeBeforeFighting, and only within its
        /// home radius, as outside it CheckMissHome would send it straight back home.
        /// </summary>
        private bool ShouldStopMovingHomeToFight()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return false;

            if (!IsMovingToHome || AttackTarget == null)
                return false;

            if (Time.GetUnixTime() - MoveToHomeStartTime < MinMoveToHomeTimeBeforeFighting)
                return false;

            return !IsOutsideHomeRadius();
        }

        /// <summary>
        /// True while this creature ignores being attacked: with the CustomDM ruleset, for its first
        /// MinMoveToHomeTimeBeforeFighting of moving home. Attacks then don't give it an attack target or wake it up
        /// (see Player.OnAttackMonster), so only attacks after that make it turn to fight.
        /// </summary>
        public bool IgnoresAttacksWhileMovingHome
        {
            get
            {
                if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                    return false;

                return IsMovingToHome && Time.GetUnixTime() - MoveToHomeStartTime < MinMoveToHomeTimeBeforeFighting;
            }
        }

        /// <summary>
        /// Stops moving home and stays awake, so the rest of the monster tick takes on the attack target.
        /// </summary>
        private void StopMovingHomeToFight()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).StopMovingHomeToFight({AttackTarget?.Name})");

            // no longer moving home first, so cancelling the move doesn't mark the move home as ended
            IsMovingToHome = false;
            IsMoveToHomePending = false;

            if (IsRouting && RouteDestination != null)
                EndRoute();

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            PendingEndMoveToHome = false;
            FailedMovementCount = 0;
            FailedSightCount = 0;

            MonsterState = State.Awake;
        }

        private void EndMoveToHome(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndMoveToHome()");

            PendingEndMoveToHome = false;

            if (IsRouting && RouteDestination != null)
                EndRoute();

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
        }

        public void ForceHome()
        {
            var homePos = GetPosition(PositionType.Home);

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).ForceHome({homePos.ToLOCString()})");

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
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryWandering()");

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
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).Wander()");

            if (AttackTarget == null || WanderTarget == null)
            {
                EndWandering();
                return;
            }

            if (IsWandering)
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);
            FailedMovementCount = 0;
            FailedSightCount = 0;

            LastWanderTime = Time.GetUnixTime();
            IsWanderingPending = false;
            IsWandering = true;

            MoveTo(WanderTarget, 2.0f, 1.0f, false, useSpheres: false);
        }

        private void EndWandering(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndWandering()");

            PendingEndWandering = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsWanderingPending = false;
            IsWandering = false;
            WanderTarget = null;
        }

        private MotionCommand DesiredEmote = MotionCommand.Invalid;
        private double LastEmoteTime = 0;
        private double ExpectedEmoteEndTime = 0;
        private bool IsEmotePending = false;
        private bool IsEmoting = false;
        private bool PendingEndEmoting = false;
        private const double MaxEmoteFrequency = 30;
        private const double EmoteChance = 0.5;

        public void TryEmoting(MotionCommand motion = MotionCommand.Invalid)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryEmoting()");

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
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).Emote()");

            if (AttackTarget == null || DesiredEmote == MotionCommand.Invalid)
            {
                EndEmoting();
                return;
            }

            if (IsEmoting)
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            LastEmoteTime = Time.GetUnixTime();
            IsEmotePending = false;
            IsEmoting = true;

            var combatStance = GetCombatStance();
            EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, DesiredEmote), null, true);
            EnqueueBroadcastMotion(new Motion(combatStance, MotionCommand.Ready), null, true);

            ExpectedEmoteEndTime = Time.GetFutureUnixTime(MotionTable.GetAnimationLength(MotionTableId, MotionStance.NonCombat, DesiredEmote));
        }

        private void EndEmoting(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndEmoting()");

            PendingEndEmoting = false;

            IsEmotePending = false;
            IsEmoting = false;
            DesiredEmote = MotionCommand.Invalid;
            ExpectedEmoteEndTime = 0;
        }

        private bool PathfindingEnabled = false;
        private double LastRouteTime = 0;
        private WorldObject RouteAttackTarget = null;

        /// <summary>
        /// The fixed position the current route leads to, e.g. home. Null when the route chases the attack target.
        /// </summary>
        private Position RouteDestination = null;

        private Position RoutePositionTarget;
        private List<Position> CurrentRoute;
        private int CurrentRouteIndex;
        private bool IsRouting = false;
        private bool LastRouteStartAttemptWasNullRoute = false;
        private bool IsRouteStartPending = false;
        private bool PendingEndRoute = false;
        private bool PendingRetryRoute = false;
        private bool PendingContinueRoute = false;

        /// <summary>
        /// True when CurrentRoute was planned to the attack target (not handed in by a debug command), so it may be re-planned.
        /// </summary>
        private bool RouteIsPlanned = false;

        /// <summary>
        /// Where the attack target was when the current route was planned.
        /// </summary>
        private Position RoutePlannedTargetLocation;

        /// <summary>
        /// A route is re-planned once the target has moved this far from where it was planned to...
        /// </summary>
        private const float RouteReplanTargetMovedDistance = 3.0f;

        /// <summary>
        /// ...but no more often than this, in seconds.
        /// </summary>
        private const double RouteReplanMinInterval = 5.0;

        /// <summary>
        /// The longest a planned route that falls short of the target (e.g. while outdoor tiles ahead are still
        /// loading) is planned again on reaching its end, before routing gives up.
        /// </summary>
        private const int MaxPartialRouteReplans = 5;

        /// <summary>
        /// How close (3D, from the creature's centre) a route waypoint counts as reached.
        /// </summary>
        private const float RouteWaypointRadius = 0.6f;

        /// <summary>
        /// True if the current planned route leads all the way to the attack target, false if it ends short of it.
        /// </summary>
        private bool RouteReachesTarget = false;

        private int PartialRouteReplanCount = 0;

        /// <summary>
        /// True while heading from the start of a drop down a ledge to its landing (see RoutePosition.IsDropStart).
        /// </summary>
        private bool IsRouteDropping = false;

        /// <summary>
        /// True if EdgeSlide was cleared for the current drop, to be set again once it is over.
        /// </summary>
        private bool RouteDropRestoreEdgeSlide = false;

        /// <summary>
        /// True if this creature routes with the pathfinder where it stands: indoors and on the landscape alike.
        /// </summary>
        public bool CanRouteHere => PathfindingEnabled && Location != null;

        public void TryRoute(List<Position> route = null)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryRoute()");

            // an explicitly provided route (debug commands) may also be on the surface
            if (!PathfindingEnabled || (route == null && !CanRouteHere) || AttackTarget == null || IsRouting)
                return;

            IsRouteStartPending = true;

            RouteAttackTarget = AttackTarget;
            RouteDestination = null;
            RoutePositionTarget = null;
            CurrentRoute = route;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
        }

        /// <summary>
        /// Starts a route to a fixed position, e.g. home, that this creature follows like a route to an attack target,
        /// but without chasing or re-planning for a moving target. Returns false if no route was found.
        /// </summary>
        private bool TryStartRouteTo(Position destination)
        {
            if (!CanRouteHere || destination == null || IsRouting)
                return false;

            RouteAttackTarget = null;
            RouteDestination = new Position(destination);
            RoutePositionTarget = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;

            StartRoute();

            return IsRouting;
        }

        /// <summary>
        /// The position the current route leads to: its fixed destination, or else the attack target.
        /// </summary>
        private Position GetRouteGoal()
        {
            return RouteDestination ?? AttackTarget?.Location;
        }

        /// <summary>
        /// A route to a position that may leave this building or cave and enter another one through their doorways,
        /// chaining one route per navmesh (see Pathfinder.FindRouteLegs).
        /// </summary>
        private List<Position> FindRouteThroughDoorways(Position goal, AgentWidth agentWidth, out bool reachesTarget)
        {
            return Pathfinder.FindRouteLegs(Location, goal, agentWidth, out reachesTarget)?.SelectMany(leg => leg).ToList();
        }

        /// <summary>
        /// The mesh this creature routes on, by its size: the smallest mesh it fits, as a smaller mesh's routes lead
        /// through gaps and under openings it can't pass. Sized by the collision spheres, which are what collide with
        /// walls and ceilings.
        /// </summary>
        public AgentWidth GetPathfindingAgentWidth()
        {
            if (PhysicsObj == null)
                return AgentWidth.Small;

            return Pathfinder.GetAgentWidth(PhysicsObj.GetWallCollisionRadius(), PhysicsObj.GetWallCollisionHeight());
        }

        /// <summary>
        /// Plans a route to the route's goal (see GetRouteGoal) on this creature's mesh (see GetPathfindingAgentWidth).
        /// </summary>
        private List<Position> PlanRoute(out bool reachesTarget)
        {
            reachesTarget = false;

            var goal = GetRouteGoal();
            if (goal == null)
                return null;

            return FindRouteThroughDoorways(goal, GetPathfindingAgentWidth(), out reachesTarget);
        }

        /// <summary>
        /// True if the attack target has moved far enough from where the current route was planned to that the route
        /// should be planned again.
        /// </summary>
        private bool ShouldReplanRoute()
        {
            // a fixed destination doesn't move
            if (RouteDestination != null || !RouteIsPlanned || AttackTarget == null || AttackTarget != RouteAttackTarget || RoutePlannedTargetLocation == null)
                return false;

            if (Time.GetUnixTime() - LastRouteTime < RouteReplanMinInterval)
                return false;

            // finish the drop first: a new route planned from the ledge may lead another way
            if (IsRouteDropping)
                return false;

            return AttackTarget.Location.DistanceTo(RoutePlannedTargetLocation) > RouteReplanTargetMovedDistance;
        }

        /// <summary>
        /// Plans the route again towards the attack target's current location. If no route is found the current one is
        /// kept, e.g. while the target stands somewhere off the mesh for a moment.
        /// </summary>
        private void ReplanRoute()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).ReplanRoute()");

            LastRouteTime = Time.GetUnixTime();
            RoutePlannedTargetLocation = new Position(AttackTarget.Location);

            var route = PlanRoute(out var reachesTarget);
            if (route == null || route.Count == 0)
                return;

            RouteReachesTarget = reachesTarget;
            CurrentRoute = route;
            CurrentRouteIndex = 0;
            PendingContinueRoute = true; // head for the new route's first waypoint on the next tick
        }

        private void StartRoute()
        {
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).StartRoute()");

            if (IsRouting)
                return;

            LastRouteTime = Time.GetUnixTime();

            IsRouteStartPending = false;
            IsRouting = true;

            var toDestination = RouteDestination != null;

            RouteIsPlanned = CurrentRoute == null;
            RoutePlannedTargetLocation = toDestination ? null : new Position(AttackTarget.Location);

            PartialRouteReplanCount = 0;
            RouteReachesTarget = !RouteIsPlanned; // a provided route is followed as is
            if (RouteIsPlanned)
            {
                CurrentRoute = PlanRoute(out var reachesTarget);
                RouteReachesTarget = reachesTarget;
            }

            if (CurrentRoute == null || CurrentRoute.Count == 0)
            {
                EndRoute();

                // after EndRoute, which clears it; only about chasing the attack target
                if (!toDestination)
                    LastRouteStartAttemptWasNullRoute = true;
                return;
            }
            else
                LastRouteStartAttemptWasNullRoute = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);
            FailedMovementCount = 0;
            FailedSightCount = 0;

            RouteAttackTarget = toDestination ? null : AttackTarget;
            RoutePositionTarget = null;
            CurrentRouteIndex = 0;

            PendingContinueRoute = true;
        }

        private void ContinueRoute(bool retry = false)
        {
            if (!MoveReady())
                return;

            if (DebugMove && !retry)
                Console.WriteLine($"{Name} ({Guid}).ContinueRoute()");

            if(retry)
                PendingRetryRoute = false;
            else
                PendingContinueRoute = false;

            // landed, or the drop failed
            var wasDropping = IsRouteDropping;
            EndRouteDrop();

            // A failed drop (e.g. stuck on the lip) is tried again from its start, rather than heading for the waypoint
            // after the landing along the edge. FailedRoutingThreshold still ends the route if it keeps failing.
            if (retry && wasDropping && CurrentRouteIndex >= 2 && RoutePosition.IsDrop(CurrentRoute?[CurrentRouteIndex - 2]))
            {
                CurrentRouteIndex -= 2;
                retry = false;
            }

            var toDestination = RouteDestination != null;

            // a route chasing the attack target ends when the target is gone or changed
            if (CurrentRoute == null || (!toDestination && (AttackTarget == null || AttackTarget != RouteAttackTarget)))
            {
                EndRoute();
                return;
            }

            if (!retry)
            {
                if (CurrentRoute != null && CurrentRouteIndex >= CurrentRoute.Count)
                {
                    if (!TryContinuePartialRoute())
                        EndRoute();
                    return;
                }

                if (!toDestination && GetDistanceToTarget() >= MaxChaseRange)
                {
                    EndRoute();
                    FindNextTarget();
                    return;
                }

                // At the start of a drop, the next waypoint is where it lands: head straight for it, off the ledge.
                var atDropStart = CurrentRouteIndex > 0 && RoutePosition.IsDrop(CurrentRoute[CurrentRouteIndex - 1]);

                if (!atDropStart)
                {
                    for (var skipIndex = CurrentRouteIndex; skipIndex < CurrentRoute.Count; skipIndex++)
                    {
                        // Skip insignificant distance steps, but not the start of a drop: it must be reached, as the
                        // drop was only checked from there.
                        if (Location.DistanceTo(CurrentRoute[skipIndex]) > 2.0f || RoutePosition.IsDrop(CurrentRoute[skipIndex]))
                        {
                            CurrentRouteIndex = skipIndex;
                            break;
                        }
                    }
                }

                RoutePositionTarget = CurrentRoute[CurrentRouteIndex];
                CurrentRouteIndex++;

                if (atDropStart)
                    BeginRouteDrop();
            }
            else
            {
                int retryIndex;
                Position retryPos = RoutePositionTarget;
                for(retryIndex = Math.Max(CurrentRouteIndex - 1, 0); retryIndex > 0; retryIndex--)
                {
                    // Rewind our route to the previous significant distance step.
                    retryPos = CurrentRoute[retryIndex];
                    if (Location.DistanceTo(retryPos) > 2.0f)
                        continue;
                    else
                        break;
                }

                // on this creature's own mesh, whose edges are where its body touches the walls
                var agentWidth = GetPathfindingAgentWidth();
                var nearbyWallPos = Pathfinder.GetNearestWallPosition(retryPos, 1.0f, agentWidth, out _, false);
                if (nearbyWallPos != null)
                {
                    // A mesh edge can also be a drop or one side of a narrow corridor, so stepping out from it can
                    // leave the mesh: bring the point back onto it.
                    var wallAvoidingPos = Pathfinder.GetClosestPointOnMesh(nearbyWallPos.InFrontOf(1.2f), agentWidth);
                    if (wallAvoidingPos != null)
                    {
                        if (HasPendingMovement)
                            CancelMoveTo(WeenieError.ObjectGone);
                        FailedSightCount = 0;

                        // measured from the centre: from the edge of the body, the creature already counts as there
                        // and doesn't move at all
                        MoveTo(wallAvoidingPos, RouteWaypointRadius, 1.0f, false, useSpheres: false);
                        return;
                    }
                }
            }

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);
            FailedSightCount = 0;

            // measured from the centre: a waypoint counts as reached where it is, not a body radius early
            MoveTo(RoutePositionTarget, RouteWaypointRadius, 1.0f, false, useSpheres: false);
        }

        /// <summary>
        /// At the end of a planned route that fell short of the target, plans again from here: the route may have
        /// ended at the edge of what was loaded (outdoor tiles load in the background). Returns false if there is
        /// nothing more to follow: no new route, or one that gets no further than here.
        /// </summary>
        private bool TryContinuePartialRoute()
        {
            var toDestination = RouteDestination != null;

            if (!RouteIsPlanned || RouteReachesTarget || (!toDestination && AttackTarget == null) || PartialRouteReplanCount >= MaxPartialRouteReplans)
                return false;

            PartialRouteReplanCount++;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryContinuePartialRoute() - attempt {PartialRouteReplanCount}");

            LastRouteTime = Time.GetUnixTime();
            if (!toDestination)
                RoutePlannedTargetLocation = new Position(AttackTarget.Location);

            var route = PlanRoute(out var reachesTarget);
            if (route == null || route.Count == 0)
                return false;

            // a route that ends where we already are leads nowhere
            if (!reachesTarget && Location.DistanceTo(route[route.Count - 1]) <= 2.0f)
                return false;

            RouteReachesTarget = reachesTarget;
            CurrentRoute = route;
            CurrentRouteIndex = 0;
            PendingContinueRoute = true; // head for the new route's first waypoint on the next tick
            return true;
        }

        /// <summary>
        /// Lets this creature walk off the ledge at the start of a drop: EdgeSlide would keep it on the edge.
        /// MoveTo waits while it falls and goes on when it lands.
        /// </summary>
        private void BeginRouteDrop()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).BeginRouteDrop()");

            IsRouteDropping = true;

            if (PhysicsObj != null && PhysicsObj.State.HasFlag(PhysicsState.EdgeSlide))
            {
                PhysicsObj.State &= ~PhysicsState.EdgeSlide;
                RouteDropRestoreEdgeSlide = true;
            }
        }

        private void EndRouteDrop()
        {
            if (!IsRouteDropping)
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndRouteDrop()");

            if (RouteDropRestoreEdgeSlide && PhysicsObj != null)
                PhysicsObj.State |= PhysicsState.EdgeSlide;

            IsRouteDropping = false;
            RouteDropRestoreEdgeSlide = false;
        }

        private void RetryRoute()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).RetryRoute()");

            ContinueRoute(true);
        }

        private void EndRoute(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndRoute()");

            PendingEndRoute = false;
            PendingContinueRoute = false;
            PendingRetryRoute = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            EndRouteDrop();

            IsRouteStartPending = false;
            IsRouting = false;
            RoutePositionTarget = null;
            RouteAttackTarget = null;
            RouteDestination = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
            RouteIsPlanned = false;
            RoutePlannedTargetLocation = null;
            RouteReachesTarget = false;
            PartialRouteReplanCount = 0;
        }

        private double LastRequestPassageTime = 0;
        private const double MaxRequestPassageFrequency = 5;

        public void TryRequestPassage(Creature target)
        {
            if (LastRequestPassageTime + MaxRequestPassageFrequency > Time.GetUnixTime())
                return;

            if (target == null || target.IsMoving || target.IsRouting)
                return;

            var angle = GetAngle(target);
            if (Math.Abs(angle) > 90)
                return;

            if (target.IsAwake && !target.IsRanged)
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryRequestPassage({target.Name} ({target.Guid}))");

            LastRequestPassageTime = Time.GetUnixTime();
            target.TryGrantPassage(this);
        }

        private Position GrantPassageTarget = null;
        private double LastGrantPassageTime = 0;
        private bool IsGrantPassagePending = false;
        private bool IsGrantingPassage = false;
        private const double MaxGrantPassageFrequency = 5;
        private bool PendingEndGrantPassage = false;
        private bool AwakeJustToGrantPassage = false;

        public void TryGrantPassage(Creature requester)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryGrantPassage()");

            if (IsGrantingPassage || requester == null)
                return;

            if (LastGrantPassageTime + MaxGrantPassageFrequency > Time.GetUnixTime())
                return;

            if (AttackTarget != null)
            {
                if (GetDistanceToTarget() < 2.0f)
                    return;
            }

            var angle = 0f;
            if (ThreadSafeRandom.Next(0.0f, 1.0f) > 0.5f)
                angle += 45;
            else
                angle -= 45;

            var offsetPosition = new Position(Location);
            offsetPosition.Rotation = new Quaternion(0, 0, offsetPosition.RotationZ, offsetPosition.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, angle.ToRadians());
            offsetPosition = offsetPosition.InFrontOf(5);
            offsetPosition.SetLandblock();

            LastGrantPassageTime = Time.GetUnixTime();
            GrantPassageTarget = offsetPosition;
            IsGrantPassagePending = true;

            if (!IsAwake)
            {
                AwakeJustToGrantPassage = true;

                MonsterState = State.Awake;
                IsAwake = true;
            }
        }

        private void GrantPassage()
        {
            if (!MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).GrantPassage()");

            if (GrantPassageTarget == null)
            {
                EndGrantPassage();
                return;
            }

            if (IsGrantingPassage)
                return;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);
            FailedMovementCount = 0;
            FailedSightCount = 0;

            LastGrantPassageTime = Time.GetUnixTime();
            IsGrantPassagePending = false;
            IsGrantingPassage = true;

            MoveTo(GrantPassageTarget, 2.0f, 1.0f, false, useSpheres: false);
        }

        private void EndGrantPassage(bool forced = true)
        {
            if (!forced && !MoveReady())
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndGrantPassage()");

            PendingEndGrantPassage = false;

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsGrantPassagePending = false;
            IsGrantingPassage = false;
            GrantPassageTarget = null;

            if (AwakeJustToGrantPassage)
                TryMoveToHome();
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
