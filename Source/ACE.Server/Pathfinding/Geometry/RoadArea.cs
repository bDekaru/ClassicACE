using DotRecast.Recast;
using System.Collections.Generic;
using System.Numerics;

using ACE.Server.Physics.Common;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// The road of a terrain cell as convex volumes that mark its navmesh spans as the Road area. It mirrors
    /// Physics.Common.Landblock.OnRoad: the road bits of the cell's four corners decide which bands of the cell are road.
    /// Each band is one convex volume (the cell square cut by up to two half planes), so a corner shape such as an L is
    /// two overlapping volumes.
    /// </summary>
    public static class RoadArea
    {
        /// <summary>
        /// The terrain cell length and the road half width OnRoad uses, in metres.
        /// </summary>
        private const float CellLength = LandDefs.CellLength;
        private static readonly float RoadWidth = Landblock.RoadWidth;

        /// <summary>
        /// The mark covers spans this far above and below the cell's terrain, so a bridge deck or an object above it is
        /// not marked.
        /// </summary>
        private const float MarkHeight = 1.5f;

        private static readonly float RMin = RoadWidth;
        private static readonly float RMax = CellLength - RoadWidth;

        /// <summary>
        /// A half plane a * dx + b * dy &lt;= c, in the cell's local coordinates.
        /// </summary>
        private readonly record struct HalfPlane(float A, float B, float C);

        private static readonly HalfPlane XLow = new HalfPlane(1, 0, RMin);
        private static readonly HalfPlane YLow = new HalfPlane(0, 1, RMin);
        private static readonly HalfPlane XHigh = new HalfPlane(-1, 0, -RMax);
        private static readonly HalfPlane YHigh = new HalfPlane(0, -1, -RMax);

        /// <summary>
        /// The bands of a cell, per corner road bits (bit 3 the (x, y) corner, 2 (x, y+1), 1 (x+1, y), 0 (x+1, y+1)):
        /// each band is the intersection of its half planes, the road is their union. Same cases as OnRoad.
        /// </summary>
        private static readonly HalfPlane[][][] Bands =
        {
            /* 0000 */ new HalfPlane[][] { },
            /* 0001 */ new[] { new[] { new HalfPlane(-1, -1, RMin - 2 * CellLength) } },
            /* 0010 */ new[] { new[] { new HalfPlane(-1, 1, RMin - CellLength) } },
            /* 0011 */ new[] { new[] { XHigh } },
            /* 0100 */ new[] { new[] { new HalfPlane(1, -1, RMin - CellLength) } },
            /* 0101 */ new[] { new[] { YHigh } },
            /* 0110 */ new[] { new[] { new HalfPlane(1, 1, CellLength + RMin), new HalfPlane(-1, -1, RMin - CellLength) } },
            /* 0111 */ new[] { new[] { XHigh }, new[] { YHigh } },
            /* 1000 */ new[] { new[] { new HalfPlane(1, 1, RMin) } },
            /* 1001 */ new[] { new[] { new HalfPlane(1, -1, RMin), new HalfPlane(-1, 1, RMin) } },
            /* 1010 */ new[] { new[] { YLow } },
            /* 1011 */ new[] { new[] { XHigh }, new[] { YLow } },
            /* 1100 */ new[] { new[] { XLow } },
            /* 1101 */ new[] { new[] { XLow }, new[] { YHigh } },
            /* 1110 */ new[] { new[] { XLow }, new[] { YLow } },
            /* 1111 */ new[] { new HalfPlane[] { } }
        };

        /// <summary>
        /// The convex polygons (cell local coordinates, counter-clockwise) that make up the road of a cell, given whether
        /// each corner has a road bit.
        /// </summary>
        private static List<List<Vector2>> GetCellPolygons(bool r0, bool r1, bool r2, bool r3)
        {
            var index = (r0 ? 8 : 0) | (r1 ? 4 : 0) | (r2 ? 2 : 0) | (r3 ? 1 : 0);
            var polygons = new List<List<Vector2>>();

            foreach (var band in Bands[index])
            {
                var polygon = new List<Vector2>
                {
                    new Vector2(0, 0), new Vector2(CellLength, 0), new Vector2(CellLength, CellLength), new Vector2(0, CellLength)
                };

                foreach (var plane in band)
                    polygon = Clip(polygon, plane);

                if (polygon.Count >= 3)
                    polygons.Add(polygon);
            }

            return polygons;
        }

        /// <summary>
        /// The volumes marking the road of one cell, in Recast space (y up).
        /// </summary>
        /// <param name="cellOrigin">The cell's corner (x, y) in world coordinates</param>
        /// <param name="minZ">The lowest terrain height of the cell's corners</param>
        /// <param name="maxZ">The highest</param>
        public static IEnumerable<RcConvexVolume> GetVolumes(bool r0, bool r1, bool r2, bool r3, Vector2 cellOrigin, float minZ, float maxZ)
        {
            foreach (var polygon in GetCellPolygons(r0, r1, r2, r3))
            {
                var verts = new float[polygon.Count * 3];
                for (var i = 0; i < polygon.Count; i++)
                {
                    verts[i * 3] = cellOrigin.X + polygon[i].X;
                    verts[i * 3 + 1] = minZ;
                    verts[i * 3 + 2] = cellOrigin.Y + polygon[i].Y;
                }

                yield return new RcConvexVolume
                {
                    verts = verts,
                    hmin = minZ - MarkHeight,
                    hmax = maxZ + MarkHeight,
                    areaMod = NavArea.RoadMod
                };
            }
        }

        /// <summary>
        /// Keeps the part of the polygon on the plane's side (Sutherland-Hodgman).
        /// </summary>
        private static List<Vector2> Clip(List<Vector2> polygon, HalfPlane plane)
        {
            var result = new List<Vector2>();

            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                var da = plane.A * a.X + plane.B * a.Y - plane.C;
                var db = plane.A * b.X + plane.B * b.Y - plane.C;

                if (da <= 0)
                    result.Add(a);

                if (da < 0 != db < 0 && da != 0 && db != 0)
                    result.Add(a + (b - a) * (da / (da - db)));
            }

            return result;
        }
    }
}
