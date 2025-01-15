using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class WeaponWcids
    {
        public static WeenieClassName Roll(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            switch (treasureRoll.WeaponType)
            {
                case TreasureWeaponType.Sword:
                    return RollSwordWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Mace:
                    return RollMaceWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Axe:
                    return RollAxeWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Spear:
                    return RollSpearWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Unarmed:
                    return RollUnarmedWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Staff:
                    return RollStaffWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Dagger:
                    return RollDaggerWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Bow:
                    return RollBowWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Crossbow:
                    return RollCrossbowWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Thrown:
                case TreasureWeaponType.Atlatl:
                    return RollAtlatlWcid(treasureDeath, treasureRoll);

                case TreasureWeaponType.Caster:
                    return RollCaster(treasureDeath);

                case TreasureWeaponType.TwoHandedWeapon:
                    return RollTwoHandedWeaponWcid(ref treasureRoll.WeaponType);
            }
            return WeenieClassName.undef;
        }

        public static TreasureHeritageGroup RollHeritage(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            treasureRoll.Heritage = HeritageChance.Roll(treasureDeath.UnknownChances, treasureRoll);
            return treasureRoll.Heritage;
        }

        public static WeenieClassName RollSwordWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    return SwordWcids_Aluvian.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);

                case TreasureHeritageGroup.Gharundim:
                    return SwordWcids_Gharundim.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);

                case TreasureHeritageGroup.Sho:
                    return SwordWcids_Sho.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollMaceWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            return MaceWcids.Roll(heritage, treasureDeath.Tier, out treasureRoll.WeaponType);
        }

        public static WeenieClassName RollAxeWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            return AxeWcids.Roll(heritage, treasureDeath.Tier, out treasureRoll.WeaponType);
        }

        public static WeenieClassName RollSpearWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            return SpearWcids.Roll(heritage, treasureDeath.Tier, out treasureRoll.WeaponType);
        }

        public static WeenieClassName RollUnarmedWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            return UnarmedWcids.Roll(heritage, treasureDeath.Tier);
        }

        public static WeenieClassName RollStaffWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            return StaffWcids.Roll(heritage, treasureDeath.Tier);
        }

        public static WeenieClassName RollDaggerWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                case TreasureHeritageGroup.Sho:
                    return DaggerWcids_Aluvian_Sho.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);

                case TreasureHeritageGroup.Gharundim:
                    return DaggerWcids_Gharundim.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollBowWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    return BowWcids_Aluvian.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);

                case TreasureHeritageGroup.Gharundim:
                    return BowWcids_Gharundim.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);

                case TreasureHeritageGroup.Sho:
                    return BowWcids_Sho.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollCrossbowWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            return CrossbowWcids.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);
        }

        public static WeenieClassName RollAtlatlWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            return AtlatlWcids.Roll(treasureDeath.Tier, out treasureRoll.WeaponType);
        }

        public static WeenieClassName RollCaster(TreasureDeath treasureDeath)
        {
            return CasterWcids.Roll(treasureDeath.Tier);
        }

        public static WeenieClassName RollTwoHandedWeaponWcid(ref TreasureWeaponType weaponType)
        {
            return TwoHandedWeaponWcids.Roll(out weaponType);
        }
    }
}
