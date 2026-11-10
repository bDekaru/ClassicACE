using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A doorway from an interior cell group to the outdoors: a cell portal that leads outside.
    /// </summary>
    public readonly struct CellGroupDoor
    {
        /// <summary>
        /// The interior cell the doorway belongs to, in the format 0xAAAABBBB
        /// </summary>
        public readonly uint CellId;

        /// <summary>
        /// The bottom center of the portal polygon (the threshold), in landblock coordinates
        /// </summary>
        public readonly Vector3 Position;

        public CellGroupDoor(uint cellId, Vector3 position)
        {
            CellId = cellId;
            Position = position;
        }
    }

    /// <summary>
    /// A set of interior cells connected to each other through cell portals. Cells in different groups have no walkable
    /// connection, even when their volumes overlap (e.g. the basements of two neighboring houses), so each group gets
    /// its own navmesh.
    /// </summary>
    public class CellGroup
    {
        /// <summary>
        /// 1-based index of this group within its landblock. 0 is reserved for the outdoors.
        /// </summary>
        public int Index { get; }

        public List<CellGeometry> Cells { get; } = new List<CellGeometry>();

        public HashSet<uint> CellIds { get; } = new HashSet<uint>();

        /// <summary>
        /// True for buildings and caves entered from the landscape, false for dungeons only reachable by portal.
        /// </summary>
        public bool ConnectsToOutside { get; internal set; }

        public List<CellGroupDoor> Doors { get; } = new List<CellGroupDoor>();

        public CellGroup(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// Contains all geometry data for a landblock. Use different methods to generate different data types,
    /// like for mapping, nav, etc.
    /// </summary>
    public class LandblockGeometry
    {
        private bool _didLoadTerrain = false;
        private readonly object _groupLock = new object();
        private List<CellGroup> _cellGroups;
        private LandblockInfo _landblockInfo;
        private CellLandblock _cellLandblock;
        private ConcurrentDictionary<uint, CellGeometry> _terrainCells = new ConcurrentDictionary<uint, CellGeometry>();

        /// <summary>
        /// The id of this landblock, in format 0xFFFF0000
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// LandblockInfo from the dat files
        /// </summary>
        public LandblockInfo LandblockInfo
        {
            get
            {
                if (_landblockInfo is null)
                {
                    _landblockInfo = DatManager.CellDat.ReadFromDat<LandblockInfo>(Id + 0xFFFE);
                }

                return _landblockInfo;
            }
        }

        /// <summary>
        /// CellLandblock from the dat files
        /// </summary>
        public CellLandblock CellLandblock
        {
            get
            {
                if (_cellLandblock is null)
                {
                    _cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(Id + 0xFFFF);
                }

                return _cellLandblock;
            }
        }

        /// <summary>
        /// Terrain cells in this landblock, if any. The dictionary key is the landblock + landcell id in the form of 0xAAAABBBB
        /// where AAAA is the landblock id, and BBBB is the landcell id.
        /// </summary>
        public ConcurrentDictionary<uint, CellGeometry> Terrain
        {
            get
            {
                if (!_didLoadTerrain)
                {
                    LoadTerrain();
                }
                return _terrainCells;
            }
        }

        /// <summary>
        /// The interior cells of this landblock, split into groups that are connected through cell portals.
        /// </summary>
        public IReadOnlyList<CellGroup> CellGroups
        {
            get
            {
                lock (_groupLock)
                {
                    if (_cellGroups == null)
                        _cellGroups = LoadCellGroups();
                    return _cellGroups;
                }
            }
        }

        /// <summary>
        /// All interior cells that connect to the landscape (buildings, caves), across all groups.
        /// </summary>
        public List<CellGeometry> SurfaceConnectedCells => CellGroups.Where(g => g.ConnectsToOutside).SelectMany(g => g.Cells).ToList();

        /// <summary>
        /// Create a new landblock geometry object
        /// </summary>
        /// <param name="id">The id of the landblock. Should be in format 0xFFFF0000. If less than 0xFFFF, it will be shifted 16bits to the left</param>
        public LandblockGeometry(uint id)
        {
            if (id <= 0xFFFF)
            {
                Id = id << 16;
            }
            else
            {
                Id = id & 0xFFFF0000;
            }
        }

        #region public api
        /// <summary>
        /// Check if this landblock contains any interior cells (buildings, caves or dungeons).
        /// </summary>
        public bool HasDungeons()
        {
            return LandblockInfo?.NumCells > 0;
        }

        /// <summary>
        /// Check if this landblock contains walkable terrain data. Landblocks containing terrain may
        /// also contain dungeons / indoor cells.
        /// </summary>
        /// <returns>True if this landblock has walkable terrain</returns>
        public bool HasTerrain()
        {
            return CellLandblock?.Height?.Where(h => h != 0).Any() == true;
        }

        /// <summary>
        /// Check if this landblock has any buildings with portals to indoor cells. Caves that are entered
        /// directly from a hole in the landscape are considered indoors, the same as buildings.
        /// </summary>
        /// <returns>True if this landblock contains indoor cells</returns>
        public bool HasIndoors()
        {
            // todo: i'm assuming all buildings have indoor cells..
            return LandblockInfo?.Buildings?.Count() > 0;
        }
        #endregion // public api

        private void LoadTerrain()
        {
            return;
        }

        /// <summary>
        /// Splits the interior cells into connected components of the cell portal graph.
        /// </summary>
        private List<CellGroup> LoadCellGroups()
        {
            var groups = new List<CellGroup>();

            var numCells = LandblockInfo?.NumCells ?? 0;
            if (numCells == 0)
                return groups;

            const uint minCell = 0x0100u;
            var envCells = new EnvCell[numCells];
            for (var i = 0; i < numCells; i++)
                envCells[i] = DatManager.CellDat.ReadFromDat<EnvCell>(Id + minCell + (uint)i);

            // union-find over portal links; portals are treated as two-way
            var parent = Enumerable.Range(0, (int)numCells).ToArray();
            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }

            for (var i = 0; i < numCells; i++)
            {
                if (envCells[i]?.CellPortals == null)
                    continue;

                foreach (var portal in envCells[i].CellPortals)
                {
                    var other = (int)portal.OtherCellId - (int)minCell;
                    if (other < 0 || other >= numCells)
                        continue; // outside (0xFFFF) or out of range
                    parent[Find(i)] = Find(other);
                }
            }

            // cells a building portal leads into
            var buildingEntries = new HashSet<uint>();
            if (LandblockInfo.Buildings != null)
            {
                foreach (var building in LandblockInfo.Buildings)
                {
                    foreach (var portal in building.Portals)
                        buildingEntries.Add(portal.OtherCellId);
                }
            }

            // groups ordered by their lowest cell id, so the indices are stable
            var byRoot = new Dictionary<int, CellGroup>();
            for (var i = 0; i < numCells; i++)
            {
                var envCell = envCells[i];
                if (envCell == null || envCell.Id == 0)
                    continue;

                var root = Find(i);
                if (!byRoot.TryGetValue(root, out var group))
                {
                    group = new CellGroup(groups.Count + 1);
                    byRoot.Add(root, group);
                    groups.Add(group);
                }

                var cellId = Id + minCell + (uint)i;
                var leadsOutside = envCell.CellPortals?.Any(p => p.OtherCellId == 0xFFFF) == true;

                if (leadsOutside || buildingEntries.Contains(minCell + (uint)i))
                    group.ConnectsToOutside = true;

                group.CellIds.Add(cellId);

                if (leadsOutside)
                    AddDoors(group, cellId, envCell);
            }

            // cell geometry type only matters for terrain, but keep the indoor/dungeon distinction for clarity
            foreach (var group in groups)
            {
                var type = group.ConnectsToOutside ? CellType.Indoors : CellType.Dungeon;
                foreach (var cellId in group.CellIds.OrderBy(c => c))
                    group.Cells.Add(CellGeometry.FromCache(cellId, type));
            }

            return groups;
        }

        private static void AddDoors(CellGroup group, uint cellId, EnvCell envCell)
        {
            var environment = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(envCell.EnvironmentId);
            if (environment == null || !environment.Cells.TryGetValue(envCell.CellStructure, out var cellStruct))
                return;

            foreach (var portal in envCell.CellPortals.Where(p => p.OtherCellId == 0xFFFF))
            {
                if (!cellStruct.Polygons.TryGetValue(portal.PolygonId, out var polygon) || polygon.VertexIds.Count == 0)
                    continue;

                var points = polygon.VertexIds
                    .Select(v => LandblockGeometryExporter.Transform(cellStruct.VertexArray.Vertices[(ushort)v].Origin, envCell.Position.Orientation) + envCell.Position.Origin)
                    .ToList();

                var center = points.Aggregate(Vector3.Zero, (a, b) => a + b) / points.Count;
                group.Doors.Add(new CellGroupDoor(cellId, new Vector3(center.X, center.Y, points.Min(p => p.Z))));
            }
        }
    }
}
