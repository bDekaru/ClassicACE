using System;

using ACE.Server.Network.Structure;

namespace ACE.Server.Network.GameAction.Actions
{
    /// <summary>
    /// Sent by the client when a movement key is pressed / released
    /// </summary>
    public static class GameActionMoveToState
    {
        [GameAction(GameActionType.MoveToState)]
        public static void Handle(ClientMessage message, Session session)
        {
            //Console.WriteLine($"{session.Player.Name}.MoveToState");

            if (session.Player.PKLogout) return;

            var moveToState = new MoveToState(session.Player, message.Payload);
            session.Player.CurrentMoveToState = moveToState;

            if (session.Player.IsPlayerMovingTo)
                session.Player.StopExistingMoveToChains();

            if (session.Player.IsPlayerMovingTo2)
                session.Player.StopExistingMoveToChains2();

            // MoveToState - UpdatePosition broadcasts were capped to 1 per second in retail
            session.Player.OnMoveToState(moveToState);
            session.Player.LastMoveToState = moveToState;

            if (!session.Player.Teleporting)
                session.Player.SetRequestedLocation(moveToState.Position, false);

            //if (!moveToState.StandingLongJump)
                session.Player.BroadcastMovement(moveToState);

            if (moveToState.RawMotionState.ForwardCommand == ACE.Entity.Enum.MotionCommand.WalkForward)
                session.Player.LatestMovementHeading = 0;
            else if (moveToState.RawMotionState.ForwardCommand == ACE.Entity.Enum.MotionCommand.WalkBackwards)
                session.Player.LatestMovementHeading = 180;
            else if (moveToState.RawMotionState.SidestepCommand == ACE.Entity.Enum.MotionCommand.SideStepRight)
                session.Player.LatestMovementHeading = -90;
            else if (moveToState.RawMotionState.SidestepCommand == ACE.Entity.Enum.MotionCommand.SideStepLeft)
                session.Player.LatestMovementHeading = 90;

            if (session.Player.IsAfk)
            {
                if (moveToState.RawMotionState.CurrentHoldKey == ACE.Entity.Enum.HoldKey.Run)
                {
                    switch (moveToState.RawMotionState.ForwardCommand)
                    {
                        case ACE.Entity.Enum.MotionCommand.Invalid:
                        case ACE.Entity.Enum.MotionCommand.AFKState:
                            break;

                        default:
                            session.Player.HandleActionSetAFKMode(false);
                            break;
                    }

                    switch (moveToState.RawMotionState.TurnCommand)
                    {
                        case ACE.Entity.Enum.MotionCommand.Invalid:
                        case ACE.Entity.Enum.MotionCommand.AFKState:
                            break;

                        default:
                            session.Player.HandleActionSetAFKMode(false);
                            break;
                    }

                    switch (moveToState.RawMotionState.SidestepCommand)
                    {
                        case ACE.Entity.Enum.MotionCommand.Invalid:
                        case ACE.Entity.Enum.MotionCommand.AFKState:
                            break;

                        default:
                            session.Player.HandleActionSetAFKMode(false);
                            break;
                    }
                }
            }
        }
    }
}
