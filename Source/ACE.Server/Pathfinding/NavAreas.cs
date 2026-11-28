using DotRecast.Recast;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The area ids of navmesh polys. Areas say what a poly is; each route filter prices them (see NavPolyFlags for
    /// what can be walked). The values are saved in the cached mesh files, so changing one means cached meshes must be
    /// rebuilt (bump a geometry version in Pathfinder).
    /// </summary>
    public static class NavArea
    {
        /// <summary>
        /// Floor.
        /// </summary>
        public const int Ground = 0;

        /// <summary>
        /// Road, outdoors (see RoadArea). Walkable like Ground; a route filter can price it differently.
        /// </summary>
        public const int Road = 1;

        /// <summary>
        /// An opening apron (see OpeningApron), outside an opening.
        /// </summary>
        public const int Opening = 3;

        /// <summary>
        /// The floor under a placed door (see DoorArea). Walkable; a route search judges each door by its state.
        /// </summary>
        public const int Door = 4;

        /// <summary>
        /// A ledge drop (see LedgeDropFinder): a one-way off-mesh connection down a ledge.
        /// </summary>
        public const int LedgeDrop = 5;

        /// <summary>
        /// Narrow floor, like the top of a wall or a fence (see ObstacleErosion). Walkable, but a route filter prices it
        /// high, so it is only taken to reach it or when the way around is much longer.
        /// </summary>
        public const int Narrow = 6;

        /// <summary>
        /// The area Recast gives every walkable span (RcRecast.RC_WALKABLE_AREA). The mesh build turns it into Ground.
        /// </summary>
        public const int Walkable = RcRecast.RC_WALKABLE_AREA;

        /// <summary>
        /// Marks the walkable spans of the geometry as Walkable.
        /// </summary>
        public static readonly RcAreaModification WalkableMod = new RcAreaModification(Walkable, RcAreaModification.RC_AREA_FLAGS_MASK);

        /// <summary>
        /// Marks the spans inside a road volume as Road.
        /// </summary>
        public static readonly RcAreaModification RoadMod = new RcAreaModification(Road, RcAreaModification.RC_AREA_FLAGS_MASK);

        /// <summary>
        /// Marks the spans inside an opening apron's volume as Opening.
        /// </summary>
        public static readonly RcAreaModification OpeningMod = new RcAreaModification(Opening, RcAreaModification.RC_AREA_FLAGS_MASK);

        /// <summary>
        /// Marks the spans inside a door's volume as Door.
        /// </summary>
        public static readonly RcAreaModification DoorMod = new RcAreaModification(Door, RcAreaModification.RC_AREA_FLAGS_MASK);
    }

    /// <summary>
    /// How a route search prices the areas (see Pathfinder.FindRoute).
    /// </summary>
    public enum RouteProfile
    {
        /// <summary>
        /// Every area costs its length (ledge drops and narrow floor more): the shortest way.
        /// </summary>
        Default,

        /// <summary>
        /// The road is preferred over the ground: a way that keeps to the road unless it is much longer.
        /// </summary>
        Road
    }

    /// <summary>
    /// The flags of navmesh polys, which query filters include or exclude. Walk, Opening, LedgeDrop, Narrow and the drop
    /// edges are saved in the cached mesh files; Island is set when a mesh is loaded.
    /// </summary>
    public static class NavPolyFlags
    {
        /// <summary>
        /// Floor (Ground, Road, Narrow and Door areas).
        /// </summary>
        public const int Walk = 0x01;

        /// <summary>
        /// An opening apron (Opening area). Opening aprons are not flagged Walk.
        /// </summary>
        public const int Opening = 0x04;

        /// <summary>
        /// A ledge drop (LedgeDrop area).
        /// </summary>
        public const int LedgeDrop = 0x08;

        /// <summary>
        /// Narrow floor (Narrow area), also flagged Walk.
        /// </summary>
        public const int Narrow = 0x10;

        /// <summary>
        /// Islands: parts of a mesh that cannot be walked to from its main area or its openings, like the top of a
        /// table, a crate or a large rock. Set when a mesh is loaded, not saved in the mesh files (see
        /// Pathfinder.MarkIslands).
        /// </summary>
        public const int Island = 0x0100;

        /// <summary>
        /// A poly's boundary edge is a drop, not a wall: this shifted left by the edge's index (see
        /// LedgeDropFinder.MarkDropEdges). Polys have up to 6 edges, so 0x0200 to 0x4000.
        /// </summary>
        public const int DropEdge = 0x0200;
    }
}
