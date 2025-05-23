using System;

using ACE.Common;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories.Tables;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    public class Healer : WorldObject
    {
        // TODO: change structure / maxstructure to int,
        // cast to ushort at network level
        public ushort? UsesLeft
        {
            get => Structure;
            set => Structure = value;
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Healer(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Healer(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.Healer;
        }

        public override void HandleActionUseOnTarget(Player healer, WorldObject target)
        {
            if (!healer.VerifyGameplayMode(this))
            {
                healer.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(healer.Session, $"This item cannot be used, invalid gameplay mode!"));
                healer.SendUseDoneEvent(WeenieError.YouCannotUseThatItem);
                return;
            }

            if (healer.GetCreatureSkill(Skill.Healing).AdvancementClass < SkillAdvancementClass.Trained)
            {
                healer.SendUseDoneEvent(WeenieError.YouArentTrainedInHealing);
                return;
            }

            if (healer.IsBusy || healer.Teleporting || healer.suicideInProgress)
            {
                healer.SendUseDoneEvent(WeenieError.YoureTooBusy);
                return;
            }

            if (!(target is Player targetPlayer) || targetPlayer.Teleporting)
            {
                healer.SendUseDoneEvent(WeenieError.YouCantHealThat);
                return;
            }

            if (healer.IsJumping)
            {
                healer.SendUseDoneEvent(WeenieError.YouCantDoThatWhileInTheAir);
                return;
            }

            // ensure same PKType, although PK and PKLite players can heal NPKs:
            // https://asheron.fandom.com/wiki/Player_Killer
            // https://asheron.fandom.com/wiki/Player_Killer_Lite

            if (targetPlayer.PlayerKillerStatus != healer.PlayerKillerStatus && targetPlayer.PlayerKillerStatus != PlayerKillerStatus.NPK)
            {
                healer.SendWeenieErrorWithString(WeenieErrorWithString.YouFailToAffect_NotSamePKType, targetPlayer.Name);
                healer.SendUseDoneEvent();
                return;
            }

            // ensure target player vital < MaxValue
            var vital = targetPlayer.GetCreatureVital(BoosterEnum);

            if (vital.Current == vital.MaxValue)
            {
                switch (vital.Vital)
                {
                    case PropertyAttribute2nd.MaxHealth:
                        healer.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(healer.Session, WeenieErrorWithString._IsAtFullHealth, target.Name));
                        break;
                    case PropertyAttribute2nd.MaxStamina:
                        healer.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(healer.Session, $"{target.Name} is already at full stamina!"));
                        break;
                    case PropertyAttribute2nd.MaxMana:
                        healer.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(healer.Session, $"{target.Name} is already at full mana!"));
                        break;
                }
                healer.SendUseDoneEvent();
                return;
            }

            /*if (!healer.Equals(targetPlayer))
            {
                // perform moveto
                healer.CreateMoveToChain(target, (success) => DoHealMotion(healer, targetPlayer, success));
            }
            else
                DoHealMotion(healer, targetPlayer, true);*/

            // MoveTo is now handled in base Player_Use
            DoHealMotion(healer, targetPlayer, true);
        }

        public const float Healing_MaxMove = 5.0f;

        public void DoHealMotion(Player healer, Player target, bool success)
        {
            bool isUseDoneRequired = ItemUseable.Value.GetTargetFlags() != Usable.Undef;

            if (!success || target.IsDead || target.Teleporting || target.suicideInProgress)
            {
                healer.SendUseDoneEvent();
                return;
            }

            healer.IsBusy = true;

            var motionCommand = healer.Equals(target) ? MotionCommand.SkillHealSelf : MotionCommand.SkillHealOther;

            var motion = new Motion(healer, motionCommand);
            var currentStance = healer.CurrentMotionState.Stance;
            var animLength = MotionTable.GetAnimationLength(healer.MotionTableId, currentStance, motionCommand);

            var startPos = new Physics.Common.Position(healer.PhysicsObj.Position);

            var vital = target.GetCreatureVital(BoosterEnum);

            var missingVital = vital.Missing;

            var actionChain = new ActionChain();
            //actionChain.AddAction(healer, () => healer.EnqueueBroadcastMotion(motion));
            actionChain.AddAction(healer, () => healer.SendMotionAsCommands(motionCommand, currentStance));
            actionChain.AddDelaySeconds(animLength);
            actionChain.AddAction(healer, () =>
            {
                // check healing move distance cap
                var endPos = new Physics.Common.Position(healer.PhysicsObj.Position);
                var dist = startPos.Distance(endPos);

                //Console.WriteLine($"Dist: {dist}");

                // only PKs affected by these caps?
                if (dist < Healing_MaxMove || healer.PlayerKillerStatus == PlayerKillerStatus.NPK)
                    DoHealing(healer, target, missingVital);
                else
                    healer.Session.Network.EnqueueSend(new GameMessageSystemChat("Your movement disrupted healing!", ChatMessageType.Broadcast));

                healer.IsBusy = false;

                healer.SendUseDoneEvent();
            });

            healer.EnqueueMotion(actionChain, MotionCommand.Ready);

            actionChain.EnqueueChain();

            healer.NextUseTime = DateTime.UtcNow.AddSeconds(animLength);
        }

        private double NextAlchemyProcAttemptTime = 0;
        private static double AlchemyProcAttemptInterval = 120;
        public void TryProcAlchemyHoT(Player healer, Creature target, int difficulty, int healAmount, PropertyAttribute2nd vital)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (target.IsDead)
                return;

            var isSelfTarget = healer == target;

            var currentTime = Time.GetUnixTime();
            if (NextAlchemyProcAttemptTime > currentTime)
                return;

            var skill = healer.GetCreatureSkill(Skill.Alchemy);
            if (skill.AdvancementClass >= SkillAdvancementClass.Trained)
            {
                var skillCheck = SkillCheck.GetSkillChance((int)skill.Current, difficulty);

                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
                if (rng >= skillCheck)
                    return;

                var vitalType = DamageType.Undef;
                switch (vital)
                {
                    case PropertyAttribute2nd.MaxHealth:
                        vitalType = DamageType.Health;
                        break;
                    case PropertyAttribute2nd.MaxStamina:
                        vitalType = DamageType.Stamina;
                        break;
                    case PropertyAttribute2nd.MaxMana:
                        vitalType = DamageType.Mana;
                        break;
                }

                if (isSelfTarget)
                    healer.ApplyHoT((int)(healAmount / 2.5), healAmount * 4, vitalType, healer, CombatType.Melee);
                else
                    target.ApplyHoT((int)(healAmount / 2.5), healAmount * 4, vitalType, healer, CombatType.Melee);
            }

            NextAlchemyProcAttemptTime = currentTime + AlchemyProcAttemptInterval;
            Proficiency.OnSuccessUse(healer, skill, difficulty);
        }

        public void DoHealing(Player healer, Player target, uint missingVital)
        {
            if (target.IsDead || target.Teleporting) return;

            var remainingMsg = "";

            if (!UnlimitedUse)
            {
                UsesLeft--;
                var s = UsesLeft == 1 ? "" : "s";
                remainingMsg = UsesLeft > 0 ? $" Your {Name} has {UsesLeft} use{s} left." : $" Your {Name} is used up.";

                Value -= StructureUnitValue;

                if (Value < 0) // fix negative value
                    Value = 0;
            }

            var stackSize = new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, UsesLeft.Value);
            var targetName = healer == target ? "yourself" : target.Name;

            var vital = target.GetCreatureVital(BoosterEnum);

            // skill check
            var difficulty = 0;
            var skillCheck = DoSkillCheck(healer, target, missingVital, ref difficulty);
            if (!skillCheck)
            {
                var failMsg = new GameMessageSystemChat($"You fail to heal {targetName}.{remainingMsg}", ChatMessageType.Broadcast);
                healer.Session.Network.EnqueueSend(failMsg, stackSize);
                if (healer != target)
                    target.Session.Network.EnqueueSend(new GameMessageSystemChat($"{healer.Name} fails to heal you.", ChatMessageType.Broadcast));
                if (UsesLeft <= 0 && !UnlimitedUse)
                    healer.TryConsumeFromInventoryWithNetworking(this, 1);
                return;
            }

            // heal up
            var healAmount = GetHealAmount(healer, target, missingVital, out var critical, out var staminaCost);

            healer.UpdateVitalDelta(healer.Stamina, (int)-staminaCost);
            // Amount displayed to player can exceed actual amount healed due to heal boost ratings, but we only want to record the actual amount healed
            var actualHealAmount = (uint)target.UpdateVitalDelta(vital, healAmount);
            if (vital.Vital == PropertyAttribute2nd.MaxHealth)
                target.DamageHistory.OnHeal(actualHealAmount);

            //if (target.Fellowship != null)
            //target.Fellowship.OnVitalUpdate(target);

            var healingSkill = healer.GetCreatureSkill(Skill.Healing);
            Proficiency.OnSuccessUse(healer, healingSkill, difficulty);

            var pkLoweredMessage = "";
            //if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && healer.PKTimerActive)
            //    pkLoweredMessage = " Your recent PvP activity lowers the effectiveness of this action.";

            var crit = critical ? "expertly " : "";
            var message = new GameMessageSystemChat($"You {crit}heal {targetName} for {healAmount} {BoosterEnum.ToString()} points.{pkLoweredMessage}{remainingMsg}", ChatMessageType.Broadcast);

            healer.Session.Network.EnqueueSend(message, stackSize);

            if (healer != target)
                target.Session.Network.EnqueueSend(new GameMessageSystemChat($"{healer.Name} heals you for {healAmount} {BoosterEnum.ToString()} points.", ChatMessageType.Broadcast));

            TryProcAlchemyHoT(healer, target, difficulty, (int)healAmount, vital.Vital);

            if (UsesLeft <= 0 && !UnlimitedUse)
                healer.TryConsumeFromInventoryWithNetworking(this, 1);
        }

        /// <summary>
        /// Determines if healer successfully heals target for attempt
        /// </summary>
        public bool DoSkillCheck(Player healer, Player target, uint missingVital, ref int difficulty)
        {
            // skill check:
            // (healing skill + healing kit boost) * trainedMod
            // vs. damage * 2 * combatMod
            var healingSkill = healer.GetCreatureSkill(Skill.Healing);
            var trainedMod = healingSkill.AdvancementClass == SkillAdvancementClass.Specialized ? 1.5f : 1.1f;

            var combatMod = healer.CombatMode == CombatMode.NonCombat ? 1.0f : 1.1f;

            var effectiveSkill = (int)Math.Round((healingSkill.Current + BoostValue) * trainedMod);
            difficulty = (int)Math.Round(missingVital * 2 * combatMod);

            var skillCheck = SkillCheck.GetSkillChance(effectiveSkill, difficulty);
            return skillCheck > ThreadSafeRandom.Next(0.0f, 1.0f);
        }

        /// <summary>
        /// Returns the healing amount for this attempt
        /// </summary>
        public uint GetHealAmount(Player healer, Player target, uint missingVital, out bool criticalHeal, out uint staminaCost)
        {
            // factors: healing skill, healing kit bonus, stamina, critical chance
            var healingSkill = healer.GetCreatureSkill(Skill.Healing).Current;
            var healBase = healingSkill * (float)HealkitMod.Value;

            // todo: determine applicable range from pcaps
            var healMin = healBase * 0.2f;      // ??
            var healMax = healBase * 0.5f;
            var healAmount = ThreadSafeRandom.Next(healMin, healMax);

            // chance for critical healing
            criticalHeal = ThreadSafeRandom.Next(0.0f, 1.0f) < 0.1f;
            if (criticalHeal) healAmount *= 2;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && !target.PKTimerActive)
            {
                var vital = target.GetCreatureVital(BoosterEnum);
                missingVital = vital.Missing;
            }

            // cap to missing vital
            if (healAmount > missingVital)
                healAmount = missingVital;

            // stamina check? On the Q&A board a dev posted that stamina directly effects the amount of damage you can heal
            // low stam = less vital healed. I don't have exact numbers for it. Working through forum archive.

            // stamina cost: 1 stamina per 5 vital healed 
            staminaCost = (uint)Math.Round(healAmount / 5.0f);
            if (staminaCost > healer.Stamina.Current)
            {
                staminaCost = healer.Stamina.Current;
                healAmount = staminaCost * 5;
            }

            // verify healing boost comes from target instead of healer?
            // sounds like target in LumAugHealingRating...
            var ratingMod = target.GetHealingRatingMod();

            healAmount *= ratingMod;

            return (uint)Math.Round(healAmount);
        }
    }
}
