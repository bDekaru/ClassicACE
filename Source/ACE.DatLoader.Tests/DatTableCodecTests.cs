using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum.Properties;

namespace ACE.DatLoader.Tests
{
    /// <summary>
    /// The XP and skill tables are packed back from their decoded form: the bytes must equal the record they were read from.
    /// </summary>
    [TestClass]
    public class DatTableCodecTests
    {
        private static readonly string SourceDats = System.Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats\Source";

        private const uint XpTableId = 0x0E000018;
        private const uint SkillTableId = 0x0E000004;

        private static string CustomFile(params string[] parts)
        {
            var path = Path.Combine(new[] { SourceDats, "Custom Files" }.Concat(parts).ToArray());

            if (!File.Exists(path))
                Assert.Inconclusive($"Test file not found: {path}");

            return path;
        }

        private static byte[] PortalRecord(string datName, uint id)
        {
            var path = Path.Combine(SourceDats, datName);

            if (!File.Exists(path))
                Assert.Inconclusive($"Test dat not found: {path}");

            var dat = new DatDatabase(path);
            return dat.GetReaderForFile(id).Buffer;
        }

        private static void AssertSameBytes(byte[] expected, byte[] actual)
        {
            var common = Math.Min(expected.Length, actual.Length);
            var offset = 0;

            while (offset < common && expected[offset] == actual[offset])
                offset++;

            Assert.IsTrue(offset == common && expected.Length == actual.Length, $"The records differ at offset {offset} (lengths {expected.Length} and {actual.Length}).");
        }

        private static XpTable UnpackXp(byte[] bytes)
        {
            var table = new XpTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        private static SkillTable UnpackSkills(byte[] bytes)
        {
            var table = new SkillTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        [TestMethod]
        public void XpTable_PackEqualsRecord_CustomDMFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"));

            AssertSameBytes(bytes, UnpackXp(bytes).Pack());
        }

        [TestMethod]
        public void XpTable_PackEqualsRecord_InfiltrationFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "Infiltration", "0E000018 - XpTable.bin"));

            // The file holds 149 skill credit values behind the 127 its header counts; neither the reader nor the client uses them.
            const int unusedTail = 149 * 4;

            AssertSameBytes(bytes.Take(bytes.Length - unusedTail).ToArray(), UnpackXp(bytes).Pack());
        }

        [TestMethod]
        public void XpTable_PackEqualsRecord_EoRDat()
        {
            var bytes = PortalRecord("client_portal - EoR.dat", XpTableId);

            AssertSameBytes(bytes, UnpackXp(bytes).Pack());
        }

        [TestMethod]
        public void SkillTable_PackEqualsRecord_CustomDMFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000004 - Skills Table.bin"));

            AssertSameBytes(bytes, UnpackSkills(bytes).Pack());
        }

        [TestMethod]
        public void SkillTable_PackEqualsRecord_InfiltrationFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "Infiltration", "0E000004 - SkillTable.bin"));

            AssertSameBytes(bytes, UnpackSkills(bytes).Pack());
        }

        [TestMethod]
        public void SkillTable_PackEqualsRecord_EoRDat()
        {
            var bytes = PortalRecord("client_portal - EoR.dat", SkillTableId);

            AssertSameBytes(bytes, UnpackSkills(bytes).Pack());
        }

        [TestMethod]
        public void SkillTable_SyntheticTable_SurvivesRoundTrip()
        {
            var table = new SkillTable { BucketSize = 16 };

            table.SkillBaseHash[7] = new SkillBase
            {
                Name = "A",
                Description = "Seven characters",
                IconId = 0x06001234,
                TrainedCost = 5,
                SpecializedCost = 9,
                Category = 2,
                ChargenUse = 1,
                MinLevel = 1,
                Formula = new SkillFormula(PropertyAttribute.Strength, 2, PropertyAttribute.Focus, 3, 4, 1),
                UpperBound = 900,
                LowerBound = 120,
                LearnMod = 1.5
            };
            table.SkillBaseHash[3] = new SkillBase { Name = "Bb", Description = "" };

            var bytes = table.Pack();
            var back = UnpackSkills(bytes);

            Assert.AreEqual(16, back.BucketSize);
            CollectionAssert.AreEqual(new uint[] { 7, 3 }, back.SkillBaseHash.Keys.ToArray());
            Assert.AreEqual("Seven characters", back.SkillBaseHash[7].Description);
            Assert.AreEqual(0x06001234u, back.SkillBaseHash[7].IconId);
            Assert.AreEqual(1.5, back.SkillBaseHash[7].LearnMod);
            Assert.AreEqual(3u, back.SkillBaseHash[7].Formula.Y);
            Assert.AreEqual("Bb", back.SkillBaseHash[3].Name);
            AssertSameBytes(bytes, back.Pack());
        }

        [TestMethod]
        public void XpTable_SyntheticTable_SurvivesRoundTrip()
        {
            var table = new XpTable();
            table.AttributeXpList.AddRange(new uint[] { 0, 10, 30 });
            table.VitalXpList.AddRange(new uint[] { 0, 5 });
            table.TrainedSkillXpList.AddRange(new uint[] { 0, 1, 2, 3 });
            table.SpecializedSkillXpList.AddRange(new uint[] { 0 });
            table.CharacterLevelXPList.AddRange(new ulong[] { 0, 100, 5000000000 });
            table.CharacterLevelSkillCreditList.AddRange(new uint[] { 0, 1, 2 });

            var bytes = table.Pack();
            var back = UnpackXp(bytes);

            CollectionAssert.AreEqual(table.AttributeXpList, back.AttributeXpList);
            CollectionAssert.AreEqual(table.VitalXpList, back.VitalXpList);
            CollectionAssert.AreEqual(table.TrainedSkillXpList, back.TrainedSkillXpList);
            CollectionAssert.AreEqual(table.SpecializedSkillXpList, back.SpecializedSkillXpList);
            CollectionAssert.AreEqual(table.CharacterLevelXPList, back.CharacterLevelXPList);
            CollectionAssert.AreEqual(table.CharacterLevelSkillCreditList, back.CharacterLevelSkillCreditList);
        }

        [TestMethod]
        public void XpTable_MismatchedLevelLists_Throw()
        {
            var table = new XpTable();
            table.CharacterLevelXPList.Add(0);

            Assert.ThrowsExactly<InvalidDataException>(() => table.Pack());
        }

        // ---- rows (the database form) ----

        private static byte[] SkillBytesThroughRows(byte[] bytes)
        {
            var table = UnpackSkills(bytes);
            return DatTables.ToSkillTable(DatTables.ToRows(table), table).Pack();
        }

        private static byte[] XpBytesThroughRows(byte[] bytes)
        {
            return DatTables.ToXpTable(DatTables.ToRows(UnpackXp(bytes))).Pack();
        }

        [TestMethod]
        public void SkillRows_RebuildTheRecord_CustomDMFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000004 - Skills Table.bin"));

            AssertSameBytes(bytes, SkillBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SkillRows_RebuildTheRecord_EoRDat()
        {
            var bytes = PortalRecord("client_portal - EoR.dat", SkillTableId);

            AssertSameBytes(bytes, SkillBytesThroughRows(bytes));
        }

        [TestMethod]
        public void XpRows_RebuildTheRecord_CustomDMFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"));

            AssertSameBytes(bytes, XpBytesThroughRows(bytes));
        }

        [TestMethod]
        public void XpRows_RebuildTheRecord_EoRDat()
        {
            var bytes = PortalRecord("client_portal - EoR.dat", XpTableId);

            AssertSameBytes(bytes, XpBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SkillRows_WithoutBase_AreWrittenInIdOrder()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000004 - Skills Table.bin"));
            var rows = DatTables.ToRows(UnpackSkills(bytes));

            var rebuilt = DatTables.ToSkillTable(rows.AsEnumerable().Reverse());

            CollectionAssert.AreEqual(rows.Select(r => r.Id).ToArray(), rebuilt.SkillBaseHash.Keys.ToArray());
            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(rebuilt)));
        }

        [TestMethod]
        public void SkillRows_ChangedValue_ChangesBytesAndHash()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000004 - Skills Table.bin"));
            var table = UnpackSkills(bytes);
            var rows = DatTables.ToRows(table);
            var before = DatTables.Hash(rows);

            rows[0].TrainedCost += 1;

            var rebuilt = DatTables.ToSkillTable(rows, table);

            Assert.AreNotEqual(before, DatTables.Hash(rows));
            Assert.AreEqual(rows[0].TrainedCost, rebuilt.SkillBaseHash[rows[0].Id].TrainedCost);
            Assert.AreEqual(bytes.Length, rebuilt.Pack().Length);
            CollectionAssert.AreNotEqual(bytes, rebuilt.Pack());
        }

        [TestMethod]
        public void SkillRows_RemovedAndAddedSkills_KeepBaseOrderThenAppendNewInIdOrder()
        {
            var table = new SkillTable();
            foreach (var id in new uint[] { 9, 2, 5 })
                table.SkillBaseHash[id] = new SkillBase { Name = id.ToString(), Description = "" };

            var rows = DatTables.ToRows(table).Where(r => r.Id != 2).ToList();
            rows.Add(new DatSkillRow { Id = 40, Name = "x", Description = "" });
            rows.Add(new DatSkillRow { Id = 3, Name = "y", Description = "" });

            var rebuilt = DatTables.ToSkillTable(rows, table);

            CollectionAssert.AreEqual(new uint[] { 9, 5, 3, 40 }, rebuilt.SkillBaseHash.Keys.ToArray());
        }

        [TestMethod]
        public void XpRows_HashIsSameForRowsAndDecodedRecord()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"));
            var rows = DatTables.ToRows(UnpackXp(bytes));
            var rebuilt = DatTables.ToRows(DatTables.ToXpTable(rows));

            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(rebuilt));

            rows.Levels[5].TotalXp += 1;
            Assert.AreNotEqual(DatTables.Hash(rebuilt), DatTables.Hash(rows));

            rebuilt.Vital[3].TotalXp += 1;
            Assert.AreNotEqual(DatTables.Hash(rebuilt), DatTables.Hash(DatTables.ToRows(UnpackXp(bytes))));
        }

        [TestMethod]
        public void XpRows_EachListKeepsItsOwnLength()
        {
            var table = new XpTable();
            table.AttributeXpList.AddRange(new uint[] { 0, 1, 2 });
            table.VitalXpList.AddRange(new uint[] { 0, 1 });
            table.TrainedSkillXpList.AddRange(new uint[] { 0 });
            table.SpecializedSkillXpList.AddRange(new uint[] { 0, 1, 2, 3 });
            table.CharacterLevelXPList.AddRange(new ulong[] { 0, 5 });
            table.CharacterLevelSkillCreditList.AddRange(new uint[] { 0, 1 });

            var rows = DatTables.ToRows(table);

            Assert.AreEqual(3, rows.Attribute.Count);
            Assert.AreEqual(2, rows.Vital.Count);
            Assert.AreEqual(1, rows.TrainedSkill.Count);
            Assert.AreEqual(4, rows.SpecializedSkill.Count);
            AssertSameBytes(table.Pack(), DatTables.ToXpTable(rows).Pack());
        }

        [TestMethod]
        public void XpRows_MissingRankOrEmptyListIsRejected()
        {
            var rows = DatTables.ToRows(UnpackXp(File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"))));
            rows.Vital.RemoveAt(10);

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToXpTable(rows));

            rows = DatTables.ToRows(UnpackXp(File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"))));
            rows.TrainedSkill.Clear();

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToXpTable(rows));
        }

        [TestMethod]
        public void InsertScript_EscapesText()
        {
            var xp = new DatXpRows { Attribute = new List<DatXpRow> { new DatXpRow { Rank = 0, TotalXp = 7 } }, Levels = new List<DatXpLevelRow> { new DatXpLevelRow { Level = 0, TotalXp = 5000000000, SkillCredits = 1 } } };
            var skills = new[] { new DatSkillRow { Id = 1, Name = "It's", Description = "a\\b\r\nc", LearnMod = 1.5 } };

            var sql = DatTables.InsertScript(xp, skills);

            StringAssert.Contains(sql, "`dat_xp_attribute` (`rank`, `total_Xp`) VALUES (0, 7);");
            StringAssert.Contains(sql, "VALUES (0, 5000000000, 1);");
            StringAssert.Contains(sql, "'It''s', 'a\\\\b\\r\\nc'");
            StringAssert.Contains(sql, "1.5);");
        }

        private class FakeSource : IDatTableSource
        {
            public DatTableRows Rows;

            public DatTableRows Read()
            {
                return Rows;
            }
        }

        private static string TempDatWith(byte[] xp, byte[] skills)
        {
            var template = Path.Combine(SourceDats, "client_portal - EoR.dat");

            if (!File.Exists(template))
                Assert.Inconclusive($"Test dat not found: {template}");

            var path = Path.Combine(Path.GetTempPath(), $"DatTableStepTests-{Guid.NewGuid():N}.dat");
            DatWriter.CreateEmpty(path, template);

            using (var writer = new DatWriter(path))
            {
                writer.Save(DatTableStep.XpRecordId, xp);
                writer.Save(DatTableStep.SkillRecordId, skills);
            }

            return path;
        }

        [TestMethod]
        public void TableStep_UnchangedChangedAndMissingSource()
        {
            var xp = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000018 - XPTable.bin"));
            var skills = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000004 - Skills Table.bin"));
            var path = TempDatWith(xp, skills);

            try
            {
                var source = new FakeSource { Rows = new DatTableRows { Xp = DatTables.ToRows(UnpackXp(xp)), Skills = DatTables.ToRows(UnpackSkills(skills)) } };

                using (var writer = new DatWriter(path))
                {
                    var unchanged = DatTableStep.Apply(writer, source, 20250101);
                    Assert.IsFalse(unchanged.Skipped);
                    Assert.AreEqual(0, unchanged.Written.Count);
                    Assert.AreEqual(2, unchanged.Unchanged.Count);

                    source.Rows.Xp.Vital[1].TotalXp += 1;
                    source.Rows.Skills[0].TrainedCost += 1;

                    var changed = DatTableStep.Apply(writer, source, 20250101);
                    Assert.AreEqual(2, changed.Written.Count);
                    Assert.AreEqual(DatTables.Hash(source.Rows.Xp), DatTables.Hash(DatTables.ToRows(UnpackXp(writer.Read(DatTableStep.XpRecordId)))));
                    Assert.AreEqual(DatTables.Hash(source.Rows.Skills), DatTables.Hash(DatTables.ToRows(UnpackSkills(writer.Read(DatTableStep.SkillRecordId)))));

                    var skipped = DatTableStep.Apply(writer, new FakeSource(), 20250101);
                    Assert.IsTrue(skipped.Skipped);
                    Assert.AreEqual(0, skipped.Written.Count);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
