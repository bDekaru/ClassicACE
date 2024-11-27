using System;
using System.Numerics;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// The delay between missile attacks (todo: find actual value)
        /// </summary>
        public const float MissileDelay = 1.0f;

        /// <summary>
        /// Returns TRUE if monster has physical ranged attacks
        /// </summary>
        public new bool IsRanged => GetEquippedMissileWeapon() != null;

        /// <summary>
        /// Starts a monster missile attack
        /// </summary>
        private void RangeAttack()
        {
            var targetCreature = AttackTarget as Creature;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                var knownDoors = targetCreature.PhysicsObj.ObjMaint.GetVisibleObjectsValuesWhere(o => o.WeenieObj.WorldObject != null && (o.WeenieObj.WorldObject.WeenieType == WeenieType.Door || o.WeenieObj.WorldObject.CreatureType == ACE.Entity.Enum.CreatureType.Wall));

                bool nearDoor = false;
                foreach (var entry in knownDoors)
                {
                    var door = entry.WeenieObj.WorldObject;
                    if (!door.IsOpen && (Location.DistanceTo(door.Location) < 2f || targetCreature.Location.DistanceTo(door.Location) < 2f))
                    {
                        nearDoor = true;
                        break;
                    }
                }

                if (nearDoor && !IsDirectVisible(targetCreature))
                {
                    EndAttack();
                    return;
                }
            }

            var weapon = GetEquippedMissileWeapon();
            if (weapon == null)
            {
                EndAttack();
                return;
            }

            var ammo = weapon.IsAmmoLauncher ? GetEquippedAmmo() : weapon;
            if (ammo == null)
            {
                EndAttack();
                return;
            }

            var launcher = GetEquippedMissileLauncher();

            /*if (!IsDirectVisible(AttackTarget))
            {
                // ensure direct line of sight
                //NextAttackTime = Timers.RunningTime + 1.0f;
                SwitchToMeleeAttack();
                return;
            }*/

            // should this be called each launch?
            AttackHeight = ChooseAttackHeight();

            var dist = GetDistanceToTarget();
            //Console.WriteLine("RangeAttack: " + dist);

            if (DebugMove)
                Console.WriteLine($"[{Timers.RunningTime}] - {Name} ({Guid}) - LaunchMissile");

            var projectileSpeed = GetProjectileSpeed();

            // get z-angle for aim motion
            var aimVelocity = GetAimVelocity(AttackTarget, projectileSpeed);

            var aimLevel = GetAimLevel(aimVelocity);

            // calculate projectile spawn pos and velocity
            var localOrigin = GetProjectileSpawnOrigin(ammo.WeenieClassId, aimLevel);

            var velocity = CalculateProjectileVelocity(localOrigin, AttackTarget, projectileSpeed, out Vector3 origin, out Quaternion orientation);

            //Console.WriteLine($"Velocity: {velocity}");

            // launch animation
            var actionChain = new ActionChain();
            var launchTime = EnqueueMotion(actionChain, aimLevel);
            //Console.WriteLine("LaunchTime: " + launchTime);

            // launch projectile
            actionChain.AddAction(this, () =>
            {
                if (AttackTarget == null || IsDead || targetCreature.IsDead || targetCreature != NextSwingAttackTarget)
                {
                    EndAttack();
                    return;
                }

                // handle self-procs
                TryProcEquippedItems(this, this, true, weapon);

                var sound = GetLaunchMissileSound(weapon);
                EnqueueBroadcast(new GameMessageSound(Guid, sound, 1.0f));

                var staminaCost = GetAttackStamina(GetPowerRange());
                UpdateVitalDelta(Stamina, -staminaCost);

                if (AttackTarget != null)
                {
                    var projectile = LaunchProjectile(launcher, ammo, AttackTarget, origin, orientation, velocity);
                    UpdateAmmoAfterLaunch(ammo);
                }

                UsedRangedAttacks = true;
                MissileCombatMeleeRangeMode = false; // reset melee range mode.
            });

            // will ammo be depleted?
            /*if (ammo.StackSize == null || ammo.StackSize <= 1)
            {
                // compare monsters: lugianmontokrenegade /  sclavusse / zombielichtowerarcher
                actionChain.EnqueueChain();
                NextMoveTime = NextAttackTime = Timers.RunningTime + launchTime + MissileDelay;
                return;
            }*/

            // reload animation
            var animSpeed = GetAnimSpeed();
            var reloadTime = EnqueueMotion(actionChain, MotionCommand.Reload, animSpeed);
            //Console.WriteLine("ReloadTime: " + reloadTime);

            // reset for next projectile
            EnqueueMotion(actionChain, MotionCommand.Ready);

            var linkAnim = reloadTime > 0 ? MotionCommand.Reload : aimLevel;

            var linkTime = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, linkAnim, MotionCommand.Ready);

            if (weapon.IsThrownWeapon)
            {
                if (reloadTime > 0)
                {
                    actionChain.EnqueueChain();
                    actionChain = new ActionChain();
                }

                actionChain.AddDelaySeconds(linkTime);
            }

            //log.Info($"{Name}.Reload time: launchTime({launchTime}) + reloadTime({reloadTime}) + linkTime({linkTime})");

            actionChain.AddAction(this, () => EnqueueBroadcast(new GameMessageParentEvent(this, ammo,
                ACE.Entity.Enum.ParentLocation.RightHand, ACE.Entity.Enum.Placement.RightHandCombat)));

            actionChain.AddAction(this, () => EndAttack());

            actionChain.EnqueueChain();

            PrevAttackTime = Timers.RunningTime;

            var timeOffset = launchTime + reloadTime + linkTime;

            NextMoveTime = NextAttackTime = PrevAttackTime + timeOffset + MissileDelay;
        }

        /// <summary>
        /// Returns missile base damage from a monster attack
        /// </summary>
        public BaseDamageMod GetMissileDamage()
        {
            // FIXME: use actual projectile, instead of currently equipped ammo
            var ammo = GetMissileAmmo();

            return ammo.GetDamageMod(this);
        }

        // reset between targets?
        public int MonsterProjectile_OnCollideEnvironment_Counter;

        public void MonsterProjectile_OnCollideEnvironment()
        {
            //Console.WriteLine($"{Name}.MonsterProjectile_OnCollideEnvironment()");
            MonsterProjectile_OnCollideEnvironment_Counter++;

            // chance of switching to melee, or static counter in retail?
            /*var rng = ThreadSafeRandom.Next(1, 3);
            if (rng == 3)
                SwitchToMeleeAttack();*/

            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                if (MonsterProjectile_OnCollideEnvironment_Counter >= 3)
                {
                    MonsterProjectile_OnCollideEnvironment_Counter = 0;
                    TrySwitchToMeleeAttack();
                }
            }
            else
            {
                if (MonsterProjectile_OnCollideEnvironment_Counter > 1 && ThreadSafeRandom.Next(1, 3) != 3)
                {
                    MonsterProjectile_OnCollideEnvironment_Counter = 0;

                    var canSwitch = HasMeleeWeapon && !IsSwitchWeaponsPending;
                    int maxRoll = canSwitch ? 3 : 2;

                    var currentUnixTime = Time.GetUnixTime();

                    var roll = ThreadSafeRandom.Next(1, maxRoll);
                    switch (roll)
                    {
                        case 1:
                            if (LastEmoteTime + MaxEmoteFrequency < currentUnixTime && EmoteChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                TryEmoting();
                            if (LastWanderTime + MaxWanderFrequency < currentUnixTime && WanderChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                TryWandering(-45, 45, 2);
                            break;
                        case 2:
                            if (PathfindingEnabled && !LastRouteStartAttemptWasNullRoute)
                            {
                                if (LastEmoteTime + MaxEmoteFrequency < currentUnixTime && EmoteChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                    TryEmoting();
                                if (LastWanderTime + MaxWanderFrequency < currentUnixTime && WanderChance > ThreadSafeRandom.Next(0.0f, 1.0f))
                                    TryWandering(160, 200, 3);
                                TryRoute();
                            }
                            else
                                MissileCombatMeleeRangeMode = true;
                            break;
                        case 3: TrySwitchToMeleeAttack(); break;
                    }
                }
            }
        }

        private bool IsSwitchWeaponsPending = false;
        private bool IsSwitchingWeapons = false;
        private CombatType WeaponSwitchType = 0;

        public double LastWeaponSwitchTime = 0;
        private const double MaxSwitchWeaponFrequency = 10;

        public bool MissileCombatMeleeRangeMode = false;

        public void TrySwitchToMeleeAttack()
        {
            //Console.WriteLine("Pathfinding: TrySwitchToMeleeAttack");

            if (IsSwitchingWeapons)
                return;

            IsSwitchWeaponsPending = true;
            WeaponSwitchType = CombatType.Melee;
        }

        public void TrySwitchToMissileAttack()
        {
            //Console.WriteLine("Pathfinding: TrySwitchToMissileAttack");

            if (IsSwitchingWeapons)
                return;

            IsSwitchWeaponsPending = true;
            WeaponSwitchType = CombatType.Missile;
        }

        private void EndSwitchWeapons()
        {
            //Console.WriteLine("Pathfinding: EndSwitchWeapons");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsSwitchWeaponsPending = false;
            IsSwitchingWeapons = false;

            WeaponSwitchType = 0;
        }

        private void SwitchWeapons()
        {
            switch (WeaponSwitchType)
            {
                case CombatType.Melee:
                    SwitchToMeleeAttack();
                    break;
                case CombatType.Missile:
                    SwitchToMissileAttack();
                    break;
                default:
                    EndSwitchWeapons();
                    break;
            }
        }

        private void SwitchToMeleeAttack()
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.SwitchToMeleeAttack()");

            // 24139 - Invisible Assailant never switches to melee?
            if (AiAllowedCombatStyle == CombatStyle.StubbornMissile || Visibility)
            {
                EndSwitchWeapons();
                return;
            }

            if (IsSwitchingWeapons)
                return;

            if (!MoveReady())
                return;

            //Console.WriteLine("Pathfinding: SwitchToMeleeAttack");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsSwitchWeaponsPending = false;
            IsSwitchingWeapons = true;

            if (IsDead || !HasMeleeWeapon)
            {
                EndSwitchWeapons();
                return;
            }

            CurrentAttackType = null;

            var weapon = GetEquippedMissileWeapon();
            var ammo = GetEquippedAmmo();

            if (weapon == null && ammo == null)
            {
                EndSwitchWeapons();
                return;
            }

            var actionChain = new ActionChain();

            EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)CurrentMotionState.Stance);

            EnqueueMotion_Force(actionChain, MotionStance.HandCombat, MotionCommand.Ready, MotionCommand.NonCombat);

            actionChain.AddAction(this, () =>
            {
                if (IsDead)
                {
                    EndSwitchWeapons();
                    return;
                }

                if (weapon != null)
                {
                    TryUnwieldObjectWithBroadcasting(weapon.Guid, out _, out _);
                    if (!TryAddToInventory(weapon, 0, false, false))
                        weapon.Destroy();
                }

                if (ammo != null)
                {
                    TryUnwieldObjectWithBroadcasting(ammo.Guid, out _, out _);
                    if (!TryAddToInventory(weapon, 0, false, false))
                        weapon.Destroy();
                }

                EquipInventoryItems(true, true, false, false);

                var innerChain = new ActionChain();

                EnqueueMotion_Force(innerChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)CurrentMotionState.Stance);

                innerChain.AddAction(this, () =>
                {
                    if (IsDead)
                    {
                        EndSwitchWeapons();
                        return;
                    }

                    //DoAttackStance();

                    // inlined DoAttackStance() / slightly modified -- do not rely on SetCombatMode() for stance swapping time in 1 action,
                    // as it doesn't support that anymore

                    var newStanceTime = SetCombatMode(CombatMode.Melee);

                    NextMoveTime = NextAttackTime = Timers.RunningTime + newStanceTime;

                    PrevAttackTime = NextMoveTime - (AiUseMagicDelay ?? 3.0f);

                    PhysicsObj.StartTimer();

                    // end inline

                    LastWeaponSwitchTime = Time.GetUnixTime();

                    // this is an unfortunate hack to fix the following scenario:

                    // since this function can be called at any point in time now,
                    // including when LaunchMissile -> EnqueueMotion is in the middle of an action queue,
                    // CurrentMotionState.Stance can get reset to the previous combat stance if that happens

                    var newStance = CurrentMotionState.Stance;

                    var swapChain = new ActionChain();
                    swapChain.AddDelaySeconds(2.0f);
                    swapChain.AddAction(this, () => CurrentMotionState.Stance = newStance);
                    swapChain.EnqueueChain();

                });
                innerChain.EnqueueChain();

                TryRoute();

                EndSwitchWeapons();
            });
            actionChain.EnqueueChain();
        }

        private void SwitchToMissileAttack()
        {
            if (DebugMove)
                Console.WriteLine($"{Name}.SwitchToMissileAttack()");

            if (IsSwitchingWeapons)
                return;

            if (!MoveReady())
                return;

            //Console.WriteLine("Pathfinding: SwitchToMissileAttack");

            if (HasPendingMovement)
                CancelMoveTo(WeenieError.ObjectGone);

            IsSwitchWeaponsPending = false;
            IsSwitchingWeapons = true;

            if (IsDead || !HasRangedWeapon)
            {
                EndSwitchWeapons();
                return;
            }

            CurrentAttackType = null;

            var weapon = GetEquippedMeleeWeapon();
            var shield = GetEquippedShield();

            var actionChain = new ActionChain();

            EnqueueMotion_Force(actionChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)CurrentMotionState.Stance);

            EnqueueMotion_Force(actionChain, MotionStance.HandCombat, MotionCommand.Ready, MotionCommand.NonCombat);

            actionChain.AddAction(this, () =>
            {
                if (IsDead)
                {
                    EndSwitchWeapons();
                    return;
                }

                if (weapon != null)
                {
                    TryUnwieldObjectWithBroadcasting(weapon.Guid, out _, out _);
                    if (!TryAddToInventory(weapon, 0, false, false))
                        weapon.Destroy();
                }

                if (shield != null)
                {
                    TryUnwieldObjectWithBroadcasting(shield.Guid, out _, out _);
                    if (!TryAddToInventory(shield, 0, false, false))
                        shield.Destroy();
                }

                EquipInventoryItems(true, false, true, false);

                var innerChain = new ActionChain();

                EnqueueMotion_Force(innerChain, MotionStance.NonCombat, MotionCommand.Ready, (MotionCommand)CurrentMotionState.Stance);

                innerChain.AddAction(this, () =>
                {
                    if (IsDead)
                    {
                        EndSwitchWeapons();
                        return;
                    }

                    //DoAttackStance();

                    // inlined DoAttackStance() / slightly modified -- do not rely on SetCombatMode() for stance swapping time in 1 action,
                    // as it doesn't support that anymore

                    var newStanceTime = SetCombatMode(CombatMode.Missile);

                    NextMoveTime = NextAttackTime = Timers.RunningTime + newStanceTime;

                    PrevAttackTime = NextMoveTime - (AiUseMagicDelay ?? 3.0f);

                    PhysicsObj.StartTimer();

                    // end inline

                    LastWeaponSwitchTime = Time.GetUnixTime();

                    // this is an unfortunate hack to fix the following scenario:

                    // since this function can be called at any point in time now,
                    // including when LaunchMissile -> EnqueueMotion is in the middle of an action queue,
                    // CurrentMotionState.Stance can get reset to the previous combat stance if that happens

                    var newStance = CurrentMotionState.Stance;

                    var swapChain = new ActionChain();
                    swapChain.AddDelaySeconds(2.0f);
                    swapChain.AddAction(this, () => CurrentMotionState.Stance = newStance);
                    swapChain.EnqueueChain();

                });
                innerChain.EnqueueChain();

                TryRoute();

                EndSwitchWeapons();
            });
            actionChain.EnqueueChain();
        }
    }
}
