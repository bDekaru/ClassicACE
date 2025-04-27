using ACE.Entity.Enum;
using ACE.Server.Factories.Enum;
using ACE.Server.WorldObjects;
using System.Collections.Generic;

namespace ACE.Server.Factories.Entity
{
    public class TreasureRoll
    {
        public TreasureItemType_Orig ItemType;
        public TreasureArmorType ArmorType;
        public TreasureWeaponType WeaponType;
        public TreasureHeritageGroup Heritage;

        public Enum.WeenieClassName Wcid;

        public int BaseArmorLevel;

        public int MinEffectiveSpellcraft;
        public int MaxEffectiveSpellcraft;
        public int RolledEffectiveSpellcraft;
        public int RealSpellcraft;

        public int MinArcaneLore;
        public int MaxArcaneLore;
        public int RolledArcaneLore;

        public List<SpellId> AllSpells;
        public List<SpellId> ItemEnchantments;
        public List<SpellId> LifeCreatureEnchantments;
        public List<SpellId> Cantrips;

        public TreasureRoll() { }

        public TreasureRoll(TreasureItemType_Orig itemType)
        {
            ItemType = itemType;
        }

        public string GetItemType()
        {
            switch (ItemType)
            {
                case TreasureItemType_Orig.Armor:
                    return ArmorType.ToString();
                case TreasureItemType_Orig.Weapon:
                    return WeaponType.ToString();
            }
            return ItemType.ToString();
        }

        public static TreasureWeaponType GetWeaponTypeFromWeapon(WorldObject weapon)
        {
            switch (weapon.WeaponSkill)
            {
                case Skill.UnarmedCombat:
                    return TreasureWeaponType.Unarmed;
                case Skill.Sword:
                    if(weapon.DefaultCombatStyle == CombatStyle.TwoHanded)
                        return TreasureWeaponType.TwoHandedSword;
                    else if (weapon.W_AttackType.IsMultiStrike())
                        return TreasureWeaponType.SwordMS;
                    else
                        return TreasureWeaponType.Sword;
                case Skill.Axe:
                    if (weapon.DefaultCombatStyle == CombatStyle.TwoHanded)
                        return TreasureWeaponType.TwoHandedAxe;
                    else
                        return TreasureWeaponType.Axe;
                case Skill.Mace:
                    if (weapon.DefaultCombatStyle == CombatStyle.TwoHanded)
                        return TreasureWeaponType.TwoHandedMace;
                    else
                        return TreasureWeaponType.Mace;
                case Skill.Spear:
                    if (weapon.DefaultCombatStyle == CombatStyle.TwoHanded)
                        return TreasureWeaponType.TwoHandedSpear;
                    else
                        return TreasureWeaponType.Spear;
                case Skill.Dagger:
                    if (weapon.W_AttackType.IsMultiStrike())
                        return TreasureWeaponType.DaggerMS;
                    else
                        return TreasureWeaponType.Dagger;
                case Skill.Staff:
                    return TreasureWeaponType.Staff;
                case Skill.Bow:
                    if (weapon.WeenieClassId == (int)Enum.WeenieClassName.bowshort || weapon.WeenieClassId == (int)Enum.WeenieClassName.nayin || weapon.WeenieClassId == (int)Enum.WeenieClassName.shouyumi)
                        return TreasureWeaponType.BowShort; 
                    return TreasureWeaponType.Bow;
                case Skill.Crossbow:
                    if (weapon.WeenieClassId == (int)Enum.WeenieClassName.crossbowlight)
                        return TreasureWeaponType.CrossbowLight;
                    else
                        return TreasureWeaponType.Crossbow;
                case Skill.ThrownWeapon:
                    switch ((Enum.WeenieClassName)weapon.WeenieClassId)
                    {
                        case Enum.WeenieClassName.dart:
                        case Enum.WeenieClassName.dartacid:
                        case Enum.WeenieClassName.dartflame:
                        case Enum.WeenieClassName.dartfrost:
                        case Enum.WeenieClassName.dartelectric:
                        case Enum.WeenieClassName.axethrowing:
                        case Enum.WeenieClassName.axethrowingacid:
                        case Enum.WeenieClassName.axethrowingfire:
                        case Enum.WeenieClassName.axethrowingfrost:
                        case Enum.WeenieClassName.axethrowingelectric:
                        case Enum.WeenieClassName.clubthrowing:
                        case Enum.WeenieClassName.clubthrowingacid:
                        case Enum.WeenieClassName.clubthrowingfire:
                        case Enum.WeenieClassName.clubthrowingfrost:
                        case Enum.WeenieClassName.clubthrowingelectric:
                        case Enum.WeenieClassName.daggerthrowing:
                        case Enum.WeenieClassName.daggerthrowingacid:
                        case Enum.WeenieClassName.daggerthrowingfire:
                        case Enum.WeenieClassName.daggerthrowingfrost:
                        case Enum.WeenieClassName.daggerthrowingelectric:
                        case Enum.WeenieClassName.javelin:
                        case Enum.WeenieClassName.javelinacid:
                        case Enum.WeenieClassName.javelinfire:
                        case Enum.WeenieClassName.javelinfrost:
                        case Enum.WeenieClassName.javelinelectric:
                        case Enum.WeenieClassName.shuriken:
                        case Enum.WeenieClassName.shurikenacid:
                        case Enum.WeenieClassName.shurikenfire:
                        case Enum.WeenieClassName.shurikenfrost:
                        case Enum.WeenieClassName.shurikenelectric:
                        case Enum.WeenieClassName.djarid:
                        case Enum.WeenieClassName.djaridacid:
                        case Enum.WeenieClassName.djaridfire:
                        case Enum.WeenieClassName.djaridfrost:
                        case Enum.WeenieClassName.djaridelectric:
                            return TreasureWeaponType.Thrown;
                        case Enum.WeenieClassName.atlatl:
                            return TreasureWeaponType.AtlatlRegular;
                        default:
                            return TreasureWeaponType.Atlatl;
                    }
                default:
                    return TreasureWeaponType.Undef;
            }
        }

        /// <summary>
        /// Returns TRUE if this roll is for a MeleeWeapon / MissileWeapon / Caster
        /// </summary>
        public bool IsWeapon => WeaponType != TreasureWeaponType.Undef;

        public bool IsMeleeWeapon => WeaponType.IsMeleeWeapon();

        public bool IsMissileWeapon => WeaponType.IsMissileWeapon();

        public bool IsCaster => WeaponType.IsCaster();

        /// <summary>
        /// Returns TRUE if this roll is for a piece of armor
        /// (clothing w/ armor level)
        /// </summary>
        public bool IsArmor => ArmorType != TreasureArmorType.Undef;

        public bool IsClothArmor => ArmorType == TreasureArmorType.Cloth;

        public bool IsClothing => ItemType == TreasureItemType_Orig.Clothing;

        public bool IsCloak => ItemType == TreasureItemType_Orig.Cloak;

        /// <summary>
        /// Returns TRUE if wo has an ArmorLevel > 0
        /// </summary>
        public bool HasArmorLevel(WorldObject wo)
        {
            return (wo.ArmorLevel ?? 0) > 0 || wo.IsClothArmor;
        }

        public bool IsGem => ItemType == TreasureItemType_Orig.Gem;

        public bool IsJewelry => ItemType == TreasureItemType_Orig.Jewelry;

        public bool IsDinnerware => ItemType == TreasureItemType_Orig.ArtObject;

        public bool IsSalvage => ItemType == TreasureItemType_Orig.Salvage;
    }
}
