using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatOverlayTests
    {
        private static readonly string SourceDats = TestSourceDats.Folder;

        private static string CopyOfDat(string name)
        {
            var source = TestSourceDats.Dat(name);

            var copy = TestSourceDats.TempPath("DatOverlayTests", ".dat");
            File.Copy(source, copy);
            return copy;
        }

        private static string NewTempFolder()
        {
            var folder = TestSourceDats.TempPath("DatOverlayTests");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static byte[] Record(uint id, int length, int seed)
        {
            var data = new byte[length];
            new Random(seed).NextBytes(data);
            Array.Copy(BitConverter.GetBytes(id), data, 4);
            return data;
        }

        [TestMethod]
        public void SyntheticFolders_AddReplaceConflictAndDuplicates()
        {
            var path = CopyOfDat("language - 2015-06-18 - Iteration 994 - EoR.dat");
            var one = NewTempFolder();
            var two = NewTempFolder();

            try
            {
                var existing = new DatDatabase(path).AllFiles.Keys.Where(DatOverlay.IsLanguageId).OrderBy(id => id).First();

                var newId = 0x23FFF001u;
                var dupId = 0x23FFF003u;
                var differId = 0x23FFF004u;
                var conflictId = 0x23FFF005u;

                File.WriteAllBytes(Path.Combine(one, $"{newId:X8} - added.bin"), Record(newId, 300, 1));
                File.WriteAllBytes(Path.Combine(one, $"{existing:X8}.bin"), Record(existing, 2500, 2));
                File.WriteAllBytes(Path.Combine(one, $"{dupId:X8}.bin"), Record(dupId, 100, 3));
                File.WriteAllBytes(Path.Combine(one, $"{differId:X8}.bin"), Record(differId, 100, 4));
                File.WriteAllBytes(Path.Combine(one, "notes.txt"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(one, "tiny.bin"), new byte[] { 1, 2, 3 });

                File.WriteAllBytes(Path.Combine(two, $"{dupId:X8}.bin"), Record(dupId, 100, 3));
                File.WriteAllBytes(Path.Combine(two, $"{differId:X8}.bin"), Record(differId, 120, 5));
                File.WriteAllBytes(Path.Combine(two, $"{conflictId:X8}.bin"), Record(conflictId + 1, 100, 6));

                DatOverlayReport report;
                using (var writer = new DatWriter(path))
                {
                    report = DatOverlay.Apply(writer, new[] { one, two });
                    writer.Audit();

                    CollectionAssert.AreEqual(Record(existing, 2500, 2), writer.Read(existing));
                    CollectionAssert.AreEqual(Record(differId, 120, 5), writer.Read(differId));
                    CollectionAssert.AreEqual(Record(newId, 300, 1), writer.Read(newId));
                    Assert.IsFalse(writer.Contains(conflictId));
                    CollectionAssert.AreEqual(Record(conflictId + 1, 100, 6), writer.Read(conflictId + 1));
                }

                Assert.AreEqual(1, report.NameMismatches.Count);
                Assert.AreEqual(1, report.IdenticalDuplicates);
                Assert.AreEqual(1, report.DifferingDuplicates.Count);
                Assert.AreEqual(2, report.Replaced);
                Assert.AreEqual(1, report.Skipped.Count);
                Assert.AreEqual(4, report.Added);
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(one, true);
                Directory.Delete(two, true);
            }
        }

        [TestMethod]
        public void SurfaceName_UsesTheFileNameId()
        {
            var path = CopyOfDat("portal - 2017-01-29 - Iteration 2072 - EoR.dat");
            var folder = NewTempFolder();

            try
            {
                var id = 0x08FFF001u;
                var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                File.WriteAllBytes(Path.Combine(folder, $"{id:X8}.bin"), data);

                using (var writer = new DatWriter(path))
                {
                    var report = DatOverlay.Apply(writer, new[] { folder }, DatOverlay.IsPortalId);

                    Assert.AreEqual(1, report.Added);
                    CollectionAssert.AreEqual(data, writer.Read(id));
                }
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void RejectedId_IsNotWritten()
        {
            var path = CopyOfDat("language - 2015-06-18 - Iteration 994 - EoR.dat");
            var folder = NewTempFolder();

            try
            {
                var id = 0x01FFF001u;
                File.WriteAllBytes(Path.Combine(folder, $"{id:X8}.bin"), Record(id, 50, 7));

                using (var writer = new DatWriter(path))
                {
                    var report = DatOverlay.Apply(writer, new[] { folder }, DatOverlay.IsLanguageId);

                    Assert.AreEqual(0, report.Written);
                    Assert.AreEqual(1, report.Refused);
                    Assert.IsFalse(writer.Contains(id));
                }
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(folder, true);
            }
        }
    }
}
