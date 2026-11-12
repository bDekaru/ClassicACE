using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Environment = ACE.DatLoader.FileTypes.Environment;
using Frame = ACE.DatLoader.Entity.Frame;
using Vector3 = System.Numerics.Vector3;

namespace ACE.Server.Pathfinding.Geometry
{
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

    class MeshGeometryCollector
    {
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public List<List<int>> Polygons { get; } = new List<List<int>>();

        public List<CellGeometry> Cells { get; }

        public MeshGeometryCollector(List<CellGeometry> cells)
        {
            Cells = cells;
        }

        public void LoadCells()
        {
            var cellsById = new Dictionary<uint, CellGeometry>();
            foreach (var cell in Cells)
                cellsById[cell.CellId] = cell;

            foreach (var cell in Cells)
            {
                LoadEnvironment(cell.EnvCell.EnvironmentId, cell.EnvCell.Position, cell.EnvCell.CellStructure);

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
                            AddPolygon(polygon);
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
                        output.Add(Vector3.Lerp(previous, current, dp / (dp - dc)));
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
                var (min, max) = GeometryHelpers.GetBounds(cellStruct.VertexArray.Vertices.Values.Select(v => v.Origin));
                min -= new Vector3(CellClipMargin);
                max += new Vector3(CellClipMargin);

                var frame = cell.EnvCell.Position;
                box = new CellClipBox
                {
                    Origin = frame.Origin,
                    Axes = new[] { Vector3.Transform(Vector3.UnitX, frame.Orientation), Vector3.Transform(Vector3.UnitY, frame.Orientation), Vector3.Transform(Vector3.UnitZ, frame.Orientation) },
                    Min = min,
                    Max = max,
                    WorldMin = new Vector3(float.MaxValue),
                    WorldMax = new Vector3(float.MinValue)
                };

                for (var corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y, (corner & 4) == 0 ? min.Z : max.Z);
                    var world = frame.LocalToGlobal(local);
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

            var (min, max) = GeometryHelpers.GetBounds(polygons.SelectMany(p => p));

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
            var cellCenter = GeometryHelpers.GetCellCenter(cellStruct, frame);

            foreach (var portal in cell.EnvCell.CellPortals.Where(p => p.OtherCellId == 0xFFFF))
            {
                if (!cellStruct.Polygons.TryGetValue(portal.PolygonId, out var polygon) || polygon.VertexIds.Count < 3)
                    return true;

                var points = polygon.VertexIds.Select(v => frame.LocalToGlobal(cellStruct.VertexArray.Vertices[(ushort)v].Origin)).ToList();
                var center = points.Aggregate(Vector3.Zero, (a, b) => a + b) / points.Count;

                var normal = GeometryHelpers.GetPolygonNormal(points);
                if (normal == Vector3.Zero)
                    continue; // no area
                if (Vector3.Dot(normal, cellCenter - center) > 0f)
                    normal = -normal; // point out of the cell

                // any corner of the bounds past the portal's plane, on the outside
                for (var corner = 0; corner < 8; corner++)
                {
                    var c = new Vector3((corner & 1) == 0 ? min.X : max.X, (corner & 2) == 0 ? min.Y : max.Y, (corner & 4) == 0 ? min.Z : max.Z);
                    if (Vector3.Dot(c - center, normal) > 0f)
                        return true;
                }
            }

            return false;
        }

        #endregion

        /// <summary>
        /// Adds the physics polygons of one cell structure of an environment, posed by the frame.
        /// </summary>
        private void LoadEnvironment(uint cellFileIndex, Frame frame, int cellStructure)
        {
            var environment = DatManager.PortalDat.ReadFromDat<Environment>(cellFileIndex);

            // weird vaulted ceiling environments we dont need... they have outwards facing polys on the roof
            // so we end up generating unneeded navmesh polys "outside" the dungeon if these are left in
            if (CellGeometry.ShouldIgnoreEnvironment(environment.Id))
                return;

            var cellStruct = environment.Cells[(uint)cellStructure];
            foreach (var poly in cellStruct.PhysicsPolygons.Values)
                AddPolygon(poly.VertexIds.Select(v => cellStruct.VertexArray.Vertices[(ushort)v].Origin).ToList(), frame);
        }

        /// <summary>
        /// Adds the floor of a door apron (see DoorApron) for each doorway in a wall.
        /// </summary>
        public void AddDoorAprons(IEnumerable<CellGroupDoor> doors, AgentSize agentSize)
        {
            foreach (var door in doors)
            {
                if (door.IsInWall)
                    AddPolygon(DoorApron.GetFloor(door, agentSize));
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

        /// <summary>
        /// Adds a polygon, posed by the frame if there is one, with its winding reversed. While a static object's
        /// polygons are being collected (see _capture) it goes there instead of into the mesh arrays.
        /// </summary>
        private void AddPolygon(List<Vector3> vertices, Frame frame = null)
        {
            var points = new List<Vector3>(vertices.Count);
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[vertices.Count - 1 - i];
                if (frame != null)
                    vertex = frame.LocalToGlobal(vertex);
                points.Add(vertex);
            }

            if (_capture != null)
                _capture.Add(points);
            else
                AddTransformedPolygon(points);
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
                var vertex = new Vector3(point.X, point.Z, point.Y);

                if (!_vertexLookup.TryGetValue(vertex, out var index))
                {
                    index = Vertices.Count;
                    Vertices.Add(vertex);
                    _vertexLookup.Add(vertex, index);
                }
                poly.Add(index);
            }
            Polygons.Add(poly);
        }

        private readonly Dictionary<Vector3, int> _vertexLookup = new Dictionary<Vector3, int>();

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
                for (var i = 2; i < poly.Count; i++)
                {
                    tris.Add(poly[0]);
                    tris.Add(poly[i - 1]);
                    tris.Add(poly[i]);
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

        /// <summary>
        /// The 2D bounds of a cell footprint's floors, in world coordinates.
        /// </summary>
        private class FootprintBounds
        {
            private bool hasData = false;

            public float MinX { get; set; }
            public float MaxX { get; set; }
            public float MinY { get; set; }
            public float MaxY { get; set; }

            public bool ContainsPoint2D(Vector3 loc)
            {
                return loc.X >= MinX && loc.X <= MaxX && loc.Y >= MinY && loc.Y <= MaxY;
            }

            public bool IntersectsRect(float minX, float minY, float maxX, float maxY)
            {
                return hasData && maxX >= MinX && minX <= MaxX && maxY >= MinY && minY <= MaxY;
            }

            public void Add(Vector3 pv)
            {
                if (!hasData || pv.X < MinX)
                    MinX = pv.X;
                if (!hasData || pv.Y < MinY)
                    MinY = pv.Y;

                if (!hasData || pv.X > MaxX)
                    MaxX = pv.X;
                if (!hasData || pv.Y > MaxY)
                    MaxY = pv.Y;

                hasData = true;
            }
        }

        private class CellFootprint
        {
            public FootprintBounds Bounds = new FootprintBounds();
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

            _clipMinX = origin.X - SurfaceMargin;
            _clipMinY = origin.Y - SurfaceMargin;
            _clipMaxX = origin.X + ACE.Entity.Position.BlockLength + SurfaceMargin;
            _clipMaxY = origin.Y + ACE.Entity.Position.BlockLength + SurfaceMargin;

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
            var (min, max) = GeometryHelpers.GetBounds(points);
            if (IntersectsClip(min.X, min.Y, max.X, max.Y))
                AddPolygon(points);
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
        /// An object whose collision is only spheres, none larger than this radius, is left out of the navmesh
        /// geometry: clutter such as bottles and books. One that small can't block a passage in the physics: on the
        /// floor a creature steps over it, and anywhere else its spheres slide past. But the mesh keeps a whole agent
        /// radius from it across the agent's height, which can cut a narrow way like a stair (e.g. landblock 0xCE95).
        /// All such objects in the dat are a single sphere.
        /// </summary>
        private const float SmallObstacleSphereRadius = 0.2f;

        /// <summary>
        /// The collision shape of a physics object as polygons, following the same rules the physics engine uses
        /// (PhysicsObj.FindObjCollisions): part BSP polygons when the object has a physics BSP, otherwise cylinder
        /// spheres, otherwise spheres. Posed and scaled as the object is, in the coordinates of its frame plus offset.
        /// Nothing for an ethereal object, nor for one of only small spheres (see SmallObstacleSphereRadius).
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
                    foreach (var poly in ShapeTriangles.CylSphere(cylSphere.LowPoint * objScale, cylSphere.Height * objScale, cylSphere.Radius * objScale))
                        yield return poly.Select(p => objFrame.LocalToGlobal(p) + offset).ToList();
                }
                yield break;
            }

            var spheres = obj.PartArray.GetSphere();
            if (spheres != null && spheres.Count > 0)
            {
                if (spheres.All(s => s.Radius * objScale <= SmallObstacleSphereRadius))
                    yield break;

                foreach (var sphere in spheres)
                {
                    foreach (var poly in ShapeTriangles.Sphere(sphere.Center * objScale, sphere.Radius * objScale))
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

                    var points = poly.VertexIds.Select(v => envCell.Position.LocalToGlobal(cellStruct.VertexArray.Vertices[(ushort)v].Origin) + origin).ToArray();

                    foreach (var p in points)
                        footprint.MaxZ = Math.Max(footprint.MaxZ, p.Z);

                    var normal = GeometryHelpers.GetPolygonNormal(points);
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
            var cellLength = (float)ACE.Entity.Position.BlockLength / sideCells;
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
                            AddPolygon(new List<Vector3>() { p00, p10, p11 });
                            AddPolygon(new List<Vector3>() { p00, p11, p01 });
                        }
                        else
                        {
                            AddPolygon(new List<Vector3>() { p00, p10, p01 });
                            AddPolygon(new List<Vector3>() { p11, p01, p10 });
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
                            AddPolygon(new List<Vector3>() { a, b, c });
                            AddPolygon(new List<Vector3>() { a, c, d });
                        }
                    }
                }
            }
        }

        #endregion
    }
}
