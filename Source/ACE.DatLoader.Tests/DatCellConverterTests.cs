using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatCellConverterTests
    {
        private static readonly string SourceDats = Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats\Source";

        private const string PreToDCell = "cell - 2005-02-XX - Iteration 1593 - 100% - Admin.dat";
        private const string ToDCell = "cell - 2005-06-02 - Iteration 4 - 100% - Throne of Destiny.dat";
        private const string SurfaceMap = "surface_map_preToD_2005-02_to_ToD.tsv";

        private static string RequireSource(string name)
        {
            var path = Path.Combine(SourceDats, name);

            if (!File.Exists(path))
                Assert.Inconclusive($"Source dat not found: {path}");

            return path;
        }

        [TestMethod]
        public void ConvertEnvCell_DropsPaddingPrependsIdAndMapsSurfaces()
        {
            // flags 0x0A (static objects + restriction), cell 0x01230105, 3 surfaces, 1 portal, 3 visible cells, 1 static object
            var retail = new List<byte>();
            void U32(uint v) => retail.AddRange(BitConverter.GetBytes(v));
            void U16(ushort v) => retail.AddRange(BitConverter.GetBytes(v));
            void Pad() { while (retail.Count % 4 != 0) retail.Add(0); }

            U32(0x0A);
            U32(0x01230105);
            retail.Add(3);
            retail.Add(1);
            U16(3);
            U16(0x0475);
            U16(0x1111);
            U16(0x047E);
            Pad();
            U16(0x0100);
            U16(0x0200);
            retail.AddRange(Enumerable.Range(1, 28).Select(i => (byte)i));
            for (var i = 0; i < 8; i++)
                retail.Add((byte)(0x50 + i));
            Pad();
            U16(1);
            U16(2);
            U16(3);
            Pad();
            U32(1);
            retail.AddRange(Enumerable.Range(100, 32).Select(i => (byte)i));
            Pad();
            U32(0xCAFE);

            var map = new Dictionary<ushort, ushort> { { 0x0475, 0x00DD }, { 0x047E, 0x0034 } };
            var unmapped = new List<ushort>();

            var result = DatCellConverter.ConvertEnvCell(0x01230105, retail.ToArray(), map, unmapped.Add);

            var expected = new List<byte>();
            expected.AddRange(BitConverter.GetBytes(0x01230105u));
            expected.AddRange(BitConverter.GetBytes(0x0Au));
            expected.AddRange(BitConverter.GetBytes(0x01230105u));
            expected.Add(3);
            expected.Add(1);
            expected.AddRange(BitConverter.GetBytes((ushort)3));
            expected.AddRange(BitConverter.GetBytes((ushort)0x00DD));
            expected.AddRange(BitConverter.GetBytes(DatCellConverter.PlaceholderSurface));
            expected.AddRange(BitConverter.GetBytes((ushort)0x0034));
            expected.AddRange(BitConverter.GetBytes((ushort)0x0100));
            expected.AddRange(BitConverter.GetBytes((ushort)0x0200));
            expected.AddRange(Enumerable.Range(1, 28).Select(i => (byte)i));
            for (var i = 0; i < 8; i++)
                expected.Add((byte)(0x50 + i));
            expected.AddRange(BitConverter.GetBytes((ushort)1));
            expected.AddRange(BitConverter.GetBytes((ushort)2));
            expected.AddRange(BitConverter.GetBytes((ushort)3));
            expected.AddRange(BitConverter.GetBytes(1u));
            expected.AddRange(Enumerable.Range(100, 32).Select(i => (byte)i));
            expected.AddRange(BitConverter.GetBytes(0xCAFEu));

            CollectionAssert.AreEqual(expected, result);
            CollectionAssert.AreEqual(new List<ushort> { 0x1111 }, unmapped);
        }

        [TestMethod]
        public void ConvertEnvCell_RejectsTruncatedRecord()
        {
            var bytes = new byte[] { 0, 0, 0, 0, 5, 1, 0x23, 1, 1, 0, 0, 0 };

            Assert.ThrowsExactly<InvalidDataException>(() => DatCellConverter.ConvertEnvCell(0x01230105, bytes, new Dictionary<ushort, ushort>()));
        }

        [TestMethod]
        public void ConvertEnvCell_MatchesToDCellDat_ForASample()
        {
            var pre = new DatDatabase(RequireSource(PreToDCell), true);
            var tod = new DatDatabase(RequireSource(ToDCell), true);
            var map = DatCellConverter.LoadSurfaceMap(RequireSource(SurfaceMap));

            var sample = pre.AllFiles.Keys.Where(id => (id & 0xFFFF) >= 0x100 && (id & 0xFFFF) < 0xFFFE && tod.AllFiles.ContainsKey(id)).Where((id, i) => i % 97 == 0).ToList();

            Assert.IsTrue(sample.Count > 1000);

            var identical = 0;

            foreach (var id in sample)
            {
                var converted = DatCellConverter.ConvertEnvCell(id, pre.GetReaderForFile(id).Buffer, map);

                if (converted.SequenceEqual(tod.GetReaderForFile(id).Buffer))
                    identical++;
            }

            // Measured 98.9% over all cells; the rest are cells whose content differs between the two builds.
            Assert.IsTrue(identical >= sample.Count * 0.97, $"{identical} of {sample.Count} identical");
        }

        [TestMethod]
        public void CreateEmpty_ProducesAnAuditableDatThatAcceptsRecords()
        {
            var template = RequireSource(ToDCell);
            var path = Path.Combine(Path.GetTempPath(), $"emptydat-{Guid.NewGuid():N}.dat");

            try
            {
                DatWriter.CreateEmpty(path, template);

                using (var writer = new DatWriter(path))
                {
                    Assert.AreEqual(0, writer.Audit().Records);

                    for (uint i = 1; i <= 300; i++)
                        writer.Save(0x01000000 + i * 0x10000 + 0x100, Enumerable.Range(0, 700).Select(n => (byte)(n + i)).ToArray());

                    var audit = writer.Audit();
                    Assert.AreEqual(300, audit.Records);
                    Assert.AreEqual(0u, audit.OrphanedBlocks);
                    CollectionAssert.AreEqual(Enumerable.Range(0, 700).Select(n => (byte)(n + 7)).ToArray(), writer.Read(0x01000000 + 7 * 0x10000 + 0x100));
                }

                var reopened = new DatDatabase(path);
                Assert.AreEqual(300, reopened.AllFiles.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private const string EoRPortal = "portal - 2017-01-29 - Iteration 2072 - EoR.dat";

        private static byte[] ToDCellWithPortals(uint id, ushort environment, ushort structure, params ushort[] polygons)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(id);
                w.Write(0u);
                w.Write(id);
                w.Write((byte)1);
                w.Write((byte)polygons.Length);
                w.Write((ushort)0);
                w.Write((ushort)0x032A);
                w.Write(environment);
                w.Write(structure);
                w.Write(new byte[28]);

                foreach (var polygon in polygons)
                {
                    w.Write((ushort)0);
                    w.Write(polygon);
                    w.Write((ushort)0xFFFF);
                    w.Write((ushort)0);
                }

                return ms.ToArray();
            }
        }

        [TestMethod]
        public void CheckStore_ReportsACellOfAReshapedStructure_AndChangesNothing()
        {
            var portal = RequireSource(EoRPortal);
            var reshaped = 0x01010105u;
            var fitting = 0x01010106u;
            var oldCell = ToDCellWithPortals(reshaped, 0x02FB, 4, 0x0B, 0x0C);
            var newCell = ToDCellWithPortals(fitting, 0x02FB, 4, 0x0D, 0x0E);
            var store = new DatCellStore(new SortedDictionary<uint, byte[]> { [reshaped] = oldCell, [fitting] = newCell });
            var report = new DatCellConversionReport();

            DatCellConverter.CheckStore(store, portal, report);

            Assert.AreEqual(1, report.PortalsUnfixed.Count, string.Join("; ", report.PortalsUnfixed));
            StringAssert.StartsWith(report.PortalsUnfixed[0], "01010105");
            Assert.AreEqual(0, report.MissingEnvironments.Count);

            store.TryGet(reshaped, out var afterOld);
            CollectionAssert.AreEqual(oldCell, afterOld);
            store.TryGet(fitting, out var afterNew);
            CollectionAssert.AreEqual(newCell, afterNew);
        }

        [TestMethod]
        public void CheckStore_ListsACellWhosePortalPolygonsFitNothing()
        {
            var portal = RequireSource(EoRPortal);
            var id = 0x01010105u;
            var original = ToDCellWithPortals(id, 0x02FB, 4, 0x0B, 0x7F);
            var store = new DatCellStore(new SortedDictionary<uint, byte[]> { [id] = original });
            var report = new DatCellConversionReport();

            DatCellConverter.CheckStore(store, portal, report);

            Assert.AreEqual(1, report.PortalsUnfixed.Count);
            store.TryGet(id, out var after);
            CollectionAssert.AreEqual(original, after);
        }
    }
}
