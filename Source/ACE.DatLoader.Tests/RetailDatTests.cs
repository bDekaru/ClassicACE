using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class RetailDatTests
    {
        private static readonly string DatsRoot = Environment.GetEnvironmentVariable("ACE_TEST_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats";

        private static string RequireFile(params string[] parts)
        {
            var path = TestSourceDats.Require(Path.Combine(new[] { DatsRoot }.Concat(parts).ToArray()));

            return path;
        }

        private static void AssertAllFilesReadable(DatDatabase dat)
        {
            foreach (var file in dat.AllFiles.Values)
            {
                var reader = dat.GetReaderForFile(file.ObjectId);

                Assert.IsNotNull(reader, $"{file.ObjectId:X8}");
                Assert.AreEqual((int)file.FileSize, reader.Buffer.Length, $"{file.ObjectId:X8}");
            }
        }

        [TestMethod]
        public void RetailPortalDat_IsDetectedAndReadable()
        {
            var dat = new DatDatabase(RequireFile("Release", "portal.dat"));

            Assert.IsTrue(dat.Header.IsRetail);
            Assert.AreEqual(1024u, dat.Header.BlockSize);
            Assert.IsTrue(dat.AllFiles.Count > 0);
            Assert.IsTrue(dat.Iteration > 0);

            AssertAllFilesReadable(dat);
        }

        [TestMethod]
        public void RetailCellDat_IsDetectedAndReadable()
        {
            var dat = new DatDatabase(RequireFile("Release", "cell.dat"));

            Assert.IsTrue(dat.Header.IsRetail);
            Assert.AreEqual(256u, dat.Header.BlockSize);
            Assert.IsTrue(dat.AllFiles.Count > 0);

            AssertAllFilesReadable(dat);
        }

        [TestMethod]
        public void TodPortalDat_IsNotRetail()
        {
            var dat = new DatDatabase(RequireFile("CustomDM", "client_portal.dat"));

            Assert.IsFalse(dat.Header.IsRetail);
            Assert.AreEqual(DatDatabaseType.Portal, dat.Header.DataSet);
            Assert.IsTrue(dat.AllFiles.Count > 0);
        }
    }
}
