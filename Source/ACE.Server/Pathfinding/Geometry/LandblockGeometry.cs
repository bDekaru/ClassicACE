using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A doorway from an interior cell group to the outdoors: a cell portal that leads outside, or one edge of the rim
    /// of a portal that is not in a wall (see LandblockGeometry.AddRimDoors).
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

        /// <summary>
        /// The height of the top of the portal polygon (the lintel), in landblock coordinates
        /// </summary>
        public readonly float Top;

        /// <summary>
        /// The horizontal width of the portal polygon
        /// </summary>
        public readonly float Width;

        /// <summary>
        /// The horizontal direction out of the cell through the portal (unit length), or zero for a portal that is not
        /// in a wall (a hole in a floor or ceiling)
        /// </summary>
        public readonly Vector3 Normal;

        /// <summary>
        /// True if the portal is in a wall, so a door apron can be placed in front of it (see DoorApron)
        /// </summary>
        public bool IsInWall => Normal != Vector3.Zero;

        public CellGroupDoor(uint cellId, Vector3 position, float top, float width, Vector3 normal)
        {
            CellId = cellId;
            Position = position;
            Top = top;
            Width = width;
            Normal = normal;
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
    /// A landblock's dat data for the pathfinder: its LandblockInfo and its interior cells, split into groups that are
    /// connected through cell portals.
    /// </summary>
    public class LandblockGeometry
    {
        private readonly object _groupLock = new object();
        private List<CellGroup> _cellGroups;
        private LandblockInfo _landblockInfo;

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
                    _landblockInfo = DatManager.CellDat.ReadFromDat<LandblockInfo>(Id + 0xFFFE);

                return _landblockInfo;
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
        /// <param name="id">The id of the landblock, or of any of its cells; only the landblock part (0xFFFF0000) is kept.</param>
        public LandblockGeometry(uint id)
        {
            Id = id & 0xFFFF0000;
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

            // Union-find over portal links. Only links both cells list count: the physics engine only moves an object
            // into a cell through a portal of the cell it is in, so a cell no other cell portals into can never be
            // entered. Some dungeons keep such orphan cells where two pieces were joined, like the unused end caps
            // of a corridor overlapping the passage that replaced them. Grouped with the passage, their floors and
            // walls would block it on the mesh.
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

                    var cellId = (ushort)(minCell + (uint)i);
                    if (envCells[other]?.CellPortals?.Any(p => p.OtherCellId == cellId) != true)
                        continue; // one-way: the other cell has no portal back

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

            foreach (var group in groups)
            {
                MergeAdjacentDoors(group.Doors);

                foreach (var cellId in group.CellIds.OrderBy(c => c))
                    group.Cells.Add(new CellGeometry(cellId, envCells[cellId - Id - minCell]));
            }

            return groups;
        }

        private static void AddDoors(CellGroup group, uint cellId, EnvCell envCell)
        {
            var environment = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(envCell.EnvironmentId);
            if (environment == null || !environment.Cells.TryGetValue(envCell.CellStructure, out var cellStruct))
                return;

            // the middle of the cell, to tell which side of a portal is outside
            var cellCenter = GeometryHelpers.GetCellCenter(cellStruct, envCell.Position);

            foreach (var portal in envCell.CellPortals.Where(p => p.OtherCellId == 0xFFFF))
            {
                if (!cellStruct.Polygons.TryGetValue(portal.PolygonId, out var polygon) || polygon.VertexIds.Count == 0)
                    continue;

                var points = polygon.VertexIds
                    .Select(v => envCell.Position.LocalToGlobal(cellStruct.VertexArray.Vertices[(ushort)v].Origin))
                    .ToList();

                var center = points.Aggregate(Vector3.Zero, (a, b) => a + b) / points.Count;
                var normal = GetOutwardNormal(points, center, cellCenter);

                if (normal == Vector3.Zero)
                {
                    AddRimDoors(group, cellId, points, center);
                    continue;
                }

                // the width along the wall
                var along = new Vector3(-normal.Y, normal.X, 0f);
                var width = points.Max(p => Vector3.Dot(p, along)) - points.Min(p => Vector3.Dot(p, along));

                group.Doors.Add(new CellGroupDoor(cellId, new Vector3(center.X, center.Y, points.Min(p => p.Z)), points.Max(p => p.Z), width, normal));
            }
        }

        /// <summary>
        /// Rim edges shorter than this horizontally get no doorway.
        /// </summary>
        private const float MinRimEdgeLength = 0.3f;

        /// <summary>
        /// A portal that is not in a wall (a hole in a floor or ceiling, like the mouth of a pit entrance) is crossed
        /// over its rim, not through its middle, which is in the air. Each edge of the rim becomes a doorway of its own,
        /// facing away from the hole, so it gets a door apron like a doorway in a wall: Recast then decides which edges
        /// can be walked over (the rim of a pit whose sides lead down into it) and which can't (a skylight, far above
        /// the floor), and the outdoor side of a usable edge is on the ground next to the hole.
        /// </summary>
        private static void AddRimDoors(CellGroup group, uint cellId, List<Vector3> points, Vector3 center)
        {
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var a = points[j];
                var b = points[i];

                var edge = new Vector3(b.X - a.X, b.Y - a.Y, 0f);
                var length = edge.Length();
                if (length < MinRimEdgeLength)
                    continue;

                var middle = (a + b) * 0.5f;
                var normal = Vector3.Normalize(new Vector3(-edge.Y, edge.X, 0f));
                if (Vector3.Dot(normal, middle - center) < 0f)
                    normal = -normal;

                group.Doors.Add(new CellGroupDoor(cellId, new Vector3(middle.X, middle.Y, Math.Min(a.Z, b.Z)), Math.Max(a.Z, b.Z), length, normal));
            }
        }

        /// <summary>
        /// Wall portals this close to the same plane count as one wall.
        /// </summary>
        private const float SameWallDistance = 0.5f;

        /// <summary>
        /// Wall portals whose openings are this close along the wall or in height count as touching.
        /// </summary>
        private const float TouchingDistance = 0.1f;

        /// <summary>
        /// Merges wall portals that together make up one opening: an opening split between cells, like the lower and
        /// upper half of a cave mouth or the two leaves of a double door. On their own each half could be too low or
        /// have no floor under it. The merged doorway spans both, in the cell of the lowest one.
        /// </summary>
        private static void MergeAdjacentDoors(List<CellGroupDoor> doors)
        {
            var merged = true;
            while (merged)
            {
                merged = false;
                for (var i = 0; i < doors.Count && !merged; i++)
                {
                    for (var j = i + 1; j < doors.Count && !merged; j++)
                    {
                        if (TryMergeDoors(doors[i], doors[j], out var door))
                        {
                            doors[i] = door;
                            doors.RemoveAt(j);
                            merged = true;
                        }
                    }
                }
            }
        }

        private static bool TryMergeDoors(CellGroupDoor a, CellGroupDoor b, out CellGroupDoor door)
        {
            door = default;

            if (!a.IsInWall || !b.IsInWall || Vector3.Dot(a.Normal, b.Normal) < 0.99f)
                return false;

            if (MathF.Abs(Vector3.Dot(a.Position - b.Position, a.Normal)) > SameWallDistance)
                return false;

            // the openings as intervals along the wall and in height
            var along = new Vector3(-a.Normal.Y, a.Normal.X, 0f);
            var aCenter = Vector3.Dot(a.Position, along);
            var bCenter = Vector3.Dot(b.Position, along);
            var aMin = aCenter - a.Width * 0.5f;
            var aMax = aCenter + a.Width * 0.5f;
            var bMin = bCenter - b.Width * 0.5f;
            var bMax = bCenter + b.Width * 0.5f;

            if (aMin > bMax + TouchingDistance || bMin > aMax + TouchingDistance)
                return false;
            if (a.Position.Z > b.Top + TouchingDistance || b.Position.Z > a.Top + TouchingDistance)
                return false;

            var lower = a.Position.Z <= b.Position.Z ? a : b;
            var min = Math.Min(aMin, bMin);
            var max = Math.Max(aMax, bMax);

            // the merged threshold: centered along the wall, on the lower opening's wall plane and floor
            var position = lower.Position + along * ((min + max) * 0.5f - Vector3.Dot(lower.Position, along));

            door = new CellGroupDoor(lower.CellId, position, Math.Max(a.Top, b.Top), max - min, lower.Normal);
            return true;
        }

        /// <summary>
        /// The horizontal unit normal of a portal polygon, pointing away from the cell's center, or zero if the polygon
        /// is (nearly) horizontal.
        /// </summary>
        private static Vector3 GetOutwardNormal(List<Vector3> points, Vector3 center, Vector3 cellCenter)
        {
            var normal = GeometryHelpers.GetPolygonNormal(points);
            var horizontal = new Vector3(normal.X, normal.Y, 0f);
            if (horizontal.Length() < 0.7f)
                return Vector3.Zero; // no area, or not a wall portal (tilted more than ~45 degrees)

            horizontal = Vector3.Normalize(horizontal);
            if (Vector3.Dot(horizontal, center - cellCenter) < 0f)
                horizontal = -horizontal;

            return horizontal;
        }
    }
}
