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
        private const uint PreToDHardwareRegionId = 0x130F0000;

        private static byte[] ReadRecord(string datFile, uint id)
        {
            var path = TestSourceDats.Dat(datFile);

            return new DatDatabase(path).GetReaderForFile(id)?.Buffer ?? throw new AssertFailedException($"{datFile} has no record {id:X8}.");
        }

        private static MasterProperty ReadMasterProperty()
        {
            return FileType.Unpack<MasterProperty>(ReadRecord(DatBuildRecipes.PortalBaseDat, MasterProperty.FILE_ID));
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

            var image = FileType.Unpack<Texture>(DatPortalImporter.ConvertImageKeepingFormat(record.ToArray(), 0x06009666, id => id + 0x3000));

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

            var image = FileType.Unpack<Texture>(DatPortalImporter.ConvertImageKeepingFormat(record.ToArray(), 0x06000136));

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
            var unpacked = FileType.Unpack<Palette>(repeated.Pack());

            Assert.AreEqual(0x04003DBAu, unpacked.Id);
            CollectionAssert.AreEqual(new uint[] { 0xFF112233, 0xFF112233, 0xFF112233, 0xFF445566, 0xFF445566, 0xFF445566 }, unpacked.Colors);
        }

        [TestMethod]
        public void IndexedImage_And_Palette_ReproduceTheCustomFiles()
        {
            var customFiles = Path.Combine(TestSourceDats.Folder, "Custom Files", "Portal", "CustomDM", "Missing Files");
            var imagePath = TestSourceDats.Require(Path.Combine(customFiles, "06-Texture", "06009666.bin"));

            var custom = File.ReadAllBytes(imagePath);
            var customImage = FileType.Unpack<Texture>(custom);

            var image = DatPortalImporter.ConvertImageKeepingFormat(ReadRecord(DatBuildRecipes.PreToDPortalDat, 0x05001666), 0x06009666, id => customImage.DefaultPaletteId.Value);
            CollectionAssert.AreEqual(custom, image);

            var palette = FileType.Unpack<Palette>(ReadRecord(DatBuildRecipes.PreToDPortalDat, customImage.DefaultPaletteId.Value));
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

            var again = FileType.Unpack<Texture>(texture.Pack());

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
            var again = FileType.Unpack<Texture>(texture.Pack());

            Assert.AreEqual(PreToDMapId, again.Id);
            Assert.AreEqual(texture.Width, again.Width);
            Assert.AreEqual(texture.Height, again.Height);
            CollectionAssert.AreEqual(texture.SourceData, again.SourceData);
        }

        [TestMethod]
        public void PreToDXpTable_PacksToATodRecordWithTheSameLists()
        {
            var table = XpTable.UnpackPreToD(ReadRecord(DatBuildRecipes.PreToDPortalDat, XpTableId));
            var again = FileType.Unpack<XpTable>(table.Pack());

            CollectionAssert.AreEqual(table.AttributeXpList, again.AttributeXpList);
            CollectionAssert.AreEqual(table.VitalXpList, again.VitalXpList);
            CollectionAssert.AreEqual(table.TrainedSkillXpList, again.TrainedSkillXpList);
            CollectionAssert.AreEqual(table.SpecializedSkillXpList, again.SpecializedSkillXpList);
            CollectionAssert.AreEqual(table.CharacterLevelXPList, again.CharacterLevelXPList);
            CollectionAssert.AreEqual(table.CharacterLevelSkillCreditList, again.CharacterLevelSkillCreditList);
        }

        [TestMethod]
        public void PreToDGfxObj_OfEveryModelOfThe2005Dat_ReadsWithItsSurfacesAndPolygons()
        {
            var path = TestSourceDats.Dat(DatBuildRecipes.PreToDPortalDat);

            var pre = new DatDatabase(path, true);
            var read = 0;

            foreach (var id in pre.AllFiles.Keys.Where(k => k >> 24 == 0x01))
            {
                var model = GfxObj.UnpackPreToD(pre.GetReaderForFile(id).Buffer);

                Assert.AreEqual(id, model.Id);
                Assert.IsTrue(model.Surfaces.Count > 0 || model.Polygons.Count == 0, $"{id:X8} has polygons but no surfaces.");
                read++;
            }

            Assert.IsTrue(read > 0);
        }

        [TestMethod]
        public void PreToDRegion_OfTheHardwareRecord_ReadsWholeAndPacksToAToDRecord()
        {
            var record = ReadRecord(DatBuildRecipes.PreToDPortalDat, PreToDHardwareRegionId);
            var region = RegionDesc.UnpackPreToD(record);
            var skyObjects = region.SkyInfo.DayGroups.Sum(d => d.SkyObjects.Count);
            var packed = region.Pack();
            var again = FileType.Unpack<RegionDesc>(packed);

            Assert.AreEqual(4 * skyObjects, packed.Length - record.Length);
            Assert.AreEqual(skyObjects, again.SkyInfo.DayGroups.Sum(d => d.SkyObjects.Count));
            Assert.AreEqual(region.SkyInfo.DayGroups.Count, again.SkyInfo.DayGroups.Count);
            Assert.AreEqual(region.TerrainInfo.TerrainTypes.Count, again.TerrainInfo.TerrainTypes.Count);
            CollectionAssert.AreEqual(packed, again.Pack());
        }

        [TestMethod]
        public void PreToDChargen_ReadsWhole_AndTakingTheFaceKeepsTheOtherFieldsOfTheSex()
        {
            var chargen = CharGen.UnpackPreToD(ReadRecord(DatBuildRecipes.PreToDPortalDat, 0x0E000002));

            CollectionAssert.AreEqual(new uint[] { 1, 2, 3 }, chargen.HeritageGroups.Keys.OrderBy(k => k).ToArray());
            Assert.IsTrue(chargen.HeritageGroups.Values.All(h => h.Genders.Count == 2 && h.Genders.Values.All(s => s.HairStyleList.Count > 0 && s.EyeStripList.Count > 0 && s.NoseStripList.Count > 0 && s.MouthStripList.Count > 0)));

            var preToD = chargen.HeritageGroups[1].Genders[1];
            var target = new SexCG();
            target.TakeFaceFrom(preToD);

            Assert.AreSame(preToD.BaseObjDesc, target.BaseObjDesc);
            Assert.AreEqual(preToD.HairStyleList.Count, target.HairStyleList.Count);
            Assert.AreEqual(0u, target.SetupID);
        }

        [TestMethod]
        public void PreToDPortalConverter_ShiftsTheEffectNumbersFrom0x1EUp_AndKeepsTheLowOnes()
        {
            var path = TestSourceDats.Dat(DatBuildRecipes.PreToDPortalDat);

            const uint tableId = 0x34000004;

            var pre = new DatDatabase(path, true);
            var converted = new DatPreToDPortalConverter(pre, 0x06100000).Convert().First(r => r.Key == tableId);

            var before = FileType.Unpack<PhysicsScriptTable>(pre.GetReaderForFile(tableId).Buffer).ScriptTable;
            var after = FileType.Unpack<PhysicsScriptTable>(converted.Value).ScriptTable;

            Assert.AreEqual(before.Count, after.Count);

            foreach (var entry in before)
            {
                var key = entry.Key >= 0x1E ? entry.Key + 1 : entry.Key;
                CollectionAssert.AreEqual(entry.Value.Scripts.Select(s => s.ScriptId).ToList(), after[key].Scripts.Select(s => s.ScriptId).ToList(), $"effect {entry.Key:X2}");
            }

            // The server's Hide (0x74) plays the script the 2005 table had at 0x73, and its UnHide (0x75) the one it had at 0x74.
            Assert.AreEqual(before[0x73].Scripts[0].ScriptId, after[0x74].Scripts[0].ScriptId);
            Assert.AreEqual(before[0x74].Scripts[0].ScriptId, after[0x75].Scripts[0].ScriptId);
        }

        [TestMethod]
        public void PreToDPortalConverter_ShiftsTheMotionNumbersFrom0x115AndTheSoundNumbersFrom0x76_AndKeepsTheLowOnes()
        {
            var path = TestSourceDats.Dat(DatBuildRecipes.PreToDPortalDat);

            var pre = new DatDatabase(path, true);
            var converted = new DatPreToDPortalConverter(pre, 0x06100000).Convert().Where(r => r.Key >> 24 == 0x09 || r.Key >> 24 == 0x20 || r.Key >> 24 == 0x30).ToDictionary(r => r.Key, r => r.Value);

            uint Motion(uint number) => number >= 0x115 ? number + 3 : number;
            uint Command(uint command) => (command & 0xFFFF0000) | Motion(command & 0xFFFF);
            uint Key(uint key) => (Motion(key >> 16) << 16) | Motion(key & 0xFFFF);

            byte[] Packed(MotionData data)
            {
                var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream))
                    data.Pack(writer);

                return stream.ToArray();
            }

            int shiftedMotions = 0, shiftedSounds = 0, shiftedManeuvers = 0;

            foreach (var id in pre.AllFiles.Keys.Where(k => k >> 24 == 0x09))
            {
                var before = FileType.Unpack<MotionTable>(pre.GetReaderForFile(id).Buffer);
                var after = FileType.Unpack<MotionTable>(converted[id]);

                Assert.AreEqual(Command(before.DefaultStyle), after.DefaultStyle);
                Assert.AreEqual(before.StyleDefaults.Count, after.StyleDefaults.Count);
                Assert.AreEqual(before.Cycles.Count, after.Cycles.Count);
                Assert.AreEqual(before.Links.Count, after.Links.Count);

                foreach (var style in before.StyleDefaults)
                    Assert.AreEqual(Command(style.Value), after.StyleDefaults[Command(style.Key)]);

                foreach (var cycle in before.Cycles)
                {
                    CollectionAssert.AreEqual(Packed(cycle.Value), Packed(after.Cycles[Key(cycle.Key)]), $"{id:X8} cycle {cycle.Key:X8}");

                    if (Key(cycle.Key) != cycle.Key)
                        shiftedMotions++;
                }

                foreach (var link in before.Links)
                    foreach (var motion in link.Value)
                        CollectionAssert.AreEqual(Packed(motion.Value), Packed(after.Links[Key(link.Key)][Command(motion.Key)]), $"{id:X8} link {link.Key:X8} {motion.Key:X8}");
            }

            foreach (var id in pre.AllFiles.Keys.Where(k => k >> 24 == 0x20))
            {
                var before = FileType.Unpack<SoundTable>(pre.GetReaderForFile(id).Buffer);
                var after = FileType.Unpack<SoundTable>(converted[id]);

                Assert.AreEqual(before.Data.Count, after.Data.Count);

                foreach (var sound in before.Data)
                {
                    var key = sound.Key >= 0x76 ? sound.Key + 0x15 : sound.Key;

                    CollectionAssert.AreEqual(sound.Value.Data.Select(d => d.SoundId).ToList(), after.Data[key].Data.Select(d => d.SoundId).ToList(), $"{id:X8} sound {sound.Key:X2}");

                    if (key != sound.Key)
                        shiftedSounds++;
                }
            }

            foreach (var id in pre.AllFiles.Keys.Where(k => k >> 24 == 0x30))
            {
                var before = FileType.Unpack<CombatManeuverTable>(pre.GetReaderForFile(id).Buffer).CMT;
                var after = FileType.Unpack<CombatManeuverTable>(converted[id]).CMT;

                Assert.AreEqual(before.Count, after.Count);

                for (var i = 0; i < before.Count; i++)
                {
                    Assert.AreEqual(Command((uint)before[i].Style), (uint)after[i].Style);
                    Assert.AreEqual(Command((uint)before[i].Motion), (uint)after[i].Motion);

                    if (Command((uint)before[i].Motion) != (uint)before[i].Motion)
                        shiftedManeuvers++;
                }
            }

            Assert.IsTrue(shiftedMotions > 0 && shiftedSounds > 0 && shiftedManeuvers > 0, $"nothing was shifted: {shiftedMotions} cycles, {shiftedSounds} sounds, {shiftedManeuvers} maneuvers");
        }

        [TestMethod]
        public void PreToDPortalConverter_ConvertsEveryRecordOfThe2005Dat_ToRecordsTheEoRCodecsRead()
        {
            var path = TestSourceDats.Dat(DatBuildRecipes.PreToDPortalDat);

            var pre = new DatDatabase(path, true);
            var converter = new DatPreToDPortalConverter(pre, 0x06100000);
            var records = new Dictionary<uint, byte[]>();

            foreach (var record in converter.Convert())
                Assert.IsTrue(records.TryAdd(record.Key, record.Value), $"{record.Key:X8} is written twice");

            Assert.AreEqual(0, converter.Failures.Count, string.Join("\n", converter.Failures.Take(20)));

            var skipped = pre.AllFiles.Keys.Count(k => k >> 24 == 0x0E || k >> 24 == 0x13 || k >> 24 == 0x31);
            Assert.AreEqual(pre.AllFiles.Count - skipped + converter.ImageOfTexture.Count, records.Count);

            foreach (var record in records)
            {
                var again = (record.Key >> 24) switch
                {
                    0x01 => FileType.Unpack<GfxObj>(record.Value).Pack(),
                    0x05 => FileType.Unpack<SurfaceTexture>(record.Value).Pack(),
                    0x06 => FileType.Unpack<Texture>(record.Value).Pack(),
                    0x08 => FileType.Unpack<Surface>(record.Value).Pack(),
                    0x0D => FileType.Unpack<ACE.DatLoader.FileTypes.Environment>(record.Value).Pack(),
                    0x10 => FileType.Unpack<ClothingTable>(record.Value).Pack(),
                    _ => record.Value
                };

                CollectionAssert.AreEqual(record.Value, again, $"{record.Key:X8} does not read back as it was written");
            }

            foreach (var model in converter.DegradedModels)
                Assert.AreEqual(0x11000000 | (model & 0xFFFFFF), FileType.Unpack<GfxObj>(records[model]).DIDDegrade);
        }

        [TestMethod]
        public void StringTable_PackRoundTrips()
        {
            var table = MakeTable((1, "one"), (2, "two\nlines"));
            var again = FileType.Unpack<StringTable>(table.Pack());

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
            var table = FileType.Unpack<StringTable>(ReadRecord(DatBuildRecipes.LanguageBaseDat, LanguageRecords.ChargenTextId));

            DatStringEdits.Apply(table, edits);

            var edited = table.StringTableData.ToDictionary(e => e.Id);

            foreach (var edit in edits)
                Assert.AreEqual(edit.Text, edited[DatRecipeData.Hex(edit.Id)].Strings[edit.Index]);

            Assert.AreEqual("Random", edited[0x05D3F822].Strings[0]);
        }

        [TestMethod]
        public void OptionStrings_AddTheTextureDetailLabelsAsNewEntriesOfTheEndOfRetailTextStrings()
        {
            var table = FileType.Unpack<StringTable>(ReadRecord(DatBuildRecipes.LanguageBaseDat, LanguageRecords.TextStringsId));
            var existing = table.StringTableData.Select(e => e.Id).ToHashSet();
            var labels = DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMOptionStringsFile).Where(e => e.Name != null).ToList();

            Assert.AreEqual(4, labels.Count);
            CollectionAssert.AreEqual(new[] { "Full", "1/2", "1/4", "1/8" }, labels.Select(e => e.Text).ToList());

            foreach (var label in labels)
                Assert.IsFalse(existing.Contains(SpellTable.ComputeHash(label.Name)));

            DatStringEdits.Apply(table, DatRecipeData.Load<List<DatStringEdit>>(LanguageRecords.CustomDMOptionStringsFile));

            var reread = FileType.Unpack<StringTable>(table.Pack());
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
            var path = TestSourceDats.Dat(DatBuildRecipes.LanguageBaseDat);

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

        private static void AssertEveryRecordRoundTrips<T>(uint type, Func<T, byte[]> pack, Func<T, bool> skip = null, Func<uint, bool> select = null) where T : FileType, new()
        {
            var path = TestSourceDats.Dat(DatBuildRecipes.PortalBaseDat);

            var database = new DatDatabase(path);
            var ids = database.AllFiles.Keys.Where(id => (id >> 24) == type && (select == null || select(id))).ToList();
            var checkedCount = 0;

            Assert.IsTrue(ids.Count > 0);

            foreach (var id in ids)
            {
                var record = database.GetReaderForFile(id).Buffer;
                var value = FileType.Unpack<T>(record);

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

        private static void AssertEveryCellRecordRoundTrips<T>(Func<uint, bool> select) where T : FileType, new()
        {
            var database = new DatDatabase(TestSourceDats.Dat(TestSourceDats.CellDatName), true);
            var ids = database.AllFiles.Keys.Where(select).ToList();

            Assert.IsTrue(ids.Count > 0);

            foreach (var id in ids)
            {
                var record = database.GetReaderForFile(id).Buffer;

                CollectionAssert.AreEqual(record, FileType.Unpack<T>(record).Pack(), $"Record {id:X8} does not round-trip.");
            }
        }

        [TestMethod]
        public void LandblockPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryCellRecordRoundTrips<CellLandblock>(id => (id & 0xFFFF) == 0xFFFF);
        }

        [TestMethod]
        public void LandblockInfoPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryCellRecordRoundTrips<LandblockInfo>(id => (id & 0xFFFF) == 0xFFFE);
        }

        [TestMethod]
        public void EnvCellPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryCellRecordRoundTrips<EnvCell>(id => (id & 0xFFFF) >= 0x100 && (id & 0xFFFF) < 0xFFFE);
        }

        [TestMethod]
        public void FontPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<Font>(0x40, record => record.Pack());
        }

        [TestMethod]
        public void ChatPoseTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ChatPoseTable>(0x0E, record => record.Pack(), select: id => id == 0x0E000007);
        }

        [TestMethod]
        public void GeneratorTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<GeneratorTable>(0x0E, record => record.Pack(), select: id => id == 0x0E00000D);
        }

        [TestMethod]
        public void BadDataPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<BadData>(0x0E, record => record.Pack(), select: id => id == 0x0E00001A);
        }

        [TestMethod]
        public void ContractTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ContractTable>(0x0E, record => record.Pack(), select: id => id == 0x0E00001D);
        }

        [TestMethod]
        public void TabooTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<TabooTable>(0x0E, record => record.Pack(), select: id => id == 0x0E00001E);
        }

        [TestMethod]
        public void QualityFilterPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<QualityFilter>(0x0E, record => record.Pack(), select: id => (id >> 16) == 0x0E01);
        }

        [TestMethod]
        public void NameFilterTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<NameFilterTable>(0x0E, record => record.Pack(), select: id => id == 0x0E000020);
        }

        [TestMethod]
        public void DidMapperPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<DidMapper>(0x25, record => record.Pack());
        }

        [TestMethod]
        public void DualDidMapperPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<DualDidMapper>(0x27, record => record.Pack());
        }

        [TestMethod]
        public void CombatManeuverTablePack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<CombatManeuverTable>(0x30, record => record.Pack());
        }

        [TestMethod]
        public void ActionMapPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ActionMap>(0x26, record => record.Pack());
        }

        [TestMethod]
        public void MasterPropertyPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<MasterProperty>(0x39, record => record.Pack());
        }

        [TestMethod]
        public void DbPropertyPack_RoundTripsEveryEndOfRetailRecord()
        {
            DatManager.Initialize(TestSourceDats.ClientFolder(), true, false);

            AssertEveryRecordRoundTrips<DbProperty>(0x78, record => record.Pack());
        }

        [TestMethod]
        public void GfxObjPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<GfxObj>(0x01, record => record.Pack());
        }

        [TestMethod]
        public void EnvironmentPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<ACE.DatLoader.FileTypes.Environment>(0x0D, record => record.Pack());
        }

        [TestMethod]
        public void DegradeInfoPack_RoundTripsEveryEndOfRetailRecord()
        {
            AssertEveryRecordRoundTrips<GfxObjDegradeInfo>(0x11, record => record.Pack());
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
        public void RegionRemap_KeepsTheRecordWithTheIdentityAndShiftsTheSkyIds()
        {
            var record = ReadRecord(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID);
            var region = FileType.Unpack<RegionDesc>(record);
            var packed = region.Pack();

            region.Remap(id => id);
            CollectionAssert.AreEqual(packed, region.Pack());

            var first = region.SkyInfo.DayGroups[0].SkyObjects[0].DefaultGFXObjectId;
            region.Remap(id => id + 0x5000);

            var shifted = FileType.Unpack<RegionDesc>(region.Pack());
            Assert.AreEqual(first + 0x5000, shifted.SkyInfo.DayGroups[0].SkyObjects[0].DefaultGFXObjectId);
            Assert.AreEqual(0u, shifted.SkyInfo.DayGroups[0].SkyObjects[0].DefaultPESObjectId);
        }

        [TestMethod]
        public void InsertSkyObject_MovesTheReplacementsOfTheObjectsFromThereOn()
        {
            var group = FileType.Unpack<RegionDesc>(ReadRecord(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID)).SkyInfo.DayGroups[0];
            var before = group.SkyObjects.Select(o => o.DefaultGFXObjectId).ToList();
            var replaces = group.SkyTime.SelectMany(t => t.SkyObjReplace).Select(r => r.ObjectIndex).ToList();
            Assert.IsTrue(replaces.Any(i => i >= 4), "the test needs a replacement of an object after the insert position");

            group.InsertSkyObject(4, group.SkyObjects[3].CloneWith(0x01007086));

            Assert.AreEqual(before.Count + 1, group.SkyObjects.Count);
            Assert.AreEqual(0x01007086u, group.SkyObjects[4].DefaultGFXObjectId);
            Assert.AreEqual(before[3], group.SkyObjects[3].DefaultGFXObjectId);
            Assert.AreEqual(before[4], group.SkyObjects[5].DefaultGFXObjectId);
            CollectionAssert.AreEqual(replaces.Select(i => i >= 4 ? i + 1 : i).ToList(), group.SkyTime.SelectMany(t => t.SkyObjReplace).Select(r => r.ObjectIndex).ToList());
        }

        [TestMethod]
        public void CoverRecords_MakeAFullyTranslucentClipMapSurface()
        {
            var records = MoonRecords.CoverRecords(0x08007087, 0x05007087, 0x06009F00, 0x04007087).ToDictionary(r => r.Key, r => r.Value);
            CollectionAssert.AreEquivalent(new uint[] { 0x08007087, 0x05007087, 0x06009F00, 0x04007087 }, records.Keys.ToList());

            var surface = FileType.Unpack<Surface>(records[0x08007087]);
            Assert.IsTrue(surface.Type.HasFlag(SurfaceType.Base1ClipMap | SurfaceType.Translucent));
            Assert.AreEqual(0x05007087u, surface.OrigTextureId);
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
        public void LayoutEdits_MoveTheUnusedHeritageButtonsOutOfTheLayout_AndKeepTheThreeOfInfiltration()
        {
            var layout = DatLayoutEdits.Unpack(ReadRecord(DatBuildRecipes.LanguageBaseDat, ChargenHeritageUiId), ReadMasterProperty());
            var moves = DatRecipeData.Load<List<DatLayoutMoveEdit>>(LanguageRecords.InfiltrationHeritageUiMovedFile);
            var kept = layout.Elements[0x100003BD].Children.Where(c => c.Value.X == 21 && c.Value.Y < 150 && c.Key != 0x100003C4).Select(c => (c.Key, c.Value.X, c.Value.Y)).ToList();

            DatLayoutEdits.Move(layout, moves);

            var buttons = DatLayoutEdits.Unpack(layout.Pack(), ReadMasterProperty()).Elements[0x100003BD].Children;

            Assert.AreEqual(13 + 2, buttons.Count);
            Assert.AreEqual(moves.Count, buttons.Values.Count(b => b.X > 800));
            Assert.IsTrue(kept.All(k => buttons[k.Key].X == k.X && buttons[k.Key].Y == k.Y));
            Assert.ThrowsExactly<InvalidDataException>(() => DatLayoutEdits.Move(layout, new List<DatLayoutMoveEdit> { new DatLayoutMoveEdit { Element = "0000FFFF" } }));
        }

        [TestMethod]
        public void ChargenEdits_DiffOfAChangedRecord_AppliesBackToTheSameBytes()
        {
            var record = ReadRecord(DatBuildRecipes.PortalBaseDat, CharGen.FILE_ID);
            var changed = FileType.Unpack<CharGen>(record);
            DatRecipeData.Load<DatChargenEdits>(CustomDMRecords.ChargenEditsFile).Apply(changed);

            var edits = DatChargenEdits.Diff(FileType.Unpack<CharGen>(record), changed);
            var again = FileType.Unpack<CharGen>(record);
            edits.Apply(again);

            Assert.IsTrue(again.Pack().SequenceEqual(changed.Pack()));
            Assert.IsFalse(again.Pack().SequenceEqual(record));
        }

        [TestMethod]
        public void GroundTextures_ConvertTheOriginalImagesToTheMergeSize()
        {
            var textures = DatRecipeData.Load<List<GroundTextureRecords.GroundTexture>>(GroundTextureRecords.TexturesFile);

            Assert.AreEqual(38, textures.Count);

            foreach (var texture in textures.Take(3))
            {
                var source = DatRecipeData.Hex(texture.Source);
                var image = DatPortalImporter.ConvertImage(ReadRecord(DatBuildRecipes.PreToDPortalDat, source), DatRecipeData.Hex(texture.Image), id => ReadRecord(DatBuildRecipes.PreToDPortalDat, id), out var width, out var height, out _);

                Assert.IsTrue(width <= 128 && height <= 128);
                Assert.AreEqual(DatRecipeData.Hex(texture.Image), BitConverter.ToUInt32(image, 0));
                Assert.AreEqual(24 + width * height * (image[16] == 28 ? 1 : 3), image.Length);

                var terrain = GroundTextureRecords.ToTerrainFormat(image, 2);

                Assert.AreEqual(image[16] == 28 ? 244u : 21u, BitConverter.ToUInt32(terrain, 16));
                Assert.AreEqual(GroundTextureRecords.BaseTexSize, BitConverter.ToUInt32(terrain, 8));
                Assert.AreEqual(GroundTextureRecords.BaseTexSize, BitConverter.ToUInt32(terrain, 12));
                Assert.AreEqual(24 + 128 * 128 * (image[16] == 28 ? 1 : 4), terrain.Length);
            }
        }

        [TestMethod]
        public void ToTerrainFormat_TilesATerrainTextureAndRepeatsTheBlendMapPixelsUpToTheMergeSize()
        {
            byte[] Image(uint format, int bytesPerPixel)
            {
                var record = new List<byte>(new byte[24]);
                record.AddRange(Enumerable.Range(0, 64 * 64 * bytesPerPixel).Select(i => (byte)(i / bytesPerPixel % 251)));
                var bytes = record.ToArray();
                Array.Copy(BitConverter.GetBytes(64), 0, bytes, 8, 4);
                Array.Copy(BitConverter.GetBytes(64), 0, bytes, 12, 4);
                Array.Copy(BitConverter.GetBytes(format), 0, bytes, 16, 4);
                return bytes;
            }

            var terrain = GroundTextureRecords.ToTerrainFormat(Image(20, 3), 2);
            var blend = GroundTextureRecords.ToTerrainFormat(Image(28, 1), 2);

            Assert.AreEqual(24 + 128 * 128 * 4, terrain.Length);
            Assert.AreEqual(24 + 128 * 128, blend.Length);

            // Terrain: the pixel at (64 + 5, 64 + 7) is the original pixel (5, 7); blend map: the pixel at (2 * 5 + 1, 2 * 7 + 1) is the original pixel (5, 7).
            Assert.AreEqual(terrain[24 + (7 * 128 + 5) * 4], terrain[24 + ((64 + 7) * 128 + 64 + 5) * 4]);
            Assert.AreEqual(blend[24 + 7 * 2 * 128 + 5 * 2], blend[24 + (7 * 2 + 1) * 128 + 5 * 2 + 1]);
            Assert.AreEqual((byte)((7 * 64 + 5) % 251), blend[24 + (7 * 2 * 128 + 5 * 2)]);
        }

        [TestMethod]
        public void BlendSurfaceTextures_PointAtTheConvertedBlendMaps()
        {
            var textures = DatRecipeData.Load<List<GroundTextureRecords.GroundTexture>>(GroundTextureRecords.TexturesFile);
            var entries = DatRecipeData.Load<List<SurfaceTextureRecords.Entry>>(SurfaceTextureRecords.BlendFile);

            Assert.AreEqual(8, entries.Count);

            foreach (var entry in entries)
                Assert.IsTrue(textures.Any(texture => texture.Image == entry.Texture && texture.Source == entry.Id), $"{entry.Id} -> {entry.Texture} is not a converted ground texture");
        }

        [TestMethod]
        public void ConvertIcon_MakesAnA8R8G8B8Record_WithBlueGreenRedAlphaBytes()
        {
            var source = new List<byte>();
            source.AddRange(BitConverter.GetBytes(0x06001F99u));
            source.AddRange(BitConverter.GetBytes(2));
            source.AddRange(BitConverter.GetBytes(1));
            source.AddRange(new byte[] { 1, 2, 3, 10, 20, 30 });

            var icon = FileType.Unpack<Texture>(IconRecords.ConvertIcon(source.ToArray(), 0x06001F99));

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

            var script = FileType.Unpack<PhysicsScript>(SneakScriptRecords.FadeScript(SneakScriptRecords.EndScriptId, 0.5f, 0f));

            Assert.AreEqual(1, script.ScriptData.Count);
            Assert.AreEqual(0.5f, ((ACE.DatLoader.Entity.AnimationHooks.TransparentHook)script.ScriptData[0].Hook).Start);
        }

        [TestMethod]
        public void ScriptTablePack_RoundTripsTheEndOfRetailTable_AndTheSneakEntriesAreAdded()
        {
            var record = ReadRecord(DatBuildRecipes.PortalBaseDat, SneakScriptRecords.ScriptTableId);
            var table = FileType.Unpack<PhysicsScriptTable>(record);

            CollectionAssert.AreEqual(record, table.Pack());

            var count = table.ScriptTable.Count;
            var entry = new PhysicsScriptTableData();
            entry.Scripts.Add(ScriptAndModData.Create(1f, SneakScriptRecords.BeginScriptId));
            table.ScriptTable[SneakScriptRecords.BeginTableKey] = entry;

            var again = FileType.Unpack<PhysicsScriptTable>(table.Pack());

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
                var record = SurfaceTexture.Create(DatRecipeData.Hex(entry.Id), DatRecipeData.Hex(entry.Texture)).Pack();
                var surfaceTexture = FileType.Unpack<SurfaceTexture>(record);

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

            var script = FileType.Unpack<PhysicsScript>(records[0x3300272D]);
            Assert.IsTrue(script.ScriptData.Any(data => data.Hook is CreateParticleHook hook && hook.EmitterInfoId == 0x320013DC));
        }

        [TestMethod]
        public void FrozenFields_ModelsAreBuiltFromThe2001Dat_WithTheSceneDensityEdit()
        {
            var data = DatRecipeData.Load<ModelRecords.Data>(RegionRecords.ModelsFile);
            var records = ModelRecords.Build(data, id => ReadRecord(DatBuildRecipes.Portal2001Dat, id));

            Assert.AreEqual(32, records.Count);

            var scene = FileType.Unpack<Scene>(records.Single(record => record.Key == 0x1200725B).Value);

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
            var original = FileType.Unpack<RegionDesc>(ReadRecord(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID));
            var region = FileType.Unpack<RegionDesc>(ReadRecord(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID));

            DatRecipeData.Load<RegionRecords.RegionEdits>(RegionRecords.RegionEditsFile).Apply(region);

            var packed = region.Pack();

            Assert.AreEqual(original.Pack().Length + 4, packed.Length);
            CollectionAssert.AreEqual(new uint[] { 0x1200725B }, FileType.Unpack<RegionDesc>(packed).SceneInfo.SceneTypes[60].Scenes);
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
            var template = TestSourceDats.Dat(DatBuildRecipes.PortalBaseDat);

            var path = TestSourceDats.TempPath("RegionStepTests", ".dat");
            DatWriter.CreateEmpty(path, DatContainerSpec.Portal);

            var regionRecord = ReadRecord(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID);
            var region = FileType.Unpack<RegionDesc>(regionRecord);

            using (var writer = new DatWriter(path))
            {
                writer.Save(RegionDesc.FILE_ID, regionRecord);

                var written = new HashSet<uint>();

                foreach (var terrain in region.TerrainInfo.LandSurfaces.TexMerge.TerrainDesc)
                {
                    var surfaceTextureId = terrain.TerrainTex.TexGID;

                    if (!written.Add(surfaceTextureId))
                        continue;

                    var surfaceTextureRecord = ReadRecord(DatBuildRecipes.PortalBaseDat, surfaceTextureId);
                    var textureId = FileType.Unpack<SurfaceTexture>(surfaceTextureRecord).Textures[0];
                    var texture = FileType.Unpack<Texture>(ReadRecord(DatBuildRecipes.PortalBaseDat, textureId));
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

                    var merge = FileType.Unpack<RegionDesc>(writer.Read(RegionDesc.FILE_ID)).TerrainInfo.LandSurfaces.TexMerge;
                    Assert.AreEqual(GroundTextureRecords.BaseTexSize, merge.BaseTexSize);

                    foreach (var terrain in merge.TerrainDesc)
                    {
                        var textureId = FileType.Unpack<SurfaceTexture>(writer.Read(terrain.TerrainTex.TexGID)).Textures[0];
                        var width = (uint)FileType.Unpack<Texture>(writer.Read(textureId)).Width;
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

        [TestMethod]
        public void SeasonalKit_EverySeasonTextureConvertsFromItsOwnDatAndDiffersFromFall()
        {
            var kit = DatRecipeData.Load<SeasonalRecords.Kit>(SeasonalRecords.KitFile);
            var all = kit.All.Select(t => DatRecipeData.Hex(t)).ToHashSet();

            Assert.AreEqual(5, kit.Seasons.Count);
            CollectionAssert.AreEquivalent(new[] { "Spring", "Winter onset", "Winter", "Winter thaw", "Early Spring" }, kit.Seasons.Select(s => s.Name).ToList());
            CollectionAssert.AreEquivalent(all.ToList(), kit.Seasons.SelectMany(s => s.Textures).Select(t => DatRecipeData.Hex(t)).Distinct().ToList(), "the kit is the union of the seasons' textures");

            foreach (var season in kit.Seasons)
                foreach (var id in season.Textures.Select(t => DatRecipeData.Hex(t)))
                {
                    byte[] ToImage(string dat) => DatPortalImporter.ConvertImage(ReadRecord(dat, id), 0x06000001, palette => ReadRecord(dat, palette), out _, out _, out _);

                    CollectionAssert.AreNotEqual(ToImage(DatBuildRecipes.PreToDPortalDat), ToImage(season.Source), $"{season.Name} {id:X8} looks like fall");
                }
        }
    }
}
