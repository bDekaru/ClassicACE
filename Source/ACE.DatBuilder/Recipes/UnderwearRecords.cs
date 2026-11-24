using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The brown underwear of the dats before the June 14, 2001 update (Chains of Command: "Underwear is now ivory/off-white"), on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>).
    /// The update changed no texture, only entries 40-95 of the skin base palette 0400007E (the colors the underwear pixels of both sexes read), so the step puts those entries of the 2000-12 dat back.
    /// </summary>
    public static class UnderwearRecords
    {
        private const uint BasePalette = 0x0400007E;
        private const int FirstColor = 40;
        private const int LastColor = 95;

        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Brown underwear from the 2000-12 dat", date, c =>
            {
                var brown = c.ReadSource<Palette>(DatBuildRecipes.Portal2000Dat, BasePalette);
                var palette = c.ReadTarget<Palette>(BasePalette);

                for (var color = FirstColor; color <= LastColor; color++)
                    for (var copy = 0; copy < DatPortalImporter.PaletteRepeat; copy++)
                        palette.Colors[color * DatPortalImporter.PaletteRepeat + copy] = brown.Colors[color];

                c.Write(BasePalette, palette.Pack(), $"skin palette with the colors {FirstColor}-{LastColor} of the underwear", true);
            });
        }
    }
}
