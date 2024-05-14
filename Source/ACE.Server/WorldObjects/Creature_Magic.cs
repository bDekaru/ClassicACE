using System;
using System.Linq;

using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Creature
    {
        public uint CalculateManaUsage(Creature caster, Spell spell, WorldObject target = null)
        {
            var baseCost = spell.BaseMana;

            // for casting spells built into a casting implement, use the ItemManaCost
            var castItem = caster.GetEquippedWand();
            if (castItem != null && (castItem.SpellDID ?? 0) == spell.Id)
                baseCost = (uint)(castItem.ItemManaCost ?? 0);

            if (target is Creature targetCreature && spell.School == MagicSchool.ItemEnchantment && spell.MetaSpellType == SpellType.Enchantment && spell.IsImpenBaneType && targetCreature == this)
            {
                var numExtraTargetItems = targetCreature.EquippedObjects.Values.Count(i => (i is Clothing || i.IsShield) && i.IsEnchantable);
                baseCost += spell.ManaMod * (uint)numExtraTargetItems;
            }
            else if ((spell.Flags & SpellFlags.FellowshipSpell) != 0)
            {
                var numFellows = 1;
                if (this is Player player && player.Fellowship != null)
                    numFellows = player.Fellowship.FellowshipMembers.Count;

                baseCost += spell.ManaMod * (uint)numFellows;
            }

            var manaConversion = caster.GetCreatureSkill(Skill.ManaConversion);

            if (manaConversion.AdvancementClass < SkillAdvancementClass.Trained || spell.Flags.HasFlag(SpellFlags.IgnoresManaConversion))
                return baseCost;

            uint difficulty;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                difficulty = spell.PowerMod;   // modified power difficulty
            else
                difficulty = spell.Level * 25;

            var robeManaConversionMod = 0.0;
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                var robe = EquippedObjects.Values.FirstOrDefault(e => e.CurrentWieldedLocation == EquipMask.Armor);
                if (robe != null)
                    robeManaConversionMod = robe.ManaConversionMod ?? 0;
            }

            var mana_conversion_skill = (uint)Math.Round(manaConversion.Current * (GetWeaponManaConversionModifier(caster) + robeManaConversionMod));

            var manaCost = GetManaCost(difficulty, baseCost, mana_conversion_skill, manaConversion.AdvancementClass);

            return manaCost;
        }

        public static uint GetManaCost(uint difficulty, uint manaCost, uint manaConv, SkillAdvancementClass advancementClass)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                // thanks to GDLE for this function!
                if (manaConv == 0)
                    return manaCost;

                // Dropping diff by half as Specced ManaC is only 48 with starter Aug so 50 at level 1 means no bonus
                //   easiest change without having to create two different formulas to try to emulate retail
                var successChance = SkillCheck.GetSkillChance(manaConv, difficulty / 2);
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);

                // Luck lowers the roll value to give better outcome
                // e.g. successChance = 0.83 & roll = 0.71 would still provide some savings.
                //   but a luck roll of 0.19 will lower that 0.71 to 0.13 so the caster would
                //   receive a 60% reduction in mana cost.  without the luck roll, 12%
                //   so players will always have a level of "luck" in manacost if they make skill checks
                var luck = ThreadSafeRandom.Next(0.0f, 1.0f);

                if (roll < successChance)
                {
                    manaCost = (uint)Math.Round(manaCost * (1.0f - (successChance - (roll * luck))));
                }

                // above seems to give a good middle of the range
                // seen in pcaps for mana usage for low level chars
                // bug still need a way to give a better reduction for the "lucky"

                // save some calc time if already at 1 mana cost
                if (manaCost > 1)
                {
                    successChance = SkillCheck.GetSkillChance(manaConv, difficulty);
                    roll = ThreadSafeRandom.Next(0.0f, 1.0f);

                    if (roll < successChance)
                        manaCost = (uint)Math.Round(manaCost * (1.0f - (successChance - (roll * luck))));
                }
            }
            else
            {
                // From an AC Q&A:
                //	A mana conversion skill check is made at 25 diff per level of
                //	spell, and the cost of the spell is reduced if the skill check is
                //	successful.The reduction is a random percentage from 0 to the
                //	chance of your mana conversion skill check success rate.In other
                //	words, if you have an 80 % chance of your mana conversion working
                //	on a spell, then 20 % of the time you will save nothing, and the
                //	other 80 % you will save between 0 and 80 % of the mana.Obviously,
                //	lower level spells will succeed much better, because you will have
                //	nearly a 100 % chance of conversion success.

                if (manaConv == 0 || manaCost <= 1)
                    return manaCost;

                var successChance = SkillCheck.GetSkillChance(manaConv, difficulty);
                var roll = ThreadSafeRandom.Next(0.0f, 1.0f);

                if (roll < successChance)
                {
                    var reductionMinAmount = 0.0f;
                    var reductionMaxAmount = (float)successChance;
                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    {
                        if(advancementClass == SkillAdvancementClass.Specialized)
                            reductionMinAmount = (float)successChance / 2.0f;
                        else
                            reductionMinAmount = (float)successChance / 4.0f;
                    }
                    var reductionRoll = ThreadSafeRandom.Next(reductionMinAmount, reductionMaxAmount);
                    manaCost = (uint)Math.Round(manaCost * (1.0f - reductionRoll));
                }
            }

            return Math.Max(manaCost, 1);
        }

        /// <summary>
        /// Handles equipping an item casting a spell on player or creature
        /// </summary>
        public bool CreateItemSpell(WorldObject item, uint spellID)
        {
            var spell = new Spell(spellID);

            if (spell.NotFound)
            {
                if (this is Player player)
                {
                    if (spell._spellBase == null)
                        player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"SpellID {spellID} Invalid."));
                    else
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{spell.Name} spell not implemented, yet!", ChatMessageType.System));
                }
                return false;
            }

            // TODO: look into condensing this
            switch (spell.School)
            {
                case MagicSchool.CreatureEnchantment:
                case MagicSchool.LifeMagic:

                    HandleCastSpell(spell, this, item, equip: true);
                    break;

                case MagicSchool.ItemEnchantment:

                    if (spell.HasItemCategory || spell.IsPortalSpell)
                        HandleCastSpell(spell, this, item, item, equip: true);
                    else
                        HandleCastSpell(spell, item, item, item, equip: true);

                    break;
            }

            return true;
        }

        /// <summary>
        /// Removes an item's spell from the appropriate enchantment registry (either the wielder, or the item)
        /// </summary>
        /// <param name="silent">if TRUE, silently removes the spell, without sending a message to the target player</param>
        public void RemoveItemSpell(WorldObject item, uint spellId, bool silent = false)
        {
            if (item == null) return;

            var spell = new Spell(spellId);

            if (spell._spellBase == null)
            {
                if (this is Player player)
                    player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"SpellId {spellId} Invalid."));

                return;
            }

            var target = spell.School == MagicSchool.ItemEnchantment && !spell.HasItemCategory ? item : this;

            // Retrieve enchantment on target and remove it, if present
            var propertiesEnchantmentRegistry = target.EnchantmentManager.GetEnchantment(spellId, item.Guid.Full);

            if (propertiesEnchantmentRegistry != null)
            {
                if (!silent)
                    target.EnchantmentManager.Remove(propertiesEnchantmentRegistry);
                else
                    target.EnchantmentManager.Dispel(propertiesEnchantmentRegistry);
            }
        }

        /// <summary>
        /// Returns the creature's effective magic defense skill
        /// with item.WeaponMagicDefense and imbues factored in
        /// </summary>
        public uint GetEffectiveMagicDefense()
        {
            var current = GetCreatureSkill(Skill.MagicDefense).Current;
            var weaponDefenseMod = GetWeaponMagicDefenseModifier(this);
            var defenseImbues = (uint)GetDefenseImbues(ImbuedEffectType.MagicDefense);

            var effectiveMagicDefense = (uint)Math.Round((current * weaponDefenseMod) + defenseImbues);

            //Console.WriteLine($"EffectiveMagicDefense: {effectiveMagicDefense}");

            return effectiveMagicDefense;
        }
    }
}
