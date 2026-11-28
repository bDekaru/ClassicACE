//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
using ACE.Server.Pathfinding.Geometry;
using ACE.Server.Pathfinding.Reachability;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Routes: paths on one mesh, and the legs of a route that crosses between meshes through openings.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Extra distance turn waypoints are pushed away from the corner they bend around, on top of the agent radius
        /// the mesh keeps from walls and shallow drops. It runs to the edge of a deep drop (see ObstacleErosion).
        /// </summary>
        private const float CornerClearance = 0.3f;

        private const int MaxSurfaceMovePolys = 64;

        /// <summary>
        /// An outdoor route searches at most this many landblocks away from its start, in each direction, and only
        /// requests the tiles in that range. Bounds the search on a server with many landblocks loaded.
        /// </summary>
        private const int MaxRouteLandblockSpan = 2;

        /// <summary>
        /// The span of a RouteProfile.Road search: it is a rare, explicit request (a player following a road), and a
        /// river crossing can be three landblocks long. The search and the tile loads grow with the square of the span
        /// (25 tiles at 2, 81 at 4, each tile a few seconds to build the first time), and monsters search every tick, so
        /// they stay at MaxRouteLandblockSpan.
        /// </summary>
        private const int RoadRouteLandblockSpan = 4;

        private static int GetRouteLandblockSpan(RouteProfile profile) => profile == RouteProfile.Road ? RoadRouteLandblockSpan : MaxRouteLandblockSpan;

        /// <summary>
        /// A ledge drop (see LedgeDropFinder) costs this many times its length, so a creature only takes one
        /// when walking around is clearly longer: it can't come back up the same way.
        /// </summary>
        private const float LedgeDropCostFactor = 5.0f;

        /// <summary>
        /// Narrow floor costs this many times its length: physics lets a creature walk along a wall or fence top, but
        /// it should not choose to over the floor beside it.
        /// </summary>
        private const float NarrowCostFactor = 5.0f;

        /// <summary>
        /// Every poly but narrow floor, for smoothing a route: its raycasts ignore costs, and would pull a straight leg
        /// across a wall top.
        /// </summary>
        private static readonly DtQueryDefaultFilter SmoothingFilter = new DtQueryDefaultFilter(0xFFFF, NavPolyFlags.Narrow, Array.Empty<float>());

        /// <summary>
        /// Every poly, for route searches: like AllPolysFilter, with ledge drops and narrow floor at their cost factors.
        /// </summary>
        private static readonly DtQueryDefaultFilter RouteFilter = CreateRouteFilter(1.0f, LedgeDropCostFactor);

        /// <param name="offRoadCost">The cost factor of every area but the road (1), ledge drops and narrow floor</param>
        private static DtQueryDefaultFilter CreateRouteFilter(float offRoadCost, float ledgeDropCost, int excludeFlags = 0)
        {
            var areaCosts = new float[DtDetour.DT_MAX_AREAS];
            Array.Fill(areaCosts, offRoadCost);
            areaCosts[NavArea.Road] = 1.0f;
            areaCosts[NavArea.LedgeDrop] = ledgeDropCost;
            areaCosts[NavArea.Narrow] = NarrowCostFactor;
            return new DtQueryDefaultFilter(0xFFFF, excludeFlags, areaCosts);
        }

        /// <summary>
        /// Off road costs this many times its length for RouteProfile.Road. Costs must stay at 1 or more (Recast's A* needs
        /// an admissible heuristic); the road is 1. Beyond 2 the routes stay as long and as far off road (measured on a
        /// Holtburg tile: 12.0% off road at 2, 11.9% at 4, against 59.7% without the preference).
        /// </summary>
        internal const float OffRoadCostFactor = 2.0f;

        /// <summary>
        /// Like RouteFilter, but the road is preferred over the ground, and no ledge drops: RoadFollower walks every
        /// waypoint with a wide arrival radius, which would cut a drop short.
        /// </summary>
        private static readonly DtQueryDefaultFilter RoadRouteFilter = CreateRouteFilter(OffRoadCostFactor, LedgeDropCostFactor, NavPolyFlags.LedgeDrop);

        /// <summary>
        /// Limits a search to the outdoor tiles in a rect of landblocks, so a route towards an unreachable target
        /// doesn't search every loaded tile of the world.
        /// </summary>
        private sealed class TileRectFilter : IDtQueryFilter
        {
            private readonly IDtQueryFilter inner;
            private readonly int minX, minY, maxX, maxY;

            public TileRectFilter(IDtQueryFilter inner, int minX, int minY, int maxX, int maxY)
            {
                this.inner = inner;
                this.minX = minX;
                this.minY = minY;
                this.maxX = maxX;
                this.maxY = maxY;
            }

            public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly)
            {
                var header = tile?.data?.header;
                if (header == null)
                    return false;

                return header.x >= minX && header.x <= maxX && header.y >= minY && header.y <= maxY && inner.PassFilter(refs, tile, poly);
            }

            public float GetCost(RcVec3f pa, RcVec3f pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef, DtMeshTile curTile, DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
            {
                return inner.GetCost(pa, pb, prevRef, prevTile, prevPoly, curRef, curTile, curPoly, nextRef, nextTile, nextPoly);
            }
        }

        /// <summary>
        /// Find a route on the start's mesh group to one of the targets: the end position when it is in that group, or
        /// the openings towards it (see GetCrossingCandidates). Each target comes with an estimate of the distance left
        /// after it, and the shortest complete route counting it wins. Outdoor routes cross landblock edges, searching
        /// up to the profile's span (GetRouteLandblockSpan) of landblocks away from the start. Outdoor tiles load in the
        /// background, so a route through landblocks whose tiles are not loaded yet ends at the edge of the loaded ones.
        /// </summary>
        /// <param name="reached">The index of the target the route leads all the way to, or -1 for a partial route,
        /// which leads as close to the most promising target as the mesh allows.</param>
        /// <returns>A list of positions</returns>
        private static List<Position> FindRoute(Position start, MeshGroupKey group, List<(Position target, float remaining)> targets, AgentSize agentSize, RouteProfile profile, out int reached)
        {
            reached = -1;

            if (targets.Count == 0)
                return null; // e.g. two dungeon sections only connected by portals

            if (group.IsOutdoor)
            {
                foreach (var (target, _) in targets)
                    RequestOutdoorTilesBetween(start, target, GetRouteLandblockSpan(profile));
            }

            using var lease = AcquireMesh(start, group, agentSize);
            if (lease == null)
                return null;

            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, lease.ToMesh(start), out var startRef, out var startPt, out var startOnIsland))
                return null; // the start is not on the mesh

            var pathFilter = GetPathFilter(lease, start, profile);

            // Path to each target and keep the shortest complete route, counting the estimated distance left after
            // an opening. If no target can be reached, use the partial route towards the most promising one.
            List<Waypoint> best = null;
            List<Waypoint> partial = null;
            var bestScore = float.MaxValue;

            for (var i = 0; i < targets.Count; i++)
            {
                var (target, remaining) = targets[i];
                var targetWaypoints = FindWaypoints(query, pathFilter, startRef, startPt, startOnIsland, lease.ToMesh(target), out var reachesTarget);
                if (targetWaypoints == null)
                    continue;

                if (!reachesTarget)
                {
                    partial ??= targetWaypoints;
                    continue;
                }

                var score = GetPathLength(startPt, targetWaypoints) + remaining;
                if (score < bestScore)
                {
                    best = targetWaypoints;
                    bestScore = score;
                    reached = i;
                }
            }

            var waypoints = best ?? partial;
            if (waypoints == null)
                return null;

            // clusters of corner waypoints become one with room around it; the corners left are pushed off their walls
            MergeCornerClusters(query, SmoothingFilter, waypoints);
            OffsetCorners(query, SmoothingFilter, waypoints);

            for (int i = 0; i < waypoints.Count; i++)
                waypoints[i] = SnapToFloor(query, waypoints[i]);

            // each waypoint's cell is looked up from the previous one's, which is usually a few cells away
            var positionList = new List<Position>(waypoints.Count);
            var previous = start;
            foreach (var waypoint in waypoints)
            {
                previous = new RoutePosition(CreatePosition(previous, group, ToRcVec3f(waypoint.Pos)), waypoint.IsLedgeDropStart);
                positionList.Add(previous);
            }

            return positionList;
        }

        /// <summary>
        /// The filter a route search runs with: every poly, but outdoors only within the profile's span
        /// landblocks of the start.
        /// </summary>
        private static IDtQueryFilter GetPathFilter(MeshLease lease, Position start, RouteProfile profile)
        {
            var filter = profile == RouteProfile.Road ? RoadRouteFilter : RouteFilter;
            if (!lease.IsOutdoor)
                return filter;

            var (x, y) = GetLandblockCoords(start.Cell);
            var span = GetRouteLandblockSpan(profile);
            return new TileRectFilter(filter, x - span, y - span, x + span, y + span);
        }

        /// <summary>
        /// The string-pulled path from a start poly to a target, as waypoints. reachesTarget is false for a partial
        /// path, which leads as close to the target as the mesh allows. Returns null if there is no path at all.
        /// </summary>
        private static List<Waypoint> FindWaypoints(DtNavMeshQuery query, IDtQueryFilter pathFilter, long startRef, RcVec3f startPt, bool startOnIsland, RcVec3f target, out bool reachesTarget)
        {
            reachesTarget = false;

            // From the main area, a target next to an island (e.g. beside a table) is snapped to the floor rather
            // than onto the island; from an island, the target is looked up anywhere.
            var endFound = startOnIsland
                ? TryFindNearestPoly(query, AllPolysFilter, target, out var endRef, out var endPt)
                : TryFindNearestReachablePoly(query, target, out endRef, out endPt, out _);
            if (!endFound)
                return null; // the target is not on the mesh (outdoors: possibly its tile is not loaded yet)

            // the buffers only live for this call (the waypoints are copied out), so they are rented
            var polys = ArrayPool<long>.Shared.Rent(MaxPolys);
            var path = ArrayPool<DtStraightPath>.Shared.Rent(MaxPolys);
            try
            {
                var pathStatus = query.FindPath(startRef, endRef, startPt, endPt, pathFilter, polys, out int polyCount, MaxPolys);
                if (pathStatus.Failed() || polyCount == 0)
                    return null;

                // When the target is unreachable (or the corridor did not fit in the buffer, or the search ran out of
                // nodes) the corridor ends on another poly: a partial route, as close to the target as the mesh allows.
                var lastPolyRef = polys[polyCount - 1];
                var reachesEnd = lastPolyRef == endRef;

                var pathEnd = endPt;
                if (!reachesEnd && query.ClosestPointOnPoly(lastPolyRef, endPt, out var clampedEnd, out _).Succeeded())
                    pathEnd = clampedEnd;

                var straightStatus = query.FindStraightPath(startPt, pathEnd, polys, polyCount, path, out int straightPathCount, MaxPolys, 0);
                if (straightStatus.Failed() || straightPathCount == 0)
                    return null;

                var waypoints = new List<Waypoint>(straightPathCount + 1);
                for (int i = 0; i < straightPathCount; i++)
                {
                    var entry = path[i];
                    var isLedgeDropStart = (entry.flags & DtStraightPathFlags.DT_STRAIGHTPATH_OFFMESH_CONNECTION) != 0;

                    // the end vertex of a straight path carries no poly ref, fall back to the last poly of the corridor
                    waypoints.Add(new Waypoint(ToVector3(entry.pos), entry.refs != 0 ? entry.refs : lastPolyRef, isLedgeDropStart));

                    // The landing of a ledge drop must be a waypoint of its own, so the ledge drop is followed as found. String
                    // pulling can leave it out when the path goes on straight from it.
                    if (isLedgeDropStart && TryGetLedgeDropLanding(query, polys, polyCount, entry.refs, out var landing) &&
                        (i + 1 >= straightPathCount || Vector3.DistanceSquared(ToVector3(path[i + 1].pos), landing.Pos) > 0.01f))
                        waypoints.Add(landing);
                }

                reachesTarget = reachesEnd;
                return waypoints;
            }
            finally
            {
                ArrayPool<long>.Shared.Return(polys);
                ArrayPool<DtStraightPath>.Shared.Return(path);
            }
        }

        /// <summary>
        /// The landing of a ledge drop (an off-mesh connection) on a path corridor, on the poly after it.
        /// </summary>
        private static bool TryGetLedgeDropLanding(DtNavMeshQuery query, long[] polys, int polyCount, long ledgeDropRef, out Waypoint landing)
        {
            landing = default;

            var index = Array.IndexOf(polys, ledgeDropRef, 0, polyCount);
            if (index <= 0 || index + 1 >= polyCount)
                return false;

            var start = RcVec3f.Zero;
            var end = RcVec3f.Zero;
            var status = query.GetAttachedNavMesh().GetOffMeshConnectionPolyEndPoints(polys[index - 1], ledgeDropRef, ref start, ref end);
            if (status.Failed())
                return false;

            landing = new Waypoint(ToVector3(end), polys[index + 1]);
            return true;
        }

        private static float GetPathLength(RcVec3f start, List<Waypoint> waypoints)
        {
            var length = 0f;
            var previous = ToVector3(start);
            foreach (var waypoint in waypoints)
            {
                length += Vector3.Distance(previous, waypoint.Pos);
                previous = waypoint.Pos;
            }
            return length;
        }

        /// <summary>
        /// The most openings FindRoute paths to when picking one.
        /// </summary>
        private const int MaxOpeningCandidates = 3;

        /// <summary>
        /// How many batches of MaxOpeningCandidates a leg tries, the next one only when no opening of the batch before
        /// can be reached: openings that lead nowhere (around an open roof) can rank ahead of the real doorway.
        /// </summary>
        private const int MaxOpeningBatches = 2;

        /// <summary>
        /// Openings closer together than this count as one (e.g. the portals of a double doorway).
        /// </summary>
        private const float SameOpeningDistance = 1.0f;

        /// <summary>
        /// The most legs a route follows. Each crossing between mesh groups starts one: leaving a building, entering one,
        /// or stepping through touching openings into the next building of a compound.
        /// </summary>
        private const int MaxRouteLegs = 12;

        /// <summary>
        /// A leg must end this close to the crossing it was headed for (see Crossing.Near) for the route to continue on
        /// the other side.
        /// </summary>
        private const float OpeningReachDistance = 2.0f;

        /// <summary>
        /// How far onto the outdoor mesh an opening's outdoor side is placed (see GetOpeningOutsidePoint).
        /// </summary>
        private const float OpeningOutsideMargin = 0.3f;

        /// <summary>
        /// Where a route meets an opening on the outdoor mesh, in landblock coordinates. The outdoor mesh keeps an agent
        /// radius from the building's walls, so in front of an opening in a wall it starts about that far out from the
        /// threshold: the point is there, a little onto the mesh, at threshold height.
        /// </summary>
        private static Vector3 GetOpeningOutsidePoint(CellGroupOpening opening, AgentSize agentSize)
        {
            return opening.Position + opening.Normal * (GetAgentRadius(agentSize) + OpeningOutsideMargin);
        }

        /// <summary>
        /// A way from one mesh group into another through an opening: between an interior group and the outdoors (either
        /// way), or between two interior groups through touching openings (see FindTouchingOpenings). Near is where a
        /// leg on From's mesh heads for it, Far where the next leg starts on To's mesh: the threshold on an interior
        /// mesh, the outdoor point (see GetOpeningOutsidePoint) on the outdoor mesh, whose opening apron reaches it.
        /// NearAt and FarAt are the same points in world coordinates. NearComponent and FarComponent are the mesh
        /// components of the opening at either end: on an interior side as FindPassableOpenings found them, outdoors the
        /// outdoor area in front (see WorldGraph.GetOutdoorArea), -1 when not known.
        /// </summary>
        private readonly record struct Crossing(MeshGroupKey From, MeshGroupKey To, Position Near, Vector3 NearAt, Position Far, Vector3 FarAt, int NearComponent, int FarComponent);

        /// <summary>
        /// True if two components (see Crossing) may be the same one: equal, or either not known.
        /// </summary>
        private static bool SameComponent(int a, int b) => a < 0 || b < 0 || a == b;

        /// <summary>
        /// The crossings a route from the start's mesh group to the end's can take, by the group they leave, each with a
        /// straight-line estimate of the distance left from it to the end, through the crossings after it. They are the
        /// crossings of the two groups and of every group joined to them through touching openings, so a route also
        /// enters or leaves a building that only opens into its neighbours. Walks through the outdoors and inside a group
        /// between its openings are estimated as straight lines. Only openings an agent of the given size can get
        /// through are used (see GetOpenings), and as exits only those the reachability bake found outdoor floor in
        /// front of (while their tile has no Recast summary, all of them). EndComponent is the end's component as in
        /// Crossing. With one group for both start and end it plans a detour, for when the mesh doesn't connect the two:
        /// out of the group through one opening and back through another, outdoors through the buildings GetDetourGroups
        /// gives. The group is then only entered again through openings a path leads from to the end.
        /// </summary>
        private static ILookup<MeshGroupKey, (Crossing crossing, float remaining)> PlanCrossings(WorldGraph graph, Position start, MeshGroupKey startGroup, Position end, MeshGroupKey endGroup, int endComponent, AgentSize agentSize, RouteProfile profile)
        {
            var detour = startGroup == endGroup;
            var crossings = new List<Crossing>();
            var groups = new HashSet<MeshGroupKey> { startGroup, endGroup };
            if (detour && startGroup.IsOutdoor)
                groups.UnionWith(GetDetourGroups(graph, start, end));
            var pending = new Stack<MeshGroupKey>(groups);

            while (pending.TryPop(out var group))
            {
                // the outdoors is entered through the openings of the interior groups
                if (group.IsOutdoor || group.IsNone)
                    continue;

                // one lookup: an unloaded landblock's groups (and touching openings) are built anew on every call
                var landblockGroups = GetGroups(group.LandblockId);
                if (!landblockGroups.ByIndex.TryGetValue(group.Index, out var cellGroup))
                    continue;

                landblockGroups.TouchingOpenings.TryGetValue(group.Index, out var touching);
                var tileSummary = graph?.Summaries[group.LandblockId >> 16];

                var openings = GetOpenings(group, cellGroup, agentSize, out var components);
                for (var d = 0; d < openings.Count; d++)
                {
                    var opening = openings[d];
                    var component = components?[d] ?? -1;
                    var threshold = new Position(opening.CellId, opening.Position, Quaternion.Identity);
                    var thresholdAt = ToGlobal(group.LandblockId, opening.Position);

                    // an opening into another group's leads there, never outside
                    var pairs = touching?.Where(t => IsSameOpening(t.opening, opening)).ToList();
                    if (pairs == null || pairs.Count == 0)
                    {
                        var area = -1;
                        if (tileSummary?.Method == ReachabilityMethod.Recast)
                        {
                            // no outdoor floor in front of it, as the bake judged (a wall outside, a window above the ground)
                            var front = tileSummary.Openings.FindIndex(s => s.Group == group.Index && s.Opening < cellGroup.Openings.Count && IsSameOpening(cellGroup.Openings[s.Opening], opening));
                            if (front < 0)
                                continue;

                            area = graph.GetOutdoorArea(new GraphSpot((int)(group.LandblockId >> 16), GraphSpot.Outdoors, tileSummary.Openings[front].Piece));
                        }

                        var outsideAt = ToGlobal(group.LandblockId, GetOpeningOutsidePoint(opening, agentSize));
                        var outside = CreateOutdoorPosition(outsideAt);
                        crossings.Add(new Crossing(group, MeshGroupKey.Outdoors, threshold, thresholdAt, outside, outsideAt, component, area));
                        crossings.Add(new Crossing(MeshGroupKey.Outdoors, group, outside, outsideAt, threshold, thresholdAt, area, component));
                        continue;
                    }

                    foreach (var (_, other, otherOpening) in pairs)
                    {
                        if (!TryGetCellGroup(other, out var otherCellGroup))
                            continue;

                        var otherOpenings = GetOpenings(other, otherCellGroup, agentSize, out var otherComponents);
                        var otherIndex = otherOpenings.FindIndex(o => IsSameOpening(o, otherOpening));
                        if (otherIndex < 0)
                            continue;

                        var otherThreshold = new Position(otherOpening.CellId, otherOpening.Position, Quaternion.Identity);
                        crossings.Add(new Crossing(group, other, threshold, thresholdAt, otherThreshold, ToGlobal(other.LandblockId, otherOpening.Position), component, otherComponents?[otherIndex] ?? -1));

                        if (groups.Add(other))
                            pending.Push(other);
                    }
                }
            }

            // Dijkstra backwards from the end, measured between the points where legs meet the crossings
            var endAt = ToGlobal(end);
            var remaining = new float[crossings.Count];
            Array.Fill(remaining, float.MaxValue);
            var byTo = Enumerable.Range(0, crossings.Count).ToLookup(i => crossings[i].To);

            // the end's component only narrows the choice: an outdoor area comes from a 2 m map cell, which near a wall
            // or an edge can lie on the far side of it (see GetSummaryPiece)
            var queue = new PriorityQueue<int, float>();
            var into = Enumerable.Range(0, crossings.Count).Where(i => crossings[i].To == endGroup).ToList();
            var matching = into.Where(i => SameComponent(crossings[i].FarComponent, endComponent)).ToList();
            var exits = matching.Count > 0 ? matching : into;

            if (detour)
            {
                // a detour needs exits a path leads from to the end: an opening back onto the start's side often looks
                // the closest
                exits = LeadingToEnd(exits);
                if (exits.Count == 0)
                    exits = LeadingToEnd(into.Except(matching));
            }

            // those of the nearest few openings that a path leads from to the end
            List<int> LeadingToEnd(IEnumerable<int> candidates)
            {
                var tried = new List<Vector3>();
                var leading = new List<int>();
                foreach (var i in candidates.OrderBy(i => Vector3.Distance(crossings[i].FarAt, endAt)))
                {
                    var at = crossings[i].FarAt;
                    if (tried.Any(t => Vector3.Distance(t, at) < SameOpeningDistance))
                        continue;

                    tried.Add(at);
                    FindRoute(crossings[i].Far, endGroup, new List<(Position, float)> { (end, 0f) }, agentSize, profile, out var reached);
                    if (reached >= 0)
                        leading.Add(i);

                    if (tried.Count == MaxOpeningCandidates * MaxOpeningBatches)
                        break;
                }
                return leading;
            }

            foreach (var i in exits)
            {
                remaining[i] = Vector3.Distance(crossings[i].NearAt, endAt);
                queue.Enqueue(i, remaining[i]);
            }

            while (queue.TryDequeue(out var next, out var nextRemaining))
            {
                if (nextRemaining > remaining[next])
                    continue;

                foreach (var i in byTo[crossings[next].From])
                {
                    // a detour passes the group it starts and ends in only at those two ends
                    if (detour && crossings[i].To == endGroup)
                        continue;

                    // straight back where it came from never leads on, but its estimate would pass for a way through. A
                    // detour does come back, through another opening.
                    if (crossings[i].From == crossings[next].To && !(detour && crossings[next].To == endGroup && Vector3.Distance(crossings[i].FarAt, crossings[next].NearAt) >= SameOpeningDistance))
                        continue;

                    // inside a group, only between openings on the same mesh component
                    if (!SameComponent(crossings[i].FarComponent, crossings[next].NearComponent))
                        continue;

                    var through = nextRemaining + Vector3.Distance(crossings[i].NearAt, crossings[next].NearAt);
                    if (through < remaining[i])
                    {
                        remaining[i] = through;
                        queue.Enqueue(i, through);
                    }
                }
            }

            return Enumerable.Range(0, crossings.Count)
                .Where(i => remaining[i] < float.MaxValue)
                .ToLookup(i => crossings[i].From, i => (crossings[i], remaining[i]));
        }

        /// <summary>
        /// The buildings an outdoor detour (see PlanCrossings) may go through: those of the start's and end's landblocks
        /// with at least two openings onto the start's or end's outdoor area. Not every building around, as planning
        /// through one loads its mesh, and creatures chasing a target they can't reach plan detours.
        /// </summary>
        private static IEnumerable<MeshGroupKey> GetDetourGroups(WorldGraph graph, Position start, Position end)
        {
            var startArea = GetOutdoorArea(graph, start);
            var endArea = GetOutdoorArea(graph, end);

            foreach (var landblockId in new[] { start.Cell & 0xFFFF0000, end.Cell & 0xFFFF0000 }.Distinct())
            {
                var tileSummary = graph?.Summaries[landblockId >> 16];

                foreach (var cellGroup in GetGroups(landblockId).ByIndex.Values)
                {
                    var openings = cellGroup.Openings;
                    if (tileSummary?.Method == ReachabilityMethod.Recast)
                    {
                        openings = tileSummary.Openings
                            .Where(s => s.Group == cellGroup.Index && s.Opening < cellGroup.Openings.Count)
                            .Where(s =>
                            {
                                var area = graph.GetOutdoorArea(new GraphSpot((int)(landblockId >> 16), GraphSpot.Outdoors, s.Piece));
                                return SameComponent(area, startArea) || SameComponent(area, endArea);
                            })
                            .Select(s => cellGroup.Openings[s.Opening])
                            .ToList();
                    }

                    if (openings.Any(d => Vector3.Distance(d.Position, openings[0].Position) >= SameOpeningDistance))
                        yield return new MeshGroupKey(landblockId, cellGroup.Index);
                }
            }
        }

        /// <summary>
        /// The crossings out of a group a leg from start could head for (see PlanCrossings): the most promising few by
        /// straight-line estimate of the whole route, best first, for FindRoute to compare by actual path length, in
        /// batches of MaxOpeningCandidates. None leads back onto a group's component the route has walked (visited, -1
        /// for any of it): its estimate runs through that group's walls. Only openings on the start's mesh component are
        /// used, -1 for any (see Crossing).
        /// </summary>
        private static List<(Crossing crossing, float remaining)> GetCrossingCandidates(ILookup<MeshGroupKey, (Crossing crossing, float remaining)> plan, Position start, int component, MeshGroupKey group, List<(MeshGroupKey group, int component)> visited)
        {
            var startAt = ToGlobal(start);
            var candidates = new List<(Crossing crossing, float remaining)>(MaxOpeningCandidates * MaxOpeningBatches);
            var reachable = plan[group].Where(c => SameComponent(component, c.crossing.NearComponent) && !visited.Any(v => v.group == c.crossing.To && SameComponent(v.component, c.crossing.FarComponent)));

            foreach (var candidate in reachable.OrderBy(c => Vector3.Distance(startAt, c.crossing.NearAt) + c.remaining))
            {
                if (candidates.Any(c => Vector3.Distance(c.crossing.NearAt, candidate.crossing.NearAt) < SameOpeningDistance))
                    continue;

                candidates.Add(candidate);
                if (candidates.Count == MaxOpeningCandidates * MaxOpeningBatches)
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// The outdoor area of an outdoor position (see WorldGraph.GetOutdoorArea), -1 indoors or when not known.
        /// </summary>
        private static int GetOutdoorArea(WorldGraph graph, Position position) =>
            graph == null || position.Indoors ? -1 : graph.GetOutdoorArea(GetGraphSpot(graph.Summaries, graph.Interiors, position));

        /// <summary>
        /// The openings of a landblock's cell groups that open straight into another group's opening (see
        /// WorldGraph.OpeningsTouch: buildings built against each other, also across a landblock border), by group
        /// index. Their outdoor side lies inside the other group.
        /// </summary>
        private static Dictionary<int, List<(CellGroupOpening opening, MeshGroupKey other, CellGroupOpening otherOpening)>> FindTouchingOpenings(uint landblockId, IEnumerable<CellGroup> groups)
        {
            var touching = new Dictionary<int, List<(CellGroupOpening opening, MeshGroupKey other, CellGroupOpening otherOpening)>>();

            try
            {
                var (x, y) = GetLandblockCoords(landblockId);
                var own = groups.Select(g => (key: new MeshGroupKey(landblockId, g.Index), group: g)).ToList();

                // read from the dat once per scan: an unloaded neighbour's groups aren't cached
                var neighbours = new Dictionary<uint, List<(MeshGroupKey key, CellGroup group)>>();

                foreach (var (key, group) in own)
                {
                    foreach (var opening in group.Openings)
                    {
                        // the neighbouring landblocks only for an opening on their edge
                        var dx = opening.Position.X <= WorldGraph.TouchingOpeningDistance ? -1 : opening.Position.X >= Position.BlockLength - WorldGraph.TouchingOpeningDistance ? 1 : 0;
                        var dy = opening.Position.Y <= WorldGraph.TouchingOpeningDistance ? -1 : opening.Position.Y >= Position.BlockLength - WorldGraph.TouchingOpeningDistance ? 1 : 0;
                        var others = own.Where(o => o.key != key);
                        foreach (var (nx, ny) in new[] { (x + dx, y), (x, y + dy), (x + dx, y + dy) }.Distinct())
                        {
                            if ((nx, ny) == (x, y) || nx < 0 || ny < 0 || nx >= 0xFF || ny >= 0xFF)
                                continue;

                            var neighbourId = GetLandblockId(nx, ny);
                            if (!neighbours.TryGetValue(neighbourId, out var neighbourGroups))
                                neighbours[neighbourId] = neighbourGroups = GetGroups(neighbourId).ByIndex.Values.Select(g => (new MeshGroupKey(neighbourId, g.Index), g)).ToList();
                            others = others.Concat(neighbourGroups);
                        }

                        var at = ToGlobal(landblockId, opening.Position);
                        foreach (var (otherKey, otherGroup) in others)
                        {
                            foreach (var otherOpening in otherGroup.Openings)
                            {
                                if (!WorldGraph.OpeningsTouch(ToGlobal(otherKey.LandblockId, otherOpening.Position), otherOpening.Normal, at, opening.Normal))
                                    continue;

                                if (!touching.TryGetValue(group.Index, out var list))
                                    touching[group.Index] = list = new List<(CellGroupOpening, MeshGroupKey, CellGroupOpening)>();
                                list.Add((opening, otherKey, otherOpening));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // routes then treat these openings as leading outside, rather than failing on every query
                log.Error($"Pathfinder: failed to find the touching openings of landblock 0x{landblockId:X8}: {e}");
            }

            return touching;
        }

        /// <summary>
        /// Finds a route that may cross between mesh groups through openings, by chaining FindRoute calls: each leg
        /// runs on one group's mesh, and when it ends at an opening the next leg starts on the other side of it. The
        /// openings are planned first (see PlanCrossings). Within one group the end is routed to directly, and through
        /// openings only if that doesn't reach it (see FindDetourLegs; not for RouteProfile.Road, whose follower would
        /// cut opening waypoints short). Returns the legs in order (the last one may be partial), or null if not even
        /// the first leg was found. Outdoor tiles load in the background, so a leg through the outdoors can be missing
        /// or partial the first time.
        /// </summary>
        /// <param name="reachesEnd">True if the legs lead all the way to the end position</param>
        public static List<List<Position>> FindRouteLegs(Position start, Position end, AgentSize agentSize, out bool reachesEnd, RouteProfile profile = RouteProfile.Default)
        {
            reachesEnd = false;

            try
            {
                var group = GetGroupKey(start);
                var endGroup = GetGroupKey(end);
                if (group.IsNone || endGroup.IsNone)
                    return null;

                // read once: a bake swaps in a new graph, and area numbers differ between graphs
                var graph = reachabilityGraph;
                var plan = group == endGroup ? null : PlanCrossings(graph, start, group, end, endGroup, GetOutdoorArea(graph, end), agentSize, profile);
                var legs = FollowPlan(graph, plan, start, group, end, endGroup, agentSize, profile, out reachesEnd);

                // creatures replan towards targets they can't reach: no detour where the direct search already shows
                // none can help (not even a first leg, or an end beyond the search span)
                if (group == endGroup && !reachesEnd && legs != null && profile != RouteProfile.Road && (!group.IsOutdoor || IsWithinRouteSpan(start, end, profile)))
                {
                    var detourLegs = FindDetourLegs(graph, start, end, group, agentSize, profile);
                    if (detourLegs != null)
                    {
                        reachesEnd = true;
                        return detourLegs;
                    }
                }

                return legs;
            }
            catch (Exception e)
            {
                // creatures call this from their ticks: a failed search is no route, rather than an exception that ends the server
                log.Error($"Pathfinder: failed to find a route from {start.ToLOCString()} to {end.ToLOCString()}: {e}");

                reachesEnd = false;
                return null;
            }
        }

        /// <summary>
        /// The legs of a detour between two positions of one mesh group (see PlanCrossings), or null if none reaches the
        /// end.
        /// </summary>
        private static List<List<Position>> FindDetourLegs(WorldGraph graph, Position start, Position end, MeshGroupKey group, AgentSize agentSize, RouteProfile profile)
        {
            try
            {
                var plan = PlanCrossings(graph, start, group, end, group, GetOutdoorArea(graph, end), agentSize, profile);
                var legs = FollowPlan(graph, plan, start, group, end, group, agentSize, profile, out var reachesEnd);
                return reachesEnd ? legs : null;
            }
            catch (Exception e)
            {
                // the direct search's partial route still stands
                log.Error($"Pathfinder: failed to find a detour from {start.ToLOCString()} to {end.ToLOCString()}: {e}");
                return null;
            }
        }

        private static bool IsWithinRouteSpan(Position a, Position b, RouteProfile profile)
        {
            var (ax, ay) = GetLandblockCoords(a.Cell);
            var (bx, by) = GetLandblockCoords(b.Cell);
            var span = GetRouteLandblockSpan(profile);
            return Math.Abs(ax - bx) <= span && Math.Abs(ay - by) <= span;
        }

        /// <summary>
        /// The legs of a route along a plan (see PlanCrossings), or the single leg to the end without one. Null if not
        /// even the first leg was found.
        /// </summary>
        private static List<List<Position>> FollowPlan(WorldGraph graph, ILookup<MeshGroupKey, (Crossing crossing, float remaining)> plan, Position start, MeshGroupKey group, Position end, MeshGroupKey endGroup, AgentSize agentSize, RouteProfile profile, out bool reachesEnd)
        {
            reachesEnd = false;

            var visited = new List<(MeshGroupKey group, int component)>();
            var legs = new List<List<Position>>();
            var current = start;
            var component = plan == null ? -1 : GetOutdoorArea(graph, start);

            // as the end's (see PlanCrossings), the start's area only narrows the choice
            if (plan != null && !plan[group].Any(c => SameComponent(component, c.crossing.NearComponent)))
                component = -1;

            for (var i = 0; i < MaxRouteLegs; i++)
            {
                // a detour starts in the end's group, and heads for the end only once it is back
                var final = group == endGroup && (plan == null || i > 0);
                var candidates = final ? null : GetCrossingCandidates(plan, current, component, group, visited);
                var targets = candidates?.Select(c => (c.crossing.Near, c.remaining)).ToList() ?? new List<(Position, float)> { (end, 0f) };

                // a batch of targets at a time; if none reaches one, the partial route of the most promising batch stands
                List<Position> leg = null;
                var reached = -1;
                for (var first = 0; reached < 0 && first < targets.Count; first += MaxOpeningCandidates)
                {
                    var found = FindRoute(current, group, targets.GetRange(first, Math.Min(MaxOpeningCandidates, targets.Count - first)), agentSize, profile, out var index);
                    if (index >= 0)
                        (leg, reached) = (found, first + index);
                    else
                        leg ??= found;
                }
                if (leg == null || leg.Count == 0)
                    break;

                // A route's first waypoint is where it starts, the mesh point nearest the start position. The route's
                // follower is already there, so it is left out of the first leg. Later legs start on the other side of
                // an opening, and that point is one of the two that take the follower through it.
                if (i == 0 && leg.Count > 1)
                    leg.RemoveAt(0);

                legs.Add(leg);

                if (final)
                {
                    // the final leg, complete or as close as this mesh gets
                    reachesEnd = reached >= 0;
                    break;
                }

                if (reached < 0)
                    break; // the opening is out of reach on this mesh

                var crossing = candidates[reached].crossing;
                if (Vector3.Distance(ToGlobal(leg[^1]), crossing.NearAt) > OpeningReachDistance)
                    break; // the leg did not make it to the opening

                // the group a detour starts in stays open, as the route comes back to it
                if (group != endGroup)
                    visited.Add((group, crossing.NearComponent));
                current = crossing.Far;
                component = crossing.FarComponent;
                group = crossing.To;
            }

            return legs.Count > 0 ? legs : null;
        }

        /// <summary>
        /// The length of the route to the end (see GetRouteLength), following it through openings between mesh groups
        /// (see FindRouteLegs). Returns true if the route reaches the end; distance and legs are set either way.
        /// </summary>
        public static bool GetRouteLegsDistance(Position start, Position end, AgentSize agentSize, out float distance, out int legCount, out float endGap)
        {
            distance = 0f;
            legCount = 0;
            endGap = float.MaxValue;

            var legs = FindRouteLegs(start, end, agentSize, out var reachesEnd);
            if (legs == null)
                return false;

            legCount = legs.Count;
            var route = legs.SelectMany(l => l).ToList();
            distance = GetRouteLength(start, route);
            endGap = route[^1].DistanceTo(end);
            return reachesEnd;
        }

        /// <summary>
        /// The length of a route followed from start: the sum of the straight distances between its waypoints.
        /// </summary>
        public static float GetRouteLength(Position start, IEnumerable<Position> route)
        {
            var length = 0f;
            var previous = start;
            foreach (var point in route)
            {
                length += previous.DistanceTo(point);
                previous = point;
            }
            return length;
        }

        /// <summary>
        /// A route point in Recast space (Y up) and the poly it lies on. At the start of a ledge drop, the poly
        /// is the ledge drop's off-mesh connection, and the next waypoint is its landing.
        /// </summary>
        private readonly struct Waypoint
        {
            public readonly Vector3 Pos;
            public readonly long PolyRef;
            public readonly bool IsLedgeDropStart;

            public Waypoint(Vector3 pos, long polyRef, bool isLedgeDropStart = false)
            {
                Pos = pos;
                PolyRef = polyRef;
                IsLedgeDropStart = isLedgeDropStart;
            }
        }

        /// <summary>
        /// Straight path points come from poly vertices, whose height is only approximate. Moves the point onto the
        /// detail mesh so its height matches the real floor, which matters on stairs and ramps.
        /// </summary>
        private static Waypoint SnapToFloor(DtNavMeshQuery query, Waypoint waypoint)
        {
            // a ledge drop starts on its connection, which was placed on the floor when the mesh was built
            if (waypoint.PolyRef == 0 || waypoint.IsLedgeDropStart)
                return waypoint;

            var status = query.ClosestPointOnPoly(waypoint.PolyRef, ToRcVec3f(waypoint.Pos), out var closest, out _);
            if (status.Failed())
                return waypoint;

            return new Waypoint(ToVector3(closest), waypoint.PolyRef);
        }

        /// <summary>
        /// Moves along the mesh surface from a waypoint towards a target, stopping at the mesh boundary.
        /// Returns false if the move could not be started.
        /// </summary>
        private static bool MoveAlongSurface(DtNavMeshQuery query, IDtQueryFilter filter, Waypoint from, Vector3 target, out Waypoint reached)
        {
            reached = from;

            Span<long> visited = stackalloc long[MaxSurfaceMovePolys];
            var status = query.MoveAlongSurface(from.PolyRef, ToRcVec3f(from.Pos), ToRcVec3f(target), filter, out var resultPos, visited, out int visitedCount, visited.Length);
            if (status.Failed() || visitedCount == 0)
                return false;

            reached = new Waypoint(ToVector3(resultPos), visited[visitedCount - 1]);
            return true;
        }

        /// <summary>
        /// True if a straight line between the two waypoints stays on the mesh.
        /// </summary>
        private static bool HasLineOfSight(DtNavMeshQuery query, IDtQueryFilter filter, Waypoint from, Waypoint to)
        {
            // only the hit fraction is used, the visited polys don't need to fit
            Span<long> hitPath = stackalloc long[MaxSurfaceMovePolys];
            var status = query.Raycast(from.PolyRef, ToRcVec3f(from.Pos), ToRcVec3f(to.Pos), filter, out float t, out _, hitPath, out _, hitPath.Length);

            // t is float.MaxValue when the ray reaches the end; allow a little slack for end points on a boundary.
            return status.Succeeded() && t >= 0.99f;
        }

        /// <summary>
        /// String pulling puts every turn exactly on the mesh boundary, at the corner the path bends around. Creatures
        /// overshoot turns, so they scrape that corner and get stuck. Pushes each turn waypoint away from its corner,
        /// along the open side of the bisector of the turn, by up to CornerClearance (or half the open space in narrow
        /// corridors).
        /// </summary>
        private static void OffsetCorners(DtNavMeshQuery query, IDtQueryFilter filter, List<Waypoint> waypoints)
        {
            for (int i = 1; i < waypoints.Count - 1; i++)
            {
                var current = waypoints[i];
                if (current.PolyRef == 0)
                    continue;

                // the ends of a ledge drop stay where it was found: it was only checked from there
                if (current.IsLedgeDropStart || waypoints[i - 1].IsLedgeDropStart)
                    continue;

                // already clear of every wall (e.g. the waypoint that replaced a corner cluster)
                if (HasRoom(query, filter, current))
                    continue;

                var toPrev = Flatten(waypoints[i - 1].Pos - current.Pos);
                var toNext = Flatten(waypoints[i + 1].Pos - current.Pos);
                if (toPrev.LengthSquared() < 0.0001f || toNext.LengthSquared() < 0.0001f)
                    continue;

                // The path wraps the corner, so the bisector of both legs points into the corner (the wedge between the
                // legs); the open side of the turn, away from it, is the other way.
                var dir = -(Vector3.Normalize(toPrev) + Vector3.Normalize(toNext));
                if (dir.LengthSquared() < 0.0001f)
                    continue; // no turn
                dir = Vector3.Normalize(dir);

                // Measure the open space so a narrow corridor doesn't push the waypoint onto the opposite wall.
                if (!MoveAlongSurface(query, filter, current, current.Pos + dir * (CornerClearance * 2f), out var probe))
                    continue;

                var push = Math.Min(CornerClearance, Flatten(probe.Pos - current.Pos).Length() * 0.5f);
                if (push < 0.05f)
                    continue;

                if (!MoveAlongSurface(query, filter, current, current.Pos + dir * push, out var moved))
                    continue;

                // The legs to the moved point must not cut through another obstacle.
                if (!HasLineOfSight(query, filter, waypoints[i - 1], moved) || !HasLineOfSight(query, filter, moved, waypoints[i + 1]))
                    continue;

                waypoints[i] = moved;
            }
        }

        /// <summary>
        /// Waypoints less than this far apart, in a row, are the bends of one corner (a chamfered or stepped corner of
        /// the mesh boundary makes the path bend two or three times within a metre or two).
        /// </summary>
        private const float CornerClusterDistance = 2.0f;

        /// <summary>
        /// How far from a corner cluster, towards the open side of its turn, the single waypoint that replaces it is looked
        /// for: from CornerMergeStep to CornerMergeMaxOffset, in steps of CornerMergeStep. A point nearer than that to a
        /// 90 degree corner has less than CornerClearance of room.
        /// </summary>
        private const float CornerMergeMaxOffset = 1.6f;

        private const float CornerMergeStep = 0.2f;

        /// <summary>
        /// How far (horizontally) a candidate waypoint may be from the mesh and still count as on it.
        /// </summary>
        private const float OnMeshTolerance = 0.05f;

        /// <summary>
        /// Replaces each cluster of corner waypoints (see CornerClusterDistance) with a single waypoint, placed on the
        /// open side of the turn so that both straight legs to and from it stay on the mesh and it has CornerClearance of
        /// room around it. Creatures stop and start a move at every waypoint, so fewer of them at a corner is smoother; the
        /// route gets a little longer (under 1% on average). A cluster no such waypoint exists for is left as it is. The
        /// first and last waypoint, the ends of a ledge drop and the waypoints after a ledge drop's start are never merged.
        /// </summary>
        private static void MergeCornerClusters(DtNavMeshQuery query, IDtQueryFilter filter, List<Waypoint> waypoints)
        {
            if (waypoints.Count < 4)
                return;

            var result = new List<Waypoint>(waypoints.Count) { waypoints[0] };

            var first = 1;
            while (first < waypoints.Count - 1)
            {
                var last = first;
                if (IsMergeable(waypoints, first))
                    while (last + 1 < waypoints.Count - 1 && IsMergeable(waypoints, last + 1) &&
                           Vector3.Distance(waypoints[last].Pos, waypoints[last + 1].Pos) < CornerClusterDistance)
                        last++;

                if (last > first && TryMergeCluster(query, filter, result[result.Count - 1], waypoints, first, last, out var single))
                    result.Add(single);
                else
                    for (var i = first; i <= last; i++)
                        result.Add(waypoints[i]);

                first = last + 1;
            }

            result.Add(waypoints[waypoints.Count - 1]);

            waypoints.Clear();
            waypoints.AddRange(result);
        }

        /// <summary>
        /// True if a waypoint can belong to a corner cluster: it is on a poly, and is neither a ledge drop's start nor
        /// the landing after one.
        /// </summary>
        private static bool IsMergeable(List<Waypoint> waypoints, int index)
        {
            return waypoints[index].PolyRef != 0 && !waypoints[index].IsLedgeDropStart && !waypoints[index - 1].IsLedgeDropStart;
        }

        private static bool TryMergeCluster(DtNavMeshQuery query, IDtQueryFilter filter, Waypoint previous, List<Waypoint> waypoints, int first, int last, out Waypoint merged)
        {
            merged = default;

            var next = waypoints[last + 1];

            var count = last - first + 1;
            var centroid = Vector3.Zero;
            for (var i = first; i <= last; i++)
                centroid += waypoints[i].Pos;
            centroid /= count;

            // the open side of the turn: away from the bisector of the legs, which points into the corner
            var toPrevious = Flatten(previous.Pos - centroid);
            var toNext = Flatten(next.Pos - centroid);
            if (toPrevious.LengthSquared() < 0.0001f || toNext.LengthSquared() < 0.0001f)
                return false;

            var open = -(Vector3.Normalize(toPrevious) + Vector3.Normalize(toNext));
            var hasOpen = open.LengthSquared() >= 0.0001f;
            if (hasOpen)
                open = Vector3.Normalize(open);

            // the cluster's own waypoints and its centroid, and points out from them on the open side
            var bases = new List<Vector3>(count + 1) { centroid };
            for (var i = first; i <= last; i++)
                bases.Add(waypoints[i].Pos);

            var candidates = new List<(Vector3 pos, float length)>();
            foreach (var origin in bases)
            {
                candidates.Add((origin, Vector3.Distance(previous.Pos, origin) + Vector3.Distance(origin, next.Pos)));
                if (!hasOpen)
                    continue;

                for (var offset = CornerMergeStep; offset <= CornerMergeMaxOffset + 0.001f; offset += CornerMergeStep)
                {
                    var pos = origin + open * offset;
                    candidates.Add((pos, Vector3.Distance(previous.Pos, pos) + Vector3.Distance(pos, next.Pos)));
                }
            }

            // the shortest route through a valid candidate wins, so the cheap arithmetic goes first and the queries stop at the first valid one
            candidates.Sort((a, b) => a.length.CompareTo(b.length));

            foreach (var (pos, _) in candidates)
            {
                if (!TryFindNearestPoly(query, filter, ToRcVec3f(pos), out var polyRef, out var point))
                    continue;

                var found = ToVector3(point);
                if (Flatten(found - pos).LengthSquared() > OnMeshTolerance * OnMeshTolerance)
                    continue; // not on the mesh

                var candidate = new Waypoint(found, polyRef);
                if (!HasRoom(query, filter, candidate))
                    continue;

                if (!HasLineOfSight(query, filter, previous, candidate) || !HasLineOfSight(query, filter, candidate, next))
                    continue;

                merged = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// True if no mesh edge (a wall, or a drop) is closer to the waypoint than CornerClearance.
        /// </summary>
        private static bool HasRoom(DtNavMeshQuery query, IDtQueryFilter filter, Waypoint waypoint)
        {
            // the distance found is the search radius when there is no edge within it
            var status = query.FindDistanceToWall(waypoint.PolyRef, ToRcVec3f(waypoint.Pos), CornerClearance, filter, out var distance, out _, out _);
            return status.Succeeded() && distance >= CornerClearance - 0.001f;
        }
    }
}
