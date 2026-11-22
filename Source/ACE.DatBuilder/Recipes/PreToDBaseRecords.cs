using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The records of a portal dat that starts from the pre-ToD portal dat instead of the EoR one: every pre-ToD record converted to the EoR layout
    /// (<see cref="DatPreToDPortalConverter"/>), then what the pre-ToD dat does not have, from the EoR dat.
    /// </summary>
    public static class PreToDBaseRecords
    {
        public const string EoRRecordsFile = "infiltration-eor-records.json";

        /// <summary>The base palettes of the objects of the worlds (the PaletteBase values of ace_world_infiltration, ace_world_customdm and the Infiltration shard), as ids.</summary>
        public const string BasePalettesFile = "palette-bases.json";

        // EoR record types copied whole besides the listed ids: the tables (chargen, spells, skills, combat), the property master the UI layouts are read with, the UI strings (0x31: the
        // EoR layouts name them, and the 2005 ones make the login screen's ENTER button a small plain text), and the types the pre-ToD dat has no record of at all (0x14-0x18, 0x22,
        // 0x25-0x27, 0x78): the header's MasterMapID (0x25000000) points into them, and the client cannot name its language dat without it ("Can't find the local data file ''").
        private static readonly uint[] EoRTypes = { 0x0E, 0x39, 0x31, 0x14, 0x15, 0x16, 0x17, 0x18, 0x22, 0x25, 0x26, 0x27, 0x78 };

        private const uint CharGenId = 0x0E000002;
        private const uint RegionId = 0x13000000;
        public const uint HardwareRegionId = 0x130F0000;

        /// <param name="rulesetEoRRecordsFiles">The EoR-only records the ruleset's world needs besides those of <see cref="EoRRecordsFile"/>.</param>
        public static DatRecordStep[] All(string date, params string[] rulesetEoRRecordsFiles)
        {
            return new[] { ConvertedBase(date), FromEoR(date, rulesetEoRRecordsFiles), UiImages(date), PreToDUiPictures(date), Region(date), MoonRecords.Overlay(date), ChargenFaces(date) };
        }

        /// <summary>
        /// The EoR character generation record with the heads of the three heritages the pre-ToD client had (Aluvian, Gharu'ndim, Sho): the hair, eye, nose and mouth descriptions of the EoR
        /// record name EoR head models and textures, and the pre-ToD models with the same ids hold other textures (a black face, wrong hair).
        /// Also the bare body: the 2005 client drew the base appearance of this record (BaseObjDesc: other arm and hand models, and a woman's chest) because its server sent it as the character's
        /// appearance, while the EoR record has the Setup's own models there and our server sends nothing for a bare body. The record itself is right, but the world client reads the Setup,
        /// whose 2005 default arms name the armor-sleeve textures, so the base models are written into the Setups.
        /// </summary>
        private static DatRecordStep ChargenFaces(string date)
        {
            return new DatRecordStep("Chargen faces and base body from the 2005 record", date, c =>
            {
                var chargen = c.ReadTarget<CharGen>(CharGenId);
                var preToD = CharGen.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, CharGenId));

                foreach (var heritage in preToD.HeritageGroups)
                    foreach (var sex in heritage.Value.Genders)
                        chargen.HeritageGroups[heritage.Key].Genders[sex.Key].TakeFaceFrom(sex.Value);

                c.Write(CharGenId, chargen.Pack(), "character generation faces", true);

                foreach (var sex in preToD.HeritageGroups.Values.SelectMany(h => h.Genders.Values).GroupBy(s => s.SetupID).Select(g => g.First()))
                {
                    var setup = c.ReadTarget<SetupModel>(sex.SetupID);

                    foreach (var change in sex.BaseObjDesc.AnimPartChanges)
                        setup.Parts[change.PartIndex] = change.PartID;

                    c.Write(sex.SetupID, setup.Pack(), "setup with the 2005 base body models", true);
                }
            });
        }

        /// <summary>
        /// The Region of the pre-ToD client (its hardware record) in the EoR layout. Only what the 2005 record cannot hold comes from the EoR record: the sky objects' properties,
        /// the region number and the terrain types the 2005 record lacks (with their terrain textures). The EoR additions the 2005 client never had (sky scripts, new scenes, redone sky models) stay out.
        /// </summary>
        /// <exception cref="InvalidDataException">The two records differ in shape in a way the fills cannot bridge.</exception>
        private static DatRecordStep Region(string date)
        {
            return Region("Region of the 2005 client", date, DatBuildRecipes.PreToDPortalDat, (c, region, eor) =>
            {
                foreach (var (old, eorObject) in MatchedSkyObjects(region, eor))
                    old.Properties = eorObject.Properties;
            });
        }

        /// <summary>
        /// The properties EoR gives the objects of the 2005 sky, by the id of the 2005 object. EoR sets them by role, not by model (it swapped the models of the sky textures and the rain layers but kept the groups and their slots),
        /// so a model of another pre-ToD dat that the 2005 sky also uses gets the value of its role, and one the 2005 sky does not use gets 0.
        /// </summary>
        /// <exception cref="InvalidDataException">The 2005 and EoR skies differ in shape, or a 2005 model has different values in different slots.</exception>
        public static Dictionary<uint, uint> SkyPropertiesOf2005(DatRecordContext c, RegionDesc eor)
        {
            var properties = new Dictionary<uint, uint>();

            foreach (var (old, eorObject) in MatchedSkyObjects(RegionDesc.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, HardwareRegionId)), eor))
                if (properties.TryGetValue(old.DefaultGFXObjectId, out var known) && known != eorObject.Properties)
                    throw new InvalidDataException($"The 2005 sky object {old.DefaultGFXObjectId:X8} has the EoR properties {known:X} and {eorObject.Properties:X} in different slots.");
                else
                    properties[old.DefaultGFXObjectId] = eorObject.Properties;

            return properties;
        }

        private static IEnumerable<(SkyObject Old, SkyObject Eor)> MatchedSkyObjects(RegionDesc old, RegionDesc eor)
        {
            var days = old.SkyInfo.DayGroups;

            if (days.Count != eor.SkyInfo.DayGroups.Count || days.Zip(eor.SkyInfo.DayGroups, (a, b) => a.SkyObjects.Count != b.SkyObjects.Count).Any(different => different))
                throw new InvalidDataException("The 2005 and EoR regions have different day groups or sky objects, so the sky object properties cannot be matched by position.");

            return days.SelectMany((day, i) => day.SkyObjects.Select((skyObject, j) => (skyObject, eor.SkyInfo.DayGroups[i].SkyObjects[j])));
        }

        /// <summary>
        /// The hardware Region record of a pre-ToD dat in the EoR layout, with the EoR record's region number and the terrain types (with their terrain textures) it lacks.
        /// <paramref name="fillSky"/> gets the build context, the converted record and the EoR record and makes the sky objects fit the EoR layout (their properties).
        /// </summary>
        public static DatRecordStep Region(string name, string date, string sourceDat, Action<DatRecordContext, RegionDesc, RegionDesc> fillSky)
        {
            return new DatRecordStep(name, date, c =>
            {
                var region = RegionDesc.UnpackPreToD(c.ReadSource(sourceDat, HardwareRegionId));
                var eor = c.ReadSource<RegionDesc>(DatBuildRecipes.PortalBaseDat, RegionId);

                region.Id = RegionId;
                region.RegionNumber = eor.RegionNumber;
                fillSky(c, region, eor);

                var types = region.TerrainInfo.TerrainTypes;
                var added = new List<int>();

                for (var i = types.Count; i < eor.TerrainInfo.TerrainTypes.Count; i++)
                {
                    var type = eor.TerrainInfo.TerrainTypes[i];

                    // Its scenery is made of scenes the 2005 list does not have; it gets none.
                    type.SceneTypes.RemoveAll(s => s >= region.SceneInfo.SceneTypes.Count);
                    types.Add(type);
                    added.Add(i);
                }

                var merge = region.TerrainInfo.LandSurfaces.TexMerge;
                var known = merge.TerrainDesc.Select(t => t.TerrainType).ToHashSet();

                foreach (var description in eor.TerrainInfo.LandSurfaces.TexMerge.TerrainDesc.Where(t => !known.Contains(t.TerrainType)))
                    merge.TerrainDesc.Add(description);

                c.Write(RegionId, region.Pack(), $"region from {HardwareRegionId:X8} (sky properties and region number from EoR, terrain types added: {string.Join(",", added)})", true);
            });
        }

        /// <summary>Every pre-ToD record converted; the new images (0x06) are numbered after the last EoR image.</summary>
        private static DatRecordStep ConvertedBase(string date)
        {
            return new DatRecordStep("Pre-ToD records converted", date, c =>
            {
                var firstImageId = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PortalBaseDat)).AllFiles.Keys.Where(k => k >> 24 == 0x06).Max() + 1;

                // The base palettes of the worlds' objects, and those of the character creation (every heritage and sex of the EoR CharGen record): its face and hair images are recoloured the same way.
                var basePalettes = DatRecipeData.Load<List<string>>(BasePalettesFile).Select(s => System.Convert.ToUInt32(s, 16)).ToHashSet();
                basePalettes.UnionWith(c.ReadSource<CharGen>(DatBuildRecipes.PortalBaseDat, CharGenId).HeritageGroups.Values.SelectMany(h => h.Genders.Values).Select(g => g.BasePalette));

                var converter = new DatPreToDPortalConverter(new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PreToDPortalDat)), firstImageId, basePalettes);

                c.Write(converter.Convert(), "converted pre-ToD records", true);

                if (converter.Failures.Count > 0)
                    throw new InvalidDataException($"{converter.Failures.Count} pre-ToD records did not convert, the first: {converter.Failures[0]}");

                c.Log.Add($"{converter.DegradedModels.Count:N0} models got a degrade id, {converter.ImageOfTexture.Count:N0} textures got a new image");
            });
        }

        /// <summary>The EoR records the pre-ToD records do not cover (the id list is recipe data) and the EoR tables; an id the converted base already holds keeps the pre-ToD record.</summary>
        private static DatRecordStep FromEoR(string date, string[] rulesetEoRRecordsFiles)
        {
            return new DatRecordStep("EoR records the pre-ToD dat lacks", date, c =>
            {
                var eor = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PortalBaseDat));

                var listed = DatRecipeData.Load<List<string>>(EoRRecordsFile).Concat(rulesetEoRRecordsFiles.SelectMany(DatRecipeData.Load<List<string>>)).Select(s => System.Convert.ToUInt32(s, 16))
                    .Concat(eor.AllFiles.Keys.Where(k => EoRTypes.Contains(k >> 24))).ToHashSet();

                // What the client reaches through the master map (the header's MasterMapID): every id an EoR DID mapper holds, and the images of every font the dat gets (a font whose images are missing
                // is drawn in a plain small face: the login screen's ENTER button).
                var mapped = MapperTargets(eor).Where(eor.AllFiles.ContainsKey).ToHashSet();
                foreach (var fontId in eor.AllFiles.Keys.Where(id => id >> 24 == 0x40 && (listed.Contains(id) || mapped.Contains(id) || c.Writer.Contains(id))).ToList())
                {
                    var font = new Font();
                    using (var reader = new BinaryReader(new MemoryStream(eor.GetReaderForFile(fontId).Buffer)))
                        font.Unpack(reader);

                    foreach (var imageId in new[] { font.ForegroundSurfaceDataID, font.BackgroundSurfaceDataID })
                        if (eor.AllFiles.ContainsKey(imageId))
                            mapped.Add(imageId);
                }

                var ids = listed.Union(mapped).Where(id => !c.Writer.Contains(id)).OrderBy(id => id).ToList();

                c.Write(ids.Select(id => new KeyValuePair<uint, byte[]>(id, c.ReadSource(DatBuildRecipes.PortalBaseDat, id))), "EoR records", true);
                c.Log.Add($"{mapped.Count(id => !listed.Contains(id) && ids.Contains(id)):N0} of them reached only through the EoR DID mappers");
            });
        }

        /// <summary>
        /// The plain images (0x06) both dats have, from the EoR dat: the UI images. The pre-ToD ones have no alpha (the client colour-keys black), the EoR ones have it, and the EoR UI is drawn against them
        /// (item icon backgrounds, panel edges), so the 2005 look of the interface does not match its layouts.
        /// </summary>
        private static DatRecordStep UiImages(string date)
        {
            return new DatRecordStep("UI images from EoR", date, c =>
            {
                var eor = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PortalBaseDat));
                var pre = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PreToDPortalDat));

                c.Write(eor.AllFiles.Keys.Where(k => k >> 24 == 0x06 && pre.AllFiles.ContainsKey(k)).OrderBy(k => k).Select(id => new KeyValuePair<uint, byte[]>(id, c.ReadSource(DatBuildRecipes.PortalBaseDat, id))), "EoR images of the UI", true);
            });
        }

        /// <summary>The UI images that keep the 2005 picture: the in-game map and the title screen (an 800x482 image in the EoR dat; the image of the main Asheron's Call title screen).</summary>
        private static readonly uint[] PreToDUiImages = { InfiltrationRecords.MapId, 0x06001343 };

        private static DatRecordStep PreToDUiPictures(string date)
        {
            return new DatRecordStep("UI pictures from the 2005 dat", date, c =>
            {
                foreach (var id in PreToDUiImages)
                    c.Write(id, Texture.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, id)).Pack(), "2005 UI picture", true);
            });
        }

        /// <summary>Every DataID the DidMapper (0x25) and DualDidMapper (0x27) records of the dat hold, client and server side.</summary>
        private static IEnumerable<uint> MapperTargets(DatDatabase dat)
        {
            foreach (var id in dat.AllFiles.Keys.Where(k => k >> 24 == 0x25 || k >> 24 == 0x27))
            {
                using var reader = new BinaryReader(new MemoryStream(dat.GetReaderForFile(id).Buffer));

                if (id >> 24 == 0x25)
                {
                    var mapper = new DidMapper();
                    mapper.Unpack(reader);
                    foreach (var target in mapper.ClientEnumToID.Values.Concat(mapper.ServerEnumToID.Values))
                        yield return target;
                }
                else
                {
                    var mapper = new DualDidMapper();
                    mapper.Unpack(reader);
                    foreach (var target in mapper.ClientEnumToID.Values.Concat(mapper.ServerEnumToID.Values))
                        yield return target;
                }
            }
        }
    }
}
