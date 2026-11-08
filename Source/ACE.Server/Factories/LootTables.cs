using ACE.Entity.Enum;
using ACE.Server.Factories.Tables;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Factories
{
    public static class LootTables
    {
        public static int[][] DefaultMaterial { get; } =
        {
            new int[] { (int)MaterialType.Copper, (int)MaterialType.Bronze, (int)MaterialType.Iron, (int)MaterialType.Steel, (int)MaterialType.Silver },            // Armor
            new int[] { (int)MaterialType.Oak, (int)MaterialType.Teak, (int)MaterialType.Mahogany, (int)MaterialType.Pine, (int)MaterialType.Ebony },               // Missile
            new int[] { (int)MaterialType.Brass, (int)MaterialType.Ivory, (int)MaterialType.Gold, (int)MaterialType.Steel, (int)MaterialType.Diamond },             // Melee
            new int[] { (int)MaterialType.RedGarnet, (int)MaterialType.Jet, (int)MaterialType.BlackOpal, (int)MaterialType.FireOpal, (int)MaterialType.Emerald },   // Caster
            new int[] { (int)MaterialType.Granite, (int)MaterialType.Ceramic, (int)MaterialType.Porcelain, (int)MaterialType.Alabaster, (int)MaterialType.Marble }, // Dinnerware
            new int[] { (int)MaterialType.Linen, (int)MaterialType.Wool, (int)MaterialType.Velvet, (int)MaterialType.Satin, (int)MaterialType.Silk }                // Clothes
        };

        // for logging epic/legendary drops
        public static HashSet<int> MinorCantrips;
        public static HashSet<int> MajorCantrips;
        public static HashSet<int> EpicCantrips;
        public static HashSet<int> LegendaryCantrips;

        static LootTables()
        {
            BuildCantripsTables();
        }

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
