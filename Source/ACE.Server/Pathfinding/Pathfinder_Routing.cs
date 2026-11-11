//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
using ACE.Server.Pathfinding.Geometry;
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
    /// Routes: paths on one mesh, and the legs of a route that crosses between meshes through doorways.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Extra distance turn waypoints are pushed away from the corner they bend around, on top of the agent radius
        /// the mesh is already eroded by.
        /// </summary>
        private const float CORNER_CLEARANCE = 0.3f;

        private const int MAX_SURFACE_MOVE_POLYS = 64;

        /// <summary>
        /// An outdoor route searches at most this many landblocks away from its start, in each direction, and only
        /// requests the tiles in that range. Bounds the search on a server with many landblocks loaded.
        /// </summary>
        private const int MAX_ROUTE_LANDBLOCK_SPAN = 2;

        /// <summary>
        /// A drop down a ledge (see DropLinkFinder) costs this many times its length, so a creature only jumps down
        /// when walking around is clearly longer: it can't come back up the same way.
        /// </summary>
        private const float DROP_COST_FACTOR = 3.0f;

        /// <summary>
        /// Every poly, for route searches: like AllPolysFilter, with drops at DROP_COST_FACTOR.
        /// </summary>
        private static readonly DtQueryDefaultFilter RouteFilter = CreateRouteFilter();

        private static DtQueryDefaultFilter CreateRouteFilter()
        {
            var areaCosts = new float[DtDetour.DT_MAX_AREAS];
            Array.Fill(areaCosts, 1.0f);
            areaCosts[NavArea.Drop] = DROP_COST_FACTOR;
            return new DtQueryDefaultFilter(0xFFFF, 0, areaCosts);
        }

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
        /// Find a route to the end position.
        /// Positions in different mesh groups have no direct route. If one of them is outdoors and the other in a
        /// building or cave, the route leads to the doorway between them; once there the caller can continue outside
        /// (or route again inside). Otherwise there is no route.
        /// </summary>
        /// <param name="end">The ending position</param>
        /// <returns>A list of positions</returns>
        public static List<Position> FindRoute(Position start, Position end, AgentSize agentSize, bool drawRoute = false)
        {
            return FindRoute(start, end, agentSize, out _, drawRoute);
        }

        /// <summary>
        /// Find a route to the end position (see the other overload). Outdoor routes cross landblock edges, searching
        /// up to MAX_ROUTE_LANDBLOCK_SPAN landblocks away from the start. Outdoor tiles load in the background, so a
        /// route through landblocks whose tiles are not loaded yet ends at the edge of the loaded ones.
        /// </summary>
        /// <param name="reachesTarget">True if the route leads all the way to its target: the end position, or the
        /// doorway towards it when the end is in another mesh group. False for a partial route, which leads as close to
        /// the target as the mesh allows.</param>
        public static List<Position> FindRoute(Position start, Position end, AgentSize agentSize, out bool reachesTarget, bool drawRoute = false)
        {
            reachesTarget = false;

            var group = GetGroupKey(start);
            var endGroup = GetGroupKey(end);
            if (group.IsNone || endGroup.IsNone)
                return null;

            // The end itself, or the doorways that lead towards it, each with an estimate of the distance left after it.
            var candidates = endGroup == group
                ? new List<(Position target, float remaining)> { (end, 0f) }
                : GetDoorCandidates(start, group, end, endGroup, agentSize);
            if (candidates == null || candidates.Count == 0)
                return null; // e.g. two dungeon sections only connected by portals

            if (group.IsOutdoor)
            {
                foreach (var (candidate, _) in candidates)
                    RequestOutdoorTilesBetween(start, candidate);
            }

            using var lease = AcquireMesh(start, group, agentSize);
            if (lease == null)
                return null;

            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, lease.ToMesh(start), out var startRef, out var startPt, out var startOnIsland))
                return null; // the start is not on the mesh

            var pathFilter = GetPathFilter(lease, start);

            // Path to each candidate and keep the shortest complete route, counting the estimated distance left after
            // a doorway. If no candidate can be reached, use the partial route towards the most promising one.
            List<Waypoint> best = null;
            List<Waypoint> partial = null;
            var bestScore = float.MaxValue;

            foreach (var (candidate, remaining) in candidates)
            {
                var candidateWaypoints = FindWaypoints(query, pathFilter, startRef, startPt, startOnIsland, lease.ToMesh(candidate), out var reachesCandidate);
                if (candidateWaypoints == null)
                    continue;

                if (!reachesCandidate)
                {
                    partial ??= candidateWaypoints;
                    continue;
                }

                var score = GetPathLength(startPt, candidateWaypoints) + remaining;
                if (score < bestScore)
                {
                    best = candidateWaypoints;
                    bestScore = score;
                }
            }

            var waypoints = best ?? partial;
            if (waypoints == null)
                return null;
            reachesTarget = best != null;

            OffsetCorners(query, AllPolysFilter, waypoints);

            for (int i = 0; i < waypoints.Count; i++)
                waypoints[i] = SnapToFloor(query, waypoints[i]);

            // each waypoint's cell is looked up from the previous one's, which is usually a few cells away
            var positionList = new List<Position>(waypoints.Count);
            var previous = start;
            foreach (var waypoint in waypoints)
            {
                previous = new RoutePosition(CreatePosition(previous, group, ToRcVec3f(waypoint.Pos)), waypoint.IsDropStart);
                positionList.Add(previous);
            }

            if (drawRoute)
                DrawRoute(positionList);

            return positionList;
        }

        /// <summary>
        /// The filter a route search runs with: every poly, but outdoors only within MAX_ROUTE_LANDBLOCK_SPAN
        /// landblocks of the start.
        /// </summary>
        private static IDtQueryFilter GetPathFilter(MeshLease lease, Position start)
        {
            if (!lease.IsOutdoor)
                return RouteFilter;

            var (x, y) = GetLandblockCoords(start.Cell);
            return new TileRectFilter(RouteFilter, x - MAX_ROUTE_LANDBLOCK_SPAN, y - MAX_ROUTE_LANDBLOCK_SPAN, x + MAX_ROUTE_LANDBLOCK_SPAN, y + MAX_ROUTE_LANDBLOCK_SPAN);
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
            var polys = ArrayPool<long>.Shared.Rent(MAX_POLYS);
            var path = ArrayPool<DtStraightPath>.Shared.Rent(MAX_POLYS);
            try
            {
                var pathStatus = query.FindPath(startRef, endRef, startPt, endPt, pathFilter, polys, out int polyCount, MAX_POLYS);
                if (pathStatus.Failed() || polyCount == 0)
                    return null;

                // When the target is unreachable (or the corridor did not fit in the buffer, or the search ran out of
                // nodes) the corridor ends on another poly: a partial route, as close to the target as the mesh allows.
                var lastPolyRef = polys[polyCount - 1];
                var reachesEnd = lastPolyRef == endRef;

                var pathEnd = endPt;
                if (!reachesEnd && query.ClosestPointOnPoly(lastPolyRef, endPt, out var clampedEnd, out _).Succeeded())
                    pathEnd = clampedEnd;

                var straightStatus = query.FindStraightPath(startPt, pathEnd, polys, polyCount, path, out int straightPathCount, MAX_POLYS, 0);
                if (straightStatus.Failed() || straightPathCount == 0)
                    return null;

                var waypoints = new List<Waypoint>(straightPathCount + 1);
                for (int i = 0; i < straightPathCount; i++)
                {
                    var entry = path[i];
                    var isDropStart = (entry.flags & DtStraightPathFlags.DT_STRAIGHTPATH_OFFMESH_CONNECTION) != 0;

                    // the end vertex of a straight path carries no poly ref, fall back to the last poly of the corridor
                    waypoints.Add(new Waypoint(ToVector3(entry.pos), entry.refs != 0 ? entry.refs : lastPolyRef, isDropStart));

                    // The landing of a drop must be a waypoint of its own, so the drop is followed as found. String
                    // pulling can leave it out when the path goes on straight from it.
                    if (isDropStart && TryGetDropLanding(query, polys, polyCount, entry.refs, out var landing) &&
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
        /// The landing of a drop (an off-mesh connection) on a path corridor, on the poly after it.
        /// </summary>
        private static bool TryGetDropLanding(DtNavMeshQuery query, long[] polys, int polyCount, long dropRef, out Waypoint landing)
        {
            landing = default;

            var index = Array.IndexOf(polys, dropRef, 0, polyCount);
            if (index <= 0 || index + 1 >= polyCount)
                return false;

            var start = RcVec3f.Zero;
            var end = RcVec3f.Zero;
            var status = query.GetAttachedNavMesh().GetOffMeshConnectionPolyEndPoints(polys[index - 1], dropRef, ref start, ref end);
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
        /// The most doorways FindRoute paths to when picking one.
        /// </summary>
        private const int MAX_DOOR_CANDIDATES = 3;

        /// <summary>
        /// Doorways closer together than this count as one (e.g. the portals of a double door).
        /// </summary>
        private const float SAME_DOOR_DISTANCE = 1.0f;

        /// <summary>
        /// For a route between the outdoors and an interior group (or between two interior groups through the outdoors),
        /// the doorways the route from start could head for: the start group's doors when leaving it, or the end
        /// group's doors when entering it from outside. The groups may be in different landblocks. Each comes with a
        /// straight-line estimate of the distance left from it to the end. Returns the most promising few by
        /// straight-line estimate of the whole route, best first, for FindRoute to compare by actual path length. Only
        /// doorways an agent of the given size can get through are used (see GetDoors). Null if there is no doorway
        /// to use.
        /// </summary>
        private static List<(Position target, float remaining)> GetDoorCandidates(Position start, MeshGroupKey group, Position end, MeshGroupKey endGroup, AgentSize agentSize)
        {
            var startAt = ToGlobal(start);
            var endAt = ToGlobal(end);

            IEnumerable<(Position target, Vector3 at, float remaining)> doors;

            if (group.IsOutdoor)
            {
                // entering a building or cave: in front of its doorways, on the outdoor mesh
                if (!TryGetCellGroup(endGroup, out var entering))
                    return null;

                var enteringDoors = GetDoors(endGroup, entering, agentSize);
                if (enteringDoors.Count == 0)
                    return null;

                doors = enteringDoors.Select(d =>
                {
                    var at = GetDoorApproach(d, endGroup.LandblockId, true, agentSize);
                    return (CreateOutdoorPosition(at), at, Vector3.Distance(at, endAt));
                });
            }
            else
            {
                // leaving a building or cave, towards the outdoors or another building
                if (!TryGetCellGroup(group, out var leaving))
                    return null;

                var leavingDoors = GetDoors(group, leaving, agentSize);
                if (leavingDoors.Count == 0)
                    return null;

                Func<Vector3, float> remainingFrom;
                if (endGroup.IsOutdoor)
                    remainingFrom = p => Vector3.Distance(p, endAt);
                else
                {
                    if (!TryGetCellGroup(endGroup, out var other))
                        return null;

                    var otherDoorList = GetDoors(endGroup, other, agentSize);
                    if (otherDoorList.Count == 0)
                        return null;

                    // across the outdoors to the other building's best doorway, then inside to the end
                    var otherDoors = otherDoorList.Select(d => GetDoorApproach(d, endGroup.LandblockId, true, agentSize)).ToList();
                    remainingFrom = p => otherDoors.Min(d => Vector3.Distance(p, d) + Vector3.Distance(d, endAt));
                }

                doors = leavingDoors.Select(d =>
                {
                    var at = ToGlobal(group.LandblockId, d.Position);
                    return (new Position(d.CellId, d.Position, Quaternion.Identity), at, remainingFrom(at));
                });
            }

            var candidates = new List<(Position target, float remaining)>(MAX_DOOR_CANDIDATES);
            var chosen = new List<Vector3>(MAX_DOOR_CANDIDATES);

            foreach (var door in doors.OrderBy(d => Vector3.Distance(startAt, d.at) + d.remaining))
            {
                if (chosen.Any(c => Vector3.Distance(c, door.at) < SAME_DOOR_DISTANCE))
                    continue;

                candidates.Add((door.target, door.remaining));
                chosen.Add(door.at);

                if (candidates.Count == MAX_DOOR_CANDIDATES)
                    break;
            }

            return candidates;
        }

        /// <summary>
        /// The most doorway crossings a multi-leg route follows: leaving a building, crossing outside, entering another.
        /// </summary>
        private const int MAX_ROUTE_LEGS = 4;

        /// <summary>
        /// A leg must end this close to where it was headed at a doorway (see GetDoorApproach) for the route to continue
        /// on the other side.
        /// </summary>
        private const float DOOR_REACH_DISTANCE = 2.0f;

        /// <summary>
        /// How far onto the outdoor mesh a doorway's outdoor side is placed (see GetDoorOutsidePoint).
        /// </summary>
        private const float DOOR_OUTSIDE_MARGIN = 0.3f;

        /// <summary>
        /// Where a route meets a doorway on the outdoor mesh, in landblock coordinates. The outdoor mesh keeps an agent
        /// radius from the building's walls, so in front of a doorway in a wall it starts about that far out from the
        /// threshold: the point is there, a little onto the mesh, at threshold height. A doorway that isn't in a wall
        /// has no direction out, and the point is its threshold.
        /// </summary>
        private static Vector3 GetDoorOutsidePoint(CellGroupDoor door, AgentSize agentSize)
        {
            return door.Position + door.Normal * (GetAgentRadius(agentSize) + DOOR_OUTSIDE_MARGIN);
        }

        /// <summary>
        /// Where a leg on the given side of a doorway heads for it, in world coordinates: the outdoor point (see
        /// GetDoorOutsidePoint) on the outdoor mesh, the threshold on the interior mesh, whose door apron reaches it.
        /// </summary>
        private static Vector3 GetDoorApproach(CellGroupDoor door, uint doorLandblockId, bool outdoor, AgentSize agentSize)
        {
            return ToGlobal(doorLandblockId, outdoor ? GetDoorOutsidePoint(door, agentSize) : door.Position);
        }

        /// <summary>
        /// Finds a route that may cross between mesh groups through doorways, by chaining FindRoute calls: each leg
        /// runs on one group's mesh, and when it ends at a doorway the next leg starts on the other side of it.
        /// Returns the legs in order (the last one may be partial), or null if not even the first leg was found.
        /// Outdoor tiles load in the background, so a leg through the outdoors can be missing or partial the first time.
        /// </summary>
        public static List<List<Position>> FindRouteLegs(Position start, Position end, AgentSize agentSize)
        {
            return FindRouteLegs(start, end, agentSize, out _);
        }

        /// <summary>
        /// Like the other overload; reachesEnd is true if the legs lead all the way to the end position.
        /// </summary>
        public static List<List<Position>> FindRouteLegs(Position start, Position end, AgentSize agentSize, out bool reachesEnd)
        {
            reachesEnd = false;

            var legs = new List<List<Position>>();
            var endGroup = GetGroupKey(end);
            var current = start;

            for (var i = 0; i < MAX_ROUTE_LEGS; i++)
            {
                var leg = FindRoute(current, end, agentSize, out var legReachesTarget);
                if (leg == null || leg.Count == 0)
                    break;

                legs.Add(leg);

                var group = GetGroupKey(current);
                if (group == endGroup)
                {
                    // the final leg, complete or as close as this mesh gets
                    reachesEnd = legReachesTarget;
                    break;
                }

                if (!legReachesTarget)
                    break; // the doorway is out of reach on this mesh

                current = GetOtherSideOfDoorway(group, leg[leg.Count - 1], endGroup, agentSize);
                if (current == null)
                    break;
            }

            return legs.Count > 0 ? legs : null;
        }

        /// <summary>
        /// For a leg that ended at a doorway, returns the start of the next leg, on the other side of it: in front of the
        /// doorway outdoors when leaving a building (see GetDoorOutsidePoint), at its threshold in the building's door
        /// cell when entering it.
        /// </summary>
        private static Position GetOtherSideOfDoorway(MeshGroupKey group, Position legEnd, MeshGroupKey endGroup, AgentSize agentSize)
        {
            // leaving: our own doorway; entering from outside: the destination group's doorway
            var doorGroup = group.IsOutdoor ? endGroup : group;
            if (!TryGetCellGroup(doorGroup, out var cellGroup))
                return null;

            var doors = GetDoors(doorGroup, cellGroup, agentSize);
            if (doors.Count == 0)
                return null;

            // the doorway the leg headed for, from the side the leg ran on
            var legEndAt = ToGlobal(legEnd);
            var door = doors.OrderBy(d => Vector3.DistanceSquared(GetDoorApproach(d, doorGroup.LandblockId, group.IsOutdoor, agentSize), legEndAt)).First();
            if (Vector3.Distance(GetDoorApproach(door, doorGroup.LandblockId, group.IsOutdoor, agentSize), legEndAt) > DOOR_REACH_DISTANCE)
                return null; // the leg did not make it to the doorway

            return group.IsOutdoor
                ? new Position(door.CellId, door.Position, Quaternion.Identity)
                : CreateOutdoorPosition(GetDoorApproach(door, doorGroup.LandblockId, true, agentSize));
        }

        /// <summary>
        /// Like GetRouteDistance, but follows the route through doorways between mesh groups (see FindRouteLegs).
        /// Returns true if the route reaches the end; distance and legs are set either way.
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
        /// A route point in Recast space (Y up) and the poly it lies on. At the start of a drop down a ledge, the poly
        /// is the drop's off-mesh connection, and the next waypoint is its landing.
        /// </summary>
        private readonly struct Waypoint
        {
            public readonly Vector3 Pos;
            public readonly long PolyRef;
            public readonly bool IsDropStart;

            public Waypoint(Vector3 pos, long polyRef, bool isDropStart = false)
            {
                Pos = pos;
                PolyRef = polyRef;
                IsDropStart = isDropStart;
            }
        }

        /// <summary>
        /// Straight path points come from poly vertices, whose height is only approximate. Moves the point onto the
        /// detail mesh so its height matches the real floor, which matters on stairs and ramps.
        /// </summary>
        private static Waypoint SnapToFloor(DtNavMeshQuery query, Waypoint waypoint)
        {
            // a drop starts on its connection, which was placed on the floor when the mesh was built
            if (waypoint.PolyRef == 0 || waypoint.IsDropStart)
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

            Span<long> visited = stackalloc long[MAX_SURFACE_MOVE_POLYS];
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
            Span<long> hitPath = stackalloc long[MAX_SURFACE_MOVE_POLYS];
            var status = query.Raycast(from.PolyRef, ToRcVec3f(from.Pos), ToRcVec3f(to.Pos), filter, out float t, out _, hitPath, out _, hitPath.Length);

            // t is float.MaxValue when the ray reaches the end; allow a little slack for end points on a boundary.
            return status.Succeeded() && t >= 0.99f;
        }

        /// <summary>
        /// String pulling puts every turn exactly on the mesh boundary, at the corner the path bends around. Creatures
        /// overshoot turns, so they scrape that corner and get stuck. Pushes each turn waypoint away from its corner,
        /// along the bisector of the turn, by up to CORNER_CLEARANCE (or half the open space in narrow corridors).
        /// </summary>
        private static void OffsetCorners(DtNavMeshQuery query, IDtQueryFilter filter, List<Waypoint> waypoints)
        {
            for (int i = 1; i < waypoints.Count - 1; i++)
            {
                var current = waypoints[i];
                if (current.PolyRef == 0)
                    continue;

                // the ends of a drop stay where the drop was found: it was only checked from there
                if (current.IsDropStart || waypoints[i - 1].IsDropStart)
                    continue;

                var toPrev = Flatten(waypoints[i - 1].Pos - current.Pos);
                var toNext = Flatten(waypoints[i + 1].Pos - current.Pos);
                if (toPrev.LengthSquared() < 0.0001f || toNext.LengthSquared() < 0.0001f)
                    continue;

                // The bisector of both legs points into the open side of the turn, away from the corner.
                var dir = Vector3.Normalize(toPrev) + Vector3.Normalize(toNext);
                if (dir.LengthSquared() < 0.0001f)
                    continue; // no turn
                dir = Vector3.Normalize(dir);

                // Measure the open space so a narrow corridor doesn't push the waypoint onto the opposite wall.
                if (!MoveAlongSurface(query, filter, current, current.Pos + dir * (CORNER_CLEARANCE * 2f), out var probe))
                    continue;

                var push = Math.Min(CORNER_CLEARANCE, Flatten(probe.Pos - current.Pos).Length() * 0.5f);
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

        public static bool GetRouteDistance(Position start, Position end, AgentSize agentSize, out float distance)
        {
            distance = 0f;

            // a route into another mesh group only leads to the doorway, see GetRouteLegsDistance for those
            if (GetGroupKey(start) != GetGroupKey(end))
                return false;

            var route = FindRoute(start, end, agentSize, out var reachesTarget);
            if (route == null || route.Count == 0 || !reachesTarget)
                return false;

            distance = GetRouteLength(start, route);
            return true;
        }
    }
}
