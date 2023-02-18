using System.Collections.Generic;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Server.Factories.Entity;
using ACE.Server.Factories.Enum;

using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;

namespace ACE.Server.Factories.Tables.Wcids
{
    public static class ArmorWcids
    {
        private static ChanceTable<WeenieClassName> LeatherWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.buckler,                 0.07f ),
            ( WeenieClassName.capleather,              0.02f ),
            ( WeenieClassName.cowlleathernew,          0.02f ),
            ( WeenieClassName.basinetleathernew,       0.03f ),
            ( WeenieClassName.bootsleathernew,         0.06f ),
            ( WeenieClassName.bracersleathernew,       0.06f ),
            ( WeenieClassName.breastplateleathernew,   0.06f ),
            ( WeenieClassName.coatleathernew,          0.03f ),
            ( WeenieClassName.cuirassleathernew,       0.06f ),
            ( WeenieClassName.gauntletsleathernew,     0.06f ),
            ( WeenieClassName.girthleathernew,         0.06f ),
            ( WeenieClassName.greavesleathernew,       0.05f ),
            ( WeenieClassName.leggingsleathernew,      0.05f ),
            ( WeenieClassName.longgauntletsleathernew, 0.06f ),
            ( WeenieClassName.pantsleathernew,         0.05f ),
            ( WeenieClassName.pauldronsleathernew,     0.06f ),
            ( WeenieClassName.shirtleathernew,         0.04f ),
            ( WeenieClassName.shortsleathernew,        0.05f ),
            ( WeenieClassName.sleevesleathernew,       0.06f ),
            ( WeenieClassName.tassetsleathernew,       0.05f ),
        };

        private static ChanceTable<WeenieClassName> StuddedLeatherWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldkite,                0.04f ),
            ( WeenieClassName.shieldround,               0.04f ),
            ( WeenieClassName.cowlstuddedleather,        0.04f ),
            ( WeenieClassName.basinetstuddedleather,     0.04f ),
            ( WeenieClassName.bootsreinforcedleather,    0.08f ),
            ( WeenieClassName.bracersstuddedleather,     0.07f ),
            ( WeenieClassName.breastplatestuddedleather, 0.07f ),
            ( WeenieClassName.coatstuddedleather,        0.03f ),
            ( WeenieClassName.cuirassstuddedleather,     0.06f ),
            ( WeenieClassName.gauntletsstuddedleather,   0.08f ),
            ( WeenieClassName.girthstuddedleather,       0.07f ),
            ( WeenieClassName.greavesstuddedleather,     0.07f ),
            ( WeenieClassName.leggingsstuddedleather,    0.07f ),
            ( WeenieClassName.pauldronsstuddedleather,   0.07f ),
            ( WeenieClassName.shirtstuddedleather,       0.04f ),
            ( WeenieClassName.sleevesstuddedleather,     0.06f ),
            ( WeenieClassName.tassetsstuddedleather,     0.07f ),
        };

        private static ChanceTable<WeenieClassName> ChainmailWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldkitelarge,      0.04f ),
            ( WeenieClassName.shieldroundlarge,     0.04f ),
            ( WeenieClassName.capmetal,             0.02f ),
            ( WeenieClassName.mailcoif,             0.02f ),
            ( WeenieClassName.coifscale,            0.02f ),
            ( WeenieClassName.basinetchainmail,     0.02f ),
            ( WeenieClassName.bootssteeltoe,        0.08f ),
            ( WeenieClassName.bracerschainmail,     0.07f ),
            ( WeenieClassName.breastplatechainmail, 0.07f ),
            ( WeenieClassName.hauberkchainmail,     0.05f ),
            ( WeenieClassName.gauntletschainmail,   0.08f ),
            ( WeenieClassName.girthchainmail,       0.07f ),
            ( WeenieClassName.greaveschainmail,     0.07f ),
            ( WeenieClassName.leggingschainmail,    0.08f ),
            ( WeenieClassName.pauldronschainmail,   0.08f ),
            ( WeenieClassName.shirtchainmail,       0.05f ),
            ( WeenieClassName.sleeveschainmail,     0.06f ),
            ( WeenieClassName.tassetschainmail,     0.08f ),
        };

        // platemail - aluvian
        private static ChanceTable<WeenieClassName> PlatemailWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldtower,          0.08f ),
            ( WeenieClassName.helmhorned,           0.02f ),
            ( WeenieClassName.helmet,               0.02f ),
            ( WeenieClassName.armet,                0.02f ),
            ( WeenieClassName.heaumenew,            0.02f ),
            ( WeenieClassName.sollerets,            0.08f ),
            ( WeenieClassName.vambracesplatemail,   0.06f ),
            ( WeenieClassName.breastplateplatemail, 0.08f ),
            ( WeenieClassName.cuirassplatemail,     0.08f ),
            ( WeenieClassName.gauntletsplatemail,   0.08f ),
            ( WeenieClassName.girthplatemail,       0.05f ),
            ( WeenieClassName.greavesplatemail,     0.07f ),
            ( WeenieClassName.tassetsplatemail,     0.07f ),
            ( WeenieClassName.hauberkplatemail,     0.06f ),
            ( WeenieClassName.leggingsplatemail,    0.08f ),
            ( WeenieClassName.pauldronsplatemail,   0.05f ),
            ( WeenieClassName.sleevesplatemail,     0.08f ),
        };

        // platemail - gharu'ndim
        private static ChanceTable<WeenieClassName> ScalemailWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldtower,          0.07f ),
            ( WeenieClassName.baigha,               0.02f ),
            ( WeenieClassName.helmet,               0.02f ),
            ( WeenieClassName.armet,                0.02f ),
            ( WeenieClassName.heaumenew,            0.02f ),
            ( WeenieClassName.basinetscalemail,     0.01f ),
            ( WeenieClassName.sollerets,            0.07f ),
            ( WeenieClassName.bracersscalemail,     0.07f ),
            ( WeenieClassName.breastplatescalemail, 0.06f ),
            ( WeenieClassName.cuirassscalemail,     0.06f ),
            ( WeenieClassName.gauntletsscalemail,   0.07f ),
            ( WeenieClassName.girthscalemail,       0.06f ),
            ( WeenieClassName.greavesscalemail,     0.07f ),
            ( WeenieClassName.tassetsscalemail,     0.07f ),
            ( WeenieClassName.hauberkscalemail,     0.06f ),
            ( WeenieClassName.leggingsscalemail,    0.07f ),
            ( WeenieClassName.pauldronsscalemail,   0.06f ),
            ( WeenieClassName.sleevesscalemail,     0.06f ),
            ( WeenieClassName.shirtscalemail,       0.06f ),
        };

        // platemail - sho
        private static ChanceTable<WeenieClassName> YoroiWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldtower,        0.08f ),
            ( WeenieClassName.kabuton,            0.02f ),
            ( WeenieClassName.helmet,             0.02f ),
            ( WeenieClassName.armet,              0.02f ),
            ( WeenieClassName.heaumenew,          0.02f ),
            ( WeenieClassName.sollerets,          0.08f ),
            ( WeenieClassName.kote,               0.08f ),
            ( WeenieClassName.breastplateyoroi,   0.08f ),
            ( WeenieClassName.cuirassyoroi,       0.08f ),
            ( WeenieClassName.gauntletsplatemail, 0.06f ),
            ( WeenieClassName.girthyoroi,         0.08f ),
            ( WeenieClassName.greavesyoroi,       0.08f ),
            ( WeenieClassName.tassetsyoroi,       0.08f ),
            ( WeenieClassName.leggingsyoroi,      0.07f ),
            ( WeenieClassName.pauldronsyoroi,     0.08f ),
            ( WeenieClassName.sleevesyoroi,       0.07f ),
        };

        // heritage low - aluvian
        private static ChanceTable<WeenieClassName> CeldonWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.girthceldon,       0.25f ),
            ( WeenieClassName.breastplateceldon, 0.25f ),
            ( WeenieClassName.leggingsceldon,    0.25f ),
            ( WeenieClassName.sleevesceldon,     0.25f ),
        };

        // heritage low - gharu'ndim
        private static ChanceTable<WeenieClassName> AmuliWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.coatamullian,     0.50f ),
            ( WeenieClassName.leggingsamullian, 0.50f ),
        };

        // heritage low - sho
        private static ChanceTable<WeenieClassName> KoujiaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.breastplatekoujia, 0.33f ),
            ( WeenieClassName.leggingskoujia,    0.34f ),
            ( WeenieClassName.sleeveskoujia,     0.33f ),
        };

        private static ChanceTable<WeenieClassName> CovenantWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldcovenant,      0.10f ),
            ( WeenieClassName.helmcovenant,        0.10f ),
            ( WeenieClassName.gauntletscovenant,   0.10f ),
            ( WeenieClassName.bracerscovenant,     0.10f ),
            ( WeenieClassName.pauldronscovenant,   0.10f ),
            ( WeenieClassName.breastplatecovenant, 0.10f ),
            ( WeenieClassName.girthcovenant,       0.10f ),
            ( WeenieClassName.tassetscovenant,     0.10f ),
            ( WeenieClassName.greavescovenant,     0.10f ),
            ( WeenieClassName.bootscovenant,       0.10f ),
        };

        // heritage high - aluvian
        private static ChanceTable<WeenieClassName> LoricaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bootslorica,       0.16f ),
            ( WeenieClassName.gauntletslorica,   0.16f ),
            ( WeenieClassName.helmlorica,        0.17f ),
            ( WeenieClassName.breastplatelorica, 0.17f ),
            ( WeenieClassName.leggingslorica,    0.17f ),
            ( WeenieClassName.sleeveslorica,     0.17f ),
        };

        // heritage high - gharu'ndim
        private static ChanceTable<WeenieClassName> NariyidWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bootsnariyid,       0.14f ),
            ( WeenieClassName.gauntletsnariyid,   0.14f ),
            ( WeenieClassName.helmnariyid,        0.14f ),
            ( WeenieClassName.breastplatenariyid, 0.14f ),
            ( WeenieClassName.girthnariyid,       0.14f ),
            ( WeenieClassName.leggingsnariyid,    0.15f ),
            ( WeenieClassName.sleevesnariyid,     0.15f ),
        };

        // heritage high - sho
        private static ChanceTable<WeenieClassName> ChiranWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.sandalschiran,   0.20f ),
            ( WeenieClassName.gauntletschiran, 0.20f ),
            ( WeenieClassName.helmchiran,      0.20f ),
            ( WeenieClassName.coatchiran,      0.20f ),
            ( WeenieClassName.leggingschiran,  0.20f ),
        };

        // ToD+

        // viamontian platemail
        // introduced 07-2005 - throne of destiny
        // equivalent to platemail / scalemail / yoroi
        private static ChanceTable<WeenieClassName> DiforsaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.shieldtower,        0.08f ),
            ( WeenieClassName.helmdiforsa,        0.02f ),
            ( WeenieClassName.helmet,             0.02f ),
            ( WeenieClassName.armet,              0.02f ),
            ( WeenieClassName.heaumenew,          0.02f ),
            ( WeenieClassName.solleretsdiforsa,   0.04f ),
            ( WeenieClassName.sollerets,          0.04f ),
            ( WeenieClassName.bracersdiforsa,     0.06f ),
            ( WeenieClassName.breastplatediforsa, 0.08f ),
            ( WeenieClassName.cuirassdiforsa,     0.08f ),
            ( WeenieClassName.gauntletsdiforsa,   0.08f ),
            ( WeenieClassName.girthdiforsa,       0.05f ),
            ( WeenieClassName.greavesdiforsa,     0.07f ),
            ( WeenieClassName.tassetsdiforsa,     0.07f ),
            ( WeenieClassName.hauberkdiforsa,     0.06f ),
            ( WeenieClassName.leggingsdiforsa,    0.08f ),
            ( WeenieClassName.pauldronsdiforsa,   0.05f ),
            ( WeenieClassName.sleevesdiforsa,     0.08f ),
        };

        // viamontian heritage low
        // introduced 07-2005 - throne of destiny
        // equivalent to celdon / amuli / koujia
        private static ChanceTable<WeenieClassName> TenassaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.breastplatetenassa, 0.33f ),
            ( WeenieClassName.leggingstenassa,    0.34f ),
            ( WeenieClassName.sleevestenassa,     0.33f ),
        };

        // viamontian heritage high
        // introduced 07-2005 - throne of destiny
        // equivalent to lorica / nariyid / chiran
        private static ChanceTable<WeenieClassName> AlduressaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.bootsalduressa,     0.20f ),
            ( WeenieClassName.gauntletsalduressa, 0.20f ),
            ( WeenieClassName.helmalduressa,      0.20f ),
            ( WeenieClassName.coatalduressa,      0.20f ),
            ( WeenieClassName.leggingsalduressa,  0.20f ),
        };

        // olthoi armor, t7+
        // introduced 08-2008 - ancient powers
        private static ChanceTable<WeenieClassName> OlthoiWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace37191_olthoigauntlets,   0.10f ),
            ( WeenieClassName.ace37193_olthoigirth,       0.10f ),
            ( WeenieClassName.ace37194_olthoigreaves,     0.10f ),
            ( WeenieClassName.ace37199_olthoihelm,        0.10f ),
            ( WeenieClassName.ace37204_olthoipauldrons,   0.10f ),
            ( WeenieClassName.ace37211_olthoisollerets,   0.10f ),
            ( WeenieClassName.ace37212_olthoitassets,     0.10f ),
            ( WeenieClassName.ace37213_olthoibracers,     0.10f ),
            ( WeenieClassName.ace37216_olthoibreastplate, 0.10f ),
            ( WeenieClassName.ace37291_olthoishield,      0.10f ),
        };

        // olthoi heritage armor, t7+
        // introduced 08-2008 - ancient powers
        private static ChanceTable<WeenieClassName> OlthoiCeldonWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace37189_olthoiceldongauntlets,   0.14f ),
            ( WeenieClassName.ace37192_olthoiceldongirth,       0.14f ),
            ( WeenieClassName.ace37197_olthoiceldonhelm,        0.14f ),
            ( WeenieClassName.ace37202_olthoiceldonleggings,    0.15f ),
            ( WeenieClassName.ace37205_olthoiceldonsleeves,     0.15f ),
            ( WeenieClassName.ace37209_olthoiceldonsollerets,   0.14f ),
            ( WeenieClassName.ace37214_olthoiceldonbreastplate, 0.14f ),
        };

        private static ChanceTable<WeenieClassName> OlthoiAmuliWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace37188_olthoiamuligauntlets, 0.20f ),
            ( WeenieClassName.ace37196_olthoiamulihelm,      0.20f ),
            ( WeenieClassName.ace37201_olthoiamulileggings,  0.20f ),
            ( WeenieClassName.ace37208_olthoiamulisollerets, 0.20f ),
            ( WeenieClassName.ace37299_olthoiamulicoat,      0.20f ),
        };

        private static ChanceTable<WeenieClassName> OlthoiKoujiaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace37190_olthoikoujiagauntlets,   0.20f ),
            ( WeenieClassName.ace37198_olthoikoujiakabuton,     0.20f ),
            ( WeenieClassName.ace37203_olthoikoujialeggings,    0.20f ),
            ( WeenieClassName.ace37206_olthoikoujiasleeves,     0.20f ),
            ( WeenieClassName.ace37215_olthoikoujiabreastplate, 0.20f ),
        };

        private static ChanceTable<WeenieClassName> OlthoiAlduressaWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace37187_olthoialduressagauntlets, 0.20f ),
            ( WeenieClassName.ace37195_olthoialduressahelm,      0.20f ),
            ( WeenieClassName.ace37200_olthoialduressaleggings,  0.20f ),
            ( WeenieClassName.ace37207_olthoialduressaboots,     0.20f ),
            ( WeenieClassName.ace37217_olthoialduressacoat,      0.20f ),
        };

        // society armor
        // introduced: 08-2008 - ancient powers
        private static ChanceTable<WeenieClassName> CelestialHandWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace38463_celestialhandbreastplate, 0.34f ),
            ( WeenieClassName.ace38464_celestialhandgauntlets,   0.33f ),
            ( WeenieClassName.ace38465_celestialhandgirth,       0.33f ),
        };

        private static ChanceTable<WeenieClassName> EldrytchWebWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace38472_eldrytchwebbreastplate, 0.34f ),
            ( WeenieClassName.ace38473_eldrytchwebgauntlets,   0.33f ),
            ( WeenieClassName.ace38474_eldrytchwebgirth,       0.33f ),
        };

        private static ChanceTable<WeenieClassName> RadiantBloodWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace38481_radiantbloodbreastplate, 0.34f ),
            ( WeenieClassName.ace38482_radiantbloodgauntlets,   0.33f ),
            ( WeenieClassName.ace38483_radiantbloodgirth,       0.33f ),
        };

        // empyrean, tier 6+
        // introduced 05-2010 - celebration
        private static ChanceTable<WeenieClassName> HaebreanWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace42749_haebreanbreastplate, 0.11f ),
            ( WeenieClassName.ace42750_haebreangauntlets,   0.11f ),
            ( WeenieClassName.ace42751_haebreangirth,       0.11f ),
            ( WeenieClassName.ace42752_haebreangreaves,     0.11f ),
            ( WeenieClassName.ace42753_haebreanhelm,        0.12f ),
            ( WeenieClassName.ace42754_haebreanpauldrons,   0.11f ),
            ( WeenieClassName.ace42755_haebreanboots,       0.11f ),
            ( WeenieClassName.ace42756_haebreantassets,     0.11f ),
            ( WeenieClassName.ace42757_haebreanvambraces,   0.11f ),
        };

        // empyrean, tier 6+
        // introduced 07-2010 - filling in the blanks
        private static ChanceTable<WeenieClassName> KnorrAcademyWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace43048_knorracademybreastplate, 0.12f ),
            ( WeenieClassName.ace43049_knorracademygauntlets,   0.11f ),
            ( WeenieClassName.ace43050_knorracademygirth,       0.11f ),
            ( WeenieClassName.ace43051_knorracademygreaves,     0.11f ),
            ( WeenieClassName.ace43052_knorracademypauldrons,   0.11f ),
            ( WeenieClassName.ace43053_knorracademyboots,       0.11f ),
            ( WeenieClassName.ace43054_knorracademytassets,     0.11f ),
            ( WeenieClassName.ace43055_knorracademyvambraces,   0.11f ),
            ( WeenieClassName.ace43068_knorracademyhelm,        0.11f ),
        };

        // tier 6+ leather
        // introduced: 03-2011 - hidden in shadows
        private static ChanceTable<WeenieClassName> SedgemailLeatherWcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace43828_sedgemailleathervest,      0.17f ),
            ( WeenieClassName.ace43829_sedgemailleathercowl,      0.17f ),
            ( WeenieClassName.ace43830_sedgemailleathergauntlets, 0.16f ),
            ( WeenieClassName.ace43831_sedgemailleatherpants,     0.17f ),
            ( WeenieClassName.ace43832_sedgemailleathershoes,     0.16f ),
            ( WeenieClassName.ace43833_sedgemailleathersleeves,   0.17f ),
        };

        // over-robes
        // introduced: 10-2011 - cloak of darkness
        private static ChanceTable<WeenieClassName> OverRobe_T3_T5_Wcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace44799_faranoverrobe,      0.25f ),     // aluvian? t3+
            ( WeenieClassName.ace44800_dhovestandoverrobe, 0.25f ),     // gharu'ndim? t3+
            ( WeenieClassName.ace44801_suikanoverrobe,     0.25f ),     // sho? t3+
            ( WeenieClassName.ace44802_vestirioverrobe,    0.25f ),     // viamontian? t3+
        };

        private static ChanceTable<WeenieClassName> OverRobe_T6_T8_Wcids = new ChanceTable<WeenieClassName>()
        {
            ( WeenieClassName.ace44799_faranoverrobe,      0.20f ),     // aluvian? t3+
            ( WeenieClassName.ace44800_dhovestandoverrobe, 0.20f ),     // gharu'ndim? t3+
            ( WeenieClassName.ace44801_suikanoverrobe,     0.20f ),     // sho? t3+
            ( WeenieClassName.ace44802_vestirioverrobe,    0.20f ),     // viamontian? t3+
            ( WeenieClassName.ace44803_empyreanoverrobe,   0.20f ),     // empyrean? t6+?
        };

        private static ChanceTable<WeenieClassName> ClothAluvianWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ( WeenieClassName.robealuvianhood,      1.0f ),
            ( WeenieClassName.robealuviannohood,    1.0f ),
        };

        private static ChanceTable<WeenieClassName> ClothGharuWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ( WeenieClassName.robegharundimhood,    1.0f ),
            ( WeenieClassName.robegharundimnohood,  1.0f ),
        };

        private static ChanceTable<WeenieClassName> ClothShoWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
        {
            ( WeenieClassName.robeshohood,      1.0f ),
            ( WeenieClassName.robeshonohood,    1.0f ),
        };

        public static WeenieClassName Roll(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            switch (treasureRoll.ArmorType)
            {
                case TreasureArmorType.Leather:
                    return LeatherWcids.Roll();

                case TreasureArmorType.StuddedLeather:
                    return StuddedLeatherWcids.Roll();

                case TreasureArmorType.Chainmail:
                    return ChainmailWcids.Roll();

                case TreasureArmorType.Platemail:
                    return RollPlatemailWcid(treasureDeath, treasureRoll);

                case TreasureArmorType.HeritageLow:
                    return RollHeritageLowWcid(treasureDeath, treasureRoll);

                case TreasureArmorType.Covenant:
                    return CovenantWcids.Roll();

                case TreasureArmorType.HeritageHigh:
                    return RollHeritageHighWcid(treasureDeath, treasureRoll);

                case TreasureArmorType.Olthoi:
                    return OlthoiWcids.Roll();

                case TreasureArmorType.OlthoiHeritage:
                    return RollOlthoiHeritageWcid(treasureDeath, treasureRoll);

                case TreasureArmorType.Society:
                    return RollSocietyArmor(ref treasureRoll.ArmorType);

                case TreasureArmorType.Haebrean:
                    return HaebreanWcids.Roll();

                case TreasureArmorType.KnorrAcademy:
                    return KnorrAcademyWcids.Roll();

                case TreasureArmorType.Sedgemail:
                    return SedgemailLeatherWcids.Roll();

                case TreasureArmorType.Overrobe:
                    return RollOverRobeWcid(treasureDeath);

                case TreasureArmorType.Cloth:
                    return RollClothWcid(treasureDeath, treasureRoll);
            }
            return WeenieClassName.undef;
        }

        public static TreasureHeritageGroup RollHeritage(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            return HeritageChance.Roll(treasureDeath.UnknownChances, treasureRoll, true);
        }

        public static WeenieClassName RollPlatemailWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    treasureRoll.ArmorType = TreasureArmorType.Platemail;
                    return PlatemailWcids.Roll();

                case TreasureHeritageGroup.Gharundim:
                    treasureRoll.ArmorType = TreasureArmorType.Scalemail;
                    return ScalemailWcids.Roll();

                case TreasureHeritageGroup.Sho:
                    treasureRoll.ArmorType = TreasureArmorType.Yoroi;
                    return YoroiWcids.Roll();

                case TreasureHeritageGroup.Viamontian:
                    treasureRoll.ArmorType = TreasureArmorType.Diforsa;
                    return DiforsaWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollHeritageLowWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    treasureRoll.ArmorType = TreasureArmorType.Celdon;
                    return CeldonWcids.Roll();

                case TreasureHeritageGroup.Gharundim:
                    treasureRoll.ArmorType = TreasureArmorType.Amuli;
                    return AmuliWcids.Roll();

                case TreasureHeritageGroup.Sho:
                    treasureRoll.ArmorType = TreasureArmorType.Koujia;
                    return KoujiaWcids.Roll();

                case TreasureHeritageGroup.Viamontian:
                    treasureRoll.ArmorType = TreasureArmorType.Tenassa;
                    return TenassaWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollHeritageHighWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    treasureRoll.ArmorType = TreasureArmorType.Lorica;
                    return LoricaWcids.Roll();

                case TreasureHeritageGroup.Gharundim:
                    treasureRoll.ArmorType = TreasureArmorType.Nariyid;
                    return NariyidWcids.Roll();

                case TreasureHeritageGroup.Sho:
                    treasureRoll.ArmorType = TreasureArmorType.Chiran;
                    return ChiranWcids.Roll();

                case TreasureHeritageGroup.Viamontian:
                    treasureRoll.ArmorType = TreasureArmorType.Alduressa;
                    return AlduressaWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollOlthoiHeritageWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    treasureRoll.ArmorType = TreasureArmorType.OlthoiCeldon;
                    return OlthoiCeldonWcids.Roll();

                case TreasureHeritageGroup.Gharundim:
                    treasureRoll.ArmorType = TreasureArmorType.OlthoiAmuli;
                    return OlthoiAmuliWcids.Roll();

                case TreasureHeritageGroup.Sho:
                    treasureRoll.ArmorType = TreasureArmorType.OlthoiKoujia;
                    return OlthoiKoujiaWcids.Roll();

                case TreasureHeritageGroup.Viamontian:
                    treasureRoll.ArmorType = TreasureArmorType.OlthoiAlduressa;
                    return OlthoiAlduressaWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollSocietyArmor(ref TreasureArmorType armorType)
        {
            // no heritage, even chance?
            var rng = ThreadSafeRandom.Next(1, 3);

            switch (rng)
            {
                case 1:
                    armorType = TreasureArmorType.CelestialHand;
                    return CelestialHandWcids.Roll();
                case 2:
                    armorType = TreasureArmorType.EldrytchWeb;
                    return EldrytchWebWcids.Roll();
                case 3:
                    armorType = TreasureArmorType.RadiantBlood;
                    return RadiantBloodWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        public static WeenieClassName RollOverRobeWcid(TreasureDeath treasureDeath)
        {
            if (treasureDeath.Tier < 6)
                return OverRobe_T3_T5_Wcids.Roll();
            else
                return OverRobe_T6_T8_Wcids.Roll();
        }

        public static WeenieClassName RollClothWcid(TreasureDeath treasureDeath, TreasureRoll treasureRoll)
        {
            var heritage = RollHeritage(treasureDeath, treasureRoll);

            switch (heritage)
            {
                case TreasureHeritageGroup.Aluvian:
                    return ClothAluvianWcids.Roll();

                case TreasureHeritageGroup.Gharundim:
                    return ClothGharuWcids.Roll();

                case TreasureHeritageGroup.Sho:
                    return ClothShoWcids.Roll();
            }
            return WeenieClassName.undef;
        }

        private static readonly Dictionary<WeenieClassName, TreasureArmorType> _combined = new Dictionary<WeenieClassName, TreasureArmorType>();

        static ArmorWcids()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                LeatherWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.buckler,                 1.0f ),

                    ( WeenieClassName.capleather,              1.0f ),
                    ( WeenieClassName.cowlleathernew,          1.0f ),
                    ( WeenieClassName.basinetleathernew,       1.0f ),

                    ( WeenieClassName.gauntletsleathernew,     2.0f ),
                    ( WeenieClassName.longgauntletsleathernew, 2.0f ),
                    ( WeenieClassName.bootsleathernew,         2.0f ),

                    ( WeenieClassName.coatleathernew,          1.0f ),
                    ( WeenieClassName.cuirassleathernew,       1.0f ),
                    ( WeenieClassName.shirtleathernew,         1.0f ),
                    ( WeenieClassName.breastplateleathernew,   1.0f ),

                    ( WeenieClassName.girthleathernew,         1.0f ),
                    ( WeenieClassName.shortsleathernew,        1.0f ),

                    ( WeenieClassName.pantsleathernew,         1.0f ),
                    ( WeenieClassName.leggingsleathernew,      1.0f ),
                    ( WeenieClassName.tassetsleathernew,       1.0f ),
                    ( WeenieClassName.greavesleathernew,       1.0f ),

                    ( WeenieClassName.sleevesleathernew,       1.0f ),
                    ( WeenieClassName.pauldronsleathernew,     1.0f ),
                    ( WeenieClassName.bracersleathernew,       1.0f ),
                };

                StuddedLeatherWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldkite,                1.0f ),
                    ( WeenieClassName.shieldround,               1.0f ),

                    ( WeenieClassName.cowlstuddedleather,        1.0f ),
                    ( WeenieClassName.basinetstuddedleather,     1.0f ),

                    ( WeenieClassName.gauntletsstuddedleather,   2.0f ),
                    ( WeenieClassName.bootsreinforcedleather,    2.0f ),

                    ( WeenieClassName.coatstuddedleather,        1.0f ),
                    ( WeenieClassName.shirtstuddedleather,       1.0f ),
                    ( WeenieClassName.cuirassstuddedleather,     1.0f ),
                    ( WeenieClassName.breastplatestuddedleather, 1.0f ),

                    ( WeenieClassName.girthstuddedleather,       1.0f ),

                    ( WeenieClassName.leggingsstuddedleather,    1.0f ),
                    ( WeenieClassName.tassetsstuddedleather,     1.0f ),
                    ( WeenieClassName.greavesstuddedleather,     1.0f ),

                    ( WeenieClassName.sleevesstuddedleather,     1.0f ),
                    ( WeenieClassName.pauldronsstuddedleather,   1.0f ),
                    ( WeenieClassName.bracersstuddedleather,     1.0f ),
                };

                ChainmailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldkitelarge,      1.0f ),
                    ( WeenieClassName.shieldroundlarge,     1.0f ),

                    ( WeenieClassName.capmetal,             1.0f ),
                    ( WeenieClassName.mailcoif,             1.0f ),
                    ( WeenieClassName.basinetchainmail,     1.0f ),

                    ( WeenieClassName.gauntletschainmail,   2.0f ),
                    ( WeenieClassName.bootssteeltoe,        2.0f ),

                    ( WeenieClassName.shirtchainmail,       1.0f ),
                    ( WeenieClassName.hauberkchainmail,     1.0f ),
                    ( WeenieClassName.breastplatechainmail, 1.0f ),

                    ( WeenieClassName.girthchainmail,       1.0f ),

                    ( WeenieClassName.leggingschainmail,    1.0f ),
                    ( WeenieClassName.tassetschainmail,     1.0f ),
                    ( WeenieClassName.greaveschainmail,     1.0f ),

                    ( WeenieClassName.sleeveschainmail,     1.0f ),
                    ( WeenieClassName.pauldronschainmail,   1.0f ),
                    ( WeenieClassName.bracerschainmail,     1.0f ),
                };

                // platemail - aluvian
                PlatemailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldtower,          1.0f ),

                    ( WeenieClassName.helmhorned,           1.0f ),
                    ( WeenieClassName.helmet,               1.0f ),
                    ( WeenieClassName.armet,                1.0f ),
                    ( WeenieClassName.heaumenew,            1.0f ),

                    ( WeenieClassName.gauntletsplatemail,   2.0f ),
                    ( WeenieClassName.sollerets,            2.0f ),

                    ( WeenieClassName.hauberkplatemail,     1.0f ),
                    ( WeenieClassName.cuirassplatemail,     1.0f ),
                    ( WeenieClassName.breastplateplatemail, 1.0f ),

                    ( WeenieClassName.girthplatemail,       1.0f ),

                    ( WeenieClassName.leggingsplatemail,    1.0f ),
                    ( WeenieClassName.tassetsplatemail,     1.0f ),
                    ( WeenieClassName.greavesplatemail,     1.0f ),

                    ( WeenieClassName.sleevesplatemail,     1.0f ),
                    ( WeenieClassName.pauldronsplatemail,   1.0f ),
                    ( WeenieClassName.vambracesplatemail,   1.0f ),
                };

                // platemail - gharu'ndim
                ScalemailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldtower,          1.0f ),

                    ( WeenieClassName.baigha,               1.0f ),
                    ( WeenieClassName.helmet,               1.0f ),
                    ( WeenieClassName.armet,                1.0f ),
                    ( WeenieClassName.heaumenew,            1.0f ),
                    ( WeenieClassName.basinetscalemail,     1.0f ),
                    ( WeenieClassName.coifscale,            1.0f ),

                    ( WeenieClassName.gauntletsscalemail,   2.0f ),
                    ( WeenieClassName.sollerets,            2.0f ),

                    ( WeenieClassName.breastplatescalemail, 1.0f ),
                    ( WeenieClassName.cuirassscalemail,     1.0f ),
                    ( WeenieClassName.hauberkscalemail,     1.0f ),
                    ( WeenieClassName.shirtscalemail,       1.0f ),

                    ( WeenieClassName.girthscalemail,       1.0f ),

                    ( WeenieClassName.leggingsscalemail,    1.0f ),
                    ( WeenieClassName.tassetsscalemail,     1.0f ),
                    ( WeenieClassName.greavesscalemail,     1.0f ),

                    ( WeenieClassName.sleevesscalemail,     1.0f ),
                    ( WeenieClassName.pauldronsscalemail,   1.0f ),
                    ( WeenieClassName.bracersscalemail,     1.0f ),
                };

                // platemail - sho
                YoroiWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldtower,        1.0f ),

                    ( WeenieClassName.kabuton,            1.0f ),
                    ( WeenieClassName.helmet,             1.0f ),
                    ( WeenieClassName.armet,              1.0f ),
                    ( WeenieClassName.heaumenew,          1.0f ),

                    ( WeenieClassName.gauntletsplatemail, 2.0f ),
                    ( WeenieClassName.sollerets,          2.0f ),

                    ( WeenieClassName.breastplateyoroi,   1.0f ),
                    ( WeenieClassName.cuirassyoroi,       1.0f ),
                    ( WeenieClassName.girthyoroi,         1.0f ),

                    ( WeenieClassName.leggingsyoroi,      1.0f ),
                    ( WeenieClassName.tassetsyoroi,       1.0f ),
                    ( WeenieClassName.greavesyoroi,       1.0f ),

                    ( WeenieClassName.sleevesyoroi,       1.0f ),
                    ( WeenieClassName.pauldronsyoroi,     1.0f ),
                    ( WeenieClassName.kote,               1.0f ),
                };

                // heritage low - aluvian
                CeldonWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.girthceldon,       1.0f ),
                    ( WeenieClassName.breastplateceldon, 1.0f ),
                    ( WeenieClassName.leggingsceldon,    1.0f ),
                    ( WeenieClassName.sleevesceldon,     1.0f ),
                };

                // heritage low - gharu'ndim
                AmuliWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.coatamullian,     1.0f ),
                    ( WeenieClassName.leggingsamullian, 1.0f ),
                };

                // heritage low - sho
                KoujiaWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.breastplatekoujia, 1.0f ),
                    ( WeenieClassName.leggingskoujia,    1.0f ),
                    ( WeenieClassName.sleeveskoujia,     1.0f ),
                };

                CovenantWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldcovenant,      1.0f ),

                    ( WeenieClassName.helmcovenant,        1.0f ),

                    ( WeenieClassName.gauntletscovenant,   2.0f ),
                    ( WeenieClassName.bootscovenant,       2.0f ),

                    ( WeenieClassName.breastplatecovenant, 1.0f ),

                    ( WeenieClassName.girthcovenant,       1.0f ),

                    ( WeenieClassName.tassetscovenant,     1.0f ),
                    ( WeenieClassName.greavescovenant,     1.0f ),

                    ( WeenieClassName.pauldronscovenant,   1.0f ),
                    ( WeenieClassName.bracerscovenant,     1.0f ),
                };

                // heritage high - aluvian
                LoricaWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmlorica,        1.0f ),

                    ( WeenieClassName.bootslorica,       2.0f ),
                    ( WeenieClassName.gauntletslorica,   2.0f ),

                    ( WeenieClassName.breastplatelorica, 1.0f ),

                    ( WeenieClassName.leggingslorica,    1.0f ),

                    ( WeenieClassName.sleeveslorica,     1.0f ),
                };

                // heritage high - gharu'ndim
                NariyidWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmnariyid,        1.0f ),

                    ( WeenieClassName.bootsnariyid,       2.0f ),
                    ( WeenieClassName.gauntletsnariyid,   2.0f ),

                    ( WeenieClassName.breastplatenariyid, 1.0f ),

                    ( WeenieClassName.girthnariyid,       1.0f ),

                    ( WeenieClassName.leggingsnariyid,    1.0f ),

                    ( WeenieClassName.sleevesnariyid,     1.0f ),
                };

                // heritage high - sho
                ChiranWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmchiran,      1.0f ),

                    ( WeenieClassName.sandalschiran,   2.0f ),
                    ( WeenieClassName.gauntletschiran, 2.0f ),

                    ( WeenieClassName.coatchiran,      1.0f ),

                    ( WeenieClassName.leggingschiran,  1.0f ),
                };
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                LeatherWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.buckler,                 2.0f ),

                    ( WeenieClassName.capleather,              1.0f ),

                    ( WeenieClassName.cowlleather,             1.0f ),
                    ( WeenieClassName.basinetleather,          1.0f ),

                    ( WeenieClassName.gauntletsleather,        2.0f ),
                    ( WeenieClassName.bootsleather,            2.0f ),

                    ( WeenieClassName.coatleather,             1.0f ),
                    ( WeenieClassName.cuirassleather,          1.0f ),
                    ( WeenieClassName.shirtleather,            1.0f ),
                    ( WeenieClassName.breastplateleather,      1.0f ),

                    ( WeenieClassName.girthleather,            1.0f ),

                    ( WeenieClassName.leggingsleather,         1.0f ),
                    ( WeenieClassName.tassetsleather,          1.0f ),
                    ( WeenieClassName.greavesleather,          1.0f ),

                    ( WeenieClassName.sleevesleather,          1.0f ),
                    ( WeenieClassName.pauldronsleather,        1.0f ),
                    ( WeenieClassName.bracersleather,          1.0f ),

                    ( WeenieClassName.cowlleathernew,          1.0f ),
                    ( WeenieClassName.basinetleathernew,       1.0f ),

                    ( WeenieClassName.gauntletsleathernew,     2.0f ),
                    ( WeenieClassName.longgauntletsleathernew, 2.0f ),
                    ( WeenieClassName.bootsleathernew,         2.0f ),

                    ( WeenieClassName.coatleathernew,          1.0f ),
                    ( WeenieClassName.cuirassleathernew,       1.0f ),
                    ( WeenieClassName.shirtleathernew,         1.0f ),
                    ( WeenieClassName.breastplateleathernew,   1.0f ),
                    
                    ( WeenieClassName.girthleathernew,         1.0f ),
                    ( WeenieClassName.shortsleathernew,        1.0f ),

                    ( WeenieClassName.pantsleathernew,         1.0f ),
                    ( WeenieClassName.leggingsleathernew,      1.0f ),
                    ( WeenieClassName.tassetsleathernew,       1.0f ),
                    ( WeenieClassName.greavesleathernew,       1.0f ),

                    ( WeenieClassName.sleevesleathernew,       1.0f ),
                    ( WeenieClassName.pauldronsleathernew,     1.0f ),
                    ( WeenieClassName.bracersleathernew,       1.0f ),
                };

                StuddedLeatherWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldkite,                1.0f ),
                    ( WeenieClassName.shieldround,               1.0f ),

                    ( WeenieClassName.cowlstuddedleather,        1.0f ),
                    ( WeenieClassName.basinetstuddedleather,     1.0f ),

                    ( WeenieClassName.gauntletsstuddedleather,   2.0f ),
                    ( WeenieClassName.bootsreinforcedleather,    2.0f ),

                    ( WeenieClassName.coatstuddedleather,        1.0f ),
                    ( WeenieClassName.shirtstuddedleather,       1.0f ),
                    ( WeenieClassName.cuirassstuddedleather,     1.0f ),
                    ( WeenieClassName.breastplatestuddedleather, 1.0f ),

                    ( WeenieClassName.girthstuddedleather,       1.0f ),

                    ( WeenieClassName.leggingsstuddedleather,    1.0f ),
                    ( WeenieClassName.tassetsstuddedleather,     1.0f ),
                    ( WeenieClassName.greavesstuddedleather,     1.0f ),

                    ( WeenieClassName.sleevesstuddedleather,     1.0f ),
                    ( WeenieClassName.pauldronsstuddedleather,   1.0f ),
                    ( WeenieClassName.bracersstuddedleather,     1.0f ),
                };

                ChainmailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldkitelarge,      1.0f ),
                    ( WeenieClassName.shieldroundlarge,     1.0f ),

                    ( WeenieClassName.capmetal,             1.0f ),
                    ( WeenieClassName.mailcoif,             1.0f ),
                    ( WeenieClassName.basinetchainmail,     1.0f ),

                    ( WeenieClassName.gauntletschainmail,   2.0f ),
                    ( WeenieClassName.bootssteeltoe,        2.0f ),
                    
                    ( WeenieClassName.shirtchainmail,       1.0f ),
                    ( WeenieClassName.hauberkchainmail,     1.0f ),
                    ( WeenieClassName.breastplatechainmail, 1.0f ),                    

                    ( WeenieClassName.girthchainmail,       1.0f ),

                    ( WeenieClassName.leggingschainmail,    1.0f ),
                    ( WeenieClassName.tassetschainmail,     1.0f ),
                    ( WeenieClassName.greaveschainmail,     1.0f ),

                    ( WeenieClassName.sleeveschainmail,     1.0f ),
                    ( WeenieClassName.pauldronschainmail,   1.0f ),
                    ( WeenieClassName.bracerschainmail,     1.0f ),
                };

                // platemail - aluvian
                PlatemailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.buckler,              0.2f ),
                    ( WeenieClassName.shieldkite,           0.2f ),
                    ( WeenieClassName.shieldround,          0.2f ),
                    ( WeenieClassName.shieldkitelarge,      0.2f ),
                    ( WeenieClassName.shieldroundlarge,     0.2f ),
                    ( WeenieClassName.shieldtower,          0.2f ),

                    ( WeenieClassName.helmet,               1.0f ),
                    ( WeenieClassName.helmhorned,           1.0f ),

                    ( WeenieClassName.armet,                1.0f ),
                    ( WeenieClassName.heaume,               1.0f ),
                    ( WeenieClassName.heaumenew,            1.0f ),

                    ( WeenieClassName.gauntletsplatemail,   2.0f ),
                    ( WeenieClassName.sollerets,            2.0f ),
                                      
                    ( WeenieClassName.hauberkplatemail,     1.0f ),
                    ( WeenieClassName.cuirassplatemail,     1.0f ),
                    ( WeenieClassName.breastplateplatemail, 1.0f ),

                    ( WeenieClassName.girthplatemail,       1.0f ),

                    ( WeenieClassName.leggingsplatemail,    1.0f ),
                    ( WeenieClassName.tassetsplatemail,     1.0f ),
                    ( WeenieClassName.greavesplatemail,     1.0f ),

                    ( WeenieClassName.sleevesplatemail,     1.0f ),
                    ( WeenieClassName.pauldronsplatemail,   1.0f ),
                    ( WeenieClassName.vambracesplatemail,   1.0f ),
                };

                // platemail - gharu'ndim
                ScalemailWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.buckler,              0.2f ),
                    ( WeenieClassName.shieldkite,           0.2f ),
                    ( WeenieClassName.shieldround,          0.2f ),
                    ( WeenieClassName.shieldkitelarge,      0.2f ),
                    ( WeenieClassName.shieldroundlarge,     0.2f ),
                    ( WeenieClassName.shieldtower,          0.2f ),

                    ( WeenieClassName.baigha,               1.0f ),
                    ( WeenieClassName.basinetscalemail,     1.0f ),
                    ( WeenieClassName.coifscale,            1.0f ),

                    ( WeenieClassName.gauntletsscalemail,   2.0f ),
                    ( WeenieClassName.sollerets,            2.0f ),

                    ( WeenieClassName.breastplatescalemail, 1.0f ),
                    ( WeenieClassName.cuirassscalemail,     1.0f ),
                    ( WeenieClassName.hauberkscalemail,     1.0f ),
                    ( WeenieClassName.shirtscalemail,       1.0f ),

                    ( WeenieClassName.girthscalemail,       1.0f ),

                    ( WeenieClassName.leggingsscalemail,    1.0f ),
                    ( WeenieClassName.tassetsscalemail,     1.0f ),
                    ( WeenieClassName.greavesscalemail,     1.0f ),

                    ( WeenieClassName.sleevesscalemail,     1.0f ),
                    ( WeenieClassName.pauldronsscalemail,   1.0f ),
                    ( WeenieClassName.bracersscalemail,     1.0f ),
                };

                // platemail - sho
                YoroiWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.buckler,              0.2f ),
                    ( WeenieClassName.shieldkite,           0.2f ),
                    ( WeenieClassName.shieldround,          0.2f ),
                    ( WeenieClassName.shieldkitelarge,      0.2f ),
                    ( WeenieClassName.shieldroundlarge,     0.2f ),
                    ( WeenieClassName.shieldtower,          0.2f ),

                    ( WeenieClassName.kabuton,              1.0f ),
                    ( WeenieClassName.helmet,               1.0f ),

                    ( WeenieClassName.gauntletsplatemail,   2.0f ),
                    ( WeenieClassName.sollerets,            2.0f ),

                    ( WeenieClassName.breastplateyoroi,     1.0f ),
                    ( WeenieClassName.cuirassyoroi,         1.0f ),
                    ( WeenieClassName.girthyoroi,           1.0f ),

                    ( WeenieClassName.leggingsyoroi,        1.0f ),
                    ( WeenieClassName.tassetsyoroi,         1.0f ),
                    ( WeenieClassName.greavesyoroi,         1.0f ),

                    ( WeenieClassName.sleevesyoroi,         1.0f ),
                    ( WeenieClassName.pauldronsyoroi,       1.0f ),
                    ( WeenieClassName.kote,                 1.0f ),
                };

                // heritage low - aluvian
                CeldonWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.girthceldon,       1.0f ),
                    ( WeenieClassName.breastplateceldon, 1.0f ),
                    ( WeenieClassName.leggingsceldon,    1.0f ),
                    ( WeenieClassName.sleevesceldon,     1.0f ),
                };

                // heritage low - gharu'ndim
                AmuliWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.coatamullian,     1.0f ),
                    ( WeenieClassName.leggingsamullian, 1.0f ),
                };

                // heritage low - sho
                KoujiaWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.breastplatekoujia, 1.0f ),
                    ( WeenieClassName.leggingskoujia,    1.0f ),
                    ( WeenieClassName.sleeveskoujia,     1.0f ),
                };

                CovenantWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.shieldcovenant,      1.0f ),

                    ( WeenieClassName.helmcovenant,        1.0f ),

                    ( WeenieClassName.gauntletscovenant,   2.0f ),
                    ( WeenieClassName.bootscovenant,       2.0f ),

                    ( WeenieClassName.breastplatecovenant, 1.0f ),

                    ( WeenieClassName.girthcovenant,       1.0f ),

                    ( WeenieClassName.tassetscovenant,     1.0f ),
                    ( WeenieClassName.greavescovenant,     1.0f ),

                    ( WeenieClassName.pauldronscovenant,   1.0f ),
                    ( WeenieClassName.bracerscovenant,     1.0f ),
                };

                // heritage high - aluvian
                LoricaWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmlorica,        1.0f ),

                    ( WeenieClassName.bootslorica,       2.0f ),
                    ( WeenieClassName.gauntletslorica,   2.0f ),

                    ( WeenieClassName.breastplatelorica, 1.0f ),

                    ( WeenieClassName.leggingslorica,    1.0f ),

                    ( WeenieClassName.sleeveslorica,     1.0f ),
                };

                // heritage high - gharu'ndim
                NariyidWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmnariyid,        1.0f ),

                    ( WeenieClassName.bootsnariyid,       2.0f ),
                    ( WeenieClassName.gauntletsnariyid,   2.0f ),
                    
                    ( WeenieClassName.breastplatenariyid, 1.0f ),

                    ( WeenieClassName.girthnariyid,       1.0f ),

                    ( WeenieClassName.leggingsnariyid,    1.0f ),

                    ( WeenieClassName.sleevesnariyid,     1.0f ),
                };

                // heritage high - sho
                ChiranWcids = new ChanceTable<WeenieClassName>(ChanceTableType.Weight)
                {
                    ( WeenieClassName.helmchiran,      1.0f ),

                    ( WeenieClassName.sandalschiran,   2.0f ),
                    ( WeenieClassName.gauntletschiran, 2.0f ),
                    
                    ( WeenieClassName.coatchiran,      1.0f ),

                    ( WeenieClassName.leggingschiran,  1.0f ),
                };
            }

            BuildCombined(LeatherWcids, TreasureArmorType.Leather);
            BuildCombined(StuddedLeatherWcids, TreasureArmorType.StuddedLeather);
            BuildCombined(ChainmailWcids, TreasureArmorType.Chainmail);
            BuildCombined(PlatemailWcids, TreasureArmorType.Platemail);
            BuildCombined(ScalemailWcids, TreasureArmorType.Scalemail);
            BuildCombined(YoroiWcids, TreasureArmorType.Yoroi);
            BuildCombined(CeldonWcids, TreasureArmorType.Celdon);
            BuildCombined(AmuliWcids, TreasureArmorType.Amuli);
            BuildCombined(KoujiaWcids, TreasureArmorType.Koujia);
            BuildCombined(CovenantWcids, TreasureArmorType.Covenant);
            BuildCombined(LoricaWcids, TreasureArmorType.Lorica);
            BuildCombined(NariyidWcids, TreasureArmorType.Nariyid);
            BuildCombined(ChiranWcids, TreasureArmorType.Chiran);
            BuildCombined(DiforsaWcids, TreasureArmorType.Diforsa);
            BuildCombined(TenassaWcids, TreasureArmorType.Tenassa);
            BuildCombined(AlduressaWcids, TreasureArmorType.Alduressa);
            BuildCombined(OlthoiWcids, TreasureArmorType.Olthoi);
            BuildCombined(OlthoiCeldonWcids, TreasureArmorType.OlthoiCeldon);
            BuildCombined(OlthoiAmuliWcids, TreasureArmorType.OlthoiAmuli);
            BuildCombined(OlthoiKoujiaWcids, TreasureArmorType.OlthoiKoujia);
            BuildCombined(OlthoiAlduressaWcids, TreasureArmorType.OlthoiAlduressa);
            //BuildCombined(CelestialHandWcids, TreasureArmorType.CelestialHand);   // handled in SocietyArmor
            //BuildCombined(EldrytchWebWcids, TreasureArmorType.EldrytchWeb);
            //BuildCombined(RadiantBloodWcids, TreasureArmorType.RadiantBlood);
            BuildCombined(HaebreanWcids, TreasureArmorType.Haebrean);
            BuildCombined(KnorrAcademyWcids, TreasureArmorType.KnorrAcademy);
            BuildCombined(SedgemailLeatherWcids, TreasureArmorType.Sedgemail);
            BuildCombined(OverRobe_T3_T5_Wcids, TreasureArmorType.Overrobe);
            BuildCombined(OverRobe_T6_T8_Wcids, TreasureArmorType.Overrobe);
            BuildCombined(ClothAluvianWcids, TreasureArmorType.Cloth);
            BuildCombined(ClothGharuWcids, TreasureArmorType.Cloth);
            BuildCombined(ClothShoWcids, TreasureArmorType.Cloth);
        }

        private static void BuildCombined(ChanceTable<WeenieClassName> wcids, TreasureArmorType armorType)
        {
            foreach (var entry in wcids)
                _combined.TryAdd(entry.result, armorType);
        }

        public static bool TryGetValue(WeenieClassName wcid, out TreasureArmorType armorType)
        {
            return _combined.TryGetValue(wcid, out armorType);
        }
    }
}
