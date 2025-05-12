using System.Collections.Generic;
using System.Linq;

namespace ACE.Entity.Enum
{
    /// <summary>
    /// note: even though these are unnumbered, order is very important.  values of "none" or commented
    /// as retired or unused --ABSOLUTELY CANNOT-- be removed. Skills that are none, retired, or not
    /// implemented have been removed from the SkillHelper.ValidSkills hashset below.
    /// </summary>
    public enum Skill
    {
        None,
        Axe,                 /* Retired */
        Bow,                 /* Retired */
        Crossbow,            /* Retired */
        Dagger,              /* Retired */
        Mace,                /* Retired */
        MeleeDefense,
        MissileDefense,
        Sling,               /* Retired */
        Spear,               /* Retired */
        Staff,               /* Retired */
        Sword,               /* Retired */
        ThrownWeapon,        /* Retired */
        UnarmedCombat,       /* Retired */
        ArcaneLore,
        MagicDefense,
        ManaConversion,
        Spellcraft,          /* Unimplemented */
        ItemTinkering,
        AssessPerson,
        Deception,
        Healing,
        Jump,
        Lockpick,
        Run,
        Awareness,           /* Unimplemented */
        ArmsAndArmorRepair,  /* Unimplemented */
        AssessCreature,
        WeaponTinkering,
        ArmorTinkering,
        MagicItemTinkering,
        CreatureEnchantment,
        ItemEnchantment,
        LifeMagic,
        WarMagic,
        Leadership,
        Loyalty,
        Fletching,
        Alchemy,
        Cooking,
        Salvaging,
        TwoHandedCombat,
        Gearcraft,           /* Retired */
        VoidMagic,
        HeavyWeapons,
        LightWeapons,
        FinesseWeapons,
        MissileWeapons,
        Shield,
        DualWield,
        Recklessness,
        SneakAttack,
        DirtyFighting,
        Challenge,          /* Unimplemented */
        Summoning,
        // CustomDM
        Appraise,
        Armor,
        Sneaking
    }

    public static class SkillExtensions
    {
        public static List<Skill> RetiredMelee = new List<Skill>()
        {
            Skill.Axe,
            Skill.Dagger,
            Skill.Mace,
            Skill.Spear,
            Skill.Staff,
            Skill.Sword,
            Skill.UnarmedCombat
        };

        public static List<Skill> RetiredMissile = new List<Skill>()
        {
            Skill.Bow,
            Skill.Crossbow,
            Skill.Sling,
            Skill.ThrownWeapon
        };

        public static List<Skill> RetiredWeapons = RetiredMelee.Concat(RetiredMissile).ToList();

        /// <summary>
        /// Will add a space infront of capital letter words in a string
        /// </summary>
        /// <param name="skill"></param>
        /// <returns>string with spaces infront of capital letters</returns>
        public static string ToSentence(this Skill skill)
        {
            switch (skill)
            {
                case Skill.None:
                    return "None";
                case Skill.Axe:
                    return "Axe";
                case Skill.Bow:
                    return "Bow";
                case Skill.Crossbow:
                    return "Crossbow";
                case Skill.Dagger:
                    return "Dagger";
                case Skill.Mace:
                    return "Mace";
                case Skill.MeleeDefense:
                    if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                        return "Melee Defense";
                    else
                        return "Evasion";
                case Skill.MissileDefense:
                    return "Missile Defense";
                case Skill.Sling:
                    return "Sling";
                case Skill.Spear:
                    return "Spear";
                case Skill.Staff:
                    return "Staff";
                case Skill.Sword:
                    return "Sword";
                case Skill.ThrownWeapon:
                    return "Thrown Weapon";
                case Skill.UnarmedCombat:
                    return "Unarmed Combat";
                case Skill.ArcaneLore:
                    return "Arcane Lore";
                case Skill.MagicDefense:
                    return "Magic Defense";
                case Skill.ManaConversion:
                    return "Mana Conversion";
                case Skill.Spellcraft:
                    return "Spellcraft";
                case Skill.ItemTinkering:
                    return "Item Tinkering";
                case Skill.AssessPerson:
                    return "Assess Person";
                case Skill.Deception:
                    return "Deception";
                case Skill.Healing:
                    return "Healing";
                case Skill.Jump:
                    return "Jump";
                case Skill.Lockpick:
                    return "Lockpick";
                case Skill.Run:
                    return "Run";
                case Skill.Awareness:
                    return "Awareness";
                case Skill.ArmsAndArmorRepair:
                    return "Arms and Armor Repair";
                case Skill.AssessCreature:
                    if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                        return "Assess Creature";
                    else
                        return "Assess";
                case Skill.WeaponTinkering:
                    return "Weapon Tinkering";
                case Skill.ArmorTinkering:
                    return "Armor Tinkering";
                case Skill.MagicItemTinkering:
                    return "Magic Item Tinkering";
                case Skill.CreatureEnchantment:
                    return "Creature Enchantment";
                case Skill.ItemEnchantment:
                    return "Item Enchantment";
                case Skill.LifeMagic:
                    return "Life Magic";
                case Skill.WarMagic:
                    return "War Magic";
                case Skill.Leadership:
                    return "Leadership";
                case Skill.Loyalty:
                    return "Loyalty";
                case Skill.Fletching:
                    return "Fletching";
                case Skill.Alchemy:
                    return "Alchemy";
                case Skill.Cooking:
                    return "Cooking";
                case Skill.Salvaging:
                    return "Salvaging";
                case Skill.TwoHandedCombat:
                    return "Two Handed Combat";
                case Skill.Gearcraft:
                    return "Gearcraft";
                case Skill.VoidMagic:
                    return "Void Magic";
                case Skill.HeavyWeapons:
                    return "Heavy Weapons";
                case Skill.LightWeapons:
                    return "Light Weapons";
                case Skill.FinesseWeapons:
                    return "Finesse Weapons";
                case Skill.MissileWeapons:
                    return "Missile Weapons";
                case Skill.Shield:
                    return "Shield";
                case Skill.DualWield:
                    return "Dual Wield";
                case Skill.Recklessness:
                    return "Recklessness";
                case Skill.SneakAttack:
                    return "Sneak Attack";
                case Skill.DirtyFighting:
                    return "Dirty Fighting";
                case Skill.Challenge:
                    return "Challenge";
                case Skill.Summoning:
                    return "Summoning";
                case Skill.Armor:
                    return "Armor";
                case Skill.Appraise:
                    return "Appraise";
                case Skill.Sneaking:
                    return "Sneaking";
            }

            // TODO we really should log this as a warning to indicate that we're missing a case up above, and that the inefficient (GC unfriendly) line below will be used
            return new string(skill.ToString().ToCharArray().SelectMany((c, i) => i > 0 && char.IsUpper(c) ? new char[] { ' ', c } : new char[] { c }).ToArray());
        }
    }

    public static class SkillHelper
    {
        static SkillHelper()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                ValidSkills.Remove(Skill.TwoHandedCombat);
                ValidSkills.Remove(Skill.HeavyWeapons);
                ValidSkills.Remove(Skill.LightWeapons);
                ValidSkills.Remove(Skill.FinesseWeapons);
                ValidSkills.Remove(Skill.MissileWeapons);
                ValidSkills.Remove(Skill.Shield);
                ValidSkills.Remove(Skill.DualWield);
                ValidSkills.Remove(Skill.Recklessness);
                ValidSkills.Remove(Skill.SneakAttack);
                ValidSkills.Remove(Skill.DirtyFighting);
                ValidSkills.Remove(Skill.VoidMagic);
                ValidSkills.Remove(Skill.Summoning);

                ValidSkills.Add(Skill.Axe);
                ValidSkills.Add(Skill.Bow);
                ValidSkills.Add(Skill.Crossbow);
                ValidSkills.Add(Skill.Dagger);
                ValidSkills.Add(Skill.Mace);
                ValidSkills.Add(Skill.Spear);
                ValidSkills.Add(Skill.Staff);
                ValidSkills.Add(Skill.Sword);
                ValidSkills.Add(Skill.ThrownWeapon);
                ValidSkills.Add(Skill.UnarmedCombat);
                ValidSkills.Add(Skill.Salvaging);
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                ValidSkills.Remove(Skill.TwoHandedCombat);
                ValidSkills.Remove(Skill.HeavyWeapons);
                ValidSkills.Remove(Skill.LightWeapons);
                ValidSkills.Remove(Skill.FinesseWeapons);
                ValidSkills.Remove(Skill.MissileWeapons);
                ValidSkills.Remove(Skill.DualWield);
                ValidSkills.Remove(Skill.Recklessness);
                ValidSkills.Remove(Skill.SneakAttack);
                ValidSkills.Remove(Skill.DirtyFighting);
                ValidSkills.Remove(Skill.VoidMagic);
                ValidSkills.Remove(Skill.Summoning);

                ValidSkills.Remove(Skill.AssessPerson);
                ValidSkills.Remove(Skill.ItemEnchantment);
                ValidSkills.Remove(Skill.CreatureEnchantment);
                ValidSkills.Remove(Skill.Crossbow);
                ValidSkills.Remove(Skill.Mace);
                ValidSkills.Remove(Skill.Staff);

                ValidSkills.Remove(Skill.MissileDefense);

                ValidSkills.Add(Skill.Axe);
                ValidSkills.Add(Skill.Bow);
                ValidSkills.Add(Skill.Crossbow);
                ValidSkills.Add(Skill.Dagger);
                ValidSkills.Add(Skill.Mace);
                ValidSkills.Add(Skill.Spear);
                ValidSkills.Add(Skill.Staff);
                ValidSkills.Add(Skill.Sword);
                ValidSkills.Add(Skill.ThrownWeapon);
                ValidSkills.Add(Skill.UnarmedCombat);
                ValidSkills.Add(Skill.Salvaging);

                ValidSkills.Add(Skill.Awareness);
                ValidSkills.Add(Skill.Appraise);
                ValidSkills.Add(Skill.Armor);
                ValidSkills.Add(Skill.Sneaking);
            }
        }

        public static HashSet<Skill> ValidSkills = new HashSet<Skill>
        {
            Skill.MeleeDefense,
            Skill.MissileDefense,
            Skill.ArcaneLore,
            Skill.MagicDefense,
            Skill.ManaConversion,
            Skill.ItemTinkering,
            Skill.AssessPerson,
            Skill.Deception,
            Skill.Healing,
            Skill.Jump,
            Skill.Lockpick,
            Skill.Run,
            Skill.AssessCreature,
            Skill.WeaponTinkering,
            Skill.ArmorTinkering,
            Skill.MagicItemTinkering,
            Skill.CreatureEnchantment,
            Skill.ItemEnchantment,
            Skill.LifeMagic,
            Skill.WarMagic,
            Skill.Leadership,
            Skill.Loyalty,
            Skill.Fletching,
            Skill.Alchemy,
            Skill.Cooking,
            Skill.Salvaging,
            Skill.TwoHandedCombat,
            Skill.VoidMagic,
            Skill.HeavyWeapons,
            Skill.LightWeapons,
            Skill.FinesseWeapons,
            Skill.MissileWeapons,
            Skill.Shield,
            Skill.DualWield,
            Skill.Recklessness,
            Skill.SneakAttack,
            Skill.DirtyFighting,
            Skill.Summoning
        };

        public static HashSet<Skill> AttackSkills = new HashSet<Skill>
        {
            Skill.Axe,
            Skill.Bow,
            Skill.Crossbow,
            Skill.Dagger,
            Skill.Mace,
            Skill.Sling,
            Skill.Spear,
            Skill.Staff,
            Skill.Sword,
            Skill.ThrownWeapon,
            Skill.UnarmedCombat,
            Skill.FinesseWeapons,
            Skill.HeavyWeapons,
            Skill.LightWeapons,
            Skill.MissileWeapons,
            Skill.TwoHandedCombat,
            Skill.WarMagic,
            Skill.LifeMagic,
            Skill.VoidMagic,
            Skill.DualWield,
            //Skill.Recklessness,   // confirmed not in client
            //Skill.DirtyFighting,
            //Skill.SneakAttack
        };

        public static HashSet<Skill> DefenseSkills = new HashSet<Skill>()
        {
            Skill.MeleeDefense,
            Skill.MissileDefense,
            Skill.MagicDefense,
            Skill.Shield            // confirmed in client
        };
    }
}
