using DotRecast.Recast;
using System.Collections.Generic;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A walkable slab added to an interior mesh's geometry just outside each doorway (a portal to the outdoors in a
    /// wall), level with the threshold and marked as a door area. Recast then decides, with the same rules as for the
    /// rest of the mesh, whether an agent can get through the doorway: the slab is only connected to the room if the
    /// opening is tall enough (agentHeight), its threshold is within reach of the floor (agentMaxClimb) and it is wide
    /// enough (the agent radius). An apron that is not connected ends up removed or as an island, so the doorway is
    /// not usable on that mesh: a window, or a door too small for the agent. The result is part of the mesh data and
    /// is cached with it, per agent size.
    /// Aprons are sized for the agent of the mesh they're built into. They are test geometry, and a bigger one would
    /// reach further over its neighbors: an apron hanging over another doorway (a window next to a door) takes that
    /// doorway's headroom.
    /// </summary>
    public static class DoorApron
    {
        /// <summary>
        /// How far past the agent radius the apron reaches out of the doorway. Erosion takes the agent radius off its
        /// outer edge and the ledge filter another cell, so it must be deeper than the radius for a door poly to remain.
        /// </summary>
        private const float DepthMargin = 0.4f;

        /// <summary>
        /// How far the apron reaches past either side of the opening, along the wall. The opening itself limits the
        /// width of the passage; this keeps the ledge filter (a cell along the apron's open sides) from making the
        /// apron narrower than the opening. More than the largest interior cell size (0.125 m for Large, see AgentProfile).
        /// </summary>
        public const float SideMargin = 0.3f;

        /// <summary>
        /// The apron starts this far inside the doorway, so it meets the floor without a gap in the voxels.
        /// </summary>
        private const float Overlap = 0.05f;

        /// <summary>
        /// The door area mark starts this far outside the doorway, so the floor inside it is not marked.
        /// </summary>
        private const float MarkStart = 0.05f;

        /// <summary>
        /// The door area mark covers spans this far above and below the threshold.
        /// </summary>
        private const float MarkHeight = 0.5f;

        /// <summary>
        /// How far the apron of a mesh for the given agent size reaches out of the doorway.
        /// </summary>
        public static float GetDepth(AgentSize agentSize)
        {
            return 2f * Pathfinder.GetAgentRadius(agentSize) + DepthMargin;
        }

        /// <summary>
        /// The center of the apron's door area, in landblock coordinates: where its door polys are looked for.
        /// </summary>
        public static Vector3 GetCenter(CellGroupDoor door, AgentSize agentSize)
        {
            return door.Position + door.Normal * ((MarkStart + GetDepth(agentSize)) * 0.5f);
        }

        /// <summary>
        /// The apron's floor quad in landblock coordinates, counter-clockwise seen from above (walkable side up).
        /// </summary>
        public static List<Vector3> GetFloor(CellGroupDoor door, AgentSize agentSize)
        {
            return GetQuad(door, -Overlap, GetDepth(agentSize));
        }

        /// <summary>
        /// The convex volume that marks the apron as a door area (Recast space).
        /// </summary>
        public static RcConvexVolume GetDoorVolume(CellGroupDoor door, AgentSize agentSize)
        {
            var quad = GetQuad(door, MarkStart, GetDepth(agentSize));

            var verts = new float[quad.Count * 3];
            for (var i = 0; i < quad.Count; i++)
            {
                // Recast space: y up
                verts[i * 3] = quad[i].X;
                verts[i * 3 + 1] = quad[i].Z;
                verts[i * 3 + 2] = quad[i].Y;
            }

            return new RcConvexVolume
            {
                verts = verts,
                hmin = door.Position.Z - MarkHeight,
                hmax = door.Position.Z + MarkHeight,
                areaMod = NavArea.DoorMod
            };
        }

        private static List<Vector3> GetQuad(CellGroupDoor door, float from, float to)
        {
            var along = new Vector3(-door.Normal.Y, door.Normal.X, 0f);
            var halfWidth = door.Width * 0.5f + SideMargin;

            var p0 = door.Position - along * halfWidth + door.Normal * from;
            var p1 = door.Position + along * halfWidth + door.Normal * from;

            // with along = normal rotated left, this order is counter-clockwise seen from above
            return new List<Vector3>
            {
                p0,
                p0 + door.Normal * (to - from),
                p1 + door.Normal * (to - from),
                p1
            };
        }
    }
}
