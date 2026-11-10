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
    /// The parts of a physics landblock the outdoor mesh is built from, with its object lists copied. The landblock
    /// owns and may change those lists, while the mesh builds on a background thread.
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
            var frames = new List<Frame>();

            foreach (var cell in Neighbors)
            {
                var tempFrames = frames.ToList();
                tempFrames.Add(cell.EnvCell.Position);

                LoadEnvironment(cell.EnvCell.EnvironmentId, tempFrames, cell.EnvCell.CellStructure);

                if (cell.EnvCell.StaticObjects != null && cell.EnvCell.StaticObjects.Count > 0)
                {
                    foreach (var staticObject in cell.EnvCell.StaticObjects)
                    {
                        var sFrames = frames.ToList();
                        sFrames.Add(staticObject.Frame);
                        BoundingBox2 bb = new BoundingBox2();
                        LoadSetupOrGfxObj(staticObject.Id, sFrames, ref bb);
                    }
                }
            }
        }

        private void LoadSetupOrGfxObj(uint id, List<Frame> sFrames, ref BoundingBox2 boundingBox, bool coneTop = false)
        {
            if ((id & 0x02000000) != 0)
            {
                LoadSetup(id, sFrames, ref boundingBox, coneTop);
            }
            else
            {
                LoadGfxObj(id, sFrames, ref boundingBox);
            }
        }

        private void LoadGfxObj(uint cellFileIndex, List<Frame> frames, ref BoundingBox2 boundingBox)
        {
            var gfxObj = DatManager.PortalDat.ReadFromDat<GfxObj>(cellFileIndex);
            if (gfxObj == null)
                return;
            foreach (var pkv in gfxObj.PhysicsPolygons)
            {
                var vertices = pkv.Value.VertexIds.Select(v => gfxObj.VertexArray.Vertices[(ushort)v].Origin).ToList();
                var pvertices = AddPolygon(vertices, frames);
                foreach (var pv in pvertices)
                {
                    boundingBox.Add(pv);
                }
            }
        }

        private void LoadSetup(uint cellFileIndex, List<Frame> frames, ref BoundingBox2 boundingBox, bool coneTop = false)
        {
            //Program.Log($"Loading setup: {cellFileIndex:X8}");  
            var setupModel = DatManager.PortalDat.ReadFromDat<SetupModel>(cellFileIndex);

            // physics draw priority is child part physics polys, then cylspheres, then spheres?
            var hasPhysicsPolys = false;
            foreach (var partId in setupModel.Parts)
            {
                var gfxObj = DatManager.PortalDat.ReadFromDat<GfxObj>(partId);
                if (gfxObj.PhysicsPolygons != null && gfxObj.PhysicsPolygons.Count > 0)
                {
                    hasPhysicsPolys = true;
                    break;
                }
            }

            if (setupModel.CylSpheres != null && setupModel.CylSpheres.Count > 0)
            {
                foreach (var cSphere in setupModel.CylSpheres)
                {
                    List<List<Vector3>> polys = GetCylSpherePolygons(cSphere.Origin, cSphere.Height, cSphere.Radius, coneTop);
                    foreach (var poly in polys)
                    {
                        var pvs = AddPolygon(poly, frames);
                        foreach (var pv in pvs)
                            boundingBox.Add(pv);
                    }
                }
            }

            if (!hasPhysicsPolys && setupModel.Spheres != null && setupModel.Spheres.Count > 0)
            {
                foreach (var sphere in setupModel.Spheres)
                {
                    List<List<Vector3>> polys = GetSpherePolygons(sphere.Origin, sphere.Radius);
                    foreach (var poly in polys)
                    {
                        var pvs = AddPolygon(poly, frames);
                        foreach (var pv in pvs)
                            boundingBox.Add(pv);
                    }
                }
            }

            // draw all the child parts
            for (var i = 0; i < setupModel.Parts.Count; i++)
            {
                // always use PlacementFrames[0] ?
                var tempFrames = frames.ToList(); // clone
                tempFrames.Insert(0, setupModel.PlacementFrames[0].AnimFrame.Frames[i]);
                LoadGfxObj(setupModel.Parts[i], tempFrames, ref boundingBox);
            }
        }

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

        public List<Vector3> AddPolygon(List<Vector3> vertices, List<Frame> frames)
        {
            var poly = new List<int>();
            var retvertices = new List<Vector3>();
            for (var i = 0; i < vertices.Count; i++)
            {
                var vertice = vertices[vertices.Count - 1 - i];
                foreach (var frame in frames)
                {
                    vertice = Transform(vertice, frame.Orientation);
                    vertice += frame.Origin;
                }
                retvertices.Add(vertice);

                vertice = new Vector3()
                {
                    X = vertice.X,
                    Y = vertice.Z,
                    Z = vertice.Y
                };

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
            return retvertices;
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

        private class CellFootprint
        {
            public BoundingBox2 Bounds = new BoundingBox2();
            public List<Vector3[]> Floors = new List<Vector3[]>();
            public List<Vector3> FloorNormals = new List<Vector3>();
            public float MaxZ = float.MinValue;
        }

        /// <summary>
        /// Exports the outdoors of a landblock: terrain, building exterior shells, static objects and scenery.
        /// Interior cells are not part of it; each connected group of interior cells gets its own mesh, so spaces that
        /// overlap without being connected never end up in the same heightfield. The interior cells in Neighbors are
        /// only used to cut the terrain out of building interiors. Everything is in landblock-local coordinates.
        /// </summary>
        public void LoadSurface(SurfaceSnapshot surface)
        {
            var landblockId = surface.Landblock.ID;

            foreach (var building in surface.Buildings)
                AddPhysicsObj(building, landblockId, true);

            foreach (var obj in surface.StaticObjects)
                AddPhysicsObj(obj, landblockId, false);

            foreach (var obj in surface.Scenery)
                AddPhysicsObj(obj, landblockId, false);

            LoadTerrain(surface.Landblock, BuildFootprints());
        }

        /// <summary>
        /// Adds the collision shape of a physics object, following the same rules the physics engine uses:
        /// part BSP polygons when the object has a physics BSP, otherwise cylinder spheres, otherwise spheres.
        /// </summary>
        private void AddPhysicsObj(ACE.Server.Physics.PhysicsObj obj, uint landblockId, bool isBuilding)
        {
            if (obj?.PartArray == null || obj.Position?.Frame == null)
                return;

            if (obj.State.HasFlag(ACE.Entity.Enum.PhysicsState.Ethereal))
                return;

            var offset = GetBlockOffset(obj.Position.ObjCellID, landblockId);

            if (isBuilding || obj.State.HasFlag(ACE.Entity.Enum.PhysicsState.HasPhysicsBSP))
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
                    {
                        var points = poly.VertexIds.Select(v => frame.LocalToGlobal(gfxObj.VertexArray.Vertices[(ushort)v].Origin * scale) + offset).ToList();
                        AddPolygon(points, NoFrames);
                    }
                }
                return;
            }

            var objFrame = obj.Position.Frame;
            var objScale = obj.Scale;

            var cylSpheres = obj.PartArray.GetCylSphere();
            if (cylSpheres != null && cylSpheres.Count > 0)
            {
                foreach (var cylSphere in cylSpheres)
                {
                    foreach (var poly in GetCylSpherePolygons(cylSphere.LowPoint * objScale, cylSphere.Height * objScale, cylSphere.Radius * objScale))
                        AddPolygon(poly.Select(p => objFrame.LocalToGlobal(p) + offset).ToList(), NoFrames);
                }
                return;
            }

            var spheres = obj.PartArray.GetSphere();
            if (spheres != null && spheres.Count > 0)
            {
                foreach (var sphere in spheres)
                {
                    foreach (var poly in GetSpherePolygons(sphere.Center * objScale, sphere.Radius * objScale))
                        AddPolygon(poly.Select(p => objFrame.LocalToGlobal(p) + offset).ToList(), NoFrames);
                }
            }
        }

        /// <summary>
        /// Objects are occasionally re-parented to a neighboring landblock (AdjustToOutside), which puts their frame in
        /// that landblock's coordinates. This returns the offset to bring them back into our landblock's frame.
        /// </summary>
        private static Vector3 GetBlockOffset(uint objCellId, uint landblockId)
        {
            if (objCellId == 0)
                return Vector3.Zero;

            var dx = (int)((objCellId >> 24) & 0xFF) - (int)((landblockId >> 24) & 0xFF);
            var dy = (int)((objCellId >> 16) & 0xFF) - (int)((landblockId >> 16) & 0xFF);

            return new Vector3(dx * ACE.Server.Physics.Common.LandDefs.BlockLength, dy * ACE.Server.Physics.Common.LandDefs.BlockLength, 0);
        }

        /// <summary>
        /// Collects the floor polygons and height range of every interior cell, in landblock coordinates.
        /// Used to cut the terrain out of building interiors.
        /// </summary>
        private List<CellFootprint> BuildFootprints()
        {
            var footprints = new List<CellFootprint>();
            var floorZ = ACE.Server.Physics.PhysicsGlobals.FloorZ;

            foreach (var cell in Neighbors)
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

                    var points = poly.VertexIds.Select(v => Transform(cellStruct.VertexArray.Vertices[(ushort)v].Origin, envCell.Position.Orientation) + envCell.Position.Origin).ToArray();

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

                if (footprint.Floors.Count > 0)
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
        /// Adds the terrain, triangulated exactly like the physics landblock. Cells overlapping an interior are
        /// subdivided and any sub-quad entirely inside an interior is dropped, so terrain doesn't poke through floors.
        /// </summary>
        private void LoadTerrain(ACE.Server.Physics.Common.Landblock landblock, List<CellFootprint> footprints)
        {
            var vertices = landblock.VertexArray.Vertices;
            var sideCells = landblock.SideCellCount;
            var sideVerts = landblock.SideVertexCount;
            var cellLength = (float)ACE.Server.Physics.Common.LandDefs.BlockLength / sideCells;

            for (var x = 0; x < sideCells; x++)
            {
                for (var y = 0; y < sideCells; y++)
                {
                    var z00 = vertices[x * sideVerts + y].Origin.Z;
                    var z10 = vertices[(x + 1) * sideVerts + y].Origin.Z;
                    var z01 = vertices[x * sideVerts + y + 1].Origin.Z;
                    var z11 = vertices[(x + 1) * sideVerts + y + 1].Origin.Z;
                    var swToNeCut = landblock.SWtoNEcut[x * sideCells + y];

                    var x0 = x * cellLength;
                    var y0 = y * cellLength;

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

        public static List<List<Vector3>> GetCylSpherePolygons(Vector3 origin, float height, float radius, bool coneTop = false)
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
            if (coneTop && axis.Z > 20)
                p0 += new Vector3(0, 0, 20);

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

        public static List<List<Vector3>> GetSpherePolygons(Vector3 origin, float radius)
        {
            var results = new List<List<Vector3>>();
            var vectors = new List<Vector3>();
            var indices = new List<int>();

            Icosahedron(vectors, indices);

            for (var i = 0; i < 1; i++)
                Subdivide(vectors, indices, true);

            /// normalize vectors to "inflate" the icosahedron into a sphere.
            for (var i = 0; i < indices.Count / 3; i++)
            {
                var testIndicies = new List<int>();
                for (var x = 2; x >= 0; x--)
                {
                    var ii = i * 3 + x;
                    vectors[indices[ii]] = Vector3.Normalize(vectors[indices[ii]]) * radius;
                    testIndicies.Add(indices[ii]);
                }

                // adjust height.. 
                var h = new Vector3(0, 0, radius);
                results.Add(new List<Vector3>() { vectors[testIndicies[0]] + h, vectors[testIndicies[1]] + h, vectors[testIndicies[2]] + h });
            }

            return results;
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
