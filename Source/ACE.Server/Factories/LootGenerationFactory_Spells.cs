using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories
{
    public partial class LootGenerationFactory
    {
        private static List<SpellId> RollSpells(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            var spells = new List<SpellId>();
            roll.AllSpells = new List<SpellId>();

            // crowns, which are classified as TreasureItemType.Jewelry, should also be getting item spells
            if (roll.HasArmorLevel(wo) || roll.IsClothArmor || roll.IsWeapon)
            {
                roll.ItemEnchantments = RollItemEnchantments(wo, profile, roll);

                if (roll.ItemEnchantments != null)
                    spells.AddRange(roll.ItemEnchantments);
            }

            roll.LifeCreatureEnchantments = RollLifeCreatureEnchantments(wo, profile, roll);

            if (roll.LifeCreatureEnchantments != null)
                spells.AddRange(roll.LifeCreatureEnchantments);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR || (!roll.IsJewelry && !roll.IsClothing))
            {
                roll.Cantrips = RollCantrips(wo, profile, roll);

                if (roll.Cantrips != null)
                    spells.AddRange(roll.Cantrips);
            }

            roll.AllSpells.AddRange(spells);
            return spells;
        }

        private static SpellId RollItemProc(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            SpellId procSpellId = SpellId.Undef;

            if (roll.IsMeleeWeapon)
            {
                procSpellId = MeleeSpells.RollProc(profile);
            }
            else if (roll.IsMissileWeapon)
            {
               procSpellId = MissileSpells.RollProc(profile);
            }
            else
            {
                log.Error($"RollItemProc({wo.Name}) - item is not melee or missile weapon");
                return SpellId.Undef;
            }

            if(procSpellId != SpellId.Undef)
                return RollProcLevel(wo, profile, procSpellId);
            return SpellId.Undef;
        }

        private static SpellId RollProcLevel(WorldObject wo, TreasureDeath profile, SpellId procSpellId)
        {
            var spellLevel = SpellLevelChance.Roll(profile.Tier);

            var spellLevels = SpellLevelProgression.GetSpellLevels(procSpellId);

            if (spellLevels.Count != 8)
            {
                log.Error($"RollSpellLevels({wo.Name}, {procSpellId}) - spell level progression returned {spellLevels.Count}, expected 8");
                return SpellId.Undef;
            }

            return(spellLevels[spellLevel - 1]);
        }

        private static List<SpellId> RollItemEnchantments(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            List<SpellId> spells = null;


            if (roll.IsClothArmor)
            {
                spells = ClothArmorSpells.Roll(profile);
            }
            else if (roll.HasArmorLevel(wo))
            {
                if(roll.ArmorType != TreasureArmorType.Covenant || Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                    spells = ArmorSpells.Roll(profile, wo.IsShield);
            }
            else if (roll.IsMeleeWeapon)
            {
                spells = MeleeSpells.Roll(profile);
            }
            else if (roll.IsMissileWeapon)
            {
                spells = MissileSpells.Roll(profile);
            }
            else if (roll.IsCaster)
            {
                spells = WandSpells.Roll(wo, profile);
            }
            else
            {
                log.Error($"RollItemSpells({wo.Name}) - item is not clothing / armor / weapon");
                return null;
            }

            if(spells != null)
                return RollSpellLevels(wo, profile, spells);
            else
                return null;
        }

        private static List<SpellId> RollSpellLevels(WorldObject wo, TreasureDeath profile, IEnumerable<SpellId> spells)
        {
            var finalSpells = new List<SpellId>();

            foreach (var spell in spells)
            {
                var spellLevel = SpellLevelChance.Roll(profile.Tier);

                var spellLevels = SpellLevelProgression.GetSpellLevels(spell);

                if (spellLevels.Count != 8)
                {
                    log.Error($"RollSpellLevels({wo.Name}, {spell}) - spell level progression returned {spellLevels.Count}, expected 8");
                    continue;
                }

                finalSpells.Add(spellLevels[spellLevel - 1]);
            }

            return finalSpells;
        }

        private static List<SpellId> RollLifeCreatureEnchantments(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            /*if (wo.SpellSelectionCode == null)
            {
                log.Warn($"RollEnchantments({wo.Name}) - missing spell selection code / PropertyInt.TsysMutationData");
                return null;
            }*/

            // test method: determine spell selection code dynamically
            var spellSelectionCode = GetSpellSelectionCode_Dynamic(wo, roll);

            if (spellSelectionCode == 0)
                return null;

            //Console.WriteLine($"Using spell selection code {spellSelectionCode} for {wo.Name}");

            var numEnchantments = RollNumEnchantments(wo, profile, roll);

            if (numEnchantments <= 0)
                return null;

            var numAttempts = numEnchantments * 3;

            var spells = new HashSet<SpellId>();

            for (var i = 0; i < numAttempts && spells.Count < numEnchantments; i++)
            {
                var spell = SpellSelectionTable.Roll(spellSelectionCode);

                if (spell != SpellId.Undef)
                    spells.Add(spell);
            }

            return RollSpellLevels(wo, profile, spells);
        }

        private static int RollNumEnchantments(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (roll.IsArmor || roll.IsWeapon)
            {
                return RollNumEnchantments_Armor_Weapon(wo, profile, roll);
            }
            // confirmed:
            // - crowns (classified as TreasureItemType.Jewelry) used this table
            // - clothing w/ al also used this table
            else if (roll.IsClothing || roll.IsJewelry || roll.IsDinnerware)
            {
                return RollNumEnchantments_Clothing_Jewelry_Dinnerware(wo, profile, roll);
            }
            else
            {
                log.Warn($"RollNumEnchantments({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - unknown item type");
                return 1;   // gems?
            }
        }

        private static readonly List<float> EnchantmentChances_Armor_MeleeMissileWeapon = new List<float>()
        {
            0.00f,  // T1
            0.05f,  // T2
            0.10f,  // T3
            0.20f,  // T4
            0.40f,  // T5
            0.60f,  // T6
            0.60f,  // T7
            0.60f,  // T8
        };

        private static readonly List<float> EnchantmentChances_Caster = new List<float>()
        {
            0.60f,  // T1
            0.60f,  // T2
            0.60f,  // T3
            0.60f,  // T4
            0.60f,  // T5
            0.75f,  // T6
            0.75f,  // T7
            0.75f,  // T8
        };

        private static int RollNumEnchantments_Armor_Weapon(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            var tierChances = roll.IsCaster ? EnchantmentChances_Caster : EnchantmentChances_Armor_MeleeMissileWeapon;

            var chance = tierChances[profile.Tier - 1];

            var rng = ThreadSafeRandom.NextInterval(profile.LootQualityMod);

            if (rng < chance)
                return 1;
            else
                return 0;
        }

        private static int RollNumEnchantments_Clothing_Jewelry_Dinnerware(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            var chance = 0.1f;

            var rng = ThreadSafeRandom.NextInterval(profile.LootQualityMod);

            if (rng >= chance)
                return 1;
            else if (profile.Tier < 6)
                return 2;

            // tier 6+ has a chance for 3 enchantments
            rng = ThreadSafeRandom.NextInterval(profile.LootQualityMod * 0.1f);

            if (rng >= chance * 0.5f)
                return 2;
            else
                return 3;
        }

        public static void CalculateSpellcraft(WorldObject wo, List<SpellId> allSpellIds, bool updateItem, out int minSpellcraft, out int maxSpellcraft, out int rolledSpellCraft)
        {
            var maxSpellPower = 0;

            if (allSpellIds != null)
            {
                foreach (var spellId in allSpellIds)
                {
                    var spell = new Server.Entity.Spell(spellId);

                    int spellPower = GetSpellPower(spell);
                    if (spellPower > maxSpellPower)
                        maxSpellPower = spellPower;
                }
            }

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

            minSpellcraft = Math.Min((int)Math.Ceiling(maxSpellPower * range.min), 370);
            maxSpellcraft = Math.Min((int)Math.Ceiling(maxSpellPower * range.max), 370);

            // Avoid lowering spellcraft.
            var currentSpellcraft = Math.Max(wo.BaseSpellcraftOverride ?? 1, wo.ItemSpellcraft ?? 1);

            minSpellcraft = Math.Max(currentSpellcraft, minSpellcraft);
            maxSpellcraft = Math.Max(currentSpellcraft, maxSpellcraft);
            rolledSpellCraft = ThreadSafeRandom.Next(minSpellcraft, maxSpellcraft);

            if (updateItem)
                wo.ItemSpellcraft = rolledSpellCraft;
        }

        public static void CalculateArcaneLore(WorldObject wo, List<SpellId> allSpellIds, List<SpellId> lifeCreatureEnchantmentsIds, List<SpellId> cantripIds, int minSpellcraft, int maxSpellcraft, int rolledSpellcraft, bool updateItem, out int minArcaneLore, out int maxArcaneLore, out int rolledArcaneLore)
        {
            var minArcaneRoll = 0.0f;
            var maxArcaneRoll = 0.0f;

            var spellIds = new List<SpellId>();

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                spellIds = allSpellIds;
            else
                spellIds = lifeCreatureEnchantmentsIds;

            if (spellIds != null)
            {
                var spells = new List<Server.Entity.Spell>();

                foreach (var spellId in spellIds)
                {
                    var spell = new Server.Entity.Spell(spellId);
                    spells.Add(spell);
                }

                spells = spells.OrderBy(i => i.Formula.Level).ToList();

                for (var i = 0; i < spells.Count; i++)
                {
                    if(i == spells.Count - 1 && Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                    {
                        // exclude highest spell
                        continue;
                    }
                    var spell = spells[i];

                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    {
                        var arcaneRoll = 4 + spell.Formula.Level;
                        if (wo.IsRobe)
                            arcaneRoll /= 2;

                        minArcaneRoll += arcaneRoll * 0.5f;
                        maxArcaneRoll += arcaneRoll * 1.0f;
                    }
                    else
                    {
                        var arcaneRoll = spell.Formula.Level * 5.0f;

                        minArcaneRoll += arcaneRoll * 0.5f;
                        maxArcaneRoll += arcaneRoll * 1.5f;
                    }
                }
            }

            if (cantripIds != null)
            {
                foreach (var cantripId in cantripIds)
                {
                    var cantrip = new Server.Entity.Spell(cantripId);
                    var cantripLevels = SpellLevelProgression.GetSpellLevels(cantripId);

                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                    {
                        if (cantripLevels == null || cantripLevels.Count != 4)
                        {
                            log.Error($"CalculateArcaneLore({cantripId}) - unknown cantrip");
                            continue;
                        }
                    }
                    else
                    {
                        if (cantripLevels == null || (cantripLevels.Count != 3 && cantripLevels.Count != 4))
                        {
                            log.Error($"CalculateArcaneLore({cantripId}) - unknown cantrip");
                            continue;
                        }
                    }

                    var cantripLevel = cantripLevels.IndexOf(cantripId);

                    if (cantripLevel == 0)
                    {
                        minArcaneRoll += 5;
                        maxArcaneRoll += 10;
                    }
                    else
                    {
                        minArcaneRoll += 10;
                        maxArcaneRoll += 20;
                    }
                }
            }

            var itemSkillLevelFactor = 0.0f;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (wo.ItemSkillLevelLimit.HasValue && wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 10.0f;
            }
            else
            {
                if (wo.ItemSkillLevelLimit.HasValue && wo.ItemSkillLevelLimit > 0)
                    itemSkillLevelFactor = wo.ItemSkillLevelLimit.Value / 2.0f;
            }

            var fArcaneMin = minSpellcraft - itemSkillLevelFactor;
            var fArcaneMax = maxSpellcraft - itemSkillLevelFactor;
            var fArcaneRolled = rolledSpellcraft - itemSkillLevelFactor;

            if (wo.ItemAllegianceRankLimit > 0)
            {
                fArcaneMin -= (float)wo.ItemAllegianceRankLimit * 10.0f;
                fArcaneMax -= (float)wo.ItemAllegianceRankLimit * 10.0f;
                fArcaneRolled -= (float)wo.ItemAllegianceRankLimit * 10.0f;
            }

            if (wo.HeritageGroup != 0)
            {
                fArcaneMin -= fArcaneMin * 0.2f;
                fArcaneMax -= fArcaneMax * 0.2f;
                fArcaneRolled -= fArcaneRolled * 0.2f;
            }

            if (wo.TinkerLog != null)
            {
                var tinkers = wo.TinkerLog.Split(",");

                var appliedCopperCount = tinkers.Count(s => s == "59");
                for (int i = 0; i < appliedCopperCount; i++)
                {
                    fArcaneMin -= 5;
                    fArcaneMax -= 5;
                    fArcaneRolled -= 5;
                }

                var appliedSilverCount = tinkers.Count(s => s == "63");
                for (int i = 0; i < appliedSilverCount; i++)
                {
                    fArcaneMin -= 10;
                    fArcaneMax -= 10;
                    fArcaneRolled -= 10;
                }
            }

            if (fArcaneMin < 0)
                fArcaneMin = 0;

            if (fArcaneMax < 0)
                fArcaneMax = 0;

            if (fArcaneRolled < 0)
                fArcaneRolled = 0;

            minArcaneLore = (int)Math.Floor(fArcaneMin + minArcaneRoll);
            maxArcaneLore = (int)Math.Floor(fArcaneMax + maxArcaneRoll);
            rolledArcaneLore = (int)Math.Floor(fArcaneRolled + ThreadSafeRandom.Next(minArcaneRoll, maxArcaneRoll));

            // Avoid lowering arcane lore.
            var currentArcaneLore = Math.Max(wo.BaseItemDifficultyOverride ?? 0, wo.ItemDifficulty ?? 0); // If set, BaseItemDifficultyOverride and BaseSpellcraftOverride account for the item default spells difficulty/spellcraft and those will not be taken into account during these recalculations. This is done so spells can be added to quest items which start with many spells without skyrocketing their difficulty.
            minArcaneLore = Math.Max(currentArcaneLore, minArcaneLore);
            maxArcaneLore = Math.Max(currentArcaneLore, maxArcaneLore);
            rolledArcaneLore = Math.Max(currentArcaneLore, rolledArcaneLore);

            if (updateItem)
                wo.ItemDifficulty = rolledArcaneLore;
        }

        private static List<SpellId> RollCantrips(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            // no cantrips on dinnerware?
            if (roll.ItemType == TreasureItemType_Orig.ArtObject)
                return null;

            int numCantrips;

            if (roll.IsClothArmor) // robes
                numCantrips = CantripChance.RollRobeNumCantrips(profile);
            else
                numCantrips = CantripChance.RollNumCantrips(profile);

            if (numCantrips == 0)
                return null;

            var numAttempts = numCantrips * 3;

            var cantrips = new HashSet<SpellId>();

            for (var i = 0; i < numAttempts && cantrips.Count < numCantrips; i++)
            {
                var cantrip = RollCantrip(wo, profile, roll);

                if (cantrip != SpellId.Undef)
                    cantrips.Add(cantrip);
            }

            var finalCantrips = new List<SpellId>();

            var hasLegendary = false;

            foreach (var cantrip in cantrips)
            {
                var cantripLevel = CantripChance.RollCantripLevel(profile);

                var cantripLevels = SpellLevelProgression.GetSpellLevels(cantrip);

                if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                {
                    if (cantripLevels.Count < 2)
                    {
                        log.Error($"RollCantrips({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - {cantrip} has {cantripLevels.Count} cantrip levels, expected 2.");
                        continue;
                    }
                    
                    finalCantrips.Add(cantripLevels[cantripLevel - 1]);
                }
                else
                {
	                if (cantripLevels.Count != 4)
	                {
	                    log.Error($"RollCantrips({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - {cantrip} has {cantripLevels.Count} cantrip levels, expected 4");
	                    continue;
	                }
	
	                finalCantrips.Add(cantripLevels[cantripLevel - 1]);
	
	                if (cantripLevel == 4)
	                    hasLegendary = true;
                }
            }

            // if a legendary cantrip dropped on this item
            if (hasLegendary && roll.ArmorType != TreasureArmorType.Society)
            {
                // and if the item has a level requirement, ensure the level requirement is at least 180
                // if the item does not already contain a level requirement, don't add one?

                if (wo.WieldRequirements == WieldRequirement.Level && wo.WieldDifficulty < 180)
                    wo.WieldDifficulty = 180;

                if (wo.WieldRequirements2 == WieldRequirement.Level && wo.WieldDifficulty2 < 180)
                    wo.WieldDifficulty2 = 180;
            }

            return finalCantrips;
        }

        private static SpellId RollCantrip(WorldObject wo, TreasureDeath profile, TreasureRoll roll)
        {
            if (roll.IsClothArmor)
            {
                // robes
                return ClothArmorCantrips.Roll();
            }
            else if (roll.HasArmorLevel(wo) || roll.IsClothing)
            {
                // armor / clothing cantrip
                // this table also applies to crowns (treasureitemtype.jewelry w/ al)
                return ArmorCantrips.Roll(wo.IsShield);
            }
            else if (roll.IsMeleeWeapon)
            {
                // melee cantrip
                var meleeCantrip = MeleeCantrips.Roll();

                // adjust for weapon skill
                if (meleeCantrip == SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1)
                    meleeCantrip = AdjustForWeaponMastery(wo);

                return meleeCantrip;
            }
            else if (roll.IsMissileWeapon)
            {
                // missile cantrip
                var missileCantrip = MissileCantrips.Roll();

                // adjust for weapon skill
                if (missileCantrip == SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1)
                    missileCantrip = AdjustForWeaponMastery(wo);

                return missileCantrip;
            }
            else if (roll.IsCaster)
            {
                // caster cantrip
                var casterCantrip = WandCantrips.Roll();

                if (casterCantrip == SpellId.CANTRIPWARMAGICAPTITUDE1)
                    casterCantrip = AdjustForDamageType(wo, casterCantrip);

                return casterCantrip;
            }
            else if (roll.IsJewelry)
            {
                // jewelry cantrip
                return JewelryCantrips.Roll();
            }
            else
            {
                log.Error($"RollCantrip({wo.Name}, {profile.TreasureType}, {roll.ItemType}) - unknown item type");
                return SpellId.Undef;
            }
        }

        private static SpellId AdjustForWeaponMastery(WorldObject wo)
        {
            if (ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR && wo.WeaponSkill != Skill.TwoHandedCombat && wo.WeaponSkill != Skill.MissileWeapons)
            {
                // 10% chance to adjust to dual wielding
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

                if (rng < 0.1f)
                    return SpellId.CantripDualWieldAptitude1;
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                switch (wo.WeaponSkill)
                {
                    case Skill.TwoHandedCombat:
                        return SpellId.CANTRIPTWOHANDEDAPTITUDE1;
                    case Skill.HeavyWeapons:
                        return SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1;
                    case Skill.LightWeapons:
                        return SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1;
                    case Skill.FinesseWeapons:
                        return SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1;
                    case Skill.MissileWeapons:
                        return SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1;
                    case Skill.Axe:
                        return SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1; // CANTRIPAXEAPTITUDE1
                    case Skill.Dagger:
                        return SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1; // CANTRIPDAGGERAPTITUDE1
                    case Skill.Mace:
                        return SpellId.CANTRIPMACEAPTITUDE1;
                    case Skill.Spear:
                        return SpellId.CANTRIPSPEARAPTITUDE1;
                    case Skill.Staff:
                        return SpellId.CANTRIPSTAFFAPTITUDE1;
                    case Skill.Sword:
                        return SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1; // CANTRIPSWORDAPTITUDE1
                    case Skill.UnarmedCombat:
                        return SpellId.CANTRIPUNARMEDAPTITUDE1;
                    case Skill.Bow:
                        return SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1; // CANTRIPBOWAPTITUDE1
                    case Skill.Crossbow:
                        return SpellId.CANTRIPCROSSBOWAPTITUDE1;
                    case Skill.ThrownWeapon:
                        return SpellId.CANTRIPTHROWNAPTITUDE1;
                }
                return SpellId.Undef;
            }
            else
            {
                switch (wo.WeaponSkill)
                {
                    case Skill.Axe:
                    case Skill.Mace:
                        return SpellId.CANTRIPLIGHTWEAPONSAPTITUDE1; // CANTRIPAXEAPTITUDE1
                    case Skill.Dagger:
                        return SpellId.CANTRIPFINESSEWEAPONSAPTITUDE1; // CANTRIPDAGGERAPTITUDE1
                    case Skill.Spear:
                    case Skill.Staff:
                        return SpellId.CANTRIPSPEARAPTITUDE1;
                    case Skill.Sword:
                        return SpellId.CANTRIPHEAVYWEAPONSAPTITUDE1; // CANTRIPSWORDAPTITUDE1
                    case Skill.UnarmedCombat:
                        return SpellId.CANTRIPUNARMEDAPTITUDE1;
                    case Skill.Bow:
                    case Skill.Crossbow:
                        return SpellId.CANTRIPMISSILEWEAPONSAPTITUDE1; // CANTRIPBOWAPTITUDE1
                    case Skill.ThrownWeapon:
                        return SpellId.CANTRIPTHROWNAPTITUDE1;
                }
                return SpellId.Undef;
            }
        }

        private static SpellId AdjustForDamageType(WorldObject wo, SpellId spell)
        {
            if (ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                return SpellId.CANTRIPWARMAGICAPTITUDE1;

            if (wo.W_DamageType == DamageType.Nether)
                return SpellId.CantripVoidMagicAptitude1;

            if (wo.W_DamageType != DamageType.Undef)
                return SpellId.CANTRIPWARMAGICAPTITUDE1;

            // even split? retail was broken here
            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

            if (rng < 0.5f)
                return SpellId.CANTRIPWARMAGICAPTITUDE1;
            else
                return SpellId.CantripVoidMagicAptitude1;
        }

        /// <summary>
        /// An alternate method to using the SpellSelectionCode from PropertyInt.TSysMutationdata
        /// </summary>
        private static int GetSpellSelectionCode_Dynamic(WorldObject wo, TreasureRoll roll)
        {
            if (wo is Gem)
            {
                return 1;
            }
            else if (roll.ItemType == TreasureItemType_Orig.Jewelry)
            {
                if (!roll.HasArmorLevel(wo))
                    return 2;
                else
                    return 3;
            }
            else if (roll.Wcid == Enum.WeenieClassName.orb)
            {
                return 4;
            }
            else if (roll.IsCaster && wo.W_DamageType != DamageType.Nether)
            {
                return 5;
            }
            else if (roll.IsMeleeWeapon && wo.WeaponSkill != Skill.TwoHandedCombat)
            {
                return 6;
            }
            else if ((roll.IsArmor || roll.IsClothing) && !wo.IsShield)
            {
                return GetSpellCode_Dynamic_ClothingArmor(wo, roll);
            }
            else if (wo.IsShield)
            {
                return 8;
            }
            else if (roll.IsDinnerware)
            {
                if (roll.Wcid == Enum.WeenieClassName.flasksimple)
                    return 0;
                else
                    return 16;
            }
            else if (roll.IsMissileWeapon || wo.IsTwoHanded)
            {
                return 17;
            }
            else if (roll.IsCaster && wo.W_DamageType == DamageType.Nether)
            {
                return 19;
            }

            log.Error($"GetSpellCode_Dynamic({wo.Name}) - couldn't determine spell selection code");

            return 0;
        }

        private static readonly CoverageMask upperArmor = CoverageMask.OuterwearChest | CoverageMask.OuterwearUpperArms | CoverageMask.OuterwearLowerArms | CoverageMask.OuterwearAbdomen;
        private static readonly CoverageMask lowerArmor = CoverageMask.OuterwearUpperLegs | CoverageMask.OuterwearLowerLegs;     // check abdomen

        private static readonly CoverageMask clothing = CoverageMask.UnderwearChest | CoverageMask.UnderwearUpperArms | CoverageMask.UnderwearLowerArms |
                CoverageMask.UnderwearAbdomen | CoverageMask.UnderwearUpperLegs | CoverageMask.UnderwearLowerLegs;

        private static int GetSpellCode_Dynamic_ClothingArmor(WorldObject wo, TreasureRoll roll)
        {
            // special cases
            switch (roll.Wcid)
            {
                case Enum.WeenieClassName.glovescloth:
                    return 14;
                case Enum.WeenieClassName.capleather:
                    return 20;
            }

            if (roll.IsClothArmor)
                return 21;

            var coverageMask = wo.ClothingPriority ?? 0;
            var isArmor = roll.IsArmor;

            if ((coverageMask & upperArmor) != 0 && (coverageMask & CoverageMask.OuterwearLowerLegs) == 0)
                return 7;

            if (coverageMask == CoverageMask.Hands && isArmor)
                return 9;

            if (coverageMask == CoverageMask.Head && roll.BaseArmorLevel >= 20)
                return 10;

            // base weenie armorLevel >= 20
            if ((coverageMask & CoverageMask.Feet) != 0 && roll.BaseArmorLevel >= 20)
                return 11;

            if ((coverageMask & clothing) != 0)
                return 12;

            // metal cap?
            if (coverageMask == CoverageMask.Head && !isArmor)
                return 13;

            if (coverageMask == CoverageMask.Hands && !isArmor)
                return 14;

            // leggings
            if ((coverageMask & lowerArmor) != 0)
                return 15;

            if (coverageMask == CoverageMask.Feet)
                return 18;

            log.Error($"GetSpellCode_Dynamic_ClothingArmor({wo.Name}) - couldn't determine spell selection code for {coverageMask}, {isArmor}");
            return 0;
        }
    }
}
