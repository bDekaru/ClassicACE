using DotRecast.Recast;
using System.Collections.Generic;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A walkable slab added to an interior mesh's geometry just outside each opening (a portal to the outdoors in a
    /// wall), level with the threshold and marked as an opening area. Recast then decides, with the same rules as for
    /// the rest of the mesh, whether an agent can get through the opening: the slab is only connected to the room if the
    /// opening is tall enough (agentHeight), its threshold is within reach of the floor (agentMaxClimb) and it is wide
    /// enough (the agent radius). An apron that is not connected ends up removed or as an island, so the opening is not
    /// usable on that mesh: a window, or a doorway too small for the agent. The result is part of the mesh data and is
    /// cached with it, per agent size. Aprons are sized for the agent of the mesh they're built into. They are test
    /// geometry, and a bigger one would reach further over its neighbors: an apron hanging over another opening (a
    /// window next to a doorway) takes that opening's headroom.
    /// </summary>
    public static class OpeningApron
    {
        /// <summary>
        /// How far past the agent radius the apron reaches out of the opening. The mesh runs to its outer edge, or ends
        /// the radius short of it where the agent steps down to a floor below (see ObstacleErosion): this much of it is
        /// left then.
        /// </summary>
        private const float DepthMargin = 0.4f;

        /// <summary>
        /// How far the apron reaches past either side of the opening, along the wall. The opening itself limits the
        /// width of the passage.
        /// </summary>
        public const float SideMargin = 0.3f;

        /// <summary>
        /// The apron starts this far inside the opening, so it meets the floor without a gap in the voxels.
        /// </summary>
        private const float Overlap = 0.05f;

        /// <summary>
        /// The opening area mark starts this far outside the opening, so the floor inside it is not marked.
        /// </summary>
        private const float MarkStart = 0.05f;

        /// <summary>
        /// The opening area mark covers spans this far above and below the threshold.
        /// </summary>
        private const float MarkHeight = 0.5f;

        /// <summary>
        /// How far the apron of a mesh for the given agent size reaches out of the opening.
        /// </summary>
        public static float GetDepth(AgentSize agentSize)
        {
            return Pathfinder.GetAgentRadius(agentSize) + DepthMargin;
        }

        /// <summary>
        /// The center of the apron's opening area, in landblock coordinates: where its opening polys are looked for.
        /// </summary>
        public static Vector3 GetCenter(CellGroupOpening opening, AgentSize agentSize)
        {
            return opening.Position + opening.Normal * ((MarkStart + GetDepth(agentSize)) * 0.5f);
        }

        /// <summary>
        /// The apron's floor quad in landblock coordinates, counter-clockwise seen from above (walkable side up).
        /// </summary>
        public static List<Vector3> GetFloor(CellGroupOpening opening, AgentSize agentSize)
        {
            return GetQuad(opening, -Overlap, GetDepth(agentSize));
        }

        /// <summary>
        /// The convex volume that marks the apron as an opening area (Recast space).
        /// </summary>
        public static RcConvexVolume GetOpeningVolume(CellGroupOpening opening, AgentSize agentSize)
        {
            var quad = GetQuad(opening, MarkStart, GetDepth(agentSize));

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
                hmin = opening.Position.Z - MarkHeight,
                hmax = opening.Position.Z + MarkHeight,
                areaMod = NavArea.OpeningMod
            };
        }

        private static List<Vector3> GetQuad(CellGroupOpening opening, float from, float to)
        {
            var along = new Vector3(-opening.Normal.Y, opening.Normal.X, 0f);
            var halfWidth = opening.Width * 0.5f + SideMargin;

            var p0 = opening.Position - along * halfWidth + opening.Normal * from;
            var p1 = opening.Position + along * halfWidth + opening.Normal * from;

            // with along = normal rotated left, this order is counter-clockwise seen from above
            return new List<Vector3>
            {
                p0,
                p0 + opening.Normal * (to - from),
                p1 + opening.Normal * (to - from),
                p1
            };
        }
    }
}
