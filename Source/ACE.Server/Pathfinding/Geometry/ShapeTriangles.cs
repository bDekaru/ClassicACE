using System;
using System.Collections.Generic;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Physics collision shapes as triangles for the navmesh geometry (see MeshGeometryCollector.GetCollisionPolygons):
    /// a physics object's cylinder spheres and spheres. Recast takes what is walkable from each triangle's winding (a
    /// cap facing up can be stood on), so the windings here are the ones the cached meshes were built with.
    /// </summary>
    internal static class ShapeTriangles
    {
        /// <summary>
        /// The number of sides of a cylinder's rings.
        /// </summary>
        private const int CylinderSides = 12;

        /// <summary>
        /// The triangles of an upright cylinder (a cylinder sphere's collision shape) standing on lowPoint: a cap at the
        /// bottom and at the top, and two triangles per side.
        /// </summary>
        public static List<List<Vector3>> CylSphere(Vector3 lowPoint, float height, float radius)
        {
            var axis = new Vector3(0f, 0f, height);
            var top = lowPoint + axis;

            // Two horizontal directions at right angles, radius long, that the rings go round: the rings start between
            // them, at 45 degrees, as they always have. A cylinder of no height has none.
            var across = Math.Abs(height) > 0.01 ? new Vector3(height, height, 0f) : new Vector3(-height, 0f, 0f);
            var side = Vector3.Cross(across, axis);
            across *= radius / across.Length();
            side *= radius / side.Length();

            var lowRing = new Vector3[CylinderSides];
            var topRing = new Vector3[CylinderSides];

            double angle = 0;
            var step = 2 * Math.PI / CylinderSides;
            for (var i = 0; i < CylinderSides; i++)
            {
                var cos = (float)Math.Cos(angle);
                var sin = (float)Math.Sin(angle);
                lowRing[i] = lowPoint + cos * across + sin * side;
                topRing[i] = top + cos * across + sin * side;
                angle += step;
            }

            var triangles = new List<List<Vector3>>(CylinderSides * 4);

            for (var i = 0; i < CylinderSides; i++)
                triangles.Add(new List<Vector3> { lowPoint, lowRing[Previous(i)], lowRing[i] });

            for (var i = 0; i < CylinderSides; i++)
                triangles.Add(new List<Vector3> { top, topRing[i], topRing[Previous(i)] });

            // the sides run from the low ring straight up
            for (var i = 0; i < CylinderSides; i++)
            {
                var low = lowRing[i];
                var previousLow = lowRing[Previous(i)];
                triangles.Add(new List<Vector3> { previousLow, previousLow + axis, low + axis });
                triangles.Add(new List<Vector3> { previousLow, low + axis, low });
            }

            return triangles;
        }

        private static int Previous(int side) => (side + CylinderSides - 1) % CylinderSides;

        /// <summary>
        /// The triangles of a sphere (see UnitSphere).
        /// </summary>
        public static List<List<Vector3>> Sphere(Vector3 center, float radius)
        {
            var triangles = new List<List<Vector3>>(UnitSphere.Count);
            foreach (var triangle in UnitSphere)
                triangles.Add(new List<Vector3> { triangle[0] * radius + center, triangle[1] * radius + center, triangle[2] * radius + center });

            return triangles;
        }

        private static List<Vector3[]> CreateUnitSphere()
        {
            var vertices = new List<Vector3>(IcosahedronVertices);
            var indices = new List<int>(IcosahedronIndices);

            Subdivide(vertices, indices);

            // reversed, the winding the sphere triangles have always had
            var triangles = new List<Vector3[]>(indices.Count / 3);
            for (var i = 0; i + 2 < indices.Count; i += 3)
                triangles.Add(new[] { Vector3.Normalize(vertices[indices[i + 2]]), Vector3.Normalize(vertices[indices[i + 1]]), Vector3.Normalize(vertices[indices[i]]) });

            return triangles;
        }

        /// <summary>
        /// Splits each triangle in four at the midpoints of its edges; an edge's midpoint is shared by the two triangles
        /// on it.
        /// </summary>
        /// <remarks>
        ///      i0
        ///     /  \
        ///    m02-m01
        ///   /  \ /  \
        /// i2---m12---i1
        /// </remarks>
        private static void Subdivide(List<Vector3> vertices, List<int> indices)
        {
            var midpoints = new Dictionary<(int, int), int>();
            var subdivided = new List<int>(indices.Count * 4);

            for (var i = 0; i + 2 < indices.Count; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];

                var m01 = GetMidpoint(midpoints, vertices, i0, i1);
                var m12 = GetMidpoint(midpoints, vertices, i1, i2);
                var m02 = GetMidpoint(midpoints, vertices, i2, i0);

                subdivided.AddRange(new[]
                {
                    i0, m01, m02,
                    i1, m12, m01,
                    i2, m02, m12,
                    m02, m01, m12,
                });
            }

            indices.Clear();
            indices.AddRange(subdivided);
        }

        /// <summary>
        /// The index of the midpoint of an edge, added to the vertices the first time the edge is seen.
        /// </summary>
        private static int GetMidpoint(Dictionary<(int, int), int> midpoints, List<Vector3> vertices, int a, int b)
        {
            var edge = (Math.Min(a, b), Math.Max(a, b));
            if (!midpoints.TryGetValue(edge, out var index))
            {
                index = vertices.Count;
                vertices.Add((vertices[a] + vertices[b]) / 2f);
                midpoints.Add(edge, index);
            }
            return index;
        }

        private const float IcosahedronX = 0.525731112119133606f;
        private const float IcosahedronZ = 0.850650808352039932f;

        /// <summary>
        /// A regular icosahedron (20 triangles) with its vertices on the unit sphere.
        /// </summary>
        private static readonly Vector3[] IcosahedronVertices =
        {
            new Vector3(-IcosahedronX, 0f, IcosahedronZ),
            new Vector3(IcosahedronX, 0f, IcosahedronZ),
            new Vector3(-IcosahedronX, 0f, -IcosahedronZ),
            new Vector3(IcosahedronX, 0f, -IcosahedronZ),
            new Vector3(0f, IcosahedronZ, IcosahedronX),
            new Vector3(0f, IcosahedronZ, -IcosahedronX),
            new Vector3(0f, -IcosahedronZ, IcosahedronX),
            new Vector3(0f, -IcosahedronZ, -IcosahedronX),
            new Vector3(IcosahedronZ, IcosahedronX, 0f),
            new Vector3(-IcosahedronZ, IcosahedronX, 0f),
            new Vector3(IcosahedronZ, -IcosahedronX, 0f),
            new Vector3(-IcosahedronZ, -IcosahedronX, 0f),
        };

        private static readonly int[] IcosahedronIndices =
        {
            0, 4, 1,
            0, 9, 4,
            9, 5, 4,
            4, 5, 8,
            4, 8, 1,
            8, 10, 1,
            8, 3, 10,
            5, 3, 8,
            5, 2, 3,
            2, 7, 3,
            7, 10, 3,
            7, 6, 10,
            7, 11, 6,
            11, 0, 6,
            0, 1, 6,
            6, 1, 10,
            9, 0, 11,
            9, 11, 2,
            9, 2, 5,
            7, 2, 11,
        };

        /// <summary>
        /// A sphere of radius 1 around the origin: an icosahedron with each triangle split in four, its vertices pushed
        /// out onto the sphere (80 triangles). Built once; Sphere scales and places it. Declared after the icosahedron,
        /// as static fields are set in order.
        /// </summary>
        private static readonly List<Vector3[]> UnitSphere = CreateUnitSphere();
    }
}
