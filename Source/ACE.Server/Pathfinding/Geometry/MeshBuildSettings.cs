using DotRecast.Recast;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// The Recast settings a navmesh is built with (see NavMeshBuilder), for one agent size. Pathfinder.GetMeshSettings
    /// picks them per mesh, and they are part of the mesh cache key (Pathfinder.GetMeshCacheKey), so a mesh built with
    /// other settings is rebuilt.
    /// </summary>
    public class MeshBuildSettings
    {
        /// <summary>
        /// The size of a voxel, horizontally and vertically.
        /// </summary>
        public float CellSize;
        public float CellHeight;

        /// <summary>
        /// The agent the mesh is for: the clearance it needs overhead, how far the walkable area keeps from walls, the
        /// highest step it climbs and the steepest slope (degrees) it walks.
        /// </summary>
        public float AgentHeight;
        public float AgentRadius;
        public float AgentMaxClimb;
        public float AgentMaxSlope;

        /// <summary>
        /// Regions smaller than MinRegionSize (the side of a square, in cells) are removed; ones smaller than
        /// MergedRegionSize are merged into their neighbors.
        /// </summary>
        public int MinRegionSize;
        public int MergedRegionSize;

        public RcPartition Partitioning = RcPartition.WATERSHED;

        /// <summary>
        /// Recast's filters: walk up onto low obstacles, drop the walkable spans along ledges, and drop the spans with
        /// less than AgentHeight overhead.
        /// </summary>
        public bool FilterLowHangingObstacles = true;
        public bool FilterLedgeSpans = true;
        public bool FilterWalkableLowHeightSpans = true;

        /// <summary>
        /// How long a poly edge along the mesh boundary may get, and how far it may stray from the walkable area's
        /// contour.
        /// </summary>
        public float EdgeMaxLen;
        public float EdgeMaxError;

        public int VertsPerPoly;

        /// <summary>
        /// The detail mesh, which follows the floor's height: its sample spacing (in cells) and how far it may stray.
        /// </summary>
        public float DetailSampleDist;
        public float DetailSampleMaxError;
    }
}
