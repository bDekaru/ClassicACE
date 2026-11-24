using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Physics.Extensions;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Walks a player along the road they stand on or near, as /followroad: traces the road ahead (see RoadNetwork) into
    /// guide points, and walks the pathfinder's route (RouteProfile.Road) from one guide to the next. The route supplies
    /// the heights and goes around obstacles and over bridges.
    /// </summary>
    public static class RoadFollower
    {
        /// <summary>
        /// A road point counts as reached this close (3D).
        /// </summary>
        private const float WaypointRadius = 2.5f;

        /// <summary>
        /// The road is walked from guide point to guide point, at most this far apart where it runs straight: the
        /// pathfinder's route between two guides keeps to the road (see RouteProfile.Road), so they only need to say where
        /// the road goes.
        /// </summary>
        private const float GuideSpacing = 80f;

        /// <summary>
        /// A road point is left out of the guides only if the way past it strays no further than this from the road.
        /// </summary>
        private const float GuideTolerance = 2f;

        /// <summary>
        /// How far above or below a bridge's far end the mesh is looked for.
        /// </summary>
        private const float DeckReach = 6f;

        /// <summary>
        /// The road must be this close to the player to start following it.
        /// </summary>
        private const float MaxApproach = 36f;

        /// <summary>
        /// A trace is this long at most; the follower traces again from the end.
        /// </summary>
        private const float MaxTraceLength = 150f;

        /// <summary>
        /// A route to a place may start and end this far off the road.
        /// </summary>
        private const float MaxRouteApproach = 150f;
        private const float MaxRouteOffRoad = 150f;

        /// <summary>
        /// A road that ends is looked for again ahead, between these distances and within this cos of its direction;
        /// the nearest candidates are tried with the pathfinder. A walk crosses a few gaps at most.
        /// </summary>
        private const float GapMinDistance = 6f;
        private const float GapMaxDistance = 600f;
        private const float GapMinCos = 0.5f;
        private const int GapCandidates = 8;
        private const int MaxCrossings = 4;

        /// <summary>
        /// A route can come back empty while the navmesh tiles are still building (over a gap, or after a move that
        /// failed): the search is repeated at this interval, this many times at most in a row.
        /// </summary>
        private const double MeshRetrySeconds = 3;
        private const int MaxMeshRetries = 30;

        /// <summary>
        /// The tiles between two points are looked at this far apart, less than the width of a tile.
        /// </summary>
        private const float MeshCheckSpacing = 64f;

        /// <summary>
        /// A move that gets nowhere is replaced by the pathfinder's route to its target this many times in a row.
        /// </summary>
        private const int MaxReplans = 3;

        /// <summary>
        /// A move that failed is followed by a step this far, at an angle in this range (degrees) from the way the
        /// player faces, before the route is planned again.
        /// </summary>
        private const float StepAsideDistance = 4f;
        private const float StepAsideMinAngle = 100f;
        private const float StepAsideMaxAngle = 260f;
        private const float StepAsideRadius = 1.5f;

        private sealed class Follow
        {
            public int Crossings;
            public int MeshRetries;

            /// <summary>
            /// The waypoint being walked to, and how many routes in a row have replaced a move that failed on the way to it.
            /// </summary>
            public Position Target;
            public int Replans;

            /// <summary>
            /// Counts the moves started. Starting a move cancels the one pending, which reports a failure at once: only
            /// the report of the latest move counts.
            /// </summary>
            public int Step;

            /// <summary>
            /// The way the walk is going overall: the player's facing at the start. A trace's own end direction can point
            /// along a stub across it.
            /// </summary>
            public Vector2 Heading;
            public Player Player;
            public Queue<Position> Waypoints = new Queue<Position>();

            /// <summary>
            /// The pathfinder's route to the guide point being walked to, not yet walked.
            /// </summary>
            public Queue<Position> Route = new Queue<Position>();
            public RoadNetwork.RoadTrace Trace;
            public string Destination;
            public MoveToState MoveToStateAtStart;
        }

        private static readonly ConcurrentDictionary<uint, Follow> Follows = new ConcurrentDictionary<uint, Follow>();

        /// <summary>
        /// A follow left over from an earlier login (the player logged out or was removed while following) doesn't count.
        /// </summary>
        public static bool IsFollowing(Player player) => Follows.TryGetValue(player.Guid.Full, out var follow) && ReferenceEquals(follow.Player, player) && IsCurrent(follow);

        public static void Stop(Player player)
        {
            Follows.TryRemove(player.Guid.Full, out _);

            player.StopExistingMoveToChains();
            if (player.IsPlayerMovingTo2)
                player.StopExistingMoveToChains2();
        }

        public static void Start(Player player)
        {
            var follow = new Follow { Player = player };
            Follows[player.Guid.Full] = follow;

            var facing = player.Location.GetCurrentDir();
            var direction = new Vector2(facing.X, facing.Y);
            follow.Heading = Vector2.Normalize(direction);
            if (!Plan(follow, direction))
                return;

            Tell(player, "Following the road. Move, or use the command again, to stop.");
            WalkNext(follow);
        }

        /// <summary>
        /// Plans the cheapest route over the roads to a point of interest (a part-name works when it is unambiguous),
        /// tells the player about it, and walks it unless previewOnly.
        /// </summary>
        public static void StartTo(Player player, string place, bool previewOnly)
        {
            if (player.Location.Indoors)
            {
                Tell(player, "You can only follow a road outdoors.");
                return;
            }

            DatabaseManager.World.CacheAllPointsOfInterest();
            var names = DatabaseManager.World.GetPointsOfInterestCache().Select(k => k.Key).ToList();
            var matches = names.Where(n => n.Equals(place, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0)
                matches = names.Where(n => n.Contains(place, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                Tell(player, $"There is no place called \"{place}\".");
                return;
            }

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            if (matches.Count > 1)
            {
                Tell(player, $"Which place? {string.Join(", ", matches.OrderBy(n => n).Take(8).Select(textInfo.ToTitleCase))}");
                return;
            }

            var name = textInfo.ToTitleCase(matches[0]);
            var poi = DatabaseManager.World.GetCachedPointOfInterest(matches[0]);
            var destination = new Position(DatabaseManager.World.GetCachedWeenie(poi.WeenieClassId).GetPosition(PositionType.Destination));
            if (destination.Indoors)
            {
                Tell(player, $"{name} is indoors; roads only lead outdoors.");
                return;
            }

            var route = GetNetwork(player).FindRoute(player.Location.ToGlobal(), destination.ToGlobal(), MaxRouteApproach, MaxRouteOffRoad);
            if (route == null)
            {
                Tell(player, $"No road route to {name} from here.");
                return;
            }

            Tell(player, $"Route to {name}: {route.RoadLength:0} m of road, {route.StartLeg:0} m to reach it and {route.EndLeg:0} m from its end to the place.");
            if (previewOnly)
                return;

            var follow = new Follow { Player = player, Destination = name };
            Follows[player.Guid.Full] = follow;
            follow.Waypoints = new Queue<Position>(GetGuides(route.Points).Append(destination));
            WalkNext(follow);
        }

        private static bool IsCurrent(Follow follow)
        {
            return follow.Player.Session != null && follow.Player.CurrentLandblock != null
                && Follows.TryGetValue(follow.Player.Guid.Full, out var current) && current == follow;
        }

        private static void Tell(Player player, string message)
        {
            if (player.Session != null)
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Help));
        }

        /// <summary>
        /// The road network, built from the dat on first use, which holds up the command for a while: the player is told.
        /// </summary>
        private static RoadNetwork GetNetwork(Player player)
        {
            if (!RoadNetwork.IsBuilt)
                Tell(player, "Mapping the roads of the world; this takes a moment the first time.");

            return RoadNetwork.GetWorld();
        }

        private static void End(Follow follow, string message)
        {
            Follows.TryRemove(new KeyValuePair<uint, Follow>(follow.Player.Guid.Full, follow));
            Tell(follow.Player, message);
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

            var network = GetNetwork(player);

            // on a bridge there is no road to trace: the pathfinder walks it
            var bridge = network.FindBridgeAhead(player.Location.ToGlobal(), direction);
            if (bridge != null)
            {
                follow.Trace = new RoadNetwork.RoadTrace { Points = new List<Vector3>(), Direction = direction, Continues = true, Bridge = bridge };
                follow.Waypoints = new Queue<Position>();
                return true;
            }

            var trace = network.Trace(player.Location.ToGlobal(), direction, MaxApproach, MaxTraceLength);

            // the first point is where the road is nearest to the player; going there is part of following it
            if (trace == null || trace.Points.Count < 2)
            {
                End(follow, "Can't find a road to follow.");
                return false;
            }

            follow.Trace = trace;
            follow.Waypoints = new Queue<Position>(GetGuides(trace.Points));
            return true;
        }

        /// <summary>
        /// The road points as guide points, GuideSpacing apart (the last one always), on the terrain: a route's target
        /// must be within reach of the mesh, and the road graph's heights are interpolated.
        /// </summary>
        private static IEnumerable<Position> GetGuides(List<Vector3> points)
        {
            var lastIndex = 0;
            for (var i = 0; i < points.Count; i++)
            {
                // a point is skipped while the way from the last guide to the next point stays near it and short
                if (i < points.Count - 1 && (i == 0 || IsSkippable(points, lastIndex, i)))
                    continue;

                lastIndex = i;
                var guide = RoadNetwork.ToPosition(points[i]);
                guide.PositionZ = guide.GetTerrainZ();
                yield return guide;
            }
        }

        /// <summary>
        /// Whether the points after the last guide, up to and including index, can be left out: the straight way from the
        /// last guide to the point after index is at most GuideSpacing long and keeps within GuideTolerance of each of them.
        /// A bend of the road keeps its guide, or the route would cut across it.
        /// </summary>
        private static bool IsSkippable(List<Vector3> points, int lastIndex, int index)
        {
            var from = new Vector2(points[lastIndex].X, points[lastIndex].Y);
            var to = new Vector2(points[index + 1].X, points[index + 1].Y);
            var chord = to - from;
            if (chord.Length() > GuideSpacing)
                return false;

            for (var k = lastIndex + 1; k <= index; k++)
            {
                var point = new Vector2(points[k].X, points[k].Y);
                var t = chord.LengthSquared() < 0.0001f ? 0f : Math.Clamp(Vector2.Dot(point - from, chord) / chord.LengthSquared(), 0f, 1f);
                if (Vector2.Distance(point, from + chord * t) > GuideTolerance)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The pathfinder's route to the target keeping to the road, as positions with the mesh's heights. Null when there
        /// is none yet (the tiles may still be building) or it does not reach the target, unless a partial route (as
        /// far as the search reaches) is allowed.
        /// </summary>
        private static List<Position> GetRoute(Position from, Position target, bool allowPartial = false)
        {
            var legs = Pathfinder.FindRouteLegs(from, target, AgentSize.Small, out var reachesTarget, RouteProfile.Road);
            if (legs == null || !reachesTarget && !allowPartial)
                return null;

            var route = legs.SelectMany(leg => leg).ToList();
            return route.Count > 0 ? route : null;
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

            if (follow.Route.Count == 0)
            {
                if (follow.Waypoints.Count == 0)
                {
                    OnTraceEnd(follow);
                    return;
                }

                // the route to the next guide point; where there is none yet, a straight move
                var guide = follow.Waypoints.Dequeue();
                follow.Route = new Queue<Position>(GetRoute(player.Location, guide) ?? new List<Position> { guide });
            }

            var target = follow.Route.Dequeue();
            follow.Target = target;
            follow.MoveToStateAtStart = player.CurrentMoveToState;
            var moveId = ++follow.Step;
            player.CreateMoveToChain(target, success => OnWaypointDone(follow, moveId, success), WaypointRadius, walk: false, useSpheres: false);
        }

        private static void OnWaypointDone(Follow follow, int moveId, bool success)
        {
            if (!IsCurrent(follow) || moveId != follow.Step)
                return;

            if (success)
            {
                follow.Replans = 0;
                follow.MeshRetries = 0;
                EnqueueStep(follow, WalkNext);
                return;
            }

            // moving or jumping yourself cancels a move-to
            if (!ReferenceEquals(follow.Player.CurrentMoveToState, follow.MoveToStateAtStart))
                End(follow, "Stopped following the road: you moved.");
            else if (follow.Replans < MaxReplans)
                EnqueueStep(follow, StepAsideOrReplan);
            else
                End(follow, "Stopped following the road: something is in the way.");
        }

        /// <summary>
        /// Run from a queued step, not from the move's callback: the callback's caller clears the move-to parameters
        /// when it returns, which would drop the move started here.
        /// </summary>
        private static void StepAsideOrReplan(Follow follow)
        {
            if (!IsCurrent(follow) || TryStepAside(follow))
                return;

            ReplanOrWait(follow);
        }

        /// <summary>
        /// Walks the pathfinder's route to the target, or waits for the tiles to build when they are why there is none.
        /// </summary>
        private static void ReplanOrWait(Follow follow)
        {
            if (!IsCurrent(follow))
                return;

            if (TryReplan(follow))
                WalkNext(follow);
            else if (!IsMeshPending(follow.Player.Location, follow.Target) || !WaitForMesh(follow, ReplanOrWait, "Waiting for the map of the way ahead to be built."))
                End(follow, "Stopped following the road: something is in the way.");
        }

        /// <summary>
        /// Whether a mesh the route between two positions may need is not built yet: the meshes under both, and the
        /// outdoor tiles along the line between them.
        /// </summary>
        private static bool IsMeshPending(Position from, Position to)
        {
            var start = from.ToGlobal();
            var end = to.ToGlobal();
            var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / MeshCheckSpacing));

            return IsMeshPending(from) || IsMeshPending(to)
                || Enumerable.Range(1, steps - 1).Any(i => IsMeshPending(RoadNetwork.ToPosition(Vector3.Lerp(start, end, (float)i / steps))));
        }

        private static bool IsMeshPending(Position position)
        {
            var status = Pathfinder.GetMeshLoadStatus(position, AgentSize.Small);
            return status == MeshLoadStatus.Loading || status == MeshLoadStatus.NotLoaded;
        }

        /// <summary>
        /// Runs the step again after a delay, telling the player the first time. False when the retries are used up.
        /// </summary>
        private static bool WaitForMesh(Follow follow, Action<Follow> retry, string message)
        {
            if (follow.MeshRetries >= MaxMeshRetries)
                return false;

            if (follow.MeshRetries++ == 0)
                Tell(follow.Player, message);

            var chain = new ActionChain();
            chain.AddDelaySeconds(MeshRetrySeconds);
            chain.AddAction(follow.Player, () =>
            {
                if (IsCurrent(follow))
                    retry(follow);
            });
            chain.EnqueueChain();
            return true;
        }

        /// <summary>
        /// After a move that got nowhere, as a monster does (Monster_Navigation TryWandering): a few metres in a random
        /// direction away from where the player faces (the blocked way), then the route is planned again from there.
        /// Things placed in the world (signs, NPCs) are not in the navmesh, so a route can lead into them.
        /// </summary>
        private static bool TryStepAside(Follow follow)
        {
            var player = follow.Player;

            var aside = new Position(player.Location);
            aside.Rotation = new Quaternion(0, 0, aside.RotationZ, aside.RotationW) * Quaternion.CreateFromYawPitchRoll(0, 0, (float)ThreadSafeRandom.Next(StepAsideMinAngle.ToRadians(), StepAsideMaxAngle.ToRadians()));
            aside = aside.InFrontOf(StepAsideDistance);
            aside.SetLandblock();

            var onMesh = Pathfinder.GetClosestPointOnMesh(aside, AgentSize.Small);
            if (onMesh == null)
                return false;

            follow.MoveToStateAtStart = player.CurrentMoveToState;
            var moveId = ++follow.Step;
            player.CreateMoveToChain(onMesh, success => OnSteppedAside(follow, moveId), StepAsideRadius, walk: false, useSpheres: false);
            return true;
        }

        private static void OnSteppedAside(Follow follow, int moveId)
        {
            if (!IsCurrent(follow) || moveId != follow.Step)
                return;

            if (!ReferenceEquals(follow.Player.CurrentMoveToState, follow.MoveToStateAtStart))
                End(follow, "Stopped following the road: you moved.");
            else
                EnqueueStep(follow, ReplanOrWait);
        }

        /// <summary>
        /// After a move that got nowhere (a straight move to a guide point that had no route yet, or a step the
        /// pathfinder's route should not have), walks the pathfinder's route to the same target instead.
        /// </summary>
        private static bool TryReplan(Follow follow)
        {
            var route = GetRoute(follow.Player.Location, follow.Target);
            if (route == null)
                return false;

            follow.Route = new Queue<Position>(route);
            follow.Replans++;
            return true;
        }

        /// <summary>
        /// Where a road ends, looks for road further on in the same direction and walks the pathfinder's route to it:
        /// bridge decks are buildings with no road under them, but they are in the navmesh. The walk then goes on along
        /// the road found.
        /// </summary>
        /// <param name="candidatesFound">True if there is road ahead to cross to, whether or not a route to it was found</param>
        private static bool TryCrossGap(Follow follow, out bool candidatesFound)
        {
            var player = follow.Player;
            var from = player.Location.ToGlobal();
            var direction = follow.Heading != Vector2.Zero ? follow.Heading : follow.Trace.Direction;

            // at a bridge the way is to the road beyond it; at any other end of the road, road ahead is looked for
            var bridge = follow.Trace.Bridge;
            var candidates = bridge != null
                ? new List<Vector3> { bridge.Exit }
                : RoadNetwork.GetWorld().FindNodesAhead(from, direction, GapMinDistance, GapMaxDistance, GapMinCos, GapCandidates);
            candidatesFound = candidates.Count > 0;
            foreach (var node in candidates)
            {
                var target = RoadNetwork.ToPosition(node);
                if (bridge != null && !bridge.ExitOnRoad)
                {
                    target.PositionZ = target.GetTerrainZ();
                    target = Pathfinder.GetClosestPointOnMesh(target, AgentSize.Small, DeckReach) ?? target;
                }

                // a crossing longer than the search reaches goes in stages: the walk ends where the partial route does,
                // and the road ahead is looked for again from there
                var route = GetRoute(player.Location, target, true);
                if (route == null)
                    continue;

                follow.Route = new Queue<Position>(route);
                follow.Waypoints.Clear();
                follow.Trace = new RoadNetwork.RoadTrace { Points = new List<Vector3>(), Direction = Vector2.Normalize(new Vector2(node.X - from.X, node.Y - from.Y)), Continues = bridge == null || bridge.ExitOnRoad };
                follow.Heading = follow.Trace.Direction;
                follow.Crossings++;
                follow.MeshRetries = 0;
                Tell(player, $"The road has a gap here; crossing it ({Vector2.Distance(new Vector2(from.X, from.Y), new Vector2(node.X, node.Y)):0} m).");
                return true;
            }

            return false;
        }

        private static void OnTraceEnd(Follow follow)
        {
            if (follow.Trace == null)
            {
                End(follow, $"Arrived at {follow.Destination}.");
                return;
            }

            if (!follow.Trace.Continues || follow.Trace.Bridge != null)
            {
                var candidatesFound = false;
                if (follow.Crossings < MaxCrossings && TryCrossGap(follow, out candidatesFound))
                    EnqueueStep(follow, WalkNext);
                else if (!candidatesFound || !WaitForMesh(follow, OnTraceEnd, "The road has a gap here; waiting for the way across to be mapped."))
                    End(follow, candidatesFound ? "Stopped following the road: the way across the gap could not be mapped." : "The road ends here.");
                return;
            }

            // the next stretch of road, in the direction this one ended in
            if (Plan(follow, follow.Trace.Direction))
                EnqueueStep(follow, WalkNext);
        }
    }
}
