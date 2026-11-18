using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.DatLoader.Tests
{
    /// <summary>
    /// The XP and skill tables are packed back from their decoded form: the bytes must equal the record they were read from.
    /// </summary>
    [TestClass]
    public class DatTableCodecTests
    {
        static DatTableCodecTests()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

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

        private static byte[] CustomDMXpRecord()
        {
            return ShippedCustomDMPortalRecord(XpTableId);
        }

        private static byte[] CustomDMSkillRecord()
        {
            return ShippedCustomDMPortalRecord(SkillTableId);
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
        public void XpTable_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = CustomDMXpRecord();

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
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", XpTableId);

            AssertSameBytes(bytes, UnpackXp(bytes).Pack());
        }

        [TestMethod]
        public void SkillTable_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = CustomDMSkillRecord();

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
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SkillTableId);

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

        private static void AssertSkillsRebuiltInIdOrder(byte[] original, byte[] rebuilt)
        {
            var before = UnpackSkills(original);
            var after = UnpackSkills(rebuilt);

            Assert.AreEqual(DatTables.Hash(DatTables.ToRows(before)), DatTables.Hash(DatTables.ToRows(after)));
            Assert.AreEqual(before.BucketSize, after.BucketSize);
            Assert.AreEqual(original.Length, rebuilt.Length);
            CollectionAssert.AreEqual(before.SkillBaseHash.Keys.OrderBy(k => k).ToArray(), after.SkillBaseHash.Keys.ToArray());
        }

        [TestMethod]
        public void SkillRows_RebuildTheRecordContentInIdOrder_ShippedCustomDMDat()
        {
            var bytes = CustomDMSkillRecord();

            AssertSkillsRebuiltInIdOrder(bytes, SkillBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SkillRows_RebuildTheRecordContentInIdOrder_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SkillTableId);

            AssertSkillsRebuiltInIdOrder(bytes, SkillBytesThroughRows(bytes));
        }

        [TestMethod]
        public void XpRows_RebuildTheRecord_ShippedCustomDMDat()
        {
            var bytes = CustomDMXpRecord();

            AssertSameBytes(bytes, XpBytesThroughRows(bytes));
        }

        [TestMethod]
        public void XpRows_RebuildTheRecord_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", XpTableId);

            AssertSameBytes(bytes, XpBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SkillRows_WithoutBase_AreWrittenInIdOrder()
        {
            var bytes = CustomDMSkillRecord();
            var rows = DatTables.ToRows(UnpackSkills(bytes));

            var rebuilt = DatTables.ToSkillTable(rows.AsEnumerable().Reverse());

            CollectionAssert.AreEqual(rows.Select(r => r.Id).ToArray(), rebuilt.SkillBaseHash.Keys.ToArray());
            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(rebuilt)));
        }

        [TestMethod]
        public void SkillRows_ChangedValue_ChangesBytesAndHash()
        {
            var bytes = CustomDMSkillRecord();
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
        public void SkillRows_RemovedAndAddedSkills_AreWrittenInIdOrderWhateverTheBaseOrder()
        {
            var table = new SkillTable();
            foreach (var id in new uint[] { 9, 2, 5 })
                table.SkillBaseHash[id] = new SkillBase { Name = id.ToString(), Description = "" };

            var rows = DatTables.ToRows(table).Where(r => r.Id != 2).ToList();
            rows.Add(new DatSkillRow { Id = 40, Name = "x", Description = "" });
            rows.Add(new DatSkillRow { Id = 3, Name = "y", Description = "" });

            var rebuilt = DatTables.ToSkillTable(rows, table);

            CollectionAssert.AreEqual(new uint[] { 3, 5, 9, 40 }, rebuilt.SkillBaseHash.Keys.ToArray());
        }

        [TestMethod]
        public void XpRows_HashIsSameForRowsAndDecodedRecord()
        {
            var bytes = CustomDMXpRecord();
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
            var rows = DatTables.ToRows(UnpackXp(CustomDMXpRecord()));
            rows.Vital.RemoveAt(10);

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToXpTable(rows));

            rows = DatTables.ToRows(UnpackXp(CustomDMXpRecord()));
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

        // ---- spell components (the record codec only; the table is not authored from the database) ----

        private const uint SpellComponentTableId = 0x0E00000F;

        private static byte[] ShippedCustomDMPortalRecord(uint id)
        {
            var path = Path.GetFullPath(Path.Combine(SourceDats, "..", "CustomDM", "client_portal.dat"));

            if (!File.Exists(path))
                Assert.Inconclusive($"Test dat not found: {path}");

            return new DatDatabase(path).GetReaderForFile(id).Buffer;
        }

        private static SpellComponentsTable UnpackComponents(byte[] bytes)
        {
            var table = new SpellComponentsTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        [TestMethod]
        public void SpellComponents_PackEqualsRecord_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellComponentTableId);

            AssertSameBytes(bytes, UnpackComponents(bytes).Pack());
        }

        [TestMethod]
        public void SpellComponents_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = ShippedCustomDMPortalRecord(SpellComponentTableId);

            AssertSameBytes(bytes, UnpackComponents(bytes).Pack());
        }

        [TestMethod]
        public void SpellComponents_SyntheticTable_SurvivesRoundTrip()
        {
            var table = new SpellComponentsTable { BucketSize = 16 };

            table.SpellComponents[7] = new SpellComponentBase { Name = "Odd–Name", Category = 1, Icon = 0x0600140A, Type = 2, Gesture = 0x80000000, Time = 1.25f, Text = "Malar", CDM = 1E-08f };
            table.SpellComponents[3] = new SpellComponentBase { Name = "Ab", Text = "" };

            var bytes = table.Pack();
            var back = UnpackComponents(bytes);

            Assert.AreEqual(16, back.BucketSize);
            CollectionAssert.AreEqual(new uint[] { 7, 3 }, back.SpellComponents.Keys.ToArray());
            Assert.AreEqual("Odd–Name", back.SpellComponents[7].Name);
            Assert.AreEqual("Malar", back.SpellComponents[7].Text);
            Assert.AreEqual(0x80000000u, back.SpellComponents[7].Gesture);
            Assert.AreEqual(1E-08f, back.SpellComponents[7].CDM);
            AssertSameBytes(bytes, back.Pack());
        }

        // ---- spell table (0x0E00000E): spells, their encrypted formulas and the equipment sets ----

        private const uint SpellTableId = 0x0E00000E;

        private static SpellTable UnpackSpells(byte[] bytes)
        {
            var table = new SpellTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        [TestMethod]
        public void SpellTable_PackEqualsRecord_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellTableId);

            AssertSameBytes(bytes, UnpackSpells(bytes).Pack());
        }

        [TestMethod]
        public void SpellTable_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = ShippedCustomDMPortalRecord(SpellTableId);

            AssertSameBytes(bytes, UnpackSpells(bytes).Pack());
        }

        [TestMethod]
        public void SpellTable_RenamedSpell_KeepsItsFormula()
        {
            var table = UnpackSpells(PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellTableId));
            var spell = table.Spells.Values.First(s => s.Formula.Count == 8);
            var formula = spell.Formula.ToList();

            spell.Name += " Renamed";
            spell.Desc += "!";

            var back = UnpackSpells(table.Pack());
            var key = table.Spells.First(s => s.Value == spell).Key;

            CollectionAssert.AreEqual(formula, back.Spells[key].Formula);
            Assert.AreEqual(spell.Name, back.Spells[key].Name);
        }

        private static byte[] SpellBytesThroughRows(byte[] bytes)
        {
            var table = UnpackSpells(bytes);
            return DatTables.ToSpellTable(DatTables.ToRows(table), DatTables.ToSetRows(table), table).Pack();
        }

        private static void AssertSpellsRebuiltInIdOrder(byte[] original, byte[] rebuilt)
        {
            var before = UnpackSpells(original);
            var after = UnpackSpells(rebuilt);

            Assert.AreEqual(DatTables.Hash(DatTables.ToRows(before), DatTables.ToSetRows(before)), DatTables.Hash(DatTables.ToRows(after), DatTables.ToSetRows(after)));
            Assert.AreEqual(before.SpellBucketSize, after.SpellBucketSize);
            Assert.AreEqual(before.SetBucketSize, after.SetBucketSize);
            Assert.AreEqual(original.Length, rebuilt.Length);
            CollectionAssert.AreEqual(before.Spells.Keys.OrderBy(k => k).ToArray(), after.Spells.Keys.ToArray());
            CollectionAssert.AreEqual(before.SpellSet.Keys.OrderBy(k => k).ToArray(), after.SpellSet.Keys.ToArray());
        }

        [TestMethod]
        public void SpellRows_RebuildTheRecordContentInIdOrder_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellTableId);

            AssertSpellsRebuiltInIdOrder(bytes, SpellBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SpellRows_RebuildTheRecordContentInIdOrder_ShippedCustomDMDat()
        {
            var bytes = ShippedCustomDMPortalRecord(SpellTableId);

            AssertSpellsRebuiltInIdOrder(bytes, SpellBytesThroughRows(bytes));
        }

        [TestMethod]
        public void SpellRows_ChangedFormulaAndName_SurviveTheRecordAndChangeTheHash()
        {
            var table = UnpackSpells(PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellTableId));
            var rows = DatTables.ToRows(table);
            var sets = DatTables.ToSetRows(table);
            var before = DatTables.Hash(rows, sets);

            var row = rows.First(r => r.Components[7] != null);
            row.Name += " Renamed";
            row.Components[7] = row.Components[0];

            Assert.AreNotEqual(before, DatTables.Hash(rows, sets));

            var back = DatTables.ToRows(UnpackSpells(DatTables.ToSpellTable(rows, sets, table).Pack()));

            Assert.AreEqual(DatTables.Hash(rows, sets), DatTables.Hash(back, sets));
        }

        [TestMethod]
        public void SpellRows_FormulaGap_IsRejected()
        {
            var row = new DatSpellRow { Id = 1, Name = "a", Description = "b" };
            row.Components[0] = 5;
            row.Components[2] = 6;

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToSpellTable(new[] { row }, new DatSpellSetSpellRow[0]));
        }

        [TestMethod]
        public void SpellRows_SetTierProblems_AreRejected()
        {
            var spells = new[] { new DatSpellRow { Id = 1, Name = "a", Description = "b" } };

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToSpellTable(spells, new[] { new DatSpellSetSpellRow { SetId = 4, Tier = 2, Position = 1, SpellId = 1 } }));
            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToSpellTable(spells, new[] { new DatSpellSetSpellRow { SetId = 4, Tier = 2, Position = 0, SpellId = 99 } }));

            var table = DatTables.ToSpellTable(spells, new[] { new DatSpellSetSpellRow { SetId = 4, Tier = 2, Position = 0, SpellId = 1 } });

            CollectionAssert.AreEqual(new uint[] { 1 }, table.SpellSet[4].SpellSetTiers[2].Spells);
        }

        [TestMethod]
        public void SpellInsertScript_WritesNullsAndFloats()
        {
            var row = new DatSpellRow { Id = 1, Name = "It's", Description = "d", BaseRangeConstant = 0.1f, Duration = 30, MetaSpellType = 1 };
            row.Components[0] = 5;

            var sql = DatTables.SpellInsertScript(new[] { row }, new[] { new DatSpellSetSpellRow { SetId = 4, Tier = 2, Position = 0, SpellId = 1 } });

            StringAssert.Contains(sql, "'It''s', 'd'");
            StringAssert.Contains(sql, "0.1, 0, 0, 0, 0, 0, 1, 0, 30, NULL, NULL, NULL, 5, NULL, NULL, NULL, NULL, NULL, NULL, NULL, 0,");
            StringAssert.Contains(sql, "`dat_spell_set_spell` (`set_Id`, `tier`, `position`, `spell_Id`) VALUES (4, 2, 0, 1);");
        }

        private static SecondaryAttributeTable UnpackVitals(byte[] bytes)
        {
            var table = new SecondaryAttributeTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        [TestMethod]
        public void VitalTable_PackEqualsRecord_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);

            AssertSameBytes(bytes, UnpackVitals(bytes).Pack());
        }

        [TestMethod]
        public void VitalTable_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = ShippedCustomDMPortalRecord(DatTableStep.VitalRecordId);

            AssertSameBytes(bytes, UnpackVitals(bytes).Pack());
        }

        [TestMethod]
        public void VitalRows_RebuildTheRecord_EoRDat()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);

            AssertSameBytes(bytes, DatTables.ToVitalTable(DatTables.ToRows(UnpackVitals(bytes))).Pack());
        }

        [TestMethod]
        public void VitalRows_RebuildTheRecord_ShippedCustomDMDat()
        {
            var bytes = ShippedCustomDMPortalRecord(DatTableStep.VitalRecordId);

            AssertSameBytes(bytes, DatTables.ToVitalTable(DatTables.ToRows(UnpackVitals(bytes))).Pack());
        }

        [TestMethod]
        public void VitalRows_ChangedValue_ChangesBytesAndHash()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);
            var rows = DatTables.ToRows(UnpackVitals(bytes));
            var before = DatTables.Hash(rows);

            Assert.AreEqual(3, rows.Count);

            rows.Single(r => r.Id == (uint)PropertyAttribute2nd.MaxStamina).FormulaZ = 2;

            var rebuilt = DatTables.ToVitalTable(rows);

            Assert.AreEqual(2u, rebuilt.MaxStamina.Formula.Z);
            Assert.AreNotEqual(before, DatTables.Hash(rows));
            CollectionAssert.AreNotEqual(bytes, rebuilt.Pack());
            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(UnpackVitals(rebuilt.Pack()))));
        }

        [TestMethod]
        public void VitalRows_MissingOrUnknownVital_IsRejected()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);
            var rows = DatTables.ToRows(UnpackVitals(bytes));

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToVitalTable(rows.Take(2)));
            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToVitalTable(rows.Concat(new[] { new DatVitalRow { Id = 7 } })));
        }

        [TestMethod]
        public void VitalInsertScript_WritesTheThreeFormulas()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);
            var sql = DatTables.VitalInsertScript(DatTables.ToRows(UnpackVitals(bytes)));

            StringAssert.StartsWith(sql, "DELETE FROM `dat_vital`;");
            StringAssert.Contains(sql, "(`id`, `formula_W`, `formula_X`, `formula_Y`, `formula_Z`, `formula_Attr1`, `formula_Attr2`) VALUES (1, 0, 1, 0, 2, 2, 0);");
            Assert.AreEqual(3, sql.Split('\n').Count(l => l.StartsWith("INSERT")));
        }

        [TestMethod]
        public void TableStep_VitalTable_WritesOnlyWhenChanged()
        {
            var xp = CustomDMXpRecord();
            var skills = CustomDMSkillRecord();
            var vitalBytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTableStep.VitalRecordId);
            var path = TempDatWith(xp, skills);

            try
            {
                var source = new FakeSource
                {
                    Rows = new DatTableRows
                    {
                        Xp = DatTables.ToRows(UnpackXp(xp)),
                        Skills = DatTables.ToRows(UnpackSkills(skills)),
                        Vitals = DatTables.ToRows(UnpackVitals(vitalBytes))
                    }
                };

                using (var writer = new DatWriter(path))
                {
                    writer.Save(DatTableStep.VitalRecordId, vitalBytes);

                    var first = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(first.Unchanged, "vital table");
                    Assert.IsNotNull(first.VitalHash);

                    source.Rows.Vitals[0].FormulaZ += 1;

                    var second = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(second.Written, "vital table");
                    Assert.AreEqual(DatTables.Hash(source.Rows.Vitals), DatTables.Hash(DatTables.ToRows(UnpackVitals(writer.Read(DatTableStep.VitalRecordId)))));

                    source.Rows.Vitals = null;

                    var third = DatTableStep.Apply(writer, source, 20250102);
                    Assert.IsNull(third.VitalHash);
                    Assert.AreEqual(DatTables.Hash(DatTables.ToRows(UnpackVitals(writer.Read(DatTableStep.VitalRecordId)))), second.VitalHash);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ---- character titles (portal enum mapper 0x22000041 and language string table 0x2300000E) ----

        private static byte[] LanguageRecord(string datName, uint id, bool shipped)
        {
            var path = shipped
                ? Path.GetFullPath(Path.Combine(SourceDats, "..", "CustomDM", datName))
                : Path.Combine(SourceDats, datName);

            if (!File.Exists(path))
                Assert.Inconclusive($"Test dat not found: {path}");

            return new DatDatabase(path).GetReaderForFile(id).Buffer;
        }

        private static byte[] TitleMapperBytes(bool shipped)
        {
            return shipped ? ShippedCustomDMPortalRecord(DatTables.TitleMapperId) : PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", DatTables.TitleMapperId);
        }

        private static byte[] TitleStringBytes(bool shipped)
        {
            return LanguageRecord(shipped ? "client_local_English.dat" : "language - 2015-06-18 - Iteration 994 - EoR.dat", DatTableStep.TitleStringRecordId, shipped);
        }

        private static EnumMapper UnpackMapper(byte[] bytes)
        {
            var mapper = new EnumMapper();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                mapper.Unpack(reader);

            return mapper;
        }

        private static StringTable UnpackStrings(byte[] bytes)
        {
            var table = new StringTable();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        private static List<DatTitleRow> TitleRows(bool shipped)
        {
            return DatTables.ToRows(UnpackMapper(TitleMapperBytes(shipped)), UnpackStrings(TitleStringBytes(shipped)));
        }

        [TestMethod]
        public void TitleMapper_PackEqualsRecord_EoRDat()
        {
            var bytes = TitleMapperBytes(false);

            AssertSameBytes(bytes, UnpackMapper(bytes).Pack());
        }

        [TestMethod]
        public void TitleMapper_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = TitleMapperBytes(true);

            AssertSameBytes(bytes, UnpackMapper(bytes).Pack());
        }

        [TestMethod]
        public void TitleStrings_PackEqualsRecord_EoRDat()
        {
            var bytes = TitleStringBytes(false);

            AssertSameBytes(bytes, UnpackStrings(bytes).Pack());
        }

        [TestMethod]
        public void TitleStrings_PackEqualsRecord_ShippedCustomDMDat()
        {
            var bytes = TitleStringBytes(true);

            AssertSameBytes(bytes, UnpackStrings(bytes).Pack());
        }

        [TestMethod]
        public void TitleRows_RebuildBothRecords_EoRDat()
        {
            var mapperBytes = TitleMapperBytes(false);
            var stringBytes = TitleStringBytes(false);
            var rows = TitleRows(false);

            Assert.AreEqual(873, rows.Count);
            AssertSameBytes(mapperBytes, DatTables.ToTitleMapper(rows, UnpackMapper(mapperBytes)).Pack());
            AssertSameBytes(stringBytes, DatTables.ToTitleStringTable(rows, UnpackStrings(stringBytes)).Pack());
        }

        [TestMethod]
        public void TitleRows_RebuildBothRecords_ShippedCustomDMDat()
        {
            var mapperBytes = TitleMapperBytes(true);
            var stringBytes = TitleStringBytes(true);
            var rows = TitleRows(true);

            AssertSameBytes(mapperBytes, DatTables.ToTitleMapper(rows, UnpackMapper(mapperBytes)).Pack());
            AssertSameBytes(stringBytes, DatTables.ToTitleStringTable(rows, UnpackStrings(stringBytes)).Pack());
        }

        [TestMethod]
        public void TitleRows_NewTitlesAndChangedText_AreWrittenToBothRecords()
        {
            var rows = TitleRows(false);
            var before = DatTables.Hash(rows);

            rows.Add(new DatTitleRow { Id = 896, Name = "ID_CharacterTitle_Solo_Self_Found", Text = "Solo Self-Found" });
            rows.Add(new DatTitleRow { Id = 895, Name = "ID_CharacterTitle_In_Limbo", Text = "In Limbo" });
            rows.Single(r => r.Id == 1).Text = "Adventurer Changed";

            var mapperBytes = TitleMapperBytes(false);
            var stringBytes = TitleStringBytes(false);
            var mapper = DatTables.ToTitleMapper(rows, UnpackMapper(mapperBytes));
            var strings = DatTables.ToTitleStringTable(rows, UnpackStrings(stringBytes));

            Assert.AreEqual(875, mapper.IdToStringMap.Count);
            Assert.AreEqual(875, strings.StringTableData.Count);
            CollectionAssert.AreEqual(new uint[] { 895, 896 }, mapper.IdToStringMap.Keys.Skip(873).ToArray());

            var rebuilt = DatTables.ToRows(UnpackMapper(mapper.Pack()), UnpackStrings(strings.Pack()));

            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(rebuilt));
            Assert.AreNotEqual(before, DatTables.Hash(rebuilt));
            Assert.AreEqual("In Limbo", rebuilt.Single(r => r.Id == 895).Text);
        }

        [TestMethod]
        public void TitleRows_RepeatedIdOrName_IsRejected()
        {
            var rows = TitleRows(false);
            var mapper = UnpackMapper(TitleMapperBytes(false));
            var strings = UnpackStrings(TitleStringBytes(false));

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToTitleMapper(rows.Concat(new[] { new DatTitleRow { Id = 1, Name = "ID_CharacterTitle_Other", Text = "x" } }), mapper));
            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToTitleStringTable(rows.Concat(new[] { new DatTitleRow { Id = 900, Name = rows[0].Name, Text = "x" } }), strings));
        }

        [TestMethod]
        public void TitleRows_MapperEntryWithoutText_IsRejected()
        {
            var mapper = UnpackMapper(TitleMapperBytes(false));
            var strings = UnpackStrings(TitleStringBytes(false));

            strings.StringTableData.RemoveAt(0);

            Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToRows(mapper, strings));
        }

        [TestMethod]
        public void TitleInsertScript_QuotesNameAndText()
        {
            var sql = DatTables.TitleInsertScript(new[] { new DatTitleRow { Id = 895, Name = "ID_CharacterTitle_X", Text = "It's" } });

            StringAssert.StartsWith(sql, "DELETE FROM `dat_character_title`;");
            StringAssert.Contains(sql, "(`id`, `name`, `text`) VALUES (895, 'ID_CharacterTitle_X', 'It''s');");
        }

        [TestMethod]
        public void TableStep_Titles_PortalMapperAndLanguageStrings_WriteOnlyWhenChanged()
        {
            var xp = CustomDMXpRecord();
            var skills = CustomDMSkillRecord();
            var mapperBytes = TitleMapperBytes(false);
            var stringBytes = TitleStringBytes(false);
            var languageTemplate = Path.Combine(SourceDats, "language - 2015-06-18 - Iteration 994 - EoR.dat");
            var portalPath = TempDatWith(xp, skills);
            var languagePath = Path.Combine(Path.GetTempPath(), $"DatTableStepTests-{Guid.NewGuid():N}.dat");

            DatWriter.CreateEmpty(languagePath, languageTemplate);

            try
            {
                var source = new FakeSource
                {
                    Rows = new DatTableRows
                    {
                        Xp = DatTables.ToRows(UnpackXp(xp)),
                        Skills = DatTables.ToRows(UnpackSkills(skills)),
                        Titles = TitleRows(false)
                    }
                };

                using (var portal = new DatWriter(portalPath))
                using (var language = new DatWriter(languagePath))
                {
                    portal.Save(DatTableStep.TitleMapperRecordId, mapperBytes);
                    language.Save(DatTableStep.TitleStringRecordId, stringBytes);

                    CollectionAssert.Contains(DatTableStep.Apply(portal, source, 20250101).Unchanged, "title mapper");
                    CollectionAssert.Contains(DatTableStep.ApplyLanguage(language, source, 20250101).Unchanged, "title strings");

                    source.Rows.Titles.Add(new DatTitleRow { Id = 895, Name = "ID_CharacterTitle_In_Limbo", Text = "In Limbo" });

                    var portalReport = DatTableStep.Apply(portal, source, 20250101);
                    var languageReport = DatTableStep.ApplyLanguage(language, source, 20250101);

                    CollectionAssert.Contains(portalReport.Written, "title mapper");
                    CollectionAssert.Contains(languageReport.Written, "title strings");
                    Assert.AreEqual(DatTables.Hash(source.Rows.Titles), DatTables.Hash(DatTables.ToRows(UnpackMapper(portal.Read(DatTableStep.TitleMapperRecordId)), UnpackStrings(language.Read(DatTableStep.TitleStringRecordId)))));

                    source.Rows.Titles = null;

                    Assert.IsNull(DatTableStep.Apply(portal, source, 20250102).TitleHash);
                    Assert.IsTrue(DatTableStep.ApplyLanguage(language, source, 20250102).Skipped);
                    Assert.AreEqual(874, UnpackMapper(portal.Read(DatTableStep.TitleMapperRecordId)).IdToStringMap.Count);
                }
            }
            finally
            {
                File.Delete(portalPath);
                File.Delete(languagePath);
            }
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
            var template = Path.Combine(SourceDats, "portal - 2017-01-29 - Iteration 2072 - EoR.dat");

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
            var xp = CustomDMXpRecord();
            var skills = SkillBytesThroughRows(CustomDMSkillRecord());
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

        [TestMethod]
        public void TableStep_SpellTables_WriteOnlyWhenChangedAndCheckComponents()
        {
            var xp = CustomDMXpRecord();
            var skills = SkillBytesThroughRows(CustomDMSkillRecord());
            var components = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellComponentTableId);
            var spellBytes = SpellBytesThroughRows(PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", SpellTableId));
            var spellTable = UnpackSpells(spellBytes);
            var path = TempDatWith(xp, skills);

            try
            {
                var source = new FakeSource
                {
                    Rows = new DatTableRows
                    {
                        Xp = DatTables.ToRows(UnpackXp(xp)),
                        Skills = DatTables.ToRows(UnpackSkills(skills)),
                        Spells = DatTables.ToRows(spellTable),
                        SetSpells = DatTables.ToSetRows(spellTable)
                    }
                };

                using (var writer = new DatWriter(path))
                {
                    writer.Save(DatTableStep.ComponentRecordId, components);
                    writer.Save(DatTableStep.SpellRecordId, spellBytes);

                    var first = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(first.Unchanged, "spell table");
                    Assert.IsNotNull(first.SpellHash);

                    source.Rows.Spells[0].Name += " Renamed";

                    var second = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(second.Written, "spell table");
                    Assert.AreEqual(DatTables.Hash(source.Rows.Spells, source.Rows.SetSpells), DatTables.Hash(DatTables.ToRows(UnpackSpells(writer.Read(DatTableStep.SpellRecordId))), source.Rows.SetSpells));

                    source.Rows.Spells[0].Components[0] = 100000;
                    Assert.ThrowsExactly<InvalidDataException>(() => DatTableStep.Apply(writer, source, 20250101));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        // ---- character generation (record codec) ----

        private const uint CharGenId = 0x0E000002;

        private static byte[] ShippedPortalRecord(string ruleset, uint id)
        {
            var path = Path.GetFullPath(Path.Combine(SourceDats, "..", ruleset, "client_portal.dat"));

            if (!File.Exists(path))
                Assert.Inconclusive($"Test dat not found: {path}");

            return new DatDatabase(path).GetReaderForFile(id).Buffer;
        }

        private static CharGen UnpackCharGen(byte[] bytes)
        {
            var table = new CharGen();

            using (var reader = new BinaryReader(new MemoryStream(bytes)))
                table.Unpack(reader);

            return table;
        }

        [TestMethod]
        public void CharGen_PackEqualsRecord_EoR()
        {
            var bytes = PortalRecord("portal - 2017-01-29 - Iteration 2072 - EoR.dat", CharGenId);
            AssertSameBytes(bytes, UnpackCharGen(bytes).Pack());
        }

        [TestMethod]
        public void CharGen_PackEqualsRecord_ShippedCustomDM()
        {
            var bytes = ShippedPortalRecord("CustomDM", CharGenId);
            AssertSameBytes(bytes, UnpackCharGen(bytes).Pack());
        }

        [TestMethod]
        public void CharGen_PackEqualsRecord_ShippedInfiltration()
        {
            var bytes = ShippedPortalRecord("Infiltration", CharGenId);
            AssertSameBytes(bytes, UnpackCharGen(bytes).Pack());
        }

        [TestMethod]
        public void CharGen_PackEqualsRecord_CustomDMFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000002 - CharGen.bin"));
            AssertSameBytes(bytes, UnpackCharGen(bytes).Pack());
        }

        [TestMethod]
        public void CharGen_PackEqualsRecord_InfiltrationFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "Infiltration", "0E000002 - CharGen.bin"));
            AssertSameBytes(bytes, UnpackCharGen(bytes).Pack());
        }

        // ---- character generation (tables) ----

        private static List<uint> CustomDMSkillIds()
        {
            return DatTables.ToRows(UnpackSkills(CustomDMSkillRecord())).Select(s => s.Id).ToList();
        }

        private static byte[] CustomDMChargenBytes()
        {
            return File.ReadAllBytes(CustomFile("Portal", "CustomDM", "0E000002 - CharGen.bin"));
        }

        private static void AssertSameLists(CharGen expected, CharGen actual)
        {
            Assert.AreEqual(expected.HeritageGroups.Count, actual.HeritageGroups.Count);

            foreach (var pair in expected.HeritageGroups)
            {
                var a = actual.HeritageGroups[pair.Key];

                Assert.AreEqual(pair.Value.Templates.Count, a.Templates.Count, $"templates of heritage group {pair.Key}");

                for (var i = 0; i < a.Templates.Count; i++)
                {
                    var e = pair.Value.Templates[i];

                    Assert.AreEqual(e.Name, a.Templates[i].Name);
                    CollectionAssert.AreEquivalent(e.NormalSkillsList.Distinct().Union(e.PrimarySkillsList).ToList(), a.Templates[i].NormalSkillsList.Union(a.Templates[i].PrimarySkillsList).ToList());
                    CollectionAssert.AreEquivalent(e.PrimarySkillsList.Distinct().ToList(), a.Templates[i].PrimarySkillsList);
                }
            }
        }

        [TestMethod]
        public void ChargenRows_RebuildTheRecord_CustomDMFile()
        {
            var bytes = CustomDMChargenBytes();
            var rows = DatTables.ToRows(UnpackCharGen(bytes));
            var rebuilt = DatTables.ToCharGen(rows, UnpackCharGen(bytes), CustomDMSkillIds());

            Assert.AreEqual(79, rows.Templates.Count);
            Assert.AreEqual(13, rows.Overrides.Count);
            Assert.AreEqual(330u, rows.Chargen.AttributeCredits);
            Assert.AreEqual(50u, rows.Chargen.SkillCredits);

            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(UnpackCharGen(rebuilt.Pack()))));
            AssertSameLists(UnpackCharGen(bytes), rebuilt);
            Assert.IsTrue(DatTables.CreditsApplied(rebuilt, rows.Chargen));
        }

        [TestMethod]
        public void ChargenRows_RebuildTheRecord_InfiltrationFile()
        {
            var bytes = File.ReadAllBytes(CustomFile("Portal", "Infiltration", "0E000002 - CharGen.bin"));
            var rows = DatTables.ToRows(UnpackCharGen(bytes));
            var skillIds = DatTables.ToRows(UnpackSkills(File.ReadAllBytes(CustomFile("Portal", "Infiltration", "0E000004 - SkillTable.bin")))).Select(s => s.Id).ToList();
            var rebuilt = DatTables.ToCharGen(rows, UnpackCharGen(bytes), skillIds);

            Assert.IsTrue(rows.Overrides.Count > 13);
            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(UnpackCharGen(rebuilt.Pack()))));
            AssertSameLists(UnpackCharGen(bytes), rebuilt);
        }

        [TestMethod]
        public void ChargenRows_SpecializedSkillIsListedOnce_AndComesBackInThePrimaryListOnly()
        {
            var bytes = ShippedPortalRecord("CustomDM", CharGenId);
            var record = UnpackCharGen(bytes);
            var overlapping = record.HeritageGroups.SelectMany(g => g.Value.Templates).First(t => t.PrimarySkillsList.Intersect(t.NormalSkillsList).Any());
            var skill = overlapping.PrimarySkillsList.Intersect(overlapping.NormalSkillsList).First();

            var rows = DatTables.ToRows(record);

            Assert.AreEqual(rows.TemplateSkills.Count, rows.TemplateSkills.Select(s => (s.HeritageGroup, s.TemplateId, s.SkillId)).Distinct().Count());

            var shippedSkillIds = DatTables.ToRows(UnpackSkills(ShippedPortalRecord("CustomDM", SkillTableId))).Select(s => s.Id).ToList();
            var rebuilt = DatTables.ToCharGen(rows, UnpackCharGen(bytes), shippedSkillIds);
            var again = rebuilt.HeritageGroups.SelectMany(g => g.Value.Templates).First(t => t.Name == overlapping.Name && t.PrimarySkillsList.Contains(skill));

            CollectionAssert.DoesNotContain(again.NormalSkillsList, skill);
        }

        [TestMethod]
        public void ChargenRows_ChangedValues_ReachTheRecordAndTheHash()
        {
            var bytes = CustomDMChargenBytes();
            var rows = DatTables.ToRows(UnpackCharGen(bytes));
            var before = DatTables.Hash(rows);

            rows.Chargen.SkillCredits = 60;
            rows.Chargen.AttributeCredits = 400;
            rows.Templates.First(t => t.HeritageGroup == 1 && t.Id == 0).Strength = 99;
            rows.Overrides.First().SpecializedCost = 7;

            var trained = rows.TemplateSkills.First(s => s.AdvancementClass == (uint)SkillAdvancementClass.Trained);
            trained.AdvancementClass = (uint)SkillAdvancementClass.Specialized;

            var rebuilt = DatTables.ToCharGen(rows, UnpackCharGen(bytes), CustomDMSkillIds());
            var group = rebuilt.HeritageGroups[1];
            var changed = rebuilt.HeritageGroups[trained.HeritageGroup].Templates[(int)trained.TemplateId];

            Assert.IsTrue(rebuilt.HeritageGroups.Values.All(g => g.SkillCredits == 60 && g.AttributeCredits == 400));
            Assert.AreEqual(99u, group.Templates[0].Strength);
            Assert.AreEqual(7, rebuilt.HeritageGroups[rows.Overrides.First().HeritageGroup].Skills.First(s => s.SkillNum == rows.Overrides.First().SkillId).PrimaryCost);
            CollectionAssert.Contains(changed.PrimarySkillsList, trained.SkillId);
            CollectionAssert.DoesNotContain(changed.NormalSkillsList, trained.SkillId);
            Assert.AreNotEqual(before, DatTables.Hash(rows));
            Assert.AreEqual(DatTables.Hash(rows), DatTables.Hash(DatTables.ToRows(UnpackCharGen(rebuilt.Pack()))));
        }

        [TestMethod]
        public void ChargenRows_EmptyOverrideTable_ClearsTheOverrides()
        {
            var bytes = CustomDMChargenBytes();
            var rows = DatTables.ToRows(UnpackCharGen(bytes));

            rows.Overrides.Clear();

            var rebuilt = DatTables.ToCharGen(rows, UnpackCharGen(bytes), CustomDMSkillIds());

            Assert.IsTrue(rebuilt.HeritageGroups.Values.All(g => g.Skills.Count == 0));
        }

        [TestMethod]
        public void ChargenRows_Problems_AreRejected()
        {
            var bytes = CustomDMChargenBytes();
            var skillIds = CustomDMSkillIds();

            void Rejects(string what, Action<DatChargenRows> change)
            {
                var rows = DatTables.ToRows(UnpackCharGen(bytes));
                change(rows);
                Assert.ThrowsExactly<InvalidDataException>(() => DatTables.ToCharGen(rows, UnpackCharGen(bytes), skillIds), what);
            }

            Rejects("no credits row", r => r.Chargen = null);
            Rejects("heritage group without templates", r => r.Templates.RemoveAll(t => t.HeritageGroup == 3));
            Rejects("template ids with a gap", r => r.Templates.First(t => t.HeritageGroup == 2 && t.Id == 1).Id = 5);
            Rejects("unknown heritage group", r => r.Overrides.Add(new DatChargenHeritageSkillOverrideRow { HeritageGroup = 40, SkillId = skillIds[0] }));
            Rejects("override skill not in the skill table", r => r.Overrides[0].SkillId = 9999);
            Rejects("override twice", r => r.Overrides.Add(new DatChargenHeritageSkillOverrideRow { HeritageGroup = r.Overrides[0].HeritageGroup, SkillId = r.Overrides[0].SkillId }));
            Rejects("template skill not in the skill table", r => r.TemplateSkills[0].SkillId = 9999);
            Rejects("untrained skill", r => r.TemplateSkills[0].AdvancementClass = (uint)SkillAdvancementClass.Untrained);
            Rejects("inactive skill", r => r.TemplateSkills[0].AdvancementClass = (uint)SkillAdvancementClass.Inactive);
            Rejects("template skill twice", r => r.TemplateSkills.Add(new DatChargenTemplateSkillRow { HeritageGroup = r.TemplateSkills[0].HeritageGroup, TemplateId = r.TemplateSkills[0].TemplateId, SkillId = r.TemplateSkills[0].SkillId, AdvancementClass = 2 }));
            Rejects("template skill without a template", r => r.TemplateSkills.Add(new DatChargenTemplateSkillRow { HeritageGroup = 1, TemplateId = 50, SkillId = skillIds[0], AdvancementClass = 2 }));
            Rejects("template without a name", r => r.Templates[0].Name = "");
            Rejects("template twice", r => r.Templates.Add(new DatChargenTemplateRow { HeritageGroup = r.Templates[0].HeritageGroup, Id = r.Templates[0].Id, Name = "x" }));
        }

        [TestMethod]
        public void ChargenInsertScript_EmptiesAllFourTablesAndWritesTheRows()
        {
            var rows = DatTables.ToRows(UnpackCharGen(CustomDMChargenBytes()));
            var sql = DatTables.ChargenInsertScript(rows);

            StringAssert.StartsWith(sql, "DELETE FROM `dat_chargen_template_skill`;");
            StringAssert.Contains(sql, "INSERT INTO `dat_chargen` (`id`, `attribute_Credits`, `skill_Credits`) VALUES (1, 330, 50);");
            Assert.AreEqual(1 + rows.Overrides.Count + rows.Templates.Count + rows.TemplateSkills.Count, sql.Split('\n').Count(l => l.StartsWith("INSERT")));
            Assert.AreEqual(4, sql.Split('\n').Count(l => l.StartsWith("DELETE")));
            StringAssert.Contains(DatTables.ChargenCreateScript, "CREATE TABLE IF NOT EXISTS `dat_chargen_template_skill`");
        }

        [TestMethod]
        public void TableStep_Chargen_WritesOnlyWhenChanged()
        {
            var xp = CustomDMXpRecord();
            var skills = CustomDMSkillRecord();
            var chargenBytes = CustomDMChargenBytes();
            var path = TempDatWith(xp, skills);

            try
            {
                var source = new FakeSource
                {
                    Rows = new DatTableRows
                    {
                        Xp = DatTables.ToRows(UnpackXp(xp)),
                        Skills = DatTables.ToRows(UnpackSkills(skills)),
                        Chargen = DatTables.ToRows(UnpackCharGen(chargenBytes))
                    }
                };

                using (var writer = new DatWriter(path))
                {
                    writer.Save(DatTableStep.ChargenRecordId, chargenBytes);

                    var first = DatTableStep.Apply(writer, source, 20250101);
                    Assert.IsNotNull(first.ChargenHash);
                    Assert.AreEqual(DatTables.Hash(source.Rows.Chargen), DatTables.Hash(DatTables.ToRows(UnpackCharGen(writer.Read(DatTableStep.ChargenRecordId)))));

                    var second = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(second.Unchanged, "character generation");

                    source.Rows.Chargen.Chargen.SkillCredits += 1;

                    var third = DatTableStep.Apply(writer, source, 20250101);
                    CollectionAssert.Contains(third.Written, "character generation");
                    Assert.IsTrue(UnpackCharGen(writer.Read(DatTableStep.ChargenRecordId)).HeritageGroups.Values.All(g => g.SkillCredits == source.Rows.Chargen.Chargen.SkillCredits));

                    var kept = writer.Read(DatTableStep.ChargenRecordId);

                    source.Rows.Chargen = null;

                    var fourth = DatTableStep.Apply(writer, source, 20250102);
                    Assert.IsNull(fourth.ChargenHash);
                    AssertSameBytes(kept, writer.Read(DatTableStep.ChargenRecordId));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
