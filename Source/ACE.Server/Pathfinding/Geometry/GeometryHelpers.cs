using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    public static class GeometryHelpers
    {
        /// <summary>
        /// The world position of a landblock's south west corner: its landblock-local origin in world coordinates,
        /// where landblock (x, y) spans [x * 192, (x + 1) * 192) and the same along y.
        /// </summary>
        /// <param name="cellId">Any cell id of the landblock, or the landblock id (0xAAAA0000)</param>
        public static Vector3 GetLandblockOrigin(uint cellId)
        {
            var blockLength = ACE.Server.Physics.Common.LandDefs.BlockLength;
            return new Vector3(((cellId >> 24) & 0xFF) * blockLength, ((cellId >> 16) & 0xFF) * blockLength, 0f);
        }

        public static Vector3 FindCentroid(params Vector3[] points)
        {
            return FindCentroid(points);
        }

        public static Vector3 FindCentroid(IEnumerable<Vector3> points)
        {
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
        public static Vector3 CalculateTriSurfaceNormal(Vector3 a, Vector3 b, Vector3 c)
        {
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
