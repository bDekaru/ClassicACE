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
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The navmeshes built per walkable area, one per agent size. A creature routes on the smallest one it fits (see
    /// Creature.GetPathfindingAgentSize). The sizes come from the collision spheres of the world's monsters, split so
    /// the clearance wasted on creatures smaller than their mesh's agent is lowest (weighted by spawn count).
    /// </summary>
    public enum AgentSize
    {
        Small,  // Fits most doors
        Medium, // Fits regular corridors
        Large   // Fits large rooms and outdoors only
    }

    /// <summary>
    /// The outdoors of the world is one tiled mesh (see OutdoorNavMesh) with a tile per landblock, so routes cross
    /// landblock edges. Each group of interior cells connected through cell portals (buildings, caves, dungeons) has
    /// its own mesh. Interior groups never share a mesh, so spaces that overlap without being connected (e.g. the
    /// basements of two neighboring houses) cannot interfere with each other.
    ///
    /// Interior meshes are in landblock-local coordinates, the outdoor mesh in world coordinates (a landblock's local
    /// origin is at GeometryHelpers.GetLandblockOrigin). Recast space is Y up: (x, z, y) of a game position.
    /// </summary>
    public static partial class Pathfinder
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int MaxVertsPerPoly = 6;

        /// <summary>
        /// The longest poly corridor and straight path a route can have. Outdoor routes can cross several landblocks.
        /// </summary>
        private const int MaxPolys = 1024;

        /// <summary>
        /// What each mesh is built for, per agent size.
        ///
        /// Radius: how far the walkable area keeps from walls. A creature's radius is that of its collision spheres
        /// (Setup.Sphere x scale), which is what collides with walls. Small covers ~69% of monster spawns (drudges,
        /// olthoi, humans), Medium ~22% (tuskers, burun, mattekars) and Large ~8% (guruks and other big creatures); the
        /// ~1% wider than Large route on it anyway.
        ///
        /// Height: the clearance needed overhead, which closes every opening lower than it on that mesh. Chosen from
        /// the world's openings: building doorways are nearly all 2.5m tall and dungeon corridors mostly 3.0m, and
        /// Recast loses about a cell (0.1m) voxelizing a floor and a lintel, so an opening stays open for an
        /// agentHeight a cell under it. Small at 2.4 keeps 86% of the building doors open for its creatures (2.6
        /// kept 41%); Medium at 2.9 keeps 99% of the dungeon openings (3.2 kept 62%). Large at 3.4 costs nothing in
        /// dungeons, as every opening wide enough for it is also tall enough. A creature taller than its radius' mesh
        /// routes on the next one up (see GetAgentSize): ~2% of spawns.
        ///
        /// MaxClimb: the ledge height stepped up (and down). A creature steps up a ledge when its foot sphere hits it
        /// and the top is within 1.5 x StepUpHeight (see Polygon.adjust_sphere_to_plane), but the top must also
        /// overlap the foot sphere: up to the sphere's radius always, up to its diameter only when moving fast enough
        /// to push into the ledge. The foot sphere is the tighter limit for nearly every creature, so these are the
        /// heights about 96% (Small) and 99-100% (Medium, Large) of each mesh's spawns climb every time.
        ///
        /// Cell sizes: the voxel cell size and the agent radius in whole cells, interior and outdoor. Recast erodes the
        /// walkable area by ceil(radius / cellSize) cells, so a radius that isn't a whole number of cells is rounded
        /// up (0.65m on 0.2m cells erodes 0.8m). Each mesh gets a cell size its agent radius divides into:
        ///
        ///   Interior: Small 0.65/7 (~0.093m) x 7 = 0.65m, Medium 0.1 x 10 = 1.0m, Large 0.125 x 14 = 1.75m.
        ///   Outdoor:  Small 0.16 x 4 = 0.64m, Medium 0.2 x 5 = 1.0m, Large 0.25 x 7 = 1.75m.
        ///
        /// Outdoor cell sizes must divide the landblock (192m) into a whole number of cells, as a tile must be; the
        /// only one that fits 0.65m exactly is 0.05m (16x the voxels), so Small erodes 1cm less there. Interiors keep
        /// finer grids for narrow doorways and stairs. Large needs no fine detail (Recast's guideline is a cell size of a
        /// half to a third of the agent radius), so its coarser grid also makes its meshes cheaper to build.
        /// </summary>
        private readonly record struct AgentProfile(float Radius, float Height, float MaxClimb, float InteriorCellSize, int InteriorRadiusCells, float OutdoorCellSize, int OutdoorRadiusCells);

        private static readonly AgentProfile SmallProfile = new AgentProfile(0.65f, 2.4f, 0.4f, 0.65f / 7f, 7, 0.16f, 4);

        private static readonly AgentProfile MediumProfile = new AgentProfile(1.0f, 2.9f, 0.6f, 0.1f, 10, 0.2f, 5);

        private static readonly AgentProfile LargeProfile = new AgentProfile(1.75f, 3.4f, 0.8f, 0.125f, 14, 0.25f, 7);

        /// <summary>
        /// Every agent size, each with its own meshes.
        /// </summary>
        private static readonly AgentSize[] AgentSizes = Enum.GetValues<AgentSize>();

        private static AgentProfile GetProfile(AgentSize agentSize) => agentSize switch
        {
            AgentSize.Small => SmallProfile,
            AgentSize.Medium => MediumProfile,
            _ => LargeProfile
        };

        /// <summary>
        /// How far a creature may exceed a mesh's agent radius or height and still route on it: under the error of
        /// Recast's voxels, and keeps creatures measured right at a limit (the Olthoi at 0.650m radius, the Burun
        /// Ruuk at 1.0002m) on the mesh the sizes were chosen for.
        /// </summary>
        private const float AgentSizeTolerance = 0.01f;

        public static float GetAgentRadius(AgentSize agentSize) => GetProfile(agentSize).Radius;

        public static float GetAgentHeight(AgentSize agentSize) => GetProfile(agentSize).Height;

        public static float GetAgentMaxClimb(AgentSize agentSize) => GetProfile(agentSize).MaxClimb;

        /// <summary>
        /// The smallest mesh a creature of this collision radius and height fits. A creature too wide or too tall
        /// for every mesh routes on Large, the closest fit.
        /// </summary>
        public static AgentSize GetAgentSize(float radius, float height)
        {
            foreach (var agentSize in new[] { AgentSize.Small, AgentSize.Medium })
            {
                if (radius <= GetAgentRadius(agentSize) + AgentSizeTolerance && height <= GetAgentHeight(agentSize) + AgentSizeTolerance)
                {
                    return agentSize;
                }
            }
            return AgentSize.Large;
        }

        /// <summary>
        /// The group index of the outdoors. Interior groups start at 1.
        /// </summary>
        public const int OutdoorGroup = 0;

        /// <summary>
        /// Group index for positions that are in no known group (no mesh).
        /// </summary>
        private const int NoGroup = -1;

        /// <summary>
        /// Group indices are packed above the agent size in the low 16 bits of a mesh id.
        /// </summary>
        private const int MaxGroup = 0xFFF;

        /// <summary>
        /// Waypoints sit on the floor; the cell lookup probes slightly above it so the point is inside the cell volume.
        /// </summary>
        private const float CellProbeHeight = 0.25f;

        /// <summary>
        /// How far around a position to look for the mesh. Positions further off the mesh than this are not on it.
        /// </summary>
        private static readonly RcVec3f QueryHalfExtents = new RcVec3f(1.25f, 1.25f, 1.25f);

        /// <summary>
        /// Every poly, islands included.
        /// </summary>
        private static readonly DtQueryDefaultFilter AllPolysFilter = new DtQueryDefaultFilter();

        /// <summary>
        /// Every poly except islands.
        /// </summary>
        private static readonly DtQueryDefaultFilter ReachableFilter = new DtQueryDefaultFilter(0xFFFF, NavPolyFlags.Island, Array.Empty<float>());

        /// <summary>
        /// Per landblock (0xAAAA0000), its interior cell groups.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, LandblockGroups> Groups = new ConcurrentDictionary<uint, LandblockGroups>();

        private class LandblockGroups
        {
            public readonly Dictionary<int, CellGroup> ByIndex = new Dictionary<int, CellGroup>();
            public readonly Dictionary<uint, int> CellToGroup = new Dictionary<uint, int>();
            private readonly Dictionary<uint, CellGeometry> cellsById = new Dictionary<uint, CellGeometry>();
            private readonly ConcurrentDictionary<uint, ACE.Server.Physics.Common.EnvCell> physicsCells = new ConcurrentDictionary<uint, ACE.Server.Physics.Common.EnvCell>();

            /// <summary>
            /// The cells FindCell has queued and its queue, per thread.
            /// </summary>
            [ThreadStatic]
            private static HashSet<uint> searchSeen;

            [ThreadStatic]
            private static Queue<uint> searchQueue;

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
                    foreach (var cell in group.Cells)
                        cellsById[cell.CellId] = cell;
                }
            }

            private ACE.Server.Physics.Common.EnvCell GetPhysicsCell(uint cellId)
            {
                return physicsCells.GetOrAdd(cellId, id => ACE.Server.Physics.Common.DBObj.GetEnvCell(id));
            }

            /// <summary>
            /// Finds the cell of a group that contains a point, searching outward from the reference cell through the
            /// group's portals, nearest first. A point next to the reference (e.g. the next waypoint of a route) is
            /// found in a few cells instead of sorting them all. Without a reference cell in the group, the cells
            /// nearest to the point are tried first. Only this group's cells are considered, so an overlapping cell of
            /// an unconnected group is never returned. Returns 0 if no cell contains the point.
            /// </summary>
            public uint FindCell(int groupIndex, uint referenceCell, Vector3 point)
            {
                if (!ByIndex.TryGetValue(groupIndex, out var group))
                    return 0;

                if (!group.CellIds.Contains(referenceCell))
                {
                    foreach (var cell in group.Cells.OrderBy(c => Vector3.DistanceSquared(c.EnvCell.Position.Origin, point)))
                    {
                        if (GetPhysicsCell(cell.CellId)?.point_in_cell(point) == true)
                            return cell.CellId;
                    }
                    return 0;
                }

                var landblockId = referenceCell & 0xFFFF0000;

                // reused per thread: a route looks up every one of its waypoints
                var seen = searchSeen ??= new HashSet<uint>();
                var queue = searchQueue ??= new Queue<uint>();
                seen.Clear();
                queue.Clear();

                seen.Add(referenceCell);
                queue.Enqueue(referenceCell);

                while (queue.Count > 0)
                {
                    var cellId = queue.Dequeue();
                    if (GetPhysicsCell(cellId)?.point_in_cell(point) == true)
                        return cellId;

                    foreach (var portal in cellsById[cellId].EnvCell.CellPortals)
                    {
                        var otherCellId = landblockId | portal.OtherCellId;
                        if (group.CellIds.Contains(otherCellId) && seen.Add(otherCellId))
                            queue.Enqueue(otherCellId);
                    }
                }

                // cells the portals didn't lead to, should a group ever hold any
                foreach (var cell in group.Cells)
                {
                    if (!seen.Contains(cell.CellId) && GetPhysicsCell(cell.CellId)?.point_in_cell(point) == true)
                        return cell.CellId;
                }
                return 0;
            }
        }

        /// <summary>
        /// A mesh group: the outdoors (one for the whole world), or a connected group of interior cells. Interior group
        /// indices are only unique within their landblock, so a group is identified by both.
        /// </summary>
        private readonly struct MeshGroupKey : IEquatable<MeshGroupKey>
        {
            /// <summary>
            /// The landblock of an interior group, in the format 0xAAAA0000. 0 for the outdoors.
            /// </summary>
            public readonly uint LandblockId;
            public readonly int Index;

            public static readonly MeshGroupKey None = new MeshGroupKey(0, NoGroup);
            public static readonly MeshGroupKey Outdoors = new MeshGroupKey(0, OutdoorGroup);

            public MeshGroupKey(uint landblockId, int index)
            {
                LandblockId = index == OutdoorGroup ? 0 : landblockId & 0xFFFF0000;
                Index = index;
            }

            public bool IsOutdoor => Index == OutdoorGroup;
            public bool IsNone => Index == NoGroup;

            public bool Equals(MeshGroupKey other) => Index == other.Index && LandblockId == other.LandblockId;
            public override bool Equals(object obj) => obj is MeshGroupKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(LandblockId, Index);
            public static bool operator ==(MeshGroupKey a, MeshGroupKey b) => a.Equals(b);
            public static bool operator !=(MeshGroupKey a, MeshGroupKey b) => !a.Equals(b);

            public override string ToString() => IsOutdoor ? "outdoors" : IsNone ? "none" : $"0x{LandblockId:X8} group {Index}";
        }

        public static bool PathfindingEnabled { get => PropertyManager.GetBool("pathfinding").Item; }

        public static WorldObject CreateMarker(Position position, string customName = "")
        {
            var marker = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.pathfinderHelper);

            if (marker == null)
                return null;

            if (customName != "")
                marker.Name = customName;

            marker.Location = new Position(position);
            if (!marker.Location.Indoors)
                marker.Location.LandblockId = new LandblockId(marker.Location.GetCell());

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

        /// <summary>
        /// The interior cell groups of a landblock. Cached only while the landblock is loaded, as its unload is what
        /// frees them (see TryUnloadMesh).
        /// </summary>
        private static LandblockGroups GetGroups(uint cell)
        {
            var landblockId = cell & 0xFFFF0000;
            if (Groups.TryGetValue(landblockId, out var groups))
                return groups;

            groups = new LandblockGroups(landblockId);
            return LandblockManager.IsLoaded(new LandblockId(landblockId | 0xFFFF)) ? Groups.GetOrAdd(landblockId, groups) : groups;
        }

        /// <summary>
        /// Returns the mesh group a position belongs to: OutdoorGroup for the landscape, the index of its connected cell
        /// group (within its landblock) for interior cells, or -1 for an unknown cell.
        /// </summary>
        public static int GetMeshGroup(Position pos)
        {
            return GetGroupKey(pos).Index;
        }

        private static MeshGroupKey GetGroupKey(Position pos)
        {
            if (!pos.Indoors)
                return MeshGroupKey.Outdoors;

            return GetGroups(pos.Cell).CellToGroup.TryGetValue(pos.Cell, out var group)
                ? new MeshGroupKey(pos.Cell, group)
                : MeshGroupKey.None;
        }

        private static bool TryGetCellGroup(MeshGroupKey key, out CellGroup cellGroup)
        {
            cellGroup = null;
            return !key.IsOutdoor && !key.IsNone && GetGroups(key.LandblockId).ByIndex.TryGetValue(key.Index, out cellGroup);
        }

        private static uint GetMeshId(uint cell, int group, AgentSize agentSize)
        {
            return (cell & 0xFFFF0000) + ((uint)group << 4) + (uint)agentSize;
        }

        #region Coordinates

        /// <summary>
        /// A landblock-local point in world coordinates.
        /// </summary>
        private static Vector3 ToGlobal(uint cell, Vector3 local)
        {
            return local + GeometryHelpers.GetLandblockOrigin(cell);
        }

        private static Vector3 ToGlobal(Position pos)
        {
            return ToGlobal(pos.Cell, pos.Pos);
        }

        /// <summary>
        /// A landblock-local point as a point in Recast space on a mesh: in world coordinates on the outdoor mesh,
        /// unchanged on an interior mesh.
        /// </summary>
        private static RcVec3f ToMeshPoint(uint cell, Vector3 local, bool outdoor)
        {
            var p = outdoor ? ToGlobal(cell, local) : local;
            return new RcVec3f(p.X, p.Z, p.Y);
        }

        private static Vector3 FromMeshPoint(RcVec3f p)
        {
            return new Vector3(p.X, p.Z, p.Y);
        }

        private static (int x, int y) GetLandblockCoords(uint cell)
        {
            return ((int)((cell >> 24) & 0xFF), (int)((cell >> 16) & 0xFF));
        }

        private static uint GetLandblockId(int x, int y)
        {
            return ((uint)x << 24) | ((uint)y << 16);
        }

        /// <summary>
        /// Creates a position for a point on a mesh, resolving the landblock (outdoors) and the cell the point is in.
        /// </summary>
        /// <param name="reference">A position on the same mesh, used for the landblock and as the fallback cell of
        /// an interior mesh</param>
        private static Position CreatePosition(Position reference, MeshGroupKey group, RcVec3f meshPoint)
        {
            var point = FromMeshPoint(meshPoint);

            if (group.IsOutdoor)
                return CreateOutdoorPosition(point);

            var cell = ResolveInteriorCell(reference, group.Index, point);
            return new Position(cell, point, Quaternion.Identity);
        }

        /// <summary>
        /// Creates a position on the landscape for a point in world coordinates: always a landcell (0xAAAA0001 - 0xAAAA0040),
        /// the outdoor mesh's group, with the landblock and cell clamped to the world's edges.
        /// We must use this function instead of PositionExtensions.FromGlobal as that would load the landblock
        /// (objects and all) if it isn't loaded, and returns building and cave cells for points inside them, which belong to
        /// other meshes.
        /// </summary>
        private static Position CreateOutdoorPosition(Vector3 global)
        {
            var x = Math.Clamp((int)MathF.Floor(global.X / Position.BlockLength), 0, 0xFF);
            var y = Math.Clamp((int)MathF.Floor(global.Y / Position.BlockLength), 0, 0xFF);
            var landblockId = GetLandblockId(x, y);

            var local = new Vector3(global.X - x * Position.BlockLength, global.Y - y * Position.BlockLength, global.Z);
            return new Position(GetLandCellId(landblockId, local), local, Quaternion.Identity);
        }

        private static uint ResolveInteriorCell(Position reference, int group, Vector3 point)
        {
            try
            {
                var probe = new Vector3(point.X, point.Y, point.Z + CellProbeHeight);

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
            var x = Math.Clamp((int)(point.X / Position.CellLength), 0, Position.CellSide - 1);
            var y = Math.Clamp((int)(point.Y / Position.CellLength), 0, Position.CellSide - 1);
            return (landblockId & 0xFFFF0000) | (uint)(x * Position.CellSide + y + 1);
        }

        #endregion

        private static Vector3 ToVector3(RcVec3f v) => new Vector3(v.X, v.Y, v.Z);

        private static RcVec3f ToRcVec3f(Vector3 v) => new RcVec3f(v.X, v.Y, v.Z);

        private static Vector3 Flatten(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        /// <summary>
        /// Finds the mesh poly nearest to a point (within QueryHalfExtents). Returns false if the point is not on the
        /// mesh. Note FindNearestPoly succeeds with a 0 ref (and the input point) when it finds nothing.
        /// </summary>
        private static bool TryFindNearestPoly(DtNavMeshQuery query, IDtQueryFilter filter, RcVec3f center, out long polyRef, out RcVec3f point)
        {
            var status = query.FindNearestPoly(center, QueryHalfExtents, filter, out polyRef, out point, out _);
            return status.Succeeded() && polyRef != 0;
        }

        /// <summary>
        /// Like TryFindNearestPoly, but prefers a poly that is not on an island. Falls back to an island poly when
        /// there is no other within reach (onIsland is then true), e.g. for a creature that stands on one.
        /// </summary>
        private static bool TryFindNearestReachablePoly(DtNavMeshQuery query, RcVec3f center, out long polyRef, out RcVec3f point, out bool onIsland)
        {
            onIsland = false;
            if (TryFindNearestPoly(query, ReachableFilter, center, out polyRef, out point))
                return true;

            onIsland = TryFindNearestPoly(query, AllPolysFilter, center, out polyRef, out point);
            return onIsland;
        }

        /// <summary>
        /// Detour's random numbers, from ThreadSafeRandom like the rest of the server.
        /// </summary>
        private sealed class ThreadSafeRcRand : IRcRand
        {
            public static readonly ThreadSafeRcRand Instance = new ThreadSafeRcRand();

            public float Next() => (float)ACE.Common.ThreadSafeRandom.Next(0f, 1f);
            public double NextDouble() => ACE.Common.ThreadSafeRandom.Next(0f, 1f);

            // ThreadSafeRandom's maximum is inclusive, and one past it must still fit an int
            public int NextInt32() => ACE.Common.ThreadSafeRandom.Next(0, int.MaxValue - 1);
        }

        /// <summary>
        /// Get a random point on the navmesh: anywhere on an interior mesh, within the landblock's reach outdoors.
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static Position GetRandomPointOnMesh(Position start, AgentSize agentSize)
        {
            var group = GetGroupKey(start);
            if (group.IsOutdoor)
                return GetRandomPointWithinCircle(start, Position.BlockLength / 2f, agentSize);

            using var lease = AcquireMesh(start, group, agentSize);
            if (lease == null)
                return null;

            // never on an island, which nothing could walk to
            var status = lease.Query.FindRandomPoint(ReachableFilter, ThreadSafeRcRand.Instance, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(start, group, randomPt);
        }

        public static Position GetRandomPointWithinCircle(Position location, float radius, AgentSize agentSize)
        {
            var group = GetGroupKey(location);
            using var lease = AcquireMesh(location, group, agentSize);
            if (lease == null)
                return null;

            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, lease.ToMesh(location), out var startRef, out var startPt, out var startOnIsland))
                return null;

            // the search spreads out from the start poly, so on an island it has to be allowed to stay on it
            var filter = startOnIsland ? AllPolysFilter : ReachableFilter;
            var status = query.FindRandomPointWithinCircle(startRef, startPt, radius, filter, ThreadSafeRcRand.Instance, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(location, group, randomPt);
        }

        public static Position GetClosestPointOnMesh(Position location, AgentSize agentSize)
        {
            var group = GetGroupKey(location);
            using var lease = AcquireMesh(location, group, agentSize);
            if (lease == null)
                return null;

            // prefers the main area over an island, e.g. the floor beside a table rather than its top
            if (!TryFindNearestReachablePoly(lease.Query, lease.ToMesh(location), out _, out var startPt, out _))
                return null;

            return CreatePosition(location, group, startPt);
        }

        /// <summary>
        /// The nearest point on a wall within radius of a position, on the mesh of the given agent size, facing away
        /// from the wall (or towards it with faceWall). Null if there is no wall in range or the mesh isn't loaded.
        /// </summary>
        /// <param name="distance">How far the wall is from the position, or float.MaxValue if none was found</param>
        public static Position GetNearestWallPosition(Position location, float radius, AgentSize agentSize, out float distance, bool faceWall = false)
        {
            distance = float.MaxValue;

            var group = GetGroupKey(location);
            using var lease = AcquireMesh(location, group, agentSize);
            if (lease == null)
                return null;

            var query = lease.Query;

            if (!TryFindNearestReachablePoly(query, lease.ToMesh(location), out var startRef, out var startPt, out _))
                return null;

            var status = query.FindDistanceToWall(startRef, startPt, radius, AllPolysFilter, out var wallDistance, out var wallPt, out var wallNormal);
            if (status.Failed())
                return null;

            // no wall closer than the radius leaves the distance at the radius
            if (wallDistance >= radius)
                return null;

            distance = wallDistance;

            var position = CreatePosition(location, group, wallPt);

            // the normal points away from the wall
            var normal = FromMeshPoint(wallNormal);
            position.Rotate(faceWall ? -normal : normal);

            return position;
        }
    }
}
