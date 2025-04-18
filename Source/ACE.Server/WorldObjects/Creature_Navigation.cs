using System;
using System.Numerics;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Extensions;
using ACE.Server.Network.GameMessages.Messages;
using System.Threading;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Creature navigation / position / rotation
    /// </summary>
    partial class Creature
    {
        /// <summary>
        /// Returns the 3D distance between this creature and target
        /// </summary>
        public float GetDistance(WorldObject target)
        {
            return Location.DistanceTo(target.Location);
        }

        /// <summary>
        /// Returns the 2D angle between current direction
        /// and position from an input target
        /// </summary>
        public float GetAngle(WorldObject target)
        {
            var currentDir = Location.GetCurrentDir();

            var targetDir = Vector3.Zero;
            if (Location.Indoors == target.Location.Indoors)
                targetDir = GetDirection(Location.ToGlobal(), target.Location.ToGlobal());
            else
                targetDir = GetDirection(Location.Pos, target.Location.Pos);

            targetDir.Z = 0.0f;
            targetDir = Vector3.Normalize(targetDir);
            
            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        public float GetAngle_Physics(WorldObject target)
        {
            var currentDir = GetCurrentDir_Physics();

            var targetDir = Vector3.Zero;
            if (Location.Indoors == target.Location.Indoors)
                targetDir = GetDirection(Location.ToGlobal(), target.Location.ToGlobal());
            else
                targetDir = GetDirection(Location.Pos, target.Location.Pos);

            targetDir.Z = 0.0f;
            targetDir = Vector3.Normalize(targetDir);

            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        public Vector3 GetCurrentDir_Physics()
        {
            return Vector3.Normalize(Vector3.Transform(Vector3.UnitY, PhysicsObj.Position.Frame.Orientation));
        }

        public float GetAngle_Physics2(WorldObject target)
        {
            return PhysicsObj.Position.heading_diff(target.PhysicsObj.Position);
        }

        /// <summary>
        /// Returns the 2D angle between current direction
        /// and rotation from an input position
        /// </summary>
        public float GetAngle(Position position)
        {
            var currentDir = Location.GetCurrentDir();
            var targetDir = position.GetCurrentDir();

            // get the 2D angle between these vectors
            return GetAngle(currentDir, targetDir);
        }

        /// <summary>
        /// Returns the 2D angle of the input vector
        /// </summary>
        public static float GetAngle(Vector3 dir)
        {
            var rads = Math.Atan2(dir.Y, dir.X);
            if (double.IsNaN(rads)) return 0.0f;

            var angle = rads * 57.2958f;
            return (float)angle;
        }

        /// <summary>
        /// Returns the 2D angle between 2 vectors
        /// </summary>
        public static float GetAngle(Vector3 a, Vector3 b)
        {
            var cosTheta = a.Dot2D(b);
            var rads = Math.Acos(cosTheta);
            if (double.IsNaN(rads)) return 0.0f;

            var angle = rads * (180.0f / Math.PI);
            return (float)angle;
        }

        /// <summary>
        /// Returns a normalized 2D vector from self to target
        /// </summary>
        public Vector3 GetDirection(Vector3 self, Vector3 target)
        {
            var target2D = new Vector3(self.X, self.Y, 0);
            var self2D = new Vector3(target.X, target.Y, 0);

            return Vector3.Normalize(target - self);
        }

        /// <summary>
        /// Sends a TurnToObject command to the client
        /// </summary>
        public void TurnToObject(WorldObject target, bool stopCompletely = true)
        {
            var turnToMotion = new Motion(this, target, MovementType.TurnToObject);

            if (!stopCompletely)
                turnToMotion.MoveToParameters.MovementParameters &= ~MovementParams.StopCompletely;

            EnqueueBroadcastMotion(turnToMotion);
        }

        /// <summary>
        /// Starts rotating a creature from its current direction
        /// so that it eventually is facing the target position
        /// </summary>
        /// <returns>The amount of time in seconds for the rotation to complete</returns>
        public virtual float Rotate(WorldObject target)
        {
            if (target == null || target.Location == null)
                return 0.0f;

            // send network message to start turning creature
            TurnToObject(target);

            var angle = GetAngle(target);
            //Console.WriteLine("Angle: " + angle);

            // estimate time to rotate to target
            var rotateDelay = GetRotateDelay(angle);
            //Console.WriteLine("RotateTime: " + rotateTime);

            // update server object rotation on completion
            // TODO: proper incremental rotation
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(rotateDelay);
            actionChain.AddAction(this, () =>
            {
                if (target == null || target.Location == null)
                    return;

                //var matchIndoors = Location.Indoors == target.Location.Indoors;

                //var globalLoc = matchIndoors ? Location.ToGlobal() : Location.Pos;
                //var targetLoc = matchIndoors ? target.Location.ToGlobal() : target.Location.Pos;
                var globalLoc = Location.ToGlobal();
                var targetLoc = target.Location.ToGlobal();

                var targetDir = GetDirection(globalLoc, targetLoc);

                Location.Rotate(targetDir);
            });
            actionChain.EnqueueChain();

            return rotateDelay;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate by the # of degrees
        /// from the input angle, using the omega speed from its MotionTable
        /// </summary>
        public virtual float GetRotateDelay(float angle)
        {
            var turnSpeed = MotionTable.GetTurnSpeed(MotionTableId);
            if (turnSpeed == 0.0f) return 0.0f;

            var rotateTime = Math.PI / turnSpeed / 180.0f * Math.Abs(angle);
            return (float)rotateTime;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate
        /// towards its target, based on the omega speed from its MotionTable
        /// </summary>
        public float GetRotateDelay(WorldObject target)
        {
            var angle = GetAngle(target);
            return GetRotateDelay(angle);
        }

        /// <summary>
        /// Starts rotating a creature from its current direction
        /// so that it eventually is facing the rotation from the input position
        /// Used by the emote system, which has the target rotation stored in positions
        /// </summary>
        /// <returns>The amount of time in seconds for the rotation to complete</returns>
        public float TurnTo(Position position, float speed = 1.0f, bool clientOnly = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TurnTo({position.ToLOCString()}, {speed}, {clientOnly})");

            // send network message to start turning creature
            var turnToMotion = GetTurnToMotion(position, speed);
            EnqueueBroadcastMotion(turnToMotion);

            var angle = GetAngle(position);
            //Console.WriteLine("Angle: " + angle);

            // estimate time to rotate to target
            var rotateDelay = GetRotateDelay(angle);
            //Console.WriteLine("RotateTime: " + rotateTime);

            if (!clientOnly)
            {
                OnMovementStarted(true);
                // update server object rotation on completion
                // TODO: proper incremental rotation
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(rotateDelay);
                actionChain.AddAction(this, () =>
                {
                    var targetDir = position.GetCurrentDir();
                    Location.Rotate(targetDir);
                    PhysicsObj.Position.Frame.Orientation = Location.Rotation;

                    OnMovementStopped();
                    if (!IsAwake)
                        UpdatePosition();
                });
                actionChain.EnqueueChain();
            }

            return rotateDelay;
        }

        /// <summary>
        /// Returns the amount of time for this creature to rotate
        /// towards the rotation from the input position, based on the omega speed from its MotionTable
        /// Used by the emote system, which has the target rotation stored in positions
        /// </summary>
        /// <param name="position">Only the rotation information from this position is used here</param>
        public float GetRotateDelay(Position position)
        {
            var angle = GetAngle(position);
            return GetRotateDelay(angle);
        }

        /// <summary>
        /// This is called by the monster AI system for ranged attacks
        /// It sets CurrentMotionState here
        /// </summary>
        public void TurnTo(WorldObject target, float speed = 1.0f, bool clientOnly = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).TurnTo({target.Name}, {speed}, {clientOnly})");

            if (this is Player)
                return;

            var motion = GetTurnToMotion(target, speed);

            EnqueueBroadcastMotion(motion);

            if (clientOnly)
                return;

            CurrentMotionState = motion;

            OnMovementStarted(true);

            // prevent initial snap forward
            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;

            var mvp = new MovementParameters(motion);
            PhysicsObj.TurnToObject(target.PhysicsObj, mvp);
        }

        /// <summary>
        /// Used by the monster AI system to start turning / running towards a target
        /// </summary>
        public virtual void MoveTo(WorldObject target, float distanceToObject = 2.0f, float speed = 1.0f, bool clientOnly = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).MoveTo({target.Name}, {distanceToObject}, {speed}, {clientOnly})");

            var motion = GetMoveToMotion(target, distanceToObject);

            EnqueueBroadcastMotion(motion);

            if (clientOnly)
                return;

            MonsterMovementLock.EnterWriteLock();
            try
            {
                LastMoveTo = motion;
            }
            finally
            {
                MonsterMovementLock.ExitWriteLock();
            }
            CurrentMotionState = motion;

            OnMovementStarted();

            // prevent initial snap forward
            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;

            var mvp = new MovementParameters(motion);
            PhysicsObj.MoveToObject(target.PhysicsObj, mvp);
        }

        /// <summary>
        /// Used by the monster AI system to start turning / running towards a position
        /// </summary>
        public void MoveTo(Position position, float distanceToObject = 2.0f, float speed = 1.0f, bool useFinalHeading = true, bool clientOnly = false)
        {
            if (DebugMove)
                Console.WriteLine($"{Name} ({Guid}).MoveTo({position.ToLOCString()}, {distanceToObject}, {speed}, {clientOnly})");

            if (!position.Indoors)
                position.AdjustMapCoords();

            var motion = GetMoveToMotion(position, distanceToObject, speed,  useFinalHeading);

            EnqueueBroadcastMotion(motion);

            if (clientOnly)
                return;

            MonsterMovementLock.EnterWriteLock();
            try
            {
                LastMoveTo = motion;
            }
            finally
            {
                MonsterMovementLock.ExitWriteLock();
            }
            CurrentMotionState = motion;

            // start executing MoveTo iterator on server
            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = Physics.Common.PhysicsTimer.CurrentTime;

            var mvp = new MovementParameters(motion);
            PhysicsObj.MoveToPosition(new Physics.Common.Position(position), mvp);

            OnMovementStarted();
        }

        private Motion GetMoveToMotion(WorldObject target, float distanceToObject = 0.6f, float speed = 1.0f)
        {
            var motion = new Motion(this, target, MovementType.MoveToObject);

            motion.MoveToParameters.MovementParameters =
                //MovementParams.CanWalk |
                MovementParams.CanRun |
                //MovementParams.CanCharge |
                //MovementParams.CanSideStep |
                MovementParams.CanWalkBackwards |
                MovementParams.FailWalk |
                MovementParams.MoveAway |
                MovementParams.MoveTowards |
                MovementParams.UseSpheres |
                MovementParams.SetHoldKey |
                MovementParams.ModifyRawState |
                MovementParams.ModifyInterpretedState |
                MovementParams.CancelMoveTo |
                MovementParams.StopCompletely |
                MovementParams.Sticky |
                MovementParams.UseFinalHeading;

            motion.MotionFlags = MotionFlags.StickToObject;

            if (IsSnared)
            {
                motion.MoveToParameters.MovementParameters ^= MovementParams.CanWalk;
                motion.MoveToParameters.MovementParameters &= ~(MovementParams.CanRun | MovementParams.Sticky);

                motion.MotionFlags &= ~MotionFlags.StickToObject;
            }

            motion.MoveToParameters.DistanceToObject = distanceToObject;
            motion.MoveToParameters.Speed = speed;

            motion.RunRate = RunRate;

            return motion;
        }

        private Motion GetMoveToMotion(Position position, float distanceToObject = 0.6f, float speed = 1.0f, bool useFinalHeading = true)
        {
            var motion = new Motion(this, position);

            motion.MoveToParameters.MovementParameters =
                MovementParams.CanWalk |
                MovementParams.CanRun |
                //MovementParams.CanSideStep |
                //MovementParams.CanWalkBackwards |
                MovementParams.FailWalk |
                //MovementParams.MoveAway |
                MovementParams.MoveTowards |
                MovementParams.UseSpheres |
                MovementParams.SetHoldKey |
                MovementParams.ModifyRawState |
                MovementParams.ModifyInterpretedState |
                MovementParams.CancelMoveTo |
                MovementParams.StopCompletely;

            if (IsSnared)
            {
                motion.MoveToParameters.MovementParameters ^= MovementParams.CanWalk;
                motion.MoveToParameters.MovementParameters &= ~(MovementParams.CanRun | MovementParams.Sticky);

                motion.MotionFlags &= ~MotionFlags.StickToObject;
            }

            motion.MoveToParameters.DistanceToObject = distanceToObject;
            motion.MoveToParameters.Speed = speed;
            motion.RunRate = RunRate;

            if (useFinalHeading)
                motion.MoveToParameters.MovementParameters |= MovementParams.UseFinalHeading;

            return motion;
        }

        private Motion GetTurnToMotion(WorldObject target, float speed = 1.0f)
        {
            var motion = new Motion(this, target, MovementType.TurnToObject);

            motion.MoveToParameters.MovementParameters =
                MovementParams.UseSpheres |
                MovementParams.SetHoldKey |
                MovementParams.ModifyRawState |
                MovementParams.ModifyInterpretedState |
                MovementParams.CancelMoveTo |
                MovementParams.StopCompletely;

            motion.MoveToParameters.Speed = speed;

            return motion;
        }

        private Motion GetTurnToMotion(Position position, float speed = 1.0f)
        {
            var frame = new AFrame(position.Pos, position.Rotation);
            var heading = frame.get_heading();

            var motion = new Motion(this, position, heading);

            motion.MoveToParameters.MovementParameters =
                MovementParams.UseSpheres |
                MovementParams.SetHoldKey |
                MovementParams.ModifyRawState |
                MovementParams.ModifyInterpretedState |
                MovementParams.CancelMoveTo |
                MovementParams.StopCompletely;

            motion.MoveToParameters.Speed = speed;

            return motion;
        }

        public readonly ReaderWriterLockSlim MonsterMovementLock = new ReaderWriterLockSlim();
        public Motion LastMoveTo;
        public virtual void BroadcastMoveTo(Player player)
        {
            MonsterMovementLock.EnterReadLock();
            try
            {
                if (LastMoveTo != null)
                    player.Session.Network.EnqueueSend(new GameMessageUpdateMotion(this, LastMoveTo));
            }
            finally
            {
                MonsterMovementLock.ExitReadLock();
            }
        }

        /// <summary>
        /// For monsters only -- blips to a new position within the same landblock
        /// </summary>
        public void FakeTeleport(Position toPosition)
        {
            var newPosition = new Position(toPosition);

            newPosition.PositionZ += 0.005f * (ObjScale ?? 1.0f);

            if (Location.Landblock != newPosition.Landblock)
            {
                log.Error($"{Name} tried to teleport from {Location} to a different landblock {newPosition}");
                return;
            }

            // force out of hotspots
            PhysicsObj.report_collision_end(true);

            //HandlePreTeleportVisibility(newPosition);

            // do the physics teleport
            var setPosition = new Physics.Common.SetPosition();
            setPosition.Pos = new Physics.Common.Position(newPosition);
            setPosition.Flags = Physics.Common.SetPositionFlags.SendPositionEvent | Physics.Common.SetPositionFlags.Slide | Physics.Common.SetPositionFlags.Placement | Physics.Common.SetPositionFlags.Teleport;

            PhysicsObj.SetPosition(setPosition);

            // update ace location
            SyncLocation();

            // broadcast blip to new position
            SendUpdatePosition(true);
        }

        public bool IsBlockedByDoor(WorldObject target)
        {
            if (target == null)
                return false;

            System.Collections.Generic.List<Physics.PhysicsObj> knownDoors;
            if (this is Player)
                knownDoors = PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject != null && (o.WeenieObj.WorldObject.WeenieType == WeenieType.Door || o.WeenieObj.WorldObject.CreatureType == ACE.Entity.Enum.CreatureType.Wall));
            else
                knownDoors = target.PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject != null && (o.WeenieObj.WorldObject.WeenieType == WeenieType.Door || o.WeenieObj.WorldObject.CreatureType == ACE.Entity.Enum.CreatureType.Wall));

            bool nearDoor = false;
            foreach (var entry in knownDoors)
            {
                var door = entry.WeenieObj.WorldObject;
                if (!door.IsOpen && (Location.DistanceTo(door.Location) < 2f || target.Location.DistanceTo(door.Location) < 2f))
                {
                    nearDoor = true;
                    break;
                }
            }

            if (nearDoor && !IsDirectVisible(target, 1))
                return true;
            return false;
        }
    }
}
