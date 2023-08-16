using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void AssignMagic(WorldObject wo, TreasureDeath profile, TreasureRoll roll, bool isArmor = false)
        {
            int numSpells = 0;
            int numEpics = 0;
            int numLegendaries = 0;

            if (roll == null)
            {
                // previous method
                if (!AssignMagic_Spells(wo, profile, isArmor, out numSpells, out numEpics, out numLegendaries))
                    return;
            }
            else
            {
                // new method
                if (!AssignMagic_New(wo, profile, roll, out numSpells))
                    return;
            }

            if (numSpells == 0 && wo.SpellDID == null && wo.ProcSpell == null)
            {
                // we ended up without any spells, revert to non-magic item.
                wo.ItemManaCost = null;
                wo.ItemMaxMana = null;
                wo.ItemCurMana = null;
                wo.ItemSpellcraft = null;
                wo.ItemDifficulty = null;

                if (roll.IsClothArmor) // Non-magical robes do not need level requirements
                {
                    wo.RemoveProperty(PropertyInt.WieldRequirements);
                    wo.RemoveProperty(PropertyInt.WieldSkillType);
                    wo.RemoveProperty(PropertyInt.WieldDifficulty);
                }
            }
            else
            {
                if(!wo.UiEffects.HasValue) // Elemental effects take precendence over magical as it is more important to know the element of a weapon than if it has spells.
                    wo.UiEffects = UiEffects.Magical;

                var maxBaseMana = GetMaxBaseMana(wo);

                wo.ManaRate = CalculateManaRate(maxBaseMana);

                if (roll == null)
                {
                    wo.ItemMaxMana = RollItemMaxMana(profile.Tier, numSpells);
                    wo.ItemCurMana = wo.ItemMaxMana;

                    wo.ItemSpellcraft = RollSpellcraft(wo);
                    wo.ItemDifficulty = RollItemDifficulty(wo, numEpics, numLegendaries);
                }
                else
                {
                    var maxSpellMana = maxBaseMana;

                    if (wo.SpellDID != null)
                    {
                        var spell = new Server.Entity.Spell(wo.SpellDID.Value);

                        var castableMana = (int)spell.BaseMana * 5;

                        if (castableMana > maxSpellMana)
                            maxSpellMana = castableMana;
                    }

                    wo.ItemMaxMana = RollItemMaxMana_New(wo, roll, maxSpellMana);
                    wo.ItemCurMana = wo.ItemMaxMana;

                    wo.ItemSpellcraft = RollSpellcraft(wo, roll);

                    AddActivationRequirements(wo, profile, roll);
                }
            }
        }

        private static bool AssignMagic_Spells(WorldObject wo, TreasureDeath profile, bool isArmor, out int numSpells, out int epicCantrips, out int legendaryCantrips)
        {
            SpellId[][] spells;
            SpellId[][] cantrips;

            int lowSpellTier = GetLowSpellTier(profile.Tier);
            int highSpellTier = GetHighSpellTier(profile.Tier);

            switch (wo.WeenieType)
            {
                case WeenieType.Clothing:
                    spells = ArmorSpells.Table;
                    cantrips = ArmorCantrips.Table;
                    break;
                case WeenieType.Caster:
                    spells = WandSpells.Table;
                    cantrips = WandCantrips.Table;
                    break;
                case WeenieType.Generic:
                    spells = JewelrySpells.Table;
                    cantrips = JewelryCantrips.Table;
                    break;
                case WeenieType.MeleeWeapon:
                    spells = MeleeSpells.Table;
                    cantrips = MeleeCantrips.Table;
                    break;
                case WeenieType.MissileLauncher:
                    spells = MissileSpells.Table;
                    cantrips = MissileCantrips.Table;
                    break;
                default:
                    spells = null;
                    cantrips = null;
                    break;
            }

            if (wo.IsShield)
            {
                spells = ArmorSpells.Table;
                cantrips = ArmorCantrips.Table;
            }

            numSpells = 0;
            epicCantrips = 0;
            legendaryCantrips = 0;

            if (spells == null || cantrips == null)
                return false;

            // Refactor 3/2/2020 - HQ
            // Magic stats
            numSpells = GetSpellDistribution(profile, out int minorCantrips, out int majorCantrips, out epicCantrips, out legendaryCantrips);
            int numCantrips = minorCantrips + majorCantrips + epicCantrips + legendaryCantrips;

            if (numSpells - numCantrips > 0)
            {
                var indices = Enumerable.Range(0, spells.Length).ToList();

                for (int i = 0; i < numSpells - numCantrips; i++)
                {
                    var idx = ThreadSafeRandom.Next(0, indices.Count - 1);
                    int col = ThreadSafeRandom.Next(lowSpellTier - 1, highSpellTier - 1);
                    SpellId spellID = spells[indices[idx]][col];
                    indices.RemoveAt(idx);
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
            }

            // Per discord discussions: ALL armor/shields if it had any spells, had an Impen spell
            if (isArmor)
            {
                var impenSpells = SpellLevelProgression.Impenetrability;

                // Ensure that one of the Impen spells was not already added
                bool impenFound = false;
                for (int i = 0; i < 8; i++)
                {
                    if (wo.Biota.SpellIsKnown((int)impenSpells[i], wo.BiotaDatabaseLock))
                    {
                        impenFound = true;
                        break;
                    }
                }
                if (!impenFound)
                {
                    int col = ThreadSafeRandom.Next(lowSpellTier - 1, highSpellTier - 1);
                    SpellId spellID = impenSpells[col];
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
            }

            if (numCantrips > 0)
            {
                var indices = Enumerable.Range(0, cantrips.Length).ToList();

                // minor cantrips
                for (var i = 0; i < minorCantrips; i++)
                {
                    var idx = ThreadSafeRandom.Next(0, indices.Count - 1);
                    SpellId spellID = cantrips[indices[idx]][0];
                    indices.RemoveAt(idx);
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
                // major cantrips
                for (var i = 0; i < majorCantrips; i++)
                {
                    var idx = ThreadSafeRandom.Next(0, indices.Count - 1);
                    SpellId spellID = cantrips[indices[idx]][1];
                    indices.RemoveAt(idx);
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
                // epic cantrips
                for (var i = 0; i < epicCantrips; i++)
                {
                    var idx = ThreadSafeRandom.Next(0, indices.Count - 1);
                    SpellId spellID = cantrips[indices[idx]][2];
                    indices.RemoveAt(idx);
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
                // legendary cantrips
                for (var i = 0; i < legendaryCantrips; i++)
                {
                    var idx = ThreadSafeRandom.Next(0, indices.Count - 1);
                    SpellId spellID = cantrips[indices[idx]][3];
                    indices.RemoveAt(idx);
                    wo.Biota.GetOrAddKnownSpell((int)spellID, wo.BiotaDatabaseLock, out _);
                }
            }
            return true;
        }

        private static int GetSpellDistribution(TreasureDeath profile, out int numMinors, out int numMajors, out int numEpics, out int numLegendaries)
        {
            int numNonCantrips = 0;

            numMinors = 0;
            numMajors = 0;
            numEpics = 0;
            numLegendaries = 0;

            int nonCantripChance = ThreadSafeRandom.Next(1, 100000);

            numMinors = GetNumMinorCantrips(profile); // All tiers have a chance for at least one minor cantrip
            numMajors = GetNumMajorCantrips(profile);
            numEpics = GetNumEpicCantrips(profile);
            numLegendaries = GetNumLegendaryCantrips(profile);

            //  Fixing the absurd amount of spells on items - HQ 6/21/2020
            //  From Mags Data all tiers have about the same chance for a given number of spells on items.  This is the ratio for magical items.
            //  1 Spell(s) - 46.410 %
            //  2 Spell(s) - 27.040 %
            //  3 Spell(s) - 17.850 %
            //  4 Spell(s) - 6.875 %
            //  5 Spell(s) - 1.525 %
            //  6 Spell(s) - 0.235 %
            //  7 Spell(s) - 0.065 %

            if (nonCantripChance <= 46410)
                numNonCantrips = 1;
            else if (nonCantripChance <= 73450)
                numNonCantrips = 2;
            else if (nonCantripChance <= 91300)
                numNonCantrips = 3;
            else if (nonCantripChance <= 98175)
                numNonCantrips = 4;
            else if (nonCantripChance <= 99700)
                numNonCantrips = 5;
            else if (nonCantripChance <= 99935)
                numNonCantrips = 6;
            else
                numNonCantrips = 7;

            return numNonCantrips + numMinors + numMajors + numEpics + numLegendaries;
        }

        private static int GetNumMinorCantrips(TreasureDeath profile)
        {
            int numMinors = 0;

            var dropRate = PropertyManager.GetDouble("minor_cantrip_drop_rate").Item;
            if (dropRate <= 0)
                return 0;

            var dropRateMod = 1.0 / dropRate;

            double lootQualityMod = 1.0f;
            if (PropertyManager.GetBool("loot_quality_mod").Item && profile.LootQualityMod > 0 && profile.LootQualityMod < 1)
                lootQualityMod = 1.0f - profile.LootQualityMod;

            switch (profile.Tier)
            {
                case 1:
                    if (ThreadSafeRandom.Next(1, (int)(100 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 1;
                    break;
                case 2:
                case 3:
                    if (ThreadSafeRandom.Next(1, (int)(50 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(250 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 2;
                    break;
                case 4:
                case 5:
                    if (ThreadSafeRandom.Next(1, (int)(50 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(250 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 2;
                    if (ThreadSafeRandom.Next(1, (int)(1000 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 3;
                    break;
                case 6:
                case 7:
                default:
                    if (ThreadSafeRandom.Next(1, (int)(50 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(250 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 2;
                    if (ThreadSafeRandom.Next(1, (int)(1000 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 3;
                    if (ThreadSafeRandom.Next(1, (int)(5000 * dropRateMod * lootQualityMod)) == 1)
                        numMinors = 4;
                    break;
            }

            return numMinors;
        }

        private static int GetNumMajorCantrips(TreasureDeath profile)
        {
            int numMajors = 0;

            var dropRate = PropertyManager.GetDouble("major_cantrip_drop_rate").Item;
            if (dropRate <= 0)
                return 0;

            var dropRateMod = 1.0 / dropRate;

            double lootQualityMod = 1.0f;
            if (PropertyManager.GetBool("loot_quality_mod").Item && profile.LootQualityMod > 0 && profile.LootQualityMod < 1)
                lootQualityMod = 1.0f - profile.LootQualityMod;

            switch (profile.Tier)
            {
                case 1:
                    numMajors = 0;
                    break;
                case 2:
                    if (ThreadSafeRandom.Next(1, (int)(500 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 1;
                    break;
                case 3:
                    if (ThreadSafeRandom.Next(1, (int)(500 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(10000 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 2;
                    break;
                case 4:
                case 5:
                case 6:
                    if (ThreadSafeRandom.Next(1, (int)(500 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(5000 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 2;
                    break;
                case 7:
                default:
                    if (ThreadSafeRandom.Next(1, (int)(500 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 1;
                    if (ThreadSafeRandom.Next(1, (int)(5000 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 2;
                    if (ThreadSafeRandom.Next(1, (int)(15000 * dropRateMod * lootQualityMod)) == 1)
                        numMajors = 3;
                    break;
            }

            return numMajors;
        }

        private static int GetNumEpicCantrips(TreasureDeath profile)
        {
            int numEpics = 0;

            if (profile.Tier < 7)
                return 0;

            var dropRate = PropertyManager.GetDouble("epic_cantrip_drop_rate").Item;
            if (dropRate <= 0)
                return 0;

            var dropRateMod = 1.0 / dropRate;

            double lootQualityMod = 1.0f;
            if (PropertyManager.GetBool("loot_quality_mod").Item && profile.LootQualityMod > 0 && profile.LootQualityMod < 1)
                lootQualityMod = 1.0f - profile.LootQualityMod;

            // 25% base chance for no epics for tier 7
            if (ThreadSafeRandom.Next(1, 4) > 1)
            {
                // 1% chance for 1 Epic, 0.1% chance for 2 Epics,
                // 0.01% chance for 3 Epics, 0.001% chance for 4 Epics 
                if (ThreadSafeRandom.Next(1, (int)(100 * dropRateMod * lootQualityMod)) == 1)
                    numEpics = 1;
                if (ThreadSafeRandom.Next(1, (int)(1000 * dropRateMod * lootQualityMod)) == 1)
                    numEpics = 2;
                if (ThreadSafeRandom.Next(1, (int)(10000 * dropRateMod * lootQualityMod)) == 1)
                    numEpics = 3;
                if (ThreadSafeRandom.Next(1, (int)(100000 * dropRateMod * lootQualityMod)) == 1)
                    numEpics = 4;
            }

            return numEpics;
        }

        private static int GetNumLegendaryCantrips(TreasureDeath profile)
        {
            int numLegendaries = 0;

            if (profile.Tier < 8)
                return 0;

            var dropRate = PropertyManager.GetDouble("legendary_cantrip_drop_rate").Item;
            if (dropRate <= 0)
                return 0;

            var dropRateMod = 1.0 / dropRate;

            double lootQualityMod = 1.0f;
            if (PropertyManager.GetBool("loot_quality_mod").Item && profile.LootQualityMod > 0 && profile.LootQualityMod < 1)
                lootQualityMod = 1.0f - profile.LootQualityMod;

            // 1% chance for a legendary, 0.02% chance for 2 legendaries
            if (ThreadSafeRandom.Next(1, (int)(100 * dropRateMod * lootQualityMod)) == 1)
                numLegendaries = 1;
            if (ThreadSafeRandom.Next(1, (int)(500 * dropRateMod * lootQualityMod)) == 1)
                numLegendaries = 2;

            return numLegendaries;
        }

        private static int GetLowSpellTier(int tier)
        {
            int lowSpellTier;
            switch (tier)
            {
                case 1:
                    lowSpellTier = 1;
                    break;
                case 2:
                    lowSpellTier = 3;
                    break;
                case 3:
                    lowSpellTier = 4;
                    break;
                case 4:
                    lowSpellTier = 5;
                    break;
                case 5:
                case 6:
                    lowSpellTier = 6;
                    break;
                default:
                    lowSpellTier = 7;
                    break;
            }

            return lowSpellTier;
        }

        private static int GetHighSpellTier(int tier)
        {
            int highSpellTier;
            switch (tier)
            {
                case 1:
                    highSpellTier = 3;
                    break;
                case 2:
                    highSpellTier = 5;
                    break;
                case 3:
                case 4:
                    highSpellTier = 6;
                    break;
                case 5:
                case 6:
                    highSpellTier = 7;
                    break;
                default:
                    highSpellTier = 8;
                    break;
            }

            return highSpellTier;
        }

        private static void Shuffle(int[] array)
        {
            // verified even distribution
            for (var i = 0; i < array.Length; i++)
            {
                var idx = ThreadSafeRandom.Next(i, array.Length - 1);

                var temp = array[idx];
                array[idx] = array[i];
                array[i] = temp;
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

            if (wo.Biota.PropertiesSpellBook != null)
            {
                foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                {
                    var spell = new Server.Entity.Spell(spellId);

                    if (spell.BaseMana > maxBaseMana)
                        maxBaseMana = (int)spell.BaseMana;
                }
            }

            if (wo.ProcSpell != null)
            {
                var spell = new Server.Entity.Spell(wo.ProcSpell.Value);

                if (spell.BaseMana > maxBaseMana)
                    maxBaseMana = (int)spell.BaseMana;
            }

            return maxBaseMana;
        }

        // old table / method

        private static readonly List<(int min, int max)> itemMaxMana_RandomRange = new List<(int min, int max)>()
        {
            (200,  400),    // T1
            (400,  600),    // T2
            (600,  800),    // T3
            (800,  1000),   // T4
            (1000, 1200),   // T5
            (1200, 1400),   // T6
            (1400, 1600),   // T7
            (1600, 1800),   // T8
        };

        private static int RollItemMaxMana(int tier, int numSpells)
        {
            var range = itemMaxMana_RandomRange[tier - 1];

            var rng = ThreadSafeRandom.Next(range.min, range.max);

            return rng * numSpells;
        }

        /// <summary>
        /// Rolls the ItemMaxMana for an object
        /// </summary>
        private static int RollItemMaxMana_New(WorldObject wo, TreasureRoll roll, int maxSpellMana)
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

        // old method / based on item type

        public static int RollSpellcraft(WorldObject wo)
        {
            var maxSpellPower = GetMaxSpellPower(wo);

            (float min, float max) range = (1.0f, 1.0f);

            switch (wo.ItemType)
            {
                case ItemType.Armor:
                case ItemType.Clothing:
                case ItemType.Jewelry:

                case ItemType.MeleeWeapon:
                case ItemType.MissileWeapon:
                case ItemType.Caster:

                    range = (0.9f, 1.1f);
                    break;
            }

            var rng = ThreadSafeRandom.Next(range.min, range.max);

            var spellcraft = (int)Math.Ceiling(maxSpellPower * rng);

            // retail was capped at 370
            spellcraft = Math.Min(spellcraft, 370);

            return spellcraft;
        }

        public static int RollSpellcraftForExtraSpells(WorldObject wo)
        {
            var maxSpellPower = GetMaxSpellPowerForExtraSpells(wo);

            (float min, float max) range = (1.0f, 1.0f);

            switch (wo.ItemType)
            {
                case ItemType.Armor:
                case ItemType.Clothing:
                case ItemType.Jewelry:

                case ItemType.MeleeWeapon:
                case ItemType.MissileWeapon:
                case ItemType.Caster:

                    range = (0.9f, 1.1f);
                    break;
            }

            var rng = ThreadSafeRandom.Next(range.min, range.max);

            var spellcraft = (int)Math.Ceiling(maxSpellPower * rng);

            // retail was capped at 370
            spellcraft = Math.Min(spellcraft, 370);

            return spellcraft;
        }

        // new method / based on treasure roll

        private static int RollSpellcraft(WorldObject wo, TreasureRoll roll)
        {
            var maxSpellPower = GetMaxSpellPower(wo);

            (float min, float max) range = (1.0f, 1.0f);

            if (roll.IsClothing || roll.IsArmor || roll.IsWeapon || roll.IsJewelry || roll.IsDinnerware)
            {
                range.min = 0.9f;
                range.max = 1.1f;
            }
            else if (!roll.IsGem)
            {
                log.Error($"RollSpellcraft({wo.Name}, {roll.ItemType}) - unknown item type");
            }

            var rng = ThreadSafeRandom.Next(range.min, range.max);

            var spellcraft = (int)Math.Ceiling(maxSpellPower * rng);

            // retail was capped at 370
            spellcraft = Math.Min(spellcraft, 370);

            return spellcraft;
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

            if (wo.SpellDID != null)
            {
                var spell = new Server.Entity.Spell(wo.SpellDID.Value);

                int spellPower = GetSpellPower(spell);
                if (spellPower > maxSpellPower)
                    maxSpellPower = spellPower;
            }

            if (wo.Biota.PropertiesSpellBook != null)
            {
                foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                {
                    var spell = new Server.Entity.Spell(spellId);

                    int spellPower = GetSpellPower(spell);
                    if (spellPower > maxSpellPower)
                        maxSpellPower = spellPower;
                }
            }

            if (wo.ProcSpell != null)
            {
                var spell = new Server.Entity.Spell(wo.ProcSpell.Value);

                int spellPower = GetSpellPower(spell);
                if (spellPower > maxSpellPower)
                    maxSpellPower = spellPower;
            }

            return maxSpellPower;
        }

        public static int GetMaxSpellPowerForExtraSpells(WorldObject wo)
        {
            if (wo.ExtraSpellsList == null)
                return 0;

            var maxSpellPower = 0;

            var extraSpells = wo.ExtraSpellsList.Split(",");
            foreach (var spellIdString in extraSpells)
            {
                if (int.TryParse(spellIdString, out var spellId))
                {
                    var spell = new Server.Entity.Spell(spellId);

                    int spellPower = GetSpellPower(spell);
                    if (spellPower > maxSpellPower)
                        maxSpellPower = spellPower;
                }
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
                if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                    TryMutate_AllegianceRequirement(wo, profile, roll);
            }

            // Arcane Lore / ItemDifficulty
            wo.ItemDifficulty = CalculateArcaneLore(wo, roll);
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
                    if (rng < 0.33f)
                    {
                        skill = Skill.MeleeDefense;
                    }
                    else if (rng < 0.66f)
                    {
                        skill = Skill.MissileDefense;
                        wo.ItemSkillLevelLimit = (int)(wo.ItemSkillLevelLimit * 0.7f);
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
            else if (roll.IsArmor && !roll.IsClothArmor)
            {
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

                return rng < 0.55f;
            }
            return false;
        }

        // previous method - replaces itemSkillLevelLimit w/ WieldDifficulty,
        // and does not use treasure roll

        private static int RollItemDifficulty(WorldObject wo, int numEpics, int numLegendaries)
        {
            // - # of spells on item
            var num_spells = wo.Biota.PropertiesSpellBook.Count();

            if (wo.ProcSpell != null)
                num_spells++;

            var spellAddonChance = num_spells * (20.0f / (num_spells + 2.0f));
            var spellAddon = (float)ThreadSafeRandom.Next(1.0f, spellAddonChance) * num_spells;

            // - # of epics / legendaries on item
            var epicAddon = numEpics > 0 ? ThreadSafeRandom.Next(1, 5) * numEpics : 0;
            var legAddon = numLegendaries > 0 ? ThreadSafeRandom.Next(5, 10) * numLegendaries : 0;

            // wield difficulty - skill requirement
            var wieldFactor = 0.0f;

            if (wo.WieldDifficulty != null && wo.WieldRequirements == WieldRequirement.RawSkill)
                wieldFactor = wo.WieldDifficulty.Value / 3.0f;

            var itemDifficulty = wo.ItemSpellcraft.Value - wieldFactor;

            if (itemDifficulty < 0)
                itemDifficulty = 0;

            return (int)Math.Floor(itemDifficulty + spellAddon + epicAddon + legAddon);
        }

        /// <summary>
        /// Calculates the Arcane Lore requirement / ItemDifficulty
        /// </summary>
        private static int CalculateArcaneLore(WorldObject wo, TreasureRoll roll)
        {
            // spellcraft - (itemSkillLevelLimit / 2.0f) + creatureLifeEnchantments + cantrips

            var spellcraft = wo.ItemSpellcraft.Value;

            // - mutates 100% of the time for melee / missile weapons
            // - mutates 55% of the time for armor
            // - mutates 0% of the time for all other item types
            var itemSkillLevelFactor = 0.0f;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 10.0f;
            }
            else
            {
                if (wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 2.0f;
            }

            var fArcane = spellcraft - itemSkillLevelFactor;

            if (wo.ItemAllegianceRankLimit > 0)
                fArcane -= (float)wo.ItemAllegianceRankLimit * 10.0f;

            if (wo.HeritageGroup != 0)
                fArcane -= fArcane * 0.2f;

            if (fArcane < 0)
                fArcane = 0;

            return (int)Math.Floor(fArcane + roll.ItemDifficulty);
        }
    }
}
