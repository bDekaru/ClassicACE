using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Walks a player along the road they stand on or near, as /followroad: traces the road ahead (see RoadNetwork),
    /// and moves the player along it a point at a time. Where a straight move to the next point of the road is blocked
    /// (a rock, a house, a fence), the pathfinder's route around it is walked instead.
    /// </summary>
    public static class RoadFollower
    {
        /// <summary>
        /// A road point counts as reached this close (3D).
        /// </summary>
        private const float WaypointRadius = 2.5f;

        /// <summary>
        /// The road must be this close to the player to start following it.
        /// </summary>
        private const float MaxApproach = 36f;

        /// <summary>
        /// A trace is this long at most. The road network covers a landblock around the player, so a longer trace could
        /// run off its edge; the follower traces again from the end.
        /// </summary>
        private const float MaxTraceLength = 150f;

        /// <summary>
        /// A route around an obstacle is used when it is longer than the straight move by this factor and distance.
        /// Anything shorter is the pathfinder's detour around the road's own bends.
        /// </summary>
        private const float DetourFactor = 1.3f;
        private const float DetourMargin = 3f;

        private sealed class Follow
        {
            public int Id;
            public Player Player;
            public Queue<Position> Waypoints = new Queue<Position>();
            public Queue<Position> Detour = new Queue<Position>();
            public RoadNetwork.RoadTrace Trace;
            public MoveToState MoveToStateAtStart;
        }

        private static readonly ConcurrentDictionary<uint, int> Follows = new ConcurrentDictionary<uint, int>();
        private static int lastFollowId;

        public static bool IsFollowing(Player player) => Follows.ContainsKey(player.Guid.Full);

        public static void Stop(Player player)
        {
            Follows.TryRemove(player.Guid.Full, out _);

            player.StopExistingMoveToChains();
            if (player.IsPlayerMovingTo2)
                player.StopExistingMoveToChains2();
        }

        public static void Start(Player player)
        {
            var follow = new Follow { Id = System.Threading.Interlocked.Increment(ref lastFollowId), Player = player };
            Follows[player.Guid.Full] = follow.Id;

            var facing = player.Location.GetCurrentDir();
            if (!Plan(follow, new Vector2(facing.X, facing.Y)))
                return;

            Tell(follow, "Following the road. Move, or use the command again, to stop.");
            WalkNext(follow);
        }

        private static bool IsCurrent(Follow follow)
        {
            return follow.Player.Session != null && follow.Player.CurrentLandblock != null
                && Follows.TryGetValue(follow.Player.Guid.Full, out var id) && id == follow.Id;
        }

        private static void Tell(Follow follow, string message)
        {
            if (follow.Player.Session != null)
                follow.Player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Help));
        }

        private static void End(Follow follow, string message)
        {
            Follows.TryRemove(new KeyValuePair<uint, int>(follow.Player.Guid.Full, follow.Id));
            Tell(follow, message);
        }

        /// <summary>
        /// Traces the road ahead from where the player is, going in the direction.
        /// </summary>
        private static bool Plan(Follow follow, Vector2 direction)
        {
            var player = follow.Player;

            if (player.Location.Indoors)
            {
                End(follow, "You can only follow a road outdoors.");
                return false;
            }

            var network = RoadNetwork.Build(player.Location.LandblockId);
            var trace = network.Trace(player.Location.ToGlobal(), direction, MaxApproach, MaxTraceLength);

            // the first point is where the road is nearest to the player; going there is part of following it
            if (trace == null || trace.Points.Count < 2)
            {
                End(follow, "Can't find a road to follow.");
                return false;
            }

            follow.Trace = trace;
            follow.Waypoints = new Queue<Position>(trace.Points.Select(RoadNetwork.ToPosition));
            return true;
        }

        private static void EnqueueStep(Follow follow, Action<Follow> step)
        {
            var actionChain = new ActionChain();
            actionChain.AddAction(follow.Player, () => step(follow));
            actionChain.EnqueueChain();
        }

        private static void WalkNext(Follow follow)
        {
            if (!IsCurrent(follow))
                return;

            var player = follow.Player;

            Position target;
            if (follow.Detour.Count > 0)
                target = follow.Detour.Dequeue();
            else if (follow.Waypoints.Count > 0)
            {
                target = follow.Waypoints.Dequeue();

                foreach (var step in GetDetour(player.Location, target))
                    follow.Detour.Enqueue(step);

                if (follow.Detour.Count > 0)
                    target = follow.Detour.Dequeue();
            }
            else
            {
                OnTraceEnd(follow);
                return;
            }

            follow.MoveToStateAtStart = player.CurrentMoveToState;
            player.CreateMoveToChain(target, success => OnWaypointDone(follow, success), WaypointRadius, walk: false, useSpheres: false);
        }

        /// <summary>
        /// The pathfinder's route to the target, when going straight to it is blocked: the route is a lot longer than
        /// the straight line. Empty when the way is open, or there is no route to tell (meshes not loaded).
        /// </summary>
        private static IEnumerable<Position> GetDetour(Position from, Position target)
        {
            var legs = Pathfinder.FindRouteLegs(from, target, AgentSize.Small, out var reachesTarget);
            if (legs == null || !reachesTarget)
                return Enumerable.Empty<Position>();

            var route = legs.SelectMany(leg => leg).ToList();
            return Pathfinder.GetRouteLength(from, route) > from.DistanceTo(target) * DetourFactor + DetourMargin ? route : Enumerable.Empty<Position>();
        }

        private static void OnWaypointDone(Follow follow, bool success)
        {
            if (!IsCurrent(follow))
                return;

            if (success)
            {
                EnqueueStep(follow, WalkNext);
                return;
            }

            // moving or jumping yourself cancels a move-to
            if (!ReferenceEquals(follow.Player.CurrentMoveToState, follow.MoveToStateAtStart))
                End(follow, "Stopped following the road: you moved.");
            else
                End(follow, "Stopped following the road: something is in the way.");
        }

        private static void OnTraceEnd(Follow follow)
        {
            if (!follow.Trace.Continues)
            {
                End(follow, "The road ends here.");
                return;
            }

            // the next stretch of road, in the direction this one ended in
            if (Plan(follow, follow.Trace.Direction))
                EnqueueStep(follow, WalkNext);
        }
    }
}
