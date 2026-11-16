using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ACE.DatLoader
{
    public class DatOverlayReport
    {
        public int Added { get; set; }
        public int Replaced { get; set; }
        public int Refused { get; set; }

        /// <summary>Files that are not records (not .bin) are never listed; these are .bin files that were not written.</summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <summary>Files whose first four bytes name a different record than their file name does (or whose name has no id). They are still written, under the id in their data, as Melt does.</summary>
        public List<string> NameMismatches { get; } = new List<string>();

        /// <summary>The same record id found in two files with different contents; the later file was written.</summary>
        public List<string> DifferingDuplicates { get; } = new List<string>();

        public int IdenticalDuplicates { get; set; }

        public int Written => Added + Replaced;
    }

    /// <summary>
    /// Lays folders of raw record files (*.bin) over a dat through a <see cref="DatWriter"/>.
    /// Follows Melt's addFilesFromFolder: a file whose name starts with "08" (a surface) takes its id from the first eight hex digits of the name, because the data does not start with the id;
    /// every other file takes its id from the first four bytes of its data, whatever its name says (a disagreement is reported but the file is still written).
    /// Folders apply in the order given, so a later file replaces an earlier one with the same id. Within one folder the files go in path order; Melt's recipes never have two files with
    /// different contents for the same id inside one folder, so that order cannot change the result.
    /// </summary>
    public static class DatOverlay
    {
        public static bool IsLanguageId(uint id)
        {
            var type = id >> 24;
            return type == 0x21 || type == 0x23 || type == 0x41;
        }

        public static bool IsPortalId(uint id)
        {
            return !IsLanguageId(id) && (id & 0xFFFF) != 0xFFFF && (id & 0xFFFF) != 0xFFFE;
        }

        /// <param name="acceptId">Records whose id this rejects are counted as refused and not written. Null accepts every id.</param>
        public const string GeneratedSource = "(generated from the pre-ToD dat)";

        /// <param name="generated">Records made by a converter, written before any folder so that a file in a folder still replaces them. Their ids are used as given.</param>
        public static DatOverlayReport Apply(DatWriter writer, IEnumerable<string> folders, Func<uint, bool> acceptId = null, IEnumerable<KeyValuePair<uint, byte[]>> generated = null)
        {
            return Apply(writer, folders.Select(f => (f, 0u)), acceptId, generated, 0);
        }

        /// <param name="steps">Each folder with the date its records carry (0 leaves the writer's record date as it is).</param>
        /// <param name="generatedDate">The date the generated records carry (0 leaves the writer's record date as it is).</param>
        public static DatOverlayReport Apply(DatWriter writer, IEnumerable<(string Folder, uint Date)> steps, Func<uint, bool> acceptId, IEnumerable<KeyValuePair<uint, byte[]>> generated, uint generatedDate)
        {
            var report = new DatOverlayReport();
            var written = new Dictionary<uint, string>();

            using (var md5 = MD5.Create())
            {
                var hashes = new Dictionary<uint, string>();

                void Write(uint id, byte[] data, string source)
                {
                    if (acceptId != null && !acceptId(id))
                    {
                        report.Refused++;
                        report.Skipped.Add($"{source}: id {id:X8} is not valid for this dat");
                        return;
                    }

                    var hash = Convert.ToBase64String(md5.ComputeHash(data));

                    if (hashes.TryGetValue(id, out var previousHash))
                    {
                        if (previousHash == hash)
                        {
                            report.IdenticalDuplicates++;
                            return;
                        }

                        report.DifferingDuplicates.Add($"{id:X8}: {written[id]} was replaced by {source}");
                    }

                    hashes[id] = hash;
                    written[id] = source;

                    if (writer.Save(id, data) == DatSaveOutcome.Added)
                        report.Added++;
                    else
                        report.Replaced++;
                }

                if (generatedDate != 0)
                    writer.RecordDate = generatedDate;

                foreach (var record in generated ?? Enumerable.Empty<KeyValuePair<uint, byte[]>>())
                    Write(record.Key, record.Value, $"{record.Key:X8} {GeneratedSource}");

                foreach (var (folder, date) in steps)
                {
                    if (!Directory.Exists(folder))
                        throw new DirectoryNotFoundException(folder);

                    if (date != 0)
                        writer.RecordDate = date;

                    var files = Directory.EnumerateFiles(folder, "*.bin", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                    foreach (var file in files)
                    {
                        var name = Path.GetFileName(file);
                        var data = File.ReadAllBytes(file);
                        uint id;

                        if (name.StartsWith("08", StringComparison.Ordinal))
                        {
                            if (!TryReadFileNameId(name, out id))
                                throw new InvalidDataException($"{file}: a surface file's name must start with its 8-digit hex id.");
                        }
                        else
                        {
                            if (data.Length < 4)
                            {
                                report.Skipped.Add($"{file}: the file is shorter than 4 bytes");
                                continue;
                            }

                            id = BitConverter.ToUInt32(data, 0);

                            if (!TryReadFileNameId(name, out var nameId) || nameId != id)
                                report.NameMismatches.Add($"{file}: written as {id:X8} (the id in its data)");
                        }

                        Write(id, data, file);
                    }
                }
            }

            return report;
        }

        private static bool TryReadFileNameId(string name, out uint id)
        {
            id = 0;

            if (name.Length < 8)
                return false;

            return uint.TryParse(name.Substring(0, 8), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out id);
        }
    }
}
