using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    public static class Program
    {
        private const int Ok = 0;
        private const int UsageError = 1;
        private const int AuditFailure = 2;

        private const string Usage =
@"ACE.DatBuilder build <recipe|all> [--source <folder>] [--custom <folder>] [--out <folder>] [--force] [--trace-map] [--skip-step <name>]... [--config <Config.js>] [--world-db <name>]
ACE.DatBuilder apply-tables <portal or language recipe> --config <Config.js> [--world-db <name>] [--dat <built portal dat> | --out <folder> | --source <folder>]
ACE.DatBuilder export-tables --dat <portal dat> [--language <language dat>] --out <sql file>
ACE.DatBuilder list
ACE.DatBuilder audit <dat>
ACE.DatBuilder compare-cell <first cell dat> <second cell dat> [landblocks to list]
ACE.DatBuilder compare-portal <first portal dat> <second portal dat>
ACE.DatBuilder check-references <portal dat> [--cell <cell dat>] [--highres <high-res dat>] [--language <language dat>] [--config <Config.js> [--world-db <name>]] [--listed <recipe data file>]...
ACE.DatBuilder convert-portal --from <pre-ToD portal dat> --target <ToD/EoR portal dat> --map <surface map tsv> --out <folder> [--ids <id,first-last,...>] [--surfaces <surface ids>]
ACE.DatBuilder convert-cell --from <pre-ToD cell dat> --map <surface map tsv> --out <new dat> [--portal <target portal dat>]

  A cell recipe needs its ruleset's portal built first (in the same --out); 'all' builds the portals before the cells.

  --source  folder with the original dats (default: the dat_source_folder setting of the --config server)
  --custom  folder with the custom files (default: <source>/Custom Files)
  --out     output root; each recipe writes to <out>/<ruleset>/<name>.dat (default: <source>/../Build)
  --force   replace an existing output dat
  --skip-step  build without the record step of that name (repeatable; the names are listed by 'list'), to find out what the step contributes: build into another --out and run compare-portal against the full build
  check-references reports the ids that the portal dat, the cell dat and the world database refer to and nothing provides, the portal records that none of them reaches by type, and which entries of the --listed id lists (for example customdm-eor-records.json) are not reachable and so unneeded.
  --trace-map  for a cell recipe with map edits: also report, per recipe source line, the records it changed and the ones it still owns at the end (0 = overridden, reverted or without effect), and write <name>.trace.txt
  --config  a server Config.js: its MySql.World connection is used to read the dat_* tables (XP, skill, vital, character generation and spell tables) of a recipe that has them; without it the records from the custom files are kept
  --world-db  the world database to read instead of the one named in the Config.js
  --event  for the recipes that start from the 2005 dat: add the event kit (daralet: the Child of Daralet records, red water golem and Region; darkness: Darkness Ascendant, the winged figure, red sun, sulfur clouds and swollen moons; fireinthesky: Fire in the Sky, the comet, orange clouds, red dome and short fog in a single forced day group) dated --event-date <yyyy-mm-dd>, the date the step is added (steps run in date order)
  --season  for the recipes that start from the 2005 dat: add the season step, foliage colors, ground and scenery (Fall or Autumn, Spring or Summer, Winter onset, Winter, Winter thaw, Early Spring; the 2005 dat is Fall; quote names with a space) dated --season-date <yyyy-mm-dd>, the date of the event

  apply-tables rewrites only the XP, skill, vital, character generation, spell and title records of an already built portal or language dat from the database (the quick loop after editing the dat_* tables).
  export-tables writes the SQL that fills the dat_* tables with the values of a portal dat's XP, skill, vital, character generation and spell records (with --language, also the character titles of the portal's title enum mapper and the language dat's title strings) (the tables themselves come from the world base script).

The source folder is only read. Each build is made in a temporary file and moved into place after both audits pass.";

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine(Usage);
                    return UsageError;
                }

                switch (args[0].ToLowerInvariant())
                {
                    case "list": return List();
                    case "audit": return Audit(args);
                    case "compare-cell": return CompareCell(args);
                    case "compare-portal": return ComparePortal(args);
                    case "check-references": return CheckReferences(args);
                    case "convert-cell": return ConvertCell(args);
                    case "convert-portal": return ConvertPortal(args);
                    case "build": return Build(args);
                    case "apply-tables": return ApplyTables(args);
                    case "export-tables": return ExportTables(args);

                    default:
                        Console.WriteLine(Usage);
                        return UsageError;
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return UsageError;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"Audit failed: {ex.Message}");
                return AuditFailure;
            }
        }

        private static int List()
        {
            foreach (var recipe in DatBuildRecipes.All.Concat(DatBuildRecipes.Alternatives))
            {
                Console.WriteLine($"{recipe.Key,-24} {recipe.BaseDat} + {recipe.Folders.Length} folder(s) -> {recipe.OutputFolder ?? recipe.Ruleset}/{recipe.OutputFileName}");

                foreach (var step in recipe.RecordSteps)
                    Console.WriteLine($"    record step: {step.Name}");
            }

            foreach (var recipe in DatBuildRecipes.AllCells)
                Console.WriteLine($"{recipe.Key,-24} {recipe.RetailCellDat} converted against {recipe.Portal.Key} -> {recipe.Ruleset}/{recipe.OutputFileName}");

            return Ok;
        }

        private static int Audit(string[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("audit takes one dat path.");

            RequireFile(args[1], "Dat");

            using (var writer = new DatWriter(args[1]))
            {
                var audit = writer.Audit();
                Console.WriteLine($"{args[1]}: {audit.Records} records, {audit.FreeBlocks} free blocks, {audit.OrphanedBlocks} orphaned blocks, audit ok");
            }

            return Ok;
        }

        private static string RecordKind(uint id)
        {
            switch (id & 0xFFFF)
            {
                case 0xFFFF: return "landblock";
                case 0xFFFE: return "landblock info";
                default: return (id >> 16) == 0xFFFF ? "other" : "cell";
            }
        }

        private sealed class KindCounts
        {
            public int Identical;
            public int Different;
            public int OnlyInFirst;
            public int OnlyInSecond;
        }

        private static int CompareCell(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
                throw new ArgumentException("compare-cell takes two dat paths and an optional number of differing landblocks to list.");

            RequireFile(args[1], "Dat");
            RequireFile(args[2], "Dat");

            var listCount = 20;

            if (args.Length == 4 && !int.TryParse(args[3], out listCount))
                throw new ArgumentException($"Not a number of landblocks: {args[3]}");

            var changesByLandblock = new Dictionary<uint, List<string>>();

            CompareRecords(new DatDatabase(args[1], true), new DatDatabase(args[2], true), RecordKind, (id, change) =>
            {
                var landblock = id & 0xFFFF0000;

                if (!changesByLandblock.TryGetValue(landblock, out var changes))
                    changesByLandblock[landblock] = changes = new List<string>();

                changes.Add($"{id:X8} {change}");
            });

            Console.WriteLine($"{changesByLandblock.Count} landblock(s) with at least one difference");

            foreach (var entry in changesByLandblock.Take(listCount))
                Console.WriteLine($"  {entry.Key >> 16:X4}: {entry.Value.Count} record(s), e.g. {string.Join("; ", entry.Value.Take(3))}");

            return Ok;
        }

        /// <summary>Compares the record sets of two dats by id and content, prints identical/different/only-in-one counts per kind and calls onDifference for every record that is not identical.</summary>
        private static void CompareRecords(DatDatabase first, DatDatabase second, Func<uint, string> kindOf, Action<uint, string> onDifference)
        {
            var countsByKind = new SortedDictionary<string, KindCounts>();

            foreach (var id in first.AllFiles.Keys.Union(second.AllFiles.Keys).OrderBy(k => k))
            {
                var kind = kindOf(id);

                if (!countsByKind.TryGetValue(kind, out var counts))
                    countsByKind[kind] = counts = new KindCounts();

                var inFirst = first.AllFiles.ContainsKey(id);
                var inSecond = second.AllFiles.ContainsKey(id);
                string change;

                if (inFirst && inSecond)
                {
                    if (first.GetReaderForFile(id).Buffer.SequenceEqual(second.GetReaderForFile(id).Buffer))
                    {
                        counts.Identical++;
                        continue;
                    }

                    counts.Different++;
                    change = "differs";
                }
                else if (inFirst)
                {
                    counts.OnlyInFirst++;
                    change = "only in first";
                }
                else
                {
                    counts.OnlyInSecond++;
                    change = "only in second";
                }

                onDifference(id, change);
            }

            foreach (var entry in countsByKind)
                Console.WriteLine($"{entry.Key,-15} identical {entry.Value.Identical}, different {entry.Value.Different}, only in first {entry.Value.OnlyInFirst}, only in second {entry.Value.OnlyInSecond}");
        }

        private static int CheckReferences(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--cell", "--highres", "--language", "--config", "--world-db", "--listed" }, allowPositional: true);

            var portal = line.Positional ?? throw new ArgumentException("check-references needs a portal dat.");
            var config = line.Value("--config");

            if (line.Value("--world-db") != null && config == null)
                throw new ArgumentException("--world-db needs --config <Config.js>.");

            foreach (var path in new[] { portal, line.Value("--cell"), line.Value("--highres"), line.Value("--language") }.Where(p => p != null))
                RequireFile(path, "Dat");

            var worldIds = config != null ? new WorldDatTableSource(config, line.Value("--world-db")).ReadDatIds() : null;
            var listed = line.Values("--listed").ToDictionary(name => name, name => DatRecipeData.Load<List<string>>(name).Select(id => Convert.ToUInt32(id, 16)).ToList());

            Console.Write(DatReferenceChecker.Run(portal, line.Value("--cell"), line.Value("--highres"), line.Value("--language"), worldIds, listed));

            return Ok;
        }

        private static int ComparePortal(string[] args)
        {
            if (args.Length != 3)
                throw new ArgumentException("compare-portal takes two dat paths.");

            RequireFile(args[1], "Dat");
            RequireFile(args[2], "Dat");

            var differences = new List<string>();

            CompareRecords(new DatDatabase(args[1], true), new DatDatabase(args[2], true), id => $"type {id >> 24:X2}", (id, change) => differences.Add($"{id:X8} {change}"));

            Console.WriteLine($"{differences.Count} record(s) not identical");

            foreach (var difference in differences)
                Console.WriteLine($"  {difference}");

            return Ok;
        }

        private static int ConvertCell(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--from", "--map", "--out", "--portal" });

            var from = line.Value("--from");
            var maps = line.Values("--map");
            var outPath = line.Value("--out");
            var portal = line.Value("--portal");

            if (from == null || maps.Count == 0 || outPath == null)
                throw new ArgumentException("convert-cell needs --from, at least one --map and --out.");

            foreach (var input in maps.Concat(new[] { from, portal }).Where(f => f != null))
                RequireFile(input, "File");

            outPath = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            var surfaceMap = new Dictionary<ushort, ushort>();

            foreach (var entry in maps.SelectMany(DatCellConverter.LoadSurfaceMap))
                surfaceMap[entry.Key] = entry.Value;

            var report = DatCellConverter.Convert(from, outPath, surfaceMap, portal).ToText();

            Console.Write(report);
            File.WriteAllText(Path.ChangeExtension(outPath, ".report.txt"), report);
            return Ok;
        }

        private static int ConvertPortal(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--from", "--target", "--map", "--out", "--ids", "--surfaces" });

            var from = line.Value("--from");
            var target = line.Value("--target");
            var map = line.Value("--map");
            var outFolder = line.Value("--out");
            var ids = line.Value("--ids");
            var surfaces = line.Value("--surfaces");

            if (from == null || target == null || map == null || outFolder == null || (ids == null && surfaces == null))
                throw new ArgumentException("convert-portal needs --from, --target, --map, --out and --ids and/or --surfaces.");

            foreach (var input in new[] { from, target, map })
                RequireFile(input, "File");

            var importer = new DatPortalImporter(new DatDatabase(from, true), new DatDatabase(target, true), DatCellConverter.LoadSurfaceMap(map));

            importer.ImportIds(ids);
            importer.ImportSurfaces(surfaces);

            Directory.CreateDirectory(outFolder);

            foreach (var record in importer.Records)
                File.WriteAllBytes(Path.Combine(outFolder, $"{record.Key:X8} - converted from pre-ToD.bin"), record.Value);

            File.WriteAllText(Path.Combine(outFolder, "report.txt"), importer.ToText());
            WriteSurfaceMapAdditions(Path.Combine(outFolder, "surface_map_additions.tsv"), importer);
            Console.Write(importer.ToText());
            return Ok;
        }

        private static int Build(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--source", "--custom", "--out", "--config", "--world-db", "--season", "--season-date", "--event", "--event-date", "--skip-step" }, new[] { "--force", "--trace-map" }, allowPositional: true);

            var selector = line.Positional;
            var config = line.Value("--config");
            var force = line.Flag("--force");

            if (selector == null)
                throw new ArgumentException("build needs a recipe name or 'all' (see: list).");

            if (line.Value("--world-db") != null && config == null)
                throw new ArgumentException("--world-db needs --config <Config.js>.");

            var source = ResolveSource(line.Value("--source"), config);

            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("No source folder: pass --source, or give --config for a server with the dat_source_folder setting.");

            source = Path.GetFullPath(source);

            var custom = Path.GetFullPath(line.Value("--custom") ?? Path.Combine(source, "Custom Files"));
            var outRoot = Path.GetFullPath(line.Value("--out") ?? Path.Combine(source, "..", "Build"));

            var all = selector.Equals("all", StringComparison.OrdinalIgnoreCase);
            var recipes = DatBuildRecipes.All.Where(r => all || KeyMatches(r.Key, selector)).Concat(DatBuildRecipes.Alternatives.Where(r => KeyMatches(r.Key, selector))).ToList();
            var cellRecipes = DatBuildRecipes.AllCells.Where(r => all || KeyMatches(r.Key, selector)).Concat(DatBuildRecipes.AlternativeCells.Where(r => KeyMatches(r.Key, selector))).ToList();

            if (recipes.Count == 0 && cellRecipes.Count == 0)
                throw new ArgumentException($"Unknown recipe '{selector}' (see: list).");

            var eventName = line.Value("--event");

            if (eventName != null)
            {
                var date = line.Value("--event-date") ?? throw new ArgumentException("--event needs --event-date <yyyy-mm-dd>, the date the step is added: every step carries a date.");
                var kitRecipes = recipes.Where(r => r.EmptyBase).ToList();

                var kits = new Dictionary<string, Func<string, DatRecordStep[]>>(StringComparer.OrdinalIgnoreCase) { ["daralet"] = DaraletRecords.All, ["darkness"] = DarknessAscendantRecords.All, ["fireinthesky"] = FireInTheSkyRecords.All };

                if (!kits.TryGetValue(eventName, out var kit))
                    throw new ArgumentException($"No event '{eventName}'; the events are: {string.Join(", ", kits.Keys)}.");

                if (kitRecipes.Count == 0)
                    throw new ArgumentException("--event applies to the recipes that start from the 2005 dat (see: list).");

                foreach (var recipe in kitRecipes)
                    recipe.WithRecordSteps(recipe.RecordSteps.Concat(kit(date)).ToArray());
            }

            var season = line.Value("--season");

            if (season != null)
            {
                var date = line.Value("--season-date") ?? throw new ArgumentException("--season needs --season-date <yyyy-mm-dd>, the date of the event: every step carries a date.");
                var seasonal = recipes.Where(r => r.EmptyBase).ToList();

                if (seasonal.Count == 0)
                    throw new ArgumentException("--season applies to the recipes that start from the 2005 dat (see: list).");

                foreach (var recipe in seasonal)
                    recipe.WithRecordSteps(recipe.RecordSteps.Append(SeasonalRecords.Step(season, date)).ToArray());
            }

            var skipped = line.Values("--skip-step");

            foreach (var name in skipped.Where(n => !recipes.Any(r => r.RecordSteps.Any(s => s.Name == n))))
                throw new ArgumentException($"No record step '{name}' in the selected recipes (see: list).");

            foreach (var recipe in recipes)
                recipe.WithRecordSteps(recipe.RecordSteps.Where(s => !skipped.Contains(s.Name)).ToArray());

            Directory.CreateDirectory(outRoot);

            var tables = config != null ? new WorldDatTableSource(config, line.Value("--world-db")) : null;

            // A dat built for one ruleset with the config of another (or without the CustomDM tables) loads in the server but has the wrong spells, skills and XP.
            if (tables != null)
            {
                Console.WriteLine($"Config: ruleset {tables.Ruleset}, world database {tables.Description}");

                var foreign = recipes.Select(r => (r.Name, r.Ruleset)).Concat(cellRecipes.Select(r => (r.Name, r.Ruleset))).Where(r => r.Ruleset != tables.Ruleset).ToList();

                if (foreign.Count > 0)
                    throw new ArgumentException($"The config is for the {tables.Ruleset} ruleset but {string.Join(", ", foreign.Select(r => $"{r.Name} ({r.Ruleset})"))} is not: build each ruleset with its own --config.");
            }

            foreach (var recipe in recipes.Where(r => r.TablesDate != null && r.Ruleset == "CustomDM" && tables == null))
                throw new ArgumentException($"{recipe.Name} needs the dat tables of the CustomDM world database: pass --config <CustomDM Config.js>.");

            foreach (var recipe in recipes)
            {
                Console.WriteLine($"Building {recipe.Name} ...");

                var readsTables = recipe.TablesDate != null && tables != null;
                var result = DatBuildRunner.Build(recipe, source, custom, outRoot, force, readsTables ? tables : null);

                if (readsTables)
                    Console.WriteLine($"  dat tables read from {tables.Description}");

                if (readsTables && result.Tables.Skipped && recipe.Ruleset == "CustomDM")
                    throw new ArgumentException($"{tables.Description} has no dat tables, so the {recipe.Name} dat just written lacks the CustomDM spells and skills: delete it and use the CustomDM world database.");

                Console.Write(result.ToText(10));

                var stem = ReportStem(outRoot, recipe.OutputFolder ?? recipe.Ruleset, recipe.OutputFileName);

                File.WriteAllText(stem + ".report.txt", ReportStamp(recipe.Ruleset, readsTables ? tables.Description : null) + result.ToText());

                if (result.Import != null)
                {
                    WriteSurfaceMapAdditions(stem + ".surface_map_additions.tsv", result.Import);
                    File.WriteAllText(stem + ".import.txt", result.Import.ToText());
                }

                if (recipe.IsLanguage)
                    Console.WriteLine("  note: a language dat written this way is the same structure Melt writes, which crashes the client (cause not yet found).");
            }

            foreach (var recipe in cellRecipes)
            {
                Console.WriteLine($"Building {recipe.Name} ...");

                var portalReport = ReportStem(outRoot, recipe.OutputFolder ?? recipe.Ruleset, recipe.Portal.OutputFileName) + ".report.txt";

                if (!File.Exists(portalReport) || !File.ReadLines(portalReport).First().StartsWith($"Built for ruleset {recipe.Ruleset},"))
                    throw new ArgumentException($"The report of {recipe.Portal.Name} ({portalReport}) is missing or not stamped for the {recipe.Ruleset} ruleset: build the portal first.");

                var result = DatCellBuildRunner.Build(recipe, source, outRoot, force, line.Flag("--trace-map") ? new DatMapTrace() : null);
                var stem = ReportStem(outRoot, recipe.OutputFolder ?? recipe.Ruleset, recipe.OutputFileName);

                Console.Write(result.ToText());
                File.WriteAllText(stem + ".report.txt", ReportStamp(recipe.Ruleset, null) + result.ToText());

                if (result.MapTrace != null)
                {
                    Console.Write(result.MapTrace);
                    File.WriteAllText(stem + ".trace.txt", result.MapTrace);
                }
            }

            return Ok;
        }

        private static int ApplyTables(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--source", "--out", "--dat", "--config", "--world-db" }, allowPositional: true);

            var selector = line.Positional;
            var config = line.Value("--config");
            var datPath = line.Value("--dat");

            if (selector == null)
                throw new ArgumentException("apply-tables needs a portal recipe name (see: list).");

            if (config == null)
                throw new ArgumentException("apply-tables needs --config <Config.js>.");

            var recipe = DatBuildRecipes.All.FirstOrDefault(r => KeyMatches(r.Key, selector));

            if (recipe == null)
                throw new ArgumentException($"Unknown recipe '{selector}' (see: list).");

            if (recipe.TablesDate == null)
                throw new ArgumentException($"{recipe.Name} has no dat tables step.");

            if (datPath == null)
            {
                var outRoot = line.Value("--out");
                var source = ResolveSource(line.Value("--source"), config);

                if (outRoot == null && string.IsNullOrEmpty(source))
                    throw new ArgumentException("No dat to change: pass --dat, --out, --source, or give --config for a server with the dat_source_folder setting.");

                outRoot = Path.GetFullPath(outRoot ?? Path.Combine(source, "..", "Build"));
                datPath = Path.Combine(outRoot, recipe.Ruleset, recipe.OutputFileName);
            }

            var tables = new WorldDatTableSource(config, line.Value("--world-db"));
            var result = DatBuildRunner.ApplyTables(recipe, Path.GetFullPath(datPath), tables);

            Console.WriteLine($"{recipe.Name}: {result.DatPath}");
            Console.WriteLine($"  dat tables read from {tables.Description}");
            Console.WriteLine(result.Tables.ToText());

            if (!result.Tables.Skipped)
                Console.WriteLine($"  {(result.Changed ? "dat changed" : "dat already matches the database")}, audit ok ({result.AuditAfter.Records} records), release {result.ReleaseNumber}, hash {result.ReleaseHash}");

            return Ok;
        }

        private static int ExportTables(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--dat", "--language", "--out" });

            var datPath = line.Value("--dat");
            var outPath = line.Value("--out");
            var languagePath = line.Value("--language");

            if (datPath == null || outPath == null)
                throw new ArgumentException("export-tables needs --dat and --out.");

            RequireFile(datPath, "Dat");

            var dat = new DatDatabase(datPath, true);

            if (!dat.AllFiles.ContainsKey(DatTableStep.XpRecordId) || !dat.AllFiles.ContainsKey(DatTableStep.SkillRecordId))
                throw new ArgumentException($"{datPath} has no XP or skill table record.");

            var xp = DatTables.ToRows(dat.ReadFromDat<XpTable>(DatTableStep.XpRecordId));
            var skills = DatTables.ToRows(dat.ReadFromDat<SkillTable>(DatTableStep.SkillRecordId));

            var script = new StringBuilder(DatTables.InsertScript(xp, skills));

            Console.WriteLine($"{outPath}: {xp.Attribute.Count} attribute ranks, {xp.Vital.Count} vital ranks, {xp.TrainedSkill.Count} trained skill ranks, {xp.SpecializedSkill.Count} specialized skill ranks, {xp.Levels.Count} levels, {skills.Count} skills");
            Console.WriteLine($"  xp hash {DatTables.Hash(xp)}, skill hash {DatTables.Hash(skills)}");

            if (dat.AllFiles.ContainsKey(DatTableStep.VitalRecordId))
            {
                var vitals = DatTables.ToRows(dat.ReadFromDat<SecondaryAttributeTable>(DatTableStep.VitalRecordId));

                script.Append(DatTables.VitalInsertScript(vitals));

                Console.WriteLine($"  {vitals.Count} vital formulas, vital hash {DatTables.Hash(vitals)}");
            }

            if (dat.AllFiles.ContainsKey(DatTableStep.SpellRecordId))
            {
                var spellTable = dat.ReadFromDat<SpellTable>(DatTableStep.SpellRecordId);
                var spells = DatTables.ToRows(spellTable);
                var setSpells = DatTables.ToSetRows(spellTable);

                script.Append(DatTables.SpellInsertScript(spells, setSpells));

                Console.WriteLine($"  {spells.Count} spells, {spellTable.SpellSet.Count} equipment sets with {setSpells.Count} tier spells, spell hash {DatTables.Hash(spells, setSpells)}");
            }

            if (dat.AllFiles.ContainsKey(DatTableStep.ChargenRecordId))
            {
                var chargen = DatTables.ToRows(dat.ReadFromDat<CharGen>(DatTableStep.ChargenRecordId));

                script.Append(DatTables.ChargenInsertScript(chargen));

                Console.WriteLine($"  {chargen.Overrides.Count} heritage skill costs, {chargen.Templates.Count} templates with {chargen.TemplateSkills.Count} skills, credits {chargen.Chargen.AttributeCredits}/{chargen.Chargen.SkillCredits}, chargen hash {DatTables.Hash(chargen)}");
            }

            if (languagePath != null)
            {
                RequireFile(languagePath, "Language dat");

                if (!dat.AllFiles.ContainsKey(DatTableStep.TitleMapperRecordId))
                    throw new ArgumentException($"{datPath} has no title enum mapper record.");

                var language = new DatDatabase(languagePath, true);

                if (!language.AllFiles.ContainsKey(DatTableStep.TitleStringRecordId))
                    throw new ArgumentException($"{languagePath} has no title string table record.");

                var titles = DatTables.ToRows(dat.ReadFromDat<EnumMapper>(DatTableStep.TitleMapperRecordId), language.ReadFromDat<StringTable>(DatTableStep.TitleStringRecordId));

                script.Append(DatTables.TitleInsertScript(titles));

                Console.WriteLine($"  {titles.Count} character titles, title hash {DatTables.Hash(titles)}");
            }

            File.WriteAllText(Path.GetFullPath(outPath), script.ToString());
            return Ok;
        }

        /// <summary>The source folder: the --source value, else the dat_source_folder setting of the --config server, else null.</summary>
        private static string ResolveSource(string source, string config)
        {
            if (!string.IsNullOrEmpty(source) || config == null)
                return source;

            source = DatSourceSetting.Read(config);

            if (source != null)
                Console.WriteLine($"Source folder read from the {DatSourceSetting.Key} setting: {source}");

            return source;
        }

        private static bool KeyMatches(string key, string selector) => key.Equals(selector, StringComparison.OrdinalIgnoreCase);

        private static void RequireFile(string path, string what)
        {
            if (!File.Exists(path))
                throw new ArgumentException($"{what} not found: {path}");
        }

        /// <summary>The path of a recipe's output dat without its extension, the start of the names of its report files.</summary>
        /// <summary>First line of a build report: which ruleset the dat is for and which database its dat tables came from. A cell build checks its portal's.</summary>
        private static string ReportStamp(string ruleset, string tablesFrom) => $"Built for ruleset {ruleset}, dat tables: {tablesFrom ?? "none"}\n";

        private static string ReportStem(string outRoot, string ruleset, string outputFileName) => Path.Combine(outRoot, ruleset, Path.ChangeExtension(outputFileName, null));

        private static void WriteSurfaceMapAdditions(string path, DatPortalImporter importer)
        {
            File.WriteAllLines(path, importer.ImportedSurfaces.Select(s => $"{s.Key:X4}\t{s.Value:X4}\timported"));
        }
    }
}
