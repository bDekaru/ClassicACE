using System;
using System.IO;
using System.Linq;

using ACE.DatLoader;

namespace ACE.DatBuilder
{
    public static class Program
    {
        private const int Ok = 0;
        private const int UsageError = 1;
        private const int AuditFailure = 2;

        private const string Usage =
@"ACE.DatBuilder build <recipe|all> [--source <folder>] [--custom <folder>] [--out <folder>] [--force] [--config <Config.js>] [--world-db <name>]
ACE.DatBuilder apply-tables <portal recipe> --config <Config.js> [--world-db <name>] [--dat <built portal dat> | --out <folder> | --source <folder>]
ACE.DatBuilder export-tables --dat <portal dat> --out <sql file>
ACE.DatBuilder list
ACE.DatBuilder audit <dat>
ACE.DatBuilder compare-cell <first cell dat> <second cell dat> [landblocks to list]
ACE.DatBuilder convert-portal --from <pre-ToD portal dat> --target <ToD/EoR portal dat> --map <surface map tsv> --out <folder> [--ids <id,first-last,...>] [--surfaces <surface ids>]
ACE.DatBuilder convert-cell --from <pre-ToD cell dat> --template <ToD cell dat> --map <surface map tsv> --out <new dat> [--portal <target portal dat>]

  A cell recipe needs its ruleset's portal built first (in the same --out); 'all' builds the portals before the cells.

  --source  folder with the original dats (default: environment variable ACE_DAT_SOURCE, then ACE_TEST_SOURCE_DATS)
  --custom  folder with the custom files (default: <source>/Custom Files)
  --out     output root; each recipe writes to <out>/<ruleset>/<name>.dat (default: <source>/../Build)
  --force   replace an existing output dat
  --config  a server Config.js: its MySql.World connection is used to read the dat_* tables (XP and skill tables) of a recipe that has them; without it the records from the custom files are kept
  --world-db  the world database to read instead of the one named in the Config.js

  apply-tables rewrites only the XP and skill records of an already built portal dat from the database (the quick loop after editing the dat_* tables).
  export-tables writes the SQL that fills the dat_* tables with the values of a portal dat's XP and skill records (the tables themselves come from the world base script).

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
                    case "list":
                        foreach (var recipe in DatBuildRecipes.All)
                            Console.WriteLine($"{recipe.Key,-24} {recipe.BaseDat} + {recipe.Folders.Length} folder(s) -> {recipe.Ruleset}/{recipe.OutputFileName}");

                        foreach (var recipe in DatBuildRecipes.AllCells)
                            Console.WriteLine($"{recipe.Key,-24} {recipe.RetailCellDat} converted against {recipe.Portal.Key} -> {recipe.Ruleset}/{recipe.OutputFileName}");
                        return Ok;

                    case "audit":
                        return Audit(args);

                    case "compare-cell":
                        return CompareCell(args);

                    case "convert-cell":
                        return ConvertCell(args);

                    case "convert-portal":
                        return ConvertPortal(args);

                    case "build":
                        return Build(args);

                    case "apply-tables":
                        return ApplyTables(args);

                    case "export-tables":
                        return ExportTables(args);

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

        private static int Audit(string[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("audit takes one dat path.");

            if (!File.Exists(args[1]))
                throw new ArgumentException($"Dat not found: {args[1]}");

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

        private static int CompareCell(string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
                throw new ArgumentException("compare-cell takes two dat paths and an optional number of differing landblocks to list.");

            foreach (var path in args.Skip(1).Take(2))
                if (!File.Exists(path))
                    throw new ArgumentException($"Dat not found: {path}");

            var listCount = args.Length == 4 ? int.Parse(args[3]) : 20;
            var a = new DatDatabase(args[1], true);
            var b = new DatDatabase(args[2], true);
            var ids = a.AllFiles.Keys.Union(b.AllFiles.Keys).OrderBy(k => k).ToList();
            var differing = new System.Collections.Generic.Dictionary<uint, System.Collections.Generic.List<string>>();
            var counts = new System.Collections.Generic.SortedDictionary<string, int[]>();

            foreach (var id in ids)
            {
                var kind = RecordKind(id);

                if (!counts.TryGetValue(kind, out var count))
                    counts[kind] = count = new int[4];

                var inA = a.AllFiles.ContainsKey(id);
                var inB = b.AllFiles.ContainsKey(id);
                string change = null;

                if (inA && inB)
                {
                    if (a.GetReaderForFile(id).Buffer.SequenceEqual(b.GetReaderForFile(id).Buffer))
                    {
                        count[0]++;
                        continue;
                    }

                    count[1]++;
                    change = "differs";
                }
                else if (inA)
                {
                    count[2]++;
                    change = "only in first";
                }
                else
                {
                    count[3]++;
                    change = "only in second";
                }

                if (!differing.TryGetValue(id & 0xFFFF0000, out var list))
                    differing[id & 0xFFFF0000] = list = new System.Collections.Generic.List<string>();

                list.Add($"{id:X8} {change}");
            }

            foreach (var entry in counts)
                Console.WriteLine($"{entry.Key,-15} identical {entry.Value[0]}, different {entry.Value[1]}, only in first {entry.Value[2]}, only in second {entry.Value[3]}");

            Console.WriteLine($"{differing.Count} landblock(s) with at least one difference");

            foreach (var entry in differing.Take(listCount))
                Console.WriteLine($"  {entry.Key >> 16:X4}: {entry.Value.Count} record(s), e.g. {string.Join("; ", entry.Value.Take(3))}");

            return Ok;
        }

        private static int ConvertCell(string[] args)
        {
            string from = null, template = null, outPath = null, portal = null;
            var maps = new System.Collections.Generic.List<string>();

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--from": from = Value(args, ref i); break;
                    case "--template": template = Value(args, ref i); break;
                    case "--map": maps.Add(Value(args, ref i)); break;
                    case "--out": outPath = Value(args, ref i); break;
                    case "--portal": portal = Value(args, ref i); break;
                    default: throw new ArgumentException($"Unexpected argument: {args[i]}");
                }
            }

            if (from == null || template == null || maps.Count == 0 || outPath == null)
                throw new ArgumentException("convert-cell needs --from, --template, at least one --map and --out.");

            foreach (var input in maps.Concat(new[] { from, template, portal }).Where(f => f != null))
                if (!File.Exists(input))
                    throw new ArgumentException($"File not found: {input}");

            outPath = Path.GetFullPath(outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));

            var surfaceMap = new System.Collections.Generic.Dictionary<ushort, ushort>();
            foreach (var map in maps)
                foreach (var entry in DatCellConverter.LoadSurfaceMap(map))
                    surfaceMap[entry.Key] = entry.Value;

            var report = DatCellConverter.Convert(from, template, outPath, surfaceMap, portal);

            Console.Write(report.ToText());
            File.WriteAllText(Path.ChangeExtension(outPath, ".report.txt"), report.ToText());
            return Ok;
        }

        private static int ConvertPortal(string[] args)
        {
            string from = null, target = null, map = null, outFolder = null, ids = null, surfaces = null;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--from": from = Value(args, ref i); break;
                    case "--target": target = Value(args, ref i); break;
                    case "--map": map = Value(args, ref i); break;
                    case "--out": outFolder = Value(args, ref i); break;
                    case "--ids": ids = Value(args, ref i); break;
                    case "--surfaces": surfaces = Value(args, ref i); break;
                    default: throw new ArgumentException($"Unexpected argument: {args[i]}");
                }
            }

            if (from == null || target == null || map == null || outFolder == null || (ids == null && surfaces == null))
                throw new ArgumentException("convert-portal needs --from, --target, --map, --out and --ids and/or --surfaces.");

            foreach (var input in new[] { from, target, map })
                if (!File.Exists(input))
                    throw new ArgumentException($"File not found: {input}");

            var importer = new DatPortalImporter(new DatDatabase(from, true), new DatDatabase(target, true), DatCellConverter.LoadSurfaceMap(map));

            importer.ImportIds(ids);
            importer.ImportSurfaces(surfaces);

            Directory.CreateDirectory(outFolder);

            foreach (var record in importer.Records)
                File.WriteAllBytes(Path.Combine(outFolder, $"{record.Key:X8} - converted from pre-ToD.bin"), record.Value);

            File.WriteAllText(Path.Combine(outFolder, "report.txt"), importer.ToText());
            File.WriteAllLines(Path.Combine(outFolder, "surface_map_additions.tsv"), importer.ImportedSurfaces.Select(s => $"{s.Key:X4}\t{s.Value:X4}\timported"));
            Console.Write(importer.ToText());
            return Ok;
        }

        private static int Build(string[] args)
        {
            string selector = null, source = null, custom = null, outRoot = null, config = null, worldDb = null;
            var force = false;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--source": source = Value(args, ref i); break;
                    case "--custom": custom = Value(args, ref i); break;
                    case "--out": outRoot = Value(args, ref i); break;
                    case "--force": force = true; break;
                    case "--config": config = Value(args, ref i); break;
                    case "--world-db": worldDb = Value(args, ref i); break;
                    default:
                        if (args[i].StartsWith("--") || selector != null)
                            throw new ArgumentException($"Unexpected argument: {args[i]}");
                        selector = args[i];
                        break;
                }
            }

            if (selector == null)
                throw new ArgumentException("build needs a recipe name or 'all' (see: list).");

            source = source ?? Environment.GetEnvironmentVariable("ACE_DAT_SOURCE") ?? Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS");

            if (string.IsNullOrEmpty(source))
                throw new ArgumentException("No source folder: pass --source or set ACE_DAT_SOURCE.");

            source = Path.GetFullPath(source);
            custom = Path.GetFullPath(custom ?? Path.Combine(source, "Custom Files"));
            outRoot = Path.GetFullPath(outRoot ?? Path.Combine(source, "..", "Build"));

            var all = selector.Equals("all", StringComparison.OrdinalIgnoreCase);

            var recipes = all
                ? DatBuildRecipes.All
                : DatBuildRecipes.All.Where(r => r.Key.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();

            var cellRecipes = all
                ? DatBuildRecipes.AllCells
                : DatBuildRecipes.AllCells.Where(r => r.Key.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (recipes.Length == 0 && cellRecipes.Length == 0)
                throw new ArgumentException($"Unknown recipe '{selector}' (see: list).");

            Directory.CreateDirectory(outRoot);

            var tables = config != null ? new WorldDatTableSource(config, worldDb) : null;

            foreach (var recipe in recipes)
            {
                Console.WriteLine($"Building {recipe.Name} ...");

                var result = DatBuildRunner.Build(recipe, source, custom, outRoot, force, recipe.TablesDate != null ? tables : null);

                if (recipe.TablesDate != null && tables != null)
                    Console.WriteLine($"  dat tables read from {tables.Description}");

                Console.Write(result.ToText(10));
                File.WriteAllText(Path.Combine(outRoot, recipe.Ruleset, Path.ChangeExtension(recipe.OutputFileName, null) + ".report.txt"), result.ToText());

                if (result.Import != null)
                {
                    var stem = Path.Combine(outRoot, recipe.Ruleset, Path.ChangeExtension(recipe.OutputFileName, null));

                    File.WriteAllLines(stem + ".surface_map_additions.tsv", result.Import.ImportedSurfaces.Select(s => $"{s.Key:X4}\t{s.Value:X4}\timported"));
                    File.WriteAllText(stem + ".import.txt", result.Import.ToText());
                }

                if (recipe.Name.StartsWith("Language"))
                    Console.WriteLine("  note: a language dat written this way is the same structure Melt writes, which crashes the client (cause not yet found).");
            }

            foreach (var recipe in cellRecipes)
            {
                Console.WriteLine($"Building {recipe.Name} ...");

                var result = DatCellBuildRunner.Build(recipe, source, outRoot, force);

                Console.Write(result.ToText());
                File.WriteAllText(Path.Combine(outRoot, recipe.Ruleset, Path.ChangeExtension(recipe.OutputFileName, null) + ".report.txt"), result.ToText());
            }

            return Ok;
        }

        private static int ApplyTables(string[] args)
        {
            string selector = null, source = null, outRoot = null, datPath = null, config = null, worldDb = null;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--source": source = Value(args, ref i); break;
                    case "--out": outRoot = Value(args, ref i); break;
                    case "--dat": datPath = Value(args, ref i); break;
                    case "--config": config = Value(args, ref i); break;
                    case "--world-db": worldDb = Value(args, ref i); break;
                    default:
                        if (args[i].StartsWith("--") || selector != null)
                            throw new ArgumentException($"Unexpected argument: {args[i]}");
                        selector = args[i];
                        break;
                }
            }

            if (selector == null)
                throw new ArgumentException("apply-tables needs a portal recipe name (see: list).");

            if (config == null)
                throw new ArgumentException("apply-tables needs --config <Config.js>.");

            var recipe = DatBuildRecipes.All.FirstOrDefault(r => r.Key.Equals(selector, StringComparison.OrdinalIgnoreCase));

            if (recipe == null)
                throw new ArgumentException($"Unknown recipe '{selector}' (see: list).");

            if (recipe.TablesDate == null)
                throw new ArgumentException($"{recipe.Name} has no dat tables step.");

            if (datPath == null)
            {
                source = source ?? Environment.GetEnvironmentVariable("ACE_DAT_SOURCE") ?? Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS");

                if (outRoot == null && string.IsNullOrEmpty(source))
                    throw new ArgumentException("No dat to change: pass --dat, --out or --source, or set ACE_DAT_SOURCE.");

                outRoot = Path.GetFullPath(outRoot ?? Path.Combine(source, "..", "Build"));
                datPath = Path.Combine(outRoot, recipe.Ruleset, recipe.OutputFileName);
            }

            var tables = new WorldDatTableSource(config, worldDb);
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
            string datPath = null, outPath = null;

            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dat": datPath = Value(args, ref i); break;
                    case "--out": outPath = Value(args, ref i); break;
                    default: throw new ArgumentException($"Unexpected argument: {args[i]}");
                }
            }

            if (datPath == null || outPath == null)
                throw new ArgumentException("export-tables needs --dat and --out.");

            if (!File.Exists(datPath))
                throw new ArgumentException($"Dat not found: {datPath}");

            var dat = new DatDatabase(datPath, true);

            if (!dat.AllFiles.ContainsKey(DatTableStep.XpRecordId) || !dat.AllFiles.ContainsKey(DatTableStep.SkillRecordId))
                throw new ArgumentException($"{datPath} has no XP or skill table record.");

            var xp = DatTables.ToRows(dat.ReadFromDat<ACE.DatLoader.FileTypes.XpTable>(DatTableStep.XpRecordId));
            var skills = DatTables.ToRows(dat.ReadFromDat<ACE.DatLoader.FileTypes.SkillTable>(DatTableStep.SkillRecordId));

            File.WriteAllText(Path.GetFullPath(outPath), DatTables.InsertScript(xp, skills));

            Console.WriteLine($"{outPath}: {xp.Attribute.Count} attribute ranks, {xp.Vital.Count} vital ranks, {xp.TrainedSkill.Count} trained skill ranks, {xp.SpecializedSkill.Count} specialized skill ranks, {xp.Levels.Count} levels, {skills.Count} skills");
            Console.WriteLine($"  xp hash {DatTables.Hash(xp)}, skill hash {DatTables.Hash(skills)}");
            return Ok;
        }

        private static string Value(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException($"{args[i]} needs a value.");

            return args[++i];
        }
    }
}
