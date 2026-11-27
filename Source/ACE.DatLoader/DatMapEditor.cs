using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader
{
    /// <summary>
    /// The records of a ToD-layout cell dat held in memory, in id order, with an index of the cells of each landblock.
    /// Map edits change a store and the finished store is written to a dat once.
    /// </summary>
    public class DatCellStore
    {
        private readonly SortedDictionary<uint, byte[]> _records;
        private readonly Dictionary<uint, SortedSet<uint>> _cellsByLandblock = new Dictionary<uint, SortedSet<uint>>();

        public DatCellStore(SortedDictionary<uint, byte[]> records)
        {
            _records = records;

            foreach (var id in records.Keys.Where(IsCell))
                IndexCell(id);
        }

        public IEnumerable<KeyValuePair<uint, byte[]>> Records => _records;

        public int Count => _records.Count;

        public static bool IsCell(uint id) => (id & 0xFFFF) < 0xFFFE;

        public static uint LandblockOf(uint id) => id & 0xFFFF0000;

        public bool TryGet(uint id, out byte[] data) => _records.TryGetValue(id, out data);

        public bool Contains(uint id) => _records.ContainsKey(id);

        private readonly Dictionary<uint, uint> _dates = new Dictionary<uint, uint>();

        /// <summary>The step date that records set from now on carry. 0 (the default) gives them no date of their own.</summary>
        public uint CurrentDate { get; set; }

        /// <summary>When set, changing a record before a step was started (see <see cref="DatMapEditor.Step"/>) throws, so an edit cannot be added without a date.</summary>
        public bool RequireDate { get; set; }

        /// <summary>The date of the step that last set the record, or 0 when no step has.</summary>
        public uint DateOf(uint id) => _dates.TryGetValue(id, out var date) ? date : 0;

        /// <summary>Raised when a record's content changes (the new data) or a record is removed (null).</summary>
        public event Action<uint, byte[]> Changed;

        public void Set(uint id, byte[] data)
        {
            var changed = !_records.TryGetValue(id, out var old) || !old.SequenceEqual(data);

            if (changed && RequireDate && CurrentDate == 0)
                throw new InvalidOperationException($"Record {id:X8} was edited before a step was started: call map.Step(\"yyyy-MM-dd\") first.");

            _records[id] = data;

            if (changed && CurrentDate != 0)
                _dates[id] = CurrentDate;

            if (IsCell(id))
                IndexCell(id);

            if (changed)
                Changed?.Invoke(id, data);
        }

        public void Remove(uint id)
        {
            var removed = _records.Remove(id);
            _dates.Remove(id);

            if (IsCell(id) && _cellsByLandblock.TryGetValue(LandblockOf(id), out var cells))
                cells.Remove(id);

            if (removed)
                Changed?.Invoke(id, null);
        }

        /// <summary>Ids of the environment cells of one landblock (id prefix in the high 16 bits), ascending.</summary>
        public IReadOnlyList<uint> CellsOf(uint landblockPrefix)
        {
            return _cellsByLandblock.TryGetValue(landblockPrefix & 0xFFFF0000, out var cells) ? cells.ToList() : new List<uint>();
        }

        private void IndexCell(uint id)
        {
            var prefix = LandblockOf(id);

            if (!_cellsByLandblock.TryGetValue(prefix, out var cells))
                _cellsByLandblock[prefix] = cells = new SortedSet<uint>();

            cells.Add(id);
        }
    }

    /// <summary>
    /// Landblock-level edits on a <see cref="DatCellStore"/>, following the semantics of Melt's cDatFile.replaceLandblock and the operations built on it.
    /// Source stores must already be in ToD layout (convert retail cell dats first).
    /// </summary>
    public class DatMapEditor
    {
        private const int LandblockHasObjectsOffset = 4;
        private const int LandblockTerrainOffset = 8;
        private const int LandblockTerrainBytes = 162;
        private const int LandblockHeightOffset = LandblockTerrainOffset + LandblockTerrainBytes;
        private const int LandblockHeightBytes = 81;

        private readonly DatCellStore _store;
        private readonly Dictionary<uint, byte[]> _emptyCells = new Dictionary<uint, byte[]>();

        public DatMapEditor(DatCellStore store)
        {
            _store = store;
        }

        public DatCellStore Store => _store;

        /// <summary>
        /// Starts a step: every record the edits after this call change carries <paramref name="date"/> ("yyyy-MM-dd", see <see cref="DatStepDate"/>). The date is chosen by hand when the edits are added and never changed afterwards.
        /// </summary>
        /// <exception cref="ArgumentException">The date is earlier than the previous step's.</exception>
        public void Step(string date)
        {
            var time = DatStepDate.Parse(date);

            if (time < _store.CurrentDate)
                throw new ArgumentException($"Step {date} is earlier than the step before it; map steps run in date order.");

            _store.CurrentDate = time;
        }

        /// <summary>Things the edits resolved on their own that a reader of the build report should know about.</summary>
        public List<string> Notes { get; } = new List<string>();

        public static uint LandblockId(uint idInLandblock) => (idInLandblock & 0xFFFF0000) | 0xFFFF;

        public static uint LandblockInfoId(uint idInLandblock) => (idInLandblock & 0xFFFF0000) | 0xFFFE;

        /// <summary>
        /// Copies parts of a landblock from another store, optionally to another landblock id. A part the source lacks is removed from the destination.
        /// </summary>
        /// <param name="heightmap">Take the 9x9 height grid.</param>
        /// <param name="textures">Take the 9x9 terrain words (type, road, scenery).</param>
        /// <param name="objects">Take the landblock's static objects and its has-objects flag.</param>
        /// <param name="cells">Take the buildings and every environment cell.</param>
        /// <param name="createOnly">Only add what the destination lacks.</param>
        /// <param name="newCellIdInLandblock">Destination landblock when it differs from the source's (any id inside it).</param>
        public void ReplaceLandblock(uint cellIdInLandblock, DatCellStore from, bool heightmap = true, bool textures = true, bool objects = true, bool cells = true, bool createOnly = false, uint newCellIdInLandblock = 0)
        {
            var landblockId = LandblockId(cellIdInLandblock);
            var newLandblockId = newCellIdInLandblock != 0 ? LandblockId(newCellIdInLandblock) : landblockId;

            ReplaceLandblockRecord(landblockId, newLandblockId, from, heightmap, textures, objects, createOnly);
            ReplaceLandblockInfo(landblockId, newLandblockId, from, objects, cells, createOnly);
        }

        private void ReplaceLandblockRecord(uint landblockId, uint newLandblockId, DatCellStore from, bool heightmap, bool textures, bool objects, bool createOnly)
        {
            var existsOnDestination = _store.TryGet(newLandblockId, out var to);

            if (from.TryGet(landblockId, out var source))
            {
                if (existsOnDestination && createOnly)
                    return;

                if (existsOnDestination)
                {
                    to = (byte[])to.Clone();

                    if (objects)
                        Array.Copy(source, LandblockHasObjectsOffset, to, LandblockHasObjectsOffset, 4);
                    if (heightmap)
                        Array.Copy(source, LandblockHeightOffset, to, LandblockHeightOffset, LandblockHeightBytes);
                    if (textures)
                        Array.Copy(source, LandblockTerrainOffset, to, LandblockTerrainOffset, LandblockTerrainBytes);
                }
                else
                {
                    to = (byte[])source.Clone();
                    SetId(to, newLandblockId);
                }

                _store.Set(newLandblockId, to);
            }
            else if (existsOnDestination && !createOnly)
                _store.Remove(newLandblockId);
        }

        private void ReplaceLandblockInfo(uint landblockId, uint newLandblockId, DatCellStore from, bool objects, bool cells, bool createOnly)
        {
            var infoId = LandblockInfoId(landblockId);
            var newInfoId = LandblockInfoId(newLandblockId);
            var existsOnDestination = _store.TryGet(newInfoId, out var toData);

            if (from.TryGet(infoId, out var sourceData))
            {
                if (existsOnDestination && createOnly)
                    return;

                var source = FileTypes.LandblockInfo.Parse(sourceData);
                FileTypes.LandblockInfo to;

                if (existsOnDestination)
                {
                    to = FileTypes.LandblockInfo.Parse(toData);

                    if (cells)
                    {
                        foreach (var id in _store.CellsOf(newLandblockId))
                            _store.Remove(id);

                        to.NumCells = 0;
                    }
                }
                else
                {
                    to = FileTypes.LandblockInfo.Parse(sourceData);

                    if (!cells)
                        to.NumCells = 0;
                }

                if (cells)
                {
                    var copied = CopyCells(landblockId, newLandblockId, from);

                    if (copied > 0 && from.TryGet(landblockId, out var sourceLandblock) && _store.TryGet(newLandblockId, out var destinationLandblock))
                    {
                        destinationLandblock = (byte[])destinationLandblock.Clone();
                        Array.Copy(sourceLandblock, LandblockHasObjectsOffset, destinationLandblock, LandblockHasObjectsOffset, 4);
                        _store.Set(newLandblockId, destinationLandblock);
                    }

                    to.NumCells = source.NumCells;
                    to.PackMask = source.PackMask;
                    to.Buildings = source.Buildings;
                }

                to.Id = newInfoId;
                to.RestrictionTables = source.RestrictionTables;
                to.RestrictionTablesBucketSize = source.RestrictionTablesBucketSize;

                if (objects)
                    to.Objects = source.Objects;

                _store.Set(newInfoId, to.Pack());
            }
            else if (existsOnDestination && !createOnly)
            {
                if (objects && cells)
                {
                    foreach (var id in _store.CellsOf(newLandblockId))
                        _store.Remove(id);

                    _store.Remove(newInfoId);
                    return;
                }

                var to = FileTypes.LandblockInfo.Parse(toData);

                if (!objects)
                {
                    to.NumCells = 0;
                    to.PackMask = 0;
                    to.Buildings = to.Buildings.Where(b => b.Portals.Count == 0).ToList();
                    to.RestrictionTables.Clear();
                    _store.Set(newInfoId, to.Pack());

                    foreach (var id in _store.CellsOf(newLandblockId))
                        _store.Remove(id);
                }
                else if (!cells)
                {
                    to.Objects.Clear();
                    _store.Set(newInfoId, to.Pack());
                }
            }
        }

        /// <summary>
        /// Landblocks of a rectangle as Melt's replaceLandblockRect walks them: x from the left to the right edge, y from the bottom-right corner's y up to the top-left corner's y
        /// (so the top-left corner has the larger y). Each pair is (source landblock, destination landblock), as the high 16 bits of a cell id.
        /// </summary>
        public static IEnumerable<KeyValuePair<uint, uint>> RectLandblocks(ushort topLeft, ushort bottomRight, ushort newTopLeft = 0)
        {
            var left = topLeft >> 8;
            var top = topLeft & 0xFF;
            var right = bottomRight >> 8;
            var bottom = bottomRight & 0xFF;

            var offsetX = newTopLeft == 0 ? 0 : (newTopLeft >> 8) - left;
            var offsetY = newTopLeft == 0 ? 0 : (newTopLeft & 0xFF) - top;

            for (var x = left; x <= right; x++)
                for (var y = bottom; y <= top; y++)
                    yield return new KeyValuePair<uint, uint>((uint)(x << 24 | y << 16), (uint)(((x + offsetX) & 0xFF) << 24 | ((y + offsetY) & 0xFF) << 16));
        }

        /// <summary>
        /// Replaces every landblock of the given rectangles from another store, as the same rectangles replaced one after another would: a landblock covered by several rectangles is replaced once,
        /// and where two rectangles map different sources to one destination the later rectangle wins and a note is added to <see cref="Notes"/>.
        /// </summary>
        public int ReplaceRects(DatCellStore from, IEnumerable<(ushort TopLeft, ushort BottomRight, ushort NewTopLeft)> rects, bool heightmap = true, bool textures = true, bool objects = true, bool cells = true)
        {
            var map = new SortedDictionary<uint, uint>();

            foreach (var rect in rects)
            {
                foreach (var pair in RectLandblocks(rect.TopLeft, rect.BottomRight, rect.NewTopLeft))
                {
                    if (map.TryGetValue(pair.Value, out var existingSource) && existingSource != pair.Key)
                        Notes.Add($"Landblock {pair.Value >> 16:X4} is the destination of both {existingSource >> 16:X4} and {pair.Key >> 16:X4}; the later rectangle's {pair.Key >> 16:X4} is used.");

                    map[pair.Value] = pair.Key;
                }
            }

            foreach (var entry in map)
                ReplaceLandblock(entry.Value, from, heightmap, textures, objects, cells, false, entry.Key);

            return map.Count;
        }

        /// <summary>Terrain words of one landblock rewritten in place by <paramref name="change"/>; returns false when the landblock does not exist.</summary>
        private bool ChangeTerrain(uint landblockId, Func<ushort[], bool> change)
        {
            if (!_store.TryGet(LandblockId(landblockId), out var data))
                return false;

            var terrain = new ushort[81];
            for (var i = 0; i < 81; i++)
                terrain[i] = BitConverter.ToUInt16(data, LandblockTerrainOffset + i * 2);

            if (change(terrain))
            {
                data = (byte[])data.Clone();

                for (var i = 0; i < 81; i++)
                    Array.Copy(BitConverter.GetBytes(terrain[i]), 0, data, LandblockTerrainOffset + i * 2, 2);

                _store.Set(LandblockId(landblockId), data);
            }

            return true;
        }

        private const ushort NoobFenceTerrain = 0xE818;
        private const ushort NoobPillarTerrain = 0xE834;
        private const ushort PlainTerrain = 0x0034;

        /// <summary>Replaces every noob fence terrain word with its left neighbour, else its right neighbour, else plain terrain.</summary>
        public void RemoveNoobFence()
        {
            foreach (var id in _store.Records.Select(r => r.Key).Where(k => (k & 0xFFFF) == 0xFFFF).ToList())
            {
                ChangeTerrain(id, terrain =>
                {
                    var changed = false;

                    for (var i = 0; i < terrain.Length; i++)
                    {
                        if (terrain[i] != NoobFenceTerrain)
                            continue;

                        if (i > 0 && terrain[i - 1] != NoobFenceTerrain)
                            terrain[i] = terrain[i - 1];
                        else if (i < 80 && terrain[i + 1] != NoobFenceTerrain)
                            terrain[i] = terrain[i + 1];
                        else
                            terrain[i] = PlainTerrain;

                        changed = true;
                    }

                    return changed;
                });
            }
        }

        public void RemoveNoobPillars(IEnumerable<uint> landblocks)
        {
            foreach (var id in landblocks)
                ChangeTerrain(id, terrain =>
                {
                    var changed = false;

                    for (var i = 0; i < terrain.Length; i++)
                        if (terrain[i] == NoobPillarTerrain)
                        {
                            terrain[i] = PlainTerrain;
                            changed = true;
                        }

                    return changed;
                });
        }

        public void RemoveRoads(IEnumerable<uint> landblocks)
        {
            foreach (var id in landblocks)
                ChangeTerrain(id, terrain =>
                {
                    var changed = false;

                    for (var i = 0; i < terrain.Length; i++)
                        if ((terrain[i] & 0x0003) != 0)
                        {
                            terrain[i] = (ushort)(terrain[i] & ~0x0003);
                            changed = true;
                        }

                    return changed;
                });
        }

        private const float OutdoorCellLength = 24f;
        private const uint OutdoorCellSide = 8;

        /// <summary>The outdoor cell (1-64, with the landblock prefix of <paramref name="landblockPrefix"/>) that a position inside the landblock lies in.</summary>
        public static uint OutdoorCell(uint landblockPrefix, float x, float y)
        {
            var cellX = x < 0 ? 0u : (uint)(x / OutdoorCellLength);
            var cellY = y < 0 ? 0u : (uint)(y / OutdoorCellLength);

            return (landblockPrefix & 0xFFFF0000) | (cellX * OutdoorCellSide + cellY + 1);
        }

        /// <summary>
        /// Removes the objects of one outdoor cell, as Melt's removeObjects does. <paramref name="fromCell"/> holds the landblock in its high 16 bits and the outdoor cell (1-64) in its low 16.
        /// With no id list every object in the cell goes; with a list only the listed ones that lie in the cell.
        /// </summary>
        public bool RemoveObjects(uint fromCell, IReadOnlyCollection<uint> objectIds = null)
        {
            var infoId = LandblockInfoId(fromCell);

            if (!_store.TryGet(infoId, out var data))
                return true;

            var info = FileTypes.LandblockInfo.Parse(data);

            if (RemoveObjectsInCell(fromCell & 0xFFFF, 0, info, objectIds))
                _store.Set(infoId, info.Pack());

            return true;
        }

        private static bool RemoveObjectsInCell(uint cell, uint landblockPrefix, FileTypes.LandblockInfo info, IReadOnlyCollection<uint> objectIds)
        {
            var kept = new List<Entity.Stab>();
            var removed = 0;

            foreach (var stab in info.Objects)
            {
                if (objectIds != null && !objectIds.Contains(stab.Id))
                {
                    kept.Add(stab);
                    continue;
                }

                var x = stab.Frame.Origin.X;
                var y = stab.Frame.Origin.Y;

                if (OutdoorCell(landblockPrefix, x, y) != cell)
                    kept.Add(stab);
                else
                    removed++;
            }

            info.Objects = kept;
            return removed > 0;
        }

        /// <summary>
        /// Removes the building that has <paramref name="cellInBuilding"/> among its portals' visible cells, and its cells (Melt's removeBuilding kept them by
        /// default, leaving interiors with no way in). Object removal covers the building's own outdoor cell. NumCells goes down to the highest cell left,
        /// so no missing ids are read past it and a landblock left with no cells is not taken for a dungeon; removed ids below it get empty cells.
        /// </summary>
        public bool RemoveBuilding(uint cellInBuilding, bool removeObjects = false, IReadOnlyCollection<uint> objectIds = null)
        {
            var infoId = LandblockInfoId(cellInBuilding);

            if (!_store.TryGet(infoId, out var data))
                return false;

            var info = FileTypes.LandblockInfo.Parse(data);
            var low = (ushort)(cellInBuilding & 0xFFFF);

            for (var i = 0; i < info.Buildings.Count; i++)
            {
                var building = info.Buildings[i];

                if (!building.Portals.Any(p => p.StabList.Contains(low)))
                    continue;

                info.Buildings.RemoveAt(i);

                if (removeObjects)
                {
                    var cell = OutdoorCell(cellInBuilding, building.Frame.Origin.X, building.Frame.Origin.Y);
                    RemoveObjectsInCell(cell, cell & 0xFFFF0000, info, objectIds);
                }

                var removed = RemoveCellsFrom(cellInBuilding);
                info.NumCells = _store.CellsOf(cellInBuilding).Where(id => !IsEmptyCell(id)).Select(id => (id & 0xFFFF) - 0xFF).Where(n => n <= info.NumCells).DefaultIfEmpty(0u).Max();

                // the client loads every cell id below NumCells and asks the server for a missing one, so a gap gets an empty cell instead
                foreach (var id in _store.CellsOf(cellInBuilding).Where(id => IsEmptyCell(id) && (id & 0xFFFF) - 0xFF > info.NumCells))
                    _store.Remove(id);

                foreach (var cell in removed)
                {
                    if ((cell.Key & 0xFFFF) - 0xFF > info.NumCells)
                        continue;

                    var empty = EmptyCell(cell.Value);
                    _store.Set(cell.Key, empty);
                    _emptyCells[cell.Key] = empty;
                }

                _store.Set(infoId, info.Pack());
                return true;
            }

            return false;
        }

        private const int CellFrameBytes = 28;
        private const int CellPortalBytes = 8;
        private const int NoCell = 0xFFFF;

        private static void CellLayout(byte[] cell, out int portalsAt, out int portalCount, out int visibleAt, out int visibleCount)
        {
            portalsAt = 16 + 2 * cell[12] + 4 + CellFrameBytes;
            portalCount = cell[13];
            visibleAt = portalsAt + portalCount * CellPortalBytes;
            visibleCount = BitConverter.ToUInt16(cell, 14);
        }

        /// <summary>A cell record cut after its frame: same id, surfaces and environment, but no flags, portals, visible cells or objects.</summary>
        private static byte[] EmptyCell(byte[] cell)
        {
            CellLayout(cell, out var portalsAt, out _, out _, out _);

            var result = new byte[portalsAt];
            Array.Copy(cell, result, portalsAt);
            Array.Clear(result, 4, 4);
            result[13] = 0;
            Array.Clear(result, 14, 2);
            return result;
        }

        /// <summary>True while the record is still the empty cell RemoveBuilding wrote, not one a later edit put there.</summary>
        private bool IsEmptyCell(uint id) => _emptyCells.TryGetValue(id, out var empty) && _store.TryGet(id, out var cell) && cell == empty;

        private static IEnumerable<ushort> VisibleCells(byte[] cell)
        {
            CellLayout(cell, out _, out _, out var visibleAt, out var visibleCount);

            for (var i = 0; i < visibleCount; i++)
                yield return BitConverter.ToUInt16(cell, visibleAt + i * 2);
        }

        /// <summary>A copy of a cell record under a new id, with its visible cells and portal destinations moved by the same distance.</summary>
        private static byte[] MovedCell(byte[] cell, uint newId, int offset)
        {
            var result = (byte[])cell.Clone();
            CellLayout(result, out var portalsAt, out var portalCount, out var visibleAt, out var visibleCount);

            SetCellId(result, newId);

            for (var i = 0; i < portalCount; i++)
            {
                var at = portalsAt + i * CellPortalBytes + 4;
                var other = BitConverter.ToUInt16(result, at);

                if (other != NoCell)
                    Array.Copy(BitConverter.GetBytes((ushort)(other + offset)), 0, result, at, 2);
            }

            for (var i = 0; i < visibleCount; i++)
            {
                var at = visibleAt + i * 2;
                Array.Copy(BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(result, at) + offset)), 0, result, at, 2);
            }

            return result;
        }

        /// <summary>Removes a cell and every cell reachable through visible-cell lists, as Melt's removeCell with recursion does. Returns the removed records by id.</summary>
        public Dictionary<uint, byte[]> RemoveCellsFrom(uint cellId)
        {
            var removed = new Dictionary<uint, byte[]>();
            var pending = new Stack<uint>();
            pending.Push(cellId);

            while (pending.Count > 0)
            {
                var id = pending.Pop();

                if (!_store.TryGet(id, out var cell))
                    continue;

                _store.Remove(id);
                removed[id] = cell;

                foreach (var visible in VisibleCells(cell))
                    pending.Push((id & 0xFFFF0000) | visible);
            }

            return removed;
        }

        /// <summary>Counts the cells of a store reachable from a cell through visible-cell lists.</summary>
        private static int CountCells(DatCellStore from, uint cellId)
        {
            var seen = new HashSet<uint>();
            var pending = new Stack<uint>();
            pending.Push(cellId);

            while (pending.Count > 0)
            {
                var id = pending.Pop();

                if (seen.Contains(id) || !from.TryGet(id, out var cell))
                    continue;

                seen.Add(id);

                foreach (var visible in VisibleCells(cell))
                    pending.Push((id & 0xFFFF0000) | visible);
            }

            return seen.Count;
        }

        /// <summary>The lowest id at or above <paramref name="start"/> that begins a run of <paramref name="length"/> unused ids.</summary>
        private uint NextFreeCellBlock(uint start, int length)
        {
            var run = 0;
            var current = start;
            var blockStart = start;

            while (true)
            {
                if (!_store.Contains(current))
                {
                    run++;
                    current++;

                    if (run >= length)
                        return blockStart;
                }
                else
                {
                    run = 0;
                    current++;
                    blockStart = current;
                }
            }
        }

        /// <summary>
        /// Copies the cells of a building from a store into free cell ids of this landblock, moving the building's portals to the new ids (Melt's copyBuildingCellsNewId).
        /// The building's visible cells are the ones copied.
        /// </summary>
        private void CopyBuildingCells(uint landblockPrefix, Entity.BuildInfo building, DatCellStore from, uint newLowestCellId)
        {
            var copied = new HashSet<uint>();
            var lowest = building.Portals.SelectMany(p => p.StabList).Select(c => (uint)c).DefaultIfEmpty(uint.MaxValue).Min();
            var offset = (int)(newLowestCellId - ((landblockPrefix & 0xFFFF0000) | lowest));

            foreach (var portal in building.Portals)
            {
                for (var i = 0; i < portal.StabList.Count; i++)
                {
                    var fromId = (landblockPrefix & 0xFFFF0000) | portal.StabList[i];
                    var toId = (uint)(fromId + offset);

                    if (!copied.Contains(toId) && from.TryGet(fromId, out var cell))
                    {
                        _store.Set(toId, MovedCell(cell, toId, offset));
                        copied.Add(toId);
                    }

                    portal.StabList[i] += (ushort)offset;
                }

                portal.OtherCellId += (ushort)offset;
            }
        }

        /// <returns>The NumCells that covers the copied cells: the free run can lie in a gap below NumCells or past orphan records.</returns>
        private uint CopyBuildingToFreeCells(uint landblockPrefix, Entity.BuildInfo building, DatCellStore from)
        {
            var needed = CountCells(from, (landblockPrefix & 0xFFFF0000) | building.Portals[0].OtherCellId);
            var newCell = NextFreeCellBlock(0x0100 | (landblockPrefix & 0xFFFF0000), needed);

            CopyBuildingCells(landblockPrefix, building, from, newCell);
            return building.Portals.SelectMany(p => p.StabList).Select(c => (uint)c - 0xFF).DefaultIfEmpty(0u).Max();
        }

        /// <summary>
        /// Adds the building that has <paramref name="cellInBuilding"/> among its portals' visible cells from another store to the same landblock here,
        /// with its cells in the first free run of cell ids (Melt's addBuildingFrom). Returns false when the source has no such building.
        /// </summary>
        public bool AddBuilding(uint cellInBuilding, DatCellStore from)
        {
            var infoId = LandblockInfoId(cellInBuilding);
            var low = (ushort)(cellInBuilding & 0xFFFF);

            if (!from.TryGet(infoId, out var sourceData))
                return false;

            var building = FileTypes.LandblockInfo.Parse(sourceData).Buildings.FirstOrDefault(b => b.Portals.Any(p => p.StabList.Contains(low)));

            if (building == null)
                return false;

            if (_store.TryGet(infoId, out var destinationData))
            {
                var info = FileTypes.LandblockInfo.Parse(destinationData);

                info.NumCells = Math.Max(info.NumCells, CopyBuildingToFreeCells(cellInBuilding, building, from));
                info.Buildings.Add(building);
                _store.Set(infoId, info.Pack());
            }

            return true;
        }

        /// <summary>
        /// Removes a landblock's buildings, static objects and environment cells: the landblock info record goes and the has-objects flag is cleared. The terrain stays.
        /// </summary>
        /// <returns>False if the landblock has no landblock info.</returns>
        public bool RemoveLandblockInfo(uint cellIdInLandblock)
        {
            var landblockId = LandblockId(cellIdInLandblock);
            var infoId = LandblockInfoId(landblockId);

            if (!_store.Contains(infoId))
                return false;

            foreach (var id in _store.CellsOf(landblockId))
                _store.Remove(id);

            _store.Remove(infoId);

            if (_store.TryGet(landblockId, out var landblock))
            {
                landblock = (byte[])landblock.Clone();
                Array.Clear(landblock, LandblockHasObjectsOffset, 4);
                _store.Set(landblockId, landblock);
            }

            return true;
        }

        /// <summary>Sets a landblock's has-objects flag, without which its landblock info (buildings and static objects) is never loaded.</summary>
        /// <returns>False if the landblock doesn't exist or has no landblock info.</returns>
        public bool SetHasObjects(uint cellIdInLandblock)
        {
            var landblockId = LandblockId(cellIdInLandblock);

            if (!_store.Contains(LandblockInfoId(landblockId)) || !_store.TryGet(landblockId, out var landblock))
                return false;

            landblock = (byte[])landblock.Clone();
            landblock[LandblockHasObjectsOffset] = 1;
            _store.Set(landblockId, landblock);

            return true;
        }

        /// <summary>Copies the terrain parts of a landblock from another store; a landblock the source lacks is removed here unless <paramref name="createOnly"/>.</summary>
        public void ReplaceTerrain(uint cellIdInLandblock, DatCellStore from, bool heightmap = true, bool textures = true, bool createOnly = false)
        {
            var landblockId = LandblockId(cellIdInLandblock);
            var exists = _store.TryGet(landblockId, out var to);

            if (from.TryGet(landblockId, out var source))
            {
                if (exists && createOnly)
                    return;

                if (exists)
                {
                    to = (byte[])to.Clone();

                    if (heightmap)
                        Array.Copy(source, LandblockHeightOffset, to, LandblockHeightOffset, LandblockHeightBytes);
                    if (textures)
                        Array.Copy(source, LandblockTerrainOffset, to, LandblockTerrainOffset, LandblockTerrainBytes);
                }
                else
                {
                    to = (byte[])source.Clone();
                    Array.Clear(to, LandblockHasObjectsOffset, 4);
                }

                _store.Set(landblockId, to);
            }
            else if (exists && !createOnly)
                _store.Remove(landblockId);
        }

        /// <summary>Replaces one terrain word by another in a landblock (a landblock that does not exist is left alone).</summary>
        public void ReplaceTexture(uint cellIdInLandblock, ushort texture, ushort replacement)
        {
            ChangeTerrain(cellIdInLandblock, terrain =>
            {
                var changed = false;

                for (var i = 0; i < terrain.Length; i++)
                    if (terrain[i] == texture)
                    {
                        terrain[i] = replacement;
                        changed = true;
                    }

                return changed;
            });
        }

        /// <summary>Replaces the surfaces of the environment cells of the landblocks (high 16 bits of an id) by what <paramref name="map"/> gives for them.</summary>
        public void ReplaceSurfaces(IEnumerable<uint> landblocks, IReadOnlyDictionary<uint, uint> map)
        {
            foreach (var id in landblocks.SelectMany(_store.CellsOf))
            {
                _store.TryGet(id, out var data);
                var cell = FileType.Unpack<EnvCell>(data);

                if (!cell.Surfaces.Any(map.ContainsKey))
                    continue;

                for (var i = 0; i < cell.Surfaces.Count; i++)
                    if (map.TryGetValue(cell.Surfaces[i], out var surface))
                        cell.Surfaces[i] = surface;

                _store.Set(id, cell.Pack());
            }
        }

        /// <summary>The landblocks of a rectangle (see <see cref="RectLandblocks"/>) that exist in this store, as the high 16 bits of an id.</summary>
        public IReadOnlyList<uint> LandblocksInRect(ushort topLeft, ushort bottomRight)
        {
            return RectLandblocks(topLeft, bottomRight).Select(p => p.Key).Where(l => _store.Contains(LandblockId(l))).ToList();
        }

        public void ReplaceLandblocks(IEnumerable<uint> landblocks, DatCellStore from, bool heightmap = true, bool textures = true, bool objects = true, bool cells = true)
        {
            foreach (var landblock in landblocks)
                ReplaceLandblock(landblock, from, heightmap, textures, objects, cells);
        }

        /// <summary>The ids of Melt's settlement list: landblocks (any id inside them) that are to lose their housing settlement, by how much of the old landblock to keep.</summary>
        public class SettlementList
        {
            public List<uint> Regular = new List<uint>();
            public List<uint> PreserveTextures = new List<uint>();
            public List<uint> PreserveSurface = new List<uint>();

            public static SettlementList Parse(System.IO.TextReader lines)
            {
                var list = new SettlementList();
                string line;

                while ((line = lines.ReadLine()) != null)
                {
                    var parts = line.Split('\t');

                    if (parts[0].Trim().Length == 0)
                        continue;

                    var id = Convert.ToUInt32(parts[0].Trim(), 16);
                    var kind = parts.Length > 1 ? parts[1].Trim() : "";

                    if (kind == "PreserveTextures")
                        list.PreserveTextures.Add(id);
                    else if (kind == "PreserveSurface")
                        list.PreserveSurface.Add(id);
                    else
                        list.Regular.Add(id);
                }

                return list;
            }
        }

        /// <summary>
        /// Takes the housing settlements out, using a store from before they existed (Melt's removeHouseSettlements). Regular landblocks are replaced whole and lose their noob pillars;
        /// "preserve textures" ones take height, objects and cells but keep their terrain words, minus the roads; "preserve surface" ones take only the buildings and cells.
        /// </summary>
        public void RemoveHouseSettlements(DatCellStore withoutSettlements, SettlementList settlements)
        {
            ReplaceLandblocks(settlements.Regular, withoutSettlements);
            RemoveNoobPillars(settlements.Regular);

            ReplaceLandblocks(settlements.PreserveTextures, withoutSettlements, true, false, true);
            RemoveRoads(settlements.PreserveTextures);

            ReplaceLandblocks(settlements.PreserveSurface, withoutSettlements, false, false, false);
        }

        private const uint RuinedSpireModel = 0x01002866;
        private const uint PristineSpireModel = 0x010014C3;
        private static readonly uint[] PortalStandObjects = { 0x020007C5, 0x02000B6F, 0x02000B61 };

        /// <summary>
        /// A starter outpost landblock from a store with the pristine spire (Melt's replaceLandblockSpecialForStarterOutposts): the terrain textures and has-objects flag come from the source,
        /// the ruined spire and its cells go, the pristine spire is copied in with new cell ids, and the objects become the portal stands that were here plus the source's objects.
        /// </summary>
        public void ReplaceStarterOutpost(uint cellIdInLandblock, DatCellStore from)
        {
            var landblockId = LandblockId(cellIdInLandblock);
            var infoId = LandblockInfoId(cellIdInLandblock);
            var prefix = landblockId & 0xFFFF0000;

            if (from.TryGet(landblockId, out var sourceLandblock))
            {
                if (_store.TryGet(landblockId, out var to))
                {
                    to = (byte[])to.Clone();
                    Array.Copy(sourceLandblock, LandblockTerrainOffset, to, LandblockTerrainOffset, LandblockTerrainBytes);
                    Array.Copy(sourceLandblock, LandblockHasObjectsOffset, to, LandblockHasObjectsOffset, 4);
                    _store.Set(landblockId, to);
                }
                else
                    _store.Set(landblockId, (byte[])sourceLandblock.Clone());
            }
            else
                _store.Remove(landblockId);

            if (!from.TryGet(infoId, out var sourceInfoData))
                return;

            var source = FileTypes.LandblockInfo.Parse(sourceInfoData);

            if (!_store.TryGet(infoId, out var destinationData))
            {
                source.NumCells = 0;
                source.Buildings.Clear();
                _store.Set(infoId, source.Pack());
                return;
            }

            var info = FileTypes.LandblockInfo.Parse(destinationData);
            var ruined = info.Buildings.FirstOrDefault(b => b.ModelId == RuinedSpireModel && b.Portals.Count > 0);

            if (ruined != null)
            {
                RemoveBuilding(prefix | ruined.Portals[0].OtherCellId);
                info = FileTypes.LandblockInfo.Parse(_store.TryGet(infoId, out var refreshed) ? refreshed : destinationData);
            }

            info.Objects = info.Objects.Where(o => PortalStandObjects.Contains(o.Id)).Concat(source.Objects).ToList();

            var pristine = source.Buildings.FirstOrDefault(b => b.ModelId == PristineSpireModel && b.Portals.Count > 0);

            if (pristine != null)
            {
                info.Buildings.Add(pristine);
                info.NumCells = Math.Max(info.NumCells, CopyBuildingToFreeCells(prefix, pristine, from));
            }

            _store.Set(infoId, info.Pack());
        }

        public void ReplaceStarterOutposts(IEnumerable<uint> landblocks, DatCellStore from)
        {
            foreach (var landblock in landblocks)
                ReplaceStarterOutpost(landblock, from);
        }

        private int CopyCells(uint landblockId, uint newLandblockId, DatCellStore from)
        {
            var copied = 0;

            foreach (var id in from.CellsOf(landblockId))
            {
                from.TryGet(id, out var data);

                var newId = (id & 0xFFFF) | (newLandblockId & 0xFFFF0000);
                var cell = (byte[])data.Clone();
                SetCellId(cell, newId);

                _store.Set(newId, cell);
                copied++;
            }

            return copied;
        }

        /// <summary>An environment cell record holds its id twice (offsets 0 and 8).</summary>
        private static void SetCellId(byte[] cell, uint id)
        {
            SetId(cell, id);
            Array.Copy(BitConverter.GetBytes(id), 0, cell, 8, 4);
        }

        private static void SetId(byte[] record, uint id)
        {
            Array.Copy(BitConverter.GetBytes(id), 0, record, 0, 4);
        }
    }
}
