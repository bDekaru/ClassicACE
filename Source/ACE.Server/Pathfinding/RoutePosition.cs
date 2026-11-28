using ACE.Entity;
using System;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// A waypoint of a route found by the Pathfinder. At the start of a ledge drop, IsLedgeDropStart is set and the
    /// next waypoint is where it lands: the creature walks off the ledge towards it and falls. Where the route enters
    /// the floor under doors, Doors holds their guids: the creature opens them there.
    /// </summary>
    public class RoutePosition : Position
    {
        public bool IsLedgeDropStart { get; }

        public uint[] Doors { get; }

        public RoutePosition(Position position, bool isLedgeDropStart, uint[] doors = null)
            : base(position)
        {
            IsLedgeDropStart = isLedgeDropStart;
            Doors = doors;
        }

        /// <summary>
        /// True if this waypoint of a route is the start of a ledge drop.
        /// </summary>
        public static bool IsLedgeDrop(Position waypoint) => waypoint is RoutePosition routePosition && routePosition.IsLedgeDropStart;

        /// <summary>
        /// The doors a route enters at this waypoint, mostly none.
        /// </summary>
        public static uint[] GetDoors(Position waypoint) => (waypoint as RoutePosition)?.Doors ?? Array.Empty<uint>();
    }
}
