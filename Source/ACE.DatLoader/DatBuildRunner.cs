using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ACE.DatLoader
{
    public class DatBuildResult
    {
        public DatBuildRecipe Recipe { get; set; }
        public string OutputPath { get; set; }
        public long OutputBytes { get; set; }
        public DatAudit AuditBefore { get; set; }
        public DatAudit AuditAfter { get; set; }
        public DatOverlayReport Overlay { get; set; }

        /// <summary>The records imported from the pre-ToD dat, when the recipe has an import step.</summary>
        public DatPortalImporter Import { get; set; }

        public string ToText(int maxListed = int.MaxValue)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{Recipe.Name}: {OutputPath} ({OutputBytes:N0} bytes)");
            sb.AppendLine($"  base {Recipe.BaseDat}: {AuditBefore.Records} records, audit ok");

            if (Import != null)
                sb.AppendLine($"  pre-ToD import from {Recipe.PreToDImport.PreToDDat}: {Import.Records.Count} records, {Import.ImportedSurfaces.Count()} imported surfaces");

            sb.AppendLine($"  overlay: added {Overlay.Added}, replaced {Overlay.Replaced}, identical duplicates {Overlay.IdenticalDuplicates}, differing duplicates {Overlay.DifferingDuplicates.Count}, name mismatches {Overlay.NameMismatches.Count}, refused {Overlay.Refused}, skipped {Overlay.Skipped.Count}");
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

    /// <summary>
    /// Builds one dat: copies the base dat from the source folder to a temporary file next to the output, audits it, lays the recipe's overlay folders over it,
    /// audits again and only then moves it to its final name, so a failed build leaves no partial dat. The source folder is only ever read.
    /// </summary>
    public static class DatBuildRunner
    {
        /// <exception cref="ArgumentException">An input is missing, or the output would overwrite something it must not.</exception>
        /// <exception cref="InvalidDataException">An audit of the base or built dat failed.</exception>
        public static DatBuildResult Build(DatBuildRecipe recipe, string sourceDats, string customFilesRoot, string outputRoot, bool force = false)
        {
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

                using (var writer = new DatWriter(temp) { NewRecordDate = DatWriter.ReproducibleRecordDate })
                {
                    result.AuditBefore = writer.Audit();
                    result.Overlay = recipe.Apply(writer, customFilesRoot, importer?.Records);
                    result.AuditAfter = writer.Audit();
                }

                result.OutputBytes = new FileInfo(temp).Length;

                if (File.Exists(output))
                    File.Delete(output);

                File.Move(temp, output);

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
