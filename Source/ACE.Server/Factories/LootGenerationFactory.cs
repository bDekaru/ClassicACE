using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using ACE.Server.Factories.Tables;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Used for cumulative ServerPerformanceMonitor event recording
        //private static readonly ThreadLocal<Stopwatch> stopwatch = new ThreadLocal<Stopwatch>(() => new Stopwatch());

        static LootGenerationFactory()
        {
            InitRares();
            InitClothingColors();

            BuildCantripsTables();

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                coinRanges = new List<(int, int)>()
                {
                    (  50,  100), // T1
                    ( 400, 1000), // T2
                    ( 800, 2000), // T3
                    (1200, 4000), // T4
                    (2000, 5000), // T5
                    (2000, 5000), // T6
                    (2000, 5000), // T7
                    (2000, 5000), // T8
                };

                ItemValue_TierMod = new List<int>()
                {
                    25,     // T1
                    50,     // T2
                    100,    // T3
                    250,    // T4
                    500,    // T5
                    1000,   // T6
                    2000,   // T7
                    3000,   // T8
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                coinRanges = new List<(int, int)>()
                {
                    ( 300,  600), // T1
                    ( 800, 2000), // T2
                    (1000, 2200), // T3
                    (1200, 2400), // T4
                    (1400, 2600), // T5
                    (1600, 2800), // T6
                    (1800, 3000), // T7
                    (2000, 3200), // T8
                };

                ItemValue_TierMod = new List<int>()
                {
                    25,    // T1
                    50,    // T2
                    65,    // T3
                    80,    // T4
                    95,    // T5
                    110,   // T6
                    125,   // T7
                    140,   // T8
                };

                EnchantmentChances_Armor_MeleeMissileWeapon = new List<float>()
                {
                    0.15f,  // T1
                    0.15f,  // T2
                    0.20f,  // T3
                    0.30f,  // T4
                    0.40f,  // T5
                    0.60f,  // T6
                    0.60f,  // T7
                    0.60f,  // T8
                };

                EnchantmentChances_Caster = new List<float>()
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
            }
        }

        public static Database.Models.World.TreasureDeath GetTweakedDeathTreasureProfile(uint deathTreasureId, object tweakedFor)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                return DatabaseManager.World.GetCachedDeathTreasure(deathTreasureId); // not tweaked.
            else
            {
                // Tweaks to make the loot system more akin to Infiltration Era and CustomDM

                if (deathTreasureId == 338) // Leave Steel Chests alone!
                    return DatabaseManager.World.GetCachedDeathTreasure(deathTreasureId);

                TreasureDeath deathTreasure;
                TreasureDeathExtended tweakedDeathTreasure;

                if (tweakedFor is Creature creature)
                {
                    deathTreasure = DatabaseManager.World.GetCachedDeathTreasure(deathTreasureId);
                    if (deathTreasure == null)
                        return deathTreasure;

                    tweakedDeathTreasure = new TreasureDeathExtended(deathTreasure);

                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    {
                        tweakedDeathTreasure.ExtendedTier = creature.Tier ?? 1;
                        tweakedDeathTreasure.Tier = creature.RollTier();
                    }

                    float itemLootChance = 1.0f;
                    float magicItemLootChance = 1.0f;
                    float mundaneItemLootChance = 1.0f;

                    switch (tweakedDeathTreasure.Tier)
                    {
                        case 1:
                            itemLootChance = 0.3f;
                            magicItemLootChance = 0.2f;
                            mundaneItemLootChance = 0.9f;
                            break;
                        case 2:
                            itemLootChance = 0.5f;
                            magicItemLootChance = 0.6f;
                            mundaneItemLootChance = 0.8f;
                            break;
                        case 3:
                            itemLootChance = 0.5f;
                            magicItemLootChance = 0.6f;
                            mundaneItemLootChance = 0.8f;
                            break;
                        case 4:
                            itemLootChance = 0.7f;
                            magicItemLootChance = 0.8f;
                            mundaneItemLootChance = 0.8f;
                            break;
                        case 5:
                            itemLootChance = 0.7f;
                            magicItemLootChance = 0.8f;
                            mundaneItemLootChance = 0.4f;
                            break;
                        case 6:
                            itemLootChance = 0.8f;
                            magicItemLootChance = 0.9f;
                            mundaneItemLootChance = 0.4f;
                            break;
                        case 7:
                            itemLootChance = 0.8f;
                            magicItemLootChance = 0.9f;
                            mundaneItemLootChance = 0.4f;
                            break;
                        case 8:
                            itemLootChance = 0.8f;
                            magicItemLootChance = 0.9f;
                            mundaneItemLootChance = 0.4f;
                            break;
                    }

                    tweakedDeathTreasure.ItemChance = (int)(tweakedDeathTreasure.ItemChance * itemLootChance);
                    tweakedDeathTreasure.MagicItemChance = (int)(tweakedDeathTreasure.MagicItemChance * magicItemLootChance);
                    tweakedDeathTreasure.MundaneItemChance = (int)(tweakedDeathTreasure.MundaneItemChance * mundaneItemLootChance);

                    return tweakedDeathTreasure;
                }

                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
                {
                    // Fix for mismatched high tier containers and generators in low level places, CustomDM has these fixed in the data files themselves.
                    switch (deathTreasureId)
                    {
                        case 4: deathTreasureId = 6; break;
                        case 16: deathTreasureId = 18; break;
                        case 313: deathTreasureId = 453; break;
                        case 457: deathTreasureId = 459; break;
                        case 463: deathTreasureId = 465; break;
                        case 15: deathTreasureId = 18; break;
                        case 462: deathTreasureId = 465; break;
                        case 460: deathTreasureId = 462; break;

                        case 3: deathTreasureId = 4; break;
                        case 13: deathTreasureId = 16; break;
                        case 454: deathTreasureId = 457; break;

                        case 1: deathTreasureId = 3; break;
                        case 456: deathTreasureId = 457; break;
                    }
                }

                deathTreasure = DatabaseManager.World.GetCachedDeathTreasure(deathTreasureId);
                if (deathTreasure == null)
                    return deathTreasure;

                if (deathTreasure.TreasureType < 1000)// leave custom deathTreasures unmodified
                {
                    if (tweakedFor is Container container)
                    {
                        // Some overrides to make chests more interesting, ideally this should be done in the data but as a quick tweak this will do.
                        tweakedDeathTreasure = new TreasureDeathExtended(deathTreasure);
                        tweakedDeathTreasure.ForContainer = true;

                        if (container.ResistAwareness.HasValue && tweakedDeathTreasure.LootQualityMod < 0.4f)
                            tweakedDeathTreasure.LootQualityMod = 0.4f;
                        else if (tweakedDeathTreasure.LootQualityMod < 0.2f)
                            tweakedDeathTreasure.LootQualityMod = 0.2f;

                        tweakedDeathTreasure.MundaneItemChance = 0;

                        if (tweakedDeathTreasure.ItemMaxAmount == 1)
                            tweakedDeathTreasure.ItemMaxAmount = 3;
                        tweakedDeathTreasure.ItemMaxAmount = (int)Math.Ceiling(tweakedDeathTreasure.ItemMaxAmount * 1.5f);

                        if (tweakedDeathTreasure.MagicItemMaxAmount == 1)
                            tweakedDeathTreasure.MagicItemMaxAmount = 3;
                        tweakedDeathTreasure.MagicItemMaxAmount = (int)Math.Ceiling(tweakedDeathTreasure.MagicItemMaxAmount * 1.5);

                        return tweakedDeathTreasure;
                    }
                    else if (tweakedFor is GenericObject generic && generic.GeneratorProfiles != null) // Ground item spawners
                    {
                        tweakedDeathTreasure = new TreasureDeathExtended(deathTreasure);
                        if (tweakedDeathTreasure.LootQualityMod < 0.2f)
                            tweakedDeathTreasure.LootQualityMod = 0.2f;

                        if (tweakedDeathTreasure.ItemChance != 0 || tweakedDeathTreasure.MagicItemChance != 0)
                            tweakedDeathTreasure.MundaneItemChance = 0; // If we can spawn something besides mundanes suppress mundane items.

                        return tweakedDeathTreasure;
                    }
                    else
                        return deathTreasure;
                }
                else
                    return deathTreasure;
            }
        }

        public static List<WorldObject> CreateRandomLootObjects(TreasureDeath profile)
        {
            //stopwatch.Value.Restart();

            try
            {
                int numItems;
                WorldObject lootWorldObject;

                var loot = new List<WorldObject>();

                if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.EoR)
                {
                    var itemChance = ThreadSafeRandom.Next(1, 100);
                    if (itemChance <= profile.ItemChance)
                    {
                        numItems = ThreadSafeRandom.Next(profile.ItemMinAmount, profile.ItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.Item);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }
                    }

                    itemChance = ThreadSafeRandom.Next(1, 100);
                    if (itemChance <= profile.MagicItemChance)
                    {
                        numItems = ThreadSafeRandom.Next(profile.MagicItemMinAmount, profile.MagicItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MagicItem);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }
                    }

                    itemChance = ThreadSafeRandom.Next(1, 100);
                    if (itemChance <= profile.MundaneItemChance)
                    {
                        numItems = ThreadSafeRandom.Next(profile.MundaneItemMinAmount, profile.MundaneItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MundaneItem);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }

                        // extra roll for mundane:
                        // https://asheron.fandom.com/wiki/Announcements_-_2010/04_-_Shedding_Skin :: May 5th, 2010 entry
                        // aetheria and coalesced mana were handled in here
                        lootWorldObject = TryRollMundaneAddon(profile);

                        if (lootWorldObject != null)
                            loot.Add(lootWorldObject);
                    }
                }
                else
                {
                    double itemChance;
                    if (profile.ItemChance == 100)
                    {
                        numItems = ThreadSafeRandom.Next(profile.ItemMinAmount, profile.ItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.Item);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }
                    }
                    else if (profile.ItemChance > 0)
                    {
                        itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                        if (itemChance < profile.ItemChance / 100.0)
                        {
                            // If we roll this bracket we are guaranteed at least ItemMinAmount of items, with an extra roll for each additional item under itemMaxAmount.
                            for (var i = 0; i < profile.ItemMinAmount; i++)
                            {
                                lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.Item);

                                if (lootWorldObject != null)
                                    loot.Add(lootWorldObject);
                            }

                            for (var i = 0; i < profile.ItemMaxAmount - profile.ItemMinAmount; i++)
                            {
                                itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                                if (itemChance < profile.ItemChance / 100.0)
                                {
                                    lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.Item);

                                    if (lootWorldObject != null)
                                        loot.Add(lootWorldObject);
                                }
                            }
                        }
                    }

                    if (profile.MagicItemChance == 100)
                    {
                        numItems = ThreadSafeRandom.Next(profile.MagicItemMinAmount, profile.MagicItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MagicItem);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }
                    }
                    else if (profile.MagicItemChance > 0)
                    {
                        itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                        if (itemChance < profile.MagicItemChance / 100.0)
                        {
                            // If we roll this bracket we are guaranteed at least MagicItemMinAmount of items, with an extra roll for each additional item under MagicItemMaxAmount.
                            for (var i = 0; i < profile.MagicItemMinAmount; i++)
                            {
                                lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MagicItem);

                                if (lootWorldObject != null)
                                    loot.Add(lootWorldObject);
                            }

                            for (var i = 0; i < profile.MagicItemMaxAmount - profile.MagicItemMinAmount; i++)
                            {
                                itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                                if (itemChance < profile.MagicItemChance / 100.0)
                                {
                                    lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MagicItem);

                                    if (lootWorldObject != null)
                                        loot.Add(lootWorldObject);
                                }
                            }
                        }
                    }

                    if (profile.MundaneItemChance == 100)
                    {
                        numItems = ThreadSafeRandom.Next(profile.MundaneItemMinAmount, profile.MundaneItemMaxAmount);

                        for (var i = 0; i < numItems; i++)
                        {
                            lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MundaneItem);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }

                        // extra roll for mundane:
                        // https://asheron.fandom.com/wiki/Announcements_-_2010/04_-_Shedding_Skin :: May 5th, 2010 entry
                        // aetheria and coalesced mana were handled in here
                        lootWorldObject = TryRollMundaneAddon(profile);

                        if (lootWorldObject != null)
                            loot.Add(lootWorldObject);
                    }
                    else if(profile.MundaneItemChance > 0)
                    {
                        itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                        if (itemChance < profile.MundaneItemChance / 100.0)
                        {
                            for (var i = 0; i < profile.MundaneItemMinAmount; i++)
                            {
                                lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MundaneItem);

                                if (lootWorldObject != null)
                                    loot.Add(lootWorldObject);
                            }

                            for (var i = 0; i < profile.MundaneItemMaxAmount - profile.MundaneItemMinAmount; i++)
                            {
                                itemChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);
                                if (itemChance < profile.MundaneItemChance / 100.0)
                                {
                                    lootWorldObject = CreateRandomLootObjects(profile, TreasureItemCategory.MundaneItem);

                                    if (lootWorldObject != null)
                                        loot.Add(lootWorldObject);
                                }
                            }

                            // extra roll for mundane:
                            // https://asheron.fandom.com/wiki/Announcements_-_2010/04_-_Shedding_Skin :: May 5th, 2010 entry
                            // aetheria and coalesced mana were handled in here
                            lootWorldObject = TryRollMundaneAddon(profile);

                            if (lootWorldObject != null)
                                loot.Add(lootWorldObject);
                        }
                    }

                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && profile is TreasureDeathExtended treasureDeathExtended && treasureDeathExtended.ForContainer)
                    {
                        // All containers have a chance of having some salvage.
                        double salvageChance = ThreadSafeRandom.NextInterval(profile.LootQualityMod);

                        if (salvageChance < 0.1)
                        {
                            var salvage = CreateRandomLootObjects(profile.Tier, profile.LootQualityMod, TreasureItemCategory.MundaneItem, TreasureItemType_Orig.Salvage);
                            if (salvage != null)
                                loot.Add(salvage);
                        }
                    }
                }

                return loot;
            }
            finally
            {
                //ServerPerformanceMonitor.AddToCumulativeEvent(ServerPerformanceMonitor.CumulativeEventHistoryType.LootGenerationFactory_CreateRandomLootObjects, stopwatch.Value.Elapsed.TotalSeconds);
            }
        }

        private static WorldObject TryRollMundaneAddon(TreasureDeath profile)
        {
            if (ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                return null;

            // coalesced mana only dropped in tiers 1-4
            if (profile.Tier <= 4)
                return TryRollCoalescedMana(profile);

            // aetheria dropped in tiers 5+
            else
                return TryRollAetheria(profile);
        }

        private static WorldObject TryRollCoalescedMana(TreasureDeath profile)
        {
            // 2% chance in here, which turns out to be less per corpse w/ MundaneItemChance > 0,
            // when the outer MundaneItemChance roll is factored in

            // loot quality mod?
            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

            if (rng < 0.02f)
                return CreateCoalescedMana(profile);
            else
                return null;
        }

        private static WorldObject TryRollAetheria(TreasureDeath profile)
        {
            var aetheria_drop_rate = (float)PropertyManager.GetDouble("aetheria_drop_rate").Item;

            if (aetheria_drop_rate <= 0.0f)
                return null;

            var dropRateMod = 1.0f / aetheria_drop_rate;

            // 2% base chance in here, which turns out to be less per corpse w/ MundaneItemChance > 0,
            // when the outer MundaneItemChance roll is factored in

            // loot quality mod?
            var rng = ThreadSafeRandom.Next(0.0f, 1.0f * dropRateMod);

            if (rng < 0.02f)
                return CreateAetheria(profile);
            else
                return null;
        }

        public static bool MutateItem(WorldObject item, TreasureDeath profile, bool isMagical)
        {
            // should ideally be split up between getting the item type,
            // and getting the specific mutate function parameters
            // however, with the way the current loot tables are set up, this is not ideal...

            // this function does a bunch of o(n) lookups through the loot tables,
            // and is only used for the /lootgen dev command currently
            // if this needs to be used in high performance scenarios, the collections for the loot tables will
            // will need to be updated to support o(1) queries

            // update: most of the o(n) lookup issues have been fixed,
            // however this is still looking into more hashtables than necessary.
            // ideally there should only be 1 hashtable that gets the roll.ItemType,
            // and any other necessary info (armorType / weaponType)
            // then just call the existing mutation method

            var roll = new TreasureRoll();

            roll.Wcid = (WeenieClassName)item.WeenieClassId;
            roll.BaseArmorLevel = item.ArmorLevel ?? 0;

            if (roll.Wcid == WeenieClassName.coinstack)
            {
                roll.ItemType = TreasureItemType_Orig.Pyreal;
                MutateCoins(item, profile);
            }
            else if (GemMaterialChance.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.Gem;
                MutateGem(item, profile, isMagical, roll);
            }
            else if (JewelryWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.Jewelry;

                if (!roll.HasArmorLevel(item))
                    MutateJewelry(item, profile, isMagical, roll);
                else
                {
                    // crowns, coronets, diadems, etc.
                    MutateArmor(item, profile, isMagical, TreasureArmorType.Cloth, roll);
                }
            }
            else if (GenericWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.ArtObject;
                MutateDinnerware(item, profile, isMagical, roll);
            }
            else if (HeavyWeaponWcids.TryGetValue(roll.Wcid, out var weaponType) ||
                LightWeaponWcids.TryGetValue(roll.Wcid, out weaponType) ||
                FinesseWeaponWcids.TryGetValue(roll.Wcid, out weaponType) ||
                TwoHandedWeaponWcids.TryGetValue(roll.Wcid, out weaponType))
            {
                roll.ItemType = TreasureItemType_Orig.Weapon;
                roll.WeaponType = weaponType;
                MutateMeleeWeapon(item, profile, isMagical, roll);
            }
            else if (BowWcids_Aluvian.TryGetValue(roll.Wcid, out weaponType) ||
                BowWcids_Gharundim.TryGetValue(roll.Wcid, out weaponType) ||
                BowWcids_Sho.TryGetValue(roll.Wcid, out weaponType) ||
                CrossbowWcids.TryGetValue(roll.Wcid, out weaponType) ||
                AtlatlWcids.TryGetValue(roll.Wcid, out weaponType))
            {
                roll.ItemType = TreasureItemType_Orig.Weapon;
                roll.WeaponType = weaponType;
                MutateMissileWeapon(item, profile, isMagical, null, roll);
            }
            else if (CasterWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.Weapon;
                roll.WeaponType = TreasureWeaponType.Caster;
                MutateCaster(item, profile, isMagical, null, roll);
            }
            else if (ArmorWcids.TryGetValue(roll.Wcid, out var armorType))
            {
                roll.ItemType = TreasureItemType_Orig.Armor;
                roll.ArmorType = armorType;
                MutateArmor(item, profile, isMagical, roll.ArmorType, roll);
            }
            else if (SocietyArmorWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.SocietyArmor;     // collapsed for mutation
                roll.ArmorType = TreasureArmorType.Society;
                MutateArmor(item, profile, isMagical, roll.ArmorType, roll);
            }
            else if (ClothingWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.Clothing;
                if (item.IsClothArmor)
                    roll.ArmorType = TreasureArmorType.Cloth;
                MutateArmor(item, profile, isMagical, roll.ArmorType, roll);
            }
            // scrolls don't really get mutated, even though they are in the main mutation method still
            else if (CloakWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.Cloak;
                MutateCloak(item, profile, roll);
            }
            else if (PetDeviceWcids.Contains(roll.Wcid))
            {
                roll.ItemType = TreasureItemType_Orig.PetDevice;
                MutatePetDevice(item, profile.Tier);
            }
            else if (AetheriaWcids.Contains(roll.Wcid))
            {
                // mundane add-on
                MutateAetheria(item, profile);
            }
            // other mundane items (mana stones, food/drink, healing kits, lockpicks, and spell components/peas) don't get mutated
            // it should be safe to return false here, for the 1 caller that currently uses this method
            // since it's not this function's responsibility to determine if an item is a lootgen item,
            // and only returns true if the item has been mutated.
            else
                return false;

            return true;
        }

        public static List<WorldObject> CreateRandomObjectsOfType(WeenieType type, int count)
        {
            var weenies = DatabaseManager.World.GetRandomWeeniesOfType((int)type, count);

            var worldObjects = new List<WorldObject>();

            foreach (var weenie in weenies)
            {
                var wo = WorldObjectFactory.CreateNewWorldObject(weenie.WeenieClassId);
                worldObjects.Add(wo);
            }

            return worldObjects;
        }

        /// <summary>
        /// Returns an appropriate material type for the World Object based on its loot tier.
        /// </summary>
        private static MaterialType GetMaterialType(WorldObject wo, int tier)
        {
            if (wo.TsysMutationData == null)
            {
                log.Warn($"[LOOT] Missing PropertyInt.TsysMutationData on loot item {wo.WeenieClassId} - {wo.Name}");
                return GetDefaultMaterialType(wo);
            }

            int materialCode = (int)wo.TsysMutationData & 0xFF;

            // Enforce some bounds
            // Data only goes to Tier 6 at the moment... Just in case the loot gem goes above this first, we'll cap it here for now.
            tier = Math.Clamp(tier, 1, 6);

            var materialBase = DatabaseManager.World.GetCachedTreasureMaterialBase(materialCode, tier);

            if (materialBase == null)
                return GetDefaultMaterialType(wo);

            var rng = ThreadSafeRandom.Next(0.0f, 1.0f);
            float probability = 0.0f;
            foreach (var m in materialBase)
            {
                probability += m.Probability;
                if (rng < probability)
                {
                    // Ivory is unique... It doesn't have a group
                    if (m.MaterialId == (uint)MaterialType.Ivory)
                        return (MaterialType)m.MaterialId;

                    var materialGroup = DatabaseManager.World.GetCachedTreasureMaterialGroup((int)m.MaterialId, tier);

                    if (materialGroup == null)
                        return GetDefaultMaterialType(wo);

                    var groupRng = ThreadSafeRandom.Next(0.0f, 1.0f);
                    float groupProbability = 0.0f;
                    foreach (var g in materialGroup)
                    {
                        groupProbability += g.Probability;
                        if (groupRng < groupProbability)
                            return (MaterialType)g.MaterialId;
                    }
                    break;
                }
            }
            return GetDefaultMaterialType(wo);
        }

        /// <summary>
        /// Gets a randomized default material type for when a weenie does not have TsysMutationData 
        /// </summary>
        private static MaterialType GetDefaultMaterialType(WorldObject wo)
        {
            if (wo == null)
                return MaterialType.Unknown;

            List<MaterialType> defaultMaterials = new List<MaterialType> { MaterialType.Unknown };

            WeenieType weenieType = wo.WeenieType;
            switch (weenieType)
            {
                case WeenieType.Caster:
                    defaultMaterials = new List<MaterialType> { MaterialType.RedGarnet, MaterialType.Jet, MaterialType.BlackOpal, MaterialType.FireOpal, MaterialType.Emerald };
                    break;
                case WeenieType.Clothing:
                    if (wo.ItemType == ItemType.Armor)
                        defaultMaterials = new List<MaterialType> { MaterialType.Copper, MaterialType.Bronze, MaterialType.Iron, MaterialType.Steel, MaterialType.Silver };
                    else if (wo.ItemType == ItemType.Clothing)
                        defaultMaterials = new List<MaterialType> { MaterialType.Linen, MaterialType.Wool, MaterialType.Velvet, MaterialType.Satin, MaterialType.Silk };
                    break;
                case WeenieType.MissileLauncher:
                case WeenieType.Missile:
                    defaultMaterials = new List<MaterialType> { MaterialType.Oak, MaterialType.Teak, MaterialType.Mahogany, MaterialType.Pine, MaterialType.Ebony };
                    break;
                case WeenieType.MeleeWeapon:
                    defaultMaterials = new List<MaterialType> { MaterialType.Brass, MaterialType.Ivory, MaterialType.Gold, MaterialType.Steel, MaterialType.Diamond };
                    break;
                case WeenieType.Generic:
                    if (wo.ItemType == ItemType.Jewelry)
                        defaultMaterials = new List<MaterialType> { MaterialType.RedGarnet, MaterialType.Jet, MaterialType.BlackOpal, MaterialType.FireOpal, MaterialType.Emerald };
                    else if (wo.ItemType == ItemType.MissileWeapon)
                        defaultMaterials = new List<MaterialType> { MaterialType.Granite, MaterialType.Ceramic, MaterialType.Porcelain, MaterialType.Alabaster, MaterialType.Marble };
                    break;
            }

            if (defaultMaterials.Count > 1)
                return defaultMaterials[ThreadSafeRandom.Next(0, defaultMaterials.Count - 1)];
            else
                return defaultMaterials.First();
        }

        /// <summary>
        /// This will assign a completely random, valid color to the item in question. It will also randomize the shade and set the appropriate icon.
        ///
        /// This was a temporary function to give some color to loot until further work was put in for "proper" color handling. Leave it here as an option for future potential use (perhaps config option?)
        /// </summary>
        private static WorldObject RandomizeColorTotallyRandom(WorldObject wo)
        {
            // Make sure the item has a ClothingBase...otherwise we can't properly randomize the colors.
            if (wo.ClothingBase != null)
            {
                DatLoader.FileTypes.ClothingTable item = DatLoader.DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.ClothingTable>((uint)wo.ClothingBase);

                // Get a random PaletteTemplate index from the ClothingBase entry
                // But make sure there's some valid ClothingSubPalEffects (in a valid loot/clothingbase item, there always SHOULD be)
                if (item.ClothingSubPalEffects.Count > 0)
                {
                    int randIndex = ThreadSafeRandom.Next(0, item.ClothingSubPalEffects.Count - 1);
                    var cloSubPal = item.ClothingSubPalEffects.ElementAt(randIndex);

                    // Make sure this entry has a valid icon, otherwise there's likely something wrong with the ClothingBase value for this WorldObject (e.g. not supposed to be a loot item)
                    if (cloSubPal.Value.Icon > 0)
                    {
                        // Assign the appropriate Icon and PaletteTemplate
                        wo.IconId = cloSubPal.Value.Icon;
                        wo.PaletteTemplate = (int)cloSubPal.Key;

                        // Throw some shade, at random
                        wo.Shade = ThreadSafeRandom.Next(0.0f, 1.0f);
                    }
                }
            }
            return wo;
        }

        public static readonly List<TreasureMaterialColor> clothingColors = new List<TreasureMaterialColor>();

        public static void InitClothingColors()
        {
            for (uint i = 1; i < 19; i++)
            {
                TreasureMaterialColor tmc = new TreasureMaterialColor
                {
                    PaletteTemplate = i,
                    Probability = 1
                };
                clothingColors.Add(tmc);
            }
        }

        /// <summary>
        /// Assign a random color (Int.PaletteTemplate and Float.Shade) to a World Object based on the material assigned to it.
        /// </summary>
        /// <returns>WorldObject with a random applicable PaletteTemplate and Shade applied, if available</returns>
        private static void MutateColor(WorldObject wo)
        {
            if (wo.MaterialType > 0 && wo.TsysMutationData != null && wo.ClothingBase != null)
            {
                byte colorCode = (byte)(wo.TsysMutationData.Value >> 16);

                // BYTE spellCode = (tsysMutationData >> 24) & 0xFF;
                // BYTE colorCode = (tsysMutationData >> 16) & 0xFF;
                // BYTE gemCode = (tsysMutationData >> 8) & 0xFF;
                // BYTE materialCode = (tsysMutationData >> 0) & 0xFF;

                List<TreasureMaterialColor> colors = DatabaseManager.World.GetCachedTreasureMaterialColors((int)wo.MaterialType, colorCode);

                if (colors == null)
                {
                    // legacy support for hardcoded colorCode 0 table
                    if (colorCode == 0 && (uint)wo.MaterialType > 0)
                    {
                        // This is a unique situation that typically applies to Under Clothes.
                        // If the Color Code is 0, they can be PaletteTemplate 1-18, assuming there is a MaterialType
                        // (gems have ColorCode of 0, but also no MaterialCode as they are defined by the weenie)

                        // this can be removed after all servers have upgraded to latest db
                        colors = clothingColors;
                    }
                    else
                        return;
                }

                // Load the clothingBase associated with the WorldObject
                DatLoader.FileTypes.ClothingTable clothingBase = DatLoader.DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.ClothingTable>((uint)wo.ClothingBase);

                // TODO : Probably better to use an intersect() function here. I defer to someone who knows how these work better than I - Optim
                // Compare the colors list and the clothingBase PaletteTemplates and remove any invalid items
                var colorsValid = new List<TreasureMaterialColor>();
                foreach (var e in colors)
                    if (clothingBase.ClothingSubPalEffects.ContainsKey(e.PaletteTemplate))
                        colorsValid.Add(e);
                colors = colorsValid;

                float totalProbability = GetTotalProbability(colors);
                // If there's zero chance to get a random color, no point in continuing.
                if (totalProbability == 0) return;

                var rng = ThreadSafeRandom.Next(0.0f, totalProbability);

                uint paletteTemplate = 0;
                float probability = 0.0f;
                // Loop through the colors until we've reach our target value
                foreach (var color in colors)
                {
                    probability += color.Probability;
                    if (rng < probability)
                    {
                        paletteTemplate = color.PaletteTemplate;
                        break;
                    }
                }

                if (paletteTemplate > 0)
                {
                    var cloSubPal = clothingBase.ClothingSubPalEffects[paletteTemplate];
                    // Make sure this entry has a valid icon, otherwise there's likely something wrong with the ClothingBase value for this WorldObject (e.g. not supposed to be a loot item)
                    if (cloSubPal.Icon > 0)
                    {
                        // Assign the appropriate Icon and PaletteTemplate
                        wo.IconId = cloSubPal.Icon;
                        wo.PaletteTemplate = (int)paletteTemplate;

                        // Throw some shade, at random
                        wo.Shade = ThreadSafeRandom.Next(0.0f, 1.0f);

                        // Some debug info...
                        // log.Info($"Color success for {wo.MaterialType}({(int)wo.MaterialType}) - {wo.WeenieClassId} - {wo.Name}. PaletteTemplate {paletteTemplate} applied.");
                    }
                }
                else
                {
                    log.Warn($"[LOOT] Color looked failed for {wo.MaterialType} ({(int)wo.MaterialType}) - {wo.WeenieClassId} - {wo.Name}.");
                }
            }
        }

        /// <summary>
        /// Some helper functions to get Probablity from different list types
        /// </summary>
        private static float GetTotalProbability(List<TreasureMaterialColor> colors)
        {
            return colors != null ? colors.Sum(i => i.Probability) : 0.0f;
        }

        private static float GetTotalProbability(List<TreasureMaterialBase> list)
        {
            return list != null ? list.Sum(i => i.Probability) : 0.0f;
        }

        private static float GetTotalProbability(List<TreasureMaterialGroups> list)
        {
            return list != null ? list.Sum(i => i.Probability) : 0.0f;
        }

        public static MaterialType RollGemType(int tier)
        {
            // previous formula
            //return (MaterialType)ThreadSafeRandom.Next(10, 50);

            // the gem class value can be further utilized for determining the item's monetary value
            var gemClass = GemClassChance.Roll(tier);

            var gemResult = GemMaterialChance.Roll(gemClass);

            return gemResult.MaterialType;
        }

        public const float WeaponBulk = 0.50f;
        public const float ArmorBulk = 0.25f;

        private static bool MutateBurden(WorldObject wo, TreasureDeath treasureDeath, bool isWeapon)
        {
            // ensure item has burden
            if (wo.EncumbranceVal == null)
                return false;

            var qualityInterval = QualityChance.RollInterval(treasureDeath);

            // only continue if the initial roll to modify the quality succeeded
            if (qualityInterval == 0.0f)
                return false;

            // only continue if initial roll succeeded?
            var bulk = isWeapon ? WeaponBulk : ArmorBulk;
            bulk *= (float)(wo.BulkMod ?? 1.0f);

            var maxBurdenMod = 1.0f - bulk;

            var burdenMod = 1.0f - (qualityInterval * maxBurdenMod);

            // modify burden
            var prevBurden = wo.EncumbranceVal.Value;
            wo.EncumbranceVal = (int)Math.Round(prevBurden * burdenMod);

            if (wo.EncumbranceVal < 1)
                wo.EncumbranceVal = 1;

            //Console.WriteLine($"Modified burden from {prevBurden} to {wo.EncumbranceVal} for {wo.Name} ({wo.WeenieClassId})");

            return true;
        }

        private static void MutateValue(WorldObject wo, int tier, TreasureRoll roll)
        {
            if (wo.Value == null)
                wo.Value = 0;   // fixme: data

            //var weenieValue = wo.Value;

            if (!(wo is Gem))
            {
                if (wo.HasArmorLevel())
                    MutateValue_Armor(wo);

                MutateValue_Generic(wo, tier);
            }
            else
                MutateValue_Gem(wo);

            MutateValue_Spells(wo);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                wo.OriginalValue = wo.Value;
                wo.Value = (int)ThreadSafeRandom.Next(wo.Value.Value * 0.7f, wo.Value.Value * 0.9f);
            }

            /*Console.WriteLine($"Mutating value for {wo.Name} ({weenieValue:N0} -> {wo.Value:N0})");

            // compare with previous function
            var materialMod = LootTables.getMaterialValueModifier(wo);
            var gemMod = LootTables.getGemMaterialValueModifier(wo);

            var rngRange = itemValue_RandomRange[tier - 1];

            var minValue = (int)(rngRange.min * gemMod * materialMod * Math.Ceiling(tier / 2.0f));
            var maxValue = (int)(rngRange.max * gemMod * materialMod * Math.Ceiling(tier / 2.0f));

            Console.WriteLine($"Previous ACE range: {minValue:N0} - {maxValue:N0}");*/
        }

        // increase for a wider variance in item value ranges
        private const float valueFactor = 1.0f / 3.0f;

        private const float valueNonFactor = 1.0f - valueFactor;

        private static void MutateValue_Generic(WorldObject wo, int tier)
        {
            // confirmed from retail magloot logs, matches up relatively closely

            var rng = (float)ThreadSafeRandom.Next(0.7f, 1.25f);

            var workmanshipMod = WorkmanshipChance.GetModifier(wo.ItemWorkmanship);

            var materialMod = MaterialTable.GetValueMod(wo.MaterialType);
            var gemValue = GemMaterialChance.GemValue(wo.GemType);

            var tierMod = ItemValue_TierMod[Math.Clamp(tier, 1, 8) - 1];

            var newValue = (int)wo.Value * valueFactor + materialMod * tierMod + gemValue;

            newValue *= (workmanshipMod /* + qualityMod */ ) * rng;

            newValue += (int)wo.Value * valueNonFactor;

            int iValue = (int)Math.Ceiling(newValue);

            // only raise value?
            if (iValue > wo.Value)
                wo.Value = iValue;
        }

        private static void MutateValue_Spells(WorldObject wo)
        {
            if (wo.ItemMaxMana != null)
                wo.Value += wo.ItemMaxMana * 2;

            int spellLevelSum = 0;

            if (wo.SpellDID != null)
            {
                var spell = new Server.Entity.Spell(wo.SpellDID.Value);
                spellLevelSum += (int)spell.Level;
            }

            if (wo.Biota.PropertiesSpellBook != null)
            {
                foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                {
                    var spell = new Server.Entity.Spell(spellId);
                    spellLevelSum += (int)spell.Level;
                }
            }
            wo.Value += spellLevelSum * 10;
        }

        private static readonly List<int> ItemValue_TierMod = new List<int>()
        {
            25,     // T1
            50,     // T2
            100,    // T3
            250,    // T4
            500,    // T5
            1000,   // T6
            2000,   // T7
            3000,   // T8
        };

        public static TreasureRoll RollWcid(TreasureDeath treasureDeath, TreasureItemCategory category)
        {
            TreasureDeathExtended treasureDeathExtended = treasureDeath as TreasureDeathExtended;

            TreasureItemType_Orig treasureItemType = treasureDeathExtended != null ? treasureDeathExtended.ForceTreasureItemType : TreasureItemType_Orig.Undef;

            if (treasureItemType == TreasureItemType_Orig.Undef)
                treasureItemType = RollItemType(treasureDeath, category);

            if (treasureItemType == TreasureItemType_Orig.Undef)
            {
                log.Error($"LootGenerationFactory.RollWcid({treasureDeath.TreasureType}, {category}): treasureItemType == Undef");
                return null;
            }

            var treasureRoll = new TreasureRoll(treasureItemType);

            if (treasureDeathExtended != null)
            {
                treasureRoll.ArmorType = treasureDeathExtended.ForceArmorType;
                treasureRoll.WeaponType = treasureDeathExtended.ForceWeaponType;
                treasureRoll.Heritage = treasureDeathExtended.ForceHeritage;
            }

            switch (treasureItemType)
            {
                case TreasureItemType_Orig.Pyreal:

                    treasureRoll.Wcid = WeenieClassName.coinstack;
                    break;

                case TreasureItemType_Orig.Gem:

                    var gemClass = GemClassChance.Roll(treasureDeath.Tier);
                    var gemResult = GemMaterialChance.Roll(gemClass);

                    treasureRoll.Wcid = gemResult.ClassName;
                    break;

                case TreasureItemType_Orig.Jewelry:

                    treasureRoll.Wcid = JewelryWcids.Roll(treasureDeath.Tier);
                    break;

                case TreasureItemType_Orig.ArtObject:

                    treasureRoll.Wcid = GenericWcids.Roll(treasureDeath.Tier);
                    break;

                case TreasureItemType_Orig.Weapon:

                    if(treasureRoll.WeaponType == TreasureWeaponType.Undef)
                        treasureRoll.WeaponType = WeaponTypeChance.Roll(treasureDeath.Tier);
                    else if(treasureRoll.WeaponType == TreasureWeaponType.MeleeWeapon || treasureRoll.WeaponType == TreasureWeaponType.MissileWeapon)
                        treasureRoll.WeaponType = WeaponTypeChance.Roll(treasureDeath.Tier, treasureRoll.WeaponType);
                    treasureRoll.Wcid = WeaponWcids.Roll(treasureDeath, treasureRoll);
                    break;

                case TreasureItemType_Orig.Armor:

                    if (treasureRoll.ArmorType == TreasureArmorType.Undef)
                        treasureRoll.ArmorType = ArmorTypeChance.Roll(treasureDeath.Tier);
                    treasureRoll.Wcid = ArmorWcids.Roll(treasureDeath, treasureRoll);
                    break;

                case TreasureItemType_Orig.Clothing:

                    treasureRoll.Wcid = ClothingWcids.Roll(treasureDeath, treasureRoll);
                    break;

                case TreasureItemType_Orig.Scroll:

                    treasureRoll.Wcid = ScrollWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.Caster:

                    // only called if TreasureItemType.Caster was specified directly
                    treasureRoll.WeaponType = TreasureWeaponType.Caster;
                    treasureRoll.Wcid = CasterWcids.Roll(treasureDeath.Tier);
                    break;

                case TreasureItemType_Orig.ManaStone:

                    treasureRoll.Wcid = ManaStoneWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.Consumable:

                    treasureRoll.Wcid = ConsumeWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.HealKit:

                    treasureRoll.Wcid = HealKitWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.Lockpick:

                    treasureRoll.Wcid = LockpickWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.SpellComponent:

                    treasureRoll.Wcid = SpellComponentWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.SocietyArmor:
                case TreasureItemType_Orig.SocietyBreastplate:
                case TreasureItemType_Orig.SocietyGauntlets:
                case TreasureItemType_Orig.SocietyGirth:
                case TreasureItemType_Orig.SocietyGreaves:
                case TreasureItemType_Orig.SocietyHelm:
                case TreasureItemType_Orig.SocietyPauldrons:
                case TreasureItemType_Orig.SocietyTassets:
                case TreasureItemType_Orig.SocietyVambraces:
                case TreasureItemType_Orig.SocietySollerets:

                    treasureRoll.ItemType = TreasureItemType_Orig.SocietyArmor;     // collapse for mutation
                    treasureRoll.ArmorType = TreasureArmorType.Society;

                    treasureRoll.Wcid = SocietyArmorWcids.Roll(treasureDeath, treasureItemType, treasureRoll);
                    break;

                case TreasureItemType_Orig.Cloak:

                    treasureRoll.Wcid = CloakWcids.Roll();
                    break;

                case TreasureItemType_Orig.PetDevice:

                    treasureRoll.Wcid = PetDeviceWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.EncapsulatedSpirit:

                    treasureRoll.Wcid = WeenieClassName.ace49485_encapsulatedspirit;
                    break;

                case TreasureItemType_Orig.Salvage:

                    treasureRoll.Wcid = SalvageWcids.Roll(treasureDeath);
                    break;

                case TreasureItemType_Orig.SpecialItem:

                    treasureRoll.Wcid = SpecialItemsWcids.Roll(treasureDeath, treasureRoll);
                    break;

                case TreasureItemType_Orig.Ammo:

                    treasureRoll.Wcid = AmmoWcids.Roll(treasureDeath);
                    break;
            }
            return treasureRoll;
        }

        /// <summary>
        /// Rolls for an overall item type, based on the *_Chances columns in the treasure_death profile
        /// </summary>
        public static TreasureItemType_Orig RollItemType(TreasureDeath treasureDeath, TreasureItemCategory category)
        {
            var result = TreasureItemType_Orig.Undef;
            switch (category)
            {
                case TreasureItemCategory.Item:
                    result = TreasureProfile_Item.Roll(treasureDeath.ItemTreasureTypeSelectionChances); break;

                case TreasureItemCategory.MagicItem:
                    result = TreasureProfile_MagicItem.Roll(treasureDeath.MagicItemTreasureTypeSelectionChances); break;

                case TreasureItemCategory.MundaneItem:
                    result = TreasureProfile_Mundane.Roll(treasureDeath.MundaneItemTypeSelectionChances); break;
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if ((result == TreasureItemType_Orig.Lockpick || result == TreasureItemType_Orig.ManaStone || result == TreasureItemType_Orig.HealKit) && ThreadSafeRandom.Next(0, 1) != 1)
                    result = TreasureItemType_Orig.Ammo; // Convert some of these drops to ammo drops.
            }
            return result;
        }

        public static WorldObject CreateRandomLootObjects(int tier, TreasureItemCategory category, TreasureItemType_Orig treasureItemType = TreasureItemType_Orig.Undef, TreasureArmorType armorType = TreasureArmorType.Undef, TreasureWeaponType weaponType = TreasureWeaponType.Undef)
        {
            return CreateRandomLootObjects(tier, 0.0f, category, treasureItemType, armorType, weaponType);
        }

        public static WorldObject CreateRandomLootObjects(int tier, float lootQualityMod, TreasureItemCategory category, TreasureItemType_Orig treasureItemType = TreasureItemType_Orig.Undef, TreasureArmorType armorType = TreasureArmorType.Undef, TreasureWeaponType weaponType = TreasureWeaponType.Undef, TreasureHeritageGroup heritageGroup = TreasureHeritageGroup.Invalid)
        {
            var treasureDeath = new TreasureDeathExtended()
            {
                Tier = tier,
                LootQualityMod = lootQualityMod,
                ForceTreasureItemType = treasureItemType,
                ForceArmorType = armorType,
                ForceWeaponType = weaponType,
                ForceHeritage = heritageGroup,

                ItemChance = 100,
                ItemMinAmount = 1,
                ItemMaxAmount = 1,
                ItemTreasureTypeSelectionChances = 8,

                MagicItemChance = 100,
                MagicItemMinAmount = 1,
                MagicItemMaxAmount = 1,
                MagicItemTreasureTypeSelectionChances = 8,

                MundaneItemChance = 100,
                MundaneItemMinAmount = 1,
                MundaneItemMaxAmount = 1,
                MundaneItemTypeSelectionChances = 7,

                UnknownChances = 21
            };

            return CreateRandomLootObjects(treasureDeath, category);
        }

        public static WorldObject CreateRandomLootObjects(TreasureDeath treasureDeath, TreasureItemCategory category)
        {
            var treasureRoll = RollWcid(treasureDeath, category);

            if (treasureRoll == null) return null;

            var wo = CreateAndMutateWcid(treasureDeath, treasureRoll, category == TreasureItemCategory.MagicItem);

            return wo;
        }

        public static WorldObject CreateAndMutateWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll, bool isMagical)
        {
            WorldObject wo = WorldObjectFactory.CreateNewWorldObject((uint)treasureRoll.Wcid);

            if (wo == null)
            {
                log.Error($"CreateAndMutateWcid({treasureDeath.TreasureType}, {(int)treasureRoll.Wcid} - {treasureRoll.Wcid}, {treasureRoll.GetItemType()}, {isMagical}) - failed to create item");
                return null;
            }

            wo.Tier = treasureDeath.Tier;


            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && wo.MaxStackSize > 1)
            {
                if (treasureRoll.ItemType == TreasureItemType_Orig.SpecialItem_Unmutated)
                    wo.SetStackSize(SpecialItemsWcids.GetAmount(wo.WeenieClassId));
                else if (treasureRoll.WeaponType == TreasureWeaponType.Thrown)
                    wo.SetStackSize(Math.Min(30, (int)(wo.MaxStackSize ?? 1)));
                else if (treasureRoll.ItemType == TreasureItemType_Orig.Consumable)
                    wo.SetStackSize(Math.Min(3, (int)(wo.MaxStackSize ?? 1)));
                else if (wo.ItemType == ItemType.SpellComponents)
                {
                    uint componentId = wo.GetProperty(PropertyDataId.SpellComponent) ?? 0;
                    if ((componentId > 6 && componentId < 49) || (componentId > 62 && componentId < 75)) // herbs, powders, potions and tapers
                        wo.SetStackSize(Math.Min(2 * treasureDeath.Tier, wo.MaxStackSize ?? 1));
                    else if ((wo.GetProperty(PropertyDataId.SpellComponent) ?? 0) < 63) // scarabs and talismans
                        wo.SetStackSize(Math.Min(treasureDeath.Tier, wo.MaxStackSize ?? 1));
                }
                else if (treasureRoll.ItemType == TreasureItemType_Orig.Ammo)
                {
                    if (wo.WeenieType == WeenieType.Missile)
                        wo.SetStackSize(Math.Min(50, (int)(wo.MaxStackSize ?? 1)));
                    else
                        wo.SetStackSize(Math.Min(10, (int)(wo.MaxStackSize ?? 1)));
                }
            }

            treasureRoll.BaseArmorLevel = wo.ArmorLevel ?? 0;

            switch (treasureRoll.ItemType)
            {
                case TreasureItemType_Orig.Pyreal:
                    MutateCoins(wo, treasureDeath);
                    break;
                case TreasureItemType_Orig.Gem:
                    MutateGem(wo, treasureDeath, isMagical, treasureRoll);
                    break;
                case TreasureItemType_Orig.Jewelry:

                    if (!treasureRoll.HasArmorLevel(wo))
                        MutateJewelry(wo, treasureDeath, isMagical, treasureRoll);
                    else
                    {
                        // crowns, coronets, diadems, etc.
                        MutateArmor(wo, treasureDeath, isMagical, TreasureArmorType.Cloth, treasureRoll);
                    }
                    break;
                case TreasureItemType_Orig.ArtObject:
                    if (wo.WeenieType == WeenieType.Generic && wo.ItemType == ItemType.MissileWeapon)
                        MutateDinnerware(wo, treasureDeath, isMagical, treasureRoll);
                    break;

                case TreasureItemType_Orig.Weapon:

                    switch (treasureRoll.WeaponType)
                    {
                        case TreasureWeaponType.Axe:
                        case TreasureWeaponType.Dagger:
                        case TreasureWeaponType.DaggerMS:
                        case TreasureWeaponType.Mace:
                        case TreasureWeaponType.MaceJitte:
                        case TreasureWeaponType.Spear:
                        case TreasureWeaponType.Staff:
                        case TreasureWeaponType.Sword:
                        case TreasureWeaponType.SwordMS:
                        case TreasureWeaponType.Unarmed:
                        case TreasureWeaponType.Thrown:

                        case TreasureWeaponType.TwoHandedAxe:
                        case TreasureWeaponType.TwoHandedMace:
                        case TreasureWeaponType.TwoHandedSpear:
                        case TreasureWeaponType.TwoHandedSword:

                            MutateMeleeWeapon(wo, treasureDeath, isMagical, treasureRoll);
                            break;

                        case TreasureWeaponType.Caster:

                            MutateCaster(wo, treasureDeath, isMagical, null, treasureRoll);
                            break;

                        case TreasureWeaponType.Bow:
                        case TreasureWeaponType.BowShort:
                        case TreasureWeaponType.Crossbow:
                        case TreasureWeaponType.CrossbowLight:
                        case TreasureWeaponType.Atlatl:
                        case TreasureWeaponType.AtlatlRegular:

                            MutateMissileWeapon(wo, treasureDeath, isMagical, null, treasureRoll);
                            break;

                        default:
                            log.Error($"CreateAndMutateWcid({treasureDeath.TreasureType}, {(int)treasureRoll.Wcid} - {treasureRoll.Wcid}, {treasureRoll.GetItemType()}, {isMagical}) - unknown weapon type");
                            break;
                    }
                    break;

                case TreasureItemType_Orig.Caster:

                    // alternate path -- only called if TreasureItemType.Caster was specified directly
                    MutateCaster(wo, treasureDeath, isMagical, null, treasureRoll);
                    break;

                case TreasureItemType_Orig.Armor:
                case TreasureItemType_Orig.SocietyArmor:    // collapsed, after rolling for initial wcid

                    MutateArmor(wo, treasureDeath, isMagical, treasureRoll.ArmorType, treasureRoll);
                    break;

                case TreasureItemType_Orig.Clothing:
                    MutateArmor(wo, treasureDeath, isMagical, TreasureArmorType.Cloth, treasureRoll);
                    break;

                case TreasureItemType_Orig.Cloak:
                    MutateCloak(wo, treasureDeath, treasureRoll);
                    break;

                case TreasureItemType_Orig.PetDevice:
                    MutatePetDevice(wo, treasureDeath.Tier);
                    break;

                case TreasureItemType_Orig.Salvage:
                    MutateSalvage(wo, treasureDeath.Tier);
                    break;

                    // other mundane items (mana stones, food/drink, healing kits, lockpicks, and spell components/peas) don't get mutated
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (wo.WieldSkillType.HasValue)
                    wo.WieldSkillType = (int)wo.ConvertToMoASkill((Skill)wo.WieldSkillType);
                if (wo.WieldSkillType2.HasValue)
                    wo.WieldSkillType2 = (int)wo.ConvertToMoASkill((Skill)wo.WieldSkillType2);
                if (wo.WieldSkillType3.HasValue)
                    wo.WieldSkillType3 = (int)wo.ConvertToMoASkill((Skill)wo.WieldSkillType3);
                if (wo.WieldSkillType4.HasValue)
                    wo.WieldSkillType4 = (int)wo.ConvertToMoASkill((Skill)wo.WieldSkillType4);
            }

            return wo;
        }

        /// <summary>
        /// The min/max amount of pyreals that can be rolled per tier, from magloot corpse logs
        /// </summary>
        private static readonly List<(int min, int max)> coinRanges = new List<(int, int)>()
        {
            (5,   50),   // T1
            (10,  200),  // T2
            (10,  500),  // T3
            (25,  1000), // T4
            (50,  5000), // T5
            (250, 5000), // T6
            (250, 5000), // T7
            (250, 5000), // T8
        };

        private static void MutateCoins(WorldObject wo, TreasureDeath profile)
        {
            var tierRange = coinRanges[profile.Tier - 1];

            // flat rng range, according to magloot corpse logs
            var rng = ThreadSafeRandom.Next(tierRange.min, tierRange.max);

            wo.SetStackSize(rng);
        }

        public static string GetLongDesc(WorldObject wo)
        {
            if (wo.SpellDID != null)
            {
                var longDesc = TryGetLongDesc(wo, (SpellId)wo.SpellDID);

                if (longDesc != null)
                    return longDesc;
            }

            if (wo.Biota.PropertiesSpellBook != null)
            {
                foreach (var spellId in wo.Biota.PropertiesSpellBook.Keys)
                {
                    var longDesc = TryGetLongDesc(wo, (SpellId)spellId);

                    if (longDesc != null)
                        return longDesc;
                }
            }
            return wo.Name;
        }

        private static string TryGetLongDesc(WorldObject wo, SpellId spellId)
        {
            var descriptor = SpellDescriptors.GetDescriptor(spellId);

            if (descriptor != null)
                return $"{wo.Name} of {descriptor}";
            else
                return null;
        }

        private static void RollWieldLevelReq_T7_T8(WorldObject wo, TreasureDeath profile)
        {
            if (profile.Tier < 7)
                return;

            var wieldLevelReq = 150;

            if (profile.Tier == 8)
            {
                // t8 had a 90% chance for 180
                // loot quality mod?
                var rng = ThreadSafeRandom.Next(0.0f, 1.0f);

                if (rng < 0.9f)
                    wieldLevelReq = 180;
            }

            wo.WieldRequirements = WieldRequirement.Level;
            wo.WieldDifficulty = wieldLevelReq;

            // as per retail pcaps, must be set to appear in client
            wo.WieldSkillType = 1;  
        }

        // for logging epic/legendary drops
        public static HashSet<int> MinorCantrips;
        public static HashSet<int> MajorCantrips;
        public static HashSet<int> EpicCantrips;
        public static HashSet<int> LegendaryCantrips;

        private static void BuildCantripsTables()
        {
            var allCantrips = new List<SpellId>();
            allCantrips.AddRange(ArmorCantrips.GetSpellIdList(false));
            allCantrips.AddRange(ArmorCantrips.GetSpellIdList(true));
            allCantrips.AddRange(JewelryCantrips.GetSpellIdList());
            allCantrips.AddRange(WandCantrips.GetSpellIdList());
            allCantrips.AddRange(MeleeCantrips.GetSpellIdList());
            allCantrips.AddRange(MissileCantrips.GetSpellIdList());
            allCantrips.AddRange(ClothArmorCantrips.GetSpellIdList());

            allCantrips = allCantrips.Distinct().ToList();

            MinorCantrips = new HashSet<int>();
            MajorCantrips = new HashSet<int>();
            EpicCantrips = new HashSet<int>();
            LegendaryCantrips = new HashSet<int>();

            foreach (var level1SpellId in allCantrips)
            {
                for (int spellLevel = 1; spellLevel <= 4; spellLevel++)
                {
                    var spellId = SpellLevelProgression.GetSpellAtLevel(level1SpellId, spellLevel);
                    if (spellId != SpellId.Undef)
                    {
                        switch (spellLevel)
                        {
                            case 1:
                                MinorCantrips.Add((int)spellId);
                                break;
                            case 2:
                                MajorCantrips.Add((int)spellId);
                                break;
                            case 3:
                                EpicCantrips.Add((int)spellId);
                                break;
                            case 4:
                                LegendaryCantrips.Add((int)spellId);
                                break;
                        }
                    }
                }
            }
        }
    }         
}
