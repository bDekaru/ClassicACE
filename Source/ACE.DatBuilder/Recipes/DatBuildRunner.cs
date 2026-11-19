using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    public class DatBuildResult
    {
        public DatBuildRecipe Recipe { get; set; }
        public string OutputPath { get; set; }
        public long OutputBytes { get; set; }
        public DatAudit AuditBefore { get; set; }
        public DatAudit AuditAfter { get; set; }
        public DatOverlayReport Overlay { get; set; }

        /// <summary>What the record steps wrote, one line per record.</summary>
        public System.Collections.Generic.List<string> RecordLog { get; } = new System.Collections.Generic.List<string>();

        /// <summary>The records imported from the pre-ToD dat, when the recipe has an import step.</summary>
        public DatPortalImporter Import { get; set; }

        /// <summary>What the dat tables step did; null when it did not run (no source given, or the recipe has no tables step).</summary>
        public DatTablesReport Tables { get; set; }

        /// <summary>The release the dat was built as (its iteration) and the <see cref="DatReleaseHash"/> of the built dat, for the release table.</summary>
        public uint ReleaseNumber { get; set; }
        public string ReleaseHash { get; set; }

        public string ToText(int maxListed = int.MaxValue)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{Recipe.Name}: {OutputPath} ({OutputBytes:N0} bytes)");
            sb.AppendLine($"  release {ReleaseNumber}, hash {ReleaseHash}");
            sb.AppendLine($"  base {Recipe.BaseDat}: {AuditBefore.Records} records, audit ok");

            if (Import != null)
                sb.AppendLine($"  pre-ToD import from {Recipe.PreToDImport.PreToDDat}: {Import.Records.Count} records, {Import.ImportedSurfaces.Count()} imported surfaces");

            foreach (var line in RecordLog)
                sb.AppendLine(line);

            sb.AppendLine($"  overlay: added {Overlay.Added}, replaced {Overlay.Replaced}, identical duplicates {Overlay.IdenticalDuplicates}, differing duplicates {Overlay.DifferingDuplicates.Count}, name mismatches {Overlay.NameMismatches.Count}, refused {Overlay.Refused}, skipped {Overlay.Skipped.Count}");
            if (Tables != null)
                sb.AppendLine(Tables.ToText());

            sb.AppendLine($"  result: {AuditAfter.Records} records, {AuditAfter.FreeBlocks} free blocks, {AuditAfter.OrphanedBlocks} orphaned blocks, audit ok");

            AppendList(sb, "differing duplicate (the later file was written)", Overlay.DifferingDuplicates, maxListed);
            AppendList(sb, "name mismatch", Overlay.NameMismatches, maxListed);
            AppendList(sb, "skipped", Overlay.Skipped, maxListed);

            return sb.ToString();
        }

        private static void AppendList(StringBuilder sb, string label, System.Collections.Generic.List<string> items, int max)
        {
            foreach (var item in items.Take(max))
                sb.AppendLine($"  {label}: {item}");

            if (items.Count > max)
                sb.AppendLine($"  ... {items.Count - max} more {label} entries in the report file");
        }
    }

    public class DatTablesApplyResult
    {
        public string DatPath { get; set; }
        public DatTablesReport Tables { get; set; }
        public DatAudit AuditAfter { get; set; }

        /// <summary>True when the dat was replaced; false when nothing needed to change.</summary>
        public bool Changed { get; set; }

        public uint ReleaseNumber { get; set; }
        public string ReleaseHash { get; set; }
    }

    /// <summary>
    /// Builds one dat: copies the base dat from the source folder to a temporary file next to the output, audits it, lays the recipe's overlay folders over it,
    /// audits again and only then moves it to its final name, so a failed build leaves no partial dat. The source folder is only ever read.
    /// </summary>
    public static class DatBuildRunner
    {
        /// <exception cref="ArgumentException">An input is missing, or the output would overwrite something it must not.</exception>
        /// <exception cref="InvalidDataException">An audit of the base or built dat failed.</exception>
        /// <summary>
        /// Rewrites only the XP and skill records of an already built portal dat from the database (the quick loop after editing the dat_* tables), through a temporary copy that is audited before it replaces the dat.
        /// </summary>
        /// <exception cref="ArgumentException">The recipe has no tables step or the dat is missing.</exception>
        /// <exception cref="InvalidDataException">An audit failed.</exception>
        public static DatTablesApplyResult ApplyTables(DatBuildRecipe recipe, string datPath, IDatTableSource tables)
        {
            if (recipe.TablesDate == null)
                throw new ArgumentException($"{recipe.Name} has no dat tables step.");

            if (!File.Exists(datPath))
                throw new ArgumentException($"Dat not found: {datPath}");

            recipe.ValidateDates();

            var temp = datPath + ".building";
            File.Copy(datPath, temp, true);

            try
            {
                var result = new DatTablesApplyResult { DatPath = datPath, ReleaseNumber = recipe.Releases.CurrentNumber };

                using (var writer = new DatWriter(temp) { IterationForDate = recipe.Releases.IterationForDate })
                {
                    writer.Audit();
                    result.Tables = recipe.IsLanguage ? DatTableStep.ApplyLanguage(writer, tables, recipe.TablesTime) : DatTableStep.Apply(writer, tables, recipe.TablesTime);
                    result.Changed = result.Tables.Written.Count > 0;

                    if (result.Changed)
                        writer.SaveIterationRecord(recipe.Releases.CurrentNumber, recipe.LastStepTime);

                    result.AuditAfter = writer.Audit();
                }

                if (result.Changed)
                {
                    File.Delete(datPath);
                    File.Move(temp, datPath);
                }
                else
                    File.Delete(temp);

                result.ReleaseHash = DatReleaseHash.Compute(datPath);
                return result;
            }
            catch
            {
                if (File.Exists(temp))
                    File.Delete(temp);

                throw;
            }
        }

        /// <param name="tables">Where the XP and skill tables are read from, for a recipe with a tables step. Null leaves the records the overlay wrote.</param>
        public static DatBuildResult Build(DatBuildRecipe recipe, string sourceDats, string customFilesRoot, string outputRoot, bool force = false, IDatTableSource tables = null)
        {
            recipe.ValidateDates();

            var baseDat = Path.Combine(sourceDats, recipe.BaseDat);

            if (!File.Exists(baseDat))
                throw new ArgumentException($"Base dat not found: {baseDat}");

            var missing = recipe.ResolveFolders(customFilesRoot).FirstOrDefault(f => !Directory.Exists(f));

            if (missing != null)
                throw new ArgumentException($"Overlay folder not found: {missing}");

            var outputFolder = Path.GetFullPath(Path.Combine(outputRoot, recipe.Ruleset));
            var output = Path.Combine(outputFolder, recipe.OutputFileName);
            var sourceFull = Path.GetFullPath(sourceDats).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if ((outputFolder + Path.DirectorySeparatorChar).StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"The output folder {outputFolder} is inside the source folder {sourceFull}; the source dats are never written.");

            if (File.Exists(output) && !force)
                throw new ArgumentException($"{output} already exists (use --force to replace it).");

            Directory.CreateDirectory(outputFolder);

            var temp = output + ".building";
            File.Copy(baseDat, temp, true);

            try
            {
                var result = new DatBuildResult { Recipe = recipe, OutputPath = output };

                DatPortalImporter importer = null;

                if (recipe.PreToDImport != null)
                {
                    if (!File.Exists(Path.Combine(sourceDats, recipe.PreToDImport.PreToDDat)))
                        throw new ArgumentException($"Pre-ToD dat not found: {Path.Combine(sourceDats, recipe.PreToDImport.PreToDDat)}");

                    importer = recipe.PreToDImport.Run(sourceDats, baseDat);
                    result.Import = importer;
                }

                using (var writer = new DatWriter(temp) { IterationForDate = recipe.Releases.IterationForDate })
                {
                    result.AuditBefore = writer.Audit();

                    var written = new HashSet<uint>();

                    foreach (var step in recipe.RecordSteps)
                    {
                        var context = new DatRecordContext(writer, sourceDats, step);
                        step.Run(context);
                        written.UnionWith(context.Written);
                        result.RecordLog.Add($"  record step '{step.Name}' ({step.Date})");
                        result.RecordLog.AddRange(context.Log.Select(l => "    " + l));
                    }

                    result.Overlay = recipe.Apply(writer, customFilesRoot, importer?.Records.Where(r => !written.Contains(r.Key)));

                    if (recipe.TablesDate != null && tables != null)
                        result.Tables = recipe.IsLanguage ? DatTableStep.ApplyLanguage(writer, tables, recipe.TablesTime) : DatTableStep.Apply(writer, tables, recipe.TablesTime);

                    writer.SaveIterationRecord(recipe.Releases.CurrentNumber, recipe.LastStepTime);
                    result.AuditAfter = writer.Audit();
                }

                result.OutputBytes = new FileInfo(temp).Length;

                if (File.Exists(output))
                    File.Delete(output);

                File.Move(temp, output);

                result.ReleaseNumber = recipe.Releases.CurrentNumber;
                result.ReleaseHash = DatReleaseHash.Compute(output);

                return result;
            }
            catch
            {
                if (File.Exists(temp))
                    File.Delete(temp);

                throw;
            }
        }
    }
}
