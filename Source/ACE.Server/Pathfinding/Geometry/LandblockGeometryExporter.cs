using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Environment = ACE.DatLoader.FileTypes.Environment;
using Frame = ACE.DatLoader.Entity.Frame;
using Vector3 = System.Numerics.Vector3;

namespace ACE.Server.Pathfinding.Geometry
{
    class BoundingBox2
    {
        private bool hasData = false;

        public float MinX { get; set; }
        public float MaxX { get; set; }
        public float MinY { get; set; }
        public float MaxY { get; set; }
        public float MinZ { get; set; }
        public float MaxZ { get; set; }

        public float Width { get => MaxX - MinX; }
        public float Depth { get => MaxY - MinY; }
        public float Height { get => MaxZ - MinZ; }

        public bool ContainsPoint2D(Vector3 loc)
        {
            return loc.X >= MinX && loc.X <= MaxX && loc.Y >= MinY && loc.Y <= MaxY;
        }

        public bool IntersectsRect(float minX, float minY, float maxX, float maxY)
        {
            return hasData && maxX >= MinX && minX <= MaxX && maxY >= MinY && minY <= MaxY;
        }

        public bool IntersectsCircle(Vector3 loc, float radius)
        {
            // dumb
            return loc.X + radius >= MinX && loc.X - radius <= MaxX && loc.Y + radius >= MinY && loc.Y - radius <= MaxY;
        }

        internal void Add(Vector3 pv)
        {
            if (!hasData || pv.X < MinX)
                MinX = pv.X;
            if (!hasData || pv.Y < MinY)
                MinY = pv.Y;
            if (!hasData || pv.Z < MinZ)
                MinZ = pv.Z;

            if (!hasData || pv.X > MaxX)
                MaxX = pv.X;
            if (!hasData || pv.Y > MaxY)
                MaxY = pv.Y;
            if (!hasData || pv.Z > MaxZ)
                MaxZ = pv.Z;

            hasData = true;
        }

        public override string ToString()
        {
            return $"MinX:{MinX}, MinY:{MinY}, MinZ:{MinZ}, MaxX:{MaxX}, MaxY:{MaxY}, MaxZ:{MaxZ}, Width:{Width}, Height:{Height}";
        }
    }

    /// <summary>
    /// The parts of a physics landblock the outdoor mesh is built from, with its object lists copied. For the
    /// pathfinder this is a detached landblock (Physics.Common.Landblock.CreateDetached) that nothing else uses.
    /// </summary>
    public class SurfaceSnapshot
    {
        public ACE.Server.Physics.Common.Landblock Landblock { get; }
        public List<ACE.Server.Physics.PhysicsObj> Buildings { get; }
        public List<ACE.Server.Physics.PhysicsObj> StaticObjects { get; }
        public List<ACE.Server.Physics.PhysicsObj> Scenery { get; }

        public SurfaceSnapshot(ACE.Server.Physics.Common.Landblock landblock)
        {
            Landblock = landblock;
            Buildings = landblock.Buildings?.Cast<ACE.Server.Physics.PhysicsObj>().ToList() ?? new List<ACE.Server.Physics.PhysicsObj>();
            StaticObjects = landblock.StaticObjects?.ToList() ?? new List<ACE.Server.Physics.PhysicsObj>();
            Scenery = landblock.Scenery?.ToList() ?? new List<ACE.Server.Physics.PhysicsObj>();
        }
    }

    /// <summary>
    /// One landblock's contribution to an outdoor tile: the tile's own landblock, or a neighbor whose edge strip
    /// forms the tile's border.
    /// </summary>
    public class SurfaceSource
    {
        /// <summary>
        /// The landblock, in the format 0xAAAA0000
        /// </summary>
        public uint LandblockId { get; }

        /// <summary>
        /// The terrain.
        /// </summary>
        public ACE.Server.Physics.Common.LandblockStruct Terrain { get; }

        /// <summary>
        /// The buildings, static objects and scenery. Null for terrain only.
        /// </summary>
        public SurfaceSnapshot Objects { get; }

        /// <summary>
        /// The interior cells of its buildings and caves, used to cut the terrain out of their interiors.
        /// </summary>
        public List<CellGeometry> InteriorCells { get; }

        public SurfaceSource(uint landblockId, ACE.Server.Physics.Common.LandblockStruct terrain, SurfaceSnapshot objects, List<CellGeometry> interiorCells)
        {
            LandblockId = landblockId & 0xFFFF0000;
            Terrain = terrain;
            Objects = objects;
            InteriorCells = interiorCells ?? new List<CellGeometry>();
        }
    }

    class LandblockGeometryExporter
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<List<int>> Polygons { get; } = new List<List<int>>();

        private List<byte> Height { get; } = new List<byte>();
        private List<ushort> Terrain { get; } = new List<ushort>();
        public LandblockGeometry Geometry { get; }
        public List<CellGeometry> Neighbors { get; }

        private List<BoundingBox> BuildingBoundingBoxes = new List<BoundingBox>();

        public LandblockGeometryExporter(LandblockGeometry geometry, List<CellGeometry> neighbors)
        {
            Geometry = geometry;
            Neighbors = neighbors;
        }

        public void LoadLandblockInfo()
        {
            var cellsById = new Dictionary<uint, CellGeometry>();
            foreach (var cell in Neighbors)
                cellsById[cell.CellId] = cell;

            foreach (var cell in Neighbors)
            {
                LoadEnvironment(cell.EnvCell.EnvironmentId, new List<Frame> { cell.EnvCell.Position }, cell.EnvCell.CellStructure);

                if (cell.EnvCell.StaticObjects == null)
                    continue;

                foreach (var staticObject in cell.EnvCell.StaticObjects)
                {
                    // the physics object the server creates for the static (EnvCell.init_static_objects), so its shape
                    // is posed, scaled and chosen exactly as the server's
                    var obj = ACE.Server.Physics.PhysicsObj.makeObject(staticObject.Id, 0, false);
                    obj.set_initial_frame(new ACE.Server.Physics.Animation.AFrame(staticObject.Frame));

                    // collect the static's polygons, then keep only what the server can collide with
                    _capture = new List<List<Vector3>>();
                    try
                    {
                        foreach (var polygon in GetCollisionPolygons(obj, Vector3.Zero, false))
                            AddPolygon(polygon, NoFrames);
                        AddCellStatic(_capture, cell, cellsById);
                    }
                    finally
                    {
                        _capture = null;
                    }
                }
            }
        }

        #region Cell statics

        /// <summary>
        /// While set, AddPolygon collects transformed polygons here instead of adding them to the mesh.
        /// </summary>
        private List<List<Vector3>> _capture;

        /// <summary>
        /// Clip boxes are grown by this much, so geometry on a cell's boundary (a shelf against a wall) stays whole.
        /// </summary>
        private const float CellClipMargin = 0.05f;

        private readonly Dictionary<uint, CellClipBox> _cellClipBoxes = new Dictionary<uint, CellClipBox>();

        /// <summary>
        /// A cell's volume, approximated by the bounding box of its structure in the cell's own frame: an oriented box
        /// in landblock coordinates.
        /// </summary>
        private sealed class CellClipBox
        {
            public Vector3 Origin;
            public Vector3[] Axes;
            public Vector3 Min;
            public Vector3 Max;

            /// <summary>
            /// The box's bounds in landblock coordinates, for quick overlap tests.
            /// </summary>
            public Vector3 WorldMin;
            public Vector3 WorldMax;

            public bool Overlaps(Vector3 min, Vector3 max)
            {
                return min.X <= WorldMax.X && max.X >= WorldMin.X && min.Y <= WorldMax.Y && max.Y >= WorldMin.Y && min.Z <= WorldMax.Z && max.Z >= WorldMin.Z;
            }

            /// <summary>
            /// The part of a convex polygon inside the box (Sutherland-Hodgman against its six faces). Keeps the
            /// winding. Returns fewer than 3 points if nothing is inside.
            /// </summary>
            public List<Vector3> Clip(List<Vector3> polygon)
            {
                var result = polygon;
                for (var axis = 0; axis < 3 && result.Count >= 3; axis++)
                {
                    var a = Axes[axis];
                    var lo = GetComponent(Min, axis);
                    var hi = GetComponent(Max, axis);
                    result = ClipToPlane(result, p => Vector3.Dot(p - Origin, a) - lo);
                    if (result.Count >= 3)
                        result = ClipToPlane(result, p => hi - Vector3.Dot(p - Origin, a));
                }
                return result;
            }

            private static float GetComponent(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

            /// <summary>
            /// Keeps the part of a polygon where inside(p) >= 0.
            /// </summary>
            private static List<Vector3> ClipToPlane(List<Vector3> polygon, Func<Vector3, float> inside)
            {
                var output = new List<Vector3>(polygon.Count + 2);
                for (var i = 0; i < polygon.Count; i++)
                {
                    var current = polygon[i];
                    var previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                    var dc = inside(current);
                    var dp = inside(previous);

                    if (dc >= 0f)
                    {
                        if (dp < 0f)
                            output.Add(Vector3.Lerp(previous, current, dp / (dp - dc)));
                        output.Add(current);
                    }
                    else if (dp >= 0f)
                    {
                        output.Add(Vector3.Lerp(previous, current, dp / (dp - dc)));
                    }
                }
                return output;
            }
        }

        private CellClipBox GetCellClipBox(CellGeometry cell)
        {
            if (_cellClipBoxes.TryGetValue(cell.CellId, out var box))
                return box;

            box = null;
            var environment = DatManager.PortalDat.ReadFromDat<Environment>(cell.EnvCell.EnvironmentId);
            if (environment?.Cells != null && environment.Cells.TryGetValue(cell.EnvCell.CellStructure, out var cellStruct) && cellStruct.VertexArray.Vertices.Count > 0)
            {
                var min = new Vector3(float.MaxValue);
                var max = new Vector3(float.MinValue);
                foreach (var v in cellStruct.VertexArray.Vertices.Values)
                {
                    min = Vector3.Min(min, v.Origin);
                    max = Vector3.Max(max, v.Origin);
                }
                min -= new Vector3(CellClipMargin);
                max += new Vector3(CellClipMargin);

                var frame = cell.EnvCell.Position;
                box = new CellClipBox
                {
                    Origin = frame.Origin,
                    Axes = new[] { Transform(Vector3.UnitX, frame.Orientation), Transform(Vector3.UnitY, frame.Orientation), Transform(Vector3.UnitZ, frame.Orientation) },
                    Min = min,
                    Max = max,
                    WorldMin = new Vector3(float.MaxValue),
                    WorldMax = new Vector3(float.MinValue)
                };

                for (var corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y, (corner & 4) == 0 ? min.Z : max.Z);
                    var world = Transform(local, frame.Orientation) + frame.Origin;
                    box.WorldMin = Vector3.Min(box.WorldMin, world);
                    box.WorldMax = Vector3.Max(box.WorldMax, world);
                }
            }

            _cellClipBoxes[cell.CellId] = box;
            return box;
        }

        /// <summary>
        /// Adds a static object of an interior cell, clipped to the cells the server registers it in for collision.
        /// The server only tests a static against objects in those cells (EnvCell.find_transit_cells: its own cell,
        /// the connected cells its bounds reach into, and the outdoor cells when it reaches through a portal to the
        /// outside). Geometry outside them can't block anything, like the lower half of a collision box that hangs
        /// through a floor. A static that reaches outside is kept whole.
        /// </summary>
        private void AddCellStatic(List<List<Vector3>> polygons, CellGeometry cell, Dictionary<uint, CellGeometry> cellsById)
        {
            if (polygons.Count == 0)
                return;

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var polygon in polygons)
            {
                foreach (var p in polygon)
                {
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            var boxes = new List<CellClipBox>();
            var visited = new HashSet<uint>();
            var queue = new Queue<CellGeometry>();
            queue.Enqueue(cell);
            visited.Add(cell.CellId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var box = GetCellClipBox(current);
                if (box == null || ReachesOutside(current, min, max))
                {
                    AddAll(polygons);
                    return;
                }
                boxes.Add(box);

                if (current.EnvCell.CellPortals == null)
                    continue;

                foreach (var portal in current.EnvCell.CellPortals)
                {
                    if (portal.OtherCellId == 0xFFFF)
                        continue;

                    var otherId = (current.CellId & 0xFFFF0000) | portal.OtherCellId;
                    if (!visited.Add(otherId) || !cellsById.TryGetValue(otherId, out var other))
                        continue;

                    var otherBox = GetCellClipBox(other);
                    if (otherBox == null || otherBox.Overlaps(min, max))
                        queue.Enqueue(other);
                }
            }

            foreach (var polygon in polygons)
            {
                foreach (var box in boxes)
                {
                    var clipped = box.Clip(polygon);
                    if (clipped.Count >= 3)
                        AddTransformedPolygon(clipped);
                }
            }
        }

        private void AddAll(List<List<Vector3>> polygons)
        {
            foreach (var polygon in polygons)
                AddTransformedPolygon(polygon);
        }

        /// <summary>
        /// True if bounds (in landblock coordinates) reach past one of a cell's portals to the outside, where the
        /// server would register a static in the outdoor cells too.
        /// </summary>
        private bool ReachesOutside(CellGeometry cell, Vector3 min, Vector3 max)
        {
            if (cell.EnvCell.CellPortals == null || !cell.EnvCell.CellPortals.Any(p => p.OtherCellId == 0xFFFF))
                return false;

            var environment = DatManager.PortalDat.ReadFromDat<Environment>(cell.EnvCell.EnvironmentId);
            if (environment?.Cells == null || !environment.Cells.TryGetValue(cell.EnvCell.CellStructure, out var cellStruct))
                return true;

            var frame = cell.EnvCell.Position;
            var cellCenter = cellStruct.VertexArray.Vertices.Values.Aggregate(Vector3.Zero, (a, v) => a + v.Origin) / Math.Max(1, cellStruct.VertexArray.Vertices.Count);
            cellCenter = Transform(cellCenter, frame.Orientation) + frame.Origin;

            foreach (var portal in cell.EnvCell.CellPortals.Where(p => p.OtherCellId == 0xFFFF))
            {
                if (!cellStruct.Polygons.TryGetValue(portal.PolygonId, out var polygon) || polygon.VertexIds.Count < 3)
                    return true;

                var points = polygon.VertexIds.Select(v => Transform(cellStruct.VertexArray.Vertices[(ushort)v].Origin, frame.Orientation) + frame.Origin).ToList();
                var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
                if (normal.LengthSquared() < 1e-12f)
                    continue;
                normal = Vector3.Normalize(normal);
                if (Vector3.Dot(normal, cellCenter - points[0]) > 0f)
                    normal = -normal; // point out of the cell

                // any corner of the bounds past the portal's plane, on the outside
                for (var corner = 0; corner < 8; corner++)
                {
                    var c = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y, (corner & 4) == 0 ? min.Z : max.Z);
                    if (Vector3.Dot(c - points[0], normal) > 0f)
                        return true;
                }
            }

            return false;
        }

        #endregion

        private void LoadEnvironment(uint cellFileIndex, List<Frame> frames, int envCellIndex = -1)
        {
            var environment = DatManager.PortalDat.ReadFromDat<Environment>(cellFileIndex);

            // weird vaulted ceiling environments we dont need... they have outwards facing polys on the roof
            // so we end up generating unneeded navmesh polys "outside" the dungeon if these are left in
            if (CellGeometry.ShouldIgnoreEnvironment(environment.Id))
                return;

            if (envCellIndex >= 0)
            {
                //var polys = DrawPhysics ? environment.Cells[(uint)envCellIndex].PhysicsPolygons : environment.Cells[(uint)envCellIndex].Polygons;
                var polys = environment.Cells[(uint)envCellIndex].PhysicsPolygons;
                foreach (var poly in polys.Values)
                {
                    AddPolygon(poly.VertexIds.Select(v => environment.Cells[(uint)envCellIndex].VertexArray.Vertices[(ushort)v].Origin).ToList(), frames);
                }
            }
            else
            {
                foreach (var envCell in environment.Cells.Values)
                {
                    foreach (var poly in envCell.PhysicsPolygons.Values)
                    {
                        AddPolygon(poly.VertexIds.Select(v => envCell.VertexArray.Vertices[(ushort)v].Origin).ToList(), frames);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the floor of a door apron (see DoorApron) for each doorway in a wall.
        /// </summary>
        public void AddDoorAprons(IEnumerable<CellGroupDoor> doors, AgentSize agentSize)
        {
            foreach (var door in doors)
            {
                if (door.IsInWall)
                    AddPolygon(DoorApron.GetFloor(door, agentSize), NoFrames);
            }
        }

        /// <summary>
        /// How much geometry has been added so far, to go back to with Truncate.
        /// </summary>
        public (int vertices, int polygons) GetMark()
        {
            return (Vertices.Count, Polygons.Count);
        }

        /// <summary>
        /// Removes the geometry added since a mark (see GetMark), e.g. one agent size's door aprons, so the same
        /// loaded cells can get another size's.
        /// </summary>
        public void Truncate((int vertices, int polygons) mark)
        {
            for (var i = mark.vertices; i < Vertices.Count; i++)
                _vertexLookup.Remove(Vertices[i]);

            Vertices.RemoveRange(mark.vertices, Vertices.Count - mark.vertices);
            Polygons.RemoveRange(mark.polygons, Polygons.Count - mark.polygons);
        }

        public List<Vector3> AddPolygon(List<Vector3> vertices, List<Frame> frames)
        {
            var retvertices = new List<Vector3>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertice = vertices[vertices.Count - 1 - i];
                foreach (var frame in frames)
                {
                    vertice = Transform(vertice, frame.Orientation);
                    vertice += frame.Origin;
                }
                retvertices.Add(vertice);
            }

            if (_capture != null)
                _capture.Add(retvertices);
            else
                AddTransformedPolygon(retvertices);

            return retvertices;
        }

        /// <summary>
        /// Adds a polygon already in landblock coordinates (in the winding AddPolygon produces) to the mesh arrays.
        /// </summary>
        private void AddTransformedPolygon(List<Vector3> points)
        {
            var poly = new List<int>(points.Count);
            foreach (var point in points)
            {
                // Recast space: y up
                var vertice = new Vector3(point.X, point.Z, point.Y);

                if (_vertexLookup.TryGetValue(vertice, out var index))
                {
                    // obj face indexes start at 1
                    poly.Add(index + 1);
                }
                else
                {
                    Vertices.Add(vertice);
                    _vertexLookup.Add(vertice, Vertices.Count - 1);
                    poly.Add(Vertices.Count);
                }
            }
            Polygons.Add(poly);
        }

        private readonly Dictionary<Vector3, int> _vertexLookup = new Dictionary<Vector3, int>();

        private static readonly List<Frame> NoFrames = new List<Frame>();

        /// <summary>
        /// Returns the geometry as recast-ready arrays (Y up), with polygons fan-triangulated.
        /// </summary>
        public void GetMeshArrays(out float[] vertices, out int[] faces)
        {
            vertices = new float[Vertices.Count * 3];
            for (var i = 0; i < Vertices.Count; i++)
            {
                vertices[i * 3] = Vertices[i].X;
                vertices[i * 3 + 1] = Vertices[i].Y;
                vertices[i * 3 + 2] = Vertices[i].Z;
            }

            var tris = new List<int>(Polygons.Count * 3);
            foreach (var poly in Polygons)
            {
                // polygon indexes are 1-based (obj style)
                for (var i = 2; i < poly.Count; i++)
                {
                    tris.Add(poly[0] - 1);
                    tris.Add(poly[i - 1] - 1);
                    tris.Add(poly[i] - 1);
                }
            }
            faces = tris.ToArray();
        }

        #region Surface

        /// <summary>
        /// Size of the terrain sub-quads used around buildings, so terrain can be cut out of building interiors.
        /// </summary>
        private const float TerrainSubdivisionSize = 0.5f;

        /// <summary>
        /// Terrain this far below an interior floor (or anywhere above it, up to the cell's ceiling) is removed.
        /// Matches the agent height: anything lower than this cannot produce a walkable span under the floor anyway.
        /// </summary>
        private const float TerrainCutBelowFloor = 2.0f;
        private const float TerrainCutAboveCeiling = 0.5f;

        /// <summary>
        /// How far past its own landblock the geometry of an outdoor tile reaches into its neighbors. Must cover the
        /// Recast border (the agent radius plus 3 cells, 2.5m for the Large agent): the walkable area at the
        /// landblock edge is then eroded against the neighbor's obstacles, and the tile's edge polys line up with the
        /// neighbor tile's, so Detour links them.
        /// </summary>
        public const float SurfaceMargin = 4.0f;

        /// <summary>
        /// Objects of a neighbor whose origin is further than this outside the margin are skipped without looking at
        /// their polygons. Generous, as building shells can be large.
        /// </summary>
        private const float MaxObjectReach = 64.0f;

        private class CellFootprint
        {
            public BoundingBox2 Bounds = new BoundingBox2();
            public List<Vector3[]> Floors = new List<Vector3[]>();
            public List<Vector3> FloorNormals = new List<Vector3>();
            public float MaxZ = float.MinValue;
        }

        /// <summary>
        /// The world rect (x, y) the surface geometry is clipped to: the tile's landblock plus SurfaceMargin.
        /// </summary>
        private float _clipMinX, _clipMinY, _clipMaxX, _clipMaxY;

        /// <summary>
        /// Exports the outdoors around one landblock, for its outdoor tile: terrain, building exterior shells, static
        /// objects and scenery of the landblock itself and of the strip of each neighbor within SurfaceMargin of it.
        /// Interior cells are not part of it; each connected group of interior cells gets its own mesh, so spaces that
        /// overlap without being connected never end up in the same heightfield. They are only used to cut the terrain
        /// out of building interiors. Everything is in world coordinates (see GeometryHelpers.GetLandblockOrigin).
        /// </summary>
        public void LoadSurface(uint landblockId, IEnumerable<SurfaceSource> sources)
        {
            var origin = GeometryHelpers.GetLandblockOrigin(landblockId);
            var blockLength = (float)ACE.Server.Physics.Common.LandDefs.BlockLength;

            _clipMinX = origin.X - SurfaceMargin;
            _clipMinY = origin.Y - SurfaceMargin;
            _clipMaxX = origin.X + blockLength + SurfaceMargin;
            _clipMaxY = origin.Y + blockLength + SurfaceMargin;

            foreach (var source in sources)
            {
                var isOwn = source.LandblockId == (landblockId & 0xFFFF0000);

                if (source.Objects != null)
                {
                    foreach (var building in source.Objects.Buildings)
                        AddPhysicsObj(building, source.LandblockId, true, isOwn);

                    foreach (var obj in source.Objects.StaticObjects)
                        AddPhysicsObj(obj, source.LandblockId, false, isOwn);

                    foreach (var obj in source.Objects.Scenery)
                        AddPhysicsObj(obj, source.LandblockId, false, isOwn);
                }

                if (source.Terrain != null)
                    LoadTerrain(source.LandblockId, source.Terrain, BuildFootprints(source.LandblockId, source.InteriorCells));
            }
        }

        private bool IntersectsClip(float minX, float minY, float maxX, float maxY)
        {
            return maxX >= _clipMinX && minX <= _clipMaxX && maxY >= _clipMinY && minY <= _clipMaxY;
        }

        /// <summary>
        /// Adds a polygon given in world coordinates, unless it lies entirely outside the clip rect.
        /// </summary>
        private void AddSurfacePolygon(List<Vector3> points)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in points)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }

            if (IntersectsClip(minX, minY, maxX, maxY))
                AddPolygon(points, NoFrames);
        }

        /// <summary>
        /// Adds the collision shape of a physics object (see GetCollisionPolygons), in world coordinates.
        /// </summary>
        /// <param name="sourceLandblockId">The landblock whose object list holds the object</param>
        /// <param name="isOwn">True for the tile's own landblock, whose objects are never skipped by distance</param>
        private void AddPhysicsObj(ACE.Server.Physics.PhysicsObj obj, uint sourceLandblockId, bool isBuilding, bool isOwn)
        {
            if (obj?.Position?.Frame == null)
                return;

            // Objects are occasionally re-parented to a neighboring landblock (AdjustToOutside), which puts their
            // frame in that landblock's coordinates: the frame is relative to the landblock of their own cell.
            var offset = GeometryHelpers.GetLandblockOrigin(obj.Position.ObjCellID != 0 ? obj.Position.ObjCellID : sourceLandblockId);

            if (!isOwn)
            {
                var at = obj.Position.Frame.Origin + offset;
                if (!IntersectsClip(at.X - MaxObjectReach, at.Y - MaxObjectReach, at.X + MaxObjectReach, at.Y + MaxObjectReach))
                    return;
            }

            foreach (var polygon in GetCollisionPolygons(obj, offset, isBuilding))
                AddSurfacePolygon(polygon);
        }

        /// <summary>
        /// The collision shape of a physics object as polygons, following the same rules the physics engine uses
        /// (PhysicsObj.FindObjCollisions): part BSP polygons when the object has a physics BSP, otherwise cylinder
        /// spheres, otherwise spheres. Posed and scaled as the object is, in the coordinates of its frame plus offset.
        /// Nothing for an ethereal object.
        /// </summary>
        /// <param name="usePolygons">Use the part BSP polygons whatever the object's state, as for buildings</param>
        private static IEnumerable<List<Vector3>> GetCollisionPolygons(ACE.Server.Physics.PhysicsObj obj, Vector3 offset, bool usePolygons)
        {
            if (obj?.PartArray == null || obj.Position?.Frame == null)
                yield break;

            if (obj.State.HasFlag(ACE.Entity.Enum.PhysicsState.Ethereal))
                yield break;

            if (usePolygons || obj.State.HasFlag(ACE.Entity.Enum.PhysicsState.HasPhysicsBSP))
            {
                foreach (var part in obj.PartArray.Parts)
                {
                    if (part?.GfxObj?.PhysicsBSP == null || part.Pos?.Frame == null)
                        continue;

                    var gfxObj = DatManager.PortalDat.ReadFromDat<GfxObj>(part.GfxObj.ID);
                    if (gfxObj?.PhysicsPolygons == null)
                        continue;

                    var frame = part.Pos.Frame;
                    var scale = part.GfxObjScale;

                    foreach (var poly in gfxObj.PhysicsPolygons.Values)
                        yield return poly.VertexIds.Select(v => frame.LocalToGlobal(gfxObj.VertexArray.Vertices[(ushort)v].Origin * scale) + offset).ToList();
                }
                yield break;
            }

            var objFrame = obj.Position.Frame;
            var objScale = obj.Scale;

            var cylSpheres = obj.PartArray.GetCylSphere();
            if (cylSpheres != null && cylSpheres.Count > 0)
            {
                foreach (var cylSphere in cylSpheres)
                {
                    foreach (var poly in GetCylSpherePolygons(cylSphere.LowPoint * objScale, cylSphere.Height * objScale, cylSphere.Radius * objScale))
                        yield return poly.Select(p => objFrame.LocalToGlobal(p) + offset).ToList();
                }
                yield break;
            }

            var spheres = obj.PartArray.GetSphere();
            if (spheres != null && spheres.Count > 0)
            {
                foreach (var sphere in spheres)
                {
                    foreach (var poly in GetSpherePolygons(sphere.Center * objScale, sphere.Radius * objScale))
                        yield return poly.Select(p => objFrame.LocalToGlobal(p) + offset).ToList();
                }
            }
        }

        /// <summary>
        /// Collects the floor polygons and height range of a landblock's interior cells near the clip rect, in world
        /// coordinates. Used to cut the terrain out of building interiors.
        /// </summary>
        private List<CellFootprint> BuildFootprints(uint landblockId, List<CellGeometry> cells)
        {
            var footprints = new List<CellFootprint>();
            var floorZ = ACE.Server.Physics.PhysicsGlobals.FloorZ;
            var origin = GeometryHelpers.GetLandblockOrigin(landblockId);

            foreach (var cell in cells)
            {
                var envCell = cell.EnvCell;
                if (envCell == null)
                    continue;

                var environment = DatManager.PortalDat.ReadFromDat<Environment>(envCell.EnvironmentId);
                if (environment == null || !environment.Cells.TryGetValue(envCell.CellStructure, out var cellStruct))
                    continue;

                var footprint = new CellFootprint();

                foreach (var poly in cellStruct.PhysicsPolygons.Values)
                {
                    if (poly.VertexIds.Count < 3)
                        continue;

                    var points = poly.VertexIds.Select(v => Transform(cellStruct.VertexArray.Vertices[(ushort)v].Origin, envCell.Position.Orientation) + envCell.Position.Origin + origin).ToArray();

                    foreach (var p in points)
                        footprint.MaxZ = Math.Max(footprint.MaxZ, p.Z);

                    var normal = GetNewellNormal(points);
                    if (normal.Z < floorZ)
                        continue;

                    footprint.Floors.Add(points);
                    footprint.FloorNormals.Add(normal);
                    foreach (var p in points)
                        footprint.Bounds.Add(p);
                }

                if (footprint.Floors.Count > 0 && footprint.Bounds.IntersectsRect(_clipMinX, _clipMinY, _clipMaxX, _clipMaxY))
                    footprints.Add(footprint);
            }

            return footprints;
        }

        private static Vector3 GetNewellNormal(Vector3[] points)
        {
            var normal = Vector3.Zero;
            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];
                normal.X += (a.Y - b.Y) * (a.Z + b.Z);
                normal.Y += (a.Z - b.Z) * (a.X + b.X);
                normal.Z += (a.X - b.X) * (a.Y + b.Y);
            }
            var length = normal.Length();
            return length > 0 ? normal / length : Vector3.Zero;
        }

        private static bool PointInPolygon2D(Vector3[] poly, float x, float y)
        {
            var inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].Y > y) != (poly[j].Y > y)) &&
                    (x < (poly[j].X - poly[i].X) * (y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// True if a terrain point at (x, y, terrainZ) lies inside an interior cell: within a floor's 2D outline,
        /// no more than TerrainCutBelowFloor below that floor and not above the cell's ceiling.
        /// </summary>
        private static bool IsInsideInterior(List<CellFootprint> footprints, float x, float y, float terrainZ)
        {
            foreach (var footprint in footprints)
            {
                if (!footprint.Bounds.ContainsPoint2D(new Vector3(x, y, 0)))
                    continue;
                if (terrainZ > footprint.MaxZ + TerrainCutAboveCeiling)
                    continue;

                for (var i = 0; i < footprint.Floors.Count; i++)
                {
                    var floor = footprint.Floors[i];
                    if (!PointInPolygon2D(floor, x, y))
                        continue;

                    var n = footprint.FloorNormals[i];
                    var floorHeight = floor[0].Z - (n.X * (x - floor[0].X) + n.Y * (y - floor[0].Y)) / n.Z;

                    if (terrainZ >= floorHeight - TerrainCutBelowFloor)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds the terrain cells of a landblock that overlap the clip rect, in world coordinates, triangulated exactly
        /// like the physics landblock. Cells overlapping an interior are subdivided and any sub-quad entirely inside an
        /// interior is dropped, so terrain doesn't poke through floors.
        /// </summary>
        private void LoadTerrain(uint landblockId, ACE.Server.Physics.Common.LandblockStruct landblock, List<CellFootprint> footprints)
        {
            var vertices = landblock.VertexArray.Vertices;
            var sideCells = landblock.SideCellCount;
            var sideVerts = landblock.SideVertexCount;
            var cellLength = (float)ACE.Server.Physics.Common.LandDefs.BlockLength / sideCells;
            var origin = GeometryHelpers.GetLandblockOrigin(landblockId);

            for (var x = 0; x < sideCells; x++)
            {
                for (var y = 0; y < sideCells; y++)
                {
                    var x0 = origin.X + x * cellLength;
                    var y0 = origin.Y + y * cellLength;

                    if (!IntersectsClip(x0, y0, x0 + cellLength, y0 + cellLength))
                        continue;

                    var z00 = vertices[x * sideVerts + y].Origin.Z;
                    var z10 = vertices[(x + 1) * sideVerts + y].Origin.Z;
                    var z01 = vertices[x * sideVerts + y + 1].Origin.Z;
                    var z11 = vertices[(x + 1) * sideVerts + y + 1].Origin.Z;
                    var swToNeCut = landblock.SWtoNEcut[x * sideCells + y];

                    // terrain height at local offset (u, v) within this cell, using the same split as the physics polygons
                    float TerrainZ(float u, float v)
                    {
                        u /= cellLength;
                        v /= cellLength;
                        if (swToNeCut)
                        {
                            if (u >= v)
                                return z00 + (z10 - z00) * u + (z11 - z10) * v;
                            return z00 + (z01 - z00) * v + (z11 - z01) * u;
                        }
                        if (u + v <= 1f)
                            return z00 + (z10 - z00) * u + (z01 - z00) * v;
                        return z11 + (z01 - z11) * (1f - u) + (z10 - z11) * (1f - v);
                    }

                    var relevant = footprints.Where(f => f.Bounds.IntersectsRect(x0 - 1f, y0 - 1f, x0 + cellLength + 1f, y0 + cellLength + 1f)).ToList();

                    if (relevant.Count == 0)
                    {
                        var p00 = new Vector3(x0, y0, z00);
                        var p10 = new Vector3(x0 + cellLength, y0, z10);
                        var p01 = new Vector3(x0, y0 + cellLength, z01);
                        var p11 = new Vector3(x0 + cellLength, y0 + cellLength, z11);

                        // counter-clockwise from above (upward facing), matching dat floor polygons
                        if (swToNeCut)
                        {
                            AddPolygon(new List<Vector3>() { p00, p10, p11 }, NoFrames);
                            AddPolygon(new List<Vector3>() { p00, p11, p01 }, NoFrames);
                        }
                        else
                        {
                            AddPolygon(new List<Vector3>() { p00, p10, p01 }, NoFrames);
                            AddPolygon(new List<Vector3>() { p11, p01, p10 }, NoFrames);
                        }
                        continue;
                    }

                    var steps = (int)Math.Ceiling(cellLength / TerrainSubdivisionSize);
                    var step = cellLength / steps;

                    var points = new Vector3[steps + 1, steps + 1];
                    var covered = new bool[steps + 1, steps + 1];
                    for (var i = 0; i <= steps; i++)
                    {
                        for (var j = 0; j <= steps; j++)
                        {
                            var u = i * step;
                            var v = j * step;
                            var p = new Vector3(x0 + u, y0 + v, TerrainZ(u, v));
                            points[i, j] = p;
                            covered[i, j] = IsInsideInterior(relevant, p.X, p.Y, p.Z);
                        }
                    }

                    for (var i = 0; i < steps; i++)
                    {
                        for (var j = 0; j < steps; j++)
                        {
                            // only drop a sub-quad when it is entirely inside, so terrain still meets door thresholds
                            if (covered[i, j] && covered[i + 1, j] && covered[i, j + 1] && covered[i + 1, j + 1])
                                continue;

                            var a = points[i, j];
                            var b = points[i + 1, j];
                            var c = points[i + 1, j + 1];
                            var d = points[i, j + 1];
                            AddPolygon(new List<Vector3>() { a, b, c }, NoFrames);
                            AddPolygon(new List<Vector3>() { a, c, d }, NoFrames);
                        }
                    }
                }
            }
        }

        #endregion

        public string ToObjString()
        {
            var outStr = new StringBuilder();

            var vs = Vertices.ToArray();
            foreach (var vertex in vs)
            {
                //var ox = coords.IsOutside() ? coords.LBX() * 192f : 0f;
                //var oy = coords.IsOutside() ? coords.LBY() * 192f : 0f;
                //outStr.Append($"v {vertex.X + ox} {vertex.Y} {vertex.Z + oy}\n");
                outStr.Append($"v {vertex.X} {vertex.Y} {vertex.Z}\n");
            }

            var ps = Polygons.ToArray();
            foreach (var polygon in ps)
            {
                outStr.Append("f ");
                var rp = polygon.ToList();
                foreach (var indice in rp)
                {
                    outStr.Append($"{indice} ");
                }
                outStr.Append("\n");
            }

            return outStr.ToString();
        }

        public static List<List<Vector3>> GetCylSpherePolygons(Vector3 origin, float height, float radius)
        {
            var num_sides = 12;
            var axis = new Vector3(0, 0, height);
            var results = new List<List<Vector3>>();
            // Get two vectors perpendicular to the axis.
            Vector3 v1;
            if (axis.Z < -0.01 || axis.Z > 0.01)
                v1 = new Vector3(axis.Z, axis.Z, -axis.X - axis.Y);
            else
                v1 = new Vector3(-axis.Y - axis.Z, axis.X, axis.X);
            Vector3 v2 = Vector3.Cross(v1, axis);

            // Make the vectors have length radius.
            v1 *= radius / v1.Length();
            v2 *= radius / v2.Length();

            var Positions = new List<Vector3>();

            // Make the top end cap.
            // Make the end point.
            int pt0 = Positions.Count; // Index of end_point.
            Positions.Add(origin);

            // Make the top points.
            double theta = 0;
            double dtheta = 2 * Math.PI / num_sides;
            for (int i = 0; i < num_sides; i++)
            {
                Positions.Add(origin +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2);
                theta += dtheta;
            }

            // Make the top triangles.
            int pt1 = Positions.Count - 1; // Index of last point.
            int pt2 = pt0 + 1;                  // Index of first point.
            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { Positions[pt0], Positions[pt1], Positions[pt2] });
                pt1 = pt2++;
            }

            // Make the bottom end cap.
            // Make the end point.
            pt0 = Positions.Count; // Index of end_point2.
            Vector3 end_point2 = origin + axis;
            Positions.Add(end_point2);

            // Make the bottom points. 
            theta = 0;
            for (int i = 0; i < num_sides; i++)
            {
                Positions.Add(end_point2 +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2);
                theta += dtheta;
            }

            // Make the bottom triangles.
            theta = 0;
            pt1 = Positions.Count - 1; // Index of last point.
            pt2 = pt0 + 1;                  // Index of first point.
            var p0 = Positions[num_sides + 1];

            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { p0, Positions[pt2], Positions[pt1] });
                pt1 = pt2++;
            }

            // Make the sides.
            // Add the points to the mesh.
            int first_side_point = Positions.Count;
            theta = 0;
            for (int i = 0; i < num_sides; i++)
            {
                Vector3 p1 = origin +
                    (float)Math.Cos(theta) * v1 +
                    (float)Math.Sin(theta) * v2;
                Positions.Add(p1);
                Vector3 p2 = p1 + axis;
                Positions.Add(p2);
                theta += dtheta;
            }

            // Make the side triangles.
            pt1 = Positions.Count - 2;
            pt2 = pt1 + 1;
            int pt3 = first_side_point;
            int pt4 = pt3 + 1;
            for (int i = 0; i < num_sides; i++)
            {
                results.Add(new List<Vector3>() { Positions[pt1], Positions[pt2], Positions[pt4] });
                results.Add(new List<Vector3>() { Positions[pt1], Positions[pt4], Positions[pt3] });

                pt1 = pt3;
                pt3 += 2;
                pt2 = pt4;
                pt4 += 2;
            }

            return results;
        }

        /// <summary>
        /// The triangles of a sphere at the given center.
        /// </summary>
        public static List<List<Vector3>> GetSpherePolygons(Vector3 origin, float radius)
        {
            var results = new List<List<Vector3>>(UnitSphereTriangles.Count);
            foreach (var triangle in UnitSphereTriangles)
                results.Add(new List<Vector3>() { triangle[0] * radius + origin, triangle[1] * radius + origin, triangle[2] * radius + origin });

            return results;
        }

        /// <summary>
        /// A sphere of radius 1 around the origin: an icosahedron subdivided once, its vertices pushed out onto the
        /// sphere. Built once, GetSpherePolygons scales and places it.
        /// </summary>
        private static readonly List<Vector3[]> UnitSphereTriangles = CreateUnitSphereTriangles();

        private static List<Vector3[]> CreateUnitSphereTriangles()
        {
            var vectors = new List<Vector3>();
            var indices = new List<int>();

            Icosahedron(vectors, indices);
            Subdivide(vectors, indices, true);

            // reversed, the winding the sphere polygons have always had
            var triangles = new List<Vector3[]>(indices.Count / 3);
            for (var i = 0; i + 2 < indices.Count; i += 3)
                triangles.Add(new[] { Vector3.Normalize(vectors[indices[i + 2]]), Vector3.Normalize(vectors[indices[i + 1]]), Vector3.Normalize(vectors[indices[i]]) });

            return triangles;
        }
        public static Vector3 CalculateTriSurfaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = new Vector3();
            Vector3 u = new Vector3();
            Vector3 v = new Vector3();

            u.X = b.X - a.X;
            u.Y = b.Y - a.Y;
            u.Z = b.Z - a.Z;

            v.X = c.X - a.X;
            v.Y = c.Y - a.Y;
            v.Z = c.Z - a.Z;

            normal.X = u.Y * v.Z - u.Z * v.Y;
            normal.Y = u.Z * v.X - u.X * v.Z;
            normal.Z = u.X * v.Y - u.Y * v.X;

            return normal;
        }

        public static Vector3 Transform(Vector3 value, Quaternion rotation)
        {
            float x2 = rotation.X + rotation.X;
            float y2 = rotation.Y + rotation.Y;
            float z2 = rotation.Z + rotation.Z;

            float wx2 = rotation.W * x2;
            float wy2 = rotation.W * y2;
            float wz2 = rotation.W * z2;
            float xx2 = rotation.X * x2;
            float xy2 = rotation.X * y2;
            float xz2 = rotation.X * z2;
            float yy2 = rotation.Y * y2;
            float yz2 = rotation.Y * z2;
            float zz2 = rotation.Z * z2;

            return new Vector3(
                value.X * (1.0f - yy2 - zz2) + value.Y * (xy2 - wz2) + value.Z * (xz2 + wy2),
                value.X * (xy2 + wz2) + value.Y * (1.0f - xx2 - zz2) + value.Z * (yz2 - wx2),
                value.X * (xz2 - wy2) + value.Y * (yz2 + wx2) + value.Z * (1.0f - xx2 - yy2));
        }

        private static int GetMidpointIndex(Dictionary<string, int> midpointIndices, List<Vector3> vertices, int i0, int i1)
        {

            var edgeKey = string.Format("{0}_{1}", Math.Min(i0, i1), Math.Max(i0, i1));

            var midpointIndex = -1;

            if (!midpointIndices.TryGetValue(edgeKey, out midpointIndex))
            {
                var v0 = vertices[i0];
                var v1 = vertices[i1];

                var midpoint = (v0 + v1) / 2f;

                if (vertices.Contains(midpoint))
                    midpointIndex = vertices.IndexOf(midpoint);
                else
                {
                    midpointIndex = vertices.Count;
                    vertices.Add(midpoint);
                    midpointIndices.Add(edgeKey, midpointIndex);
                }
            }


            return midpointIndex;

        }

        /// <remarks>
        ///      i0
        ///     /  \
        ///    m02-m01
        ///   /  \ /  \
        /// i2---m12---i1
        /// </remarks>
        /// <param name="vectors"></param>
        /// <param name="indices"></param>
        public static void Subdivide(List<Vector3> vectors, List<int> indices, bool removeSourceTriangles)
        {
            var midpointIndices = new Dictionary<string, int>();

            var newIndices = new List<int>(indices.Count * 4);

            if (!removeSourceTriangles)
                newIndices.AddRange(indices);

            for (var i = 0; i < indices.Count - 2; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpointIndex(midpointIndices, vectors, i0, i1);
                var m12 = GetMidpointIndex(midpointIndices, vectors, i1, i2);
                var m02 = GetMidpointIndex(midpointIndices, vectors, i2, i0);

                newIndices.AddRange(
                    new[]
                    {
                    i0,m01,m02
                    ,
                    i1,m12,m01
                    ,
                    i2,m02,m12
                    ,
                    m02,m01,m12
                    }
                    );

            }

            indices.Clear();
            indices.AddRange(newIndices);
        }

        /// <summary>
        /// create a regular icosahedron (20-sided polyhedron)
        /// </summary>
        /// <param name="primitiveType"></param>
        /// <param name="size"></param>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <remarks>
        /// You can create this programmatically instead of using the given vertex 
        /// and index list, but it's kind of a pain and rather pointless beyond a 
        /// learning exercise.
        /// </remarks>

        /// note: icosahedron definition may have come from the OpenGL red book. I don't recall where I found it. 
        public static void Icosahedron(List<Vector3> vertices, List<int> indices, float scale = 1f)
        {

            indices.AddRange(
                new int[]
                {
                0,4,1,
                0,9,4,
                9,5,4,
                4,5,8,
                4,8,1,
                8,10,1,
                8,3,10,
                5,3,8,
                5,2,3,
                2,7,3,
                7,10,3,
                7,6,10,
                7,11,6,
                11,0,6,
                0,1,6,
                6,1,10,
                9,0,11,
                9,11,2,
                9,2,5,
                7,2,11
                }
                .Select(i => i + vertices.Count)
            );

            var X = 0.525731112119133606f;
            var Z = 0.850650808352039932f;

            vertices.AddRange(
                new[]
                {
                new Vector3(-X, 0f, Z),
                new Vector3(X, 0f, Z),
                new Vector3(-X, 0f, -Z),
                new Vector3(X, 0f, -Z),
                new Vector3(0f, Z, X),
                new Vector3(0f, Z, -X),
                new Vector3(0f, -Z, X),
                new Vector3(0f, -Z, -X),
                new Vector3(Z, X, 0f),
                new Vector3(-Z, X, 0f),
                new Vector3(Z, -X, 0f),
                new Vector3(-Z, -X, 0f)
                }
            );
        }
    }
}
