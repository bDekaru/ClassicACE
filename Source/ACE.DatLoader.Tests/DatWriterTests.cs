using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatWriterTests
    {
        private static readonly string SourceDats = Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats\Source";

        private static string CopyOfLanguageDat()
        {
            var source = Path.Combine(SourceDats, "language - 2015-06-18 - Iteration 994 - EoR.dat");

            if (!File.Exists(source))
                Assert.Inconclusive($"Test dat not found: {source}");

            var copy = Path.Combine(Path.GetTempPath(), $"DatWriterTests-{Guid.NewGuid():N}.dat");
            File.Copy(source, copy);
            return copy;
        }

        private static Dictionary<uint, byte[]> ReadAll(string path)
        {
            var dat = new DatDatabase(path);
            return dat.AllFiles.Keys.ToDictionary(id => id, id => dat.GetReaderForFile(id).Buffer);
        }

        private static void AssertSame(Dictionary<uint, byte[]> expected, Dictionary<uint, byte[]> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);

            foreach (var kvp in expected)
            {
                Assert.IsTrue(actual.ContainsKey(kvp.Key), $"{kvp.Key:X8} is missing");
                CollectionAssert.AreEqual(kvp.Value, actual[kvp.Key], $"{kvp.Key:X8} differs");
            }
        }

        private static byte[] RandomBytes(Random random, int length)
        {
            var bytes = new byte[length];
            random.NextBytes(bytes);
            return bytes;
        }

        [TestMethod]
        public void UntouchedDat_AuditsClean()
        {
            var path = CopyOfLanguageDat();

            try
            {
                using (var writer = new DatWriter(path))
                {
                    var audit = writer.Audit();
                    Assert.IsTrue(audit.Records > 0);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void WithoutARecordDate_AReplacementKeepsItsDateAndIteration()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var existing = ReadAll(path).Keys.First();
                var before = new DatDatabase(path).AllFiles[existing];

                using (var writer = new DatWriter(path))
                {
                    writer.Save(existing, new byte[] { 5, 6, 7, 8 });
                    writer.Audit();
                }

                var after = new DatDatabase(path).AllFiles[existing];
                Assert.AreEqual(before.Date, after.Date);
                Assert.AreEqual(before.Iteration, after.Iteration);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void RecordDate_AppliesToNewAndReplacedRecords_AndGivesTheIteration()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var date = DatStepDate.Parse("2026-11-16");
                var other = DatStepDate.Parse("2000-01-01");
                var existing = ReadAll(path).Keys.First();

                using (var writer = new DatWriter(path) { RecordDate = date, IterationForDate = d => d == date ? 20045u : 0u })
                {
                    writer.Save(0x7F000001, new byte[] { 1, 2, 3, 4 });
                    writer.Save(existing, new byte[] { 5, 6, 7, 8 });
                    writer.Save(0x7F000002, new byte[] { 9, 9, 9, 9 }, date: other);
                    writer.Audit();
                }

                var after = new DatDatabase(path);
                Assert.AreEqual(date, after.AllFiles[0x7F000001].Date);
                Assert.AreEqual(20045u, after.AllFiles[0x7F000001].Iteration);
                Assert.AreEqual(date, after.AllFiles[existing].Date);
                Assert.AreEqual(20045u, after.AllFiles[existing].Iteration);
                Assert.AreEqual(other, after.AllFiles[0x7F000002].Date);
                Assert.AreEqual(0u, after.AllFiles[0x7F000002].Iteration);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void IterationRecord_HoldsOneRunFromOneToTheTotal()
        {
            var path = CopyOfLanguageDat();

            try
            {
                using (var writer = new DatWriter(path))
                {
                    writer.SaveIterationRecord(20012, DatStepDate.Parse("2026-11-16"));
                    writer.Audit();
                }

                var db = new DatDatabase(path);
                Assert.AreEqual(20012, db.Iteration);

                var entry = db.AllFiles[DatWriter.IterationRecordId];
                Assert.AreEqual(1u, entry.Iteration);
                CollectionAssert.AreEqual(new byte[] { 0x2C, 0x4E, 0, 0, 0xD4, 0xB1, 0xFF, 0xFF, 1, 0, 0, 0 }, db.GetReaderForFile(DatWriter.IterationRecordId).Buffer);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ReplaceRecords_SmallerSameAndLarger_ReadBack()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var expected = ReadAll(path);
                var random = new Random(1);
                var ids = expected.Keys.Take(6).ToList();

                using (var writer = new DatWriter(path))
                {
                    expected[ids[0]] = RandomBytes(random, 10);
                    expected[ids[1]] = RandomBytes(random, expected[ids[1]].Length);
                    expected[ids[2]] = RandomBytes(random, expected[ids[2]].Length + 5000);
                    expected[ids[3]] = RandomBytes(random, 1020);
                    expected[ids[4]] = RandomBytes(random, 1021);
                    expected[ids[5]] = RandomBytes(random, 1);

                    foreach (var id in ids)
                        Assert.AreEqual(DatSaveOutcome.Replaced, writer.Save(id, expected[id]));

                    writer.Audit();
                }

                AssertSame(expected, ReadAll(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void AddManyRecords_SplitsTheDirectoryAndGrowsTheFile()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var expected = ReadAll(path);
                var sizeBefore = new FileInfo(path).Length;
                var random = new Random(2);
                var added = new List<uint>();

                using (var writer = new DatWriter(path))
                {
                    while (added.Count < 4000)
                    {
                        var id = 0x23000000u + (uint)random.Next(1, 0x00FFFFFF);
                        if (expected.ContainsKey(id))
                            continue;

                        expected[id] = RandomBytes(random, random.Next(1, 3000));
                        added.Add(id);

                        Assert.AreEqual(DatSaveOutcome.Added, writer.Save(id, expected[id]));
                    }

                    var audit = writer.Audit();
                    Assert.AreEqual(expected.Count, audit.Records);
                }

                Assert.IsTrue(new FileInfo(path).Length > sizeBefore);
                AssertSame(expected, ReadAll(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void LargePortalDat_ReplaceAndAdd_AuditsCleanAndReadsBack()
        {
            var source = Path.Combine(SourceDats, "portal - 2017-01-29 - Iteration 2072 - EoR.dat");

            if (!File.Exists(source))
                Assert.Inconclusive($"Test dat not found: {source}");

            var path = Path.Combine(Path.GetTempPath(), $"DatWriterTests-{Guid.NewGuid():N}.dat");
            File.Copy(source, path);

            try
            {
                var random = new Random(3);
                var changed = new Dictionary<uint, byte[]>();
                Dictionary<uint, int> originalSizes;

                var before = new DatDatabase(path);
                originalSizes = before.AllFiles.ToDictionary(kvp => kvp.Key, kvp => (int)kvp.Value.FileSize);
                var existing = before.AllFiles.Keys.OrderBy(id => id).Where((id, i) => i % 97 == 0).Take(300).ToList();

                using (var writer = new DatWriter(path))
                {
                    var baseline = writer.Audit();
                    Assert.AreEqual(originalSizes.Count, baseline.Records);

                    foreach (var id in existing)
                    {
                        var data = RandomBytes(random, random.Next(1, 20000));
                        changed[id] = data;
                        Assert.AreEqual(DatSaveOutcome.Replaced, writer.Save(id, data));
                    }

                    for (var i = 0; i < 300; i++)
                    {
                        var id = 0x0F000000u + (uint)random.Next(0x100000, 0x7FFFFF);
                        if (originalSizes.ContainsKey(id) || changed.ContainsKey(id))
                            continue;

                        var data = RandomBytes(random, random.Next(1, 20000));
                        changed[id] = data;
                        Assert.AreEqual(DatSaveOutcome.Added, writer.Save(id, data));
                    }

                    var audit = writer.Audit();
                    Assert.AreEqual(originalSizes.Count + changed.Count - existing.Count, audit.Records);

                    foreach (var kvp in changed)
                        CollectionAssert.AreEqual(kvp.Value, writer.Read(kvp.Key), $"{kvp.Key:X8}");
                }

                var after = new DatDatabase(path);
                Assert.AreEqual(originalSizes.Count + changed.Count - existing.Count, after.AllFiles.Count);

                foreach (var kvp in changed)
                    CollectionAssert.AreEqual(kvp.Value, after.GetReaderForFile(kvp.Key).Buffer, $"{kvp.Key:X8}");

                foreach (var id in originalSizes.Keys.Where(id => !changed.ContainsKey(id)).Where((id, i) => i % 500 == 0))
                    Assert.AreEqual(originalSizes[id], (int)after.AllFiles[id].FileSize, $"{id:X8}");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void OlderIteration_IsRefused()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var expected = ReadAll(path);
                var id = expected.Keys.First();

                using (var writer = new DatWriter(path))
                {
                    Assert.AreEqual(DatSaveOutcome.Replaced, writer.Save(id, new byte[] { 1, 2, 3 }, iteration: 50));
                    Assert.AreEqual(DatSaveOutcome.RefusedOlderIteration, writer.Save(id, new byte[] { 9 }, iteration: 40));
                    CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, writer.Read(id));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void Replace_KeepsVersionIterationAndDate_NewRecordGetsDefaultVersion()
        {
            var path = CopyOfLanguageDat();

            try
            {
                var id = ReadAll(path).Keys.First();
                const uint newId = 0x23FFFF00;

                using (var writer = new DatWriter(path))
                {
                    Assert.IsTrue(writer.TryGetEntryInfo(id, out var version, out var iteration, out var date));

                    writer.Save(id, new byte[] { 1, 2, 3 });
                    Assert.IsTrue(writer.TryGetEntryInfo(id, out var v2, out var i2, out var d2));
                    Assert.AreEqual(version, v2);
                    Assert.AreEqual(iteration, i2);
                    Assert.AreEqual(date, d2);

                    writer.Save(id, new byte[] { 4 }, iteration: iteration + 1, version: 7, date: 123);
                    Assert.IsTrue(writer.TryGetEntryInfo(id, out v2, out i2, out d2));
                    Assert.AreEqual((ushort)7, v2);
                    Assert.AreEqual(iteration + 1, i2);
                    Assert.AreEqual(123u, d2);

                    writer.Save(newId, new byte[] { 5 });
                    Assert.IsTrue(writer.TryGetEntryInfo(newId, out v2, out i2, out d2));
                    Assert.AreEqual(DatWriter.NewRecordVersion, v2);
                    Assert.AreEqual(0u, i2);
                    Assert.AreNotEqual(0u, d2);

                    writer.Audit();
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
