using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader.Tests
{
    /// <summary>The codecs and edit lists behind the record steps of the recipes (pre-ToD images and XP table, string table and UI layout edits, the embedded data files).</summary>
    [TestClass]
    public class DatRecipeRecordTests
    {
        static DatRecipeRecordTests()
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        private const uint ChargenHeritageUiId = 0x21000046;
        private const uint PreToDMapId = 0x0600127D;
        private const uint XpTableId = 0x0E000018;

        private static byte[] ReadRecord(string datFile, uint id)
        {
            var path = Path.Combine(TestSourceDats.Folder, datFile);

            if (!File.Exists(path))
                Assert.Inconclusive($"Source dat not found: {path}");

            return new DatDatabase(path).GetReaderForFile(id)?.Buffer ?? throw new AssertFailedException($"{datFile} has no record {id:X8}.");
        }

        private static MasterProperty ReadMasterProperty()
        {
            return DatRecordContext.Unpack<MasterProperty>(ReadRecord(DatBuildRecipes.PortalBaseDat, DatLayoutEdits.MasterPropertyId));
        }

        private static StringTable MakeTable(params (uint Id, string Text)[] entries)
        {
            var table = new StringTable { Language = 1 };

            foreach (var (id, text) in entries)
            {
                var entry = new StringTableData { Id = id };
                entry.Strings.Add(text);
                table.StringTableData.Add(entry);
            }

            return table;
        }

        [TestMethod]
        public void PreToDTexture_SwapsBlueAndRed_AndPackRoundTrips()
        {
            var record = new byte[12 + 6];
            BitConverter.GetBytes(0x0600127Du).CopyTo(record, 0);
            BitConverter.GetBytes(2).CopyTo(record, 4);
            BitConverter.GetBytes(1).CopyTo(record, 8);
            new byte[] { 1, 2, 3, 4, 5, 6 }.CopyTo(record, 12);

            var texture = Texture.UnpackPreToD(record);

            CollectionAssert.AreEqual(new byte[] { 3, 2, 1, 6, 5, 4 }, texture.SourceData);
            Assert.AreEqual(2, texture.Width);
            Assert.AreEqual(1, texture.Height);

            var again = DatRecordContext.Unpack<Texture>(texture.Pack());

            Assert.AreEqual(texture.Id, again.Id);
            Assert.AreEqual(texture.Format, again.Format);
            CollectionAssert.AreEqual(texture.SourceData, again.SourceData);
        }

        [TestMethod]
        public void PreToDTexture_RejectsARecordOfTheWrongSize()
        {
            var record = new byte[12 + 5];
            BitConverter.GetBytes(2).CopyTo(record, 4);
            BitConverter.GetBytes(1).CopyTo(record, 8);

            Assert.ThrowsExactly<InvalidDataException>(() => Texture.UnpackPreToD(record));
        }

        [TestMethod]
        public void PreToDTexture_OfTheRetailMap_PacksToAToDRecord()
        {
            var texture = Texture.UnpackPreToD(ReadRecord(DatBuildRecipes.PreToDPortalDat, PreToDMapId));
            var again = DatRecordContext.Unpack<Texture>(texture.Pack());

            Assert.AreEqual(PreToDMapId, again.Id);
            Assert.AreEqual(texture.Width, again.Width);
            Assert.AreEqual(texture.Height, again.Height);
            CollectionAssert.AreEqual(texture.SourceData, again.SourceData);
        }

        [TestMethod]
        public void PreToDXpTable_PacksToATodRecordWithTheSameLists()
        {
            var table = XpTable.UnpackPreToD(ReadRecord(DatBuildRecipes.PreToDPortalDat, XpTableId));
            var again = DatRecordContext.Unpack<XpTable>(table.Pack());

            CollectionAssert.AreEqual(table.AttributeXpList, again.AttributeXpList);
            CollectionAssert.AreEqual(table.VitalXpList, again.VitalXpList);
            CollectionAssert.AreEqual(table.TrainedSkillXpList, again.TrainedSkillXpList);
            CollectionAssert.AreEqual(table.SpecializedSkillXpList, again.SpecializedSkillXpList);
            CollectionAssert.AreEqual(table.CharacterLevelXPList, again.CharacterLevelXPList);
            CollectionAssert.AreEqual(table.CharacterLevelSkillCreditList, again.CharacterLevelSkillCreditList);
        }

        [TestMethod]
        public void StringTable_PackRoundTrips()
        {
            var table = MakeTable((1, "one"), (2, "two\nlines"));
            var again = DatRecordContext.Unpack<StringTable>(table.Pack());

            Assert.AreEqual(2, again.StringTableData.Count);
            Assert.AreEqual("two\nlines", again.StringTableData[1].Strings[0]);
            CollectionAssert.AreEqual(table.Pack(), again.Pack());
        }

        [TestMethod]
        public void StringEdits_DiffThenApply_ReproducesTheTarget()
        {
            var source = MakeTable((1, "one"), (2, "two"), (3, "three"));
            var target = MakeTable((1, "one"), (2, ""), (3, "THREE"));

            var edits = DatStringEdits.Diff(source, target);

            Assert.AreEqual(2, edits.Count);
            Assert.AreEqual("00000002", edits[0].Id);

            DatStringEdits.Apply(source, edits);

            CollectionAssert.AreEqual(target.Pack(), source.Pack());
        }

        [TestMethod]
        public void StringEdits_RejectAnUnknownEntryOrString()
        {
            var table = MakeTable((1, "one"));

            Assert.ThrowsExactly<InvalidDataException>(() => DatStringEdits.Apply(table, new List<DatStringEdit> { new DatStringEdit { Id = "00000009", Text = "x" } }));
            Assert.ThrowsExactly<InvalidDataException>(() => DatStringEdits.Apply(table, new List<DatStringEdit> { new DatStringEdit { Id = "00000001", Index = 1, Text = "x" } }));
        }

        [TestMethod]
        public void ChargenTextEdits_OfTheRecipe_ApplyToTheEndOfRetailRecord()
        {
            var edits = DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.InfiltrationChargenTextFile);
            var table = DatRecordContext.Unpack<StringTable>(ReadRecord(DatBuildRecipes.LanguageBaseDat, LanguageRecords.ChargenTextId));

            DatStringEdits.Apply(table, edits);

            var edited = table.StringTableData.ToDictionary(e => e.Id);

            foreach (var edit in edits)
                Assert.AreEqual(edit.Text, edited[Convert.ToUInt32(edit.Id, 16)].Strings[edit.Index]);

            Assert.AreEqual("Random", edited[0x05D3F822].Strings[0]);
        }

        [TestMethod]
        public void LayoutPack_RoundTripsEveryLayoutOfTheEndOfRetailLanguageDat()
        {
            var path = Path.Combine(TestSourceDats.Folder, DatBuildRecipes.LanguageBaseDat);

            if (!File.Exists(path))
                Assert.Inconclusive($"Source dat not found: {path}");

            var master = ReadMasterProperty();
            var database = new DatDatabase(path);
            var ids = database.AllFiles.Keys.Where(id => (id >> 24) == 0x21).ToList();

            Assert.IsTrue(ids.Count > 0);

            foreach (var id in ids)
            {
                var record = database.GetReaderForFile(id).Buffer;

                CollectionAssert.AreEqual(record, DatLayoutEdits.Unpack(record, master).Pack(), $"Layout {id:X8} does not round-trip.");
            }
        }

        [TestMethod]
        public void LayoutEdits_ChangeOnlyTheNamedMediaItem()
        {
            var master = ReadMasterProperty();
            var record = ReadRecord(DatBuildRecipes.LanguageBaseDat, ChargenHeritageUiId);
            var original = DatLayoutEdits.Unpack(record, master);
            var edit = DatRecipeData.Load<List<DatLayoutMediaEdit>>(LanguageRecords.InfiltrationChargenHeritageUiFile)[0];

            var changed = DatLayoutEdits.Unpack(record, master);
            DatLayoutEdits.Apply(changed, new List<DatLayoutMediaEdit> { edit });

            var diff = DatLayoutEdits.Diff(original, DatLayoutEdits.Unpack(changed.Pack(), master));

            Assert.AreEqual(1, diff.Count);
            Assert.AreEqual(edit.Element, diff[0].Element);
            Assert.AreEqual(edit.State, diff[0].State);
            Assert.AreEqual(edit.Media, diff[0].Media);
            Assert.AreEqual(edit.File, diff[0].File);
        }

        [TestMethod]
        public void LayoutEdits_RejectAnUnknownElement()
        {
            var layout = DatLayoutEdits.Unpack(ReadRecord(DatBuildRecipes.LanguageBaseDat, ChargenHeritageUiId), ReadMasterProperty());

            Assert.ThrowsExactly<InvalidDataException>(() => DatLayoutEdits.Apply(layout, new List<DatLayoutMediaEdit> { new DatLayoutMediaEdit { Element = "0000FFFF", State = "", Media = 0, File = "00000000" } }));
        }

        [TestMethod]
        public void RecipeDataFiles_AreEmbeddedAndHoldTheMeasuredEditCounts()
        {
            Assert.AreEqual(33, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.InfiltrationChargenTextFile).Count);
            Assert.AreEqual(10, DatRecipeData.Load<List<DatLayoutMediaEdit>>(LanguageRecords.InfiltrationChargenHeritageUiFile).Count);
            Assert.AreEqual(840, DatRecipeData.Load<List<Dictionary<string, System.Text.Json.JsonElement>>>(InfiltrationRecords.SpellEditsFile).Count);
            Assert.IsNotNull(DatRecipeData.Load<DatChargenEdits>(InfiltrationRecords.ChargenEditsFile));
        }
    }
}
