using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using DotRecast.Recast;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using AFrame = ACE.Server.Physics.Animation.AFrame;
using PhysicsObj = ACE.Server.Physics.PhysicsObj;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A placed door: its object guid, and the position and floor footprint of it closed, in its landblock's coordinates.
    /// </summary>
    public sealed record DoorPlacement(uint Guid, Vector3 Position, Vector3[] Footprint);

    /// <summary>
    /// The placed doors of a landblock as convex volumes that mark the navmesh spans under them as the Door area. The
    /// floor stays connected: whether a creature may pass a door is up to each route search, by the door's state then.
    /// Doors spawned by generators aren't covered.
    /// </summary>
    public static class DoorArea
    {
        /// <summary>
        /// The mark reaches this far past either end of the door, into the frame.
        /// </summary>
        private const float SideMargin = 0.3f;

        /// <summary>
        /// The mark reaches at least this far to either side of the door's plane: a thin door still marks a strip of
        /// spans that no route gets around.
        /// </summary>
        private const float MinHalfDepth = 0.3f;

        /// <summary>
        /// The mark covers spans this far above and below the door's origin, which is at its bottom.
        /// </summary>
        private const float MarkHeight = 0.5f;

        /// <summary>
        /// How far an outline has to reach into a door to count as covering it, so the next leaf's floor doesn't.
        /// </summary>
        private const float MinOverlap = 0.1f;

        private static readonly ConcurrentDictionary<uint, (Vector3 Min, Vector3 Max)?> ShapeBounds = new ConcurrentDictionary<uint, (Vector3 Min, Vector3 Max)?>();

        /// <summary>
        /// The doors placed in a landblock, in guid order (their volumes are part of a mesh's fingerprint).
        /// </summary>
        /// <param name="offset">Moves them into the coordinates of a mesh, e.g. world coordinates outdoors</param>
        public static List<DoorPlacement> GetDoors(uint landblockId, Vector3 offset = default)
        {
            var doors = new List<DoorPlacement>();

            foreach (var instance in DatabaseManager.World.GetCachedInstancesByLandblock((ushort)(landblockId >> 16)))
            {
                var weenie = DatabaseManager.World.GetCachedWeenie(instance.WeenieClassId);
                if (weenie?.WeenieType != WeenieType.Door)
                    continue;

                if (!(ShapeBounds.GetOrAdd(instance.WeenieClassId, _ => GetShapeBounds(weenie)) is (var min, var max)))
                    continue;

                var position = new Vector3(instance.OriginX, instance.OriginY, instance.OriginZ);
                var rotation = new Quaternion(instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);

                // a door is thin along its own Y
                min = new Vector3(min.X - SideMargin, System.MathF.Min(min.Y, -MinHalfDepth), 0f);
                max = new Vector3(max.X + SideMargin, System.MathF.Max(max.Y, MinHalfDepth), 0f);

                var footprint = new[] { min, new Vector3(max.X, min.Y, 0f), max, new Vector3(min.X, max.Y, 0f) };

                doors.Add(new DoorPlacement(instance.Guid, position + offset, footprint.Select(corner => position + offset + Vector3.Transform(corner, rotation)).ToArray()));
            }

            return doors.OrderBy(door => door.Guid).ToList();
        }

        /// <summary>
        /// The volume that marks the door's footprint (Recast space).
        /// </summary>
        public static RcConvexVolume GetVolume(DoorPlacement door)
        {
            var verts = new float[door.Footprint.Length * 3];
            for (var i = 0; i < door.Footprint.Length; i++)
            {
                var corner = door.Footprint[i];

                // Recast space: y up
                verts[i * 3] = corner.X;
                verts[i * 3 + 1] = corner.Z;
                verts[i * 3 + 2] = corner.Y;
            }

            return new RcConvexVolume
            {
                verts = verts,
                hmin = door.Position.Z - MarkHeight,
                hmax = door.Position.Z + MarkHeight,
                areaMod = NavArea.DoorMod
            };
        }

        /// <summary>
        /// True if a convex outline reaches into the door itself: its footprint without the side margins.
        /// </summary>
        public static bool Covers(DoorPlacement door, Vector2[] outline)
        {
            var corners = door.Footprint.Select(corner => new Vector2(corner.X, corner.Y)).ToArray();

            // corners 0 to 1 and 3 to 2 run along the door
            var margin = Vector2.Normalize(corners[1] - corners[0]) * SideMargin;
            var shape = new[] { corners[0] + margin, corners[1] - margin, corners[2] - margin, corners[3] + margin };

            return !HasSeparatingEdge(shape, shape, outline) && !HasSeparatingEdge(outline, shape, outline);
        }

        /// <summary>
        /// Which side of the door's plane a point is on.
        /// </summary>
        public static bool IsBehind(DoorPlacement door, Vector2 point)
        {
            var across = door.Footprint[3] - door.Footprint[0];
            return Vector2.Dot(point - new Vector2(door.Position.X, door.Position.Y), new Vector2(across.X, across.Y)) > 0f;
        }

        /// <summary>
        /// True if two convex outlines overlap by less than MinOverlap across one of the given edges.
        /// </summary>
        private static bool HasSeparatingEdge(Vector2[] edges, Vector2[] first, Vector2[] second)
        {
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[(i + 1) % edges.Length] - edges[i];
                if (edge.LengthSquared() < 0.000001f)
                    continue;

                var axis = Vector2.Normalize(new Vector2(-edge.Y, edge.X));
                var overlap = System.MathF.Min(first.Max(v => Vector2.Dot(v, axis)), second.Max(v => Vector2.Dot(v, axis))) -
                    System.MathF.Max(first.Min(v => Vector2.Dot(v, axis)), second.Min(v => Vector2.Dot(v, axis)));

                if (overlap < MinOverlap)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The bounds of a door weenie's closed collision shape, in the door's own frame. Null if it has none.
        /// </summary>
        private static (Vector3 Min, Vector3 Max)? GetShapeBounds(Weenie weenie)
        {
            var obj = PhysicsObj.makeObject(weenie.GetProperty(PropertyDataId.Setup) ?? 0, 0, false);
            if (obj?.PartArray == null)
                return null;

            obj.SetScaleStatic((float)(weenie.GetProperty(PropertyFloat.DefaultScale) ?? 1.0));
            obj.set_initial_frame(new AFrame());

            var points = MeshGeometryCollector.GetCollisionPolygons(obj, Vector3.Zero, false).SelectMany(polygon => polygon).ToList();
            if (points.Count == 0)
                return null;

            return GeometryHelpers.GetBounds(points);
        }
    }
}
