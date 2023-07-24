using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.Entity.AnimationHooks;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Physics.Animation;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        /// <summary>
        /// The maximum delay in seconds between the end of a monster's previous attack,
        /// and their next attack
        /// </summary>
        public double? PowerupTime
        {
            get => GetProperty(PropertyFloat.PowerupTime);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.PowerupTime); else SetProperty(PropertyFloat.PowerupTime, value.Value); }
        }


        /// <summary>
        /// Returns TRUE if creature can perform a melee attack
        /// </summary>
        public bool MeleeReady()
        {
            return IsMeleeRange() && Timers.RunningTime >= NextAttackTime;
        }

        /// <summary>
        /// Performs a melee attack for the monster
        /// </summary>
        /// <returns>The length in seconds for the attack animation</returns>
        public float MeleeAttack()
        {
            var target = AttackTarget as Creature;
            var targetPlayer = AttackTarget as Player;
            var targetPet = AttackTarget as CombatPet;
            var combatPet = this as CombatPet;

            if (target == null || !target.IsAlive)
            {
                FindNextTarget();
                return 0.0f;
            }

            if (CurrentMotionState.Stance == MotionStance.NonCombat)
                DoAttackStance();

            var weapon = GetEquippedWeapon();

            // select combat maneuver
            var motionCommand = GetCombatManeuver();
            if (motionCommand == null)
                return 0.0f;

            DoSwingMotion(AttackTarget, motionCommand.Value, out float animLength, out var attackFrames);

            if (!AiImmobile)
                PhysicsObj.stick_to_object(AttackTarget.PhysicsObj.ID);

            var extraStaminaUsage = 0;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && weapon == null)
                extraStaminaUsage = (int)(Strength.Base / 75f); // Make unarmed creatures have a non-trivial stamina cost.

            var staminaCost = GetAttackStamina(GetPowerRange()) + extraStaminaUsage;
            UpdateVitalDelta(Stamina, -staminaCost);

            var numStrikes = attackFrames.Count;

            var actionChain = new ActionChain();

            // handle self-procs
            TryProcEquippedItems(this, this, true, weapon);

            var prevTime = 0.0f;
            bool targetProc = false;

            for (var i = 0; i < numStrikes; i++)
            {
                actionChain.AddDelaySeconds(attackFrames[i].time * animLength - prevTime);
                prevTime = attackFrames[i].time * animLength;

                actionChain.AddAction(this, () =>
                {
                    if (AttackTarget == null || IsDead || target.IsDead) return;

                    //if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && !IsDirectVisible(target))
                    //    return;

                    if (WeenieType == WeenieType.GamePiece)
                    {
                        target.TakeDamage(this, DamageType.Slash, target.Health.Current);
                        (this as GamePiece).OnDealtDamage();
                        return;
                    }

                    var damageEvent = DamageEvent.CalculateDamage(this, target, weapon, motionCommand, attackFrames[0].attackHook);

                    target.OnAttackReceived(this, CombatType.Melee, damageEvent.IsCritical, damageEvent.Evaded);

                    //var damage = CalculateDamage(ref damageType, maneuver, bodyPart, ref critical, ref shieldMod);

                    if (damageEvent.HasDamage)
                    {
                        if (targetPlayer != null)
                        {
                            // this is a player taking damage
                            targetPlayer.TakeDamage(this, damageEvent);

                            if (damageEvent.ShieldMod != 1.0f)
                            {
                                var shieldSkill = targetPlayer.GetCreatureSkill(Skill.Shield);
                                Proficiency.OnSuccessUse(targetPlayer, shieldSkill, damageEvent.EffectiveAttackSkill);
                            }

                            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                            {
                                if (damageEvent.Armor != null && damageEvent.Armor.Count > 0)
                                {
                                    var armorSkill = targetPlayer.GetCreatureSkill(Skill.Armor);
                                    Proficiency.OnSuccessUse(targetPlayer, armorSkill, damageEvent.EffectiveAttackSkill);
                                }
                            }

                            // handle Dirty Fighting
                            if (GetCreatureSkill(Skill.DirtyFighting).AdvancementClass >= SkillAdvancementClass.Trained)
                                FightDirty(targetPlayer, damageEvent.Weapon);
                        }
                        else if (combatPet != null || targetPet != null || Faction1Bits != null || target.Faction1Bits != null || PotentialFoe(target))
                        {
                            // combat pet inflicting or receiving damage
                            //Console.WriteLine($"{target.Name} taking {Math.Round(damage)} {damageType} damage from {Name}");
                            target.TakeDamage(this, damageEvent.DamageType, damageEvent.Damage);

                            EmitSplatter(target, damageEvent.Damage);

                            // handle Dirty Fighting
                            if (GetCreatureSkill(Skill.DirtyFighting).AdvancementClass >= SkillAdvancementClass.Trained)
                                FightDirty(target, damageEvent.Weapon);
                        }

                        // handle target procs
                        if (!targetProc)
                        {
                            TryProcEquippedItems(this, target, false, weapon);
                            targetProc = true;
                        }
                    }
                    else
                        target.OnEvade(this, CombatType.Melee);

                    if (combatPet != null)
                        combatPet.PetOnAttackMonster(target);
                    else if (targetPlayer == null)
                        MonsterOnAttackMonster(target);
                });
            }
            actionChain.EnqueueChain();

            PrevAttackTime = Timers.RunningTime;
            NextMoveTime = PrevAttackTime + animLength + 0.5f;

            var meleeDelay = ThreadSafeRandom.Next(0.0f, (float)(PowerupTime ?? 1.0f));

            NextAttackTime = PrevAttackTime + animLength + meleeDelay;

            return animLength;
        }

        /// <summary>
        /// Selects a random combat maneuver for a monster's next attack
        /// </summary>
        public MotionCommand? GetCombatManeuver()
        {
            // similar to Player.GetSwingAnimation(), more logging

            if (CombatTable == null)
            {
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - CombatTable is null");
                return null;
            }

            //ShowCombatTable();

            var motionTable = DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.MotionTable>(MotionTableId);
            if (motionTable == null)
            {
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - motionTable is null");
                return null;
            }

            if (!CombatTable.Stances.TryGetValue(CurrentMotionState.Stance, out var stanceManeuvers))
            {
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                return null;
            }

            var stanceKey = (uint)CurrentMotionState.Stance << 16 | ((uint)MotionCommand.Ready & 0xFFFFFF);
            motionTable.Links.TryGetValue(stanceKey, out var motions);
            if (motions == null)
            {
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find stance {CurrentMotionState.Stance} in MotionTable {MotionTableId:X8}");
                return null;
            }

            // choose a random attack height?
            // apparently this might have been based on monster Z vs. player Z?

            // 28659 - Uber Penguin (CMT 30000040) doesn't have High attack height
            // do a more thorough investigation for this...
            var startHeight = stanceManeuvers.Table.Count == 3 ? 1 : 2;

            AttackHeight = (AttackHeight)ThreadSafeRandom.Next(startHeight, 3);

            if (!stanceManeuvers.Table.TryGetValue(AttackHeight.Value, out var attackTypes))
            {
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find attack height {AttackHeight} for stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                return null;
            }

            if (IsDualWieldAttack)
                DualWieldAlternate = !DualWieldAlternate;

            var offhand = IsDualWieldAttack && !DualWieldAlternate;

            var weapon = GetEquippedMeleeWeapon();

            // monsters supposedly always used 0.5 PowerLevel according to anon docs,
            // which translates into a 1.0 PowerMod

            if (weapon != null)
            {
                AttackType = weapon.GetAttackType(CurrentMotionState.Stance, 0.5f, offhand);
            }
            else
            {
                if (AttackHeight != ACE.Entity.Enum.AttackHeight.Low)
                    AttackType = AttackType.Punch;
                else
                    AttackType = AttackType.Kick;
            }

            if (!attackTypes.Table.TryGetValue(AttackType, out var maneuvers) || maneuvers.Count == 0)
            {
                if (AttackType == AttackType.Punch && AttackHeight == ACE.Entity.Enum.AttackHeight.Low || AttackType == AttackType.Kick)
                {
                    // 27864 - Mosswart Muckstalker w/ a katar, low punch not found in CMT, but contains kick
                    // might need additional research
                    AttackType = AttackType == AttackType.Punch ? AttackType.Kick : AttackType.Punch;

                    if (!attackTypes.Table.TryGetValue(AttackType, out maneuvers) || maneuvers.Count == 0)
                    {
                        log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find attack type Kick or Punch for attack height {AttackHeight} and stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                        return null;
                    }
                }
                else if (AttackType.IsMultiStrike())
                {
                    var reduced = AttackType.ReduceMultiStrike();

                    if (!attackTypes.Table.TryGetValue(reduced, out maneuvers) || maneuvers.Count == 0)
                    {
                        log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find attack type {reduced} for attack height {AttackHeight} and stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                        return null;
                    }
                    //else
                    //log.Info($"{Name} ({Guid}).GetCombatManeuver() - successfully reduced attack type {AttackType} to {reduced} for attack height {AttackHeight} and stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                }
                else
                {
                    log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find attack type {AttackType} for attack height {AttackHeight} and stance {CurrentMotionState.Stance} in CMT {CombatTableDID:X8}");
                    return null;
                }
            }

            var motionCommand = maneuvers[0];

            if (maneuvers.Count > 1)
            {
                // only used for special attacks?

                // note that with rolling for AttackHeight first,
                // for a CMT with high, med, med-special, and low
                // the chance of rolling the special attack is reduced from 1/4 to 1/6 -- investigate

                var rng = ThreadSafeRandom.Next(0, maneuvers.Count - 1);
                motionCommand = maneuvers[rng];
            }

            // ensure this motionCommand exists in monster's motion table
            if (!motions.ContainsKey((uint)motionCommand))
            {
                // for some reason, the combat maneuvers table can return stance motions that don't exist in the motion table
                // ie. skeletons (combat maneuvers table 0x30000000, motion table 0x09000025)
                // for sword combat, they have double and triple strikes (dagger / two-handed only?)
                if (motionCommand.IsMultiStrike())
                {
                    var singleStrike = motionCommand.ReduceMultiStrike();

                    if (motions.ContainsKey((uint)singleStrike))
                    {
                        //log.Info($"{Name} ({Guid}).GetCombatManeuver() - successfully reduced {motionCommand} to {singleStrike}");
                        return singleStrike;
                    }
                }
                else if (motionCommand.IsSubsequent())
                {
                    var firstCommand = motionCommand.ReduceSubsequent();

                    if (motions.ContainsKey((uint)firstCommand))
                    {
                        //log.Info($"{Name} ({Guid}).GetCombatManeuver() - successfully reduced {motionCommand} to {firstCommand}");
                        return firstCommand;
                    }
                }
                log.Error($"{Name} ({Guid}).GetCombatManeuver() - couldn't find {motionCommand} in MotionTable {MotionTableId:X8}");
                return null;
            }

            //Console.WriteLine(motionCommand);

            return motionCommand;
        }

        /// <summary>
        /// Shows debug info for this monster's combat maneuvers table
        /// </summary>
        public void ShowCombatTable()
        {
            Console.WriteLine($"CombatManeuverTable ID: {CombatTable.Id:X8}");
            CombatTable.ShowCombatTable();

            /*for (var i = 0; i < CombatTable.CMT.Count; i++)
            {
                var maneuver = CombatTable.CMT[i];
                Console.WriteLine($"{i} - {maneuver.Style} - {maneuver.Motion} - {maneuver.AttackHeight}");
            }*/
        }

        private static readonly List<(float time, AttackHook attackHook)> defaultAttackFrames = new List<(float time, AttackHook attackHook)>() { ( 1.0f / 3.0f, null ) };

        private static readonly ConcurrentDictionary<AttackFrameParams, bool> missingAttackFrames = new ConcurrentDictionary<AttackFrameParams, bool>();

        private bool moveBit;

        /// <summary>
        /// Perform the melee attack swing animation
        /// </summary>
        public void DoSwingMotion(WorldObject target, MotionCommand motionCommand, out float animLength, out List<(float time, AttackHook attackHook)> attackFrames)
        {
            if (!moveBit)
            {
                SendUpdatePosition(true);
                moveBit = true;
            }

            //Console.WriteLine($"{maneuver.Style} - {maneuver.Motion} - {maneuver.AttackHeight}");

            var baseSpeed = GetAnimSpeed();
            var animSpeedMod = IsDualWieldAttack ? 1.2f : 1.0f;     // dual wield swing animation 20% faster
            var animSpeed = baseSpeed * animSpeedMod;

            animLength = MotionTable.GetAnimationLength(MotionTableId, CurrentMotionState.Stance, motionCommand, animSpeed);

            attackFrames = MotionTable.GetAttackFrames(MotionTableId, CurrentMotionState.Stance, motionCommand);

            if (attackFrames.Count == 0)
            {
                var attackFrameParams = new AttackFrameParams(MotionTableId, CurrentMotionState.Stance, motionCommand);
                if (!missingAttackFrames.ContainsKey(attackFrameParams))
                {
                    // only show warning message once for each combo
                    log.Warn($"{Name} ({Guid}) - no attack frames for MotionTable {MotionTableId:X8}, {CurrentMotionState.Stance}, {motionCommand}, using defaults");
                    missingAttackFrames.TryAdd(attackFrameParams, true);
                }
                attackFrames = defaultAttackFrames;
            }

            var motion = new Motion(this, motionCommand, animSpeed);
            motion.MotionState.TurnSpeed = 2.25f;

            if (!AiImmobile)
                motion.MotionFlags |= MotionFlags.StickToObject;

            motion.TargetGuid = target.Guid;
            CurrentMotionState = motion;

            EnqueueBroadcastMotion(motion);
        }

        /// <summary>
        /// Returns base damage range for next monster attack
        /// </summary>
        public BaseDamageMod GetBaseDamage(PropertiesBodyPart attackPart)
        {
            if (CurrentAttack == CombatType.Missile && GetMissileAmmo() != null)
                return GetMissileDamage();

            // use weapon damage for every attack?
            var weapon = GetEquippedMeleeWeapon();
            if (weapon != null)
            {
                var weaponDamage = weapon.GetDamageMod(this);
                //Console.WriteLine($"{Name} using weapon damage: {weaponDamage}");
                return weaponDamage;
            }

            var maxDamage = attackPart.DVal;
            var variance = attackPart.DVar;

            var baseDamage = new BaseDamage(maxDamage, variance);
            return new BaseDamageMod(baseDamage);
        }

        /// <summary>
        /// Returns the creature armor for a body part
        /// </summary>
        public List<WorldObject> GetArmorLayers(Player target, BodyPart bodyPart)
        {
            //Console.WriteLine("BodyPart: " + bodyPart);
            //Console.WriteLine("===");

            var coverageMask = BodyParts.GetCoverageMask(bodyPart);

            var equipped = target.EquippedObjects.Values.Where(e => e is Clothing && (e.ClothingPriority & coverageMask) != 0).ToList();

            return equipped;
        }

        /// <summary>
        /// Returns the percent of damage absorbed by layered armor + clothing
        /// </summary>
        /// <param name="armors">The list of armor/clothing covering the targeted body part</param>
        public float GetArmorMod(Creature defender, DamageType damageType, List<WorldObject> armors, WorldObject weapon, float armorRendingMod = 1.0f, bool isPvP = false)
        {
            var ignoreMagicArmor =  (weapon?.IgnoreMagicArmor ?? false)  || IgnoreMagicArmor;
            var ignoreMagicResist = (weapon?.IgnoreMagicResist ?? false) || IgnoreMagicResist;

            var effectiveAL = 0.0f;

            foreach (var armor in armors)
                effectiveAL += defender.GetArmorMod(armor, damageType, ignoreMagicArmor);

            // life spells
            // additive: armor/imperil
            int bodyArmorMod;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                bodyArmorMod = defender.EnchantmentManager.GetBodyArmorMod(true); // Do not take into account armor debuffs yet.
            else
            {
                bodyArmorMod = defender.EnchantmentManager.GetBodyArmorMod();
                if (ignoreMagicResist)
                    bodyArmorMod = IgnoreMagicResistScaled(bodyArmorMod);
            }

            // handle armor rending mod here?
            //if (bodyArmorMod > 0)
            //bodyArmorMod *= armorRendingMod;

            //Console.WriteLine("==");
            //Console.WriteLine("Armor Self: " + bodyArmorMod);

            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                effectiveAL += bodyArmorMod;
            else
            {
                if (bodyArmorMod > effectiveAL)
                    effectiveAL = bodyArmorMod; // Body armor doesn't stack with equipment armor, use whichever is highest.

                if (!ignoreMagicResist)
                    effectiveAL += defender.EnchantmentManager.GetBodyArmorMod(false); // Take into account armor debuffs now, but only if weapon isn't hollow.
            }

            // Armor Rending reduces physical armor too?
            if (effectiveAL > 0)
                effectiveAL *= armorRendingMod;

            if (isPvP)
                effectiveAL *= (float)PropertyManager.GetInterpolatedDouble(Level ?? 1, "pvp_dmg_mod_low_armor_level", "pvp_dmg_mod_high_armor_level", "pvp_dmg_mod_low_level", "pvp_dmg_mod_high_level");

            var armorMod = SkillFormula.CalcArmorMod(effectiveAL);

            //Console.WriteLine("Total AL: " + effectiveAL);
            //Console.WriteLine("Armor mod: " + armorMod);

            return armorMod;
        }

        public float IgnoreMagicArmorScaled(float enchantments)
        {
            if (!(this is Player))
                return 0.0f;

            var scalar = PropertyManager.GetDouble("ignore_magic_armor_pvp_scalar").Item;

            if (scalar != 1.0)
                return (float)(enchantments * (1.0 - scalar));
            else
                return 0.0f;
        }

        public int IgnoreMagicResistScaled(int enchantments)
        {
            if (!(this is Player))
                return 0;

            var scalar = PropertyManager.GetDouble("ignore_magic_resist_pvp_scalar").Item;

            if (scalar != 1.0)
                return (int)Math.Round(enchantments * (1.0 - scalar));
            else
                return 0;
        }

        public float GetSkillModifiedShieldLevel(float shieldLevel)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
                return shieldLevel;

            if (shieldLevel == 0)
                return 0;

            var player = this as Player;
            if (player != null) // Creatures always have full shield level.
            {
                // SL cap:
                // EoR:
                // Trained / untrained: 1/2 shield skill
                // Spec: shield skill
                // CustomDM:
                // SL cap:
                // Spec / Trained / untrained: shield skill

                // SL cap is applied *after* item enchantments
                var shieldSkill = GetCreatureSkill(Skill.Shield);
                var shieldCap = shieldSkill.Current;
                if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                {
                    if (shieldSkill.AdvancementClass != SkillAdvancementClass.Specialized)
                        shieldCap = (uint)Math.Round(shieldCap / 2.0f);
                }
                else
                {
                    if (shieldSkill.Current > shieldLevel)
                        shieldLevel += (shieldSkill.Current - shieldLevel) / 5;
                }

                return Math.Min(shieldLevel, shieldCap);
            }
            else
                return shieldLevel;
        }

        public float GetSkillModifiedArmorLevel(float armorLevel)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return armorLevel;

            if (armorLevel == 0)
                return 0;

            var player = this as Player;
            if (player != null) // Creatures always have full armor level.
            {
                // AL cap:
                // Spec / Trained / untrained: armor skill

                // AL cap is applied *after* item enchantments
                var armorSkill = GetCreatureSkill(Skill.Armor);
                var armorCap = armorSkill.Current;

                if (armorSkill.Current > armorLevel)
                    armorLevel += (armorSkill.Current - armorLevel) / 5;

                return Math.Min(armorLevel, armorCap);
            }
            else
                return armorLevel;
        }

        /// <summary>
        /// Returns the effective AL for 1 piece of armor/clothing
        /// </summary>
        /// <param name="armor">A piece of armor or clothing</param>
        public float GetArmorMod(WorldObject armor, DamageType damageType, bool ignoreMagicArmor)
        {
            // get base armor/resistance level
            var baseArmor = armor.GetProperty(PropertyInt.ArmorLevel) ?? 0;
            var armorType = armor.GetProperty(PropertyInt.ArmorType) ?? 0;
            var resistance = GetResistance(armor, damageType);

            /*Console.WriteLine(armor.Name);
            Console.WriteLine("--");
            Console.WriteLine("Base AL: " + baseArmor);
            Console.WriteLine("Base RL: " + resistance);*/

            // armor level additives
            var armorMod = armor.EnchantmentManager.GetArmorMod();

            if (ignoreMagicArmor)
                armorMod = (int)Math.Round(IgnoreMagicArmorScaled(armorMod));

            // Console.WriteLine("Impen: " + armorMod);
            var effectiveAL = baseArmor + armorMod;

            // resistance additives
            var armorBane = armor.EnchantmentManager.GetArmorModVsType(damageType);

            if (ignoreMagicArmor)
                armorBane = IgnoreMagicArmorScaled(armorBane);

            // Console.WriteLine("Bane: " + armorBane);
            var effectiveRL = (float)(resistance + armorBane);

            // resistance clamp
            effectiveRL = Math.Clamp(effectiveRL, -2.0f, 2.0f);

            // TODO: could brittlemail / lures send a piece of armor or clothing's AL into the negatives?
            //if (effectiveAL < 0 && effectiveRL != 0)
            //effectiveRL = 1.0f / effectiveRL;

            /*Console.WriteLine("Effective AL: " + effectiveAL);
            Console.WriteLine("Effective RL: " + effectiveRL);
            Console.WriteLine();*/

            return GetSkillModifiedArmorLevel(effectiveAL) * effectiveRL;
        }

        /// <summary>
        /// Returns the natural resistance to DamageType for a piece of armor
        /// </summary>
        public double GetResistance(WorldObject armor, DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Slash:
                    return armor.GetProperty(PropertyFloat.ArmorModVsSlash) ?? 0.0;
                case DamageType.Pierce:
                    return armor.GetProperty(PropertyFloat.ArmorModVsPierce) ?? 0.0;
                case DamageType.Bludgeon:
                    return armor.GetProperty(PropertyFloat.ArmorModVsBludgeon) ?? 0.0;
                case DamageType.Fire:
                    return armor.GetProperty(PropertyFloat.ArmorModVsFire) ?? 0.0;
                case DamageType.Cold:
                    return armor.GetProperty(PropertyFloat.ArmorModVsCold) ?? 0.0;
                case DamageType.Acid:
                    return armor.GetProperty(PropertyFloat.ArmorModVsAcid) ?? 0.0;
                case DamageType.Electric:
                    return armor.GetProperty(PropertyFloat.ArmorModVsElectric) ?? 0.0;
                case DamageType.Nether:
                    return armor.GetProperty(PropertyFloat.ArmorModVsNether) ?? 0.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Displays all of the natural resistances for a piece of armor
        /// </summary>
        public void ShowResistance(WorldObject armor)
        {
            Console.WriteLine("Resistance:");
            Console.WriteLine("Slashing: " + armor.GetProperty(PropertyFloat.ArmorModVsSlash));
            Console.WriteLine("Piercing: " + armor.GetProperty(PropertyFloat.ArmorModVsPierce));
            Console.WriteLine("Bludgeoning: " + armor.GetProperty(PropertyFloat.ArmorModVsBludgeon));
            Console.WriteLine("Fire: " + armor.GetProperty(PropertyFloat.ArmorModVsFire));
            Console.WriteLine("Ice: " + armor.GetProperty(PropertyFloat.ArmorModVsCold));
            Console.WriteLine("Acid: " + armor.GetProperty(PropertyFloat.ArmorModVsAcid));
            Console.WriteLine("Lightning: " + armor.GetProperty(PropertyFloat.ArmorModVsElectric));
            Console.WriteLine("Nether: " + armor.GetProperty(PropertyFloat.ArmorModVsNether));
        }

        /// <summary>
        /// Returns the power range for the current melee attack
        /// </summary>
        public virtual PowerAccuracy GetPowerRange()
        {
            return PowerAccuracy.Low; // always low for monsters?
        }

        /// <summary>
        /// Returns the monster body part performing the next attack
        /// </summary>
        public KeyValuePair<CombatBodyPart, PropertiesBodyPart> GetAttackPart(MotionCommand motionCommand, AttackHook attackHook)
        {
            List<KeyValuePair<CombatBodyPart, PropertiesBodyPart>> parts = null;

            // todo: speed up key lookup?
            if (attackHook != null)
            {
                parts = Biota.PropertiesBodyPart.Where(b => (uint)b.Key == attackHook.AttackCone.PartIndex).ToList();
            }
            else if (motionCommand >= MotionCommand.SpecialAttack1 && motionCommand <= MotionCommand.SpecialAttack3)
            {
                //parts = Biota.BiotaPropertiesBodyPart.Where(b => b.DVal != 0 && b.BH == 0).ToList();
                parts = Biota.PropertiesBodyPart.Where(b => b.Key == CombatBodyPart.Breath).ToList(); // always use Breath?
            }

            // added parts.Count check for monsters wielding weapons -- should we be getting a body part here?
            if (parts == null || parts.Count == 0)
                //parts = Biota.BiotaPropertiesBodyPart.Where(b => b.DVal != 0 && b.BH != 0).ToList();
                parts = Biota.PropertiesBodyPart.Where(b => b.Value.DVal != 0 && b.Key != CombatBodyPart.Breath).ToList();

            if (parts.Count == 0)
            {
                log.Warn($"{Name} ({Guid}.GetAttackPart({motionCommand}) failed");
                log.Warn($"CombatTable: {CombatTableDID:X8}, MotionTable: {MotionTableId:X8}, CurrentStance: {CurrentMotionState.Stance}, AttackHeight: {AttackHeight}, AttackType: {AttackType}");
                return new KeyValuePair<CombatBodyPart, PropertiesBodyPart>();
            }

            var part = parts[ThreadSafeRandom.Next(0, parts.Count - 1)];

            return part;
        }
    }
}
