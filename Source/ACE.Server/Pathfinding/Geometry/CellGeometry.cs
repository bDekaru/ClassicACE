using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using EnvCell = ACE.DatLoader.FileTypes.EnvCell;
using Plane = System.Numerics.Plane;
using Polygon = ACE.DatLoader.Entity.Polygon;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Cell geometry
    /// </summary>
    public class CellGeometry {
        private static Dictionary<CellType, Dictionary<uint, CellGeometry>> _cellGeometryCache = new Dictionary<CellType, Dictionary<uint, CellGeometry>>();
        private List<List<Vector3>> _walkableTriangles = new List<List<Vector3>>();
        private Dictionary<uint, List<short>> _walkableVertexIds = new Dictionary<uint, List<short>>();
        private Dictionary<uint, List<ushort>> _walkablePolyIds = new Dictionary<uint, List<ushort>>();
        private EnvCell _envCell;
        private ACE.DatLoader.FileTypes.Environment _environment;
        private bool? _hasWalkablePolys;
        private bool? _isRamp;
        private float? _floorZ;
        private BoundingBox _boundingBox;
        public const float MAX_FLOOR_Z_DIFF = 1.0f;

        private List<Plane> _walkablePlanes = new List<Plane>();

        private bool _didInitPolyData = false;

        /// <summary>
        /// The id of this landcell in the format of 0xAAAABBBB where AAAA is the landblock
        /// id and BBBB is the cell id
        /// </summary>
        public uint CellId { get; }

        private List<List<Vector3>> _walkablePolys = new List<List<Vector3>>();
        public List<List<Vector3>> WalkablePolys {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _walkablePolys;
            }
        }

        private List<List<Vector3>> _wallLines = new List<List<Vector3>>();
        public List<List<Vector3>> WallLines {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _wallLines;
            }
        }

        /// <summary>
        /// The type of cell this is.
        /// </summary>
        public CellType Type { get; }

        /// <summary>
        /// Wether this cell has walkable polygons
        /// </summary>
        public bool HasWalkablePolys {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _hasWalkablePolys.Value;
            }
        }

        /// <summary>
        /// wether this cell is a ramp or not
        /// </summary>
        public bool IsRamp => _isRamp ??= _IsRamp();

        /// <summary>
        /// Wether or not this cell is a "catwalk". Catwalks are defined as walkable areas that sit above the floor of a room.
        /// An example is in the marketplace (0x019C) side rooms.
        /// </summary>
        public bool IsCatwalk {
            get {
                if (WalkablePolys.Count == 0)
                    return false;
                // todo: this is bad, but it mostly works?  worried about false positives...
                if (BoundingBox.Size.Length() <= 0.01f)
                    return false;

                var height = BoundingBox.MaxBounds.Z - BoundingBox.MinBounds.Z;
                if (height <= 2f && height >= 0.25f)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// average walkable z height
        /// </summary>
        public float AverageFloorZ => _floorZ ??= GetFloorZ();

        public BoundingBox BoundingBox {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _boundingBox;
            }
        }

        public Dictionary<uint, List<short>> WalkableVertexIds {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _walkableVertexIds;
            }
        }
        public Dictionary<uint, List<ushort>> WalkablePolyIds {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _walkablePolyIds;
            }
        }
        public IEnumerable<Plane> WalkablePlanes {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _walkablePlanes;
            }
        }
        public List<List<Vector3>> WalkableTriangles {
            get {
                if (!_didInitPolyData)
                    InitPolyData();
                return _walkableTriangles;
            }
        }

        /// <summary>
        /// Dat env cell. Not valid on Cells with type CellType.Terrain
        /// </summary>
        public EnvCell EnvCell {
            get {
                if (_envCell == null) {
                    if (Type == CellType.Terrain) {
                        return null;
                    }
                    _envCell = DatManager.CellDat.ReadFromDat<EnvCell>(CellId);
                }

                return _envCell;
            }
        }

        /// <summary>
        /// Dat environment this cell uses. Not valid on Cells with type CellType.Terrain
        /// </summary>
        public ACE.DatLoader.FileTypes.Environment Environment {
            get {
                if (_environment == null) {
                    if (Type == CellType.Terrain) {
                        return null;
                    }
                    _environment = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(EnvCell.EnvironmentId);
                }

                return _environment;
            }
        }

        /// <summary>
        /// A list of polygons used to make up the physics environment of this cell.
        /// </summary>
        public List<Polygon> Polygons { get; } = new List<Polygon>();


        public static CellGeometry FromCache(uint cellId, CellType type) {
            if (!_cellGeometryCache.TryGetValue(type, out var _typeCache)) {
                _typeCache = new Dictionary<uint, CellGeometry>();
                _cellGeometryCache.Add(type, _typeCache);
            }

            if (!_typeCache.TryGetValue(cellId, out var cellGeometry)) {
                cellGeometry = new CellGeometry(cellId, type);
                //_typeCache.Add(cellId, cellGeometry);
            }

            return cellGeometry;
        }

        private CellGeometry(uint cellId, CellType type) {
            CellId = cellId;
            Type = type;
        }

        #region public api
        /// <summary>
        /// Get a list of all cells connected to this cell, including this one. Only valid where CellType != CellType.Terrain.
        /// CellIds present in checkedCells keys will be ignored.
        /// </summary>
        /// <param name="strategy">The strategy to use while finding connected cells</param>
        /// <param name="checkedCells">A dictionary of cellid keys in the format 0xAAAABBBB where AAAA is the landblock id and BBBB is the cell id</param>
        /// <returns>A list of connected cells, including the current cell.</returns>
        public List<CellGeometry> GetConnectedCells(ConnectionStrategy strategy, Dictionary<uint, bool> checkedCells, out List<CellGeometry> neighbors) {
            neighbors = new List<CellGeometry>();
            if (Type == CellType.Terrain) {
                return new List<CellGeometry>() { this };
            }

            List<CellGeometry> cells = null;

            switch (strategy)
            {
                case ConnectionStrategy.Visible:
                    cells = GetConnectedVisibleCells(checkedCells);
                    break;
                case ConnectionStrategy.VisibleSameFloor:
                    var x = new Dictionary<uint, bool>();
                    cells = GetConnectedVisibleCellsSameFloor(x, ref neighbors);
                    foreach (var kv in x) {
                        if (!checkedCells.ContainsKey(kv.Key)) {
                            checkedCells.Add(kv.Key, true);
                        }
                    }
                    break;
                case ConnectionStrategy.VisibleSameFloorSeparatePits:
                    var xx = new Dictionary<uint, bool>();
                    cells = GetConnectedVisibleCellsSameFloorSeparatePits(checkedCells, ref neighbors);
                    foreach (var kv in xx) {
                        if (!checkedCells.ContainsKey(kv.Key)) {
                            checkedCells.Add(kv.Key, true);
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"CellGeometry.GetConnectedCells: Unsupported connection strategy: {strategy}");
            }

            neighbors.Sort((a, b) => a.CellId.CompareTo(b.CellId));

            return cells.Where(c => c.EnvCell.Id != 0).ToList();
        }
        #endregion // public api

        internal bool ShouldIgnoreEnvironment(uint environmentId) {
            switch (environmentId) {
                // roof tiles with "walkable" ceilings. the normal of the top ceiling poly is facing up
                case 0x0D0000CA:
                case 0x0D00016D:
                    return true;
                default:
                    return false;
            }
        }

        private List<CellGeometry>? GetAllCells()
        {
            var ret = new List<CellGeometry>();

            return ret;
        }

        private List<CellGeometry> GetConnectedVisibleCellsSameFloorSeparatePits(Dictionary<uint, bool> checkedCells, ref List<CellGeometry> neighbors) {
            var ret = new List<CellGeometry>();
            if (!checkedCells.ContainsKey(CellId)) {
                checkedCells.Add(CellId, true);
                ret.Add(this);
            }

            foreach (var portal in EnvCell.CellPortals) {
                var otherCellId = (CellId & 0xFFFF0000) + portal.OtherCellId;
                var otherCell = FromCache(otherCellId, Type);
                var thisCell = FromCache(EnvCell.Id, Type);

                if (!neighbors.Any(n => n.CellId == otherCellId)) {
                    neighbors.Add(otherCell);
                }
                if (!checkedCells.ContainsKey(otherCellId) && Math.Abs(EnvCell.Position.Origin.Z - otherCell.EnvCell.Position.Origin.Z) <= MAX_FLOOR_Z_DIFF) {
                    ret.AddRange(otherCell.GetConnectedVisibleCellsSameFloorSeparatePits(checkedCells, ref neighbors));
                }
            }
            return ret;
        }

        private List<CellGeometry> GetConnectedVisibleCellsSameFloor(Dictionary<uint, bool> checkedCells, ref List<CellGeometry> neighbors) {
            var ret = new List<CellGeometry>();
            if (!checkedCells.ContainsKey(CellId)) {
                checkedCells.Add(CellId, true);
                ret.Add(this);
            }
            foreach (var portal in EnvCell.CellPortals) {
                var otherCellId = (CellId & 0xFFFF0000) + portal.OtherCellId;
                var otherCell = FromCache(otherCellId, Type);

                if (!neighbors.Any(n => n.CellId == otherCellId)) {
                    neighbors.Add(otherCell);
                }
                if (!checkedCells.ContainsKey(otherCellId) && Math.Abs(EnvCell.Position.Origin.Z - otherCell.EnvCell.Position.Origin.Z) <= MAX_FLOOR_Z_DIFF) {
                    ret.AddRange(otherCell.GetConnectedVisibleCellsSameFloor(checkedCells, ref neighbors));
                }
            }
            return ret;
        }

        private List<CellGeometry> GetConnectedVisibleCells(Dictionary<uint, bool> checkedCells) {
            var ret = new List<CellGeometry>();
            if (!checkedCells.ContainsKey(CellId)) {
                checkedCells.Add(CellId, true);
                ret.Add(this);
            }
            foreach (var otherCellIndex in EnvCell.VisibleCells) {
                var otherCellId = (CellId & 0xFFFF0000) + otherCellIndex;
                if (!checkedCells.ContainsKey(otherCellId)) {
                    var otherCell = new CellGeometry(otherCellId, Type);
                    ret.AddRange(otherCell.GetConnectedVisibleCells(checkedCells));
                }
            }
            return ret;
        }

        internal void AddPoly(Polygon poly) {
            Polygons.Add(poly);
        }

        private void InitPolyData() {
            if (_didInitPolyData)
                return;

            _hasWalkablePolys = false;
            _boundingBox = new BoundingBox();

            var env = DatManager.PortalDat.ReadFromDat<ACE.DatLoader.FileTypes.Environment>(EnvCell.EnvironmentId);
            if (env.Cells.ContainsKey(EnvCell.CellStructure)) {
                InitCellStruct(EnvCell.CellStructure);
            }
            else {
                foreach (var cellStruct in env.Cells.Keys) {
                    InitCellStruct(cellStruct);
                }
            }

            _didInitPolyData = true;
        }
        private void InitCellStruct(uint cellStructKey) {
            if (EnvCell.EnvironmentId == 0x0D0000CA || EnvCell.EnvironmentId == 0x0D0000CA) {
                _didInitPolyData = true;
                return;
            }

            var cellStruct = Environment.Cells[cellStructKey];
            var polys = cellStruct.PhysicsPolygons;
            var vertices = cellStruct.VertexArray.Vertices;

            if (!_walkablePolyIds.ContainsKey(cellStructKey)) {
                _walkablePolyIds.Add(cellStructKey, new List<ushort>());
            }
            if (!_walkableVertexIds.ContainsKey(cellStructKey)) {
                _walkableVertexIds.Add(cellStructKey, new List<short>());
            }

            // check for short cellstructs that the player cant fit through
            // todo: is this ok? probably no i think...
            //if (cellStruct.PhysicsBSP.RootNode.Sphere.Radius < 2f)
            //    return;

            float walkableThr = (float)Math.Cos(50 / 180.0f * Math.PI);

            List<ushort> _walkableIds = new List<ushort>();

            foreach (var kv in polys) {
                var poly = kv.Value;
                var hasWalkable = false;

                _boundingBox.AddVectors(poly.VertexIds.Select(vId => vertices[(ushort)vId].Origin));

                for (int i = 2; i < poly.VertexIds.Count; i++) {
                    var a = vertices[(ushort)poly.VertexIds[0]].Origin;
                    var b = vertices[(ushort)poly.VertexIds[i - 1]].Origin;
                    var c = vertices[(ushort)poly.VertexIds[i]].Origin;
                    var norm = GeometryHelpers.CalculateTriSurfaceNormal(a, b, c);

                    if (norm.Z >= walkableThr) {
                        _walkableIds.Add(kv.Key);
                        _hasWalkablePolys = true;
                        hasWalkable = true;
                        _walkableTriangles.Add(new List<Vector3>() { a, b, c });
                        _walkablePlanes.Add(Plane.CreateFromVertices(a, b, c));
                    }
                }

                if (hasWalkable) {
                    _walkablePolys.Add(kv.Value.VertexIds.Select(v => vertices[(ushort)v].Origin).ToList());
                    _walkablePolyIds[cellStructKey].Add(kv.Key);
                    _walkableVertexIds[cellStructKey].AddRange(poly.VertexIds);
                }
            }

            foreach (var kv in polys) {
                if (_walkableIds.Contains(kv.Key))
                    continue;

                var poly = kv.Value;
                var verts = new List<Vector3>();
                for (int i = 0; i < poly.VertexIds.Count; i++) {
                    var i1 = poly.VertexIds[i];
                    var i2 = poly.VertexIds[i == 0 ? poly.VertexIds.Count - 1 : i - 1];

                    var v1 = vertices[(ushort)i1].Origin;
                    var v2 = vertices[(ushort)i2].Origin;

                    var d = 0.1f;

                    foreach (var plane in _walkablePlanes) {
                        if (DistanceToPlane(v1, plane) < d && DistanceToPlane(v2, plane) < d && !(verts.Contains(v1) && verts.Contains(v2))) {
                            verts.Add(v1);
                            verts.Add(v2);
                            break;
                        }
                    }
                }

                _wallLines.Add(verts);
            }

            _didInitPolyData = true;
        }

        Vector3 ClosestPointOnPlane(Vector3 point, Plane plane) {
            var pointToPlaneDistance = Vector3.Dot(plane.Normal, point) + plane.D;
            return point - (plane.Normal * pointToPlaneDistance);
        }

        float DistanceToPlane(Vector3 point, Plane plane) {
            var closest = ClosestPointOnPlane(point, plane);

            return Math.Abs(Vector3.Distance(point, closest));
        }

        private bool _IsRamp() {
            var zs = _walkableTriangles.SelectMany(v => v.Select(c => c.Z));
            return Math.Abs(zs.Min() - zs.Max()) > 0.1f;
        }

        private float GetFloorZ() {
            return _walkableTriangles.SelectMany(v => v.Select(c => c.Z)).Average();
        }
    }
}
