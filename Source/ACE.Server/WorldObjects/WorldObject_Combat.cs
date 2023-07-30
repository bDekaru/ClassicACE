using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class WorldObject
    {
        /// <summary>
        /// Determines if WorldObject can damage a target via PlayerKillerStatus
        /// </summary>
        /// <returns>null if no errors, else pk error list</returns>
        public virtual List<WeenieErrorWithString> CheckPKStatusVsTarget(WorldObject target, Spell spell)
        {
            // no restrictions here
            // player attacker restrictions handled in override
            return null;
        }

        /// <summary>
        /// Tries to proc any relevant items for the attack
        /// </summary>
        public void TryProcEquippedItems(WorldObject attacker, Creature target, bool selfTarget, WorldObject weapon)
        {
            if (!selfTarget && (AttacksCauseBleedChance ?? 0) > 0)
                TryProcInnateBleed(attacker, target);

            // handle procs directly on this item -- ie. phials
            // this could also be monsters with the proc spell directly on the creature
            if (HasProc && ProcSpellSelfTargeted == selfTarget)
            {
                // projectile
                // monster
                TryProcItem(attacker, target, selfTarget);
            }

            // handle proc spells for weapon
            // this could be a melee weapon, or a missile launcher
            if (weapon != null && weapon.HasProc && weapon.ProcSpellSelfTargeted == selfTarget)
            {
                // weapon
                weapon.TryProcItem(attacker, target, selfTarget);
            }

            if (attacker != this && attacker.HasProc && attacker.ProcSpellSelfTargeted == selfTarget)
            {
                // handle special case -- missile projectiles from monsters w/ a proc directly on the mob
                // monster
                attacker.TryProcItem(attacker, target, selfTarget);
            }

            // handle aetheria procs
            if (attacker is Creature wielder)
            {
                var equippedAetheria = wielder.EquippedObjects.Values.Where(i => Aetheria.IsAetheria(i.WeenieClassId) && i.HasProc && i.ProcSpellSelfTargeted == selfTarget);

                // aetheria
                foreach (var aetheria in equippedAetheria)
                    aetheria.TryProcItem(attacker, target, selfTarget);
            }
        }

        private double NextInnateBleedAttemptTime = 0;
        private static double InnateBleedAttemptInterval = 8;
        public void TryProcInnateBleed(WorldObject attacker, Creature target)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (target.IsDead)
                return; // Target is already dead, abort!

            var procSpellId = SpellId.Undef;
            var creatureAttacker = attacker as Creature;
            if (creatureAttacker == null)
                return;

            var currentTime = Time.GetUnixTime();
            if (NextInnateBleedAttemptTime > currentTime)
                return;

            var chance = AttacksCauseBleedChance;

            var playerAttacker = attacker as Player;
            if (playerAttacker != null)
                chance += playerAttacker.ScaleWithPowerAccuracyBar((float)chance);

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (rng >= chance)
                return;

            bool showCastMessage = true;

            var skill = creatureAttacker.GetCreatureSkill(creatureAttacker.GetCurrentAttackSkill());
            var enchantments = target.EnchantmentManager.GetEnchantments(SpellCategory.DFBleedDamage);
            var highest = enchantments?.OrderByDescending(i => i.PowerLevel).FirstOrDefault();
            if (skill.Current < 100)
                procSpellId = SpellId.Bleeding1;
            else if (skill.Current < 150)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding1;
                else
                    procSpellId = SpellId.Bleeding2;
            }
            else if (skill.Current < 200)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding1;
                else if (highest.SpellId == (int)SpellId.Bleeding1)
                    procSpellId = SpellId.Bleeding2;
                else
                    procSpellId = SpellId.Bleeding3;
            }
            else if (skill.Current < 250)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding2;
                else if (highest.SpellId == (int)SpellId.Bleeding2)
                    procSpellId = SpellId.Bleeding3;
                else
                    procSpellId = SpellId.Bleeding4;
            }
            else if (skill.Current < 300)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding3;
                else if (highest.SpellId == (int)SpellId.Bleeding3)
                    procSpellId = SpellId.Bleeding4;
                else
                    procSpellId = SpellId.Bleeding5;
            }
            else if (skill.Current < 325)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding4;
                else if (highest.SpellId == (int)SpellId.Bleeding4)
                    procSpellId = SpellId.Bleeding5;
                else
                    procSpellId = SpellId.Bleeding6;
            }
            else if (skill.Current < 350)
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding5;
                else if (highest.SpellId == (int)SpellId.Bleeding5)
                    procSpellId = SpellId.Bleeding6;
                else
                    procSpellId = SpellId.Bleeding7;
            }
            else
            {
                if (highest == null)
                    procSpellId = SpellId.Bleeding6;
                else if (highest.SpellId == (int)SpellId.Bleeding6)
                    procSpellId = SpellId.Bleeding7;
                else
                    procSpellId = SpellId.Bleeding8;
            }

            if (procSpellId == SpellId.Undef)
                return;

            var spell = new Spell(procSpellId);

            if (spell.NotFound)
            {
                if (attacker is Player player)
                {
                    if (spell._spellBase == null)
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"SpellId {ProcSpell.Value} Invalid.", ChatMessageType.System));
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} spell not implemented, yet!", ChatMessageType.System));
                }
                return;
            }

            // not sure if this should go before or after the resist check
            // after would match Player_Magic, but would require changing the signature of TryCastSpell yet again
            // starting with the simpler check here
            if (target != null && target.NonProjectileMagicImmune && !spell.IsProjectile)
            {
                if (attacker is Player player)
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You fail to affect {target.Name} with {spell.Name}", ChatMessageType.Magic));

                return;
            }

            if (creatureAttacker != null)
                NextInnateBleedAttemptTime = currentTime + InnateBleedAttemptInterval;

            var itemCaster = this is Creature ? null : this;

            if (spell.NonComponentTargetType == ItemType.None)
                attacker.TryCastSpell(spell, null, itemCaster, itemCaster, true, true, true, showCastMessage);
            else if (spell.NonComponentTargetType == ItemType.Vestements)
            {
                // TODO: spell.NonComponentTargetType should probably always go through TryCastSpell_WithItemRedirects,
                // however i don't feel like testing every possible known type of item procspell in the current db to ensure there are no regressions
                // current test case: 33990 Composite Bow casting Tattercoat
                attacker.TryCastSpell_WithRedirects(spell, target, itemCaster, itemCaster, true, true);
            }
            else
                attacker.TryCastSpell(spell, target, itemCaster, itemCaster, true, true, true, showCastMessage);
        }

        public float GetShieldMissileBlockBonus()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM || !IsShield || !Mass.HasValue)
                return 0;

            if (Mass >= 600)
                return 1.0f;
            else if (Mass >= 400)
                return 0.25f;
            else if (Mass >= 200)
                return 0.10f;
            else
                return 0;
        }
    }
}
