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
        /// A door apron (see DoorApron), outside a doorway.
        /// </summary>
        public const int Door = 3;

        /// <summary>
        /// A ledge drop (see LedgeDropFinder): a one-way off-mesh connection down a ledge.
        /// </summary>
        public const int LedgeDrop = 5;

        /// <summary>
        /// The area Recast gives every walkable span (RcRecast.RC_WALKABLE_AREA). The mesh build turns it into Ground.
        /// </summary>
        public const int Walkable = RcRecast.RC_WALKABLE_AREA;

        /// <summary>
        /// Marks the walkable spans of the geometry as Walkable.
        /// </summary>
        public static readonly RcAreaModification WalkableMod = new RcAreaModification(Walkable, RcAreaModification.RC_AREA_FLAGS_MASK);

        /// <summary>
        /// Marks the spans inside a door apron's volume as Door.
        /// </summary>
        public static readonly RcAreaModification DoorMod = new RcAreaModification(Door, RcAreaModification.RC_AREA_FLAGS_MASK);
    }

    /// <summary>
    /// The flags of navmesh polys, which query filters include or exclude. Walk, Door and LedgeDrop are saved in the cached
    /// mesh files; Island is set when a mesh is loaded.
    /// </summary>
    public static class NavPolyFlags
    {
        /// <summary>
        /// Floor (Ground area).
        /// </summary>
        public const int Walk = 0x01;

        /// <summary>
        /// A door apron (Door area). Door aprons are not flagged Walk.
        /// </summary>
        public const int Door = 0x04;

        /// <summary>
        /// A ledge drop (LedgeDrop area).
        /// </summary>
        public const int LedgeDrop = 0x08;

        /// <summary>
        /// Islands: parts of a mesh that cannot be walked to from its main area or its doorways, like the top of a
        /// table, a crate or a large rock. Set when a mesh is loaded, not saved in the mesh files (see
        /// Pathfinder.MarkIslands).
        /// </summary>
        public const int Island = 0x0100;
    }
}
