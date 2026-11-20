using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;
using ACE.DatLoader.Entity;
using ACE.DatLoader.Entity.AnimationHooks;
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
            Assert.AreEqual(0u, image.DataCategory);
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
        public void OptionStrings_AddTheTextureDetailLabelsAsNewEntriesOfTheEndOfRetailTextStrings()
        {
            var table = DatRecordContext.Unpack<StringTable>(ReadRecord(DatBuildRecipes.LanguageBaseDat, LanguageRecords.TextStringsId));
            var existing = table.StringTableData.Select(e => e.Id).ToHashSet();
            var labels = DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMOptionStringsFile).Where(e => e.Name != null).ToList();

            Assert.AreEqual(4, labels.Count);
            CollectionAssert.AreEqual(new[] { "Full", "1/2", "1/4", "1/8" }, labels.Select(e => e.Text).ToList());

            foreach (var label in labels)
                Assert.IsFalse(existing.Contains(SpellTable.ComputeHash(label.Name)));

            DatStringEdits.Apply(table, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMOptionStringsFile));

            var reread = DatRecordContext.Unpack<StringTable>(table.Pack());
            Assert.AreEqual(236 + 4, reread.StringTableData.Count);
            Assert.AreEqual("1/4", reread.StringTableData.Single(e => e.Id == SpellTable.ComputeHash("ID_Graphics_TexQuarter")).Strings[0]);

            // the client rejects a record whose entries are not in ascending bucket order (token modulo 383 for size byte 5)
            Assert.AreEqual(5, reread.Unknown);
            var bucketOrder = reread.StringTableData.Select(e => e.Id % 383).ToList();
            CollectionAssert.AreEqual(bucketOrder.OrderBy(b => b).ToList(), bucketOrder);
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
        public void RegionPack_RoundTripsTheEndOfRetailRegion()
        {
            AssertEveryRecordRoundTrips<RegionDesc>(0x13, region => region.Pack());
        }

        [TestMethod]
        public void AnimationPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<Animation>(0x03, record => record.Pack());
        }

        [TestMethod]
        public void PaletteSetPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<PaletteSet>(0x0F, record => record.Pack());
        }

        [TestMethod]
        public void ClothingPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ClothingTable>(0x10, record => record.Pack());
        }

        [TestMethod]
        public void SoundTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<SoundTable>(0x20, record => record.Pack());
        }

        [TestMethod]
        public void ParticleEmitterPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ParticleEmitterInfo>(0x32, record => record.Pack());
        }

        [TestMethod]
        public void PhysicsScriptPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<PhysicsScript>(0x33, record => record.Pack());
        }

        [TestMethod]
        public void WavePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<Wave>(0x0A, record => record.Pack());
        }

        [TestMethod]
        public void MotionTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<MotionTable>(0x09, record => record.Pack());
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
            Assert.AreEqual(Texture.ToDHeaderValue, icon.DataCategory);
            CollectionAssert.AreEqual(new byte[] { 3, 2, 1, 255, 30, 20, 10, 255 }, icon.SourceData);
            Assert.AreEqual(1, DatRecipeData.Load<List<IconRecords.Icon>>(IconRecords.IconsFile).Count);
        }

        private static byte[] FadeScriptBytes(uint id, float start, float end)
        {
            var bytes = new List<byte>();
            bytes.AddRange(BitConverter.GetBytes(id));
            bytes.AddRange(BitConverter.GetBytes(1u));
            bytes.AddRange(BitConverter.GetBytes(0.0));
            bytes.AddRange(BitConverter.GetBytes((uint)AnimationHookType.Transparent));
            bytes.AddRange(BitConverter.GetBytes(0));
            bytes.AddRange(BitConverter.GetBytes(start));
            bytes.AddRange(BitConverter.GetBytes(end));
            bytes.AddRange(BitConverter.GetBytes(1f));
            return bytes.ToArray();
        }

        [TestMethod]
        public void SneakScripts_PackAOneHookFadeScript()
        {
            CollectionAssert.AreEqual(FadeScriptBytes(SneakScriptRecords.BeginScriptId, 0f, 0.5f), SneakScriptRecords.FadeScript(SneakScriptRecords.BeginScriptId, 0f, 0.5f));
            CollectionAssert.AreEqual(new byte[] { 0xA0, 0x13, 0x00, 0x33, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x14, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x3F, 0, 0, 0x80, 0x3F }, SneakScriptRecords.FadeScript(SneakScriptRecords.BeginScriptId, 0f, 0.5f));

            var script = DatRecordContext.Unpack<PhysicsScript>(SneakScriptRecords.FadeScript(SneakScriptRecords.EndScriptId, 0.5f, 0f));

            Assert.AreEqual(1, script.ScriptData.Count);
            Assert.AreEqual(0.5f, ((ACE.DatLoader.Entity.AnimationHooks.TransparentHook)script.ScriptData[0].Hook).Start);
        }

        [TestMethod]
        public void ScriptTablePack_RoundTripsTheEndOfRetailTable_AndTheSneakEntriesAreAdded()
        {
            var record = ReadRecord(DatBuildRecipes.PortalBaseDat, SneakScriptRecords.ScriptTableId);
            var table = DatRecordContext.Unpack<PhysicsScriptTable>(record);

            CollectionAssert.AreEqual(record, table.Pack());

            var count = table.ScriptTable.Count;
            var entry = new PhysicsScriptTableData();
            entry.Scripts.Add(ScriptAndModData.Create(1f, SneakScriptRecords.BeginScriptId));
            table.ScriptTable[SneakScriptRecords.BeginTableKey] = entry;

            var again = DatRecordContext.Unpack<PhysicsScriptTable>(table.Pack());

            Assert.AreEqual(count + 1, again.ScriptTable.Count);
            Assert.AreEqual(SneakScriptRecords.BeginScriptId, again.ScriptTable[SneakScriptRecords.BeginTableKey].Scripts[0].ScriptId);
        }

        [TestMethod]
        public void Lifestones_AreRebuiltFromThe2005RecordsAsTheCustomFilesHoldThem()
        {
            var folder = Path.Combine(TestSourceDats.Folder, "Custom Files", "Portal", "CustomDM");
            var backup = Path.Combine(TestSourceDats.Folder, "_replaced-by-recipes", "Portal", "CustomDM");
            var data = DatRecipeData.Load<ModelRecords.Data>(CustomDMRecords.LifestonesFile);
            var records = ModelRecords.Build(data, id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id));

            Assert.AreEqual(4, records.Count);

            foreach (var record in records)
            {
                var file = new[] { folder, backup }.Where(Directory.Exists).SelectMany(directory => Directory.EnumerateFiles(directory, record.Key.ToString("X8") + "*.bin")).FirstOrDefault();

                if (file == null)
                    Assert.Inconclusive($"Custom file {record.Key:X8} not found.");

                CollectionAssert.AreEqual(File.ReadAllBytes(file), record.Value, $"Lifestone {record.Key:X8}");
            }
        }

        [TestMethod]
        public void MiscModels_AreBuiltFromThe2005Dat_WithEveryReferenceMapped()
        {
            var data = DatRecipeData.Load<ModelRecords.Data>(CustomDMRecords.MiscModelsFile);
            var records = ModelRecords.Build(data, id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id));

            Assert.AreEqual(191, records.Count);
            Assert.AreEqual(records.Count, records.Select(record => record.Key).Distinct().Count());

            foreach (var record in records.Where(record => (record.Key >> 24) != 0x08))
                Assert.AreEqual(record.Key, BitConverter.ToUInt32(record.Value, 0), $"The record {record.Key:X8} starts with another id.");
        }

        [TestMethod]
        public void SurfaceTextureEdits_WriteOneTexturePerSurfaceTexture()
        {
            var expected = new Dictionary<string, (uint Id, uint Texture)>
            {
                { SurfaceTextureRecords.GroundFile, (0x05001C3A, 0x06006D3B) },
                { SurfaceTextureRecords.RockFile, (0x05000FF3, 0x06008FF3) }
            };

            foreach (var file in expected)
            {
                var entry = DatRecipeData.Load<List<SurfaceTextureRecords.Entry>>(file.Key).Single();
                var record = SurfaceTexture.Create(Convert.ToUInt32(entry.Id, 16), Convert.ToUInt32(entry.Texture, 16)).Pack();
                var surfaceTexture = DatRecordContext.Unpack<SurfaceTexture>(record);

                Assert.AreEqual(17, record.Length);
                Assert.AreEqual(file.Value.Id, surfaceTexture.Id);
                CollectionAssert.AreEqual(new[] { file.Value.Texture }, surfaceTexture.Textures);
            }
        }

        [TestMethod]
        public void Trees_IncludeTheParticleEmitterAndTheTwoScriptsThatUseIt()
        {
            var records = ModelRecords.Build(DatRecipeData.Load<ModelRecords.Data>(ModelRecords.TreesFile), id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id)).ToDictionary(record => record.Key, record => record.Value);

            foreach (var id in new uint[] { 0x320013DC, 0x3300272D, 0x3300272E })
            {
                Assert.IsTrue(records.ContainsKey(id), $"{id:X8} is not built");
                Assert.AreEqual(id, BitConverter.ToUInt32(records[id], 0));
            }

            var script = DatRecordContext.Unpack<PhysicsScript>(records[0x3300272D]);
            Assert.IsTrue(script.ScriptData.Any(data => data.Hook is CreateParticleHook hook && hook.EmitterInfoId == 0x320013DC));
        }

        [TestMethod]
        public void FrozenFields_ModelsAreBuiltFromThe2001Dat_WithTheSceneDensityEdit()
        {
            var data = DatRecipeData.Load<ModelRecords.Data>(RegionRecords.ModelsFile);
            var records = ModelRecords.Build(data, id => ReadRecord(DatBuildRecipes.Portal2001Dat, id));

            Assert.AreEqual(32, records.Count);

            var scene = DatRecordContext.Unpack<Scene>(records.Single(record => record.Key == 0x1200025B).Value);

            Assert.IsTrue(scene.Objects.All(o => o.Freq == 0.25f));
        }

        [TestMethod]
        public void OldModels_AreBuiltFromThe2005Dat_WithEveryReferenceMapped()
        {
            var counts = new Dictionary<string, int>
            {
                { CustomDMRecords.BanderlingFile, 2 }, { CustomDMRecords.CarenziFile, 64 }, { CustomDMRecords.DrudgeFile, 87 }, { CustomDMRecords.LugianLeatherFile, 48 },
                { CustomDMRecords.LugianMetalFile, 1 }, { CustomDMRecords.MattekarFile, 40 }, { CustomDMRecords.MiteFile, 53 }, { CustomDMRecords.MiteAlternatePalettesFile, 11 },
                { CustomDMRecords.MosswartFile, 1 }, { CustomDMRecords.OlthoiFile, 5 }, { CustomDMRecords.ReedsharkFile, 56 }, { CustomDMRecords.SiraluunFile, 108 },
                { CustomDMRecords.TuskerFile, 35 }, { CustomDMRecords.UrsuinFile, 60 }
            };
            var ids = new HashSet<uint>();

            foreach (var file in counts)
            {
                var records = ModelRecords.Build(DatRecipeData.Load<ModelRecords.Data>(file.Key), id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id));

                Assert.AreEqual(file.Value, records.Count, file.Key);
                Assert.AreEqual(records.Count, records.Select(record => record.Key).Distinct().Count(), file.Key);

                foreach (var record in records)
                    ids.Add(record.Key);
            }

            Assert.AreEqual(562, ids.Count);
        }

        [TestMethod]
        public void UnusedOldModels_AreBuiltFromThe2005Dat_WithEveryReferenceMapped()
        {
            var counts = new Dictionary<string, int> { { CustomDMRecords.MonougaFile, 51 }, { CustomDMRecords.OldDrudgeFile, 50 }, { CustomDMRecords.SclavusFile, 47 }, { CustomDMRecords.TumerokFile, 59 } };

            foreach (var file in counts)
            {
                var records = ModelRecords.Build(DatRecipeData.Load<ModelRecords.Data>(file.Key), id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id));

                Assert.AreEqual(file.Value, records.Count, file.Key);
                Assert.AreEqual(records.Count, records.Select(record => record.Key).Distinct().Count(), file.Key);
            }
        }

        [TestMethod]
        public void MissingModels_AreBuiltFromThe2005Dat_WithEveryReferenceMapped()
        {
            var data = DatRecipeData.Load<ModelRecords.Data>(CustomDMRecords.MissingModelsFile);
            var records = ModelRecords.Build(data, id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id));

            Assert.AreEqual(200, records.Count);
            Assert.AreEqual(records.Count, records.Select(record => record.Key).Distinct().Count());
        }

        [TestMethod]
        public void FrozenFields_RegionEditAddsTheSceneToTheEndOfRetailRegion()
        {
            var original = DatRecordContext.Unpack<RegionDesc>(ReadRecord(DatBuildRecipes.PortalBaseDat, RegionRecords.RegionId));
            var region = DatRecordContext.Unpack<RegionDesc>(ReadRecord(DatBuildRecipes.PortalBaseDat, RegionRecords.RegionId));

            DatRecipeData.Load<RegionRecords.RegionEdits>(RegionRecords.RegionEditsFile).Apply(region);

            var packed = region.Pack();

            Assert.AreEqual(original.Pack().Length + 4, packed.Length);
            CollectionAssert.AreEqual(new uint[] { 0x1200025B }, DatRecordContext.Unpack<RegionDesc>(packed).SceneInfo.SceneTypes[60].Scenes);
        }

        [TestMethod]
        public void RecipeDataFiles_OfCustomDM_AreEmbedded()
        {
            Assert.AreEqual(34, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMChargenTextFile).Count);
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

        private static string RegionDatWithTextureWidths(Func<int, int> widthOfDistinctTexture)
        {
            var template = Path.Combine(TestSourceDats.Folder, DatBuildRecipes.PortalBaseDat);

            if (!File.Exists(template))
                Assert.Inconclusive($"Source dat not found: {template}");

            var path = Path.Combine(Path.GetTempPath(), $"RegionStepTests-{Guid.NewGuid():N}.dat");
            DatWriter.CreateEmpty(path, template);

            var regionRecord = ReadRecord(DatBuildRecipes.PortalBaseDat, 0x13000000);
            var region = DatRecordContext.Unpack<RegionDesc>(regionRecord);

            using (var writer = new DatWriter(path))
            {
                writer.Save(0x13000000, regionRecord);

                var written = new HashSet<uint>();

                foreach (var terrain in region.TerrainInfo.LandSurfaces.TexMerge.TerrainDesc)
                {
                    var surfaceTextureId = terrain.TerrainTex.TexGID;

                    if (!written.Add(surfaceTextureId))
                        continue;

                    var surfaceTextureRecord = ReadRecord(DatBuildRecipes.PortalBaseDat, surfaceTextureId);
                    var textureId = DatRecordContext.Unpack<SurfaceTexture>(surfaceTextureRecord).Textures[0];
                    var texture = DatRecordContext.Unpack<Texture>(ReadRecord(DatBuildRecipes.PortalBaseDat, textureId));
                    texture.Width = widthOfDistinctTexture(written.Count - 1);

                    writer.Save(surfaceTextureId, surfaceTextureRecord);
                    writer.Save(textureId, texture.Pack());
                }
            }

            return path;
        }

        [TestMethod]
        public void RegionStep_SetsTheMergeSizeAndTilesEachTerrainToFitItsTextureWidth()
        {
            var widths = new[] { 32, 64, 128 };
            var path = RegionDatWithTextureWidths(i => widths[i % widths.Length]);
            var tilings = new HashSet<uint>();

            try
            {
                using (var writer = new DatWriter(path))
                {
                    var step = GroundTextureRecords.RegionStep("2025-01-01");
                    step.Run(new DatRecordContext(writer, TestSourceDats.Folder, step));

                    var merge = DatRecordContext.Unpack<RegionDesc>(writer.Read(0x13000000)).TerrainInfo.LandSurfaces.TexMerge;
                    Assert.AreEqual(GroundTextureRecords.BaseTexSize, merge.BaseTexSize);

                    foreach (var terrain in merge.TerrainDesc)
                    {
                        var textureId = DatRecordContext.Unpack<SurfaceTexture>(writer.Read(terrain.TerrainTex.TexGID)).Textures[0];
                        var width = (uint)DatRecordContext.Unpack<Texture>(writer.Read(textureId)).Width;
                        Assert.AreEqual(GroundTextureRecords.BaseTexSize / width, terrain.TerrainTex.TexTiling, $"texture {textureId:X8}");
                        tilings.Add(terrain.TerrainTex.TexTiling);
                    }

                    Assert.IsTrue(tilings.Count > 1, "the test needs terrains of different widths");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void RegionStep_ATextureWidthThatDoesNotDivideTheMergeSize_IsRefused()
        {
            var path = RegionDatWithTextureWidths(i => i == 1 ? 100 : 128);

            try
            {
                using (var writer = new DatWriter(path))
                {
                    var step = GroundTextureRecords.RegionStep("2025-01-01");
                    Assert.ThrowsExactly<InvalidDataException>(() => step.Run(new DatRecordContext(writer, TestSourceDats.Folder, step)));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void DirectPixels_ReorderThePlanarPre24BitImageToBgrAndCopyOtherFormats()
        {
            var method = typeof(DatPortalImporter).GetMethod("DirectPixels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            var planar = new byte[16 + 6];
            planar[16] = 1; planar[17] = 2;
            planar[18] = 3; planar[19] = 4;
            planar[20] = 5; planar[21] = 6;
            CollectionAssert.AreEqual(new byte[] { 5, 3, 1, 6, 4, 2 }, (byte[])method.Invoke(null, new object[] { planar, 10u, 2, 6 }));

            var plain = new byte[16 + 4];
            plain[16] = 9; plain[19] = 7;
            CollectionAssert.AreEqual(new byte[] { 9, 0, 0, 7 }, (byte[])method.Invoke(null, new object[] { plain, 4u, 1, 4 }));
        }
    }
}
