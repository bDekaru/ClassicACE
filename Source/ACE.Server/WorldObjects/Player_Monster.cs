using System;
using System.Linq;
using ACE.Server.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Common;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Handles player->monster visibility checks
    /// </summary>
    partial class Player
    {
        private double NextSneakTestTimestamp;

        /// <summary>
        /// Wakes up any monsters within the applicable range
        /// </summary>
        public void CheckMonsters()
        {
            if (!Attackable || Teleporting) return;

            var visibleObjs = PhysicsObj.ObjMaint.GetVisibleObjectsValuesOfTypeCreature();

            var testSneak = false;
            if(IsSneaking && Time.GetUnixTime() > NextSneakTestTimestamp)
            {
                // Let's throttle sneak tests here otherwise the player will get checked at every movement keystroke.
                testSneak = true;
                NextSneakTestTimestamp = Time.GetFutureUnixTime(2);
            }

            foreach (var monster in visibleObjs)
            {
                if (monster is Player) continue;

                var distSq = PhysicsObj.get_distance_sq_to_object(monster.PhysicsObj, true);
                //var distSq = Location.SquaredDistanceTo(monster.Location);

                if (distSq <= monster.VisualAwarenessRangeSq && (!IsSneaking || (testSneak && !TestSneaking(monster, distSq, $"{monster.Name} sees you! You stop sneaking."))))
                    AlertMonster(monster);
            }
        }

        /// <summary>
        /// Called when this player attacks a monster
        /// </summary>
        public void OnAttackMonster(Creature monster)
        {
            if (monster == null || !Attackable) return;

            /*Console.WriteLine($"{Name}.OnAttackMonster({monster.Name})");
            Console.WriteLine($"Attackable: {monster.Attackable}");
            Console.WriteLine($"Tolerance: {monster.Tolerance}");*/

            // faction mobs will retaliate against players belonging to the same faction
            if (SameFaction(monster))
                monster.AddRetaliateTarget(this);

            if (monster.MonsterState != State.Awake && (monster.Tolerance & PlayerCombatPet_RetaliateExclude) == 0)
            {
                monster.AttackTarget = this;
                monster.WakeUp();
            }
        }
    }
}
