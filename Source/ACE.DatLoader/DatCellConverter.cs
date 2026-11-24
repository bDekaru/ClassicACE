using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ACE.DatLoader
{
    public class DatCellConversionReport
    {
        public int LandBlocks { get; set; }
        public int LandBlockInfos { get; set; }
        public int EnvCells { get; set; }

        /// <summary>Records whose id is none of the three kinds; they are not written.</summary>
        public List<string> Skipped { get; } = new List<string>();

        /// <summary>Pre-ToD surface ids used by cells that the map does not know; they were replaced by <see cref="DatCellConverter.PlaceholderSurface"/>, with the number of uses.</summary>
        public SortedDictionary<ushort, int> UnmappedSurfaces { get; } = new SortedDictionary<ushort, int>();

        /// <summary>Cells that could not be converted (the record does not parse); not written.</summary>
        public List<string> BadCells { get; } = new List<string>();

        /// <summary>Cells whose portal polygon ids are not portals of their cell structure in the target portal dat (the client crashes on such a cell). Written unchanged.</summary>
        public List<string> PortalsUnfixed { get; } = new List<string>();

        /// <summary>Cells whose environment or cell structure is missing from the target portal dat. Written unchanged.</summary>
        public List<string> MissingEnvironments { get; } = new List<string>();

        public string ToText()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"cell conversion: {LandBlocks} landblocks, {LandBlockInfos} landblock infos, {EnvCells} environment cells written");
            sb.AppendLine($"  skipped records: {Skipped.Count}, cells that did not parse: {BadCells.Count}, unmapped surface ids: {UnmappedSurfaces.Count}");

            sb.AppendLine($"  portal lists that do not fit their cell structure: {PortalsUnfixed.Count}, cells with an environment or structure missing from the portal dat: {MissingEnvironments.Count}");

            foreach (var s in PortalsUnfixed)
                sb.AppendLine($"  portals unfixed: {s}");

            foreach (var s in MissingEnvironments)
                sb.AppendLine($"  missing environment: {s}");

            foreach (var s in Skipped.Take(20))
                sb.AppendLine($"  skipped: {s}");

            foreach (var s in BadCells.Take(50))
                sb.AppendLine($"  bad cell: {s}");

            foreach (var pair in UnmappedSurfaces)
                sb.AppendLine($"  unmapped surface {pair.Key:X4}: {pair.Value} uses, replaced by {DatCellConverter.PlaceholderSurface:X4}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// Converts a pre-ToD (retail layout) cell dat into a ToD-layout one. Measured against the 2005-06-02 ToD cell dat (see the original-executables notes):
    /// landblock (xxxxFFFF) and landblock info (xxxxFFFE) records are the same bytes in both formats; an environment cell loses the 4-byte alignment the old reader applied
    /// after the surface list, the portals, the visible-cell list and the static-object list, gains a leading file-id dword, and has its surface ids (ushort, 0x08xxxxxx in the portal dat)
    /// renumbered through a map, because the portal dat was renumbered between the two builds.
    /// </summary>
    public static class DatCellConverter
    {
        /// <summary>Surface that replaces an id the map does not know (the same placeholder Melt uses).</summary>
        public const ushort PlaceholderSurface = 0x032A;

        private const int FrameBytes = 28;
        private const int StabBytes = 32;
        private const int PortalBytes = 8;

        /// <summary>Reads lines of "pre-id ToD-id ..." (hex, tab or space separated); other lines are ignored.</summary>
        public static Dictionary<ushort, ushort> LoadSurfaceMap(string path)
        {
            using (var reader = new StreamReader(path))
                return LoadSurfaceMap(reader);
        }

        public static Dictionary<ushort, ushort> LoadSurfaceMap(TextReader lines)
        {
            var map = new Dictionary<ushort, ushort>();
            string line;

            while ((line = lines.ReadLine()) != null)
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 2
                    && ushort.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var from)
                    && ushort.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var to))
                    map[from] = to;
            }

            return map;
        }

        /// <summary>
        /// Converts one retail environment cell record to the ToD layout.
        /// </summary>
        /// <param name="unmapped">Called with every surface id the map does not contain.</param>
        /// <exception cref="InvalidDataException">The record does not have the retail layout.</exception>
        public static byte[] ConvertEnvCell(uint cellId, byte[] retail, IReadOnlyDictionary<ushort, ushort> surfaceMap, Action<ushort> unmapped = null)
        {
            var p = 0;

            uint U32() { Need(4); var v = BitConverter.ToUInt32(retail, p); p += 4; return v; }
            ushort U16() { Need(2); var v = BitConverter.ToUInt16(retail, p); p += 2; return v; }
            void Need(int n) { if (p + n > retail.Length) throw new InvalidDataException("the record ends early"); }
            void Align() { p = (p + 3) & ~3; }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                var flags = U32();
                var id = U32();

                if (id != cellId)
                    throw new InvalidDataException($"the record holds cell id {id:X8}");

                Need(4);
                int numSurfaces = retail[p++];
                int numPortals = retail[p++];
                int numCells = U16();

                w.Write(cellId);
                w.Write(flags);
                w.Write(cellId);
                w.Write((byte)numSurfaces);
                w.Write((byte)numPortals);
                w.Write((ushort)numCells);

                for (var i = 0; i < numSurfaces; i++)
                {
                    var surface = U16();

                    if (!surfaceMap.TryGetValue(surface, out var mapped))
                    {
                        unmapped?.Invoke(surface);
                        mapped = PlaceholderSurface;
                    }

                    w.Write(mapped);
                }

                Align();

                Need(4 + FrameBytes);
                w.Write(retail, p, 4 + FrameBytes);
                p += 4 + FrameBytes;

                Need(numPortals * PortalBytes);
                w.Write(retail, p, numPortals * PortalBytes);
                p += numPortals * PortalBytes;
                Align();

                Need(numCells * 2);
                w.Write(retail, p, numCells * 2);
                p += numCells * 2;
                Align();

                if ((flags & 2) != 0)
                {
                    var count = (int)U32();

                    if (count < 0)
                        throw new InvalidDataException("negative static object count");

                    w.Write(count);
                    Need(count * StabBytes);
                    w.Write(retail, p, count * StabBytes);
                    p += count * StabBytes;
                }

                Align();

                if ((flags & 8) != 0)
                    w.Write(U32());

                if (p != retail.Length)
                    throw new InvalidDataException($"{retail.Length - p} bytes left over");

                return ms.ToArray();
            }
        }

        private struct CellParts
        {
            public uint EnvironmentId;
            public ushort Structure;
            public int PortalsAt;
            public int PortalCount;
            public byte[] Data;

            public ushort PolygonId(int i) => BitConverter.ToUInt16(Data, PortalsAt + i * PortalBytes + 2);
        }

        private static CellParts Parts(byte[] cell)
        {
            var surfaces = cell[12];
            var at = 16 + 2 * surfaces;

            return new CellParts
            {
                Data = cell,
                EnvironmentId = 0x0D000000u | BitConverter.ToUInt16(cell, at),
                Structure = BitConverter.ToUInt16(cell, at + 2),
                PortalsAt = at + 4 + FrameBytes,
                PortalCount = cell[13],
            };
        }

        private class PortalChecker
        {
            private readonly DatDatabase portal;
            private readonly Dictionary<uint, ACE.DatLoader.FileTypes.Environment> environments = new Dictionary<uint, ACE.DatLoader.FileTypes.Environment>();

            public PortalChecker(string portalDat)
            {
                portal = new DatDatabase(portalDat, true);
            }

            /// <summary>The cell structure's portal polygon ids in the target portal dat, or null when the environment or structure is not there.</summary>
            private List<ushort> PortalPolygons(uint environmentId, ushort structure)
            {
                if (!environments.TryGetValue(environmentId, out var environment))
                {
                    environment = null;

                    if (portal.AllFiles.ContainsKey(environmentId))
                    {
                        using (var ms = new MemoryStream(portal.GetReaderForFile(environmentId).Buffer))
                        using (var reader = new BinaryReader(ms))
                        {
                            var parsed = new ACE.DatLoader.FileTypes.Environment();
                            parsed.Unpack(reader);
                            environment = parsed;
                        }
                    }

                    environments[environmentId] = environment;
                }

                if (environment == null || !environment.Cells.TryGetValue(structure, out var cellStruct))
                    return null;

                return cellStruct.Portals;
            }

            /// <param name="known">False when the cell's environment or structure is not in the portal dat; Fits is then true (nothing to compare with).</param>
            public bool Fits(CellParts cell, out bool known)
            {
                var polygons = PortalPolygons(cell.EnvironmentId, cell.Structure);
                known = polygons != null;

                if (!known)
                    return true;

                for (var i = 0; i < cell.PortalCount; i++)
                    if (!polygons.Contains(cell.PolygonId(i)))
                        return false;

                return true;
            }
        }

        /// <summary>
        /// Runs the portal-list check of the conversion over every environment cell of a store, after map edits have moved cells between landblocks and dats.
        /// Cells that do not fit are listed in the report and left as they are.
        /// </summary>
        public static void CheckStore(DatCellStore store, string portalDat, DatCellConversionReport report)
        {
            var checker = new PortalChecker(portalDat);

            foreach (var record in store.Records.Where(r => DatCellStore.IsCell(r.Key) && (r.Key >> 16) != 0xFFFF && (r.Key & 0xFFFF) >= 0x100))
                CheckPortals(record.Key, record.Value, checker, report);
        }

        private static void CheckPortals(uint id, byte[] cell, PortalChecker checker, DatCellConversionReport report)
        {
            var parts = Parts(cell);

            if (checker.Fits(parts, out var known))
            {
                if (!known)
                    report.MissingEnvironments.Add($"{id:X8}: environment {parts.EnvironmentId:X8} structure {parts.Structure}");

                return;
            }

            report.PortalsUnfixed.Add($"{id:X8}: environment {parts.EnvironmentId & 0xFFFF:X4} structure {parts.Structure}");
        }

        /// <summary>
        /// Writes a new ToD-layout cell dat at <paramref name="outputPath"/> holding every landblock, landblock info and environment cell of the retail-layout dat.
        /// </summary>
        /// <param name="portalDat">The portal dat the cell dat will be used with. When given, each cell's portal polygon ids are checked against its cell structure there.
        /// Cell structures were reshaped between the builds (environments 0574, 0575, 02FB, 05BF), and a cell whose portal polygon is not a portal of its structure crashes the client;
        /// the portal build replaces those buildings with their pre-ToD versions, so a cell that still does not fit is only reported.</param>
        /// <param name="recordDate">The date every record carries (Unix time); 0 uses the current time.</param>
        /// <param name="iterationForDate">Gives each record's entry iteration from its date; null leaves them 0.</param>
        public static DatCellConversionReport Convert(string retailCellDat, string outputPath, IReadOnlyDictionary<ushort, ushort> surfaceMap, string portalDat = null, uint recordDate = 0, Func<uint, uint> iterationForDate = null)
        {
            var report = new DatCellConversionReport();

            DatWriter.CreateEmpty(outputPath, DatContainerSpec.Cell);

            try
            {
                using (var writer = new DatWriter(outputPath) { RecordDate = recordDate, IterationForDate = iterationForDate })
                {
                    ConvertRecords(retailCellDat, surfaceMap, portalDat, report, (id, data) => writer.Save(id, data));
                    writer.Audit();
                }
            }
            catch
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                throw;
            }

            return report;
        }

        /// <summary>
        /// Converts every record of a retail-layout cell dat and returns the ToD-layout records in memory, in id order, so that map edits can be applied before anything is written.
        /// </summary>
        public static SortedDictionary<uint, byte[]> ConvertToStore(string retailCellDat, IReadOnlyDictionary<ushort, ushort> surfaceMap, DatCellConversionReport report, string portalDat = null)
        {
            var store = new SortedDictionary<uint, byte[]>();

            ConvertRecords(retailCellDat, surfaceMap, portalDat, report, (id, data) => store[id] = data);

            return store;
        }

        private static void ConvertRecords(string retailCellDat, IReadOnlyDictionary<ushort, ushort> surfaceMap, string portalDat, DatCellConversionReport report, Action<uint, byte[]> emit)
        {
            var checker = portalDat == null ? null : new PortalChecker(portalDat);
            var source = new DatDatabase(retailCellDat, true);

            foreach (var id in source.AllFiles.Keys.OrderBy(k => k))
            {
                var low = id & 0xFFFF;
                var data = source.GetReaderForFile(id).Buffer;

                if (data == null || data.Length == 0)
                {
                    report.Skipped.Add($"{id:X8}: empty record");
                    continue;
                }

                if (low == 0xFFFF)
                {
                    emit(id, data);
                    report.LandBlocks++;
                }
                else if (low == 0xFFFE)
                {
                    emit(id, data);
                    report.LandBlockInfos++;
                }
                else if (low >= 0x100 && (id >> 16) != 0xFFFF)
                {
                    try
                    {
                        var converted = ConvertEnvCell(id, data, surfaceMap, s => report.UnmappedSurfaces[s] = report.UnmappedSurfaces.TryGetValue(s, out var n) ? n + 1 : 1);

                        if (checker != null)
                            CheckPortals(id, converted, checker, report);

                        emit(id, converted);
                        report.EnvCells++;
                    }
                    catch (InvalidDataException ex)
                    {
                        report.BadCells.Add($"{id:X8}: {ex.Message}");
                    }
                }
                else
                    report.Skipped.Add($"{id:X8}: not a landblock, landblock info or cell id");
            }
        }
    }
}
