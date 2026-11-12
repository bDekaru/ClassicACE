using System.Collections.Generic;
using System.Numerics;
using ACE.DatLoader.Entity;

namespace ACE.Server.Pathfinding.Geometry
{
    public static class GeometryHelpers
    {
        /// <summary>
        /// A point local to a dat frame (e.g. a cell's or a static's) in the frame's parent coordinates, as the physics
        /// engine places it (see Physics.Animation.AFrame.LocalToGlobal).
        /// </summary>
        public static Vector3 LocalToGlobal(this Frame frame, Vector3 local)
        {
            return Vector3.Transform(local, frame.Orientation) + frame.Origin;
        }

        /// <summary>
        /// The world position of a landblock's south west corner: its landblock-local origin in world coordinates,
        /// where landblock (x, y) spans [x * 192, (x + 1) * 192) and the same along y.
        /// </summary>
        /// <param name="cellId">Any cell id of the landblock, or the landblock id (0xAAAA0000)</param>
        public static Vector3 GetLandblockOrigin(uint cellId)
        {
            return new Vector3(((cellId >> 24) & 0xFF) * ACE.Entity.Position.BlockLength, ((cellId >> 16) & 0xFF) * ACE.Entity.Position.BlockLength, 0f);
        }

        /// <summary>
        /// The axis-aligned bounds of a set of points.
        /// </summary>
        public static (Vector3 Min, Vector3 Max) GetBounds(IEnumerable<Vector3> points)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            return (min, max);
        }

        /// <summary>
        /// The middle of a cell (the average of its structure's vertices) in the coordinates the cell's frame places it
        /// in, e.g. to tell which side of a portal is outside the cell.
        /// </summary>
        public static Vector3 GetCellCenter(CellStruct cellStruct, Frame frame)
        {
            var sum = Vector3.Zero;
            foreach (var vertex in cellStruct.VertexArray.Vertices.Values)
                sum += vertex.Origin;

            return frame.LocalToGlobal(sum / System.Math.Max(1, cellStruct.VertexArray.Vertices.Count));
        }

        /// <summary>
        /// The unit normal of a planar polygon by Newell's method, robust for any vertex count and for concave polygons.
        /// Zero for a polygon without area.
        /// </summary>
        public static Vector3 GetPolygonNormal(IReadOnlyList<Vector3> points)
        {
            var normal = Vector3.Zero;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var a = points[j];
                var b = points[i];
                normal.X += (a.Y - b.Y) * (a.Z + b.Z);
                normal.Y += (a.Z - b.Z) * (a.X + b.X);
                normal.Z += (a.X - b.X) * (a.Y + b.Y);
            }

            var length = normal.Length();
            return length > 1e-6f ? normal / length : Vector3.Zero;
        }
    }
}
