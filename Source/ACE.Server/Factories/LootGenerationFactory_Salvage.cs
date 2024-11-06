using ACE.Common;
using ACE.Server.Factories.Tables.Wcids;
using ACE.Server.WorldObjects;
using System;

namespace ACE.Server.Factories
{
    public static partial class LootGenerationFactory
    {
        private static void MutateSalvage(WorldObject salvage, int tier)
        {
            if (salvage.ItemType != ACE.Entity.Enum.ItemType.TinkeringMaterial)
                return;

            ushort structure = 100;
            var workmanship = SalvageWorkmanshipChances.Roll(tier);
            var numItemsInMaterial = ThreadSafeRandom.Next(1, 100);
            var itemWorkmanship = (int)Math.Round(workmanship * numItemsInMaterial);
            var value = (int)(workmanship * 1500) * SalvageWcids.GetValueMod(salvage.WeenieClassId);
            value *= (float)ThreadSafeRandom.Next(0.8f, 1.2f);

            salvage.Name = $"Salvage ({structure})";
            salvage.Structure = structure;
            salvage.ItemWorkmanship = itemWorkmanship;
            salvage.NumItemsInMaterial = numItemsInMaterial;
            salvage.Value = (int)value;
        }
    }
}
