using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatEnvironmentConverterTests
    {
        private static readonly string SourceDats = Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats\Source";

        private const string EoRPortal = "portal - 2017-01-29 - Iteration 2072 - EoR.dat";
        private const string PreToDPortalVariable = "ACE_TEST_PRETOD_PORTAL";

        [TestMethod]
        public void Convert_TruncatedRecord_Throws()
        {
            var record = new byte[] { 0x01, 0x00, 0x00, 0x0D, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };

            Assert.ThrowsExactly<InvalidDataException>(() => DatEnvironmentConverter.Convert(record));
        }

        [TestMethod]
        public void Import_RemovedBuildingModels_ParseAndUseTargetSurfaces()
        {
            var prePath = Environment.GetEnvironmentVariable(PreToDPortalVariable);
            var eorPath = Path.Combine(SourceDats, EoRPortal);
            var mapPath = Path.Combine(SourceDats, "surface_map_preToD_2005-02_to_ToD.tsv");

            if (string.IsNullOrEmpty(prePath) || !File.Exists(prePath) || !File.Exists(eorPath) || !File.Exists(mapPath))
                Assert.Inconclusive($"Needs {PreToDPortalVariable}, {EoRPortal} and the surface map.");

            var target = new DatDatabase(eorPath, true);
            var importer = new DatPortalImporter(new DatDatabase(prePath, true), target, DatCellConverter.LoadSurfaceMap(mapPath));

            for (uint id = 0x01001753; id <= 0x01001756; id++)
                importer.ImportModel(id);

            // 08000959 is not in the surface map, so it is imported with its texture chain under new ids
            Assert.IsTrue(importer.Records.Keys.Any(k => k >> 24 == 0x08));
            Assert.IsTrue(importer.Records.Keys.Any(k => k >> 24 == 0x06));

            foreach (var record in importer.Records.Where(r => r.Key >> 24 == 0x01))
            {
                var model = new FileTypes.GfxObj();

                using (var stream = new MemoryStream(record.Value))
                using (var reader = new BinaryReader(stream))
                {
                    model.Unpack(reader);
                    Assert.AreEqual(record.Value.Length, stream.Position);
                }

                foreach (var surface in model.Surfaces)
                    Assert.IsTrue(target.AllFiles.ContainsKey(surface) || importer.Records.ContainsKey(surface), $"Surface {surface:X8} is neither in the target nor imported.");
            }
        }

        [TestMethod]
        public void ImportTexture_PlainPixelFormats_KeepPixelsInToDFormat()
        {
            var prePath = Environment.GetEnvironmentVariable(PreToDPortalVariable);
            var eorPath = Path.Combine(SourceDats, EoRPortal);
            var mapPath = Path.Combine(SourceDats, "surface_map_preToD_2005-02_to_ToD.tsv");

            if (string.IsNullOrEmpty(prePath) || !File.Exists(prePath) || !File.Exists(eorPath) || !File.Exists(mapPath))
                Assert.Inconclusive($"Needs {PreToDPortalVariable}, {EoRPortal} and the surface map.");

            var pre = new DatDatabase(prePath, true);
            var importer = new DatPortalImporter(pre, new DatDatabase(eorPath, true), DatCellConverter.LoadSurfaceMap(mapPath));

            // pre-ToD format -> ToD pixel format and bytes per pixel
            var expected = new System.Collections.Generic.Dictionary<uint, (uint Format, int Bytes)> { { 4, (26, 2) }, { 7, (23, 2) }, { 10, (20, 3) }, { 11, (28, 1) } };
            var seen = new System.Collections.Generic.HashSet<uint>();

            foreach (var id in pre.AllFiles.Keys.Where(k => (k >> 24) == 0x05))
            {
                var texture = pre.GetReaderForFile(id).Buffer;
                var format = BitConverter.ToUInt32(texture, 4);

                if (!expected.TryGetValue(format, out var want))
                    continue;

                var recordsBefore = importer.Records.Keys.ToHashSet();
                importer.ImportTexture(id);

                var imageId = importer.Records.Keys.Single(k => k >> 24 == 0x06 && !recordsBefore.Contains(k));
                var image = importer.Records[imageId];
                var pixels = BitConverter.ToInt32(texture, 8) * BitConverter.ToInt32(texture, 12);

                Assert.AreEqual(want.Format, BitConverter.ToUInt32(image, 16), $"{id:X8} pixel format");
                Assert.AreEqual(pixels * want.Bytes, BitConverter.ToInt32(image, 20), $"{id:X8} length");
                Assert.IsTrue(image.Skip(24).SequenceEqual(texture.Skip(16).Take(pixels * want.Bytes)), $"{id:X8} pixels");
                seen.Add(format);
            }

            Assert.AreEqual(expected.Count, seen.Count, "Not every plain pixel format occurs in the pre-ToD dat.");
        }

        [TestMethod]
        public void Convert_PreToDEnvironments_MatchEoRRecords()
        {
            var prePath = Environment.GetEnvironmentVariable(PreToDPortalVariable);
            var eorPath = Path.Combine(SourceDats, EoRPortal);

            if (string.IsNullOrEmpty(prePath) || !File.Exists(prePath))
                Assert.Inconclusive($"Set {PreToDPortalVariable} to a pre-ToD portal dat.");

            if (!File.Exists(eorPath))
                Assert.Inconclusive($"Source dat not found: {eorPath}");

            var pre = new DatDatabase(prePath, true);
            var eor = new DatDatabase(eorPath, true);

            var shared = 0;
            var equal = 0;

            foreach (var id in pre.AllFiles.Keys.Where(k => (k >> 24) == 0x0D))
            {
                var converted = DatEnvironmentConverter.Convert(pre.GetReaderForFile(id).Buffer);

                if (!eor.AllFiles.ContainsKey(id))
                    continue;

                shared++;

                if (converted.SequenceEqual(eor.GetReaderForFile(id).Buffer))
                    equal++;
            }

            Assert.IsTrue(shared > 600, $"Only {shared} shared environments.");
            Assert.IsTrue(equal >= shared - 4, $"{equal} of {shared} converted environments equal the EoR record.");
        }
    }
}
