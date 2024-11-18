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
            if (environment.Id == 0x0D0000CA || environment.Id == 0x0D00016D)
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

                var index = Vertices.IndexOf(vertice);
                if (index != -1)
                {
                    // obj face indexes start at 1
                    poly.Add(index + 1);
                }
                else
                {
                    Vertices.Add(vertice);
                    poly.Add(Vertices.Count);
                }
            }
            Polygons.Add(poly);
            return retvertices;
        }

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
                    new[] {
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
