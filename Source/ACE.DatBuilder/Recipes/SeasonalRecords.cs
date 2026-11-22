using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The foliage colors of the seasonal patches of the old client, on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>). The seasons only recolor: the same pixels with another palette, so a season
    /// step rewrites the image (0x06) of each kit texture from the palette of the season's own dat. The 2005 dat itself is the autumn state, so Autumn is the state a build has without any step.
    /// A season step is self-contained (it writes every kit texture, from the season's dat where the season changes it and from the 2005 dat otherwise), so switching from any season to any other
    /// is one more dated step. Only the steps in a recipe's list run: the others stay unused until an event needs them. Each season's dat is in the source dats folder, the kit is in <see cref="KitFile"/>.
    /// </summary>
    /// <remarks>
    /// How the kit was found (2026-11-21, every archive portal dat from 2000-02 to 2005-04 fingerprinted): the foliage palettes 040008E4 and 04000E7C hold the season (summer A31231/B6E27F until 2001-07, autumn 200A18/3636BD
    /// from 2001-06 to the 2001-07-05 revert and from 2002-10 on, Snow ADAF2A on 2002-01-03, Winter 8F4B8E from 2002-01-30 to 2002-08-15); the 2000-12 dats carry a red look through 29 other palettes (04001083-040010A6).
    /// The kit is every texture that uses one of those palettes and has the pixels the 2005 dat has; a texture whose pixels differ from the 2005 dat is old art, not a season, and stays out, except the four red effect textures
    /// (05001267, 0500129B, 05001394, 050016A9), which the Red season takes whole. Left out on purpose: the permanent 2002 palette change 04000FAA and everything else the seasonal dats changed. Open: the Red season's
    /// eight birch textures (050017F1-050017F9) have older art in the 2000-12 dat and stay as in the 2005 dat; the Dec 2000 dats' further seasonal surfaces and the EoR weather dats are not in the kit yet.
    /// </remarks>
    public static class SeasonalRecords
    {
        public const string KitFile = "seasonal-foliage.json";

        /// <summary>The season of the 2005 dat itself.</summary>
        public const string Autumn = "Autumn";

        public class Season
        {
            public string Name { get; set; }

            /// <summary>The pre-ToD portal dat in the source dats folder that holds the season's palettes.</summary>
            public string Source { get; set; }

            /// <summary>The textures (0x05) the season takes from its dat; the other kit textures keep the 2005 look.</summary>
            public List<string> Textures { get; set; }
        }

        public class Kit
        {
            public List<Season> Seasons { get; set; }

            /// <summary>Every texture a season changes: each season step writes all of them.</summary>
            public List<string> All { get; set; }
        }

        /// <summary>The season names a step can be made for.</summary>
        public static IEnumerable<string> Names => new[] { Autumn }.Concat(DatRecipeData.Load<Kit>(KitFile).Seasons.Select(s => s.Name));

        /// <summary>The step that puts the kit into the named season (Summer, Autumn, Snow, Winter, Red), carrying the date of the event it is added for.</summary>
        /// <exception cref="ArgumentException">There is no such season.</exception>
        public static DatRecordStep Step(string season, string date)
        {
            var kit = DatRecipeData.Load<Kit>(KitFile);
            var chosen = kit.Seasons.SingleOrDefault(s => string.Equals(s.Name, season, StringComparison.OrdinalIgnoreCase));

            if (chosen == null && !string.Equals(season, Autumn, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"No season '{season}'; the seasons are {string.Join(", ", Names)}.", nameof(season));

            var own = new HashSet<uint>((chosen?.Textures ?? new List<string>()).Select(t => Convert.ToUInt32(t, 16)));

            return new DatRecordStep($"Foliage colors: {chosen?.Name ?? Autumn}", date, c =>
            {
                var clipMap = DatPreToDPortalConverter.ClipMapTextures(new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PreToDPortalDat)));

                foreach (var id in kit.All.Select(t => Convert.ToUInt32(t, 16)))
                {
                    var dat = own.Contains(id) ? chosen.Source : DatBuildRecipes.PreToDPortalDat;
                    var imageId = c.ReadTarget<SurfaceTexture>(id).Textures[0];
                    var image = DatPortalImporter.ConvertImage(c.ReadSource(dat, id), imageId, palette => c.ReadSource(dat, palette), out var width, out var height, out _, clipMap.Contains(id));

                    c.Write(imageId, image, $"foliage texture {id:X8} ({width}x{height}) from {(own.Contains(id) ? chosen.Name : Autumn)}");
                }
            });
        }
    }
}
