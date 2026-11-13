using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
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
            if (!MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).StartMovement({IsRanged})");

            if (MoveSpeed == 0.0f)
                UpdateMovementSpeed();

            // send network actions
            var targetDist = GetDistanceToTarget();
            var turnTo = (IsRanged && targetDist <= MaxRange) || (CurrentAttackType == CombatType.Magic && targetDist <= GetSpellMaxRange()) || AiImmobile;
            if (turnTo)
                TurnTo(AttackTarget, interruptAnimations: Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM);
            else
                MoveTo(AttackTarget, MaxMeleeRange - 0.05f, interruptAnimations: Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM);

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

                var wasSteppingOut = IsRouteSteppingOut;
                IsRouteSteppingOut = false;

                switch (status)
                {
                    case WeenieError.ObjectGone:
                        FailedMovementCount = 0;
                        return;
                    case WeenieError.None:
                        if (!wasSteppingOut)
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

            StopCurrentActivity(false);

            IsMoveToHomePending = true;
        }

        public void MoveToHome()
        {
            if (!MoveReady(true))
                return;

            // Planning the route home takes route planning time like any other plan (see CanPlanRouteThisTick): without
            // any left this tick the move home stays pending, and the monster tick tries it again on the next one.
            if (UsesPathfinding && !CanPlanRouteThisTick)
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).MoveToHome()");

            if (IsMovingToHome)
                return;

            CancelPendingMovement();

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
            CancelPendingMovement();

            // cancelling the previous move reports it completed, which must not end the move home
            PendingEndMoveToHome = false;

            MoveTo(GetPosition(PositionType.Home), 0.6f, useSpheres: false, interruptAnimations: Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM);
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
                UpdateRoute();

                // the route is over: done, or given up after failing to move
                if (!IsRouting && IsMovingToHome)
                    MoveToHomeDirectly();

                return;
            }

            if (PendingEndMoveToHome)
                EndMoveToHome();
        }

        /// <summary>
        /// Runs the next step of the current route, from the monster tick: a route chasing the attack target, a route
        /// home, or a test route (see TryStartTestRoute). It ends, retries or continues as the last move left it, or is
        /// planned again if the target moved away (see ShouldReplanRoute).
        /// </summary>
        private void UpdateRoute()
        {
            // A move cancelled from outside (e.g. UpdateMoveSpeed when a spell changes the move speed, or the chase range
            // check) completes with ObjectGone, which leaves no step pending: head for the waypoint it was interrupted on
            // again. ContinueRoute ends the route if its target is gone or changed.
            if (!PendingEndRoute && !PendingRetryRoute && !PendingContinueRoute && !HasPendingMovement && MoveReady(true))
            {
                if (DebugMove)
                    Console.WriteLine($"{Name} ({Guid}).UpdateRoute() - resuming the interrupted route");

                RewindToRouteTarget();
                PendingContinueRoute = true;
            }

            if (PendingEndRoute)
                EndRoute(false);
            else if (PendingRetryRoute)
                RetryRoute();
            else if (PendingContinueRoute)
                ContinueRoute();
            else if (ShouldReplanRoute())
                ReplanRoute(); // the target moved away from where the route leads
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

            CancelPendingMovement();

            PendingEndMoveToHome = false;
            FailedMovementCount = 0;
            FailedSightCount = 0;

            MonsterState = State.Awake;
        }

        private void EndMoveToHome(bool forced = true)
        {
            if (!forced && !MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndMoveToHome()");

            PendingEndMoveToHome = false;

            if (IsRouting && RouteDestination != null)
                EndRoute();

            CancelPendingMovement();

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

        /// <summary>
        /// Stops the movement this creature is on its way through, if any. Unlike CancelMoveTo, it does nothing (and
        /// broadcasts no Ready motion) when no movement is pending.
        /// </summary>
        private void CancelPendingMovement()
        {
            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);
        }

        /// <summary>
        /// Drops what this creature is doing or about to do (emoting, wandering, routing, optionally attacking) and
        /// stops its movement, for a change that takes over its behavior, such as a stun or heading home.
        /// </summary>
        private void StopCurrentActivity(bool includingAttack)
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

            if (includingAttack && IsAttacking)
                EndAttack();

            CancelPendingMovement();
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

            Home = PlaceOnFloor(offsetPosition);
        }

        /// <summary>
        /// Puts a point found by stepping away from the creature on the flat (it keeps the creature's height) onto the
        /// floor there: on the creature's navmesh when there is one within reach, otherwise on the landscape when
        /// outdoors. MoveTo checks arrival in 3D, so a point left above or below the floor (on a slope) is never
        /// reached.
        /// </summary>
        private Position PlaceOnFloor(Position position)
        {
            if (UsesPathfinding)
            {
                var onMesh = Pathfinder.GetClosestPointOnMesh(position, GetPathfindingAgentSize());
                if (onMesh != null)
                {
                    onMesh.Rotation = position.Rotation;
                    return onMesh;
                }
            }

            if (!position.Indoors)
                position.AdjustMapCoords();

            return position;
        }

        /// <summary>
        /// How far the navmesh floor found for a spawn home may be from it horizontally. The mesh keeps an agent radius
        /// from walls, so a creature placed against one finds the floor that much further out; past it, the point is
        /// not the floor under the home.
        /// </summary>
        private const float SpawnHomeFloorMargin = 0.25f;

        /// <summary>
        /// Indoors, a scatter spawn keeps the generator's height (see PhysicsObj.SetScatterPositionInternal), so on a
        /// ramp or stairs the home can be above or below the floor, and MoveTo, which checks arrival in 3D, never gets
        /// there. The home takes the floor height of the dungeon's navmesh under it, which is loaded by now when cached
        /// (see BeforeEnterWorld). Outdoors, spawns are already placed on the landscape. Set from OnGeneration: scatter
        /// spawns always come from a generator.
        /// </summary>
        protected Position AdjustHomePosition()
        {
            var home = new Position(Location);

            if (!UsesPathfinding || !home.Indoors)
                return home;

            var agentSize = GetPathfindingAgentSize();
            var floor = Pathfinder.GetClosestPointOnMesh(home, agentSize);
            if (floor == null)
                return home;

            var horizontal = new Vector2(floor.PositionX - home.PositionX, floor.PositionY - home.PositionY).Length();
            if (horizontal <= Pathfinder.GetAgentRadius(agentSize) + SpawnHomeFloorMargin)
                home.PositionZ = floor.PositionZ;

            return home;
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

            WanderTarget = PlaceOnFloor(offsetPosition);
            IsWanderingPending = true;
        }

        private void Wander()
        {
            if (!MoveReady(true))
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

            CancelPendingMovement();
            FailedMovementCount = 0;
            FailedSightCount = 0;

            LastWanderTime = Time.GetUnixTime();
            IsWanderingPending = false;
            IsWandering = true;

            MoveTo(WanderTarget, 2.0f, 1.0f, false, useSpheres: false, interruptAnimations: true);
        }

        private void EndWandering(bool forced = true)
        {
            if (!forced && !MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndWandering()");

            PendingEndWandering = false;

            CancelPendingMovement();

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

            CancelPendingMovement();

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
            if (!forced && !MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndEmoting()");

            PendingEndEmoting = false;

            IsEmotePending = false;
            IsEmoting = false;
            DesiredEmote = MotionCommand.Invalid;
            ExpectedEmoteEndTime = 0;
        }

        private static bool UsesPathfinding => Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && Pathfinder.PathfindingEnabled;

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
        /// True while a retry steps the creature away from the wall it got stuck on (see ContinueRoute).
        /// </summary>
        private bool IsRouteSteppingOut = false;

        /// <summary>
        /// Where the attack target was when the current route was planned.
        /// </summary>
        private Position RoutePlannedTargetLocation;

        /// <summary>
        /// A route is re-planned once the target has moved this far from where it was planned to...
        /// </summary>
        private const float RouteReplanTargetMovedDistance = 10.0f;

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
        /// True while heading from the start of a ledge drop to its landing (see RoutePosition.IsLedgeDropStart).
        /// </summary>
        private bool IsLedgeDropping = false;

        /// <summary>
        /// How many times in a row the current route's ledge drop has failed (see ContinueRoute). Kept apart from
        /// FailedMovementCount, which the successful move back to the ledge drop's start resets each time.
        /// </summary>
        private int FailedLedgeDropCount = 0;

        /// <summary>
        /// True if EdgeSlide was cleared for the current ledge drop, to be set again once it is over.
        /// </summary>
        private bool LedgeDropRestoreEdgeSlide = false;

        /// <summary>
        /// True if this creature routes with the pathfinder where it stands: indoors and on the landscape alike.
        /// </summary>
        public bool CanRoute => UsesPathfinding && Location != null;

        /// <summary>
        /// Routes to the attack target if it can't be reached in a straight line: a melee attacker that can't touch it,
        /// or a missile attacker that has no line to it or is out of range.
        /// </summary>
        private void TryRouteToTargetOutOfReach()
        {
            if (!CanRoute || AttackTarget == null)
                return;

            if ((!IsRanged && !IsMeleeVisible(AttackTarget, true)) || (IsRanged && (!IsDirectVisible(AttackTarget, true) || GetDistanceToTarget() > GetMaxMissileRange())))
                TryRoute();
        }

        public void TryRoute()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TryRoute()");

            if (!CanRoute || AttackTarget == null || IsRouting)
                return;

            IsRouteStartPending = true;

            RouteAttackTarget = AttackTarget;
            RouteDestination = null;
            RoutePositionTarget = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
        }

        /// <summary>
        /// Starts a route to a fixed position, e.g. home, that this creature follows like a route to an attack target,
        /// but without chasing or re-planning for a moving target. Returns false if no route was found.
        /// </summary>
        private bool TryStartRouteTo(Position destination)
        {
            if (!CanRoute || destination == null || IsRouting)
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
        /// True while this creature follows a test route (see TryStartTestRoute). Cleared when the route ends.
        /// </summary>
        public bool IsTestRouting { get; private set; }

        /// <summary>
        /// True while the current route is walked rather than run (a test route, see TryStartTestRoute). Cleared when
        /// the route ends.
        /// </summary>
        private bool IsRouteWalking = false;

        /// <summary>
        /// Sends this creature along a route to a fixed position, to test the pathfinder (see @pathtome), running, or
        /// walking with walk. Whatever it was doing gives way, including its attack target and a move home, and it
        /// wakes if it was asleep. Until the route ends it only follows the route: it doesn't look for targets or head
        /// home. Afterwards it carries on as an awake creature without a target, which heads home. Returns false if it
        /// couldn't start: pathfinding off, no route, or busy (e.g. mid-animation).
        /// </summary>
        public bool TryStartTestRoute(Position destination, bool walk = false)
        {
            if (!CanRoute || destination == null)
                return false;

            IsEmotePending = false;
            IsWanderingPending = false;
            IsRouteStartPending = false;

            if (IsEmoting)
                EndEmoting();

            if (IsWandering)
                EndWandering();

            // without heading home, which EndGrantPassage does for a creature woken just to grant passage
            AwakeJustToGrantPassage = false;
            if (IsGrantingPassage || IsGrantPassagePending)
                EndGrantPassage();

            if (IsRouting)
                EndRoute();

            // without going to sleep, which EndMoveToHome would do
            IsMoveToHomePending = false;
            IsMovingToHome = false;
            PendingEndMoveToHome = false;

            AttackTarget = null;

            if (!IsAwake)
            {
                MonsterState = State.Awake;
                IsAwake = true;
            }

            IsTestRouting = TryStartRouteTo(destination);
            IsRouteWalking = IsTestRouting && walk;
            return IsTestRouting;
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
        private List<Position> FindRouteThroughDoorways(Position goal, AgentSize agentSize, out bool reachesTarget)
        {
            return Pathfinder.FindRouteLegs(Location, goal, agentSize, out reachesTarget)?.SelectMany(leg => leg).ToList();
        }

        /// <summary>
        /// The mesh this creature routes on, by its size: the smallest mesh it fits, as a smaller mesh's routes lead
        /// through gaps and under openings it can't pass. Sized by the collision spheres, which are what collide with
        /// walls and ceilings.
        /// </summary>
        public AgentSize GetPathfindingAgentSize()
        {
            if (PhysicsObj == null)
                return AgentSize.Small;

            return Pathfinder.GetAgentSize(PhysicsObj.GetWallCollisionRadius(), PhysicsObj.GetWallCollisionHeight());
        }

        /// <summary>
        /// Plans a route to the route's goal (see GetRouteGoal) on this creature's mesh (see GetPathfindingAgentSize).
        /// </summary>
        private List<Position> PlanRoute(out bool reachesTarget)
        {
            reachesTarget = false;

            var goal = GetRouteGoal();
            if (goal == null)
                return null;

            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                return FindRouteThroughDoorways(goal, GetPathfindingAgentSize(), out reachesTarget);
            }
            finally
            {
                var landblock = CurrentLandblock;
                if (landblock != null)
                    landblock.RoutePlanningTime += System.Diagnostics.Stopwatch.GetTimestamp() - started;
            }
        }

        /// <summary>
        /// The time a landblock's monster ticks may spend planning routes in one tick (see Landblock.RoutePlanningTime).
        /// A route can take tens of ms, so a pack woken at once plans a few routes per tick instead of all of them.
        /// </summary>
        private static readonly long MaxRoutePlanningTimePerTick = System.Diagnostics.Stopwatch.Frequency / 50; // 20 ms

        /// <summary>
        /// True if this creature's landblock has route planning time left this tick. A plan that can wait (starting a
        /// route, re-planning) waits for the creature's next tick otherwise.
        /// </summary>
        private bool CanPlanRouteThisTick => CurrentLandblock == null || CurrentLandblock.RoutePlanningTime < MaxRoutePlanningTimePerTick;

        /// <summary>
        /// True if the attack target has moved far enough from where the current route was planned to that the route
        /// should be planned again.
        /// </summary>
        private bool ShouldReplanRoute()
        {
            // a fixed destination doesn't move
            if (RouteDestination != null || AttackTarget == null || AttackTarget != RouteAttackTarget || RoutePlannedTargetLocation == null)
                return false;

            if (Time.GetUnixTime() - LastRouteTime < RouteReplanMinInterval)
                return false;

            // finish the ledge drop first: a new route planned from the ledge may lead another way
            if (IsLedgeDropping)
                return false;

            if (!CanPlanRouteThisTick)
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

            AdoptRoute(route, reachesTarget);
        }

        /// <summary>
        /// Follows a newly planned route from its first waypoint, which this creature heads for on its next tick.
        /// </summary>
        private void AdoptRoute(List<Position> route, bool reachesTarget)
        {
            RouteReachesTarget = reachesTarget;
            CurrentRoute = route;
            CurrentRouteIndex = 0;
            PendingContinueRoute = true;
        }

        private void StartRoute()
        {
            if (!MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).StartRoute()");

            if (IsRouting)
                return;

            LastRouteTime = Time.GetUnixTime();

            IsRouteStartPending = false;
            IsRouting = true;

            var toDestination = RouteDestination != null;

            RoutePlannedTargetLocation = toDestination ? null : new Position(AttackTarget.Location);

            PartialRouteReplanCount = 0;
            var route = PlanRoute(out var reachesTarget);
            if (route == null || route.Count == 0)
            {
                EndRoute();

                // after EndRoute, which clears it; only about chasing the attack target
                if (!toDestination)
                    LastRouteStartAttemptWasNullRoute = true;
                return;
            }

            LastRouteStartAttemptWasNullRoute = false;

            CancelPendingMovement();
            FailedMovementCount = 0;
            FailedSightCount = 0;

            RouteAttackTarget = toDestination ? null : AttackTarget;
            RoutePositionTarget = null;
            AdoptRoute(route, reachesTarget);
        }

        private void ContinueRoute(bool retry = false)
        {
            if (!MoveReady(true))
                return;

            if (DebugMove && !retry)
                Console.WriteLine($"{Name} ({Guid}).ContinueRoute()");

            if (retry)
                PendingRetryRoute = false;
            else
                PendingContinueRoute = false;

            // landed, or the ledge drop failed
            var wasLedgeDropping = IsLedgeDropping;
            EndLedgeDrop();

            // A failed ledge drop (e.g. stuck on the lip) is tried again from its start, rather than heading for the waypoint
            // after the landing along the edge. Reaching the start again resets FailedMovementCount, so the ledge drop's own
            // failures are counted, and FailedRoutingThreshold of them in a row end the route.
            if (retry && wasLedgeDropping && CurrentRouteIndex >= 2 && RoutePosition.IsLedgeDrop(CurrentRoute?[CurrentRouteIndex - 2]))
            {
                if (++FailedLedgeDropCount >= FailedRoutingThreshold)
                {
                    if (DebugMove)
                        Console.WriteLine($"{Name} ({Guid}).ContinueRoute() - the ledge drop failed {FailedLedgeDropCount} times, giving up the route");

                    EndRoute();
                    return;
                }

                CurrentRouteIndex -= 2;
                retry = false;
            }
            else if (wasLedgeDropping && !retry)
                FailedLedgeDropCount = 0; // landed

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

                // At the start of a ledge drop, the next waypoint is where it lands: head straight for it, off the ledge.
                var atLedgeDropStart = CurrentRouteIndex > 0 && RoutePosition.IsLedgeDrop(CurrentRoute[CurrentRouteIndex - 1]);

                RoutePositionTarget = CurrentRoute[CurrentRouteIndex];
                CurrentRouteIndex++;

                if (atLedgeDropStart)
                    BeginLedgeDrop();
            }
            else
            {
                // attempt to step away from the nearest wall, hopefully that will get us past the point we got stuck.
                var agentSize = GetPathfindingAgentSize();
                var nearbyWallPos = Pathfinder.GetNearestWallPosition(Location, 1.0f, agentSize);
                if (nearbyWallPos != null)
                {
                    // A mesh edge can also be a ledge or one side of a narrow corridor, so stepping out from it can
                    // leave the mesh: bring the point back onto it.
                    var wallAvoidingPos = Pathfinder.GetClosestPointOnMesh(nearbyWallPos.InFrontOf(1.2f), agentSize);
                    if (wallAvoidingPos != null)
                    {
                        CancelPendingMovement();
                        FailedSightCount = 0;

                        // after the step, the route heads for the waypoint that failed again
                        RewindToRouteTarget();
                        IsRouteSteppingOut = true;

                        MoveTo(wallAvoidingPos, RouteWaypointRadius, 1.0f, false, useSpheres: false, walk: IsRouteWalking, interruptAnimations: true);
                        return;
                    }
                }
            }

            CancelPendingMovement();
            FailedSightCount = 0;

            // Measured from the centre: a waypoint counts as reached where it is, not a body radius early. A ledge drop
            // is walked off, so the creature comes down where LedgeDropFinder tested: running, it would land further out.
            MoveTo(RoutePositionTarget, RouteWaypointRadius, 1.0f, false, useSpheres: false, walk: IsRouteWalking || IsLedgeDropping, interruptAnimations: true);
        }

        /// <summary>
        /// At the end of a planned route that fell short of the target, plans again from here: the route may have
        /// ended at the edge of what was loaded (outdoor tiles load in the background). Returns false if there is
        /// nothing more to follow: no new route, or one that gets no further than here. Without route planning time
        /// left this tick (see CanPlanRouteThisTick) it returns true and tries again on the next tick, which doesn't
        /// count as an attempt.
        /// </summary>
        private bool TryContinuePartialRoute()
        {
            var toDestination = RouteDestination != null;

            if (RouteReachesTarget || (!toDestination && AttackTarget == null) || PartialRouteReplanCount >= MaxPartialRouteReplans)
                return false;

            if (!CanPlanRouteThisTick)
            {
                PendingContinueRoute = true; // the same step again on the creature's next tick
                return true;
            }

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

            AdoptRoute(route, reachesTarget);
            return true;
        }

        /// <summary>
        /// Lets this creature walk off the ledge at the start of a ledge drop: EdgeSlide would keep it on the edge.
        /// MoveTo waits while it falls and goes on when it lands. The move to the landing walks (see ContinueRoute).
        /// </summary>
        private void BeginLedgeDrop()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).BeginLedgeDrop()");

            IsLedgeDropping = true;

            if (PhysicsObj != null && PhysicsObj.State.HasFlag(PhysicsState.EdgeSlide))
            {
                PhysicsObj.State &= ~PhysicsState.EdgeSlide;
                LedgeDropRestoreEdgeSlide = true;
            }
        }

        private void EndLedgeDrop()
        {
            if (!IsLedgeDropping)
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndLedgeDrop()");

            if (LedgeDropRestoreEdgeSlide && PhysicsObj != null)
                PhysicsObj.State |= PhysicsState.EdgeSlide;

            IsLedgeDropping = false;
            LedgeDropRestoreEdgeSlide = false;
        }

        /// <summary>
        /// Points the route back at the waypoint it is heading for, so the next ContinueRoute heads for it again. Does
        /// nothing if it already does (e.g. after an earlier rewind).
        /// </summary>
        private void RewindToRouteTarget()
        {
            if (RoutePositionTarget != null && CurrentRouteIndex > 0 && CurrentRoute?[CurrentRouteIndex - 1] == RoutePositionTarget)
                CurrentRouteIndex--;
        }

        private void RetryRoute()
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).RetryRoute()");

            ContinueRoute(true);
        }

        private void EndRoute(bool forced = true)
        {
            if (!forced && !MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndRoute()");

            PendingEndRoute = false;
            PendingContinueRoute = false;
            PendingRetryRoute = false;
            IsRouteSteppingOut = false;

            CancelPendingMovement();

            EndLedgeDrop();

            IsRouteStartPending = false;
            IsRouting = false;
            IsTestRouting = false;
            IsRouteWalking = false;
            FailedLedgeDropCount = 0;
            RoutePositionTarget = null;
            RouteAttackTarget = null;
            RouteDestination = null;
            CurrentRoute = null;
            CurrentRouteIndex = 0;
            LastRouteStartAttemptWasNullRoute = false;
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
            if (!MoveReady(true))
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

            CancelPendingMovement();
            FailedMovementCount = 0;
            FailedSightCount = 0;

            LastGrantPassageTime = Time.GetUnixTime();
            IsGrantPassagePending = false;
            IsGrantingPassage = true;

            MoveTo(GrantPassageTarget, 2.0f, 1.0f, false, useSpheres: false, interruptAnimations: true);
        }

        private void EndGrantPassage(bool forced = true)
        {
            if (!forced && !MoveReady(true))
                return;

            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).EndGrantPassage()");

            PendingEndGrantPassage = false;

            CancelPendingMovement();

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
