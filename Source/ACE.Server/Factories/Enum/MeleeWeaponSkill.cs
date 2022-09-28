using ACE.Entity.Enum;

namespace ACE.Server.Factories.Enum
{
    public enum MeleeWeaponSkill
    {
        Undef,
        HeavyWeapons,
        LightWeapons,
        FinesseWeapons,
        TwoHandedCombat,
        Axe,
        Dagger,
        Mace,
        Spear,
        Staff,
        Sword,
        UnarmedCombat
    };

    public static class MeleeWeaponSkillExtensions
    {
        public static MeleeWeaponSkill ToMeleeWeaponSkill(this Skill skill)
        {
            switch (skill)
            {
                case Skill.HeavyWeapons:
                    return MeleeWeaponSkill.HeavyWeapons;
                case Skill.LightWeapons:
                    return MeleeWeaponSkill.LightWeapons;
                case Skill.FinesseWeapons:
                    return MeleeWeaponSkill.FinesseWeapons;
                case Skill.TwoHandedCombat:
                    return MeleeWeaponSkill.TwoHandedCombat;
                case Skill.Axe:
                    return MeleeWeaponSkill.Axe;
                case Skill.Dagger:
                    return MeleeWeaponSkill.Dagger;
                case Skill.Mace:
                    return MeleeWeaponSkill.Mace;
                case Skill.Spear:
                    return MeleeWeaponSkill.Spear;
                case Skill.Staff:
                    return MeleeWeaponSkill.Staff;
                case Skill.Sword:
                    return MeleeWeaponSkill.Sword;
                case Skill.UnarmedCombat:
                    return MeleeWeaponSkill.UnarmedCombat;
            }
            return MeleeWeaponSkill.Undef;
        }

        public static string GetScriptName(this MeleeWeaponSkill weaponSkill)
        {
            switch (weaponSkill)
            {
                case MeleeWeaponSkill.HeavyWeapons:
                    return "heavy";
                case MeleeWeaponSkill.LightWeapons:
                    return "light";
                case MeleeWeaponSkill.FinesseWeapons:
                    return "finesse";
                case MeleeWeaponSkill.TwoHandedCombat:
                    return "two_handed";
                case MeleeWeaponSkill.Axe:
                    return "axe";
                case MeleeWeaponSkill.Dagger:
                    return "dagger";
                case MeleeWeaponSkill.Mace:
                    return "mace";
                case MeleeWeaponSkill.Spear:
                    return "spear";
                case MeleeWeaponSkill.Staff:
                    return "staff";
                case MeleeWeaponSkill.Sword:
                    return "sword";
                case MeleeWeaponSkill.UnarmedCombat:
                    return "unarmed";
            }
            return null;
        }

        public static string GetScriptName_Combined(this MeleeWeaponSkill weaponSkill)
        {
            switch (weaponSkill)
            {
                case MeleeWeaponSkill.HeavyWeapons:
                    return "heavy";
                case MeleeWeaponSkill.LightWeapons:
                case MeleeWeaponSkill.FinesseWeapons:
                    return "light_finesse";
                case MeleeWeaponSkill.TwoHandedCombat:
                    return "two_handed";
            }
            return null;
        }
    }
}
