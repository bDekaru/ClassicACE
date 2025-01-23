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

        public int MinSpellcraft;
        public int MaxSpellcraft;
        public int RolledSpellCraft;

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
