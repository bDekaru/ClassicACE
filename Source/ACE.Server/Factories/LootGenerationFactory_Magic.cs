using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;
using System;
using System.Collections.Generic;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void AssignMagic(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            var spells = RollSpells(wo, profile, roll);

            foreach (var spell in spells)
            {
                wo.Biota.GetOrAddKnownSpell((int)spell, wo.BiotaDatabaseLock, out _);
            }

            if ((roll.IsMeleeWeapon || roll.IsMissileWeapon) && Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                var itemProc = RollItemProc(wo, profile, roll);

                if (itemProc != SpellId.Undef)
                {
                    Server.Entity.Spell spell = new Server.Entity.Spell(itemProc);
                    wo.ProcSpellRate = 0.15f;
                    wo.ProcSpell = (uint)itemProc;
                    wo.ProcSpellSelfTargeted = spell.IsSelfTargeted;

                    roll.AllSpells.Add(itemProc);
                }
            }

            if (wo.SpellDID.HasValue && wo.SpellDID != (uint)SpellId.Undef)
                roll.AllSpells.Add((SpellId)wo.SpellDID);

            if (spells.Count == 0 && wo.SpellDID == null && wo.ProcSpell == null)
            {
                // we ended up without any spells, revert to non-magic item.
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
            }
            else
            {
                if(!wo.UiEffects.HasValue) // Elemental effects take precendence over magical as it is more important to know the element of a weapon than if it has spells.
                    wo.UiEffects = UiEffects.Magical;

                var maxBaseMana = GetMaxBaseMana(wo);

                wo.ManaRate = CalculateManaRate(maxBaseMana);

                var maxSpellMana = maxBaseMana;

                if (wo.SpellDID != null)
                {
                    var spell = new Server.Entity.Spell(wo.SpellDID.Value);

                    var castableMana = (int)spell.BaseMana * 5;

                    if (castableMana > maxSpellMana)
                        maxSpellMana = castableMana;
                }

                wo.ItemMaxMana = RollItemMaxMana(wo, roll, maxSpellMana);
                wo.ItemCurMana = wo.ItemMaxMana;

                CalculateSpellcraft(wo, roll.AllSpells, true, out roll.MinSpellcraft, out roll.MaxSpellcraft, out roll.RolledSpellCraft);
                AddActivationRequirements(wo, profile, roll);
            }
        }

        /// <summary>
        /// Returns the maximum BaseMana from the spells in item's spellbook
        /// </summary>
        public static int GetMaxBaseMana(WorldObject wo)
        {
            var maxBaseMana = 0;

            if (wo.SpellDID != null)
            {
                var spell = new Server.Entity.Spell(wo.SpellDID.Value);

                if (spell.BaseMana > maxBaseMana)
                    maxBaseMana = (int)spell.BaseMana;
            }

            if (wo.ProcSpell != null)
            {
                var spell = new Server.Entity.Spell(wo.ProcSpell.Value);

                if (spell.BaseMana > maxBaseMana)
                    maxBaseMana = (int)spell.BaseMana;
            }

            if (wo.Biota.PropertiesSpellBook != null)
            {
                foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                {
                    var spell = new Server.Entity.Spell(spellId);

                    if (spell.BaseMana > maxBaseMana)
                        maxBaseMana = (int)spell.BaseMana;
                }
            }

            return maxBaseMana;
        }

        /// <summary>
        /// Rolls the ItemMaxMana for an object
        /// </summary>
        private static int RollItemMaxMana(WorldObject wo, TreasureRoll roll, int maxSpellMana)
        {
            // verified matches up with magloot eor logs

            var workmanship = WorkmanshipChance.GetModifier(wo.ItemWorkmanship - 1);

            (int min, int max) range;

            if (roll.IsClothing || roll.IsArmor || roll.IsWeapon || roll.IsDinnerware)
            {
                range.min = 6;
                range.max = 15;
            }
            else if (roll.IsJewelry)
            {
                // includes crowns
                range.min = 12;
                range.max = 20;
            }
            else if (roll.IsGem)
            {
                range.min = 1;
                range.max = 1;
            }
            else
            {
                log.Error($"RollItemMaxMana({wo.Name}, {roll.ItemType}, {maxSpellMana}) - unknown item type");
                return 1;
            }

            var rng = ThreadSafeRandom.Next(range.min, range.max);

            return (int)Math.Ceiling(maxSpellMana * workmanship * rng);
        }

        /// <summary>
        /// Calculates the ManaRate for an item
        /// </summary>
        public static float CalculateManaRate(int maxBaseMana)
        {
            if (maxBaseMana <= 0)
                maxBaseMana = 1;

            // verified with eor data
            return -1.0f / (float)Math.Ceiling(1200.0f / maxBaseMana);
        }

        public static int GetSpellPower(Server.Entity.Spell spell)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                switch (spell.Formula.Level)
                {
                    case 1: return 20; // EoR is 1
                    case 2: return 50; // EoR is 50
                    case 3: return 75; // EoR is 100
                    case 4: return 125; // EoR is 150
                    case 5: return 150; // EoR is 200
                    case 6: return 180; // EoR is 250
                    default:
                    case 7: return 200; // EoR is 300
                }
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                switch (spell.Formula.Level)
                {
                    case 1: return 20; // EoR is 1
                    case 2: return 75; // EoR is 50
                    case 3: return 130; // EoR is 100
                    case 4: return 160; // EoR is 150
                    case 5: return 190; // EoR is 200
                    case 6: return 220; // EoR is 250
                    default:
                    case 7: return 250; // EoR is 300
                }
            }
            else
                return (int)spell.Power;
        }

        /// <summary>
        /// Returns the maximum power from the spells in item's SpellDID / spellbook / ProcSpell
        /// </summary>
        public static int GetMaxSpellPower(WorldObject wo)
        {
            var maxSpellPower = 0;

            var spells = new List<SpellId>();
            if (!wo.BaseItemDifficultyOverride.HasValue)
            {
                if (wo.SpellDID != null)
                    spells.Add((SpellId)wo.SpellDID.Value);

                if (wo.ProcSpell != null)
                    spells.Add((SpellId)wo.ProcSpell.Value);

                if (wo.Biota.PropertiesSpellBook != null)
                {
                    foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                    {
                        spells.Add((SpellId)spellId);
                    }
                }
            }
            else
            {
                if (wo.ExtraSpellsList == null)
                    return 0;

                var extraSpells = wo.ExtraSpellsList.Split(",");
                foreach (var spellIdString in extraSpells)
                {
                    if (int.TryParse(spellIdString, out var spellId))
                        spells.Add((SpellId)spellId);
                }
            }

            foreach (var spellId in spells)
            {
                var spell = new Server.Entity.Spell(spellId);

                int spellPower = GetSpellPower(spell);
                if (spellPower > maxSpellPower)
                    maxSpellPower = spellPower;
            }

            return maxSpellPower;
        }

        private static void AddActivationRequirements(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.Infiltration)
                TryMutate_ItemSkillLimit(wo, roll); // ItemSkill/LevelLimit

            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
            {
                TryMutate_HeritageRequirement(wo, profile, roll);
                TryMutate_AllegianceRequirement(wo, profile, roll);
            }

            CalculateArcaneLore(wo, roll.AllSpells, roll.LifeCreatureEnchantments, roll.Cantrips, roll.MinSpellcraft, roll.MaxSpellcraft, roll.RolledSpellCraft, true, out roll.MinArcaneLore, out roll.MaxArcaneLore, out roll.RolledArcaneLore);
        }

        private static bool TryMutate_HeritageRequirement(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (wo.Biota.PropertiesSpellBook == null && (wo.SpellDID ?? 0) == 0 && (wo.ProcSpell ?? 0) == 0)
                return false;

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (rng < 0.05)
            {
                if(roll.Heritage == TreasureHeritageGroup.Invalid)
                    roll.Heritage = (TreasureHeritageGroup)ThreadSafeRandom.Next(1, 3);

                switch (roll.Heritage)
                {
                    case TreasureHeritageGroup.Aluvian:
                        wo.HeritageGroup = HeritageGroup.Aluvian;
                        wo.ItemHeritageGroupRestriction = "Aluvian";
                        break;

                    case TreasureHeritageGroup.Gharundim:
                        wo.HeritageGroup = HeritageGroup.Gharundim;
                        wo.ItemHeritageGroupRestriction = "Gharu'ndim";
                        break;

                    case TreasureHeritageGroup.Sho:
                        wo.HeritageGroup = HeritageGroup.Sho;
                        wo.ItemHeritageGroupRestriction = "Sho";
                        break;
                }
                return true;
            }
            return false;
        }

        private static bool TryMutate_AllegianceRequirement(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (wo.Biota.PropertiesSpellBook == null && (wo.SpellDID ?? 0) == 0 && (wo.ProcSpell ?? 0) == 0)
                return false;

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (rng < (roll.Wcid == Enum.WeenieClassName.crown ? 0.25 : 0.05)) // Crowns are special and have allegiance requirements more often.
            {
                wo.ItemAllegianceRankLimit = AllegianceRankChance.Roll(profile.Tier);
                return true;
            }
            return false;
        }

        private static bool TryMutate_ItemSkillLimit(WorldObject wo, TreasureRoll roll)
        {
            if (!RollItemSkillLimit(roll))
                return false;

            wo.ItemSkillLevelLimit = wo.ItemSpellcraft + 20;

            var skill = Skill.None;

            if (roll.IsMeleeWeapon || roll.IsMissileWeapon)
            {
                skill = wo.WeaponSkill;
                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && wo.WieldRequirements == WieldRequirement.RawSkill && wo.WieldDifficulty > wo.ItemSkillLevelLimit)
                    wo.ItemSkillLevelLimit = wo.WieldDifficulty + ThreadSafeRandom.Next(5, 20);
            }
            else if (roll.IsArmor)
            {
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

                if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                {
                    if (rng < 0.5f)
                    {
                        skill = Skill.MeleeDefense;
                    }
                    else
                    {
                        skill = Skill.MissileDefense;
                        wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 0.7f);
                    }
                }
                else
                {
                    if (!roll.IsClothArmor)
                    {
                        if (rng < 0.33f)
                        {
                            skill = Skill.MeleeDefense;
                        }
                        else if (rng < 0.66f)
                        {
                            skill = Skill.MissileDefense;
                            wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 3f / 5f);
                        }
                        else
                        {
                            if (wo.IsShield)
                            {
                                skill = Skill.Shield;
                                wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 3f / 4f);
                            }
                            else
                            {
                                skill = Skill.Armor;
                                wo.ItemSkillLevelLimit = (int)(((wo.ItemSkillLevelLimit * 3f) + 30) / 4f);
                            }
                        }
                    }
                    else
                    {
                        if (rng < 0.33f)
                        {
                            skill = Skill.MagicDefense;
                            wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 3f / 7f);
                        }
                        else if (rng < 0.66f)
                        {
                            skill = Skill.ManaConversion;
                            wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 3f / 6f);
                        }
                        else
                        {
                            if (roll.LifeCreatureEnchantments != null)
                            {
                                foreach (var spellId in roll.LifeCreatureEnchantments)
                                {
                                    if (SpellLevelProgression.GetLevel1SpellId(spellId) == SpellId.WarMagicMasteryOther1)
                                    {
                                        skill = Skill.WarMagic;
                                        break;
                                    }
                                    else if (SpellLevelProgression.GetLevel1SpellId(spellId) == SpellId.LifeMagicMasteryOther1)
                                    {
                                        skill = Skill.LifeMagic;
                                        break;
                                    }
                                }
                            }

                            if (skill == Skill.None)
                            {
                                var skillRoll = ThreadSafeRandom.Next(0, 1);
                                if (skillRoll == 0)
                                    skill = Skill.WarMagic;
                                else
                                    skill = Skill.LifeMagic;
                            }

                            wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 3f / 4f);
                        }
                    }
                }
            }
            else
            {
                log.Error($"RollItemSkillLimit({wo.Name}, {roll.ItemType}) - unknown item type");
                return false;
            }

            wo.ItemSkillLimit = wo.ConvertToMoASkill(skill);
            return true;
        }

        private static bool RollItemSkillLimit(TreasureRoll roll)
        {
            if (roll.IsMeleeWeapon || roll.IsMissileWeapon)
            {
                return true;
            }
            else if (roll.IsArmor)
            {
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

                return rng < 0.55f;
            }
            return false;
        }
    }
}
