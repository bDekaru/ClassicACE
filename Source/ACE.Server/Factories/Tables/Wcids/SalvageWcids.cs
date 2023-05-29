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
            ( WeenieClassName.materialleather,          5.00f ), // Add Retained Status
            ( WeenieClassName.materialsilk,             5.00f ), // Remove Allegiance Requirement
            ( WeenieClassName.materialivory,            5.00f ), // Remove Attuned Status
            ( WeenieClassName.materialsandstone,        5.00f ), // Remove Retained Status
            ( WeenieClassName.materialsilver,           5.00f ), // Switch Melee to Missile
            ( WeenieClassName.materialcopper,           5.00f ), // Switch Missile to Melee
            ( WeenieClassName.materialteak,             5.00f ), // Switch to Aluvian
            ( WeenieClassName.materialebony,            5.00f ), // Switch to Gharundim
            ( WeenieClassName.materialporcelain,        5.00f ), // Switch to Sho

            ( WeenieClassName.materialgold,             5.00f ), // Value + 25%
            ( WeenieClassName.materialpine,             5.00f ), // Value - 25%
            ( WeenieClassName.materiallinen,            5.00f ), // Burden - 25%
            ( WeenieClassName.materialmoonstone,        5.00f ), // Max Mana + 500

            ( WeenieClassName.materialsteel,            2.00f ), // AL + 20
            ( WeenieClassName.materialalabaster,        3.00f ), // Armor Piercing Resist + 0.2
            ( WeenieClassName.materialbronze,           3.00f ), // Armor Slashing Resist + 0.2
            ( WeenieClassName.materialmarble,           3.00f ), // Armor Bludgeoning Resist + 0.2
            ( WeenieClassName.materialceramic,          3.00f ), // Armor Fire Resist + 0.4
            ( WeenieClassName.materialwool,             3.00f ), // Armor Cold Resist + 0.4
            ( WeenieClassName.materialreedsharkhide,    3.00f ), // Armor Lightning Resist + 0.4
            ( WeenieClassName.materialarmoredillohide,  3.00f ), // Armor Acid Resist + 0.4

            ( WeenieClassName.materialiron,             2.00f ), // Weapon Damage + 1
            ( WeenieClassName.materialmahogany,         2.00f ), // Missile Weapon Mod + 4%

            ( WeenieClassName.materialgranite,          3.00f ), // Weapon Variance - 20%
            ( WeenieClassName.materialoak,              3.00f ), // Weapon Speed - 15
            ( WeenieClassName.materialbrass,            3.00f ), // Weapon Melee Defense + 1%
            ( WeenieClassName.materialvelvet,           3.00f ), // Weapon Attack Skill + 1%

            ( WeenieClassName.materialopal,             2.00f ), // Mana Conversion + 1
            ( WeenieClassName.materialperidot,          2.00f ), // Melee Defense + 1
            ( WeenieClassName.materialyellowtopaz,      2.00f ), // Missile Defense + 1
            ( WeenieClassName.materialzircon,           2.00f ), // Magic Defense + 1

            ( WeenieClassName.materialcarnelian,        1.00f ), // Minor Strength
            ( WeenieClassName.materialsmokyquartz,      1.00f ), // Minor Coordination
            ( WeenieClassName.materialbloodstone,       1.00f ), // Minor Endurance
            ( WeenieClassName.materialrosequartz,       1.00f ), // Minor Quickness
            ( WeenieClassName.materialagate,            1.00f ), // Minor Focus
            ( WeenieClassName.materiallapislazuli,      1.00f ), // Minor Willpower
            ( WeenieClassName.materialredjade,          1.00f ), // Minor Health Gain
            ( WeenieClassName.materialcitrine,          1.00f ), // Minor Stamina Gain
            ( WeenieClassName.materiallavenderjade,     1.00f ), // Minor Mana Gain

            ( WeenieClassName.materialtigereye,         0.50f ), // Fire Element
            ( WeenieClassName.materialwhitequartz,      0.50f ), // Cold Element
            ( WeenieClassName.materialserpentine,       0.50f ), // Acid Element
            ( WeenieClassName.materialamethyst,         0.50f ), // Lightning Element
            ( WeenieClassName.materialyellowgarnet,     0.50f ), // Slash Element
            ( WeenieClassName.materialwhitejade,        0.50f ), // Pierce Element
            ( WeenieClassName.materialobsidian,         0.50f ), // Bludge Element

            ( WeenieClassName.materialmalachite,        0.10f ), // Warrior's Vigor
            ( WeenieClassName.materialhematite,         0.10f ), // Warrior's Vitality
            ( WeenieClassName.materialazurite,          0.10f ), // Wizard's Intellect

            ( WeenieClassName.materialsunstone,         0.10f ), // Armor Rending
            ( WeenieClassName.materialblackopal,        0.10f ), // Critical Strike
            ( WeenieClassName.materialfireopal,         0.10f ), // Critical Blow
            ( WeenieClassName.materialblackgarnet,      0.10f ), // Pierce Rend
            ( WeenieClassName.materialimperialtopaz,    0.10f ), // Slashing Rend
            ( WeenieClassName.materialwhitesapphire,    0.10f ), // Bludgeoning Rend
            ( WeenieClassName.materialredgarnet,        0.10f ), // Fire Rend
            ( WeenieClassName.materialaquamarine,       0.10f ), // Frost Rend
            ( WeenieClassName.materialjet,              0.10f ), // Lightning Rend
            ( WeenieClassName.materialemerald,          0.10f ), // Acid Rend

            //( WeenieClassName.materialgreengarnet,      2.00f ), // Wand Damage + 1%
            //( WeenieClassName.materialsatin,            6.00f ), // Switch to Viamontian
            //( WeenieClassName.materialdiamond,          1.00f ), // Armature
            //( WeenieClassName.materialamber,            1.00f ), // Armature
            //( WeenieClassName.materialgromniehide,      1.00f ), // Armature
            //( WeenieClassName.materialpyreal,           1.00f ), // Armature
            //( WeenieClassName.materialruby,             1.00f ), // Armature
            //( WeenieClassName.materialsapphire,         1.00f ), // Armature
            //( WeenieClassName.materialgreenjade,        1.00f ), // Unused
            //( WeenieClassName.materialonyx,             1.00f ), // Unused
            //( WeenieClassName.materialtourmaline,       1.00f ), // Unused
            //( WeenieClassName.materialturquoise,        1.00f ), // Unused
        };

        public static WeenieClassName Roll(TreasureDeath profile)
        {
            return salvageWcids.Roll(profile.LootQualityMod);
        }

        private static readonly Dictionary<WeenieClassName, float> ValueMod = new Dictionary<WeenieClassName, float>()
        {
            { WeenieClassName.materialleather,          0.25f }, // Add Retained Status
            { WeenieClassName.materialsilk,             0.50f }, // Remove Allegiance Requirement
            { WeenieClassName.materialivory,            0.25f }, // Remove Attuned Status
            { WeenieClassName.materialsandstone,        0.25f }, // Remove Retained Status
            { WeenieClassName.materialsilver,           0.50f }, // Switch Melee to Missile
            { WeenieClassName.materialcopper,           0.50f }, // Switch Missile to Melee
            { WeenieClassName.materialteak,             0.50f }, // Switch to Aluvian
            { WeenieClassName.materialebony,            0.50f }, // Switch to Gharundim
            { WeenieClassName.materialporcelain,        0.50f }, // Switch to Sho

            { WeenieClassName.materialgold,             1.00f }, // Value + 25%
            { WeenieClassName.materialpine,             1.00f }, // Value - 25%
            { WeenieClassName.materiallinen,            1.00f }, // Burden - 25%
            { WeenieClassName.materialmoonstone,        1.00f }, // Max Mana + 500

            { WeenieClassName.materialsteel,            1.50f }, // AL + 20
            { WeenieClassName.materialalabaster,        1.25f }, // Armor Piercing Resist + 0.2
            { WeenieClassName.materialbronze,           1.25f }, // Armor Slashing Resist + 0.2
            { WeenieClassName.materialmarble,           1.25f }, // Armor Bludgeoning Resist + 0.2
            { WeenieClassName.materialceramic,          1.25f }, // Armor Fire Resist + 0.4
            { WeenieClassName.materialwool,             1.25f }, // Armor Cold Resist + 0.4
            { WeenieClassName.materialreedsharkhide,    1.25f }, // Armor Lightning Resist + 0.4
            { WeenieClassName.materialarmoredillohide,  1.25f }, // Armor Acid Resist + 0.4

            { WeenieClassName.materialiron,             1.50f }, // Weapon Damage + 1
            { WeenieClassName.materialmahogany,         1.50f }, // Missile Weapon Mod + 4%

            { WeenieClassName.materialgranite,          1.25f }, // Weapon Variance - 20%
            { WeenieClassName.materialoak,              1.25f }, // Weapon Speed - 15
            { WeenieClassName.materialbrass,            1.50f }, // Weapon Melee Defense + 1%
            { WeenieClassName.materialvelvet,           1.50f }, // Weapon Attack Skill + 1%

            { WeenieClassName.materialopal,             1.25f }, // Mana Conversion + 1
            { WeenieClassName.materialperidot,          1.25f }, // Melee Defense + 1
            { WeenieClassName.materialyellowtopaz,      1.25f }, // Missile Defense + 1
            { WeenieClassName.materialzircon,           1.25f }, // Magic Defense + 1

            { WeenieClassName.materialcarnelian,        2.00f }, // Minor Strength
            { WeenieClassName.materialsmokyquartz,      2.00f }, // Minor Coordination
            { WeenieClassName.materialbloodstone,       2.00f }, // Minor Endurance
            { WeenieClassName.materialrosequartz,       2.00f }, // Minor Quickness
            { WeenieClassName.materialagate,            2.00f }, // Minor Focus
            { WeenieClassName.materiallapislazuli,      2.00f }, // Minor Willpower
            { WeenieClassName.materialredjade,          2.00f }, // Minor Health Gain
            { WeenieClassName.materialcitrine,          2.00f }, // Minor Stamina Gain
            { WeenieClassName.materiallavenderjade,     2.00f }, // Minor Mana Gain

            { WeenieClassName.materialtigereye,         2.00f }, // Fire Element
            { WeenieClassName.materialwhitequartz,      2.00f }, // Cold Element
            { WeenieClassName.materialserpentine,       2.00f }, // Acid Element
            { WeenieClassName.materialamethyst,         2.00f }, // Lightning Element
            { WeenieClassName.materialyellowgarnet,     2.00f }, // Slash Element
            { WeenieClassName.materialwhitejade,        2.00f }, // Pierce Element
            { WeenieClassName.materialobsidian,         2.00f }, // Bludge Element

            { WeenieClassName.materialmalachite,        3.00f }, // Warrior's Vigor
            { WeenieClassName.materialhematite,         3.00f }, // Warrior's Vitality
            { WeenieClassName.materialazurite,          3.00f }, // Wizard's Intellect

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
            //{ WeenieClassName.materialsatin,            1.00f }, // Switch to Viamontian
            //{ WeenieClassName.materialdiamond,          1.00f }, // Armature
            //{ WeenieClassName.materialamber,            1.00f }, // Armature
            //{ WeenieClassName.materialgromniehide,      1.00f }, // Armature
            //{ WeenieClassName.materialpyreal,           1.00f }, // Armature
            //{ WeenieClassName.materialruby,             1.00f }, // Armature
            //{ WeenieClassName.materialsapphire,         1.00f }, // Armature            
            //{ WeenieClassName.materialgreenjade,        1.00f }, // Unused            
            //{ WeenieClassName.materialonyx,             1.00f }, // Unused
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
