//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Geometry;
using ACE.Server.WorldObjects;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Toolset;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE.Server.Pathfinding
{
    public enum AgentWidth
    {
        Narrow,
        Wide
    }

    /// <summary>
    /// Each landblock has one mesh for its outdoors (group 0: terrain, building shells, objects) and one mesh per group
    /// of interior cells connected through cell portals (buildings, caves, dungeons). Interior groups never share a
    /// mesh, so spaces that overlap without being connected (e.g. the basements of two neighboring houses) cannot
    /// interfere with each other.
    /// </summary>
    public static class Pathfinder
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int VERTS_PER_POLY = 6;
        private const int MAX_POLYS = 256;

        /// <summary>
        /// The agent radius each mesh is built for: how far its walkable area keeps from walls.
        /// </summary>
        public const float NarrowAgentRadius = 0.6f;
        public const float WideAgentRadius = 1.1f;

        /// <summary>
        /// The group index of the outdoors. Interior groups start at 1.
        /// </summary>
        public const int OutdoorGroup = 0;

        /// <summary>
        /// Group index for positions that are in no known group (no mesh).
        /// </summary>
        private const int NoGroup = -1;

        /// <summary>
        /// Group indices are packed above the agent width in the low 16 bits of a mesh id.
        /// </summary>
        private const int MaxGroup = 0xFFF;

        /// <summary>
        /// Waypoints sit on the floor; the cell lookup probes slightly above it so the point is inside the cell volume.
        /// </summary>
        private const float CELL_PROBE_HEIGHT = 0.25f;

        /// <summary>
        /// Extra distance turn waypoints are pushed away from the corner they bend around, on top of the agent radius
        /// the mesh is already eroded by.
        /// </summary>
        private const float CORNER_CLEARANCE = 0.3f;

        private const int MAX_SURFACE_MOVE_POLYS = 64;

        /// <summary>
        /// How far around a position to look for the mesh. Positions further off the mesh than this are not on it.
        /// </summary>
        private static readonly RcVec3f QueryHalfExtents = new RcVec3f(1.25f, 1.25f, 1.25f);

        /// <summary>
        /// Part of the mesh cache file names. Build setting changes are picked up automatically; bump this when the
        /// exported geometry changes, so cached meshes built from the old geometry are not loaded.
        /// </summary>
        private const int MESH_GEOMETRY_VERSION = 1;

        /// <summary>
        /// After an outdoor mesh build fails, no new attempt is made for this long (unless a rebuild is requested).
        /// </summary>
        private static readonly TimeSpan OutdoorBuildRetryDelay = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Per landblock (0xAAAA0000), when its last outdoor mesh build failed.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, DateTime> OutdoorBuildFailures = new ConcurrentDictionary<uint, DateTime>();

        private static readonly ConcurrentDictionary<(bool outdoor, AgentWidth agentWidth), string> MeshCacheKeys = new ConcurrentDictionary<(bool, AgentWidth), string>();

        /// <summary>
        /// Poly flag for islands: parts of a mesh that cannot be walked to from its main area or its doorways, like the
        /// top of a table, a crate or a large rock. Set when a mesh is loaded, not saved in the mesh files.
        /// </summary>
        private const int POLYFLAG_ISLAND = 0x0100;

        /// <summary>
        /// Outdoor mesh components with a vertex this close to the landblock edge are not islands: they may continue
        /// into the neighboring landblock. Covers the mesh erosion at the edge (up to the widest agent radius).
        /// </summary>
        private const float LANDBLOCK_EDGE_TOLERANCE = 2.0f;

        /// <summary>
        /// How far around a doorway to look for the mesh poly that anchors its component (doorways are not on the
        /// eroded mesh themselves).
        /// </summary>
        private static readonly RcVec3f DoorSeedHalfExtents = new RcVec3f(2.0f, 2.0f, 2.0f);

        /// <summary>
        /// Every poly, islands included.
        /// </summary>
        private static readonly DtQueryDefaultFilter AllPolysFilter = new DtQueryDefaultFilter();

        /// <summary>
        /// Every poly except islands.
        /// </summary>
        private static readonly DtQueryDefaultFilter ReachableFilter = new DtQueryDefaultFilter(0xFFFF, POLYFLAG_ISLAND, Array.Empty<float>());

        /// <summary>
        /// A query holds per-search state and is not thread safe, while a mesh can be shared. Queries are pooled per
        /// mesh instead of created per call. The table holds meshes weakly, so an unloaded mesh's pool goes with it.
        /// </summary>
        private static readonly ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>> QueryPools = new ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>>();

        private const int MAX_POOLED_QUERIES_PER_MESH = 8;

        /// <summary>
        /// Smallest walkable region kept, and the size below which regions are merged into their neighbors, in m².
        /// In world units so they don't change with the cell size.
        /// </summary>
        private const float MIN_REGION_AREA = 0.64f;
        private const float MERGE_REGION_AREA = 4.0f;

        public static string InteriorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Interiors");
        public static string OutdoorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Outdoors");
        public static readonly ConcurrentDictionary<uint, DtNavMesh?> Meshes = new ConcurrentDictionary<uint, DtNavMesh?>();

        /// <summary>
        /// Per landblock (0xAAAA0000), its interior cell groups.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, LandblockGroups> Groups = new ConcurrentDictionary<uint, LandblockGroups>();

        private class LandblockGroups
        {
            public readonly Dictionary<int, CellGroup> ByIndex = new Dictionary<int, CellGroup>();
            public readonly Dictionary<uint, int> CellToGroup = new Dictionary<uint, int>();
            private readonly ConcurrentDictionary<uint, ACE.Server.Physics.Common.EnvCell> physicsCells = new ConcurrentDictionary<uint, ACE.Server.Physics.Common.EnvCell>();

            public LandblockGroups(uint landblockId)
            {
                foreach (var group in new LandblockGeometry(landblockId).CellGroups)
                {
                    if (group.Index > MaxGroup)
                    {
                        log.Warn($"Pathfinder: landblock 0x{landblockId:X8} has more than {MaxGroup} cell groups, the rest get no mesh");
                        break;
                    }

                    ByIndex.Add(group.Index, group);
                    foreach (var cellId in group.CellIds)
                        CellToGroup[cellId] = group.Index;
                }
            }

            private ACE.Server.Physics.Common.EnvCell GetPhysicsCell(uint cellId)
            {
                return physicsCells.GetOrAdd(cellId, id => ACE.Server.Physics.Common.DBObj.GetEnvCell(id));
            }

            /// <summary>
            /// Finds the cell of a group that contains a point, trying the reference cell first and then the group's
            /// cells nearest to the point. Only this group's cells are considered, so an overlapping cell of an
            /// unconnected group is never returned. Returns 0 if no cell contains the point.
            /// </summary>
            public uint FindCell(int groupIndex, uint referenceCell, Vector3 point)
            {
                if (!ByIndex.TryGetValue(groupIndex, out var group))
                    return 0;

                if (group.CellIds.Contains(referenceCell) && GetPhysicsCell(referenceCell)?.point_in_cell(point) == true)
                    return referenceCell;

                foreach (var cell in group.Cells.OrderBy(c => Vector3.DistanceSquared(c.EnvCell.Position.Origin, point)))
                {
                    if (cell.CellId != referenceCell && GetPhysicsCell(cell.CellId)?.point_in_cell(point) == true)
                        return cell.CellId;
                }
                return 0;
            }
        }

        static Pathfinder()
        {
            if (!Directory.Exists(InteriorMeshDirectory))
            {
                Directory.CreateDirectory(InteriorMeshDirectory);
            }
            if (!Directory.Exists(OutdoorMeshDirectory))
            {
                Directory.CreateDirectory(OutdoorMeshDirectory);
            }
        }

        public static bool PathfindingEnabled { get => PropertyManager.GetBool("pathfinding").Item; }

        public static WorldObject CreateMarker(Position position, string customName = "")
        {
            var marker = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.pathfinderHelper);

            if (marker == null)
                return null;

            if (customName != "")
                marker.Name = customName;

            marker.Location = position;
            marker.Location.LandblockId = new LandblockId(marker.Location.GetCell());
            var landblock = LandblockManager.GetLandblock(marker.Location.LandblockId, false);

            if (marker.EnterWorld())
                return marker;

            marker.Destroy();
            return null;
        }

        public static void DrawRoute(List<Position> route)
        {
            if (route == null || route.Count == 0)
                return;

            foreach (var entry in route)
            {
                CreateMarker(entry);
            }
        }

        private static LandblockGroups GetGroups(uint cell)
        {
            return Groups.GetOrAdd(cell & 0xFFFF0000, id => new LandblockGroups(id));
        }

        /// <summary>
        /// Returns the mesh group a position belongs to: OutdoorGroup for the landscape, the index of its connected cell
        /// group for interior cells, or -1 for an unknown cell.
        /// </summary>
        public static int GetMeshGroup(Position pos)
        {
            if (!pos.Indoors)
                return OutdoorGroup;

            return GetGroups(pos.Cell).CellToGroup.TryGetValue(pos.Cell, out var group) ? group : NoGroup;
        }

        private static uint GetMeshId(uint cell, int group, AgentWidth agentWidth)
        {
            return (cell & 0xFFFF0000) + ((uint)group << 4) + (uint)agentWidth;
        }

        /// <summary>
        /// Find a route to the end position.
        /// Positions in different mesh groups have no direct route. If one of them is outdoors and the other in a
        /// building or cave, the route leads to the doorway between them; once there the caller can continue outside
        /// (or route again inside). Otherwise there is no route.
        /// </summary>
        /// <param name="end">The ending position</param>
        /// <returns>A list of positions</returns>
        public static List<Position>? FindRoute(Position start, Position end, AgentWidth agentWidth, bool drawRoute = false)
        {
            return FindRoute(start, end, agentWidth, out _, drawRoute);
        }

        /// <summary>
        /// Find a route to the end position (see the other overload).
        /// </summary>
        /// <param name="reachesTarget">True if the route leads all the way to its target: the end position, or the
        /// doorway towards it when the end is in another mesh group. False for a partial route, which leads as close to
        /// the target as the mesh allows.</param>
        public static List<Position>? FindRoute(Position start, Position end, AgentWidth agentWidth, out bool reachesTarget, bool drawRoute = false)
        {
            reachesTarget = false;

            if ((start.Cell & 0xFFFF0000) != (end.Cell & 0xFFFF0000))
            {
                log.Warn($"FindRoute only works inside a single landblock.");
                return null;
            }

            var group = GetMeshGroup(start);
            var endGroup = GetMeshGroup(end);
            if (group == NoGroup || endGroup == NoGroup)
                return null;

            if (!TryGetMesh(start, agentWidth, out var mesh) || mesh is null)
            {
                return null;
            }

            // The end itself, or the doorways that lead towards it, each with an estimate of the distance left after it.
            var candidates = endGroup == group
                ? new List<(Position target, float remaining)> { (end, 0f) }
                : GetDoorCandidates(start, group, end, endGroup);
            if (candidates == null || candidates.Count == 0)
                return null; // e.g. two dungeon sections only connected by portals

            using var lease = RentQuery(mesh);
            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, start, out var startRef, out var startPt, out var startOnIsland))
                return null; // the start is not on the mesh

            // Path to each candidate and keep the shortest complete route, counting the estimated distance left after
            // a doorway. If no candidate can be reached, use the partial route towards the most promising one.
            List<Waypoint> best = null;
            List<Waypoint> partial = null;
            var bestScore = float.MaxValue;

            foreach (var (candidate, remaining) in candidates)
            {
                var candidateWaypoints = FindWaypoints(query, startRef, startPt, startOnIsland, candidate, out var reachesCandidate);
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

            var positionList = new List<Position>(waypoints.Count);
            foreach (var waypoint in waypoints)
                positionList.Add(CreatePosition(start, group, new Vector3(waypoint.Pos.X, waypoint.Pos.Z, waypoint.Pos.Y)));

            if(drawRoute)
                DrawRoute(positionList);

            return positionList;
        }

        /// <summary>
        /// The string-pulled path from a start poly to a target, as waypoints. reachesTarget is false for a partial
        /// path, which leads as close to the target as the mesh allows. Returns null if there is no path at all.
        /// </summary>
        private static List<Waypoint>? FindWaypoints(DtNavMeshQuery query, long startRef, RcVec3f startPt, bool startOnIsland, Position target, out bool reachesTarget)
        {
            reachesTarget = false;

            // From the main area, a target next to an island (e.g. beside a table) is snapped to the floor rather
            // than onto the island; from an island, the target is looked up anywhere.
            var endFound = startOnIsland
                ? TryFindNearestPoly(query, AllPolysFilter, target, out var endRef, out var endPt)
                : TryFindNearestReachablePoly(query, target, out endRef, out endPt, out _);
            if (!endFound)
                return null; // the target is not on the mesh

            Span<long> polys = stackalloc long[MAX_POLYS];
            var pathStatus = query.FindPath(startRef, endRef, startPt, endPt, AllPolysFilter, polys, out int polyCount, MAX_POLYS);
            if (pathStatus.Failed() || polyCount == 0)
                return null;

            // When the target is unreachable (or the corridor did not fit in the buffer, or the search ran out of
            // nodes) the corridor ends on another poly: a partial route, as close to the target as the mesh allows.
            var lastPolyRef = polys[polyCount - 1];
            var reachesEnd = lastPolyRef == endRef;

            var pathEnd = endPt;
            if (!reachesEnd && query.ClosestPointOnPoly(lastPolyRef, endPt, out var clampedEnd, out _).Succeeded())
                pathEnd = clampedEnd;

            var path = new DtStraightPath[MAX_POLYS];
            var straightStatus = query.FindStraightPath(startPt, pathEnd, polys, polyCount, path, out int straightPathCount, MAX_POLYS, 0);
            if (straightStatus.Failed() || straightPathCount == 0)
                return null;

            var waypoints = new List<Waypoint>(straightPathCount);
            for (int i = 0; i < straightPathCount; i++)
            {
                var entry = path[i];
                // the end vertex of a straight path carries no poly ref, fall back to the last poly of the corridor
                waypoints.Add(new Waypoint(ToVector3(entry.pos), entry.refs != 0 ? entry.refs : lastPolyRef));
            }

            reachesTarget = reachesEnd;
            return waypoints;
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
        /// group's doors when entering it from outside. Each comes with a straight-line estimate of the distance left
        /// from it to the end. Returns the most promising few by straight-line estimate of the whole route, best
        /// first, for FindRoute to compare by actual path length. Null if there is no doorway to use.
        /// </summary>
        private static List<(Position target, float remaining)> GetDoorCandidates(Position start, int group, Position end, int endGroup)
        {
            var groups = GetGroups(start.Cell);
            var landblockId = start.Cell & 0xFFFF0000;

            IEnumerable<(Position target, Vector3 at, float remaining)> doors;

            if (group == OutdoorGroup)
            {
                // entering a building or cave: its doorways, on the outdoor mesh
                if (!groups.ByIndex.TryGetValue(endGroup, out var entering) || entering.Doors.Count == 0)
                    return null;

                doors = entering.Doors.Select(d => (
                    new Position(GetLandCellId(landblockId, d.Position), d.Position, Quaternion.Identity),
                    d.Position,
                    Vector3.Distance(d.Position, end.Pos)));
            }
            else
            {
                // leaving a building or cave, towards the outdoors or another building
                if (!groups.ByIndex.TryGetValue(group, out var leaving) || leaving.Doors.Count == 0)
                    return null;

                Func<Vector3, float> remainingFrom;
                if (endGroup == OutdoorGroup)
                    remainingFrom = p => Vector3.Distance(p, end.Pos);
                else
                {
                    if (!groups.ByIndex.TryGetValue(endGroup, out var other) || other.Doors.Count == 0)
                        return null;

                    // across the outdoors to the other building's best doorway, then inside to the end
                    remainingFrom = p => other.Doors.Min(d => Vector3.Distance(p, d.Position) + Vector3.Distance(d.Position, end.Pos));
                }

                doors = leaving.Doors.Select(d => (
                    new Position(d.CellId, d.Position, Quaternion.Identity),
                    d.Position,
                    remainingFrom(d.Position)));
            }

            var candidates = new List<(Position target, float remaining)>(MAX_DOOR_CANDIDATES);
            var chosen = new List<Vector3>(MAX_DOOR_CANDIDATES);

            foreach (var door in doors.OrderBy(d => Vector3.Distance(start.Pos, d.at) + d.remaining))
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
        /// A leg must end this close to a doorway for the route to continue on the other side.
        /// </summary>
        private const float DOOR_REACH_DISTANCE = 2.0f;

        /// <summary>
        /// Finds a route that may cross between mesh groups through doorways, by chaining FindRoute calls: each leg
        /// runs on one group's mesh, and when it ends at a doorway the next leg starts on the other side of it.
        /// Returns the legs in order (the last one may be partial), or null if not even the first leg was found.
        /// Outdoor meshes load in the background, so a leg through the outdoors can be missing the first time.
        /// </summary>
        public static List<List<Position>>? FindRouteLegs(Position start, Position end, AgentWidth agentWidth)
        {
            return FindRouteLegs(start, end, agentWidth, out _);
        }

        /// <summary>
        /// Like the other overload; reachesEnd is true if the legs lead all the way to the end position.
        /// </summary>
        public static List<List<Position>>? FindRouteLegs(Position start, Position end, AgentWidth agentWidth, out bool reachesEnd)
        {
            reachesEnd = false;

            var legs = new List<List<Position>>();
            var endGroup = GetMeshGroup(end);
            var current = start;

            for (var i = 0; i < MAX_ROUTE_LEGS; i++)
            {
                var leg = FindRoute(current, end, agentWidth, out var legReachesTarget);
                if (leg == null || leg.Count == 0)
                    break;

                legs.Add(leg);

                var group = GetMeshGroup(current);
                if (group == endGroup || group == NoGroup)
                {
                    // the final leg, complete or as close as this mesh gets
                    reachesEnd = group == endGroup && legReachesTarget;
                    break;
                }

                if (!legReachesTarget)
                    break; // the doorway is out of reach on this mesh

                current = GetOtherSideOfDoorway(current, group, leg[leg.Count - 1], endGroup);
                if (current == null)
                    break;
            }

            return legs.Count > 0 ? legs : null;
        }

        /// <summary>
        /// For a leg that ended at a doorway of its group, returns the start of the next leg: the same spot in the
        /// group on the other side (the outdoors when leaving a building, the building's door cell when entering it).
        /// </summary>
        private static Position GetOtherSideOfDoorway(Position legStart, int group, Position legEnd, int endGroup)
        {
            var groups = GetGroups(legStart.Cell);
            var landblockId = legStart.Cell & 0xFFFF0000;

            // leaving: our own doorway; entering from outside: the destination group's doorway
            var doorGroup = group == OutdoorGroup ? endGroup : group;
            if (!groups.ByIndex.TryGetValue(doorGroup, out var cellGroup) || cellGroup.Doors.Count == 0)
                return null;

            var door = cellGroup.Doors.OrderBy(d => Vector3.DistanceSquared(d.Position, legEnd.Pos)).First();
            if (Vector3.Distance(door.Position, legEnd.Pos) > DOOR_REACH_DISTANCE)
                return null; // the leg did not make it to the doorway

            return group == OutdoorGroup
                ? new Position(door.CellId, door.Position, Quaternion.Identity)
                : new Position(GetLandCellId(landblockId, door.Position), door.Position, Quaternion.Identity);
        }

        /// <summary>
        /// Like GetRouteDistance, but follows the route through doorways between mesh groups (see FindRouteLegs).
        /// Returns true if the route reaches the end; distance and legs are set either way.
        /// </summary>
        public static bool GetRouteLegsDistance(Position start, Position end, AgentWidth agentWidth, out float distance, out int legCount, out float endGap)
        {
            distance = 0f;
            legCount = 0;
            endGap = float.MaxValue;

            var legs = FindRouteLegs(start, end, agentWidth, out var reachesEnd);
            if (legs == null)
                return false;

            legCount = legs.Count;
            var previous = start;
            foreach (var point in legs.SelectMany(l => l))
            {
                distance += previous.DistanceTo(point);
                previous = point;
            }

            endGap = previous.DistanceTo(end);
            return reachesEnd;
        }

        /// <summary>
        /// A route point in Recast space (Y up) and the poly it lies on.
        /// </summary>
        private readonly struct Waypoint
        {
            public readonly Vector3 Pos;
            public readonly long PolyRef;

            public Waypoint(Vector3 pos, long polyRef)
            {
                Pos = pos;
                PolyRef = polyRef;
            }
        }

        private static Vector3 ToVector3(RcVec3f v) => new Vector3(v.X, v.Y, v.Z);
        private static RcVec3f ToRcVec3f(Vector3 v) => new RcVec3f(v.X, v.Y, v.Z);

        private static Vector3 Flatten(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        /// <summary>
        /// Finds the mesh poly nearest to a position (within QueryHalfExtents). Returns false if the position is not on
        /// the mesh. Note FindNearestPoly succeeds with a 0 ref (and the input point) when it finds nothing.
        /// </summary>
        private static bool TryFindNearestPoly(DtNavMeshQuery query, IDtQueryFilter filter, Position pos, out long polyRef, out RcVec3f point)
        {
            var center = new RcVec3f(pos.PositionX, pos.PositionZ, pos.PositionY);
            var status = query.FindNearestPoly(center, QueryHalfExtents, filter, out polyRef, out point, out _);
            return status.Succeeded() && polyRef != 0;
        }

        /// <summary>
        /// Like TryFindNearestPoly, but prefers a poly that is not on an island. Falls back to an island poly when
        /// there is no other within reach (onIsland is then true), e.g. for a creature that stands on one.
        /// </summary>
        private static bool TryFindNearestReachablePoly(DtNavMeshQuery query, Position pos, out long polyRef, out RcVec3f point, out bool onIsland)
        {
            onIsland = false;
            if (TryFindNearestPoly(query, ReachableFilter, pos, out polyRef, out point))
                return true;

            onIsland = TryFindNearestPoly(query, AllPolysFilter, pos, out polyRef, out point);
            return onIsland;
        }

        /// <summary>
        /// A query rented from its mesh's pool, returned on Dispose. Use with 'using'.
        /// </summary>
        private readonly struct QueryLease : IDisposable
        {
            private readonly ConcurrentQueue<DtNavMeshQuery> pool;
            public readonly DtNavMeshQuery Query;

            public QueryLease(ConcurrentQueue<DtNavMeshQuery> pool, DtNavMeshQuery query)
            {
                this.pool = pool;
                Query = query;
            }

            public void Dispose()
            {
                if (pool.Count < MAX_POOLED_QUERIES_PER_MESH)
                    pool.Enqueue(Query);
            }
        }

        private static QueryLease RentQuery(DtNavMesh mesh)
        {
            var pool = QueryPools.GetValue(mesh, _ => new ConcurrentQueue<DtNavMeshQuery>());
            return new QueryLease(pool, pool.TryDequeue(out var query) ? query : new DtNavMeshQuery(mesh));
        }

        /// <summary>
        /// Flags the islands of a freshly loaded or built mesh: connected components of polys that cannot be walked to.
        /// A component is kept (not an island) if it is the largest one, holds one of the seed points (doorways), or,
        /// outdoors, reaches the landblock edge where it may continue into the neighbor. The rest are tabletops, the
        /// tops of crates and rocks, pits you can only drop into and the like.
        /// Must run before the mesh is published, since it changes poly flags.
        /// </summary>
        /// <param name="seeds">Points known to be reachable, in landblock coordinates</param>
        private static void MarkIslands(DtNavMesh mesh, IEnumerable<Vector3> seeds, bool outdoor)
        {
            var tile = mesh.GetMaxTiles() > 0 ? mesh.GetTile(0) : null;
            var data = tile?.data;
            if (data?.header == null || data.polys == null)
                return;

            var polyCount = data.header.polyCount;
            if (polyCount == 0)
                return;

            // connected components over the poly links
            var component = new int[polyCount];
            Array.Fill(component, -1);
            var componentArea = new List<float>();
            var stack = new Stack<int>();

            for (var i = 0; i < polyCount; i++)
            {
                if (component[i] != -1)
                    continue;

                var id = componentArea.Count;
                var area = 0f;
                component[i] = id;
                stack.Push(i);

                while (stack.Count > 0)
                {
                    var poly = data.polys[stack.Pop()];
                    area += GetPolyArea(data, poly);

                    for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                    {
                        mesh.GetTileAndPolyByRefUnsafe(tile.links[link].refs, out var neighborTile, out var neighbor);
                        if (neighborTile != tile || neighbor == null || component[neighbor.index] != -1)
                            continue;

                        component[neighbor.index] = id;
                        stack.Push(neighbor.index);
                    }
                }

                componentArea.Add(area);
            }

            if (componentArea.Count == 1)
                return; // all connected

            var keep = new bool[componentArea.Count];

            var largest = 0;
            for (var c = 1; c < componentArea.Count; c++)
            {
                if (componentArea[c] > componentArea[largest])
                    largest = c;
            }
            keep[largest] = true;

            using (var lease = RentQuery(mesh))
            {
                foreach (var seed in seeds)
                {
                    var status = lease.Query.FindNearestPoly(new RcVec3f(seed.X, seed.Z, seed.Y), DoorSeedHalfExtents, AllPolysFilter, out var seedRef, out _, out _);
                    if (status.Failed() || seedRef == 0)
                        continue;

                    mesh.GetTileAndPolyByRefUnsafe(seedRef, out var seedTile, out var seedPoly);
                    if (seedTile == tile && seedPoly != null)
                        keep[component[seedPoly.index]] = true;
                }
            }

            if (outdoor)
            {
                var blockLength = (float)ACE.Server.Physics.Common.LandDefs.BlockLength;
                for (var i = 0; i < polyCount; i++)
                {
                    if (keep[component[i]])
                        continue;

                    var poly = data.polys[i];
                    for (var v = 0; v < poly.vertCount; v++)
                    {
                        // Recast space: x and z are the horizontal axes
                        var x = data.verts[poly.verts[v] * 3];
                        var z = data.verts[poly.verts[v] * 3 + 2];
                        if (x <= LANDBLOCK_EDGE_TOLERANCE || z <= LANDBLOCK_EDGE_TOLERANCE || x >= blockLength - LANDBLOCK_EDGE_TOLERANCE || z >= blockLength - LANDBLOCK_EDGE_TOLERANCE)
                        {
                            keep[component[i]] = true;
                            break;
                        }
                    }
                }
            }

            var islandPolys = 0;
            for (var i = 0; i < polyCount; i++)
            {
                if (keep[component[i]])
                    continue;

                data.polys[i].flags |= POLYFLAG_ISLAND;
                islandPolys++;
            }

            if (islandPolys > 0)
                log.Debug($"Pathfinder: flagged {islandPolys} of {polyCount} polys as islands ({keep.Count(k => !k)} of {keep.Length} components)");
        }

        /// <summary>
        /// The horizontal area of a poly, in m².
        /// </summary>
        private static float GetPolyArea(DtMeshData data, DtPoly poly)
        {
            var area = 0f;
            for (int i = 0, j = poly.vertCount - 1; i < poly.vertCount; j = i++)
            {
                var a = poly.verts[j] * 3;
                var b = poly.verts[i] * 3;
                area += data.verts[a] * data.verts[b + 2] - data.verts[b] * data.verts[a + 2];
            }
            return Math.Abs(area) * 0.5f;
        }

        /// <summary>
        /// Straight path points come from poly vertices, whose height is only approximate. Moves the point onto the
        /// detail mesh so its height matches the real floor, which matters on stairs and ramps.
        /// </summary>
        private static Waypoint SnapToFloor(DtNavMeshQuery query, Waypoint waypoint)
        {
            if (waypoint.PolyRef == 0)
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
            Span<long> hitPath = stackalloc long[MAX_POLYS];
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

        public static bool GetRouteDistance(Position start, Position end, AgentWidth agentWidth, out float distance)
        {
            distance = 0f;

            // a route into another mesh group only leads to the doorway, see GetRouteLegsDistance for those
            if (GetMeshGroup(start) != GetMeshGroup(end))
                return false;

            var route = FindRoute(start, end, agentWidth, out var reachesTarget);
            if (route == null || route.Count == 0 || !reachesTarget)
                return false;

            var previousRouteEntry = start;
            foreach (var routeEntry in route)
            {
                distance += previousRouteEntry.DistanceTo(routeEntry);
                previousRouteEntry = routeEntry;
            }
            return true;
        }

        /// <summary>
        /// Get a random point on the navmesh
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static Position? GetRandomPointOnMesh(Position start, AgentWidth agentWidth)
        {
            if (!TryGetMesh(start, agentWidth, out var mesh) || mesh is null)
            {
                return null;
            }

            using var lease = RentQuery(mesh);
            var query = lease.Query;
            var frand = new RcRand(DateTime.Now.Ticks);

            // never on an island, which nothing could walk to
            var status = query.FindRandomPoint(ReachableFilter, frand, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(start, GetMeshGroup(start), new Vector3(randomPt.X, randomPt.Z, randomPt.Y));
        }

        public static Position? GetRandomPointWithinCircle(Position location, float radius, AgentWidth agentWidth)
        {
            if (!TryGetMesh(location, agentWidth, out var mesh) || mesh is null)
            {
                return null;
            }

            using var lease = RentQuery(mesh);
            var query = lease.Query;
            var frand = new RcRand(DateTime.Now.Ticks);

            if (!TryFindNearestReachablePoly(query, location, out var startRef, out var startPt, out var startOnIsland))
                return null;

            // the search spreads out from the start poly, so on an island it has to be allowed to stay on it
            var filter = startOnIsland ? AllPolysFilter : ReachableFilter;
            var status = query.FindRandomPointWithinCircle(startRef, startPt, radius, filter, frand, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(location, GetMeshGroup(location), new Vector3(randomPt.X, randomPt.Z, randomPt.Y));
        }

        public static Position? GetClosestPointOnMesh(Position location, AgentWidth agentWidth)
        {
            if (!TryGetMesh(location, agentWidth, out var mesh) || mesh is null)
            {
                return null;
            }

            using var lease = RentQuery(mesh);

            // prefers the main area over an island, e.g. the floor beside a table rather than its top
            if (!TryFindNearestReachablePoly(lease.Query, location, out _, out var startPt, out _))
                return null;

            return CreatePosition(location, GetMeshGroup(location), new Vector3(startPt.X, startPt.Z, startPt.Y));
        }

        public static Position? GetNearestWallPosition(Position location, float radius, AgentWidth agentWidth, out float distance, bool inverseNormal = false)
        {
            distance = float.MaxValue;

            if (!TryGetMesh(location, agentWidth, out var mesh) || mesh is null)
            {
                return null;
            }

            using var lease = RentQuery(mesh);
            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, location, out var startRef, out var startPt, out _))
                return null;

            var status = query.FindDistanceToWall(startRef, startPt, radius, AllPolysFilter, out var wallDistance, out var wallPt, out var wallNormal);
            if (status.Failed())
                return null;

            // no wall within the radius leaves the hit point and normal at zero
            if (wallPt.X == 0 && wallPt.Y == 0 && wallPt.Z == 0 && wallNormal.X == 0 && wallNormal.Y == 0 && wallNormal.Z == 0)
                return null;

            distance = wallDistance;

            var position = CreatePosition(location, GetMeshGroup(location), new Vector3(wallPt.X, wallPt.Z, wallPt.Y));

            if(inverseNormal)
                position.Rotate(new Vector3(-wallNormal.X, -wallNormal.Z, -wallNormal.Y));
            else
                position.Rotate(new Vector3(wallNormal.X, wallNormal.Z, wallNormal.Y));

            return position;
        }

        /// <summary>
        /// Creates a position for a point on a mesh, resolving the cell the point is actually in.
        /// </summary>
        /// <param name="reference">A position on the same mesh, used for the landblock and as the fallback cell</param>
        private static Position CreatePosition(Position reference, int group, Vector3 point)
        {
            var cell = ResolveCell(reference, group, point);
            return new Position(cell, point, Quaternion.Identity);
        }

        private static uint ResolveCell(Position reference, int group, Vector3 point)
        {
            try
            {
                var landblockId = reference.Cell & 0xFFFF0000;
                var probe = new Vector3(point.X, point.Y, point.Z + CELL_PROBE_HEIGHT);

                if (group == OutdoorGroup)
                    return GetLandCellId(landblockId, probe);

                // only the cells of this mesh's group: an overlapping cell of another group is not where we are
                var cell = GetGroups(reference.Cell).FindCell(group, reference.Cell, probe);
                return cell != 0 ? cell : reference.Cell;
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: failed to resolve cell for {point} near {reference.ToLOCString()}: {e.Message}");
                return reference.Cell;
            }
        }

        /// <summary>
        /// The outdoor landcell (0xAAAA0001 - 0xAAAA0040) a landblock-local point is over.
        /// </summary>
        private static uint GetLandCellId(uint landblockId, Vector3 point)
        {
            var cellLength = Position.BlockLength / 8f;
            var x = Math.Clamp((int)(point.X / cellLength), 0, 7);
            var y = Math.Clamp((int)(point.Y / cellLength), 0, 7);
            return (landblockId & 0xFFFF0000) | (uint)(x * 8 + y + 1);
        }

        private static bool TryGetMesh(Position pos, AgentWidth agentWidth, out DtNavMesh? mesh)
        {
            mesh = null;

            var group = GetMeshGroup(pos);
            if (group == NoGroup)
                return false;

            var meshId = GetMeshId(pos.Cell, group, agentWidth);

            if (Meshes.TryGetValue(meshId, out mesh))
                return mesh is not null;

            // Interior meshes load synchronously, outdoor meshes in the background.
            TryLoadMesh(pos);
            return Meshes.TryGetValue(meshId, out mesh) && mesh is not null;
        }

        public static void TryUnloadMesh(Position pos)
        {
            TryUnloadMesh(pos.Cell);
        }

        public static void TryUnloadMesh(Landblock landblock)
        {
            TryUnloadMesh(landblock.Id.Raw);
        }

        private static void TryUnloadMesh(uint cell)
        {
            var landblockId = cell & 0xFFFF0000;

            foreach (var meshId in Meshes.Keys.Where(k => (k & 0xFFFF0000) == landblockId).ToList())
                Meshes.TryRemove(meshId, out _);

            Groups.TryRemove(landblockId, out _);
            OutdoorBuildFailures.TryRemove(landblockId, out _);
        }

        private static void UnloadMesh(uint cell, int group)
        {
            foreach (var agentWidth in Enum.GetValues(typeof(AgentWidth)).Cast<AgentWidth>())
                Meshes.TryRemove(GetMeshId(cell, group, agentWidth), out _);
        }

        public static void TryLoadMesh(Position pos, bool rebuildMesh = false)
        {
            var group = GetMeshGroup(pos);

            if (group == OutdoorGroup)
                TryLoadOutdoorMesh(pos, rebuildMesh);
            else if (group != NoGroup)
                TryLoadInteriorMesh(pos, group, rebuildMesh);
        }

        /// <summary>
        /// The cache file of a mesh. The name includes a hash of the build settings and MESH_GEOMETRY_VERSION, so a
        /// mesh built with other settings or older geometry is never loaded; it is rebuilt instead.
        /// </summary>
        private static string GetMeshPath(string directory, uint meshId, bool outdoor, AgentWidth agentWidth)
        {
            return Path.Combine(directory, $"{meshId:X8}_{GetMeshCacheKey(outdoor, agentWidth)}.mesh");
        }

        private static string GetMeshCacheKey(bool outdoor, AgentWidth agentWidth)
        {
            return MeshCacheKeys.GetOrAdd((outdoor, agentWidth), key =>
            {
                var s = GetMeshSettings(key.outdoor, key.agentWidth);
                var text = FormattableString.Invariant(
                    $"{MESH_GEOMETRY_VERSION}|{VERTS_PER_POLY}|{s.vertsPerPoly}|{s.partitioning}|{s.cellSize}|{s.cellHeight}|{s.agentHeight}|{s.agentRadius}|{s.agentMaxClimb}|{s.agentMaxSlope}|{s.minRegionSize}|{s.mergedRegionSize}|{s.edgeMaxLen}|{s.edgeMaxError}|{s.detailSampleDist}|{s.detailSampleMaxError}|{s.filterLowHangingObstacles}|{s.filterLedgeSpans}|{s.filterWalkableLowHeightSpans}");
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 4);
            });
        }

        /// <summary>
        /// Reads a cached mesh. A file that cannot be read (e.g. truncated) is deleted, so the mesh gets rebuilt.
        /// </summary>
        private static bool TryReadMeshFile(string meshPath, out DtNavMesh? mesh)
        {
            mesh = null;

            if (!File.Exists(meshPath))
                return false;

            try
            {
                var bytes = File.ReadAllBytes(meshPath);
                if (bytes.Length == 0)
                    throw new InvalidDataException("empty file");

                var meshData = new DtMeshDataReader().Read(new RcByteBuffer(bytes), VERTS_PER_POLY, true);

                var navMesh = new DtNavMesh();
                var status = navMesh.Init(meshData, VERTS_PER_POLY, 0);
                if (status.Failed())
                    throw new InvalidDataException($"mesh init failed ({status})");

                mesh = navMesh;
                return true;
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: discarding unreadable mesh file {meshPath}: {e.Message}");
                TryDeleteFile(meshPath);
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: could not delete {path}: {e.Message}");
            }
        }

        private static DtNavMesh? BuildAndSaveMesh(CellGeometryProvider geom, RcNavMeshBuildSettings settings, uint meshId, string meshPath)
        {
            var builder = new NavMeshBuilder();
            var res = builder.Build(geom, settings);
            if (res is null)
                return null;

            var mesh = new DtNavMesh();
            var status = mesh.Init(res, VERTS_PER_POLY, 0);
            if (status.Failed())
            {
                log.Warn($"Pathfinder: could not initialize the built mesh 0x{meshId:X8} ({status})");
                return null;
            }

            // write to a temporary file and move it into place, so an interrupted write never leaves a truncated mesh
            var tempPath = $"{meshPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = File.Create(tempPath))
                using (var writer = new BinaryWriter(stream))
                {
                    new DtMeshDataWriter().Write(writer, res, RcByteOrder.LITTLE_ENDIAN, false);
                }
                File.Move(tempPath, meshPath, true);

                // cache files of this mesh built with other settings or geometry are stale now
                var directory = Path.GetDirectoryName(meshPath);
                foreach (var stale in Directory.GetFiles(directory, $"{meshId:X8}*.mesh"))
                {
                    if (!string.Equals(Path.GetFullPath(stale), Path.GetFullPath(meshPath), StringComparison.OrdinalIgnoreCase))
                        TryDeleteFile(stale);
                }
            }
            catch (Exception e)
            {
                // the mesh is still usable, it just gets built again next time
                log.Warn($"Pathfinder: could not save mesh {meshPath}: {e.Message}");
                if (File.Exists(tempPath))
                    TryDeleteFile(tempPath);
            }

            return mesh;
        }

        /// <summary>
        /// At most this many meshes build at once (indoors and outdoors together), so a burst of requests - like a
        /// server start after the cache was invalidated - doesn't take every core.
        /// </summary>
        private static readonly SemaphoreSlim MeshBuildSlots = new SemaphoreSlim(Math.Max(1, System.Environment.ProcessorCount / 2));

        /// <summary>
        /// Runs a mesh build on the thread pool, waiting for a free build slot first.
        /// </summary>
        private static void RunMeshBuild(Action build)
        {
            Task.Run(async () =>
            {
                await MeshBuildSlots.WaitAsync();
                try
                {
                    build();
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: mesh build failed: {e}");
                }
                finally
                {
                    MeshBuildSlots.Release();
                }
            });
        }

        /// <summary>
        /// Loads the meshes of one interior cell group: only that group's cells and their static objects. Cached meshes
        /// load right away. Missing ones are built in the background, which can take seconds for a large dungeon; until
        /// then the mesh is present as null and queries return no result.
        /// </summary>
        private static void TryLoadInteriorMesh(Position pos, int group, bool rebuildMesh = false)
        {
            var landblockId = pos.Cell & 0xFFFF0000;
            var location = pos.ToLOCString();

            if (rebuildMesh)
                UnloadMesh(landblockId, group);

            if (!GetGroups(landblockId).ByIndex.TryGetValue(group, out var cellGroup))
                return;

            var doors = cellGroup.Doors.Select(d => d.Position).ToList();
            var toBuild = new List<(AgentWidth agentWidth, uint meshId, string meshPath)>();

            foreach (var agentWidth in Enum.GetValues(typeof(AgentWidth)).Cast<AgentWidth>())
            {
                var meshId = GetMeshId(landblockId, group, agentWidth);
                if (!Meshes.TryAdd(meshId, null))
                    continue;

                var meshPath = GetMeshPath(InteriorMeshDirectory, meshId, false, agentWidth);
                try
                {
                    if (!rebuildMesh && TryReadMeshFile(meshPath, out var cachedMesh))
                    {
                        MarkIslands(cachedMesh, doors, false);
                        Meshes.TryUpdate(meshId, cachedMesh, null);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: could not load the {agentWidth} mesh for cell group {group} at {location}, rebuilding it: {e.Message}");
                }

                toBuild.Add((agentWidth, meshId, meshPath));
            }

            if (toBuild.Count == 0)
                return;

            RunMeshBuild(() =>
            {
                CellGeometryProvider geom = null;

                foreach (var (agentWidth, meshId, meshPath) in toBuild)
                {
                    // On failure the placeholder stays, so no retry is made: the same cells would fail the same way
                    // until a rebuild is requested.
                    try
                    {
                        geom ??= CellGeometryProvider.LoadGeometry(new LandblockGeometry(landblockId), cellGroup.Cells);
                        // sealed rooms and orphan cells often have nothing walkable; that is expected, so only debug logging
                        if (geom is null)
                        {
                            log.Debug($"Pathfinder: no geometry for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            return;
                        }

                        var stopwatch = Stopwatch.StartNew();
                        var mesh = BuildAndSaveMesh(geom, GetMeshSettings(false, agentWidth), meshId, meshPath);
                        if (mesh is null)
                        {
                            log.Debug($"Pathfinder: could not build the {agentWidth} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            continue;
                        }

                        MarkIslands(mesh, doors, false);
                        Meshes.TryUpdate(meshId, mesh, null);
                        log.Debug($"Pathfinder: built {agentWidth} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location} in {stopwatch.ElapsedMilliseconds}ms");
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Pathfinder: failed to build the {agentWidth} mesh for cell group {group} at {location}: {e}");
                    }
                }
            });
        }

        /// <summary>
        /// Loads (or builds) the outdoor meshes for the landblock of pos in the background.
        /// Until it is done, the meshes are present as null and queries return no result.
        /// </summary>
        private static void TryLoadOutdoorMesh(Position pos, bool rebuildMesh = false)
        {
            var landblockId = pos.Cell & 0xFFFF0000;
            var location = pos.ToLOCString();

            if (rebuildMesh)
            {
                UnloadMesh(landblockId, OutdoorGroup);
                OutdoorBuildFailures.TryRemove(landblockId, out _);
            }
            else if (OutdoorBuildFailures.TryGetValue(landblockId, out var failedAt) && DateTime.UtcNow - failedAt < OutdoorBuildRetryDelay)
                return;

            var pending = new List<(AgentWidth agentWidth, uint meshId)>();
            foreach (var agentWidth in Enum.GetValues(typeof(AgentWidth)).Cast<AgentWidth>())
            {
                var meshId = GetMeshId(landblockId, OutdoorGroup, agentWidth);
                if (Meshes.TryAdd(meshId, null))
                    pending.Add((agentWidth, meshId));
            }

            if (pending.Count == 0)
                return;

            // Snapshot the landblock's object lists here rather than on the build thread, which would read them while
            // the landblock may be changing them. Null if the landblock is not loaded: cached meshes can still be read.
            SurfaceSnapshot surface = null;
            try
            {
                var lbid = new LandblockId(landblockId | 0xFFFF);
                var physicsLandblock = LandblockManager.IsLoaded(lbid) ? LandblockManager.GetLandblock(lbid, false)?.PhysicsLandblock : null;
                if (physicsLandblock != null)
                    surface = new SurfaceSnapshot(physicsLandblock);
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: could not snapshot the outdoors of 0x{landblockId:X8}: {e.Message}");
            }

            RunMeshBuild(() =>
            {
                CellGeometryProvider geom = null;
                var geometryFailed = false;

                foreach (var (agentWidth, meshId) in pending)
                {
                    try
                    {
                        var meshPath = GetMeshPath(OutdoorMeshDirectory, meshId, true, agentWidth);
                        if (!rebuildMesh && TryReadMeshFile(meshPath, out var cachedMesh))
                        {
                            MarkIslands(cachedMesh, GetOutdoorDoorPositions(landblockId), true);
                            Meshes.TryUpdate(meshId, cachedMesh, null);
                            continue;
                        }

                        if (surface == null)
                        {
                            // not loaded (yet); drop the placeholder so a later request can try again
                            ReleasePlaceholder(meshId);
                            continue;
                        }

                        if (geometryFailed)
                        {
                            FailOutdoorBuild(landblockId, meshId);
                            continue;
                        }

                        if (geom == null)
                        {
                            var geometry = new LandblockGeometry(landblockId);

                            // interior cells only cut the terrain out of building interiors, their own meshes hold their geometry
                            geom = CellGeometryProvider.LoadSurfaceGeometry(geometry, geometry.SurfaceConnectedCells, surface);
                            if (geom is null)
                            {
                                log.Warn($"Pathfinder: could not load outdoor geometry for 0x{landblockId:X8}");
                                geometryFailed = true;
                                FailOutdoorBuild(landblockId, meshId);
                                continue;
                            }
                        }

                        var stopwatch = Stopwatch.StartNew();
                        var mesh = BuildAndSaveMesh(geom, GetMeshSettings(true, agentWidth), meshId, meshPath);
                        if (mesh is null)
                        {
                            // the same geometry would build the same way, so the placeholder stays and no retry is made
                            log.Warn($"Pathfinder: could not build the {agentWidth} outdoor nav mesh for 0x{landblockId:X8}");
                            continue;
                        }

                        MarkIslands(mesh, GetOutdoorDoorPositions(landblockId), true);
                        Meshes.TryUpdate(meshId, mesh, null);
                        log.Info($"Pathfinder: built {agentWidth} outdoor nav mesh for 0x{landblockId:X8} in {stopwatch.ElapsedMilliseconds}ms ({geom.vertices.Length / 3} vertices, {geom.faces.Length / 3} triangles)");
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Pathfinder: failed to load {agentWidth} outdoor mesh at {location}: {e}");
                        FailOutdoorBuild(landblockId, meshId);
                    }
                }
            });
        }

        /// <summary>
        /// The doorways from the outdoors into the landblock's buildings and caves, which anchor the outdoor mesh
        /// components around them (see MarkIslands).
        /// </summary>
        private static IEnumerable<Vector3> GetOutdoorDoorPositions(uint landblockId)
        {
            return GetGroups(landblockId).ByIndex.Values
                .Where(g => g.ConnectsToOutside)
                .SelectMany(g => g.Doors)
                .Select(d => d.Position)
                .ToList();
        }

        /// <summary>
        /// Removes a mesh's loading placeholder, so the next request for the mesh tries to load it again.
        /// </summary>
        private static void ReleasePlaceholder(uint meshId)
        {
            Meshes.TryRemove(new KeyValuePair<uint, DtNavMesh?>(meshId, null));
        }

        /// <summary>
        /// Records a failed outdoor build: the mesh can be requested again, but is not retried for OutdoorBuildRetryDelay.
        /// </summary>
        private static void FailOutdoorBuild(uint landblockId, uint meshId)
        {
            OutdoorBuildFailures[landblockId] = DateTime.UtcNow;
            ReleasePlaceholder(meshId);
        }

        private static RcNavMeshBuildSettings GetMeshSettings(bool outdoor, AgentWidth type)
        {
            var settings = new RcNavMeshBuildSettings()
            {
                agentHeight = type == AgentWidth.Narrow ? 2.0f : 2.2f, // 2f,
                agentMaxClimb = type == AgentWidth.Narrow ? 0.7f : 0.8f, // 0.95f,
                agentMaxSlope = 48f, // matches PhysicsGlobals.FloorZ (acos(0.664) = ~48.4 degrees), steeper is not walkable
                cellHeight = 0.1f,
                cellSize = 0.1f,
                agentRadius = type == AgentWidth.Narrow ? NarrowAgentRadius : WideAgentRadius,
                detailSampleDist = 6.0f,
                detailSampleMaxError = 1.0f,
                edgeMaxError = 1f,
                edgeMaxLen = 12.0f,
                vertsPerPoly = VERTS_PER_POLY,
                partitioning = (int)RcPartition.WATERSHED
            };

            if (outdoor)
            {
                // Recast's guideline is a cell size of a half to a third of the agent radius. Interiors keep a finer
                // grid for narrow doorways and stairs; over a whole landblock (192m) 0.2 cuts the voxels to a quarter.
                // The radii still convert exactly, except Wide which rounds up from 1.1 to 1.2m (6 cells).
                settings.cellSize = 0.2f;

                // longer edges keep the poly count down over a whole landblock
                settings.edgeMaxLen = 24.0f;
            }

            // Recast takes the region sizes as the side, in cells, of a square of that area
            settings.minRegionSize = GetRegionSize(MIN_REGION_AREA, settings.cellSize);
            settings.mergedRegionSize = GetRegionSize(MERGE_REGION_AREA, settings.cellSize);

            return settings;
        }

        private static int GetRegionSize(float area, float cellSize)
        {
            return Math.Max(1, (int)MathF.Round(MathF.Sqrt(area) / cellSize));
        }
    }
}
