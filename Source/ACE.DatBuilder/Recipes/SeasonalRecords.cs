using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The foliage colors and the ground of the seasonal patches of the old client, on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>). The foliage only recolors: the same pixels with another palette, so a season
    /// step rewrites the image (0x06) of each kit texture from the palette of the season's own dat. The ground is the Region's terrain table: a season points some terrain types at other ground textures (snow, dormant grass), all of which
    /// the 2005 dat already has with the same pixels. The scenery is the Region's scene slots: the winter seasons point them at the snow-time scenes (fewer trees, no flowers or butterflies), which the 2005 dat still has. The 2005 dat itself is the fall state, so Fall is the state a build has without any step.
    /// A season step is self-contained (it writes every kit texture, from the season's dat where the season changes it and from the 2005 dat otherwise, and every terrain type any season changes, from the season where it changes it and from the 2005 Region otherwise),
    /// so switching from any season to any other is one more dated step. Only the steps in a recipe's list run: the others stay unused until an event needs them. Each season's dat is in the source dats folder, the kit is in <see cref="KitFile"/>.
    /// </summary>
    /// <remarks>
    /// How the kit was found (2026-11-21 and 22, every archive portal dat from 2000-02 to 2005-04 fingerprinted, the offline wiki's patch notes for the dates): the foliage palettes 040008E4 and 04000E7C hold the season (the green of spring and summer until 2001-09, autumn
    /// 87690F from 2001-09 to 2001-12 and from 2002-10 on, red-brown 87390F on 2002-01-03, dull olive 66582F from 2002-01-30 to 2002-08-15). The terrain table (Region 130F0000) holds the ground: partial snow on 2002-01-03 and 2002-02-14, full snow on 2002-01-30,
    /// dormant grass and forest floor (1B1A, 1B1C) from 2002-03-08 to 2002-08-15. The 2001-03-26 (spring) and 2001-07-05 (summer) dats have the same palettes, so Spring and Summer are one season. The names follow the EoR weather sets (Fall, Spring, Winter with onset and thaw).
    /// The foliage kit is every texture that uses one of those palettes and has the pixels the 2005 dat has; a texture whose pixels differ from the 2005 dat is old art, not a season, and stays out.
    /// Left out on purpose: the permanent 2002 palette change 04000FAA, the terrain types 28-30 (moss, which the 2002-01-03 and 2002-02-14 dats show as barren rock because they come from an older branch), the scenes of Spring and Early Spring (the 2001 and 2002 scene lists are older than the 2005 trees) and the sky of the seasonal dats, and the Red season of the 2000-12 dats (the Child of Daralet kit has the red look). The EoR weather dats are not in the kit yet.
    /// </remarks>
    public static class SeasonalRecords
    {
        public const string KitFile = "seasonal-foliage.json";

        /// <summary>The season of the 2005 dat itself; the kit has no entry for it.</summary>
        public const string Fall = "Fall";

        private const string FallAlias = "Autumn";

        public class Season
        {
            public string Name { get; set; }

            /// <summary>Other names of the same season, such as Summer for Spring.</summary>
            public List<string> Aliases { get; set; } = new List<string>();

            /// <summary>The pre-ToD portal dat in the source dats folder that holds the season's palettes.</summary>
            public string Source { get; set; }

            /// <summary>The textures (0x05) the season takes from its dat; the other kit textures keep the 2005 look.</summary>
            public List<string> Textures { get; set; }

            /// <summary>The ground textures (0x05) the season puts on terrain types, by terrain type; the other terrain types of the kit keep their 2005 texture.</summary>
            public Dictionary<string, string> Ground { get; set; } = new Dictionary<string, string>();

            /// <summary>The scenes (0x12) the season puts on the Region's scene slots (what grows on each terrain), by slot; the other slots of the kit keep their 2005 scenes.</summary>
            public Dictionary<string, List<string>> Scenes { get; set; } = new Dictionary<string, List<string>>();
        }

        public class Kit
        {
            /// <summary>The seasons that differ from the 2005 dat. Fall has no entry: the 2005 dat is already fall (<see cref="Fall"/>), so its step just writes every kit texture and terrain type from it.</summary>
            public List<Season> Seasons { get; set; }

            /// <summary>Every texture a season changes: each season step writes all of them.</summary>
            public List<string> All { get; set; }
        }

        /// <summary>The season names a step can be made for.</summary>
        public static IEnumerable<string> Names => new[] { Fall }.Concat(DatRecipeData.Load<Kit>(KitFile).Seasons.Select(s => s.Name));

        /// <summary>The step that puts the kit into the named season (Fall, Spring, Winter onset, Winter, Winter thaw, Early Spring, or an alias such as Summer or Autumn), carrying the date of the event it is added for.</summary>
        /// <exception cref="ArgumentException">There is no such season.</exception>
        public static DatRecordStep Step(string season, string date)
        {
            var kit = DatRecipeData.Load<Kit>(KitFile);
            var chosen = kit.Seasons.SingleOrDefault(s => s.Aliases.Prepend(s.Name).Contains(season, StringComparer.OrdinalIgnoreCase));

            if (chosen == null && !new[] { Fall, FallAlias }.Contains(season, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"No season '{season}'; the seasons are {string.Join(", ", Names)}.", nameof(season));

            var own = new HashSet<uint>((chosen?.Textures ?? new List<string>()).Select(t => Convert.ToUInt32(t, 16)));

            return new DatRecordStep($"Season: {chosen?.Name ?? Fall}", date, c =>
            {
                var clipMap = DatPreToDPortalConverter.ClipMapTextures(new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PreToDPortalDat)));

                foreach (var id in kit.All.Select(t => Convert.ToUInt32(t, 16)))
                {
                    var dat = own.Contains(id) ? chosen.Source : DatBuildRecipes.PreToDPortalDat;
                    var imageId = c.ReadTarget<SurfaceTexture>(id).Textures[0];
                    var image = DatPortalImporter.ConvertImage(c.ReadSource(dat, id), imageId, palette => c.ReadSource(dat, palette), out var width, out var height, out _, clipMap.Contains(id));

                    c.Write(imageId, image, $"foliage texture {id:X8} ({width}x{height}) from {(own.Contains(id) ? chosen.Name : Fall)}");
                }

                var ground = (chosen?.Ground ?? new Dictionary<string, string>()).ToDictionary(g => uint.Parse(g.Key), g => Convert.ToUInt32(g.Value, 16));

                foreach (var id in ground.Values.Distinct())
                    GroundTextureRecords.WriteTerrainImage(c, c.ReadTarget<SurfaceTexture>(id).Textures[0], id);

                var source = RegionDesc.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, PreToDBaseRecords.HardwareRegionId));
                var original = source.TerrainInfo.LandSurfaces.TexMerge.TerrainDesc.ToDictionary(d => d.TerrainType, d => d.TerrainTex.TexGID);
                var types = kit.Seasons.SelectMany(s => s.Ground.Keys).Select(uint.Parse).ToHashSet();
                var region = c.ReadTarget<RegionDesc>(GroundTextureRecords.RegionId);

                foreach (var description in region.TerrainInfo.LandSurfaces.TexMerge.TerrainDesc.Where(d => types.Contains(d.TerrainType)))
                    description.TerrainTex.TexGID = ground.TryGetValue(description.TerrainType, out var texture) ? texture : original[description.TerrainType];

                var scenes = (chosen?.Scenes ?? new Dictionary<string, List<string>>()).ToDictionary(s => int.Parse(s.Key), s => s.Value.Select(id => Convert.ToUInt32(id, 16)).ToList());

                foreach (var index in kit.Seasons.SelectMany(s => s.Scenes.Keys).Select(int.Parse).Distinct())
                {
                    region.SceneInfo.SceneTypes[index].Scenes.Clear();
                    region.SceneInfo.SceneTypes[index].Scenes.AddRange(scenes.TryGetValue(index, out var list) ? list : source.SceneInfo.SceneTypes[index].Scenes);
                }

                c.Write(GroundTextureRecords.RegionId, region.Pack(), $"region ground and scenes of {chosen?.Name ?? Fall}");
                GroundTextureRecords.RegionStep(c.Step.Date).Run(c);
            });
        }
    }
}
