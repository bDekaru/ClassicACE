using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatMapEditorTests
    {
        private static byte[] Landblock(uint id, bool hasObjects, byte terrainSeed, byte height)
        {
            var data = new byte[252];
            BitConverter.GetBytes(id).CopyTo(data, 0);
            BitConverter.GetBytes(hasObjects ? 1u : 0u).CopyTo(data, 4);

            for (var i = 0; i < 81; i++)
            {
                BitConverter.GetBytes((ushort)(terrainSeed + i)).CopyTo(data, 8 + i * 2);
                data[170 + i] = height;
            }

            return data;
        }

        private static byte[] Info(uint id, uint numCells, int objects, int buildings)
        {
            var info = new DatLandblockInfo { Id = id, NumCells = numCells };

            for (var i = 0; i < objects; i++)
                info.Objects.Add(new DatStab { Id = 0x01000000u + (uint)i });

            for (var i = 0; i < buildings; i++)
            {
                var building = new DatBuilding { ModelId = 0x01000100u + (uint)i, NumLeaves = 1 };
                building.Portals.Add(new DatBuildingPortal { Flags = 3, OtherCellId = 0xFFFF, OtherPortalId = 1, VisibleCells = { 0x100, 0x101, 0x102 } });
                info.Buildings.Add(building);
            }

            return info.Pack();
        }

        private static byte[] Cell(uint id)
        {
            var data = new byte[16];
            BitConverter.GetBytes(id).CopyTo(data, 0);
            BitConverter.GetBytes(id).CopyTo(data, 8);
            return data;
        }

        private static DatCellStore Store(params KeyValuePair<uint, byte[]>[] records)
        {
            var sorted = new SortedDictionary<uint, byte[]>();

            foreach (var record in records)
                sorted[record.Key] = record.Value;

            return new DatCellStore(sorted);
        }

        private static KeyValuePair<uint, byte[]> R(uint id, byte[] data) => new KeyValuePair<uint, byte[]>(id, data);

        [TestMethod]
        public void LandblockInfo_RoundTrips()
        {
            var data = Info(0x1234FFFE, 3, 4, 2);
            var info = DatLandblockInfo.Parse(data);

            Assert.AreEqual(4, info.Objects.Count);
            Assert.AreEqual(2, info.Buildings.Count);
            CollectionAssert.AreEqual(data, info.Pack());
        }

        [TestMethod]
        public void ReplaceLandblock_Relocates_RecordsAndCells()
        {
            var source = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 10, 20)),
                R(0xAAAAFFFE, Info(0xAAAAFFFE, 2, 1, 1)),
                R(0xAAAA0100, Cell(0xAAAA0100)),
                R(0xAAAA0101, Cell(0xAAAA0101)));

            var target = Store();
            new DatMapEditor(target).ReplaceLandblock(0xAAAA0000, source, newCellIdInLandblock: 0xBBBB0000);

            Assert.IsTrue(target.Contains(0xBBBBFFFF));
            Assert.IsTrue(target.Contains(0xBBBBFFFE));
            Assert.IsTrue(target.Contains(0xBBBB0100));
            Assert.IsTrue(target.Contains(0xBBBB0101));
            Assert.IsFalse(target.Contains(0xAAAA0100));

            target.TryGet(0xBBBBFFFF, out var landblock);
            Assert.AreEqual(0xBBBBFFFFu, BitConverter.ToUInt32(landblock, 0));

            target.TryGet(0xBBBB0101, out var cell);
            Assert.AreEqual(0xBBBB0101u, BitConverter.ToUInt32(cell, 0));
            Assert.AreEqual(0xBBBB0101u, BitConverter.ToUInt32(cell, 8), "the second copy of the cell id moves too");

            target.TryGet(0xBBBBFFFE, out var info);
            Assert.AreEqual(0xBBBBFFFEu, DatLandblockInfo.Parse(info).Id);
        }

        [TestMethod]
        public void ReplaceLandblock_TerrainOnly_KeepsObjectsAndCells()
        {
            var source = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 100, 9)),
                R(0xAAAAFFFE, Info(0xAAAAFFFE, 0, 0, 0)));

            var target = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 1, 2)),
                R(0xAAAAFFFE, Info(0xAAAAFFFE, 1, 3, 1)),
                R(0xAAAA0100, Cell(0xAAAA0100)));

            new DatMapEditor(target).ReplaceLandblock(0xAAAA0000, source, true, true, false, false);

            target.TryGet(0xAAAAFFFF, out var landblock);
            Assert.AreEqual(1u, BitConverter.ToUInt32(landblock, 4), "has-objects flag is kept when objects are not taken");
            Assert.AreEqual(100, BitConverter.ToUInt16(landblock, 8));
            Assert.AreEqual(9, landblock[170]);

            target.TryGet(0xAAAAFFFE, out var info);
            var parsed = DatLandblockInfo.Parse(info);
            Assert.AreEqual(3, parsed.Objects.Count);
            Assert.AreEqual(1, parsed.Buildings.Count);
            Assert.IsTrue(target.Contains(0xAAAA0100));
        }

        [TestMethod]
        public void ReplaceLandblock_SourceWithoutInfo_ClearsDestinationBuildingsAndCells()
        {
            var source = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 5, 5)));

            var target = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 1, 2)),
                R(0xAAAAFFFE, Info(0xAAAAFFFE, 1, 3, 1)),
                R(0xAAAA0100, Cell(0xAAAA0100)));

            new DatMapEditor(target).ReplaceLandblock(0xAAAA0000, source, true, true, false, true);

            var parsed = DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE));
            Assert.AreEqual(3, parsed.Objects.Count);
            Assert.AreEqual(0, parsed.Buildings.Count);
            Assert.AreEqual(0u, parsed.NumCells);
            Assert.IsFalse(target.Contains(0xAAAA0100));
        }

        [TestMethod]
        public void ReplaceLandblock_SourceWithoutLandblock_RemovesDestination()
        {
            var target = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 1, 2)));

            new DatMapEditor(target).ReplaceLandblock(0xAAAA0000, Store());

            Assert.IsFalse(target.Contains(0xAAAAFFFF));
        }

        [TestMethod]
        public void ReplaceLandblock_CreateOnly_LeavesExisting()
        {
            var source = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 100, 9)));
            var target = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 1, 2)));

            new DatMapEditor(target).ReplaceLandblock(0xAAAA0000, source, createOnly: true);

            Assert.AreEqual(1, BitConverter.ToUInt16(Read(target, 0xAAAAFFFF), 8));
        }

        [TestMethod]
        public void RectLandblocks_WalkXThenYAndOffset()
        {
            var pairs = DatMapEditor.RectLandblocks(0x5C80, 0x5D7F, 0x6E53).ToArray();

            Assert.AreEqual(4, pairs.Length);
            Assert.AreEqual(0x5C7Fu << 16, pairs[0].Key);
            Assert.AreEqual(0x6E52u << 16, pairs[0].Value);
            Assert.AreEqual(0x5D80u << 16, pairs[3].Key);
            Assert.AreEqual(0x6F53u << 16, pairs[3].Value);
        }

        [TestMethod]
        public void ReplaceRects_OverlappingRectsReplaceEachLandblockOnce_LaterRectangleWins()
        {
            var source = Store(
                R(0x1010FFFF, Landblock(0x1010FFFF, false, 1, 1)),
                R(0x1110FFFF, Landblock(0x1110FFFF, false, 2, 2)),
                R(0x1210FFFF, Landblock(0x1210FFFF, false, 3, 3)));

            var target = Store();
            var count = new DatMapEditor(target).ReplaceRects(source, new[] { ((ushort)0x1010, (ushort)0x1110, (ushort)0), ((ushort)0x1110, (ushort)0x1210, (ushort)0) });

            Assert.AreEqual(3, count);
            Assert.AreEqual(3, target.Count);

            var conflict = Store();
            var editor = new DatMapEditor(conflict);
            editor.ReplaceRects(source, new[] { ((ushort)0x1010, (ushort)0x1010, (ushort)0x2020), ((ushort)0x1110, (ushort)0x1110, (ushort)0x2020) });

            Assert.AreEqual(1, editor.Notes.Count, "the overlap is reported");
            Assert.AreEqual((ushort)2, BitConverter.ToUInt16(Read(conflict, 0x2020FFFF), 8), "the later rectangle's landblock wins");
        }

        [TestMethod]
        public void RemoveNoobFence_UsesNeighbours()
        {
            var landblock = Landblock(0xAAAAFFFF, false, 0, 0);
            BitConverter.GetBytes((ushort)0x0024).CopyTo(landblock, 8);
            BitConverter.GetBytes((ushort)0xE818).CopyTo(landblock, 10);
            BitConverter.GetBytes((ushort)0xE818).CopyTo(landblock, 12);

            var target = Store(R(0xAAAAFFFF, landblock));
            new DatMapEditor(target).RemoveNoobFence();

            var result = Read(target, 0xAAAAFFFF);
            Assert.AreEqual(0x0024, BitConverter.ToUInt16(result, 10));
            Assert.AreEqual(0x0024, BitConverter.ToUInt16(result, 12));
        }

        [TestMethod]
        public void RemoveObjects_OnlyListedObjectsInThatOutdoorCell()
        {
            var info = new DatLandblockInfo { Id = 0xAAAAFFFE };
            info.Objects.Add(StabAt(0x02000001, 5f, 5f));    // cell 1
            info.Objects.Add(StabAt(0x02000002, 5f, 5f));    // cell 1, not listed
            info.Objects.Add(StabAt(0x02000001, 100f, 5f));  // cell 5 (100/24 = 4 -> 4*8+0+1 = 33)

            var target = Store(R(0xAAAAFFFE, info.Pack()));
            new DatMapEditor(target).RemoveObjects(0xAAAA0001, new uint[] { 0x02000001 });

            var parsed = DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE));
            Assert.AreEqual(2, parsed.Objects.Count);
            Assert.AreEqual(0x02000002u, parsed.Objects[0].Id);
            Assert.AreEqual(100f, BitConverter.ToSingle(parsed.Objects[1].Frame, 0));
        }

        [TestMethod]
        public void RemoveBuilding_FindsItByVisibleCell()
        {
            var target = Store(R(0xAAAAFFFE, Info(0xAAAAFFFE, 3, 0, 2)));

            Assert.IsTrue(new DatMapEditor(target).RemoveBuilding(0xAAAA0101));
            Assert.AreEqual(1, DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE)).Buildings.Count, "one building per call");
            Assert.IsTrue(new DatMapEditor(target).RemoveBuilding(0xAAAA0101));
            Assert.IsFalse(new DatMapEditor(target).RemoveBuilding(0xAAAA0101));
        }

        private static byte[] RealCell(uint id, ushort[] visible, ushort[] portalDestinations)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(id);
                w.Write(0u);
                w.Write(id);
                w.Write((byte)0);
                w.Write((byte)portalDestinations.Length);
                w.Write((ushort)visible.Length);
                w.Write((ushort)0x0101);
                w.Write((ushort)0);
                w.Write(new byte[28]);

                foreach (var destination in portalDestinations)
                {
                    w.Write((ushort)0);
                    w.Write((ushort)0);
                    w.Write(destination);
                    w.Write((ushort)0);
                }

                foreach (var cell in visible)
                    w.Write(cell);

                return ms.ToArray();
            }
        }

        private static byte[] BuildingInfo(uint id, uint numCells, uint model, ushort otherCell, params ushort[] visible)
        {
            var info = new DatLandblockInfo { Id = id, NumCells = numCells };
            var building = new DatBuilding { ModelId = model, NumLeaves = 1 };
            building.Portals.Add(new DatBuildingPortal { Flags = 3, OtherCellId = otherCell, OtherPortalId = 1, VisibleCells = visible.ToList() });
            info.Buildings.Add(building);
            return info.Pack();
        }

        private static ushort[] VisibleOf(byte[] cell)
        {
            var count = BitConverter.ToUInt16(cell, 14);
            var at = 16 + 4 + 28 + cell[13] * 8;
            return Enumerable.Range(0, count).Select(i => BitConverter.ToUInt16(cell, at + i * 2)).ToArray();
        }

        private static ushort PortalDestinationOf(byte[] cell, int index) => BitConverter.ToUInt16(cell, 16 + 4 + 28 + index * 8 + 4);

        [TestMethod]
        public void AddBuilding_CopiesCellsIntoFreeIds_MovesPortalsAndCountsCells()
        {
            var source = Store(
                R(0xAAAAFFFE, BuildingInfo(0xAAAAFFFE, 2, 0x01000500, 0x100, 0x100, 0x101)),
                R(0xAAAA0100, RealCell(0xAAAA0100, new ushort[] { 0x101 }, new ushort[] { 0x101 })),
                R(0xAAAA0101, RealCell(0xAAAA0101, new ushort[] { 0x100 }, new ushort[] { 0x100, 0xFFFF })));
            var target = Store(
                R(0xAAAAFFFE, Info(0xAAAAFFFE, 2, 0, 0)),
                R(0xAAAA0100, Cell(0xAAAA0100)),
                R(0xAAAA0101, Cell(0xAAAA0101)));

            Assert.IsTrue(new DatMapEditor(target).AddBuilding(0xAAAA0101, source));

            var first = Read(target, 0xAAAA0102);
            var second = Read(target, 0xAAAA0103);
            Assert.AreEqual(0xAAAA0102u, BitConverter.ToUInt32(first, 0));
            Assert.AreEqual(0xAAAA0102u, BitConverter.ToUInt32(first, 8));
            CollectionAssert.AreEqual(new ushort[] { 0x103 }, VisibleOf(first));
            Assert.AreEqual((ushort)0x103, PortalDestinationOf(first, 0));
            CollectionAssert.AreEqual(new ushort[] { 0x102 }, VisibleOf(second));
            Assert.AreEqual((ushort)0x102, PortalDestinationOf(second, 0));
            Assert.AreEqual((ushort)0xFFFF, PortalDestinationOf(second, 1), "an outside portal stays outside");

            var info = DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE));
            Assert.AreEqual(4u, info.NumCells);
            Assert.AreEqual((ushort)0x102, info.Buildings.Single().Portals[0].OtherCellId);
            CollectionAssert.AreEqual(new ushort[] { 0x102, 0x103 }, info.Buildings.Single().Portals[0].VisibleCells);
            Assert.IsFalse(new DatMapEditor(target).AddBuilding(0xAAAA0199, source));
        }

        [TestMethod]
        public void RemoveBuilding_WithCells_RemovesEveryReachableCell()
        {
            var target = Store(
                R(0xAAAAFFFE, BuildingInfo(0xAAAAFFFE, 3, 0x01000500, 0x100, 0x100, 0x101)),
                R(0xAAAA0100, RealCell(0xAAAA0100, new ushort[] { 0x101 }, new ushort[0])),
                R(0xAAAA0101, RealCell(0xAAAA0101, new ushort[] { 0x100 }, new ushort[0])),
                R(0xAAAA0102, RealCell(0xAAAA0102, new ushort[0], new ushort[0])));

            Assert.IsTrue(new DatMapEditor(target).RemoveBuilding(0xAAAA0100, false, null, true));
            Assert.IsFalse(target.Contains(0xAAAA0100));
            Assert.IsFalse(target.Contains(0xAAAA0101));
            Assert.IsTrue(target.Contains(0xAAAA0102));
            Assert.AreEqual(3u, DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE)).NumCells, "NumCells stays as Melt leaves it");
        }

        [TestMethod]
        public void ReplaceTerrain_CopiesChosenPartsAndNeverObjects()
        {
            var source = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 100, 9)), R(0xBBBBFFFF, Landblock(0xBBBBFFFF, true, 100, 9)));
            var target = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 10, 1)), R(0xCCCCFFFF, Landblock(0xCCCCFFFF, false, 10, 1)));
            var editor = new DatMapEditor(target);

            editor.ReplaceTerrain(0xAAAA0000, source, true, false);
            var kept = Read(target, 0xAAAAFFFF);
            Assert.AreEqual((ushort)10, BitConverter.ToUInt16(kept, 8), "terrain words stay");
            Assert.AreEqual((byte)9, kept[170], "height comes over");
            Assert.AreEqual(0u, BitConverter.ToUInt32(kept, 4), "has-objects flag stays");

            editor.ReplaceTerrain(0xBBBB0000, source);
            var added = Read(target, 0xBBBBFFFF);
            Assert.AreEqual((ushort)100, BitConverter.ToUInt16(added, 8));
            Assert.AreEqual(0u, BitConverter.ToUInt32(added, 4), "a new landblock starts without objects");

            editor.ReplaceTerrain(0xCCCC0000, source, true, true, true);
            Assert.IsTrue(target.Contains(0xCCCCFFFF), "create-only never removes");
            editor.ReplaceTerrain(0xCCCC0000, source);
            Assert.IsFalse(target.Contains(0xCCCCFFFF), "a landblock the source lacks is removed");
        }

        [TestMethod]
        public void ReplaceTexture_ChangesOnlyMatchingWords_InExistingLandblocksOnly()
        {
            var target = Store(R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 10, 1)));
            var editor = new DatMapEditor(target);

            editor.ReplaceTexture(0xAAAA0000, 12, 0x5000);
            editor.ReplaceTexture(0xBBBB0000, 12, 0x5000);

            var data = Read(target, 0xAAAAFFFF);
            Assert.AreEqual((ushort)0x5000, BitConverter.ToUInt16(data, 8 + 2 * 2));
            Assert.AreEqual((ushort)11, BitConverter.ToUInt16(data, 8 + 1 * 2));
            Assert.IsFalse(target.Contains(0xBBBBFFFF));
            CollectionAssert.AreEqual(new uint[] { 0xAAAA0000 }, editor.LandblocksInRect(0xAAAA, 0xAAAA).ToArray());
        }

        [TestMethod]
        public void SettlementList_ParsesThreeKinds()
        {
            var list = DatMapEditor.SettlementList.Parse(new StringReader("02000000\tPreserveSurface\r\n3FAC0005\r\n\r\n030f0000\tPreserveTextures\r\n"));

            CollectionAssert.AreEqual(new uint[] { 0x3FAC0005 }, list.Regular);
            CollectionAssert.AreEqual(new uint[] { 0x030F0000 }, list.PreserveTextures);
            CollectionAssert.AreEqual(new uint[] { 0x02000000 }, list.PreserveSurface);

            var embedded = DatMapEditor.SettlementList.LoadEmbedded("settlement_landblocks.tsv");
            Assert.AreEqual(1319, embedded.Regular.Count + embedded.PreserveTextures.Count + embedded.PreserveSurface.Count);
            Assert.AreEqual(25, embedded.PreserveTextures.Count);
            Assert.AreEqual(114, embedded.PreserveSurface.Count);
        }

        [TestMethod]
        public void ReplaceStarterOutpost_SwapsSpireKeepsPortalStands()
        {
            var ruined = DatLandblockInfo.Parse(BuildingInfo(0xAAAAFFFE, 2, 0x01002866, 0x100, 0x100, 0x101));
            ruined.Objects.Add(StabAt(0x020007C5, 1, 1));
            ruined.Objects.Add(StabAt(0x01000001, 2, 2));

            var pristine = DatLandblockInfo.Parse(BuildingInfo(0xAAAAFFFE, 3, 0x010014C3, 0x100, 0x100, 0x101, 0x102));
            pristine.Objects.Add(StabAt(0x01000009, 3, 3));

            var source = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, true, 100, 9)),
                R(0xAAAAFFFE, pristine.Pack()),
                R(0xAAAA0100, RealCell(0xAAAA0100, new ushort[] { 0x101, 0x102 }, new ushort[0])),
                R(0xAAAA0101, RealCell(0xAAAA0101, new ushort[] { 0x100 }, new ushort[0])),
                R(0xAAAA0102, RealCell(0xAAAA0102, new ushort[] { 0x100 }, new ushort[0])));
            var target = Store(
                R(0xAAAAFFFF, Landblock(0xAAAAFFFF, false, 10, 1)),
                R(0xAAAAFFFE, ruined.Pack()),
                R(0xAAAA0100, RealCell(0xAAAA0100, new ushort[] { 0x101 }, new ushort[0])),
                R(0xAAAA0101, RealCell(0xAAAA0101, new ushort[] { 0x100 }, new ushort[0])),
                R(0xAAAA0104, Cell(0xAAAA0104)));

            new DatMapEditor(target).ReplaceStarterOutpost(0xAAAA0010, source);

            var landblock = Read(target, 0xAAAAFFFF);
            Assert.AreEqual((ushort)100, BitConverter.ToUInt16(landblock, 8), "terrain words come from the source");
            Assert.AreEqual((byte)1, landblock[170], "height stays");
            Assert.AreEqual(1u, BitConverter.ToUInt32(landblock, 4));

            var info = DatLandblockInfo.Parse(Read(target, 0xAAAAFFFE));
            CollectionAssert.AreEqual(new uint[] { 0x020007C5, 0x01000009 }, info.Objects.Select(o => o.Id).ToArray());
            Assert.AreEqual(0x010014C3u, info.Buildings.Single().ModelId);
            Assert.AreEqual(2u, info.NumCells, "NumCells stays as Melt leaves it");
            Assert.AreEqual((ushort)0x100, info.Buildings.Single().Portals[0].OtherCellId, "the ruined spire's cells were free again");
            Assert.IsTrue(target.Contains(0xAAAA0100) && target.Contains(0xAAAA0101) && target.Contains(0xAAAA0102));
            CollectionAssert.AreEqual(new ushort[] { 0x101, 0x102 }, VisibleOf(Read(target, 0xAAAA0100)));
        }

        private static DatStab StabAt(uint id, float x, float y)
        {
            var stab = new DatStab { Id = id };
            BitConverter.GetBytes(x).CopyTo(stab.Frame, 0);
            BitConverter.GetBytes(y).CopyTo(stab.Frame, 4);
            return stab;
        }

        private static byte[] Read(DatCellStore store, uint id)
        {
            Assert.IsTrue(store.TryGet(id, out var data));
            return data;
        }

        [TestMethod]
        public void LandblockInfo_RoundTripsEveryRecordOfARealDat()
        {
            var path = Environment.GetEnvironmentVariable("ACE_TEST_CELL_DAT");

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                Assert.Inconclusive("Set ACE_TEST_CELL_DAT to a ToD-layout cell dat.");

            var db = new DatDatabase(path, true);
            var checkedCount = 0;

            foreach (var id in db.AllFiles.Keys.Where(k => (k & 0xFFFF) == 0xFFFE))
            {
                var data = db.GetReaderForFile(id).Buffer;
                var packed = DatLandblockInfo.Parse(data).Pack();

                if (!data.SequenceEqual(packed))
                    Assert.Fail($"{id:X8} did not round-trip ({data.Length} -> {packed.Length} bytes)");

                checkedCount++;
            }

            Assert.IsTrue(checkedCount > 1000);
        }
    }
}
