using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// One dat build: a base dat and the ordered list of record steps run over it.
    /// Every step carries a date, in non-decreasing order; the dat's release table turns the dates into entry iterations.
    /// </summary>
    public class DatBuildRecipe
    {
        /// <summary>Set for portal recipes whose XP and skill tables come from the ruleset's dat_* database tables: the date the records written from them carry, after the record steps.</summary>
        public string TablesDate { get; private set; }

        public uint TablesTime => DatStepDate.Parse(TablesDate);

        public DatBuildRecipe WithTables(string date)
        {
            TablesDate = date;
            return this;
        }

        /// <summary>Record steps (records made from other records by function calls, or from the data files), run in this order.</summary>
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

        public string Name { get; }

        /// <summary>True for a language dat recipe; its tables step writes the title strings instead of the portal tables.</summary>
        public bool IsLanguage => BaseDat == DatBuildRecipes.LanguageBaseDat;

        /// <summary>File name of the base dat in the source dats folder.</summary>
        public string BaseDat { get; }

        /// <summary>The releases of the dat this recipe builds.</summary>
        public DatReleaseSet Releases { get; }

        /// <summary>Output subfolder name, e.g. CustomDM or Infiltration.</summary>
        public string Ruleset { get; }

        /// <summary>The built dat's file name: client_local_English.dat for a language recipe, client_portal.dat for a portal one (the source dats are named <kind> - <date> - Iteration N - X% - notes).</summary>
        public string OutputFileName => IsLanguage ? "client_local_English.dat" : "client_portal.dat";

        /// <summary>Recipe name as typed on the command line, e.g. portal-customdm.</summary>
        public string Key => Name.Replace(' ', '-').ToLowerInvariant();

        public DatBuildRecipe(string name, string ruleset, string baseDat, DatReleaseSet releases)
        {
            Name = name;
            Ruleset = ruleset;
            BaseDat = baseDat;
            Releases = releases;
        }

        /// <summary>The date of the last step.</summary>
        public uint LastStepTime
        {
            get
            {
                var last = Releases.StartTime;

                foreach (var step in RecordSteps)
                    last = Math.Max(last, step.Time);

                return TablesDate != null ? Math.Max(last, TablesTime) : last;
            }
        }

        /// <summary>Checks the step dates: none before the release table's start date, and never earlier than the step before.</summary>
        /// <exception cref="InvalidDataException">A date breaks the order.</exception>
        public void ValidateDates()
        {
            var previous = Releases.StartTime;

            foreach (var step in RecordSteps)
            {
                if (step.Time < previous)
                    throw new InvalidDataException($"{Name}: record step '{step.Name}' is dated {step.Date}, earlier than the step before it (steps run in date order, none before {Releases.StartDate}).");

                previous = step.Time;
            }

            if (TablesDate != null && TablesTime < previous)
                throw new InvalidDataException($"{Name}: the dat tables step is dated {TablesDate}, earlier than the step before it (steps run in date order, none before {Releases.StartDate}).");
        }
    }

    /// <summary>The Language and Portal recipes of each ruleset, which replace Melt's builders (Program.cs: LanguageManipulationForCustomDM, LanguageManipulationForInfiltration, PortalManipulationForCustomDM, PortalManipulationForInfiltration).</summary>
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

        // Every step is dated by hand when it is added and the date is never changed afterwards (see DatReleases). All steps below make up the initial version.
        public const string InitialVersionDate = "2026-11-16";

        public static readonly DatBuildRecipe LanguageCustomDM = new DatBuildRecipe("Language CustomDM", "CustomDM", LanguageBaseDat, DatReleases.CustomDMLanguage)
            .WithRecordSteps(LanguageRecords.CustomDM(InitialVersionDate))
            .WithTables(InitialVersionDate);

        public static readonly DatBuildRecipe LanguageInfiltration = new DatBuildRecipe("Language Infiltration", "Infiltration", LanguageBaseDat, DatReleases.InfiltrationLanguage)
            .WithRecordSteps(LanguageRecords.Infiltration(InitialVersionDate));

        /// <summary>The Infiltration portal: the 2005 dat with the Infiltration tables.</summary>
        public static readonly DatBuildRecipe PortalInfiltration = new DatBuildRecipe("Portal Infiltration", "Infiltration", PortalBaseDat, DatReleases.InfiltrationPortal)
            .WithEmptyBase()
            .WithRecordSteps(PreToDBaseRecords.All(InitialVersionDate).Concat(InfiltrationRecords.Tables(InitialVersionDate)).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(GroundTextureRecords.SurfaceTextureStep(InitialVersionDate)).Append(SurfaceTextureRecords.Step("Blend surface textures", InitialVersionDate, SurfaceTextureRecords.BlendFile)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(IconRecords.EoRStep(InitialVersionDate)).Append(CustomImageRecords.Step("Infiltration", InitialVersionDate)).ToArray());

        /// <summary>The CustomDM counterpart of <see cref="PortalInfiltration"/>: the 2005 look everywhere, with only the CustomDM additions that have no 2005 record laid over it.</summary>
        public static readonly DatBuildRecipe PortalCustomDM = new DatBuildRecipe("Portal CustomDM", "CustomDM", PortalBaseDat, DatReleases.CustomDMPortal)
            .WithEmptyBase()
            .WithRecordSteps(PreToDBaseRecords.All(InitialVersionDate, CustomDMRecords.EoRRecordsFile, CustomDMRecords.MapEoRRecordsFile).Append(InfiltrationRecords.ChargenFromEoR(InitialVersionDate, CustomDMRecords.ChargenEditsFile)).Concat(CustomDMRecords.AdditionsTo2005(InitialVersionDate)).Append(GroundTextureRecords.Step(InitialVersionDate)).Append(GroundTextureRecords.SurfaceTextureStep(InitialVersionDate)).Append(GroundTextureRecords.RegionStep(InitialVersionDate)).Append(IconRecords.Step(InitialVersionDate)).Concat(RegionRecords.All(InitialVersionDate)).Append(IconRecords.EoRStep(InitialVersionDate)).Append(SeasonalRecords.Step("Summer", InitialVersionDate)).Append(CustomImageRecords.Step("CustomDM", InitialVersionDate)).ToArray())
            .WithTables(InitialVersionDate);

        public static readonly DatBuildRecipe[] All = { LanguageCustomDM, LanguageInfiltration, PortalCustomDM, PortalInfiltration };

        public const string PreToDCellDat = "cell - 2005-02-XX - Iteration 1593 - 100% - Admin.dat";
        public const string ToDCellDat = "cell - 2005-06-02 - Iteration 4 - 100% - Throne of Destiny.dat";

        public static readonly DatCellBuildRecipe CellInfiltration = new DatCellBuildRecipe("Cell Infiltration", "Infiltration", PreToDCellDat, ToDCellDat, PortalInfiltration, DatReleases.InfiltrationCell, InitialVersionDate);

        public static readonly DatCellBuildRecipe CellCustomDM = new DatCellBuildRecipe("Cell CustomDM", "CustomDM", PreToDCellDat, ToDCellDat, PortalCustomDM, DatReleases.CustomDMCell, InitialVersionDate, (map, sources) => DatMapRecipes.CustomDM(map, sources, true));

        /// <summary>Cell recipes need their ruleset's portal dat built first.</summary>
        public static readonly DatCellBuildRecipe[] AllCells = { CellCustomDM, CellInfiltration };

        private static bool KeyMatches(string key, string selector) => key.Equals(selector, StringComparison.OrdinalIgnoreCase);

        /// <summary>The recipe with that key (a portal or language recipe), or null.</summary>
        public static DatBuildRecipe Find(string key)
        {
            return All.FirstOrDefault(r => KeyMatches(r.Key, key));
        }

        /// <summary>The recipes a build selector names: <c>all</c> for the default ones, or one key, which can be an alternative.</summary>
        /// <exception cref="ArgumentException">The selector names no recipe.</exception>
        public static (List<DatBuildRecipe> Portals, List<DatCellBuildRecipe> Cells) Select(string selector)
        {
            var all = selector.Equals("all", StringComparison.OrdinalIgnoreCase);
            var portals = All.Where(r => all || KeyMatches(r.Key, selector)).ToList();
            var cells = AllCells.Where(r => all || KeyMatches(r.Key, selector)).ToList();

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
