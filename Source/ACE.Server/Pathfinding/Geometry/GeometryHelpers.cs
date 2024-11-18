using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    public static class GeometryHelpers {
        public static Vector3 FindCentroid(params Vector3[] points) {
            return FindCentroid(points);
        }

        public static Vector3 FindCentroid(IEnumerable<Vector3> points) {
            var x = points.Select(p => p.X).Sum();
            var y = points.Select(p => p.Y).Sum();
            var z = points.Select(p => p.Z).Sum();

            return new Vector3(x, y, z) / points.Count();
        }

        /// <summary>
        /// Calculate the surface normal of a triangle
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static Vector3 CalculateTriSurfaceNormal(Vector3 a, Vector3 b, Vector3 c) {
            Vector3 normal = new Vector3();
            var u = b - a;
            var v = c - a;
            normal.X = u.Y * v.Z - u.Z * v.Y;
            normal.Y = u.Z * v.X - u.X * v.Z;
            normal.Z = u.X * v.Y - u.Y * v.X;


            return Vector3.Normalize(normal);
        }
    }
}
