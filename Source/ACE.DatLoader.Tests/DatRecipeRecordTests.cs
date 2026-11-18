using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;
using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

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
        public void IndexedImage_KeepsItsPalette_WithIndicesTimesPaletteRepeat()
        {
            var record = new List<byte>();
            record.AddRange(BitConverter.GetBytes(0x05001666u));
            record.AddRange(BitConverter.GetBytes(2u));
            record.AddRange(BitConverter.GetBytes(2));
            record.AddRange(BitConverter.GetBytes(2));
            record.AddRange(new byte[] { 0x0A, 0x08, 0xFF, 0x00 });
            record.AddRange(BitConverter.GetBytes(0x04000DBAu));

            var image = DatRecordContext.Unpack<Texture>(DatPortalImporter.ConvertImageKeepingFormat(record.ToArray(), 0x06009666, id => id + 0x3000));

            Assert.AreEqual(SurfacePixelFormat.PFID_INDEX16, image.Format);
            Assert.AreEqual(0, image.Unknown);
            Assert.AreEqual(2, image.Width);
            Assert.AreEqual(0x04003DBAu, image.DefaultPaletteId);
            CollectionAssert.AreEqual(new byte[] { 0x50, 0, 0x40, 0, 0xF8, 7, 0, 0 }, image.SourceData);
        }

        [TestMethod]
        public void PlainPixelImage_KeepsItsPixels()
        {
            var record = new List<byte>();
            record.AddRange(BitConverter.GetBytes(0x05000136u));
            record.AddRange(BitConverter.GetBytes(7u));
            record.AddRange(BitConverter.GetBytes(2));
            record.AddRange(BitConverter.GetBytes(1));
            record.AddRange(new byte[] { 1, 2, 3, 4 });

            var image = DatRecordContext.Unpack<Texture>(DatPortalImporter.ConvertImageKeepingFormat(record.ToArray(), 0x06000136));

            Assert.AreEqual(SurfacePixelFormat.PFID_R5G6B5, image.Format);
            Assert.IsNull(image.DefaultPaletteId);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, image.SourceData);
        }

        [TestMethod]
        public void Palette_Repeated_RepeatsEveryColor_AndPackRoundTrips()
        {
            var palette = new Palette();
            palette.Colors.AddRange(new uint[] { 0xFF112233, 0xFF445566 });

            var repeated = palette.Repeated(0x04003DBA, 3);
            var unpacked = DatRecordContext.Unpack<Palette>(repeated.Pack());

            Assert.AreEqual(0x04003DBAu, unpacked.Id);
            CollectionAssert.AreEqual(new uint[] { 0xFF112233, 0xFF112233, 0xFF112233, 0xFF445566, 0xFF445566, 0xFF445566 }, unpacked.Colors);
        }

        [TestMethod]
        public void IndexedImage_And_Palette_ReproduceTheCustomFiles()
        {
            var customFiles = Path.Combine(TestSourceDats.Folder, "Custom Files", "Portal", "CustomDM", "Missing Files");
            var imagePath = Path.Combine(customFiles, "06-Texture", "06009666.bin");

            if (!File.Exists(imagePath))
                Assert.Inconclusive($"Custom file not found: {imagePath}");

            var custom = File.ReadAllBytes(imagePath);
            var customImage = DatRecordContext.Unpack<Texture>(custom);

            var image = DatPortalImporter.ConvertImageKeepingFormat(ReadRecord(DatBuildRecipes.PreToDPortalDat, 0x05001666), 0x06009666, id => customImage.DefaultPaletteId.Value);
            CollectionAssert.AreEqual(custom, image);

            var palette = DatRecordContext.Unpack<Palette>(ReadRecord(DatBuildRecipes.PreToDPortalDat, customImage.DefaultPaletteId.Value));
            var palettePath = Directory.EnumerateFiles(Path.Combine(TestSourceDats.Folder, "Custom Files", "Portal"), customImage.DefaultPaletteId.Value.ToString("X8") + ".bin", SearchOption.AllDirectories).FirstOrDefault();

            if (palettePath == null)
                Assert.Inconclusive("Custom palette not found.");

            CollectionAssert.AreEqual(File.ReadAllBytes(palettePath),palette.Repeated(palette.Id, DatPortalImporter.PaletteRepeat).Pack());
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

        private static void AssertEveryRecordRoundTrips<T>(uint type, Func<T, byte[]> pack, Func<T, bool> skip = null) where T : FileType, new()
        {
            var path = Path.Combine(TestSourceDats.Folder, DatBuildRecipes.PortalBaseDat);

            if (!File.Exists(path))
                Assert.Inconclusive($"Source dat not found: {path}");

            var database = new DatDatabase(path);
            var ids = database.AllFiles.Keys.Where(id => (id >> 24) == type).ToList();
            var checkedCount = 0;

            Assert.IsTrue(ids.Count > 0);

            foreach (var id in ids)
            {
                var record = database.GetReaderForFile(id).Buffer;
                var value = DatRecordContext.Unpack<T>(record);

                if (skip != null && skip(value))
                    continue;

                CollectionAssert.AreEqual(record, pack(value), $"Record {id:X8} does not round-trip.");
                checkedCount++;
            }

            Assert.IsTrue(checkedCount > ids.Count / 2, $"Only {checkedCount} of {ids.Count} records were checked.");
        }

        [TestMethod]
        public void SetupPack_RoundTripsTheEndOfRetailSetupsWithoutHooks()
        {
            AssertEveryRecordRoundTrips<SetupModel>(0x02, setup => setup.Pack(), setup => setup.PlacementFrames.Values.Any(placement => placement.AnimFrame.Hooks.Count > 0));
        }

        [TestMethod]
        public void SurfacePack_RoundTripsEveryEndOfRetailSurface()
        {
            AssertEveryRecordRoundTrips<Surface>(0x08, surface => surface.Pack());
        }

        [TestMethod]
        public void SurfaceTexturePack_RoundTripsEveryEndOfRetailSurfaceTexture()
        {
            AssertEveryRecordRoundTrips<SurfaceTexture>(0x05, surfaceTexture => surfaceTexture.Pack());
        }

        [TestMethod]
        public void ScenePack_RoundTripsEveryEndOfRetailScene()
        {
            AssertEveryRecordRoundTrips<Scene>(0x12, scene => scene.Pack());
        }

        [TestMethod]
        public void Remap_ChangesOnlyTheReferencedIds()
        {
            var setup = new SetupModel();
            setup.Parts.Add(0x01000002);
            setup.Parts.Add(0x01000003);
            setup.Remap(id => id + 0x5000);

            CollectionAssert.AreEqual(new uint[] { 0x01005002, 0x01005003 }, setup.Parts);
            Assert.AreEqual(0u, setup.DefaultMotionTable);

            var surfaceTexture = new SurfaceTexture();
            surfaceTexture.Textures.Add(0x06000002);
            surfaceTexture.Remap(id => id + 0x8000);

            Assert.AreEqual(0x06008002u, surfaceTexture.Textures[0]);
        }

        [TestMethod]
        public void LayoutEdits_ChangeOnlyTheNamedMediaItem()
        {
            var master = ReadMasterProperty();
            var record = ReadRecord(DatBuildRecipes.LanguageBaseDat, ChargenHeritageUiId);
            var original = DatLayoutEdits.Unpack(record, master);
            var edit = DatRecipeData.Load<List<DatLayoutMediaEdit>>(LanguageRecords.ChargenHeritageUiFile)[0];

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
        public void ChargenEdits_DiffOfAChangedRecord_AppliesBackToTheSameBytes()
        {
            var record = ReadRecord(DatBuildRecipes.PortalBaseDat, InfiltrationRecords.ChargenId);
            var changed = DatRecordContext.Unpack<CharGen>(record);
            DatRecipeData.Load<DatChargenEdits>(CustomDMRecords.ChargenEditsFile).Apply(changed);

            var edits = DatChargenEdits.Diff(DatRecordContext.Unpack<CharGen>(record), changed);
            var again = DatRecordContext.Unpack<CharGen>(record);
            edits.Apply(again);

            Assert.IsTrue(again.Pack().SequenceEqual(changed.Pack()));
            Assert.IsFalse(again.Pack().SequenceEqual(record));
        }

        [TestMethod]
        public void GroundTextures_ConvertTheOriginalImagesAtTheirOwnSize()
        {
            var textures = DatRecipeData.Load<List<GroundTextureRecords.GroundTexture>>(GroundTextureRecords.TexturesFile);

            Assert.AreEqual(38, textures.Count);

            foreach (var texture in textures.Take(3))
            {
                var source = Convert.ToUInt32(texture.Source, 16);
                var image = DatPortalImporter.ConvertImage(ReadRecord(DatBuildRecipes.PreToDPortalDat, source), Convert.ToUInt32(texture.Image, 16), id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id), out var width, out var height, out _);

                Assert.IsTrue(width <= 128 && height <= 128);
                Assert.AreEqual(Convert.ToUInt32(texture.Image, 16), BitConverter.ToUInt32(image, 0));
                Assert.AreEqual(24 + width * height * (image[16] == 28 ? 1 : 3), image.Length);

                var terrain = GroundTextureRecords.ToTerrainFormat(image, 2);

                Assert.AreEqual(image[16] == 28 ? 244u : 21u, BitConverter.ToUInt32(terrain, 16));
                Assert.AreEqual(24 + width * height * (image[16] == 28 ? 1 : 4), terrain.Length);
            }
        }

        [TestMethod]
        public void ConvertIcon_MakesAnA8R8G8B8Record_WithBlueGreenRedAlphaBytes()
        {
            var source = new List<byte>();
            source.AddRange(BitConverter.GetBytes(0x06001F99u));
            source.AddRange(BitConverter.GetBytes(2));
            source.AddRange(BitConverter.GetBytes(1));
            source.AddRange(new byte[] { 1, 2, 3, 10, 20, 30 });

            var icon = DatRecordContext.Unpack<Texture>(IconRecords.ConvertIcon(source.ToArray(), 0x06001F99));

            Assert.AreEqual(0x06001F99u, icon.Id);
            Assert.AreEqual(2, icon.Width);
            Assert.AreEqual(1, icon.Height);
            Assert.AreEqual(SurfacePixelFormat.PFID_A8R8G8B8, icon.Format);
            Assert.AreEqual(Texture.ToDHeaderValue, icon.Unknown);
            CollectionAssert.AreEqual(new byte[] { 3, 2, 1, 255, 30, 20, 10, 255 }, icon.SourceData);
            Assert.AreEqual(1, DatRecipeData.Load<List<IconRecords.Icon>>(IconRecords.IconsFile).Count);
        }

        [TestMethod]
        public void RecipeDataFiles_OfCustomDM_AreEmbedded()
        {
            Assert.AreEqual(33, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMChargenTextFile).Count);
            Assert.IsNotNull(DatRecipeData.Load<DatChargenEdits>(CustomDMRecords.ChargenEditsFile).Templates);
        }

        [TestMethod]
        public void RecipeDataFiles_AreEmbeddedAndHoldTheMeasuredEditCounts()
        {
            Assert.AreEqual(33, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.InfiltrationChargenTextFile).Count);
            Assert.AreEqual(10, DatRecipeData.Load<List<DatLayoutMediaEdit>>(LanguageRecords.ChargenHeritageUiFile).Count);
            Assert.AreEqual(840, DatRecipeData.Load<List<Dictionary<string, System.Text.Json.JsonElement>>>(InfiltrationRecords.SpellEditsFile).Count);
            Assert.IsNotNull(DatRecipeData.Load<DatChargenEdits>(InfiltrationRecords.ChargenEditsFile));
        }
    }
}
