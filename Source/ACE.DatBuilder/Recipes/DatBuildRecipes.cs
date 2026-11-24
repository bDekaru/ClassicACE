using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Records taken from a pre-ToD portal dat during a portal build: converted by <see cref="DatPortalImporter"/> and written before the overlay folders.
    /// </summary>
    public class DatPreToDImport
    {
        /// <summary>File name of the pre-ToD portal dat in the source dats folder.</summary>
        public string PreToDDat { get; }

        /// <summary>File name of the embedded pre-ToD to ToD surface map (Data folder).</summary>
        public string SurfaceMap { get; }

        /// <summary>Environments, models and setups to import, e.g. "0D000418-0D00041B,01001753". Imported in this order; surface ids handed out for new surfaces depend on it.</summary>
        public string Ids { get; }

        /// <summary>Surfaces to import although the map does not know them.</summary>
        public string Surfaces { get; }

        /// <summary>File name of the pre-ToD cell dat in the source dats folder, which says which environments each building uses.</summary>
        public string PreToDCellDat { get; }

        /// <summary>Building models to replace as a whole with the pre-ToD versions, e.g. "01002EF1,01002EF2": each brings its group (see <see cref="DatBuildingGroups"/>). Imported after the surfaces.</summary>
        public string Buildings { get; }

        /// <summary>The step date the imported records carry.</summary>
        public string Date { get; }

        public DatPreToDImport(string date, string preToDDat, string surfaceMap, string ids, string surfaces, string preToDCellDat = null, string buildings = null)
        {
            Date = date;
            PreToDDat = preToDDat;
            SurfaceMap = surfaceMap;
            Ids = ids;
            Surfaces = surfaces;
            PreToDCellDat = preToDCellDat;
            Buildings = buildings;
        }

        public DatPortalImporter Run(string sourceDats, string targetDat)
        {
            var importer = new DatPortalImporter(new DatDatabase(Path.Combine(sourceDats, PreToDDat), true), new DatDatabase(targetDat, true), DatCellConverter.LoadEmbeddedSurfaceMap(SurfaceMap));

            importer.ImportIds(Ids);
            importer.ImportSurfaces(Surfaces);

            if (!string.IsNullOrEmpty(Buildings))
            {
                var groups = new DatBuildingGroups(new DatDatabase(Path.Combine(sourceDats, PreToDCellDat), true));

                foreach (var model in DatPortalImporter.ParseIds(Buildings))
                    importer.ImportBuilding(model, groups);
            }

            return importer;
        }
    }

    /// <summary>One overlay folder (relative to the Custom Files root, written with '/') and the date the records it writes carry. The date is chosen by hand when the step is added and never changed afterwards.</summary>
    public class DatBuildStep
    {
        public string Folder { get; }
        public string Date { get; }
        public uint Time { get; }

        public DatBuildStep(string folder, string date)
        {
            Folder = folder;
            Date = date;
            Time = DatStepDate.Parse(date);
        }
    }

    /// <summary>
    /// One dat build: a base dat, optionally records imported from a pre-ToD dat, and the ordered list of overlay folders (relative to the Custom Files root) laid over it.
    /// Every step carries a date, in non-decreasing order; the dat's release table turns the dates into entry iterations.
    /// </summary>
    public class DatBuildRecipe
    {
        /// <summary>Set for portal recipes that need old content the EoR portal dat no longer has.</summary>
        public DatPreToDImport PreToDImport { get; private set; }

        public DatBuildRecipe WithPreToDImport(DatPreToDImport import)
        {
            PreToDImport = import;
            return this;
        }

        /// <summary>Set for portal recipes whose XP and skill tables come from the ruleset's dat_* database tables: the date the records written from them carry, after the folder steps.</summary>
        public string TablesDate { get; private set; }

        public uint TablesTime => DatStepDate.Parse(TablesDate);

        public DatBuildRecipe WithTables(string date)
        {
            TablesDate = date;
            return this;
        }

        /// <summary>Record steps (records made from other records by function calls), run after the pre-ToD import and before the overlay folders, in this order.</summary>
        public DatRecordStep[] RecordSteps { get; private set; } = new DatRecordStep[0];

        public DatBuildRecipe WithRecordSteps(params DatRecordStep[] steps)
        {
            RecordSteps = steps;
            return this;
        }

        /// <summary>Set for a recipe that starts from an empty dat made like the base dat (the base dat is then only the template), its records all coming from record steps.</summary>
        public bool EmptyBase { get; private set; }

        public DatBuildRecipe WithEmptyBase()
        {
            EmptyBase = true;
            return this;
        }

        /// <summary>Output subfolder when it is not the ruleset's name, so an alternative recipe of a ruleset does not overwrite the ruleset's dat.</summary>
        public string OutputFolder { get; private set; }

        public DatBuildRecipe WithOutputFolder(string folder)
        {
            OutputFolder = folder;
            return this;
        }

        public string Name { get; }

        /// <summary>True for a language dat recipe; its tables step writes the title strings instead of the portal tables.</summary>
        public bool IsLanguage => BaseDat == DatBuildRecipes.LanguageBaseDat;

        /// <summary>File name of the base dat in the source dats folder.</summary>
        public string BaseDat { get; }

        /// <summary>The overlay steps in application order.</summary>
        public DatBuildStep[] Steps { get; }

        /// <summary>The releases of the dat this recipe builds.</summary>
        public DatReleaseSet Releases { get; }

        public Func<uint, bool> AcceptId { get; }

        /// <summary>Output subfolder name, e.g. CustomDM or Infiltration.</summary>
        public string Ruleset { get; }

        /// <summary>The built dat's file name: client_local_English.dat for a language recipe, client_portal.dat for a portal one (the source dats are named <kind> - <date> - Iteration N - X% - notes).</summary>
        public string OutputFileName => IsLanguage ? "client_local_English.dat" : "client_portal.dat";

        /// <summary>Recipe name as typed on the command line, e.g. portal-customdm.</summary>
        public string Key => Name.Replace(' ', '-').ToLowerInvariant();

        public DatBuildRecipe(string name, string ruleset, string baseDat, Func<uint, bool> acceptId, DatReleaseSet releases, params DatBuildStep[] steps)
        {
            Name = name;
            Ruleset = ruleset;
            BaseDat = baseDat;
            AcceptId = acceptId;
            Releases = releases;
            Steps = steps;
        }

        /// <summary>The date of the last step.</summary>
        public uint LastStepTime
        {
            get
            {
                var last = Releases.StartTime;

                if (PreToDImport != null)
                    last = Math.Max(last, DatStepDate.Parse(PreToDImport.Date));

                foreach (var step in RecordSteps)
                    last = Math.Max(last, step.Time);

                foreach (var step in Steps)
                    last = Math.Max(last, step.Time);

                return TablesDate != null ? Math.Max(last, TablesTime) : last;
            }
        }

        /// <summary>Checks the step dates: none before the release table's start date, and never earlier than the step before.</summary>
        /// <exception cref="InvalidDataException">A date breaks the order.</exception>
        public void ValidateDates()
        {
            var previous = Releases.StartTime;

            if (PreToDImport != null)
            {
                var importTime = DatStepDate.Parse(PreToDImport.Date);

                if (importTime < previous)
                    throw new InvalidDataException($"{Name}: the import step is dated {PreToDImport.Date}, before {Releases.StartDate}.");

                previous = importTime;
            }

            foreach (var step in RecordSteps)
            {
                if (step.Time < previous)
                    throw new InvalidDataException($"{Name}: record step '{step.Name}' is dated {step.Date}, earlier than the step before it (steps run in date order, none before {Releases.StartDate}).");

                previous = step.Time;
            }

            foreach (var step in Steps)
            {
                if (step.Time < previous)
                    throw new InvalidDataException($"{Name}: step '{step.Folder}' is dated {step.Date}, earlier than the step before it (steps run in date order, none before {Releases.StartDate}).");

                previous = step.Time;
            }

            if (TablesDate != null && TablesTime < previous)
                throw new InvalidDataException($"{Name}: the dat tables step is dated {TablesDate}, earlier than the step before it (steps run in date order, none before {Releases.StartDate}).");
        }

        public string[] ResolveFolders(string customFilesRoot)
        {
            return Steps.Select(s => Path.Combine(customFilesRoot, s.Folder.Replace('/', Path.DirectorySeparatorChar))).ToArray();
        }

        public DatOverlayReport Apply(DatWriter writer, string customFilesRoot, IEnumerable<KeyValuePair<uint, byte[]>> generated = null)
        {
            var folders = ResolveFolders(customFilesRoot);

            return DatOverlay.Apply(writer, Steps.Select((s, i) => (folders[i], s.Time)), AcceptId, generated, PreToDImport != null ? DatStepDate.Parse(PreToDImport.Date) : 0);
        }
    }

    /// <summary>
    /// The overlay steps of Melt's Language and Portal builders (Program.cs: LanguageManipulationForCustomDM, LanguageManipulationForInfiltration,
    /// PortalManipulationForCustomDM, PortalManipulationForInfiltration), with the same folders in the same order.
    /// Folders Melt has commented out are deliberately absent.
    /// </summary>
    public static class DatBuildRecipes
    {
        // Source dats used by the recipes (MD5 of the file as checked 2026-11-17).Donor cell dats are listed in DatMapSources.
        // LanguageBaseDat: MD5 b55f7465dbf4fa94fc919d51afcb70da
        // PortalBaseDat:   MD5 2c89662a44fcdc2a3c31fe8f6677d265
        // PreToDPortalDat: MD5 304275cb743dc28f37410e06e91cb17c
        // Portal2001Dat:   MD5 c1438f6802b4d15c9d3f003f46339c41 (CRC32 2B64BF52, the PORTAL.DAT of the archive's 2001-09-12 snapshot)
        // Portal2000Dat:   MD5 29cffe605048756ba57868d343b08932 (the 2000-12-12 portal.dat)
        // PreToDCellDat:   MD5 0f32588ffbf648cd0695a4dbd6488205
        // ToDCellDat:      MD5 c1521ecd4dab4123df5467090742a45b
        public const string LanguageBaseDat = "language - 2015-06-18 - Iteration 994 - EoR.dat";
        public const string PortalBaseDat = "portal - 2017-01-29 - Iteration 2072 - EoR.dat";

        public const string PreToDPortalDat = "portal - 2005-02-XX - Iteration 2112 - Infiltration.dat";
        public const string Portal2001Dat = "portal - 2001-09-12 - Iteration 541.dat";
        public const string Portal2000Dat = "portal - 2000-12-12 - Iteration 382.dat";
        public const string PreToDSurfaceMap = "surface_map_preToD_2005-02_to_ToD.tsv";

        // Environments and models of the landblocks whose content was removed in ToD (2459 2577 2677 287F), the EC0E objects, and the old models
        // CustomDM also gets from Melt's Missing Files; surfaces that the cell conversion reports as unmapped.
        // 0D0003DA is the environment of two cells of landblock EBF7 (CustomDM, from the older-era dats), which ToD removed.
        private const string RemovedContentIds = "0D000418-0D00041B,01001753-01001756,0100353E-0100353F,010015F3-010015F4,0100281E,020001DC,0D0003DA";
        private const string UnmappedSurfaces = "080015B0,080015B1,080015E2,08001E7A";

        // Buildings whose exterior model or environments were reshaped between 2005 and EoR, so the 2005 cells no longer fit the EoR records. Each is replaced with its whole group (models and environments).
        // 01002EF1/2: Candeth Keep and Renegade Fortress (0574, 0575), 010034FA: the EC0E chapel (05BF), 01000830: the 02FB buildings (the 2005 look), 01002EF6: exterior only.
        private const string ReplacedBuildings = "01000830,01002EF1,01002EF2,01002EF6,010034FA";

        // Every portal recipe imports the same set, so the rulesets get identical converted records and the cell conversion can use either portal.
        private static DatPreToDImport RemovedContent()
        {
            return new DatPreToDImport(InitialVersionDate, PreToDPortalDat, PreToDSurfaceMap, RemovedContentIds, UnmappedSurfaces, PreToDCellDat, ReplacedBuildings);
        }

        // Every step is dated by hand when it is added and the date is never changed afterwards (see DatReleases). All steps below make up the initial version.
        public const string InitialVersionDate = "2026-11-16";

        private static DatBuildStep Initial(string folder)
        {
            return new DatBuildStep(folder, InitialVersionDate);
        }

        public static readonly DatBuildRecipe LanguageCustomDM = new DatBuildRecipe("Language CustomDM", "CustomDM", LanguageBaseDat, DatOverlay.IsLanguageId, DatReleases.CustomDMLanguage)
            .WithRecordSteps(LanguageRecords.CustomDM(InitialVersionDate))
            .WithTables(InitialVersionDate);

        public static readonly DatBuildRecipe LanguageInfiltration = new DatBuildRecipe("Language Infiltration", "Infiltration", LanguageBaseDat, DatOverlay.IsLanguageId, DatReleases.InfiltrationLanguage)
            .WithRecordSteps(LanguageRecords.Infiltration(InitialVersionDate));

        /// <summary>Deprecated (2026-11-24): the portal built from the EoR dat. <see cref="PortalCustomDM2005"/> is the one in use.</summary>
        public static readonly DatBuildRecipe PortalCustomDM = new DatBuildRecipe("Portal CustomDM", "CustomDM", PortalBaseDat, DatOverlay.IsPortalId, DatReleases.CustomDMPortal,
            Initial("Portal/CustomDM"))
            .WithPreToDImport(RemovedContent())
            .WithRecordSteps(CustomDMRecords.All(InitialVersionDate).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(SurfaceTextureRecords.Step("Ground surface textures", InitialVersionDate, SurfaceTextureRecords.GroundFile)).Append(SurfaceTextureRecords.Step("Blend surface textures", InitialVersionDate, SurfaceTextureRecords.BlendFile)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(SurfaceTextureRecords.Step("Rock surface textures", InitialVersionDate, SurfaceTextureRecords.RockFile)).Append(ModelRecords.Trees(InitialVersionDate)).Append(IconRecords.Step(InitialVersionDate)).Concat(RegionRecords.All(InitialVersionDate)).Append(MoonRecords.Overlay(InitialVersionDate)).Append(RemovedRecords.Step(InitialVersionDate)).ToArray())
            .WithTables(InitialVersionDate);

        /// <summary>Deprecated (2026-11-24): the portal built from the EoR dat. <see cref="PortalInfiltration2005"/> is the one in use.</summary>
        public static readonly DatBuildRecipe PortalInfiltration = new DatBuildRecipe("Portal Infiltration", "Infiltration", PortalBaseDat, DatOverlay.IsPortalId, DatReleases.InfiltrationPortal,
            Initial("Portal/Infiltration"))
            .WithPreToDImport(RemovedContent())
            .WithRecordSteps(InfiltrationRecords.All(InitialVersionDate).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(SurfaceTextureRecords.Step("Ground surface textures", InitialVersionDate, SurfaceTextureRecords.GroundFile)).Append(SurfaceTextureRecords.Step("Blend surface textures", InitialVersionDate, SurfaceTextureRecords.BlendFile)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(ModelRecords.Trees(InitialVersionDate)).Append(RemovedRecords.Step(InitialVersionDate)).ToArray());

        public static readonly DatBuildRecipe[] All = { LanguageCustomDM, LanguageInfiltration, PortalCustomDM, PortalInfiltration };

        /// <summary>The Infiltration portal that starts from the 2005 dat, the one in use; it is built only when asked for by name (<see cref="Alternatives"/>) until <see cref="All"/> and the cell recipes are switched from the deprecated <see cref="PortalInfiltration"/>.</summary>
        public static readonly DatBuildRecipe PortalInfiltration2005 = new DatBuildRecipe("Portal Infiltration 2005", "Infiltration", PortalBaseDat, DatOverlay.IsPortalId, DatReleases.InfiltrationPortal,
            Initial("Portal/Infiltration"))
            .WithEmptyBase()
            .WithOutputFolder("Infiltration2005")
            .WithRecordSteps(PreToDBaseRecords.All(InitialVersionDate).Concat(InfiltrationRecords.Tables(InitialVersionDate)).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(GroundTextureRecords.SurfaceTextureStep(InitialVersionDate)).Append(SurfaceTextureRecords.Step("Blend surface textures", InitialVersionDate, SurfaceTextureRecords.BlendFile)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(IconRecords.EoRStep(InitialVersionDate)).ToArray());

        /// <summary>The CustomDM counterpart of <see cref="PortalInfiltration2005"/>: the 2005 look everywhere, with only the CustomDM additions that have no 2005 record laid over it.</summary>
        public static readonly DatBuildRecipe PortalCustomDM2005 = new DatBuildRecipe("Portal CustomDM 2005", "CustomDM", PortalBaseDat, DatOverlay.IsPortalId, DatReleases.CustomDMPortal,
            Initial("Portal/CustomDM"))
            .WithEmptyBase()
            .WithOutputFolder("CustomDM2005")
            .WithRecordSteps(PreToDBaseRecords.All(InitialVersionDate, CustomDMRecords.EoRRecordsFile, CustomDMRecords.MapEoRRecordsFile).Append(InfiltrationRecords.ChargenFromEoR(InitialVersionDate, CustomDMRecords.ChargenEditsFile)).Concat(CustomDMRecords.AdditionsTo2005(InitialVersionDate)).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(GroundTextureRecords.SurfaceTextureStep(InitialVersionDate)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(IconRecords.Step(InitialVersionDate)).Concat(RegionRecords.All(InitialVersionDate)).Append(IconRecords.EoRStep(InitialVersionDate)).Append(SeasonalRecords.Step("Summer", InitialVersionDate)).ToArray())
            .WithTables(InitialVersionDate);

        public static readonly DatBuildRecipe[] Alternatives = { PortalInfiltration2005, PortalCustomDM2005 };

        public const string PreToDCellDat = "cell - 2005-02-XX - Iteration 1593 - 100% - Admin.dat";
        public const string ToDCellDat = "cell - 2005-06-02 - Iteration 4 - 100% - Throne of Destiny.dat";

        public static readonly DatCellBuildRecipe CellCustomDM = new DatCellBuildRecipe("Cell CustomDM", "CustomDM", PreToDCellDat, ToDCellDat, PortalCustomDM, PreToDSurfaceMap, DatReleases.CustomDMCell, InitialVersionDate, (map, sources) => DatMapRecipes.CustomDM(map, sources));

        public static readonly DatCellBuildRecipe CellInfiltration = new DatCellBuildRecipe("Cell Infiltration", "Infiltration", PreToDCellDat, ToDCellDat, PortalInfiltration, PreToDSurfaceMap, DatReleases.InfiltrationCell, InitialVersionDate);

        public static readonly DatCellBuildRecipe CellInfiltration2005 = new DatCellBuildRecipe("Cell Infiltration 2005", "Infiltration", PreToDCellDat, ToDCellDat, PortalInfiltration2005, PreToDSurfaceMap, DatReleases.InfiltrationCell, InitialVersionDate)
            .WithIdentitySurfaces()
            .WithOutputFolder("Infiltration2005");

        public static readonly DatCellBuildRecipe CellCustomDM2005 = new DatCellBuildRecipe("Cell CustomDM 2005", "CustomDM", PreToDCellDat, ToDCellDat, PortalCustomDM2005, PreToDSurfaceMap, DatReleases.CustomDMCell, InitialVersionDate, (map, sources) => DatMapRecipes.CustomDM(map, sources, true))
            .WithIdentitySurfaces()
            .WithOutputFolder("CustomDM2005");

        public static readonly DatCellBuildRecipe[] AlternativeCells = { CellInfiltration2005, CellCustomDM2005 };

        /// <summary>Cell recipes need their ruleset's portal dat built first.</summary>
        public static readonly DatCellBuildRecipe[] AllCells = { CellCustomDM, CellInfiltration };

        private static bool KeyMatches(string key, string selector) => key.Equals(selector, StringComparison.OrdinalIgnoreCase);

        /// <summary>The recipe with that key (a portal or language recipe, not an alternative), or null.</summary>
        public static DatBuildRecipe Find(string key)
        {
            return All.FirstOrDefault(r => KeyMatches(r.Key, key));
        }

        /// <summary>The recipes a build selector names: <c>all</c> for the default ones, or one key, which can be an alternative.</summary>
        /// <exception cref="ArgumentException">The selector names no recipe.</exception>
        public static (List<DatBuildRecipe> Portals, List<DatCellBuildRecipe> Cells) Select(string selector)
        {
            var all = selector.Equals("all", StringComparison.OrdinalIgnoreCase);
            var portals = All.Where(r => all || KeyMatches(r.Key, selector)).Concat(Alternatives.Where(r => KeyMatches(r.Key, selector))).ToList();
            var cells = AllCells.Where(r => all || KeyMatches(r.Key, selector)).Concat(AlternativeCells.Where(r => KeyMatches(r.Key, selector))).ToList();

            if (portals.Count == 0 && cells.Count == 0)
                throw new ArgumentException($"Unknown recipe '{selector}' (see: list).");

            return (portals, cells);
        }

        private static readonly Dictionary<string, Func<string, DatRecordStep[]>> EventKits = new Dictionary<string, Func<string, DatRecordStep[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["daralet"] = DaraletRecords.All,
            ["darkness"] = DarknessAscendantRecords.All,
            ["fireinthesky"] = FireInTheSkyRecords.All
        };

        /// <summary>Adds the steps of an event kit, dated <paramref name="date"/>, to the recipes that start from the 2005 dat.</summary>
        /// <exception cref="ArgumentException">The event is unknown, the date is missing, or no recipe starts from the 2005 dat.</exception>
        public static void AddEvent(IEnumerable<DatBuildRecipe> recipes, string eventName, string date)
        {
            if (date == null)
                throw new ArgumentException("--event needs --event-date <yyyy-mm-dd>, the date the step is added: every step carries a date.");

            if (!EventKits.TryGetValue(eventName, out var kit))
                throw new ArgumentException($"No event '{eventName}'; the events are: {string.Join(", ", EventKits.Keys)}.");

            var targets = recipes.Where(r => r.EmptyBase).ToList();

            if (targets.Count == 0)
                throw new ArgumentException("--event applies to the recipes that start from the 2005 dat (see: list).");

            foreach (var recipe in targets)
                recipe.WithRecordSteps(recipe.RecordSteps.Concat(kit(date)).ToArray());
        }

        /// <summary>Adds the season step, dated <paramref name="date"/>, to the recipes that start from the 2005 dat.</summary>
        /// <exception cref="ArgumentException">The date is missing, or no recipe starts from the 2005 dat.</exception>
        public static void AddSeason(IEnumerable<DatBuildRecipe> recipes, string season, string date)
        {
            if (date == null)
                throw new ArgumentException("--season needs --season-date <yyyy-mm-dd>, the date of the event: every step carries a date.");

            var targets = recipes.Where(r => r.EmptyBase).ToList();

            if (targets.Count == 0)
                throw new ArgumentException("--season applies to the recipes that start from the 2005 dat (see: list).");

            foreach (var recipe in targets)
                recipe.WithRecordSteps(recipe.RecordSteps.Append(SeasonalRecords.Step(season, date)).ToArray());
        }

        /// <summary>Removes the record steps of those names from the recipes.</summary>
        /// <exception cref="ArgumentException">A name is no record step of any of the recipes.</exception>
        public static void SkipSteps(List<DatBuildRecipe> recipes, IReadOnlyList<string> skipped)
        {
            foreach (var name in skipped.Where(n => !recipes.Any(r => r.RecordSteps.Any(s => s.Name == n))))
                throw new ArgumentException($"No record step '{name}' in the selected recipes (see: list).");

            foreach (var recipe in recipes)
                recipe.WithRecordSteps(recipe.RecordSteps.Where(s => !skipped.Contains(s.Name)).ToArray());
        }
    }
}
