using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using System;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        private static int SneakingCheckInterval = 1;
        private static uint EnterSneakingDifficulty = 50;

        public bool IsSneaking = false;
        public bool IsAttackFromSneaking = false;

        private double SneakingDeltaTimeSum = 0;
        private ACE.Entity.Position PositionAtLastSneakingCheck = null;

        public void BeginSneaking()
        {
            if (IsSneaking)
                return;

            var result = TestSneakingInternal(EnterSneakingDifficulty);
            if (result == SneakingTestResult.Success)
            {
                IsSneaking = true;
                SneakingDeltaTimeSum = 0;
                PositionAtLastSneakingCheck = new ACE.Entity.Position(Location);
                Session.Network.EnqueueSend(new GameMessageSystemChat("You start sneaking.", ChatMessageType.Broadcast));
                EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SneakingBegin));
            }
            else if(result == SneakingTestResult.Failure)
                Session.Network.EnqueueSend(new GameMessageSystemChat("You fail on your attempt to start sneaking.", ChatMessageType.Broadcast));
            else
                Session.Network.EnqueueSend(new GameMessageSystemChat("You are not trained in sneaking!", ChatMessageType.Broadcast));
        }

        public void EndSneaking(string message = null, bool isAttackFromSneaking = false)
        {
            if (!IsSneaking)
                return;

            IsSneaking = false;
            IsAttackFromSneaking = isAttackFromSneaking;

            Session.Network.EnqueueSend(new GameMessageSystemChat(message == null ? "You stop sneaking." : message, ChatMessageType.Broadcast));
            if (!Teleporting)
            {
                // Add delay here to avoid remaining translucent indefinitely when EndSneaking is called right after BeginSneaking(like when a creature detects the player instantly).
                var actionChain = new ActionChain();
                actionChain.AddDelayForOneTick();
                actionChain.AddAction(this, () =>
                {
                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.SneakingEnd));
                });
                actionChain.EnqueueChain();
            }
        }

        public bool TestSneaking(Creature creature, double distanceSquared, string failureMessage)
        {
            if (!IsSneaking)
                return false;

            if (creature == null || distanceSquared > creature.VisualAwarenessRangeSq || !creature.IsDirectVisible(this))
                return true;

            var angle = Math.Abs(creature.GetAngle(this));
            if (angle < 110)
            {
                if (distanceSquared < 2)
                {
                    EndSneaking(failureMessage);
                    return false;
                }
                else
                {
                    var difficulty = (uint)((creature.Level ?? 1) * 3.0f);
                    return TestSneaking(difficulty, failureMessage);
                }
            }
            else
                return true;
        }

        public bool TestSneaking(Creature creature, string failureMessage)
        {
            if (!IsSneaking)
                return false;
            return TestSneaking(creature, PhysicsObj.get_distance_sq_to_object(creature.PhysicsObj, true), failureMessage);
        }

        public bool TestSneaking(uint difficulty, string failureMessage)
        {
            if(TestSneakingInternal(difficulty) != SneakingTestResult.Success)
            {
                EndSneaking(failureMessage);
                return false;
            }
            else
                return true;
        }

        private enum SneakingTestResult
        {
            Untrained,
            Failure,
            Success
        }

        private SneakingTestResult TestSneakingInternal(uint difficulty)
        {
            var sneakingSkill = GetCreatureSkill(Skill.Sneaking);
            if (sneakingSkill.AdvancementClass < SkillAdvancementClass.Trained)
                return SneakingTestResult.Untrained;

            var chance = SkillCheck.GetSkillChance(sneakingSkill.Current, difficulty);
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                Proficiency.OnSuccessUse(this, sneakingSkill, difficulty);
                return SneakingTestResult.Success;
            }
            return SneakingTestResult.Failure;
        }

        public void CheckSneaking(double deltaTime)
        {
            if (!IsSneaking)
                return;

            SneakingDeltaTimeSum += deltaTime;
            if (SneakingDeltaTimeSum < SneakingCheckInterval)
                return;
            SneakingDeltaTimeSum = 0;

            if (Location.DistanceTo(PositionAtLastSneakingCheck) > 5)
                EndSneaking("You've moved too quickly to keep sneaking.");

            PositionAtLastSneakingCheck = Location;
        }

        public bool IsAware(WorldObject wo)
        {
            return wo.IsAware(this);
        }

        public bool TestAwareness(WorldObject wo)
        {
            var awarenessSkill = GetCreatureSkill(Skill.Awareness);
            var chance = SkillCheck.GetSkillChance(awarenessSkill.Current, (uint)(wo.ResistAwareness ?? 0));
            if (chance > ThreadSafeRandom.Next(0.0f, 1.0f))
            {
                Proficiency.OnSuccessUse(this, awarenessSkill, (uint)(wo.ResistAwareness ?? 0));
                return true;
            }
            return false;
        }
    }
}
