using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;
using System.Collections.Generic;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class SalvageWcids
    {
        private static ChanceTable<WeenieClassName> salvageWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ( WeenieClassName.materialleather,          1.00f ), // Add Retained Status
            ( WeenieClassName.materialsilk,             1.00f ), // Remove Allegiance Requirement
            ( WeenieClassName.materialivory,            1.00f ), // Remove Attuned Status
            ( WeenieClassName.materialsandstone,        1.00f ), // Remove Retained Status
            ( WeenieClassName.materialteak,             1.00f ), // Switch to Aluvian
            ( WeenieClassName.materialebony,            1.00f ), // Switch to Gharundim
            ( WeenieClassName.materialporcelain,        1.00f ), // Switch to Sho

            ( WeenieClassName.materialsilver,           1.00f ), // Arcane Lore - 10 Mana Rate * 200%
            ( WeenieClassName.materialcopper,           1.00f ), // Arcane Lore - 5

            ( WeenieClassName.materialgold,             1.00f ), // Value + 100% or 5000
            ( WeenieClassName.materialpine,             1.00f ), // Value - 50%
            ( WeenieClassName.materiallinen,            1.00f ), // Burden - 50%
            ( WeenieClassName.materialmoonstone,        1.00f ), // Max Mana + 100% or 500
            ( WeenieClassName.materialpyreal,           1.00f ), // Mana Rate * 50%
            ( WeenieClassName.materialamber,            1.00f ), // Extra Spell Slots + 1 for regular items and + 2 for robes

            ( WeenieClassName.materialsteel,            1.00f ), // AL + 25% or 20
            ( WeenieClassName.materialalabaster,        1.00f ), // Armor Piercing Resist + 0.5
            ( WeenieClassName.materialbronze,           1.00f ), // Armor Slashing Resist + 0.5
            ( WeenieClassName.materialmarble,           1.00f ), // Armor Bludgeoning Resist + 0.5
            ( WeenieClassName.materialceramic,          1.00f ), // Armor Fire Resist + 1.0
            ( WeenieClassName.materialwool,             1.00f ), // Armor Cold Resist + 1.0
            ( WeenieClassName.materialreedsharkhide,    1.00f ), // Armor Lightning Resist + 1.0
            ( WeenieClassName.materialarmoredillohide,  1.00f ), // Armor Acid Resist + 1.0
            ( WeenieClassName.materialsatin,            1.00f ), // Armor Max Evasion Penalty - 50%
            ( WeenieClassName.materialdiamond,          1.00f ), // Shield Block Chance + 5%

            ( WeenieClassName.materialiron,             1.00f ), // Weapon Damage + 4 for regular weapons and + 2 for multi-strike weapons
            ( WeenieClassName.materialmahogany,         1.00f ), // Missile Weapon Mod + 16%
            ( WeenieClassName.materialgreengarnet,      1.00f ), // Caster Damage + 4%

            ( WeenieClassName.materialgranite,          1.00f ), // Weapon Variance - 50%
            ( WeenieClassName.materialoak,              1.00f ), // Weapon Speed - 50
            ( WeenieClassName.materialbrass,            1.00f ), // Weapon Melee Defense + 5%
            ( WeenieClassName.materialvelvet,           1.00f ), // Weapon Attack Skill + 5%
            ( WeenieClassName.materialopal,             1.00f ), // Mana Conversion + 5%

            ( WeenieClassName.materialperidot,          0.50f ), // Melee Defense + 3
            ( WeenieClassName.materialyellowtopaz,      0.50f ), // Missile Defense + 3
            ( WeenieClassName.materialzircon,           0.50f ), // Magic Defense + 3

            ( WeenieClassName.materialcarnelian,        0.50f ), // Minor Strength
            ( WeenieClassName.materialsmokyquartz,      0.50f ), // Minor Coordination
            ( WeenieClassName.materialbloodstone,       0.50f ), // Minor Endurance
            ( WeenieClassName.materialrosequartz,       0.50f ), // Minor Quickness
            ( WeenieClassName.materialagate,            0.50f ), // Minor Focus
            ( WeenieClassName.materiallapislazuli,      0.50f ), // Minor Willpower
            ( WeenieClassName.materialredjade,          0.50f ), // Minor Health Gain
            ( WeenieClassName.materialcitrine,          0.50f ), // Minor Stamina Gain
            ( WeenieClassName.materiallavenderjade,     0.50f ), // Minor Mana Gain

            ( WeenieClassName.materialmalachite,        0.50f ), // Warrior's Vigor
            ( WeenieClassName.materialhematite,         0.50f ), // Warrior's Vitality
            ( WeenieClassName.materialazurite,          0.50f ), // Wizard's Intellect

            ( WeenieClassName.materialsunstone,         0.33f ), // Armor Rending
            ( WeenieClassName.materialblackopal,        0.33f ), // Critical Strike
            ( WeenieClassName.materialfireopal,         0.33f ), // Crippling Blow
            ( WeenieClassName.materialtigereye,         0.33f ), // Elemental Rending

            ( WeenieClassName.materialblackgarnet,      1.00f ), // Pierce Element
            ( WeenieClassName.materialimperialtopaz,    1.00f ), // Slashing Element
            ( WeenieClassName.materialwhitesapphire,    1.00f ), // Bludgeoning Element
            ( WeenieClassName.materialredgarnet,        1.00f ), // Fire Element
            ( WeenieClassName.materialaquamarine,       1.00f ), // Frost Element
            ( WeenieClassName.materialjet,              1.00f ), // Lightning Element
            ( WeenieClassName.materialemerald,          1.00f ), // Acid Element

            //( WeenieClassName.materialonyx,             1.00f ), // Unused            
            //( WeenieClassName.materialgromniehide,      1.00f ), // Unused
            //( WeenieClassName.materialruby,             1.00f ), // Unused
            //( WeenieClassName.materialsapphire,         1.00f ), // Unused
            //( WeenieClassName.materialgreenjade,        1.00f ), // Unused
            //( WeenieClassName.materialtourmaline,       1.00f ), // Unused
            //( WeenieClassName.materialturquoise,        1.00f ), // Unused
            //( WeenieClassName.materialwhitequartz,      1.00f ), // Unused
            //( WeenieClassName.materialserpentine,       1.00f ), // Unused
            //( WeenieClassName.materialamethyst,         1.00f ), // Unused
            //( WeenieClassName.materialyellowgarnet,     1.00f ), // Unused
            //( WeenieClassName.materialwhitejade,        1.00f ), // Unused
            //( WeenieClassName.materialobsidian,         1.00f ), // Unused
        };

        public static WeenieClassName Roll(TreasureDeath profile)
        {
            return salvageWcids.Roll(profile.LootQualityMod);
        }

        private static readonly Dictionary<WeenieClassName, float> ValueMod = new Dictionary<WeenieClassName, float>()
        {
            { WeenieClassName.materialleather,          0.50f }, // Add Retained Status
            { WeenieClassName.materialsilk,             0.50f }, // Remove Allegiance Requirement
            { WeenieClassName.materialivory,            0.50f }, // Remove Attuned Status
            { WeenieClassName.materialsandstone,        0.50f }, // Remove Retained Status
            { WeenieClassName.materialteak,             0.50f }, // Switch to Aluvian
            { WeenieClassName.materialebony,            0.50f }, // Switch to Gharundim
            { WeenieClassName.materialporcelain,        0.50f }, // Switch to Sho

            { WeenieClassName.materialsilver,           1.00f }, // Arcane Lore - 10 Mana Rate * 200%
            { WeenieClassName.materialcopper,           1.00f }, // Arcane Lore - 5

            { WeenieClassName.materialgold,             1.00f }, // Value + 100% or 5000
            { WeenieClassName.materialpine,             1.00f }, // Value - 50%
            { WeenieClassName.materiallinen,            1.00f }, // Burden - 50%
            { WeenieClassName.materialmoonstone,        1.00f }, // Max Mana + 100% or 500
            { WeenieClassName.materialpyreal,           1.00f }, // Mana Rate * 50%
            { WeenieClassName.materialamber,            1.00f }, // Extra Spell Slots + 1 for regular items and + 2 for robes

            { WeenieClassName.materialsteel,            2.00f }, // AL + 25% or 20
            { WeenieClassName.materialalabaster,        1.25f }, // Armor Piercing Resist + 0.5
            { WeenieClassName.materialbronze,           1.25f }, // Armor Slashing Resist + 0.5
            { WeenieClassName.materialmarble,           1.25f }, // Armor Bludgeoning Resist + 0.5
            { WeenieClassName.materialceramic,          1.25f }, // Armor Fire Resist + 1.0
            { WeenieClassName.materialwool,             1.25f }, // Armor Cold Resist + 1.0
            { WeenieClassName.materialreedsharkhide,    1.25f }, // Armor Lightning Resist + 1.0
            { WeenieClassName.materialarmoredillohide,  1.25f }, // Armor Acid Resist + 1.0
            { WeenieClassName.materialsatin,            1.50f }, // Armor Max Evasion Penalty - 50%
            { WeenieClassName.materialdiamond,          1.50f }, // Shield Block Chance + 5%

            { WeenieClassName.materialiron,             2.00f }, // Weapon Damage + 4 for regular weapons and + 2 for multi-strike weapons
            { WeenieClassName.materialmahogany,         2.00f }, // Missile Weapon Mod + 16%
            { WeenieClassName.materialgreengarnet,      2.00f }, // Wand Damage + 4%

            { WeenieClassName.materialgranite,          1.25f }, // Weapon Variance - 50%
            { WeenieClassName.materialoak,              1.25f }, // Weapon Speed - 50
            { WeenieClassName.materialbrass,            1.50f }, // Weapon Melee Defense + 5%
            { WeenieClassName.materialvelvet,           1.50f }, // Weapon Attack Skill + 5%
            { WeenieClassName.materialopal,             1.50f }, // Mana Conversion + 5%

            { WeenieClassName.materialperidot,          1.00f }, // Melee Defense + 3
            { WeenieClassName.materialyellowtopaz,      1.00f }, // Missile Defense + 3
            { WeenieClassName.materialzircon,           1.00f }, // Magic Defense + 3

            { WeenieClassName.materialcarnelian,        2.00f }, // Minor Strength
            { WeenieClassName.materialsmokyquartz,      2.00f }, // Minor Coordination
            { WeenieClassName.materialbloodstone,       2.00f }, // Minor Endurance
            { WeenieClassName.materialrosequartz,       2.00f }, // Minor Quickness
            { WeenieClassName.materialagate,            2.00f }, // Minor Focus
            { WeenieClassName.materiallapislazuli,      2.00f }, // Minor Willpower
            { WeenieClassName.materialredjade,          2.00f }, // Minor Health Gain
            { WeenieClassName.materialcitrine,          2.00f }, // Minor Stamina Gain
            { WeenieClassName.materiallavenderjade,     2.00f }, // Minor Mana Gain

            { WeenieClassName.materialblackgarnet,      1.00f }, // Pierce Element
            { WeenieClassName.materialimperialtopaz,    1.00f }, // Slashing Element
            { WeenieClassName.materialwhitesapphire,    1.00f }, // Bludgeoning Element
            { WeenieClassName.materialredgarnet,        1.00f }, // Fire Element
            { WeenieClassName.materialaquamarine,       1.00f }, // Frost Element
            { WeenieClassName.materialjet,              1.00f }, // Lightning Element
            { WeenieClassName.materialemerald,          1.00f }, // Acid Element

            { WeenieClassName.materialmalachite,        2.00f }, // Warrior's Vigor
            { WeenieClassName.materialhematite,         2.00f }, // Warrior's Vitality
            { WeenieClassName.materialazurite,          2.00f }, // Wizard's Intellect

            { WeenieClassName.materialsunstone,         3.00f }, // Armor Rending
            { WeenieClassName.materialblackopal,        3.00f }, // Critical Strike
            { WeenieClassName.materialfireopal,         3.00f }, // Crippling Blow
            { WeenieClassName.materialtigereye,         3.00f }, // Elemental Rending

            //{ WeenieClassName.materialonyx              1.00f }, // Unused
            //{ WeenieClassName.materialgromniehide,      1.00f }, // Unused            
            //{ WeenieClassName.materialruby,             1.00f }, // Unused
            //{ WeenieClassName.materialsapphire,         1.00f }, // Unused            
            //{ WeenieClassName.materialgreenjade,        1.00f }, // Unused
            //{ WeenieClassName.materialtourmaline,       1.00f }, // Unused
            //{ WeenieClassName.materialturquoise,        1.00f }, // Unused
            //{ WeenieClassName.materialwhitequartz,      1.00f }, // Unused
            //{ WeenieClassName.materialserpentine,       1.00f }, // Unused
            //{ WeenieClassName.materialamethyst,         1.00f }, // Unused
            //{ WeenieClassName.materialyellowgarnet,     1.00f }, // Unused
            //{ WeenieClassName.materialwhitejade,        1.00f }, // Unused
            //{ WeenieClassName.materialobsidian,         1.00f }, // Unused
        };

        public static float GetValueMod(uint wcid)
        {
            if (ValueMod.TryGetValue((WeenieClassName)wcid, out var valueMod))
                return valueMod;
            else
                return 1.0f;
        }
    }
}
