using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;
using System.Collections.Generic;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void MutateGem(WorldObject wo, TreasureDeath profile, bool isMagical, TreasureRoll roll = null)
        {
            // workmanship
            wo.ItemWorkmanship = WorkmanshipChance.Roll(profile.Tier, profile.LootQualityMod);

            // item color
            MutateColor(wo);

            if (!isMagical)
            {
                // TODO: verify if this is needed
                wo.ItemUseable = Usable.No;
                wo.SpellDID = null;
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;
                wo.ItemSkillLevelLimit = null;
                wo.ManaRate = null;
            }
            else
            {
                AssignMagic_Gem(wo, profile, roll);

                wo.UiEffects = UiEffects.Magical;

                wo.ItemUseable = Usable.Contained;
            }

            // item value
            if (wo.HasMutateFilter(MutateFilter.Value))
                MutateValue(wo, profile.Tier, roll);

            // long desc
            wo.LongDesc = GetLongDesc(wo);
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && isMagical && wo.LongDesc != wo.Name)
                wo.Name = wo.LongDesc;
        }

        private static bool AssignMagic_Gem(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // TODO: move to standard AssignMagic() pipeline

            var spell = SpellSelectionTable.Roll(1);

            var spellLevel = SpellLevelChance.Roll(profile.Tier);

            var spellLevels = SpellLevelProgression.GetSpellLevels(spell);

            if (spellLevels == null || spellLevels.Count != 8)
            {
                log.Error($"AssignMagic_Gem({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - unknown spell {spell}");
                return false;
            }

            var finalSpellId = spellLevels[spellLevel - 1];

            wo.SpellDID = (uint)finalSpellId;

            var _spell = new Server.Entity.Spell(finalSpellId);

            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                var castableMana = (int)_spell.BaseMana * 5;

                wo.ItemMaxMana = RollItemMaxMana(wo, roll, castableMana);
                wo.ItemCurMana = wo.ItemMaxMana;

                // verified
                wo.ItemManaCost = castableMana;
            }
            else
            {
                wo.MaxStructure = RollItemMaxStructure(wo);
                wo.Structure = wo.MaxStructure;
            }

            roll.AllSpells = new List<SpellId>();
            roll.AllSpells.Add(finalSpellId);

            roll.LifeCreatureEnchantments = new List<SpellId>();
            roll.LifeCreatureEnchantments.Add(finalSpellId);

            CalculateSpellcraft(wo, roll.AllSpells, true, out roll.MinSpellcraft, out roll.MaxSpellcraft, out roll.RolledSpellCraft);
            AddActivationRequirements(wo, profile, roll);

            return true;
        }

        public static ushort RollItemMaxStructure(WorldObject wo)
        {
            var maxStructure = (ushort)ThreadSafeRandom.Next(10, 20 + (int)(wo.ItemWorkmanship * 2));

            return maxStructure;
        }

        private static void MutateValue_Gem(WorldObject wo)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                var materialMod = MaterialTable.GetValueMod(wo.MaterialType);

                var workmanshipMod = WorkmanshipChance.GetModifier(wo.ItemWorkmanship);

                wo.Value = (int)(wo.Value * materialMod * workmanshipMod);
            }
            else
            {
                var gemValue = GemMaterialChance.GemValue(wo.MaterialType);
                var materialMod = MaterialTable.GetValueMod(wo.MaterialType);

                var workmanshipMod = WorkmanshipChance.GetModifier(wo.ItemWorkmanship);

                wo.Value = (int)(gemValue * materialMod * workmanshipMod);
            }
        }
    }
}
