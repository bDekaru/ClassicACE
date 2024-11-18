namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// A strategy for use when getting connected cells
    /// </summary>
    public enum ConnectionStrategy
    {
        /// <summary>
        /// All visible cells from the current.
        /// </summary>
        Visible,

        /// <summary>
        /// All visible cells from the current that don't overlap on the z axis.
        /// </summary>
        VisibleNoZOverlap,

        /// <summary>
        /// All visible cells from the current that are on the same Z floor level.
        /// </summary>
        VisibleSameFloor,
        VisibleSameFloorSeparatePits,
        All
    }
}
