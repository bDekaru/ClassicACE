using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public void ClearAllDoTsAndHoTs()
        {
            lock(DoTHoTListLock)
            {
                ActiveDamageOverTimeList.Clear();
                ActiveHealOverTimeList.Clear();
            }
        }

        public void ApplyHoT(int tickAmount, int totalAmount, DamageType vitalType, WorldObject source, CombatType combatType, string sourceMessage = null)
        {
            if (vitalType != DamageType.Health && vitalType != DamageType.Stamina && vitalType != DamageType.Mana)
            {
                log.WarnFormat($"ApplyHoT() {source?.Name} tried to add non-vital HoT: {vitalType}");
                return;
            }

            var sourcePlayer = source as Player;
            var targetPlayer = this as Player;

            //Todo: add support for other vital types
            var heartbeatMod = (float)HeartbeatInterval / 5.0f; // Modifier to account for non-default heartbeat intervals.

            tickAmount = (int)Math.Round(tickAmount * heartbeatMod);

            lock(DoTHoTListLock)
            {
                ActiveHealOverTimeList.Add(new HoTInfo(tickAmount, totalAmount, vitalType, source));
            }

            if (sourcePlayer != null)
            {
                var targetName = source == this ? "yourself" : Name;
                if (combatType != CombatType.Magic || sourceMessage == null || sourceMessage == "")
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"You infuse {targetName} with {totalAmount:N0} points of periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
                else
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"With {sourceMessage} you infuse {targetName} {totalAmount:N0} points of with periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
            }

            if (targetPlayer != null && targetPlayer != sourcePlayer)
            {
                if (combatType != CombatType.Magic || sourceMessage == null || sourceMessage == "")
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourcePlayer.Name} infuses you with {totalAmount:N0} points of periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
                else
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourcePlayer.Name} casts {sourceMessage} and infuses you with {totalAmount:N0} points of periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
            }
        }

        public void ApplyDoT(int tickAmount, int totalAmount, bool isCritical, CombatType combatType, DamageType damageType, WorldObject source, WorldObject sourceWeapon, CreatureSkill attackSkill, string sourceMessage = null)
        {
            var sourceCreature = source as Creature;
            var sourcePlayer = source as Player;
            var targetPlayer = this as Player;
            var isPvP = sourcePlayer != null && targetPlayer != null;

            if (source == null || IsDead || Invincible || IsOnNoDamageLandblock)
                return;

            // check lifestone protection
            if (targetPlayer != null && targetPlayer.UnderLifestoneProtection)
            {
                if (sourcePlayer != null)
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"The Lifestone's magic protects {targetPlayer.Name} from the attack!", ChatMessageType.Magic));

                targetPlayer.HandleLifestoneProtection();
                return;
            }

            var dotResistRatingMod = GetNegativeRatingMod(GetDotResistanceRating());

            tickAmount = (int)Math.Round(tickAmount * dotResistRatingMod);
            totalAmount = (int)Math.Round(totalAmount * dotResistRatingMod);

            lock(DoTHoTListLock)
            {
                ActiveDamageOverTimeList.Add(new DoTInfo(tickAmount, totalAmount, combatType, damageType, source));
            }

            if (targetPlayer != null && sourceCreature != null)
                targetPlayer.SetCurrentAttacker(sourceCreature);

            if (isPvP)
                Player.UpdatePKTimers(sourcePlayer, targetPlayer);

            var critMsg = isCritical ? "Critical hit!  " : "";
            var messageType = ChatMessageType.Magic;
            var damageTypeString = damageType.GetName().ToLower();

            if (sourcePlayer != null || sourceMessage == "")
            {
                var targetName = source == this ? "yourself" : Name;
                if (sourceMessage == null)
                    sourcePlayer.SendMessage($"{critMsg}Your attack adds {totalAmount:N0} points of periodic {damageTypeString} damage to {targetName}.", messageType);
                else
                    sourcePlayer.SendMessage($"{critMsg}Your {sourceMessage} adds {totalAmount:N0} points of periodic {damageTypeString} damage to {targetName}.", messageType);
            }

            if (targetPlayer != null && targetPlayer != source)
            {
                if (sourceMessage == null || sourceMessage == "")
                    targetPlayer.SendMessage($"{critMsg}{source.Name}'s attack adds {totalAmount:N0} points of periodic {damageTypeString} damage to you.", messageType);
                else
                    targetPlayer.SendMessage($"{critMsg}{source.Name}'s {sourceMessage} adds {totalAmount:N0} points of periodic {damageTypeString} damage to you.", messageType);
            }
        }

        private double DoTHoT_TickTimestamp;
        private const double DoTHoT_TickInterval = 2.5;

        public void DoTHotTick(double currentUnixTime)
        {
            if (DoTHoT_TickTimestamp != 0 && currentUnixTime <= DoTHoT_TickTimestamp)
                return;

            DoTHoT_TickTimestamp = Time.GetFutureUnixTime(DoTHoT_TickInterval);

            if (IsDead)
                return;

            lock (DoTHoTListLock)
            {
                if ((ActiveDamageOverTimeList == null || ActiveDamageOverTimeList.Count == 0) && (ActiveHealOverTimeList == null || ActiveHealOverTimeList.Count == 0))
                    return;

                var player = this as Player;

                if (ActiveDamageOverTimeList != null && ActiveDamageOverTimeList.Count > 0)
                {
                    bool isDead = false;

                    var totalTickAmountPerDamageType = new Dictionary<DamageType, (float TotalTickAmount, Dictionary<WorldObject, float> Damagers)>();

                    var totalTickAmount = 0.0f;

                    var updatedDamageOverTimeList = new List<DoTInfo>();
                    foreach (var dot in ActiveDamageOverTimeList)
                    {
                        var tickAmount = (float)Math.Min(dot.TickAmount, dot.TotalAmount);
                        var totalAmountRemaining = Math.Max(dot.TotalAmount - dot.TickAmount, 0);

                        if (totalAmountRemaining > 0)
                            updatedDamageOverTimeList.Add(new DoTInfo(dot.TickAmount, totalAmountRemaining, dot.CombatType, dot.DamageType, dot.Source));

                        // make sure the target's current health is not exceeded
                        if (totalTickAmount + tickAmount >= Health.Current)
                        {
                            tickAmount = Health.Current - totalTickAmount;
                            isDead = true;
                        }

                        var elementTotalTickAmount = 0.0f;
                        Dictionary<WorldObject, float> elementDamagers = null;
                        if (totalTickAmountPerDamageType.TryGetValue(dot.DamageType, out var value))
                        {
                            elementTotalTickAmount = value.TotalTickAmount + tickAmount;
                            elementDamagers = value.Damagers;
                        }
                        else
                        {
                            elementTotalTickAmount = tickAmount;
                            elementDamagers = new Dictionary<WorldObject, float>();
                        }

                        if (dot.Source != null)
                        {
                            if (elementDamagers.ContainsKey(dot.Source))
                                elementDamagers[dot.Source] += tickAmount;
                            else
                                elementDamagers.Add(dot.Source, tickAmount);

                            DamageHistory.Add(dot.Source, dot.DamageType, (uint)tickAmount);
                        }

                        totalTickAmountPerDamageType[dot.DamageType] = (elementTotalTickAmount, elementDamagers);

                        if (isDead)
                            break;
                    }
                    ActiveDamageOverTimeList = updatedDamageOverTimeList;

                    totalTickAmountPerDamageType = totalTickAmountPerDamageType.OrderByDescending(o => o.Value.TotalTickAmount).ToDictionary();

                    var playedEffects = false;
                    foreach (var entry in totalTickAmountPerDamageType)
                    {
                        TakeDamageOverTime(entry.Value.TotalTickAmount, entry.Key, playedEffects);

                        if (!playedEffects)
                            playedEffects = true;

                        if (!IsAlive)
                            return;

                        foreach (var kvp in entry.Value.Damagers)
                        {
                            var damager = kvp.Key;
                            var amount = kvp.Value;

                            if (Invincible || IsDead || IsOnNoDamageLandblock)
                                amount = 0;

                            var damageSourcePlayer = damager as Player;
                            if (damageSourcePlayer != null && damageSourcePlayer != this)
                            {
                                if (player != null)
                                    Player.UpdatePKTimers(damageSourcePlayer, player);

                                TakeDamageOverTime_NotifySource(damageSourcePlayer, entry.Key, amount, false);

                                if (IsAlive)
                                    EmoteManager.OnDamage(damageSourcePlayer);
                            }
                        }
                    }
                }

                if (ActiveHealOverTimeList != null && ActiveHealOverTimeList.Count > 0)
                {
                    var totalTickAmountPerVitalType = new Dictionary<DamageType, (float TotalTickAmount, Dictionary<WorldObject, float> Healers)>();

                    var updatedHealOverTimeList = new List<HoTInfo>();
                    foreach (var hot in ActiveHealOverTimeList)
                    {
                        var baseTickAmount = Math.Min(hot.TickAmount, hot.TotalAmount);
                        var totalAmountRemaining = Math.Max(hot.TotalAmount - hot.TickAmount, 0);

                        if (totalAmountRemaining > 0)
                            updatedHealOverTimeList.Add(new HoTInfo(hot.TickAmount, totalAmountRemaining, hot.VitalType, hot.Source));

                        var healingRateMod = GetHealingRatingMod();

                        var tickAmount = baseTickAmount * healingRateMod;

                        var vitalTypeTotalTickAmount = 0.0f;
                        Dictionary<WorldObject, float> vitalTypeHealers = null;
                        if (totalTickAmountPerVitalType.TryGetValue(hot.VitalType, out var value))
                        {
                            vitalTypeTotalTickAmount = value.TotalTickAmount + tickAmount;
                            vitalTypeHealers = value.Healers;
                        }
                        else
                        {
                            vitalTypeTotalTickAmount = tickAmount;
                            vitalTypeHealers = new Dictionary<WorldObject, float>();
                        }

                        if (hot.Source != null)
                        {
                            if (vitalTypeHealers.ContainsKey(hot.Source))
                                vitalTypeHealers[hot.Source] += tickAmount;
                            else
                                vitalTypeHealers.Add(hot.Source, tickAmount);

                            if (hot.VitalType == DamageType.Health)
                                DamageHistory.OnHeal((uint)tickAmount);
                        }

                        totalTickAmountPerVitalType[hot.VitalType] = (vitalTypeTotalTickAmount, vitalTypeHealers);
                    }
                    ActiveHealOverTimeList = updatedHealOverTimeList;

                    totalTickAmountPerVitalType = totalTickAmountPerVitalType.OrderByDescending(o => o.Value.TotalTickAmount).ToDictionary();

                    var playedEffects = false;
                    foreach (var entry in totalTickAmountPerVitalType)
                    {
                        var healAmount = UpdateVitalDelta(Health, (int)Math.Round(entry.Value.TotalTickAmount));

                        if (player != null)
                        {
                            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                                player.SendMessage($"You receive {healAmount} points of periodic {(entry.Key == DamageType.Health ? "healing" : $"{entry.Key.GetName().ToLower()} gain")}.", PropertyManager.GetBool("aetheria_heal_color").Item ? ChatMessageType.Broadcast : ChatMessageType.Combat);
                            else
                                player.SendMessage($"You receive {healAmount} points of periodic {(entry.Key == DamageType.Health ? "healing" : $"{entry.Key.GetName().ToLower()} gain")}.", ChatMessageType.Magic);
                        }

                        if (!playedEffects)
                            playedEffects = true;

                        if (!IsAlive)
                            return;

                        foreach (var kvp in entry.Value.Healers)
                        {
                            var healer = kvp.Key;
                            var amount = kvp.Value;

                            if (IsDead)
                                amount = 0;

                            var healSourcePlayer = healer as Player;
                            if (healSourcePlayer != null && healSourcePlayer != this)
                            {
                                var targetName = healSourcePlayer == this ? "yourself" : Name;
                                healSourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"You {(entry.Key == DamageType.Health ? "heal" : "infuse")} {targetName} with {amount} points of periodic {(entry.Key == DamageType.Health ? "healing" : $"{entry.Key.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
                            }
                        }
                    }
                }
            }
        }

        public void ApplySkillAndInnateDoTs(WorldObject source, WorldObject weapon, float baseDamage, DamageType damageType, bool isCritical, Skill attackSkill)
        {
            if (baseDamage == 0)
                return;

            var hasMultistrikeDoT = weapon != null && (weapon.WeaponSkill == Skill.Dagger || (weapon.WeaponSkill == Skill.Sword && weapon.W_AttackType.IsMultiStrike()));
            var hasInnateDoT = source.AttacksCauseBleedChance.HasValue && source.AttacksCauseBleedChance > 0;
            var hasInnateWeaponDoT = weapon != null && weapon.AttacksCauseBleedChance.HasValue && weapon.AttacksCauseBleedChance > 0;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && baseDamage > 0 && (hasMultistrikeDoT || hasInnateDoT || hasInnateWeaponDoT))
            {
                var chance = 0d;
                if (hasInnateDoT)
                    chance = source.AttacksCauseBleedChance.Value;

                if (hasInnateWeaponDoT && weapon.AttacksCauseBleedChance.Value > chance)
                    chance = weapon.AttacksCauseBleedChance.Value;

                if (hasMultistrikeDoT || chance > ThreadSafeRandom.Next(0.0f, 1.0f))
                {
                    var damageTotal = 5 + (int)Math.Round(baseDamage);
                    var damageTick = damageTotal / 5;
                    var hemorrhaged = false;

                    if (0.10 > ThreadSafeRandom.Next(0.0f, 1.0f))
                        hemorrhaged = DoTHemorrhage(source, CombatType.Melee, damageType);

                    if (!hemorrhaged)
                        ApplyDoT(damageTick, damageTotal, isCritical, CombatType.Melee, damageType, source, weapon, GetCreatureSkill(attackSkill));
                }
            }
        }

        public bool DoTHemorrhage(WorldObject source, CombatType combatType, DamageType damageType)
        {
            if (IsDead)
                return false;

            var sourceCreature = source as Creature;
            var sourcePlayer = source as Player;
            var targetPlayer = this as Player;
            var isPvP = sourcePlayer != null && targetPlayer != null;

            if (source == null || IsDead || Invincible || IsOnNoDamageLandblock)
                return false;

            // check lifestone protection
            if (targetPlayer != null && targetPlayer.UnderLifestoneProtection)
            {
                if (sourcePlayer != null)
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"The Lifestone's magic protects {targetPlayer.Name} from the attack!", ChatMessageType.Magic));

                targetPlayer.HandleLifestoneProtection();
                return false;
            }

            List<DoTInfo> hemorrhageList;
            lock (DoTHoTListLock)
            {
                hemorrhageList = ActiveDamageOverTimeList.Where(e => e.Source == source && e.CombatType == combatType && e.DamageType == damageType).OrderByDescending(d => d.TotalAmount).ToList();

                if (hemorrhageList.Count == 0)
                    return false;

                ActiveDamageOverTimeList = ActiveDamageOverTimeList.Where(e => e.Source != source || e.CombatType != combatType || e.DamageType != damageType).ToList();
            }

            var damage = 0f;
            foreach(var entry in hemorrhageList)
            {
                damage += entry.TotalAmount;
            }

            if (damage > 0)
            {
                if (targetPlayer != null && sourceCreature != null)
                    targetPlayer.SetCurrentAttacker(sourceCreature);

                if (isPvP)
                    Player.UpdatePKTimers(sourcePlayer, targetPlayer);

                TakeDamage(null, damageType, damage);

                // splatter effects
                var hitSound = new GameMessageSound(Guid, Sound.HitFlesh1, 0.5f);
                //var splatter = (PlayScript)Enum.Parse(typeof(PlayScript), "Splatter" + playerSource.GetSplatterHeight() + playerSource.GetSplatterDir(this));
                var splatter = new GameMessageScript(Guid, damageType == DamageType.Nether ? PlayScript.HealthDownVoid : PlayScript.DirtyFightingDamageOverTime);
                EnqueueBroadcast(hitSound, splatter);

                if (!IsDead)
                {

                    if (damage >= Health.MaxValue * 0.25f)
                    {
                        var painSound = (Sound)Enum.Parse(typeof(Sound), "Wound" + ThreadSafeRandom.Next(1, 3), true);
                        EnqueueBroadcast(new GameMessageSound(Guid, painSound, 1.0f));
                    }

                    string verb = null, plural = null;
                    var percent = damage / Health.MaxValue;
                    Strings.GetAttackVerb(damageType, percent, ref verb, ref plural);
                    var messageType = ChatMessageType.x1B;
                    var damageTypeString = damageType.GetName().ToLower();

                    if (damageType == DamageType.Health || damageType == DamageType.Slash || damageType == DamageType.Pierce || damageType == DamageType.Bludgeon)
                    {
                        verb = "bleed";
                        plural = "bleeds";
                    }

                    if (sourcePlayer != null)
                    {
                        var targetName = source == this ? "yourself" : Name;
                        sourcePlayer.SendMessage($"Hemorrhage! You {verb} {targetName} with {damage:N0} points of {damageTypeString} damage!", messageType);
                    }

                    if (targetPlayer != null && targetPlayer != sourcePlayer)
                        targetPlayer.SendMessage($"Hemorrhage! {source.Name} {plural} you with {damage:N0} points of {damageTypeString} damage.", messageType);
                }
            }

            return true;
        }
    }
}
