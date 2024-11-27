using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Entity.Chess;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    public class GamePiece : Creature
    {
        public ChessMatch ChessMatch;

        public GamePieceState GamePieceState;
        public Position Position;
        public GamePiece TargetPiece;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public GamePiece(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public GamePiece(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            TimeToRot = -1;
        }

        public void Kill()
        {
            ActionChain killChain = new ActionChain();
            killChain.AddAction(this, () =>
            {
                EnqueueBroadcastMotion(new Motion(MotionStance.NonCombat, MotionCommand.Dead));
            });
            killChain.AddDelaySeconds(5);
            killChain.AddAction(this, () =>
            {
                ApplyVisualEffects(PlayScript.Destroy);
            });
            killChain.AddDelaySeconds(1);
            killChain.AddAction(this, () => Destroy());
            killChain.EnqueueChain();
        }

        public void MoveEnqueue(Position dest)
        {
            GamePieceState = GamePieceState.MoveToSquare;
            Position = dest;
        }

        public void AttackEnqueue(Position dest, ObjectGuid victim)
        {
            GamePieceState = GamePieceState.MoveToAttack;
            Position = dest;
            TargetPiece = CurrentLandblock.GetObject(victim) as GamePiece;
        }

        public void Tick(double currentUnixTime)
        {
            NextMonsterTickTime = currentUnixTime + monsterTickInterval;

            switch (GamePieceState)
            {
                case GamePieceState.MoveToSquare:
                    GamePieceState = GamePieceState.WaitingForMoveToSquare;
                    MoveWeenie(Position, 0.3f, true);
                    break;

                // visual awareness range of piece is only 1, make sure we are close enough to attack
                case GamePieceState.MoveToAttack:
                    GamePieceState = GamePieceState.WaitingForMoveToAttack;
                    MoveWeenie(Position, PhysicsObj.GetRadius() + TargetPiece.PhysicsObj.GetRadius(), false);
                    break;

                case GamePieceState.WaitingForMoveToSquare:
                case GamePieceState.WaitingForMoveToAttack:
                    UpdatePosition();
                    break;

                case GamePieceState.WaitingForMoveToSquareAnimComplete:

                    /*if (PhysicsObj.IsAnimating)
                    {
                        UpdatePosition();
                        break;
                    }*/

                    ChessMatch.PieceReady(Guid);
                    GamePieceState = GamePieceState.None;
                    break;

                case GamePieceState.Combat:

                    if (CombatTable == null)
                        GetCombatTable();

                    MeleeAttack();
                    break;
            }
        }

        public override void OnMoveComplete(WeenieError status)
        {
            //Console.WriteLine($"{Name}.OnMoveComplete({status})");

            base.OnMoveComplete(status);

            if (status != WeenieError.None)
                return;

            switch (GamePieceState)
            {
                // we are done, tell the match so the turn can finish
                case GamePieceState.WaitingForMoveToSquare:
                    GamePieceState = GamePieceState.WaitingForMoveToSquareAnimComplete;
                    break;

                // there is another piece on this square, attack it!
                case GamePieceState.WaitingForMoveToAttack:
                    AttackTarget = TargetPiece;
                    GamePieceState = GamePieceState.Combat;
                    break;
            }
        }

        public void OnDealtDamage(/*DamageEvent damageData*/)
        {
            // weenie piece is dead, time to move into the square completely
            if (TargetPiece.IsDead)
                GamePieceState = GamePieceState.MoveToSquare;
        }

        public Motion LastMoveTo;

        public void MoveWeenie(Position to, float distanceToObject, bool finalHeading)
        {
            if (MoveSpeed == 0.0f)
                UpdateMovementSpeed();

            var motion = new Motion(this, to);
            motion.MoveToParameters.DistanceToObject = distanceToObject;
            motion.MoveToParameters.MovementParameters &= ~MovementParams.UseSpheres;

            if (finalHeading)
                motion.MoveToParameters.MovementParameters |= MovementParams.UseFinalHeading;

            SetWalkRunThreshold(motion, to);

            var mvp = new MovementParameters(motion);

            PhysicsObj.MoveToPosition(new Physics.Common.Position(to), mvp);

            IsMoving = true;
            MonsterState = State.Awake;
            IsAwake = true;

            LastMoveTo = motion;

            EnqueueBroadcastMotion(motion);
        }

        public override void BroadcastMoveTo(Player player)
        {
            player.Session.Network.EnqueueSend(new GameMessageUpdateMotion(this, LastMoveTo));
        }
    }
}
