using ACE.Entity;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// A waypoint of a route found by the Pathfinder. At the start of a ledge drop, IsLedgeDropStart is set and the
    /// next waypoint is where it lands: the creature walks off the ledge towards it and falls.
    /// </summary>
    public class RoutePosition : Position
    {
        public bool IsLedgeDropStart { get; }

        public RoutePosition(Position position, bool isLedgeDropStart)
            : base(position)
        {
            IsLedgeDropStart = isLedgeDropStart;
        }

        /// <summary>
        /// True if this waypoint of a route is the start of a ledge drop.
        /// </summary>
        public static bool IsLedgeDrop(Position waypoint) => waypoint is RoutePosition routePosition && routePosition.IsLedgeDropStart;
    }
}
