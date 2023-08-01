using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private readonly ActionQueue actionQueue = new ActionQueue();

        private int initialAge;
        private DateTime initialAgeTime;

        private const double ageUpdateInterval = 7;
        private double nextAgeUpdateTime;

        private double houseRentWarnTimestamp;
        private const double houseRentWarnInterval = 3600;

        private double leyLineAmuletsTickTimestamp;
        private const double leyLineAmuletsTickInterval = 1800;

        private double enchantmentTickTimestamp;
        private const double enchantmentTickInterval = 0.5;

        private double PvPInciteTickTimestamp;
        private const double PvPInciteTickInterval = 600;
        private const double PvPInciteInitialDelay = 3600;

        public void Player_Tick(double currentUnixTime)
        {
            if (CharacterSaveFailed)
            {
                // Boot the player as their Character object is not saving properly
                if (!IsLoggingOut)
                {
                    log.Error($"{Session.Player.Name} | 0x{Guid} | Account: {Account.AccountName} - disconnected for CharacterSaveFailed");
                    //Session.SendCharacterError(CharacterError.AccountLogin); // forces client to error screen
                    Session.Terminate(SessionTerminationReason.CharacterSaveFailed, new GameMessageCharacterError(CharacterError.AccountLogin));
                    //Session.LogOffPlayer(true);
                    CharacterSaveFailed = false;
                }
                return;
            }

            if (BiotaSaveFailed)
            {
                // Boot the player as their Biota object is not saving properly
                if (!IsLoggingOut)
                {
                    log.Error($"{Session.Player.Name} | 0x{Guid} | Account: {Account.AccountName} - disconnected for BiotaSaveFailed");
                    //Session.SendCharacterError(CharacterError.AccountLogin); // forces client to error screen
                    Session.Terminate(SessionTerminationReason.BiotaSaveFailed, new GameMessageCharacterError(CharacterError.AccountLogin));
                    //Session.LogOffPlayer(true);
                    BiotaSaveFailed = false;
                }
                return;
            }

            actionQueue.RunActions();

            if (nextAgeUpdateTime <= currentUnixTime)
            {
                nextAgeUpdateTime = currentUnixTime + ageUpdateInterval;

                if (initialAgeTime == DateTime.MinValue)
                {
                    initialAge = Age ?? 1;
                    initialAgeTime = DateTime.UtcNow;
                }

                Age = initialAge + (int)(DateTime.UtcNow - initialAgeTime).TotalSeconds;

                // In retail, this is sent every 7 seconds. If you adjust ageUpdateInterval from 7, you'll need to re-add logic to send this every 7s (if you want to match retail)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Age, Age ?? 1));
            }

            if (FellowVitalUpdate && Fellowship != null)
            {
                Fellowship.OnVitalUpdate(this);
                FellowVitalUpdate = false;
            }

            if (House != null && PropertyManager.GetBool("house_rent_enabled").Item)
            {
                if (houseRentWarnTimestamp > 0 && currentUnixTime > houseRentWarnTimestamp)
                {
                    HouseManager.GetHouse(House.Guid.Full, (house) =>
                    {
                        if (house != null && house.HouseStatus == HouseStatus.Active && !house.SlumLord.IsRentPaid())
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Warning!  You have not paid your maintenance costs for the last {(house.IsApartment ? "90" : "30")} day maintenance period.  Please pay these costs by this deadline or you will lose your house, and all your items within it.", ChatMessageType.Broadcast));
                    });

                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
                }
                else if (houseRentWarnTimestamp == 0)
                    houseRentWarnTimestamp = Time.GetFutureUnixTime(houseRentWarnInterval);
            }

            if (leyLineAmuletsTickTimestamp == 0 || currentUnixTime > leyLineAmuletsTickTimestamp)
            {
                LeyLineAmuletsTick(currentUnixTime);

                leyLineAmuletsTickTimestamp = Time.GetFutureUnixTime(leyLineAmuletsTickInterval);
            }

            if(PvPInciteTickTimestamp == 0)
                PvPInciteTickTimestamp = Time.GetFutureUnixTime(PvPInciteInitialDelay);
            else if (currentUnixTime > PvPInciteTickTimestamp)
            {
                PvPInciteTick(currentUnixTime);
                PvPInciteTickTimestamp = Time.GetFutureUnixTime(PvPInciteTickInterval);
            }

            if (enchantmentTickTimestamp == 0 || currentUnixTime > enchantmentTickTimestamp)
            {
                if (EnchantmentManager.HasEnchantments)
                    EnchantmentManager.HeartBeat(enchantmentTickInterval, false);
                enchantmentTickTimestamp = Time.GetFutureUnixTime(enchantmentTickInterval);
            }
        }

        private static readonly TimeSpan MaximumTeleportTime = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Called every ~5 seconds for Players
        /// </summary>
        public override void Heartbeat(double currentUnixTime)
        {
            NotifyLandblocks();

            ManaConsumersTick();

            HandleTargetVitals();

            LifestoneProtectionTick();

            PK_DeathTick();

            GagsTick();

            PhysicsObj.ObjMaint.DestroyObjects();

            // Check if we're due for our periodic SavePlayer
            if (LastRequestedDatabaseSave == DateTime.MinValue)
                LastRequestedDatabaseSave = DateTime.UtcNow;

            if (LastRequestedDatabaseSave.AddSeconds(PlayerSaveIntervalSecs) <= DateTime.UtcNow)
                SavePlayerToDatabase();

            if (Teleporting && DateTime.UtcNow > Time.GetDateTimeFromTimestamp(LastTeleportStartTimestamp ?? 0).Add(MaximumTeleportTime))
            {
                if (Session != null)
                    Session.LogOffPlayer(true);
                else
                    LogOut();
            }

            bool wasAlreadyEnforcing = EnforceMovement || EnforceMovementSpeed;
            EnforceMovement = PropertyManager.GetBool("enforce_player_movement").Item;
            EnforceMovementSpeed = PropertyManager.GetBool("enforce_player_movement_speed").Item;
            if ((EnforceMovement || EnforceMovementSpeed) && !Teleporting)
            {
                if (!wasAlreadyEnforcing)
                {
                    MovementEnforcementCounter = 0;
                    Location = PhysicsObj.Position.ACEPosition();
                    SnapPos = Location;
                    PrevMovementUpdateMaxSpeed = 0.0f;
                    LastPlayerMovementCheckTime = currentUnixTime;
                    HasPerformedActionsSinceLastMovementUpdate = false;
                }

                if (MovementEnforcementCounter > 0)
                    MovementEnforcementCounter--;

                if (!HasAnyMovement() && currentUnixTime > LastPlayerMovementCheckTime + 5)
                {
                    LastPlayerMovementCheckTime = currentUnixTime;
                    PrevMovementUpdateMaxSpeed = 0.0f;
                }
            }

            base.Heartbeat(currentUnixTime);
        }

        public static float MaxSpeed = 50;
        public static float MaxSpeedSq = MaxSpeed * MaxSpeed;

        public static bool DebugPlayerMoveToStatePhysics { get; set; } = false;

        /// <summary>
        /// Flag indicates if player is doing full physics simulation
        /// </summary>
        //public bool FastTick => IsPKType;
        public bool FastTick => true;

        public bool EnforceMovement { get; set; } = false;
        public bool EnforceMovementSpeed { get; set; } = false;

        /// <summary>
        /// For advanced spellcasting / players glitching around during powersliding,
        /// the reason for this retail bug is from 2 different functions for player movement
        /// 
        /// The client's self-player uses DoMotion/StopMotion
        /// The server and other players on the client use apply_raw_movement
        ///
        /// When a 3+ button powerslide is performed, this bugs out apply_raw_movement,
        /// and causes the player to spin in place. With DoMotion/StopMotion, it performs a powerslide.
        ///
        /// With this option enabled (retail defaults to false), the player's position on the server
        /// will match up closely with the player's client during powerslides.
        ///
        /// Since the client uses apply_raw_movement to simulate the movement of nearby players,
        /// the other players will still glitch around on screen, even with this option enabled.
        ///
        /// If you wish for the positions of other players to be less glitchy, the 'MoveToState_UpdatePosition_Threshold'
        /// can be lowered to achieve that
        /// </summary>

        public void OnMoveToState(MoveToState moveToState)
        {
            HasPerformedActionsSinceLastMovementUpdate = true;

            LastMoveToStateWasRun = CheckIsRunning();
            IsFirstAutoPosPacketSinceMoveToState = true;
            //Session.Network.EnqueueSend(new GameMessageSystemChat($"moveToState - Running: {LastMoveToStateWasRun}", ChatMessageType.Broadcast));

            if (!FastTick)
                return;

            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine(moveToState.RawMotionState);

            if (RecordCast.Enabled)
                RecordCast.OnMoveToState(moveToState);

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            if (!PropertyManager.GetBool("client_movement_formula").Item || moveToState.StandingLongJump)
                OnMoveToState_ServerMethod(moveToState);
            else
                OnMoveToState_ClientMethod(moveToState);

            if (MagicState.IsCasting && MagicState.PendingTurnRelease && moveToState.RawMotionState.TurnCommand == 0)
                OnTurnRelease();
        }

        public void OnMoveToState_ClientMethod(MoveToState moveToState)
        {
            var rawState = moveToState.RawMotionState;
            var prevState = LastMoveToState?.RawMotionState ?? RawMotionState.None;

            var mvp = new Physics.Animation.MovementParameters();
            mvp.HoldKeyToApply = rawState.CurrentHoldKey;

            if (!PhysicsObj.IsMovingOrAnimating)
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

            // ForwardCommand
            if (rawState.ForwardCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.ForwardCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)MotionCommand.Ready, mvp);
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
                // press alternate key
                else if (prevState.ForwardCommand != rawState.ForwardCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.ForwardCommand, mvp);
                }
            }
            else if (prevState.ForwardCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.ForwardCommand, mvp, true);
            }

            // StrafeCommand
            if (rawState.SidestepCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.SidestepCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
                // press alternate key
                else if (prevState.SidestepCommand != rawState.SidestepCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.SidestepCommand, mvp);
                }
            }
            else if (prevState.SidestepCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.SidestepCommand, mvp, true);
            }

            // TurnCommand
            if (rawState.TurnCommand != MotionCommand.Invalid)
            {
                // press new key
                if (prevState.TurnCommand == MotionCommand.Invalid)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
                // press alternate key
                else if (prevState.TurnCommand != rawState.TurnCommand)
                {
                    PhysicsObj.DoMotion((uint)rawState.TurnCommand, mvp);
                }
            }
            else if (prevState.TurnCommand != MotionCommand.Invalid)
            {
                // release key
                PhysicsObj.StopMotion((uint)prevState.TurnCommand, mvp, true);
            }
        }

        public void OnMoveToState_ServerMethod(MoveToState moveToState)
        {
            var minterp = PhysicsObj.get_minterp();
            minterp.RawState.SetState(moveToState.RawMotionState);

            if (moveToState.StandingLongJump)
            {
                minterp.RawState.ForwardCommand = (uint)MotionCommand.Ready;
                minterp.RawState.SideStepCommand = 0;
            }

            var allowJump = minterp.motion_allows_jump(minterp.InterpretedState.ForwardCommand) == WeenieError.None;

            //PhysicsObj.cancel_moveto();

            minterp.apply_raw_movement(true, allowJump);
        }

        public bool InUpdate;

        public override bool UpdateObjectPhysics()
        {
            try
            {
                stopwatch.Restart();

                bool landblockUpdate = false;

                InUpdate = true;

                // update position through physics engine
                if (RequestedLocation != null)
                {
                    landblockUpdate = UpdatePlayerPosition(RequestedLocation);
                    RequestedLocation = null;
                }

                if (FastTick && PhysicsObj.IsMovingOrAnimating || PhysicsObj.Velocity != Vector3.Zero)
                {
                    UpdatePlayerPhysics();

                    if (MoveToParams?.Callback != null && !PhysicsObj.IsMovingOrAnimating)
                        HandleMoveToCallback();
                }

                InUpdate = false;

                return landblockUpdate;
            }
            finally
            {
                var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                if (elapsedSeconds >= 1) // Yea, that ain't good....
                    log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdateObjectPhysics() at loc: {Location}");
                else if (elapsedSeconds >= 0.010)
                    log.Debug($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdateObjectPhysics() at loc: {Location}");
            }
        }

        public void UpdatePlayerPhysics()
        {
            if (DebugPlayerMoveToStatePhysics)
                Console.WriteLine($"{Name}.UpdatePlayerPhysics({PhysicsObj.PartArray.Sequence.CurrAnim.Value.Anim.ID:X8})");

            //Console.WriteLine($"{PhysicsObj.Position.Frame.Origin}");
            //Console.WriteLine($"{PhysicsObj.Position.Frame.get_heading()}");

            PhysicsObj.update_object();

            // sync ace position?
            Location.Rotation = PhysicsObj.Position.Frame.Orientation;

            if (!FastTick) return;

            // ensure PKLogout position is synced up for other players
            if (PKLogout)
            {
                EnqueueBroadcast(new GameMessageUpdateMotion(this, new Motion(MotionStance.NonCombat, MotionCommand.Ready)));
                PhysicsObj.StopCompletely(true);

                if (!PhysicsObj.IsMovingOrAnimating)
                {
                    SyncLocation();
                    EnqueueBroadcast(new GameMessageUpdatePosition(this));
                }
            }

            // this fixes some differences between client movement (DoMotion/StopMotion) and server movement (apply_raw_movement)
            //
            // scenario: start casting a self-spell, and then immediately start holding the run forward key during the windup
            // on client: player will start running forward after the cast has completed
            // on server: player will stand still

            // this block of code can improve the sync between these 2 methods,
            // however there are some bugs that originate in acclient that cannot be resolved on the server
            // for example, equip a wand, and then start running forward in non-combat mode. switch to magic combat mode, and then release forward during the stance swap
            // the client will never send a 'client released forward' MoveToState in this scenario unfortunately.
            // because of this, it's better to have the 'client blip forward' bug without it, than to have the client invisibly running forward on the server.
            // commenting out this block because of this...

            /*if (!PhysicsObj.IsMovingOrAnimating && LastMoveToState != null)
            {
                // apply latest MoveToState, if applicable
                //if ((LastMoveToState.RawMotionState.Flags & (RawMotionFlags.ForwardCommand | RawMotionFlags.SideStepCommand | RawMotionFlags.TurnCommand)) != 0)
                if ((LastMoveToState.RawMotionState.Flags & RawMotionFlags.ForwardCommand) != 0 && LastMoveToState.RawMotionState.ForwardHoldKey == HoldKey.Invalid)
                {
                    if (DebugPlayerMoveToStatePhysics)
                        Console.WriteLine("Re-applying movement: " + LastMoveToState.RawMotionState.Flags);

                    OnMoveToState(LastMoveToState);

                    // re-broadcast MoveToState to other clients only
                    EnqueueBroadcast(false, new GameMessageUpdateMotion(this, CurrentMovementData));
                }
                LastMoveToState = null;
            }*/

            if (MagicState.IsCasting && MagicState.PendingTurnRelease)
                CheckTurn();
        }

        /// <summary>
        /// The maximum rate UpdatePosition packets from MoveToState will be broadcast for each player
        /// AutonomousPosition still always broadcasts UpdatePosition
        ///  
        /// The default value (1 second) was estimated from this retail video:
        /// https://youtu.be/o5lp7hWhtWQ?t=112
        /// 
        /// If you wish for players to glitch around less during powerslides, lower this value
        /// </summary>
        public static TimeSpan MoveToState_UpdatePosition_Threshold = TimeSpan.FromSeconds(1);

        bool LastMoveToStateWasRun = false;
        bool IsFirstAutoPosPacketSinceMoveToState = false;

        public bool CheckIsRunning()
        {
            var minterp = PhysicsObj.get_minterp();
            var isRunning = minterp.RawState.CurrentHoldKey == HoldKey.Run;
            var isSideStepping = minterp.RawState.SideStepCommand != (uint)MotionCommand.Invalid;
            if (isSideStepping)
            {
                // We're dealing with a lot of inconsistencies here.
                var interpretedMotionState = CurrentMotionState.MotionState;
                isRunning = (interpretedMotionState.ForwardCommand == MotionCommand.RunForward && minterp.RawState.ForwardCommand != (uint)MotionCommand.WalkBackwards)
                    || (interpretedMotionState.ForwardCommand == MotionCommand.Invalid && minterp.RawState.SideStepCommand != (uint)MotionCommand.Invalid && minterp.RawState.CurrentHoldKey == HoldKey.Run)
                    || (interpretedMotionState.ForwardCommand == MotionCommand.WalkForward && interpretedMotionState.SidestepCommand == MotionCommand.Invalid && !(minterp.RawState.CurrentHoldKey == HoldKey.Invalid && (minterp.RawState.ForwardCommand == (uint)MotionCommand.WalkForward || minterp.RawState.ForwardCommand == (uint)MotionCommand.WalkBackwards)))
                    || (interpretedMotionState.ForwardCommand == MotionCommand.WalkForward && (interpretedMotionState.SidestepCommand == MotionCommand.SideStepRight || interpretedMotionState.SidestepCommand == MotionCommand.SideStepLeft) && minterp.RawState.CurrentHoldKey == HoldKey.Run);
            }

            return isRunning;
        }

        /// <summary>
        /// Used by physics engine to actually update a player position
        /// Automatically notifies clients of updated position
        /// </summary>
        /// <param name="newPosition">The new position being requested, before verification through physics engine</param>
        /// <returns>TRUE if object moves to a different landblock</returns>
        public bool UpdatePlayerPosition(ACE.Entity.Position newPosition, bool forceUpdate = false)
        {
            var currentTime = Time.GetUnixTime();
            float deltaTime = (float)(currentTime - LastPlayerAutoposTime);
            LastPlayerAutoposTime = currentTime;

            //Console.WriteLine($"{Name}.UpdatePlayerPhysics({newPosition}, {forceUpdate}, {Teleporting})");
            bool verifyContact = false;

            // possible bug: while teleporting, client can still send AutoPos packets from old landblock
            if (Teleporting && !forceUpdate) return false;

            // pre-validate movement
            if (!ValidateMovement(newPosition))
            {
                log.Error($"{Name}.UpdatePlayerPosition() - movement pre-validation failed from {Location} to {newPosition}");
                return false;
            }

            try
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                    stopwatch.Restart();

                var success = true;

                if (PhysicsObj != null)
                {
                    var distSq = Location.SquaredDistanceTo(newPosition);

                    if (distSq > PhysicsGlobals.EpsilonSq)
                    {
                        /*var p = new Physics.Common.Position(newPosition);
                        var dist = PhysicsObj.Position.Distance(p);
                        Console.WriteLine($"Dist: {dist}");*/

                        if (newPosition.Landblock == 0x18A && Location.Landblock != 0x18A)
                            log.Info($"{Name} is getting swanky");

                        if (!Teleporting)
                        {
                            var blockDist = PhysicsObj.GetBlockDist(Location.Cell, newPosition.Cell);

                            // verify movement
                            if (distSq > MaxSpeedSq && blockDist > 1)
                            {
                                //Session.Network.EnqueueSend(new GameMessageSystemChat("Movement error", ChatMessageType.Broadcast));
                                log.Warn($"MOVEMENT SPEED: {Name} trying to move from {Location} to {newPosition}, speed: {Math.Sqrt(distSq)}");
                                return false;
                            }

                            // verify z-pos
                            if (blockDist == 0 && LastGroundPos != null && newPosition.PositionZ - LastGroundPos.PositionZ > 10 && DateTime.UtcNow - LastJumpTime > TimeSpan.FromSeconds(1) && GetCreatureSkill(Skill.Jump).Current < 1000)
                                verifyContact = true;
                        }

                        var curCell = LScape.get_landcell(newPosition.Cell);
                        if (curCell != null)
                        {
                            //if (PhysicsObj.CurCell == null || curCell.ID != PhysicsObj.CurCell.ID)
                            //PhysicsObj.change_cell_server(curCell);

                            PhysicsObj.set_request_pos(newPosition.Pos, newPosition.Rotation, curCell, Location.LandblockId.Raw);

                            if (!Teleporting)
                            {
                                // The client does not seem to send any packets when walk/run is toggled by hitting shift, so unless the player does something else(like turning) we won't find about it.
                                // To reduce the delay we check the distance the player requested on this packet and toggle walk/run ourselves, this still has a delay compared to the client.
                                var minterp = PhysicsObj.get_minterp();
                                if (!IsJumping && !PhysicsObj.TransientState.HasFlag(TransientStateFlags.Sliding) && !IsFirstAutoPosPacketSinceMoveToState && (minterp.RawState.ForwardCommand != (uint)MotionCommand.Ready || minterp.RawState.SideStepCommand != (uint)MotionCommand.Invalid))
                                {
                                    var isRunning = CheckIsRunning();
                                    var isForward = minterp.RawState.ForwardCommand != (uint)MotionCommand.WalkBackwards;
                                    var hasForwardOrBackwardsMovement = minterp.RawState.ForwardCommand != (uint)MotionCommand.Ready;
                                    var isSideStepping = minterp.RawState.SideStepCommand != (uint)MotionCommand.Invalid;
                                    var myRunRate = GetRunRate();

                                    var curPos = Location.PhysPosition();
                                    var reqPos = RequestedLocation.PhysPosition();
                                    var realDist = curPos.Distance(reqPos);

                                    float runRate;
                                    float walkRate;
                                    if (isForward)
                                    {
                                        runRate = myRunRate;
                                        walkRate = 1.0f;
                                    }
                                    else
                                    {
                                        runRate = -0.65f * myRunRate * 0.65f;
                                        walkRate = -0.65f * 0.65f;
                                    }

                                    if (isSideStepping)
                                    {
                                        if (hasForwardOrBackwardsMovement)
                                        {
                                            runRate *= 3.12f / 1.25f * 0.5f;
                                            walkRate *= 3.12f / 1.25f * 0.5f;
                                        }
                                        else
                                        {
                                            var multiplier = 1.0f;
                                            if (minterp.RawState.SideStepCommand == (uint)MotionCommand.SideStepLeft)
                                                multiplier = -1.0f;

                                            runRate = multiplier * 0.65f * myRunRate * 0.65f;
                                            walkRate = multiplier * 0.65f * 0.65f;
                                        }
                                    }

                                    var heading = curPos.Frame.get_vector_heading();
                                    var testRunPoint = curPos.Frame.Origin + (heading * (runRate * 4.0f * deltaTime));
                                    var testRunDist = (curPos.Frame.Origin - testRunPoint).Length();

                                    var testWalkPoint = curPos.Frame.Origin + (heading * (walkRate * 4.0f * deltaTime));
                                    var testWalkDist = (curPos.Frame.Origin - testWalkPoint).Length();

                                    var shouldBeWalking = false;
                                    var runDistDelta = Math.Abs(testRunDist - realDist);
                                    var walkDistDelta = Math.Abs(testWalkDist - realDist);
                                    if (LastMoveToStateWasRun)
                                        runDistDelta -= 0.25f * deltaTime;
                                    else
                                        walkDistDelta -= 0.25f * deltaTime;

                                    if (runDistDelta > walkDistDelta)
                                        shouldBeWalking = true;

                                    //Session.Network.EnqueueSend(new GameMessageSystemChat($"isRunning: {isRunning} wasRunning: {LastMoveToStateWasRun}", ChatMessageType.Broadcast));
                                    //Session.Network.EnqueueSend(new GameMessageSystemChat($"{realDist.ToString("0.00")} {testRunDist.ToString("0.00")} {testWalkDist.ToString("0.00")}", ChatMessageType.Broadcast));

                                    var toggledRunWalkState = false;
                                    if (isRunning && shouldBeWalking)
                                    {
                                        toggledRunWalkState = true;

                                        minterp.RawState.CurrentHoldKey = HoldKey.Invalid;
                                        CurrentMoveToState.RawMotionState.CurrentHoldKey = HoldKey.Invalid;
                                        CurrentMoveToState.RawMotionState.Flags &= ~RawMotionFlags.CurrentHoldKey;

                                        if (isSideStepping && (hasForwardOrBackwardsMovement && !isForward))
                                            CurrentMoveToState.RawMotionState.Flags &= ~RawMotionFlags.SideStepCommand;

                                        //Session.Network.EnqueueSend(new GameMessageSystemChat($"{realDist.ToString("0.00")} {testRunDist.ToString("0.00")} {testWalkDist.ToString("0.00")}", ChatMessageType.Broadcast));
                                        //Session.Network.EnqueueSend(new GameMessageSystemChat("Switch to walk", ChatMessageType.Broadcast));
                                    }
                                    else if (!isRunning && !shouldBeWalking)
                                    {
                                        toggledRunWalkState = true;

                                        minterp.RawState.CurrentHoldKey = HoldKey.Run;
                                        CurrentMoveToState.RawMotionState.CurrentHoldKey = HoldKey.Run;
                                        CurrentMoveToState.RawMotionState.Flags |= RawMotionFlags.CurrentHoldKey;

                                        if (isSideStepping && (hasForwardOrBackwardsMovement && !isForward))
                                            CurrentMoveToState.RawMotionState.Flags |= RawMotionFlags.SideStepCommand;

                                        //Session.Network.EnqueueSend(new GameMessageSystemChat($"{realDist.ToString("0.00")} {testRunDist.ToString("0.00")} {testWalkDist.ToString("0.00")}", ChatMessageType.Broadcast));
                                        //Session.Network.EnqueueSend(new GameMessageSystemChat("Switch to run", ChatMessageType.Broadcast));
                                    }

                                    if (toggledRunWalkState)
                                    {
                                        var allowJump = minterp.motion_allows_jump(minterp.InterpretedState.ForwardCommand) == WeenieError.None;
                                        minterp.apply_raw_movement(true, allowJump);

                                        BroadcastMovement(CurrentMoveToState);
                                    }
                                }
                                else
                                {
                                    IsFirstAutoPosPacketSinceMoveToState = false;
                                }
                            }

                            if (FastTick)
                                success = PhysicsObj.update_object_server_new(!EnforceMovement) ;
                            else
                                success = PhysicsObj.update_object_server();

                            if (PhysicsObj.CurCell == null && curCell.ID >> 16 != 0x18A)
                            {
                                PhysicsObj.CurCell = curCell;
                            }

                            if (verifyContact && IsJumping)
                            {
                                var blockDist = PhysicsObj.GetBlockDist(newPosition.Cell, LastGroundPos.Cell);

                                if (blockDist <= 1)
                                {
                                    log.Warn($"z-pos hacking detected for {Name}, lastGroundPos: {LastGroundPos.ToLOCString()} - requestPos: {newPosition.ToLOCString()}");
                                    Location = new ACE.Entity.Position(LastGroundPos);
                                    Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                    SendUpdatePosition();
                                    return false;
                                }
                            }

                            CheckMonsters();
                        }
                    }
                    else
                        PhysicsObj.Position.Frame.Orientation = newPosition.Rotation;

                    if (EnforceMovementSpeed && success && !Teleporting && GodState == null)
                    {
                        float enforcementDeltaTime = (float)(currentTime - LastPlayerMovementCheckTime);
                        LastPlayerMovementCheckTime = currentTime;

                        // Check for illegal player movements.
                        var loggingHasPerformedActionsSinceLastMovementUpdate = HasPerformedActionsSinceLastMovementUpdate;
                        var loggingPrevMaxMovementSpeed = PrevMovementUpdateMaxSpeed;
                        var loggingInertia = false;

                        var dist = Location.DistanceTo(newPosition);
                        float velocity = PhysicsObj.CachedVelocity.Length();
                        float currentMaxSpeed;

                        if (dist > PhysicsGlobals.EPSILON)
                        {
                            if (FastTick)
                            {
                                var runRate = GetRunRate();
                                currentMaxSpeed = (1.8f * runRate * enforcementDeltaTime * (1.0f + velocity / 8.0f)) + 5.0f;
                                if (runRate < 1.9f && PhysicsObj.CachedVelocity.Z < -20.0f) // Very slow characters can still fall pretty quickly.
                                    currentMaxSpeed *= 2.5f;
                            }
                            else
                            {
                                // This is no longer used because FastTick is set to be always on but leaving it here for now.
                                currentMaxSpeed = (5.5f * GetRunRate() * enforcementDeltaTime * (1.0f + velocity / 5.0f)) + 2.0f;

                                if (HasPerformedActionsSinceLastMovementUpdate)
                                    currentMaxSpeed *= 1.8f;
                            }

                            if (currentMaxSpeed < PrevMovementUpdateMaxSpeed && PrevMovementUpdateMaxSpeed > 25.0f)
                            {
                                // We were going really fast and now we are slowing down but we might still have some inertia.
                                loggingInertia = true;
                                currentMaxSpeed = PrevMovementUpdateMaxSpeed * 0.5f;
                            }
                            PrevMovementUpdateMaxSpeed = currentMaxSpeed;

                            if (dist > currentMaxSpeed)
                            {
                                if (MovementEnforcementCounter < 10 || !PropertyManager.GetBool("enforce_player_movement_kick").Item)
                                {
                                    if (MovementEnforcementCounter == 0 && currentMaxSpeed != 0 && dist < currentMaxSpeed * 1.5)
                                    {
                                        // Slight invalid movement detected but the player has otherwise been behaving, assume it was just a lag spike or client stutter.
                                        MovementEnforcementCounter++;
                                    }
                                    else
                                    {
                                        MovementEnforcementCounter++;
                                        Location = new ACE.Entity.Position(SnapPos);
                                        Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                        SendUpdatePosition();

                                        Session.Network.EnqueueSend(new GameMessageSystemChat("Invalid movement detected. Rolling back to last known good location.", ChatMessageType.Help));

                                        log.Warn($"{Name} - INVALID MOVEMENT DETECTED - Speed: {dist.ToString("0.00")}/{currentMaxSpeed.ToString("0.00")} PrevMaxSpeed: {loggingPrevMaxMovementSpeed.ToString("0.00")}({loggingInertia}) FastTick: {FastTick} TimeSpam: {enforcementDeltaTime.ToString("0.00")} Velocity: {velocity.ToString("0.00")} actionsSinceLastMovementUpdate: {loggingHasPerformedActionsSinceLastMovementUpdate}");
                                        //Session.Network.EnqueueSend(new GameMessageSystemChat($"Speed: {dist.ToString("0.00")}/{currentMaxSpeed.ToString("0.00")} PrevMaxSpeed: {loggingPrevMaxMovementSpeed.ToString("0.00")}({loggingInertia}) FastTick: {FastTick} TimeSpam: {deltaTime.ToString("0.00")} Velocity: {velocity.ToString("0.00")} timeSinceLastAction: {timeSinceLastAction.ToString("0.00")} isMovingOrAnimating: {isMovingOrAnimating} actionsSinceLastMovementUpdate: {loggingHasPerformedActionsSinceLastMovementUpdate}", ChatMessageType.Help));
                                        return false;
                                    }
                                }
                                else
                                {
                                    // Kick players when they go over 10 enforcements in a minute.
                                    Session.Terminate(SessionTerminationReason.MovementEnforcementFailure, new GameMessageBootAccount(" because there is a divergence between your server and client locations"));
                                    log.Warn($"{Name} - INVALID MOVEMENT DETECTED - Speed: {dist.ToString("0.00")}/{currentMaxSpeed.ToString("0.00")} PrevMaxSpeed: {loggingPrevMaxMovementSpeed.ToString("0.00")}({loggingInertia}) FastTick: {FastTick} TimeSpam: {enforcementDeltaTime.ToString("0.00")} Velocity: {velocity.ToString("0.00")} actionsSinceLastMovementUpdate: {loggingHasPerformedActionsSinceLastMovementUpdate}");
                                    return false;
                                }
                            }
                            //else
                            //    Session.Network.EnqueueSend(new GameMessageSystemChat($"Speed: {dist.ToString("0.00")}/{currentMaxSpeed.ToString("0.00")} PrevMaxSpeed: {loggingPrevMaxMovementSpeed.ToString("0.00")}({loggingInertia}) FastTick: {FastTick} TimeSpam: {deltaTime.ToString("0.00")} Velocity: {velocity.ToString("0.00")} timeSinceLastAction: {timeSinceLastAction.ToString("0.00")} isMovingOrAnimating: {isMovingOrAnimating} actionsSinceLastMovementUpdate: {loggingHasPerformedActionsSinceLastMovementUpdate}", ChatMessageType.Broadcast));
                        }
                        //else
                        //    Session.Network.EnqueueSend(new GameMessageSystemChat($"Speed: {dist.ToString("0.00")}/0.00 PrevMaxSpeed: {loggingPrevMaxMovementSpeed.ToString("0.00")}({loggingInertia}) FastTick: {FastTick} TimeSpam: {deltaTime.ToString("0.00")} Velocity: {velocity.ToString("0.00")} timeSinceLastAction: {timeSinceLastAction.ToString("0.00")} isMovingOrAnimating: {isMovingOrAnimating} actionsSinceLastMovementUpdate: {loggingHasPerformedActionsSinceLastMovementUpdate}", ChatMessageType.Broadcast));

                        if (HasPerformedActionsSinceLastMovementUpdate && !IsJumping)
                            HasPerformedActionsSinceLastMovementUpdate = false; // Delay disabling this until we're done with the jump.

                        if (!IsJumping && PhysicsObj.TransientState.HasFlag(TransientStateFlags.OnWalkable))
                            SnapPos = Location;
                    }
                }

                // double update path: landblock physics update -> updateplayerphysics() -> update_object_server() -> Teleport() -> updateplayerphysics() -> return to end of original branch
                if (Teleporting && !forceUpdate) return true;

                if (!success) return false;

                var landblockUpdate = Location.Cell >> 16 != newPosition.Cell >> 16;

                Location = newPosition;

                if (RecordCast.Enabled)
                    RecordCast.Log($"CurPos: {Location.ToLOCString()}");

                if (RequestedLocationBroadcast || DateTime.UtcNow - LastUpdatePosition >= MoveToState_UpdatePosition_Threshold)
                    SendUpdatePosition();
                else
                    Session.Network.EnqueueSend(new GameMessageUpdatePosition(this));

                if (!InUpdate)
                    LandblockManager.RelocateObjectForPhysics(this, true);

                return landblockUpdate;
            }
            finally
            {
                if (!forceUpdate) // This is needed beacuse this function might be called recursively
                {
                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.Player_Tick_UpdateObjectPhysics, elapsedSeconds);
                    if (elapsedSeconds >= 0.100) // Yea, that ain't good....
                        log.Warn($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                    else if (elapsedSeconds >= 0.010)
                        log.Debug($"[PERFORMANCE][PHYSICS] {Guid}:{Name} took {(elapsedSeconds * 1000):N1} ms to process UpdatePlayerPosition() at loc: {Location}");
                }
            }
        }

        public bool HasAnyMovement()
        {
            if (FastTick)
            {
                if (PhysicsObj.IsMovingOrAnimating || IsMoving || IsPlayerMovingTo || IsPlayerMovingTo2)
                    return true;
            }
            else
            {
                var isWaitingForNextUseTime = DateTime.UtcNow < NextUseTime;
                var isPlayerInitiatedMovement = (CurrentMoveToState.RawMotionState.Flags & (RawMotionFlags.ForwardCommand | RawMotionFlags.SideStepCommand | RawMotionFlags.TurnCommand)) != 0;

                if (isPlayerInitiatedMovement || HasPerformedActionsSinceLastMovementUpdate || IsJumping || PhysicsObj.IsMovingOrAnimating || IsMoving || IsPlayerMovingTo || IsPlayerMovingTo2 || isWaitingForNextUseTime)
                    return true;
            }
            return false;
        }

        private static HashSet<uint> buggedCells = new HashSet<uint>()
        {
            0xD6990112,
            0xD599012C
        };

        public bool ValidateMovement(ACE.Entity.Position newPosition)
        {
            if (CurrentLandblock == null)
                return false;

            if (!Teleporting && Location.Landblock != newPosition.Cell >> 16)
            {
                if ((Location.Cell & 0xFFFF) >= 0x100 && (newPosition.Cell & 0xFFFF) >= 0x100)
                {
                    if (!buggedCells.Contains(Location.Cell) || !buggedCells.Contains(newPosition.Cell))
                        return false;
                }

                if (CurrentLandblock.IsDungeon)
                {
                    var destBlock = LScape.get_landblock(newPosition.Cell);
                    if (destBlock != null && destBlock.IsDungeon)
                        return false;
                }
            }
            return true;
        }


        public bool SyncLocationWithPhysics()
        {
            if (PhysicsObj.CurCell == null)
            {
                Console.WriteLine($"{Name}.SyncLocationWithPhysics(): CurCell is null!");
                return false;
            }

            var blockcell = PhysicsObj.Position.ObjCellID;
            var pos = PhysicsObj.Position.Frame.Origin;
            var rotate = PhysicsObj.Position.Frame.Orientation;

            var landblockUpdate = blockcell << 16 != CurrentLandblock.Id.Landblock;

            Location = new ACE.Entity.Position(blockcell, pos, rotate);

            return landblockUpdate;
        }

        private bool gagNoticeSent = false;

        public void GagsTick()
        {
            if (IsGagged)
            {
                if (!gagNoticeSent)
                {
                    SendGagNotice();
                    gagNoticeSent = true;
                }

                // check for gag expiration, if expired, remove gag.
                GagDuration -= CachedHeartbeatInterval;

                if (GagDuration <= 0)
                {
                    IsGagged = false;
                    GagTimestamp = 0;
                    GagDuration = 0;
                    SaveBiotaToDatabase();
                    SendUngagNotice();
                    gagNoticeSent = false;
                }
            }
        }

        /// <summary>
        /// Prepare new action to run on this player
        /// </summary>
        public override void EnqueueAction(IAction action)
        {
            actionQueue.EnqueueAction(action);
        }

        public void LeyLineAmuletsTick(double currentUnixTime)
        {
            if (EquippedObjectsLoaded && InventoryLoaded)
            {
                var list = EquippedObjects.Values.Where(i => i.WeenieType == WeenieType.LeyLineAmulet).ToList();
                list = list.Concat(Inventory.Values).Where(i => i.WeenieType == WeenieType.LeyLineAmulet).ToList();

                foreach (var item in list)
                {
                    LeyLineAmulet amulet = item as LeyLineAmulet;
                    if(amulet != null)
                        amulet.CheckAlignmentDecay(this, currentUnixTime);
                }
            }
        }

        private static List<string> PvPInciteMessages = new List<string>
        {
            "It would be a shame if someone would kill them...",
            "Be a good boy and do your thing would you?",
            "Just hanging around without a care in the world..."
        };
        public void PvPInciteTick(double currentUnixTime)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if ((!IsPK && !IsPKL) || ThreadSafeRandom.Next(0.0f, 1.0f) > 0.2f)
                return;

            List<Player> possiblePlayers;

            if(GameplayMode == GameplayModes.HardcorePK)
                possiblePlayers = PlayerManager.GetAllOnline().Where(e => e.Guid != Guid && e.GameplayMode == GameplayModes.HardcorePK && e.Level >= Level && e.Level <= Level + 5).ToList();
            else
                possiblePlayers = PlayerManager.GetAllOnline().Where(e => e.Guid != Guid && e.GameplayMode == GameplayModes.Regular && e.IsPK && e.Level >= Level && e.Level <= Level + 5).ToList();

            if (possiblePlayers.Count() > 0)
            {
                var validPossiblePlayers = new List<Player>();
                foreach (var entry in possiblePlayers)
                {
                    if (!LandblockManager.apartmentLandblocks.Contains((uint)entry.Location.LandblockId.Landblock << 16 ^ 0x0000FFFF) && !NoDamage_Landblocks.Contains(entry.Location.LandblockId.Landblock) && (Allegiance == null || entry.Allegiance != Allegiance) && (Fellowship == null || entry.Fellowship != Fellowship))
                        validPossiblePlayers.Add(entry);
                }

                if (validPossiblePlayers.Count() > 0)
                {
                    var rolledPlayer = validPossiblePlayers[ThreadSafeRandom.Next(0, validPossiblePlayers.Count() - 1)];

                    var position = rolledPlayer.Biota.GetPosition(PositionType.Location, rolledPlayer.BiotaDatabaseLock);

                    string locationString = Server.Entity.Landblock.GetLocationString(position.LandblockId.Landblock);

                    if (locationString != "")
                    {
                        var message = PvPInciteMessages[ThreadSafeRandom.Next(0, PvPInciteMessages.Count() - 1)];
                        Session.Network.EnqueueSend(new GameMessageSound(Guid, Sound.HealthDownVoid));
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"Bael'zharon whispers in your ear, \"{rolledPlayer.Name} is currently{locationString}. {(Gender == 2 ? "She" : "He")} is level {rolledPlayer.Level}. {message}\"", ChatMessageType.Tell));
                    }
                }
            }
        }

        /// <summary>
        /// Called every ~5 secs for equipped mana consuming items
        /// </summary>
        public void ManaConsumersTick()
        {
            if (!EquippedObjectsLoaded) return;

            foreach (var item in EquippedObjects.Values)
            {
                if (!item.IsAffecting)
                    continue;

                if (item.ItemCurMana == null || item.ItemMaxMana == null || item.ManaRate == null)
                    continue;

                var burnRate = -item.ManaRate.Value;

                if (LumAugItemManaUsage != 0)
                    burnRate *= GetNegativeRatingMod(LumAugItemManaUsage * 5);

                item.ItemManaRateAccumulator += (float)(burnRate * CachedHeartbeatInterval);

                if (item.ItemManaRateAccumulator < 1)
                    continue;

                var manaToBurn = (int)Math.Floor(item.ItemManaRateAccumulator);

                if (manaToBurn > item.ItemCurMana)
                    manaToBurn = item.ItemCurMana.Value;

                item.ItemCurMana -= manaToBurn;

                item.ItemManaRateAccumulator -= manaToBurn;

                if (item.ItemCurMana > 0)
                    CheckLowMana(item, burnRate);
                else
                    HandleManaDepleted(item);
            }
        }

        private bool CheckLowMana(WorldObject item, double burnRate)
        {
            const int lowManaWarningSeconds = 120;

            var secondsUntilEmpty = item.ItemCurMana / burnRate;

            if (secondsUntilEmpty > lowManaWarningSeconds)
            {
                item.ItemManaDepletionMessage = false;
                return false;
            }
            if (!item.ItemManaDepletionMessage)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {item.Name} is low on Mana.", ChatMessageType.Magic));
                item.ItemManaDepletionMessage = true;
            }
            return true;
        }

        private void HandleManaDepleted(WorldObject item)
        {
            var msg = new GameMessageSystemChat($"Your {item.Name} is out of Mana.", ChatMessageType.Magic);
            var sound = new GameMessageSound(Guid, Sound.ItemManaDepleted);
            Session.Network.EnqueueSend(msg, sound);

            // unsure if these messages / sounds were ever sent in retail,
            // or if it just purged the enchantments invisibly
            // doing a delay here to prevent 'SpellExpired' sounds from overlapping with 'ItemManaDepleted'
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(2.0f);
            actionChain.AddAction(this, () =>
            {
                foreach (var spellId in item.Biota.GetKnownSpellsIds(item.BiotaDatabaseLock))
                    RemoveItemSpell(item, (uint)spellId);
            });
            actionChain.EnqueueChain();

            item.OnSpellsDeactivated();
        }

        public override void HandleMotionDone(uint motionID, bool success)
        {
            //Console.WriteLine($"{Name}.HandleMotionDone({(MotionCommand)motionID}, {success})");

            if (!FastTick) return;

            if (FoodState.IsChugging)
                HandleMotionDone_UseConsumable(motionID, success);

            if (MagicState.IsCasting)
                HandleMotionDone_Magic(motionID, success);
        }
    }
}
