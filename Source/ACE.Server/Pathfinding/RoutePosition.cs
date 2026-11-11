using ACE.Entity;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// A waypoint of a route found by the Pathfinder. At the start of a drop down a ledge, IsDropStart is set and the
    /// next waypoint is where the drop lands: the creature walks off the ledge towards it and falls.
    /// </summary>
    public class RoutePosition : Position
    {
        public bool IsDropStart { get; }

        public RoutePosition(Position position, bool isDropStart)
            : base(position)
        {
            IsDropStart = isDropStart;
        }

        /// <summary>
        /// True if this waypoint of a route is the start of a drop.
        /// </summary>
        public static bool IsDrop(Position waypoint) => waypoint is RoutePosition routePosition && routePosition.IsDropStart;
    }
}
