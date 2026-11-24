using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// One cell dat build: a pre-ToD (retail layout) cell dat converted to ToD layout against the portal dat the same ruleset's portal recipe built.
    /// </summary>
    public class DatCellBuildRecipe
    {
        public string Name { get; }
        public string Ruleset { get; }

        /// <summary>File name of the retail-layout cell dat in the source dats folder.</summary>
        public string RetailCellDat { get; }

        /// <summary>A ToD-layout cell dat in the source dats folder: supplies the header, block size and pack versions of the output dat.</summary>
        public string ToDCellDat { get; }

        /// <summary>The portal recipe whose built dat the cells must match; its imported surfaces extend the surface map.</summary>
        public DatBuildRecipe Portal { get; }

        /// <summary>File name of the embedded pre-ToD to ToD surface map.</summary>
        public string SurfaceMap { get; }

        /// <summary>Edits applied to the converted records before they are written; null writes the converted dat as it is. The edits start their own steps with <see cref="DatMapEditor.Step"/>.</summary>
        public Action<DatMapEditor, DatMapSources> MapOperations { get; }

        /// <summary>The releases of the cell dat this recipe builds.</summary>
        public DatReleaseSet Releases { get; }

        /// <summary>The step date of the conversion itself ("yyyy-MM-dd"): the date of every record the map edits do not change.</summary>
        public string ConversionDate { get; }

        /// <summary>Set when the portal dat is made from the pre-ToD portal dat, which keeps the surface ids: the cells keep theirs instead of going through the surface map, and no additions file is read.</summary>
        public bool IdentitySurfaces { get; private set; }

        public DatCellBuildRecipe WithIdentitySurfaces()
        {
            IdentitySurfaces = true;
            return this;
        }

        /// <summary>Output subfolder when it is not the ruleset's name, which is also where the portal build is read from.</summary>
        public string OutputFolder { get; private set; }

        public DatCellBuildRecipe WithOutputFolder(string folder)
        {
            OutputFolder = folder;
            return this;
        }

        public string OutputFileName => "client_cell_1.dat";

        public string Key => Name.Replace(' ', '-').ToLowerInvariant();

        public DatCellBuildRecipe(string name, string ruleset, string retailCellDat, string toDCellDat, DatBuildRecipe portal, string surfaceMap, DatReleaseSet releases, string conversionDate, Action<DatMapEditor, DatMapSources> mapOperations = null)
        {
            Name = name;
            Ruleset = ruleset;
            RetailCellDat = retailCellDat;
            ToDCellDat = toDCellDat;
            Portal = portal;
            SurfaceMap = surfaceMap;
            Releases = releases;
            ConversionDate = conversionDate;
            MapOperations = mapOperations;
        }

        /// <summary>The built portal dat and the surface map additions file the portal build wrote next to it.</summary>
        public string BuiltPortalPath(string outputRoot) => Path.Combine(outputRoot, OutputFolder ?? Ruleset, Portal.OutputFileName);

        public string BuiltAdditionsPath(string outputRoot) => Path.Combine(Path.GetDirectoryName(BuiltPortalPath(outputRoot)), Path.ChangeExtension(Portal.OutputFileName, null) + ".surface_map_additions.tsv");
    }

    public class DatCellBuildResult
    {
        public DatCellBuildRecipe Recipe { get; set; }
        public string OutputPath { get; set; }
        public long OutputBytes { get; set; }
        public DatAudit Audit { get; set; }
        public uint ReleaseNumber { get; set; }
        public string ReleaseHash { get; set; }
        public DatCellConversionReport Conversion { get; set; }

        /// <summary>The conversion reports of the donor dats that map edits converted.</summary>
        public Dictionary<DatMapSource, DatCellConversionReport> SourceReports { get; set; } = new Dictionary<DatMapSource, DatCellConversionReport>();

        public List<string> MapNotes { get; set; } = new List<string>();

        /// <summary>The per-line report of <see cref="DatMapTrace"/> when the build was traced.</summary>
        public string MapTrace { get; set; }

        /// <summary>The portal-list check over the finished store (map edits only).</summary>
        public DatCellConversionReport FinalCheck { get; set; }

        private string FinalCheckText()
        {
            if (FinalCheck == null)
                return "";

            return $"\n  final check after map edits: portal lists that do not fit: {FinalCheck.PortalsUnfixed.Count}, cells with an environment or structure missing from the portal dat: {FinalCheck.MissingEnvironments.Count}"
                + string.Concat(FinalCheck.PortalsUnfixed.Select(l => $"\n    unfixed: {l}"))
                + string.Concat(FinalCheck.MissingEnvironments.Select(l => $"\n    missing: {l}"));
        }

        public string ToText()
        {
            return $"{Recipe.Name}: {OutputPath} ({OutputBytes:N0} bytes)\n"
                + $"  release {ReleaseNumber}, hash {ReleaseHash}\n"
                + $"  from {Recipe.RetailCellDat}, portal {Recipe.Portal.OutputFileName} ({Recipe.Ruleset}), template {Recipe.ToDCellDat}\n"
                + $"  result: {Audit.Records} records, {Audit.FreeBlocks} free blocks, {Audit.OrphanedBlocks} orphaned blocks, audit ok\n"
                + Conversion.ToText()
                + string.Concat(MapNotes.Select(n => $"\n  map note: {n}"))
                + FinalCheckText()
                + string.Concat(SourceReports.Select(r => $"\nDonor {r.Key} ({DatMapSources.Files[r.Key].File}):\n{r.Value.ToText()}"));
        }
    }

    /// <summary>
    /// Builds a cell dat in a temporary file and moves it into place after the audit passes. The source folder is only read.
    /// </summary>
    public static class DatCellBuildRunner
    {
        /// <exception cref="ArgumentException">An input is missing (including the ruleset's built portal dat), or the output would overwrite something it must not.</exception>
        public static DatCellBuildResult Build(DatCellBuildRecipe recipe, string sourceDats, string outputRoot, bool force = false, DatMapTrace trace = null)
        {
            var retail = Path.Combine(sourceDats, recipe.RetailCellDat);
            var template = Path.Combine(sourceDats, recipe.ToDCellDat);
            var portal = recipe.BuiltPortalPath(outputRoot);
            var additions = recipe.BuiltAdditionsPath(outputRoot);

            foreach (var input in new[] { retail, template })
                if (!File.Exists(input))
                    throw new ArgumentException($"Source dat not found: {input}");

            foreach (var input in recipe.IdentitySurfaces ? new[] { portal } : new[] { portal, additions })
                if (!File.Exists(input))
                    throw new ArgumentException($"{input} not found: build {recipe.Portal.Key} first.");

            var output = DatBuildOutput.Prepare(sourceDats, outputRoot, recipe.OutputFolder ?? recipe.Ruleset, recipe.OutputFileName, force);

            Dictionary<ushort, ushort> surfaceMap;

            if (recipe.IdentitySurfaces)
                surfaceMap = new DatDatabase(portal).AllFiles.Keys.Where(id => id >> 24 == 0x08).ToDictionary(id => (ushort)id, id => (ushort)id);
            else
            {
                surfaceMap = DatCellConverter.LoadEmbeddedSurfaceMap(recipe.SurfaceMap);

                foreach (var entry in DatCellConverter.LoadSurfaceMap(additions))
                    surfaceMap[entry.Key] = entry.Value;
            }

            var built = DatBuildOutput.BuildInTemp(output, temp =>
            {
                var result = new DatCellBuildResult { Recipe = recipe, OutputPath = output };

                var conversionTime = DatStepDate.Parse(recipe.ConversionDate);

                if (recipe.MapOperations == null)
                {
                    result.Conversion = DatCellConverter.Convert(retail, temp, surfaceMap, portal, conversionTime, recipe.Releases.IterationForDate);

                    using (var writer = new DatWriter(temp))
                    {
                        writer.SaveIterationRecord(recipe.Releases.CurrentNumber, conversionTime);
                        result.Audit = writer.Audit();
                    }
                }
                else
                {
                    result.Conversion = new DatCellConversionReport();
                    var store = new DatCellStore(DatCellConverter.ConvertToStore(retail, surfaceMap, result.Conversion, portal));
                    var sources = new DatMapSources(sourceDats, surfaceMap);

                    store.RequireDate = true;

                    var editor = new DatMapEditor(store);
                    trace?.Attach(store);
                    recipe.MapOperations(editor, sources);
                    result.MapTrace = trace?.ToText(store);
                    result.SourceReports = sources.Reports;
                    result.MapNotes = editor.Notes;

                    result.FinalCheck = new DatCellConversionReport();
                    DatCellConverter.CheckStore(store, portal, result.FinalCheck);

                    File.Delete(temp);
                    DatWriter.CreateEmpty(temp, DatContainerSpec.Cell);

                    using (var writer = new DatWriter(temp) { RecordDate = conversionTime, IterationForDate = recipe.Releases.IterationForDate })
                    {
                        foreach (var record in store.Records)
                            writer.Save(record.Key, record.Value, date: store.DateOf(record.Key));

                        writer.SaveIterationRecord(recipe.Releases.CurrentNumber, Math.Max(conversionTime, store.CurrentDate));
                        result.Audit = writer.Audit();
                    }
                }

                result.OutputBytes = new FileInfo(temp).Length;

                return result;
            });

            built.ReleaseNumber = recipe.Releases.CurrentNumber;
            built.ReleaseHash = DatReleaseHash.Compute(output);

            return built;
        }
    }
}
