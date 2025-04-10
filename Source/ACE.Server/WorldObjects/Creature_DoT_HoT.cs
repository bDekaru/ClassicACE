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
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"You infuse {targetName} with periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
                else
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"With {sourceMessage} you infuse {targetName} with periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
            }

            if (targetPlayer != null && targetPlayer != sourcePlayer)
            {
                if (combatType != CombatType.Magic || sourceMessage == null || sourceMessage == "")
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourcePlayer.Name} infuses you with periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
                else
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourcePlayer.Name} casts {sourceMessage} and infuses you with periodic {(vitalType == DamageType.Health ? "healing" : $"{vitalType.GetName().ToLower()} gain")}.", ChatMessageType.Magic));
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

            var damageResistRatingMod = GetDamageResistRatingMod(combatType, false);

            var pvpMod = 1.0f;
            if (isPvP)
            {
                var pkDamageResistRatingMod = GetNegativeRatingMod(targetPlayer.GetPKDamageResistRating());

                damageResistRatingMod = AdditiveCombine(damageResistRatingMod, pkDamageResistRatingMod);

                pvpMod = (float)PropertyManager.GetInterpolatedDouble(sourcePlayer.Level ?? 1, "pvp_dmg_mod_low", "pvp_dmg_mod_high", "pvp_dmg_mod_low_level", "pvp_dmg_mod_high_level");
                if (damageType == DamageType.Nether)
                    pvpMod *= (float)PropertyManager.GetInterpolatedDouble(sourcePlayer.Level ?? 1, "pvp_dmg_mod_low_void_dot", "pvp_dmg_mod_high_void_dot", "pvp_dmg_mod_low_level", "pvp_dmg_mod_high_level");
                else
                    pvpMod *= (float)PropertyManager.GetInterpolatedDouble(sourcePlayer.Level ?? 1, "pvp_dmg_mod_low_dot", "pvp_dmg_mod_high_dot", "pvp_dmg_mod_low_level", "pvp_dmg_mod_high_level");
            }

            var heartbeatMod = (float)HeartbeatInterval / 5.0f; // Modifier to account for non-default heartbeat intervals.

            var dotResistRatingMod = GetNegativeRatingMod(GetDotResistanceRating());

            var modifiers = damageResistRatingMod * dotResistRatingMod * pvpMod * heartbeatMod;

            tickAmount = (int)Math.Round(tickAmount * modifiers);
            totalAmount = (int)Math.Round(totalAmount * modifiers);

            var weaponResistanceMod = 1.0f;
            var elementalDamageMod = 1.0f;
            var slayerMod = 1.0f;
            var absorbMod = 1.0f;

            if (sourceWeapon != null)
            {
                weaponResistanceMod = GetWeaponResistanceModifier(sourceWeapon, sourceCreature, attackSkill, damageType);
                elementalDamageMod = GetCasterElementalDamageModifier(sourceWeapon, sourceCreature, this, damageType);
                slayerMod = GetWeaponCreatureSlayerModifier(sourceWeapon, sourceCreature, this);
            }

            if (attackSkill != null && Player.MagicSkills.Contains(attackSkill.Skill))
            {
                absorbMod = SpellProjectile.GetAbsorbMod(source, this);

                //http://acpedia.org/wiki/Announcements_-_2014/01_-_Forces_of_Nature - Aegis is 72% effective in PvP
                if (isPvP && (targetPlayer.CombatMode == CombatMode.Melee || targetPlayer.CombatMode == CombatMode.Missile) && Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                {
                    absorbMod = 1 - absorbMod;
                    absorbMod *= 0.72f;
                    absorbMod = 1 - absorbMod;
                }
            }

            var casterMods = elementalDamageMod * slayerMod * absorbMod;

            lock(DoTHoTListLock)
            {
                ActiveDamageOverTimeList.Add(new DoTInfo(tickAmount, totalAmount, combatType, damageType, source, casterMods, weaponResistanceMod));
            }

            if (targetPlayer != null && sourceCreature != null)
                targetPlayer.SetCurrentAttacker(sourceCreature);

            if (isPvP)
                Player.UpdatePKTimers(sourcePlayer, targetPlayer);

            string verb = null, plural = null;
            var percent = totalAmount / Health.MaxValue;
            Strings.GetAttackVerb(damageType, percent, ref verb, ref plural);
            var critMsg = isCritical ? "Critical hit! " : "";

            if (sourcePlayer != null || sourceMessage == "")
            {
                var targetName = source == this ? "yourself" : Name;
                if(sourceMessage == null)
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{critMsg}You {verb} {targetName} with periodic {damageType.GetName().ToLower()} damage.", ChatMessageType.Magic));
                else
                    sourcePlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{critMsg}Your {sourceMessage} {plural} {targetName} with periodic {damageType.GetName().ToLower()} damage.", ChatMessageType.Magic));
            }

            if (targetPlayer != null && targetPlayer != sourcePlayer)
            {
                if (sourceMessage == null || sourceMessage == "")
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{critMsg}{sourcePlayer.Name} {plural} you with periodic {damageType.GetName().ToLower()} damage.", ChatMessageType.Magic));
                else
                    targetPlayer.Session.Network.EnqueueSend(new GameMessageSystemChat($"{critMsg}{sourcePlayer.Name}'s {sourceMessage} {plural} you with periodic {damageType.GetName().ToLower()} damage.", ChatMessageType.Magic));
            }
        }

        public void DoTHotHeartbeat()
        {
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

                    var totalTickAmountPerDamageTypeMagic = new Dictionary<DamageType, (float TotalTickAmount, Dictionary<WorldObject, float> Damagers)>();
                    var totalTickAmountPerDamageTypePhysical = new Dictionary<DamageType, (float TotalTickAmount, Dictionary<WorldObject, float> Damagers)>();

                    var totalTickAmount = 0.0f;

                    var updatedDamageOverTimeList = new List<DoTInfo>();
                    foreach (var dot in ActiveDamageOverTimeList)
                    {
                        var baseTickAmount = Math.Min(dot.TickAmount, dot.TotalAmount);
                        var totalAmountRemaining = Math.Max(dot.TotalAmount - dot.TickAmount, 0);

                        if (totalAmountRemaining > 0)
                            updatedDamageOverTimeList.Add(new DoTInfo(dot.TickAmount, totalAmountRemaining, dot.CombatType, dot.DamageType, dot.Source, dot.CasterMods, dot.CasterWeaponResistanceMod));

                        var resistanceMod = (float)Math.Max(0.0f, GetResistanceMod(GetResistanceType(dot.DamageType), this, null, dot.CasterWeaponResistanceMod));

                        var tickAmount = baseTickAmount * dot.CasterMods * resistanceMod;

                        // make sure the target's current health is not exceeded
                        if (totalTickAmount + tickAmount >= Health.Current)
                        {
                            tickAmount = Health.Current - totalTickAmount;
                            isDead = true;
                        }

                        if (dot.CombatType == CombatType.Magic)
                        {
                            var elementTotalTickAmountMagic = 0.0f;
                            Dictionary<WorldObject, float> elementDamagersMagic = null;
                            if (totalTickAmountPerDamageTypeMagic.TryGetValue(dot.DamageType, out var value))
                            {
                                elementTotalTickAmountMagic = value.TotalTickAmount + tickAmount;
                                elementDamagersMagic = value.Damagers;
                            }
                            else
                            {
                                elementTotalTickAmountMagic = tickAmount;
                                elementDamagersMagic = new Dictionary<WorldObject, float>();
                            }

                            if (dot.Source != null)
                            {
                                if (elementDamagersMagic.ContainsKey(dot.Source))
                                    elementDamagersMagic[dot.Source] += tickAmount;
                                else
                                    elementDamagersMagic.Add(dot.Source, tickAmount);

                                DamageHistory.Add(dot.Source, dot.DamageType, (uint)tickAmount);
                            }

                            totalTickAmountPerDamageTypeMagic[dot.DamageType] = (elementTotalTickAmountMagic, elementDamagersMagic);
                        }
                        else
                        {
                            var elementTotalTickAmountPhysical = 0.0f;
                            Dictionary<WorldObject, float> elementDamagersPhysical = null;
                            if (totalTickAmountPerDamageTypePhysical.TryGetValue(dot.DamageType, out var value))
                            {
                                elementTotalTickAmountPhysical = value.TotalTickAmount + tickAmount;
                                elementDamagersPhysical = value.Damagers;
                            }
                            else
                            {
                                elementTotalTickAmountPhysical = tickAmount;
                                elementDamagersPhysical = new Dictionary<WorldObject, float>();
                            }

                            if (dot.Source != null)
                            {
                                if (elementDamagersPhysical.ContainsKey(dot.Source))
                                    elementDamagersPhysical[dot.Source] += tickAmount;
                                else
                                    elementDamagersPhysical.Add(dot.Source, tickAmount);

                                DamageHistory.Add(dot.Source, dot.DamageType, (uint)tickAmount);
                            }

                            totalTickAmountPerDamageTypePhysical[dot.DamageType] = (elementTotalTickAmountPhysical, elementDamagersPhysical);
                        }

                        if (isDead)
                            break;
                    }
                    ActiveDamageOverTimeList = updatedDamageOverTimeList;

                    totalTickAmountPerDamageTypeMagic = totalTickAmountPerDamageTypeMagic.OrderByDescending(o => o.Value.TotalTickAmount).ToDictionary();
                    totalTickAmountPerDamageTypePhysical = totalTickAmountPerDamageTypePhysical.OrderByDescending(o => o.Value.TotalTickAmount).ToDictionary();

                    var playedEffects = false;
                    foreach (var entry in totalTickAmountPerDamageTypeMagic)
                    {
                        TakeDamageOverTime(entry.Value.TotalTickAmount, entry.Key, playedEffects, false);

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

                                TakeDamageOverTime_NotifySource(damageSourcePlayer, entry.Key, amount, false, false);

                                if (IsAlive)
                                    EmoteManager.OnDamage(damageSourcePlayer);
                            }
                        }
                    }

                    foreach (var entry in totalTickAmountPerDamageTypePhysical)
                    {
                        TakeDamageOverTime(entry.Value.TotalTickAmount, entry.Key, playedEffects, true);

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

                                TakeDamageOverTime_NotifySource(damageSourcePlayer, entry.Key, amount, false, true);

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
    }
}
