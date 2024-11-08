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
            ( WeenieClassName.materialsilver,           1.00f ), // Switch Melee to Missile
            ( WeenieClassName.materialcopper,           1.00f ), // Switch Missile to Melee
            ( WeenieClassName.materialteak,             1.00f ), // Switch to Aluvian
            ( WeenieClassName.materialebony,            1.00f ), // Switch to Gharundim
            ( WeenieClassName.materialporcelain,        1.00f ), // Switch to Sho

            ( WeenieClassName.materialgold,             1.00f ), // Value + 100% or 5000
            ( WeenieClassName.materialpine,             1.00f ), // Value - 50%
            ( WeenieClassName.materiallinen,            1.00f ), // Burden - 50%
            ( WeenieClassName.materialmoonstone,        1.00f ), // Max Mana + 100% or 500

            ( WeenieClassName.materialsteel,            1.00f ), // AL + 25% or 20
            ( WeenieClassName.materialalabaster,        1.00f ), // Armor Piercing Resist + 0.5
            ( WeenieClassName.materialbronze,           1.00f ), // Armor Slashing Resist + 0.5
            ( WeenieClassName.materialmarble,           1.00f ), // Armor Bludgeoning Resist + 0.5
            ( WeenieClassName.materialceramic,          1.00f ), // Armor Fire Resist + 1.0
            ( WeenieClassName.materialwool,             1.00f ), // Armor Cold Resist + 1.0
            ( WeenieClassName.materialreedsharkhide,    1.00f ), // Armor Lightning Resist + 1.0
            ( WeenieClassName.materialarmoredillohide,  1.00f ), // Armor Acid Resist + 1.0

            ( WeenieClassName.materialiron,             1.00f ), // Weapon Damage + 4 for regular weapons and +2 for multi-strike weapons
            ( WeenieClassName.materialmahogany,         1.00f ), // Missile Weapon Mod + 16%

            ( WeenieClassName.materialgranite,          1.00f ), // Weapon Variance - 50%
            ( WeenieClassName.materialoak,              1.00f ), // Weapon Speed - 50
            ( WeenieClassName.materialbrass,            1.00f ), // Weapon Melee Defense + 5%
            ( WeenieClassName.materialvelvet,           1.00f ), // Weapon Attack Skill + 5%
            ( WeenieClassName.materialonyx,             1.00f ), // Shield Block Chance + 5%
            ( WeenieClassName.materialsatin,            1.00f ), // Armor Max Evasion Penalty - 5%

            ( WeenieClassName.materialopal,             0.50f ), // Mana Conversion + 1
            ( WeenieClassName.materialperidot,          0.50f ), // Melee Defense + 1
            ( WeenieClassName.materialyellowtopaz,      0.50f ), // Missile Defense + 1
            ( WeenieClassName.materialzircon,           0.50f ), // Magic Defense + 1

            ( WeenieClassName.materialcarnelian,        0.50f ), // Minor Strength
            ( WeenieClassName.materialsmokyquartz,      0.50f ), // Minor Coordination
            ( WeenieClassName.materialbloodstone,       0.50f ), // Minor Endurance
            ( WeenieClassName.materialrosequartz,       0.50f ), // Minor Quickness
            ( WeenieClassName.materialagate,            0.50f ), // Minor Focus
            ( WeenieClassName.materiallapislazuli,      0.50f ), // Minor Willpower
            ( WeenieClassName.materialredjade,          0.50f ), // Minor Health Gain
            ( WeenieClassName.materialcitrine,          0.50f ), // Minor Stamina Gain
            ( WeenieClassName.materiallavenderjade,     0.50f ), // Minor Mana Gain

            ( WeenieClassName.materialtigereye,         0.80f ), // Fire Element
            ( WeenieClassName.materialwhitequartz,      0.80f ), // Cold Element
            ( WeenieClassName.materialserpentine,       0.80f ), // Acid Element
            ( WeenieClassName.materialamethyst,         0.80f ), // Lightning Element
            ( WeenieClassName.materialyellowgarnet,     0.80f ), // Slash Element
            ( WeenieClassName.materialwhitejade,        0.80f ), // Pierce Element
            ( WeenieClassName.materialobsidian,         0.80f ), // Bludge Element

            ( WeenieClassName.materialmalachite,        0.50f ), // Warrior's Vigor
            ( WeenieClassName.materialhematite,         0.50f ), // Warrior's Vitality
            ( WeenieClassName.materialazurite,          0.50f ), // Wizard's Intellect

            ( WeenieClassName.materialsunstone,         0.33f ), // Armor Rending
            ( WeenieClassName.materialblackopal,        0.33f ), // Critical Strike
            ( WeenieClassName.materialfireopal,         0.33f ), // Critical Blow
            ( WeenieClassName.materialblackgarnet,      0.33f ), // Pierce Rend
            ( WeenieClassName.materialimperialtopaz,    0.33f ), // Slashing Rend
            ( WeenieClassName.materialwhitesapphire,    0.33f ), // Bludgeoning Rend
            ( WeenieClassName.materialredgarnet,        0.33f ), // Fire Rend
            ( WeenieClassName.materialaquamarine,       0.33f ), // Frost Rend
            ( WeenieClassName.materialjet,              0.33f ), // Lightning Rend
            ( WeenieClassName.materialemerald,          0.33f ), // Acid Rend

            //( WeenieClassName.materialgreengarnet,      1.00f ), // Wand Damage + 1%
            //( WeenieClassName.materialdiamond,          1.00f ), // Armature
            //( WeenieClassName.materialamber,            1.00f ), // Armature
            //( WeenieClassName.materialgromniehide,      1.00f ), // Armature
            //( WeenieClassName.materialpyreal,           1.00f ), // Armature
            //( WeenieClassName.materialruby,             1.00f ), // Armature
            //( WeenieClassName.materialsapphire,         1.00f ), // Armature
            //( WeenieClassName.materialgreenjade,        1.00f ), // Unused
            //( WeenieClassName.materialtourmaline,       1.00f ), // Unused
            //( WeenieClassName.materialturquoise,        1.00f ), // Unused
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
            { WeenieClassName.materialsilver,           0.50f }, // Switch Melee to Missile
            { WeenieClassName.materialcopper,           0.50f }, // Switch Missile to Melee
            { WeenieClassName.materialteak,             0.50f }, // Switch to Aluvian
            { WeenieClassName.materialebony,            0.50f }, // Switch to Gharundim
            { WeenieClassName.materialporcelain,        0.50f }, // Switch to Sho

            { WeenieClassName.materialgold,             1.00f }, // Value + 100% or 5000
            { WeenieClassName.materialpine,             1.00f }, // Value - 50%
            { WeenieClassName.materiallinen,            1.00f }, // Burden - 50%
            { WeenieClassName.materialmoonstone,        1.00f }, // Max Mana + 100% or 500

            { WeenieClassName.materialsteel,            2.00f }, // AL + 25% or 20
            { WeenieClassName.materialalabaster,        1.25f }, // Armor Piercing Resist + 0.5
            { WeenieClassName.materialbronze,           1.25f }, // Armor Slashing Resist + 0.5
            { WeenieClassName.materialmarble,           1.25f }, // Armor Bludgeoning Resist + 0.5
            { WeenieClassName.materialceramic,          1.25f }, // Armor Fire Resist + 1.0
            { WeenieClassName.materialwool,             1.25f }, // Armor Cold Resist + 1.0
            { WeenieClassName.materialreedsharkhide,    1.25f }, // Armor Lightning Resist + 1.0
            { WeenieClassName.materialarmoredillohide,  1.25f }, // Armor Acid Resist + 1.0
            { WeenieClassName.materialonyx,             1.50f }, // Shield Block Chance + 5%
            { WeenieClassName.materialsatin,            1.50f }, // Armor Max Evasion Penalty - 5%

            { WeenieClassName.materialiron,             2.00f }, // Weapon Damage + 4 for regular weapons and +2 for multi-strike weapons
            { WeenieClassName.materialmahogany,         2.00f }, // Missile Weapon Mod + 16%

            { WeenieClassName.materialgranite,          1.25f }, // Weapon Variance - 50%
            { WeenieClassName.materialoak,              1.25f }, // Weapon Speed - 50
            { WeenieClassName.materialbrass,            1.50f }, // Weapon Melee Defense + 5%
            { WeenieClassName.materialvelvet,           1.50f }, // Weapon Attack Skill + 5%

            { WeenieClassName.materialopal,             1.00f }, // Mana Conversion + 1
            { WeenieClassName.materialperidot,          1.00f }, // Melee Defense + 1
            { WeenieClassName.materialyellowtopaz,      1.00f }, // Missile Defense + 1
            { WeenieClassName.materialzircon,           1.00f }, // Magic Defense + 1

            { WeenieClassName.materialcarnelian,        2.00f }, // Minor Strength
            { WeenieClassName.materialsmokyquartz,      2.00f }, // Minor Coordination
            { WeenieClassName.materialbloodstone,       2.00f }, // Minor Endurance
            { WeenieClassName.materialrosequartz,       2.00f }, // Minor Quickness
            { WeenieClassName.materialagate,            2.00f }, // Minor Focus
            { WeenieClassName.materiallapislazuli,      2.00f }, // Minor Willpower
            { WeenieClassName.materialredjade,          2.00f }, // Minor Health Gain
            { WeenieClassName.materialcitrine,          2.00f }, // Minor Stamina Gain
            { WeenieClassName.materiallavenderjade,     2.00f }, // Minor Mana Gain

            { WeenieClassName.materialtigereye,         1.00f }, // Fire Element
            { WeenieClassName.materialwhitequartz,      1.00f }, // Cold Element
            { WeenieClassName.materialserpentine,       1.00f }, // Acid Element
            { WeenieClassName.materialamethyst,         1.00f }, // Lightning Element
            { WeenieClassName.materialyellowgarnet,     1.00f }, // Slash Element
            { WeenieClassName.materialwhitejade,        1.00f }, // Pierce Element
            { WeenieClassName.materialobsidian,         1.00f }, // Bludge Element

            { WeenieClassName.materialmalachite,        2.00f }, // Warrior's Vigor
            { WeenieClassName.materialhematite,         2.00f }, // Warrior's Vitality
            { WeenieClassName.materialazurite,          2.00f }, // Wizard's Intellect

            { WeenieClassName.materialsunstone,         3.00f }, // Armor Rending
            { WeenieClassName.materialblackopal,        3.00f }, // Critical Strike
            { WeenieClassName.materialfireopal,         3.00f }, // Critical Blow
            { WeenieClassName.materialblackgarnet,      3.00f }, // Pierce Rend
            { WeenieClassName.materialimperialtopaz,    3.00f }, // Slashing Rend
            { WeenieClassName.materialwhitesapphire,    3.00f }, // Bludgeoning Rend
            { WeenieClassName.materialredgarnet,        3.00f }, // Fire Rend
            { WeenieClassName.materialaquamarine,       3.00f }, // Frost Rend
            { WeenieClassName.materialjet,              3.00f }, // Lightning Rend
            { WeenieClassName.materialemerald,          3.00f }, // Acid Rend

            //{ WeenieClassName.materialgreengarnet,      1.00f }, // Wand Damage + 1%
            //{ WeenieClassName.materialdiamond,          1.00f }, // Armature
            //{ WeenieClassName.materialamber,            1.00f }, // Armature
            //{ WeenieClassName.materialgromniehide,      1.00f }, // Armature
            //{ WeenieClassName.materialpyreal,           1.00f }, // Armature
            //{ WeenieClassName.materialruby,             1.00f }, // Armature
            //{ WeenieClassName.materialsapphire,         1.00f }, // Armature            
            //{ WeenieClassName.materialgreenjade,        1.00f }, // Unused
            //{ WeenieClassName.materialtourmaline,       1.00f }, // Unused
            //{ WeenieClassName.materialturquoise,        1.00f }, // Unused
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
