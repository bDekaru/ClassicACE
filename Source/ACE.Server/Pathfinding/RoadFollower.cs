using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
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
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        /// <summary>
        /// How long after using a portal the player may take to start teleporting before the portal counts as not working.
        /// </summary>
        private const double PortalUseSeconds = 5;

        /// <summary>
        /// Portals placed inside a building (by instance guid) that routes may take as well: the Yanshi portal at
        /// 36.8S 12.1E. The other building portals are far from any road or have monsters and doors.
        /// </summary>
        private static readonly uint[] BuildingPortalGuids = { 2028277760 };

        private sealed class Follow
        {
            /// <summary>
            /// Where a walk to a place is going, whether it may take portals, and the portals that are left out because
            /// the player could not use them.
            /// </summary>
            public Position DestinationPosition;
            public bool UsePortals;
            public HashSet<RoadNetwork.PortalLink> RefusedPortals = new HashSet<RoadNetwork.PortalLink>();

            /// <summary>
            /// The portal the walk is going to take, at the end of the waypoints.
            /// </summary>
            public RoadNetwork.PortalLink PendingPortal;

            /// <summary>
            /// Set when the walk uses the pending portal: only a teleport after that is the walk's own.
            /// </summary>
            public bool PortalUsed;

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
        public static void StartTo(Player player, string place, bool previewOnly, bool usePortals)
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

            var follow = new Follow { Player = player, Destination = name, DestinationPosition = destination, UsePortals = usePortals };
            var route = FindRoute(follow);
            if (route == null)
            {
                Tell(player, $"No road route to {name} from here.");
                return;
            }

            var portals = route.PortalCount == 0 ? "" : $", {route.PortalCount} portal{(route.PortalCount > 1 ? "s" : "")}, the first {route.Portal.Portal.Name}";
            Tell(player, $"Route to {name}: {route.RoadLength:0} m of road{portals}, {route.StartLeg:0} m to reach it and {route.EndLeg:0} m from its end to the place.");
            if (previewOnly)
                return;

            Follows[player.Guid.Full] = follow;
            WalkRoute(follow, route);
        }

        private static RoadNetwork.RoadRoute FindRoute(Follow follow)
        {
            var player = follow.Player;
            Func<RoadNetwork.PortalLink, bool> canUsePortal = link => !follow.RefusedPortals.Contains(link) && link.Portal.GetRestriction(player) == null
                && (player.IgnorePortalRestrictions || player.VerifyGameplayMode(link.Portal));

            return GetNetwork(player).FindRoute(player.Location.ToGlobal(), follow.DestinationPosition.ToGlobal(), MaxRouteApproach, MaxRouteOffRoad, follow.UsePortals ? canUsePortal : null);
        }

        /// <summary>
        /// Walks the route's points, to the portal if it takes one, else to the place.
        /// </summary>
        private static void WalkRoute(Follow follow, RoadNetwork.RoadRoute route)
        {
            follow.PendingPortal = route.Portal;
            follow.PortalUsed = false;
            follow.Route.Clear();

            var guides = GetGuides(route.Points).ToList();

            // the last guide is on the terrain: a portal inside a building is walked to at its own position, which has the cell the pathfinder routes to
            if (route.Portal != null && route.Portal.Location.Indoors)
                guides[guides.Count - 1] = route.Portal.Location;

            follow.Waypoints = new Queue<Position>(route.Portal == null ? guides.Append(follow.DestinationPosition) : guides);
            WalkNext(follow);
        }

        /// <summary>
        /// Plans the rest of the way to the place from where the player is, after a portal was taken or did not work.
        /// </summary>
        private static void Reroute(Follow follow)
        {
            if (!IsCurrent(follow))
                return;

            // a portal can end the walk right at the place
            var here = follow.Player.Location.ToGlobal();
            var place = follow.DestinationPosition.ToGlobal();
            var distance = Vector2.Distance(new Vector2(here.X, here.Y), new Vector2(place.X, place.Y));
            if (distance <= WaypointRadius)
            {
                End(follow, $"Arrived at {follow.Destination}.");
                return;
            }

            var route = FindRoute(follow);
            if (route != null)
            {
                WalkRoute(follow, route);
                return;
            }

            // a place within walking distance has no road route, it is walked to
            if (distance > RoadNetwork.MaxDirectWalk)
            {
                End(follow, $"No road route to {follow.Destination} from here.");
                return;
            }

            follow.PendingPortal = null;
            follow.Route.Clear();
            follow.Waypoints = new Queue<Position>(new[] { follow.DestinationPosition });
            WalkNext(follow);
        }

        /// <summary>
        /// Called when a player has been teleported: a walk that was going to a portal goes on from where it came out.
        /// </summary>
        public static void OnTeleported(Player player)
        {
            if (Follows.TryGetValue(player.Guid.Full, out var follow) && ReferenceEquals(follow.Player, player) && follow.PortalUsed)
                EnqueueStep(follow, Reroute);
        }

        /// <summary>
        /// Uses the portal the walk has got to, as a double click would. If the player is not teleporting soon after (the
        /// portal refused them, or is not there), the route is planned again without it.
        /// </summary>
        private static void UsePortal(Follow follow)
        {
            var portal = follow.PendingPortal;
            var step = follow.Step;

            // a second portal within the portal timer is refused: wait it out
            var sinceLastPortal = follow.Player.LastPortalTeleportTimestamp.HasValue ? Time.GetUnixTime() - follow.Player.LastPortalTeleportTimestamp.Value : double.MaxValue;

            // a teleport (under way, or done and gone on to a new route, which started a move) ends the wait
            bool IsWaiting() => IsCurrent(follow) && follow.Step == step && !follow.Player.Teleporting;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(Math.Max(0, Portal.minTimeSinceLastPortal - sinceLastPortal));

            // the second try covers a slow approach or a busy server
            for (var attempt = 0; attempt < 2; attempt++)
            {
                actionChain.AddAction(follow.Player, () =>
                {
                    if (!IsWaiting())
                        return;

                    follow.PortalUsed = true;
                    follow.Player.HandleActionUseItem(portal.Portal.Guid.Full);
                });
                actionChain.AddDelaySeconds(PortalUseSeconds);
            }

            actionChain.AddAction(follow.Player, () =>
            {
                if (!IsWaiting())
                    return;

                follow.RefusedPortals.Add(portal);
                Tell(follow.Player, $"The portal {portal.Portal.Name} did not work; planning another way.");
                Reroute(follow);
            });
            actionChain.EnqueueChain();
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

            return Network.Value;
        }

        /// <summary>
        /// The road network with the world's portals joined to it, from the database on first use.
        /// </summary>
        private static readonly Lazy<RoadNetwork> Network = new Lazy<RoadNetwork>(() =>
        {
            var network = RoadNetwork.GetWorld();

            // a database error leaves the walk without portals; the Lazy would keep the exception for good
            try
            {
                network.SetPortals(LoadPortals(), MaxRouteApproach);
            }
            catch (Exception ex)
            {
                log.Error($"Loading the portals for /followroad failed: {ex}");
            }

            return network;
        });

        /// <summary>
        /// Builds the road network and joins the portals to it at server start, so no command waits for them.
        /// </summary>
        public static void Prebuild() => _ = Network.Value;

        /// <summary>
        /// The portals placed on the ground (and the building portals listed) that lead to the ground, and that need
        /// nothing from a player but their level, kind and the like: not those that ask a quest.
        /// </summary>
        private static List<RoadNetwork.PortalLink> LoadPortals()
        {
            var links = new List<RoadNetwork.PortalLink>();
            var database = DatabaseManager.World;

            // one query for the placed portals on the ground: a query per portal weenie takes minutes
            List<ACE.Database.Models.World.LandblockInstance> instances;
            using (var context = new ACE.Database.Models.World.WorldDbContext())
            {
                var portalWcids = database.GetAllWeenieTypes(context).Where(t => t.Value == (int)WeenieType.Portal).Select(t => t.Key).ToList();
                var buildingGuids = BuildingPortalGuids.ToList();
                instances = context.LandblockInstance.Where(i => portalWcids.Contains(i.WeenieClassId) && ((i.ObjCellId & 0xFFFF) < 0x100 || buildingGuids.Contains(i.Guid))).ToList();
            }

            foreach (var instance in instances)
            {
                var location = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                if (!(WorldObjectFactory.CreateWorldObject(database.GetCachedWeenie(instance.WeenieClassId), new ObjectGuid(instance.Guid)) is Portal portal))
                    continue;

                if (portal.Destination == null || portal.Destination.Indoors || portal.Quest != null || portal.QuestRestriction != null)
                    continue;

                links.Add(new RoadNetwork.PortalLink { From = location.ToGlobal(), Location = location, To = portal.Destination.ToGlobal(), Portal = portal });
            }

            return links;
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
                    if (follow.PendingPortal != null)
                        UsePortal(follow);
                    else
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
