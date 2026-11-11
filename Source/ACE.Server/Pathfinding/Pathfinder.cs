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

        private const int VERTS_PER_POLY = 6;

        /// <summary>
        /// The longest poly corridor and straight path a route can have. Outdoor routes can cross several landblocks.
        /// </summary>
        private const int MAX_POLYS = 1024;

        /// <summary>
        /// The agent radius each mesh is built for: how far its walkable area keeps from walls. A creature's radius is
        /// that of its collision spheres (Setup.Sphere x scale), which is what collides with walls. Small covers ~69%
        /// of monster spawns (drudges, olthoi, humans), Medium ~22% (tuskers, burun, mattekars) and Large ~8% (guruks
        /// and other big creatures); the ~1% wider than Large route on it anyway.
        /// Each mesh's cell size is chosen so its radius is a whole number of cells (see GetCellSize).
        /// </summary>
        public const float SmallAgentRadius = 0.65f;
        public const float MediumAgentRadius = 1.0f;
        public const float LargeAgentRadius = 1.75f;

        /// <summary>
        /// The clearance each mesh needs overhead, which closes every opening lower than it on that mesh. Chosen from
        /// the world's openings: building doorways are nearly all 2.5m tall and dungeon corridors mostly 3.0m, and
        /// Recast loses about a cell (0.1m) voxelizing a floor and a lintel, so an opening stays open for an
        /// agentHeight a cell under it. Small at 2.4 keeps 86% of the building doors open for its creatures (2.6
        /// kept 41%); Medium at 2.9 keeps 99% of the dungeon openings (3.2 kept 62%). Large at 3.4 costs nothing in
        /// dungeons, as every opening wide enough for it is also tall enough.
        /// A creature taller than its radius' mesh routes on the next one up (see GetAgentSize): ~2% of spawns.
        /// </summary>
        public const float SmallAgentHeight = 2.4f;
        public const float MediumAgentHeight = 2.9f;
        public const float LargeAgentHeight = 3.4f;

        /// <summary>
        /// How far a creature may exceed a mesh's agent radius or height and still route on it: under the error of
        /// Recast's voxels, and keeps creatures measured right at a limit (the Olthoi at 0.650m radius, the Burun
        /// Ruuk at 1.0002m) on the mesh the sizes were chosen for.
        /// </summary>
        private const float AgentSizeTolerance = 0.01f;

        /// <summary>
        /// The ledge height each mesh steps up (and down). A creature steps up a ledge when its foot sphere hits it
        /// and the top is within 1.5 x StepUpHeight (see Polygon.adjust_sphere_to_plane), but the top must also
        /// overlap the foot sphere: up to the sphere's radius always, up to its diameter only when moving fast enough
        /// to push into the ledge. The foot sphere is the tighter limit for nearly every creature, so these are the
        /// heights about 96% (Small) and 99-100% (Medium, Large) of each mesh's spawns climb every time.
        /// </summary>
        public const float SmallAgentMaxClimb = 0.4f;
        public const float MediumAgentMaxClimb = 0.6f;
        public const float LargeAgentMaxClimb = 0.8f;

        public static float GetAgentRadius(AgentSize agentSize)
        {
            switch (agentSize)
            {
                case AgentSize.Small:
                    return SmallAgentRadius;
                case AgentSize.Medium:
                    return MediumAgentRadius;
                default:
                    return LargeAgentRadius;
            }
        }

        public static float GetAgentHeight(AgentSize agentSize)
        {
            switch (agentSize)
            {
                case AgentSize.Small:
                    return SmallAgentHeight;
                case AgentSize.Medium:
                    return MediumAgentHeight;
                default:
                    return LargeAgentHeight;
            }
        }

        public static float GetAgentMaxClimb(AgentSize agentSize)
        {
            switch (agentSize)
            {
                case AgentSize.Small:
                    return SmallAgentMaxClimb;
                case AgentSize.Medium:
                    return MediumAgentMaxClimb;
                default:
                    return LargeAgentMaxClimb;
            }
        }

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
        private const float CELL_PROBE_HEIGHT = 0.25f;

        /// <summary>
        /// Extra distance turn waypoints are pushed away from the corner they bend around, on top of the agent radius
        /// the mesh is already eroded by.
        /// </summary>
        private const float CORNER_CLEARANCE = 0.3f;

        private const int MAX_SURFACE_MOVE_POLYS = 64;

        private static readonly float BlockLength = ACE.Server.Physics.Common.LandDefs.BlockLength;

        /// <summary>
        /// How far around a position to look for the mesh. Positions further off the mesh than this are not on it.
        /// </summary>
        private static readonly RcVec3f QueryHalfExtents = new RcVec3f(1.25f, 1.25f, 1.25f);

        /// <summary>
        /// Part of the interior mesh cache file names. Build setting changes are picked up automatically; bump this
        /// when the exported geometry changes, so cached meshes built from the old geometry are not loaded.
        /// </summary>
        private const int MESH_GEOMETRY_VERSION = 4; // 2: door aprons, 3: cell statics clipped to the cells they collide in, aprons built per agent size, 4: cell statics shaped as the server's physics objects, spheres at their centers

        /// <summary>
        /// Like MESH_GEOMETRY_VERSION, for the outdoor tiles. 2: tiles in world coordinates with a border from the
        /// neighboring landblocks. 3: the objects of every landblock come from the dat, loaded or not. 4: spheres at
        /// their centers.
        /// </summary>
        private const int OUTDOOR_MESH_GEOMETRY_VERSION = 4;

        /// <summary>
        /// After an outdoor tile build fails, no new attempt is made for this long (unless a rebuild is requested).
        /// </summary>
        private static readonly TimeSpan OutdoorBuildRetryDelay = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Per landblock (0xAAAA0000), when its last outdoor tile build failed.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, DateTime> OutdoorBuildFailures = new ConcurrentDictionary<uint, DateTime>();

        private static readonly ConcurrentDictionary<(bool outdoor, AgentSize agentSize), string> MeshCacheKeys = new ConcurrentDictionary<(bool, AgentSize), string>();

        /// <summary>
        /// Outdoor tile components with a vertex this close to the tile edge are not islands: they may continue into
        /// the neighboring tile. Tiles are built with a border, so the walkable area reaches the edge exactly.
        /// </summary>
        private const float TILE_EDGE_TOLERANCE = 0.25f;

        /// <summary>
        /// An outdoor route searches at most this many landblocks away from its start, in each direction, and only
        /// requests the tiles in that range. Bounds the search on a server with many landblocks loaded.
        /// </summary>
        private const int MAX_ROUTE_LANDBLOCK_SPAN = 2;

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
        private static readonly DtQueryDefaultFilter ReachableFilter = new DtQueryDefaultFilter(0xFFFF, NavPolyFlags.Island, Array.Empty<float>());

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
        /// A query holds per-search state and is not thread safe, while a mesh can be shared. Queries are pooled per
        /// mesh instead of created per call. The table holds meshes weakly, so an unloaded mesh's pool goes with it.
        /// </summary>
        private static readonly ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>> QueryPools = new ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>>();

        private const int MAX_POOLED_QUERIES_PER_MESH = 16;

        /// <summary>
        /// Smallest walkable region kept, and the size below which regions are merged into their neighbors, in m².
        /// In world units so they don't change with the cell size.
        /// </summary>
        private const float MIN_REGION_AREA = 0.64f;
        private const float MERGE_REGION_AREA = 4.0f;

        public static string InteriorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Interiors");
        public static string OutdoorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Outdoors");

        /// <summary>
        /// The interior group meshes, by mesh id (see GetMeshId). A placeholder while a mesh is loading.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, InteriorMesh> Meshes = new ConcurrentDictionary<uint, InteriorMesh>();

        /// <summary>
        /// An interior group mesh and the doorways of its cell group an agent of the mesh's size can get through: those
        /// whose door apron (see DoorApron) is connected to the rest of the mesh. Published together, so the doorways
        /// always belong to the mesh.
        /// A load claims its mesh id with a placeholder of its own, and only replaces that placeholder with the mesh: a
        /// mesh that was unloaded or rebuilt while it was loading has another placeholder or none, and the stale result
        /// is dropped. ConcurrentDictionary.TryUpdate compares by reference here, as Equals isn't overridden.
        /// </summary>
        private sealed class InteriorMesh
        {
            public readonly DtNavMesh Mesh;
            public readonly List<CellGroupDoor> PassableDoors;

            public bool IsPlaceholder => Mesh == null;

            public InteriorMesh(DtNavMesh mesh, List<CellGroupDoor> passableDoors)
            {
                Mesh = mesh;
                PassableDoors = passableDoors;
            }

            public static InteriorMesh CreatePlaceholder() => new InteriorMesh(null, null);
        }

        /// <summary>
        /// The door polys of reachable door aprons.
        /// </summary>
        private static readonly DtQueryDefaultFilter PassableDoorFilter = new DtQueryDefaultFilter(NavPolyFlags.Door, NavPolyFlags.Island, Array.Empty<float>());

        /// <summary>
        /// How far around the center of a door apron to look for its door polys, on the mesh of an agent size. Erosion
        /// leaves them well inside the apron; vertically they are on the threshold.
        /// </summary>
        private static RcVec3f GetDoorPolyHalfExtents(AgentSize agentSize)
        {
            var horizontal = DoorApron.GetDepth(agentSize) * 0.5f + 0.25f;
            return new RcVec3f(horizontal, 0.6f, horizontal);
        }

        /// <summary>
        /// The outdoors of the world, per agent size.
        /// </summary>
        private static readonly Dictionary<AgentSize, OutdoorNavMesh> OutdoorMeshes = new Dictionary<AgentSize, OutdoorNavMesh>();

        /// <summary>
        /// Landblocks (0xAAAA0000) whose outdoor tiles are loaded, or loading.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, byte> OutdoorTilesRequested = new ConcurrentDictionary<uint, byte>();

        /// <summary>
        /// Per landblock, bumped when it unloads. A tile build that finishes after its landblock unloaded is discarded.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, int> OutdoorTileGenerations = new ConcurrentDictionary<uint, int>();

        /// <summary>
        /// Guards the generation check and the tile change that depends on it, against an unload in between.
        /// </summary>
        private static readonly object OutdoorTileSync = new object();

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

        /// <summary>
        /// A mesh to query, with a query rented from its pool. For the outdoor mesh it also holds the read lock, so
        /// tiles don't change while it is used. Use with 'using', on one thread.
        /// </summary>
        private sealed class MeshLease : IDisposable
        {
            private readonly ConcurrentQueue<DtNavMeshQuery> pool;
            private readonly OutdoorNavMesh outdoorMesh;
            private bool disposed;

            public readonly DtNavMeshQuery Query;
            public readonly MeshGroupKey Group;

            public bool IsOutdoor => outdoorMesh != null;

            public MeshLease(DtNavMesh mesh, MeshGroupKey group, OutdoorNavMesh outdoorMesh)
            {
                pool = QueryPools.GetValue(mesh, _ => new ConcurrentQueue<DtNavMeshQuery>());
                Query = pool.TryDequeue(out var query) ? query : new DtNavMeshQuery(mesh);
                Group = group;
                this.outdoorMesh = outdoorMesh;
            }

            /// <summary>
            /// A position as a point on this mesh: in world coordinates outdoors, landblock-local indoors.
            /// </summary>
            public RcVec3f ToMesh(Position pos) => ToMeshPoint(pos.Cell, pos.Pos, IsOutdoor);

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;

                if (pool.Count < MAX_POOLED_QUERIES_PER_MESH)
                    pool.Enqueue(Query);

                outdoorMesh?.ExitRead();
            }
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

            foreach (var agentSize in Enum.GetValues(typeof(AgentSize)).Cast<AgentSize>())
                OutdoorMeshes[agentSize] = new OutdoorNavMesh(agentSize, BlockLength, VERTS_PER_POLY);
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

        private static LandblockGroups GetGroups(uint cell)
        {
            return Groups.GetOrAdd(cell & 0xFFFF0000, id => new LandblockGroups(id));
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
        /// Creates a position on the landscape for a point in world coordinates.
        /// </summary>
        private static Position CreateOutdoorPosition(Vector3 global)
        {
            var x = Math.Clamp((int)MathF.Floor(global.X / BlockLength), 0, 0xFF);
            var y = Math.Clamp((int)MathF.Floor(global.Y / BlockLength), 0, 0xFF);
            var landblockId = GetLandblockId(x, y);

            var local = new Vector3(global.X - x * BlockLength, global.Y - y * BlockLength, global.Z);
            return new Position(GetLandCellId(landblockId, local), local, Quaternion.Identity);
        }

        private static uint ResolveInteriorCell(Position reference, int group, Vector3 point)
        {
            try
            {
                var probe = new Vector3(point.X, point.Y, point.Z + CELL_PROBE_HEIGHT);

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

        #endregion

        /// <summary>
        /// Find a route to the end position.
        /// Positions in different mesh groups have no direct route. If one of them is outdoors and the other in a
        /// building or cave, the route leads to the doorway between them; once there the caller can continue outside
        /// (or route again inside). Otherwise there is no route.
        /// </summary>
        /// <param name="end">The ending position</param>
        /// <returns>A list of positions</returns>
        public static List<Position>? FindRoute(Position start, Position end, AgentSize agentSize, bool drawRoute = false)
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
        public static List<Position>? FindRoute(Position start, Position end, AgentSize agentSize, out bool reachesTarget, bool drawRoute = false)
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

            var positionList = new List<Position>(waypoints.Count);
            foreach (var waypoint in waypoints)
                positionList.Add(new RoutePosition(CreatePosition(start, group, ToRcVec3f(waypoint.Pos)), waypoint.IsDropStart));

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
        private static List<Waypoint>? FindWaypoints(DtNavMeshQuery query, IDtQueryFilter pathFilter, long startRef, RcVec3f startPt, bool startOnIsland, RcVec3f target, out bool reachesTarget)
        {
            reachesTarget = false;

            // From the main area, a target next to an island (e.g. beside a table) is snapped to the floor rather
            // than onto the island; from an island, the target is looked up anywhere.
            var endFound = startOnIsland
                ? TryFindNearestPoly(query, AllPolysFilter, target, out var endRef, out var endPt)
                : TryFindNearestReachablePoly(query, target, out endRef, out endPt, out _);
            if (!endFound)
                return null; // the target is not on the mesh (outdoors: possibly its tile is not loaded yet)

            var polys = new long[MAX_POLYS];
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

            var path = new DtStraightPath[MAX_POLYS];
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

                // The landing of a drop must be a waypoint of its own, so the drop is followed as found. String pulling
                // can leave it out when the path goes on straight from it.
                if (isDropStart && TryGetDropLanding(query, polys, polyCount, entry.refs, out var landing) &&
                    (i + 1 >= straightPathCount || Vector3.DistanceSquared(ToVector3(path[i + 1].pos), landing.Pos) > 0.01f))
                    waypoints.Add(landing);
            }

            reachesTarget = reachesEnd;
            return waypoints;
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
                // entering a building or cave: its doorways, on the outdoor mesh
                if (!TryGetCellGroup(endGroup, out var entering))
                    return null;

                var enteringDoors = GetDoors(endGroup, entering, agentSize);
                if (enteringDoors.Count == 0)
                    return null;

                doors = enteringDoors.Select(d =>
                {
                    var at = ToGlobal(endGroup.LandblockId, d.Position);
                    return (new Position(GetLandCellId(endGroup.LandblockId, d.Position), d.Position, Quaternion.Identity), at, Vector3.Distance(at, endAt));
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
                    var otherDoors = otherDoorList.Select(d => ToGlobal(endGroup.LandblockId, d.Position)).ToList();
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
        /// A leg must end this close to a doorway for the route to continue on the other side.
        /// </summary>
        private const float DOOR_REACH_DISTANCE = 2.0f;

        /// <summary>
        /// Finds a route that may cross between mesh groups through doorways, by chaining FindRoute calls: each leg
        /// runs on one group's mesh, and when it ends at a doorway the next leg starts on the other side of it.
        /// Returns the legs in order (the last one may be partial), or null if not even the first leg was found.
        /// Outdoor tiles load in the background, so a leg through the outdoors can be missing or partial the first time.
        /// </summary>
        public static List<List<Position>>? FindRouteLegs(Position start, Position end, AgentSize agentSize)
        {
            return FindRouteLegs(start, end, agentSize, out _);
        }

        /// <summary>
        /// Like the other overload; reachesEnd is true if the legs lead all the way to the end position.
        /// </summary>
        public static List<List<Position>>? FindRouteLegs(Position start, Position end, AgentSize agentSize, out bool reachesEnd)
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
                if (group == endGroup || group.IsNone)
                {
                    // the final leg, complete or as close as this mesh gets
                    reachesEnd = group == endGroup && legReachesTarget;
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
        /// For a leg that ended at a doorway, returns the start of the next leg: the same spot in the group on the other
        /// side (the outdoors when leaving a building, the building's door cell when entering it).
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

            var legEndAt = ToGlobal(legEnd);
            var door = doors.OrderBy(d => Vector3.DistanceSquared(ToGlobal(doorGroup.LandblockId, d.Position), legEndAt)).First();
            if (Vector3.Distance(ToGlobal(doorGroup.LandblockId, door.Position), legEndAt) > DOOR_REACH_DISTANCE)
                return null; // the leg did not make it to the doorway

            return group.IsOutdoor
                ? new Position(door.CellId, door.Position, Quaternion.Identity)
                : new Position(GetLandCellId(doorGroup.LandblockId, door.Position), door.Position, Quaternion.Identity);
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

        private static DtNavMeshQuery RentQuery(DtNavMesh mesh, out ConcurrentQueue<DtNavMeshQuery> pool)
        {
            pool = QueryPools.GetValue(mesh, _ => new ConcurrentQueue<DtNavMeshQuery>());
            return pool.TryDequeue(out var query) ? query : new DtNavMeshQuery(mesh);
        }

        private static void ReturnQuery(ConcurrentQueue<DtNavMeshQuery> pool, DtNavMeshQuery query)
        {
            if (pool.Count < MAX_POOLED_QUERIES_PER_MESH)
                pool.Enqueue(query);
        }

        /// <summary>
        /// Flags the islands of a mesh with a single tile, before it is published: connected components of polys that
        /// cannot be walked to. A component is kept (not an island) if it is the largest one, holds one of the seed
        /// points (doorways), has floor connected to a door apron (see DoorApron), or, for an outdoor tile, reaches
        /// the tile edge where it may continue into the neighbor. The rest are tabletops, the tops of crates and rocks,
        /// pits you can only drop into, door aprons that can't be reached (windows) and the like. Sizes are compared
        /// without the door aprons, and a component that is only a door apron is never kept.
        /// Must run before the tile is published, since it changes poly flags.
        /// </summary>
        /// <param name="seeds">Points known to be reachable, on the mesh (Recast space)</param>
        private static void MarkIslands(DtNavMesh mesh, IEnumerable<RcVec3f> seeds, bool outdoor)
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
            // the area of each component without its door aprons, and whether it has any door apron polys
            var componentArea = new List<float>();
            var componentHasDoor = new List<bool>();
            var stack = new Stack<int>();

            // the drops (one-way off-mesh connections) between components, as (from, to) poly indices
            var drops = new List<(int dropPoly, int from, int to)>();

            for (var i = 0; i < polyCount; i++)
            {
                // drops are not part of any component, see below
                if (component[i] != -1 || data.polys[i].GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    continue;

                var id = componentArea.Count;
                var area = 0f;
                var hasDoor = false;
                component[i] = id;
                stack.Push(i);

                while (stack.Count > 0)
                {
                    var poly = data.polys[stack.Pop()];
                    if (poly.GetArea() == NavArea.Door)
                        hasDoor = true;
                    else
                        area += GetPolyArea(data, poly);

                    for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                    {
                        mesh.GetTileAndPolyByRefUnsafe(tile.links[link].refs, out var neighborTile, out var neighbor);
                        if (neighborTile != tile || neighbor == null || component[neighbor.index] != -1 ||
                            neighbor.GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                            continue;

                        component[neighbor.index] = id;
                        stack.Push(neighbor.index);
                    }
                }

                componentArea.Add(area);
                componentHasDoor.Add(hasDoor);
            }

            // A drop links to the poly at its start (edge 0) and the one it lands on (edge 1).
            for (var i = 0; i < polyCount; i++)
            {
                var poly = data.polys[i];
                if (poly.GetPolyType() != DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    continue;

                int from = -1, to = -1;
                for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                {
                    mesh.GetTileAndPolyByRefUnsafe(tile.links[link].refs, out var endTile, out var endPoly);
                    if (endTile != tile || endPoly == null)
                        continue;

                    if (tile.links[link].edge == 0)
                        from = endPoly.index;
                    else
                        to = endPoly.index;
                }

                drops.Add((i, from, to));
            }

            if (componentArea.Count == 1)
            {
                // all connected; a drop that isn't attached to the floor on both ends leads nowhere
                foreach (var (dropPoly, from, to) in drops)
                {
                    if (from < 0 || to < 0)
                        data.polys[dropPoly].flags |= NavPolyFlags.Island;
                }
                return;
            }

            var keep = new bool[componentArea.Count];

            var largest = 0;
            for (var c = 1; c < componentArea.Count; c++)
            {
                if (componentArea[c] > componentArea[largest])
                    largest = c;
            }
            keep[largest] = true;

            // A component with a door apron connected to its floor can be walked to through that doorway. A door apron
            // on its own (the doorway can't be reached from the floor, e.g. a window) is never kept.
            for (var c = 0; c < componentArea.Count; c++)
            {
                if (componentHasDoor[c] && componentArea[c] > 0f)
                    keep[c] = true;
            }

            var query = RentQuery(mesh, out var pool);
            try
            {
                foreach (var seed in seeds)
                {
                    var status = query.FindNearestPoly(seed, DoorSeedHalfExtents, AllPolysFilter, out var seedRef, out _, out _);
                    if (status.Failed() || seedRef == 0)
                        continue;

                    mesh.GetTileAndPolyByRefUnsafe(seedRef, out var seedTile, out var seedPoly);
                    if (seedTile == tile && seedPoly != null && component[seedPoly.index] >= 0 && componentArea[component[seedPoly.index]] > 0f)
                        keep[component[seedPoly.index]] = true;
                }
            }
            finally
            {
                ReturnQuery(pool, query);
            }

            if (outdoor)
            {
                // Recast space: x and z are the horizontal axes
                var bmin = data.header.bmin;
                var bmax = data.header.bmax;
                for (var i = 0; i < polyCount; i++)
                {
                    if (component[i] < 0 || keep[component[i]])
                        continue;

                    var poly = data.polys[i];
                    for (var v = 0; v < poly.vertCount; v++)
                    {
                        var x = data.verts[poly.verts[v] * 3];
                        var z = data.verts[poly.verts[v] * 3 + 2];
                        if (x <= bmin.X + TILE_EDGE_TOLERANCE || z <= bmin.Z + TILE_EDGE_TOLERANCE || x >= bmax.X - TILE_EDGE_TOLERANCE || z >= bmax.Z - TILE_EDGE_TOLERANCE)
                        {
                            keep[component[i]] = true;
                            break;
                        }
                    }
                }
            }

            // What a kept component drops down onto can be reached too (a pit, the floor below a balcony). Drops only
            // go down, so only the way down: a component kept this way can still be left only by its own drops.
            for (var changed = true; changed;)
            {
                changed = false;
                foreach (var (_, from, to) in drops)
                {
                    if (from < 0 || to < 0)
                        continue;

                    var fromComponent = component[from];
                    var toComponent = component[to];
                    if (keep[fromComponent] && !keep[toComponent] && componentArea[toComponent] > 0f)
                    {
                        keep[toComponent] = true;
                        changed = true;
                    }
                }
            }

            // a drop is usable if its start is
            foreach (var (dropPoly, from, to) in drops)
            {
                if (from < 0 || to < 0 || !keep[component[from]])
                    data.polys[dropPoly].flags |= NavPolyFlags.Island;
            }

            var islandPolys = 0;
            for (var i = 0; i < polyCount; i++)
            {
                if (component[i] < 0)
                    continue; // a drop, flagged above

                if (keep[component[i]])
                    continue;

                data.polys[i].flags |= NavPolyFlags.Island;
                islandPolys++;
            }

            if (islandPolys > 0)
                log.Debug($"Pathfinder: flagged {islandPolys} of {polyCount} polys as islands ({keep.Count(k => !k)} of {keep.Length} components)");
        }

        /// <summary>
        /// The doorways of a cell group an agent can get through on the group's mesh (built for that agent's size), from
        /// the door aprons it has left (see DoorApron). Islands must be marked first. Doorways that are not in a wall
        /// have no apron and are kept.
        /// </summary>
        private static List<CellGroupDoor> FindPassableDoors(DtNavMesh mesh, CellGroup cellGroup, AgentSize agentSize)
        {
            var passable = new List<CellGroupDoor>(cellGroup.Doors.Count);
            var aprons = cellGroup.Doors.Where(d => d.IsInWall).ToList();
            var halfExtents = GetDoorPolyHalfExtents(agentSize);

            var query = RentQuery(mesh, out var pool);
            try
            {
                foreach (var door in cellGroup.Doors)
                {
                    if (!door.IsInWall)
                    {
                        passable.Add(door);
                        continue;
                    }

                    var center = DoorApron.GetCenter(door, agentSize);
                    var status = query.FindNearestPoly(ToMeshPoint(0, center, false), halfExtents, PassableDoorFilter, out var doorRef, out var doorPt, out _);
                    if (status.Failed() || doorRef == 0)
                        continue;

                    // the door poly found could belong to the apron of a doorway next to this one
                    var found = FromMeshPoint(doorPt);
                    var owner = aprons.OrderBy(d => Vector3.DistanceSquared(DoorApron.GetCenter(d, agentSize), found)).First();
                    if (owner.CellId == door.CellId && owner.Position == door.Position)
                        passable.Add(door);
                }
            }
            finally
            {
                ReturnQuery(pool, query);
            }

            return passable;
        }

        /// <summary>
        /// The doorways of a cell group an agent of the given size can get through (see FindPassableDoors). Loads the
        /// group's mesh if it isn't yet. While the mesh is still being built that isn't known, and all doorways are
        /// returned.
        /// </summary>
        private static List<CellGroupDoor> GetDoors(MeshGroupKey key, CellGroup cellGroup, AgentSize agentSize)
        {
            var meshId = GetMeshId(key.LandblockId, key.Index, agentSize);
            if (!Meshes.TryGetValue(meshId, out var interiorMesh))
            {
                // cached meshes load right away
                TryLoadInteriorMesh(key.LandblockId, key.Index);
                Meshes.TryGetValue(meshId, out interiorMesh);
            }

            return interiorMesh?.PassableDoors ?? cellGroup.Doors;
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

            var previousRouteEntry = start;
            foreach (var routeEntry in route)
            {
                distance += previousRouteEntry.DistanceTo(routeEntry);
                previousRouteEntry = routeEntry;
            }
            return true;
        }

        /// <summary>
        /// Get a random point on the navmesh: anywhere on an interior mesh, within the landblock's reach outdoors.
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static Position? GetRandomPointOnMesh(Position start, AgentSize agentSize)
        {
            var group = GetGroupKey(start);
            if (group.IsOutdoor)
                return GetRandomPointWithinCircle(start, BlockLength / 2f, agentSize);

            using var lease = AcquireMesh(start, group, agentSize);
            if (lease == null)
                return null;

            var frand = new RcRand(DateTime.Now.Ticks);

            // never on an island, which nothing could walk to
            var status = lease.Query.FindRandomPoint(ReachableFilter, frand, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(start, group, randomPt);
        }

        public static Position? GetRandomPointWithinCircle(Position location, float radius, AgentSize agentSize)
        {
            var group = GetGroupKey(location);
            using var lease = AcquireMesh(location, group, agentSize);
            if (lease == null)
                return null;

            var query = lease.Query;
            var frand = new RcRand(DateTime.Now.Ticks);

            if (!TryFindNearestReachablePoly(query, lease.ToMesh(location), out var startRef, out var startPt, out var startOnIsland))
                return null;

            // the search spreads out from the start poly, so on an island it has to be allowed to stay on it
            var filter = startOnIsland ? AllPolysFilter : ReachableFilter;
            var status = query.FindRandomPointWithinCircle(startRef, startPt, radius, filter, frand, out long randomRef, out var randomPt);
            if (status.Failed() || randomRef == 0)
                return null;

            return CreatePosition(location, group, randomPt);
        }

        public static Position? GetClosestPointOnMesh(Position location, AgentSize agentSize)
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

        public static Position? GetNearestWallPosition(Position location, float radius, AgentSize agentSize, out float distance, bool inverseNormal = false)
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

            // no wall within the radius leaves the hit point and normal at zero
            if (wallPt.X == 0 && wallPt.Y == 0 && wallPt.Z == 0 && wallNormal.X == 0 && wallNormal.Y == 0 && wallNormal.Z == 0)
                return null;

            distance = wallDistance;

            var position = CreatePosition(location, group, wallPt);

            if (inverseNormal)
                position.Rotate(new Vector3(-wallNormal.X, -wallNormal.Z, -wallNormal.Y));
            else
                position.Rotate(new Vector3(wallNormal.X, wallNormal.Z, wallNormal.Y));

            return position;
        }

        #region Mesh access

        /// <summary>
        /// The mesh of a position's group, ready to query, or null if it is not loaded (yet). Interior meshes load from
        /// their cache right away; missing ones build in the background, as do outdoor tiles.
        /// </summary>
        private static MeshLease AcquireMesh(Position pos, MeshGroupKey group, AgentSize agentSize)
        {
            if (group.IsNone)
                return null;

            if (group.IsOutdoor)
                return AcquireOutdoorMesh(pos.Cell, agentSize);

            var meshId = GetMeshId(group.LandblockId, group.Index, agentSize);
            if (!Meshes.TryGetValue(meshId, out var interiorMesh))
            {
                TryLoadInteriorMesh(group.LandblockId, group.Index);
                Meshes.TryGetValue(meshId, out interiorMesh);
            }

            return interiorMesh == null || interiorMesh.IsPlaceholder ? null : new MeshLease(interiorMesh.Mesh, group, null);
        }

        /// <summary>
        /// The outdoor mesh, under its read lock, if the tile of the given landblock is in it. Otherwise requests the
        /// tile and returns null.
        /// </summary>
        private static MeshLease AcquireOutdoorMesh(uint cell, AgentSize agentSize)
        {
            var landblockId = cell & 0xFFFF0000;
            var outdoorMesh = OutdoorMeshes[agentSize];

            var hasTile = false;
            outdoorMesh.EnterRead();
            try
            {
                if (outdoorMesh.HasTile(landblockId))
                {
                    hasTile = true;
                    return new MeshLease(outdoorMesh.Mesh, MeshGroupKey.Outdoors, outdoorMesh);
                }
            }
            finally
            {
                // on success the lease releases the lock
                if (!hasTile)
                    outdoorMesh.ExitRead();
            }

            TryLoadOutdoorTile(landblockId);
            return null;
        }

        /// <summary>
        /// Requests the outdoor tiles of the landblocks between two positions (inclusive), as far as a route from a
        /// searches (MAX_ROUTE_LANDBLOCK_SPAN).
        /// </summary>
        private static void RequestOutdoorTilesBetween(Position a, Position b)
        {
            var (ax, ay) = GetLandblockCoords(a.Cell);
            var (bx, by) = GetLandblockCoords(b.Cell);

            bx = Math.Clamp(bx, ax - MAX_ROUTE_LANDBLOCK_SPAN, ax + MAX_ROUTE_LANDBLOCK_SPAN);
            by = Math.Clamp(by, ay - MAX_ROUTE_LANDBLOCK_SPAN, ay + MAX_ROUTE_LANDBLOCK_SPAN);

            for (var x = Math.Max(0, Math.Min(ax, bx)); x <= Math.Min(0xFF, Math.Max(ax, bx)); x++)
            {
                for (var y = Math.Max(0, Math.Min(ay, by)); y <= Math.Min(0xFF, Math.Max(ay, by)); y++)
                    TryLoadOutdoorTile(GetLandblockId(x, y));
            }
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

            // a mesh still loading for this landblock is dropped when it is done (see InteriorMesh)
            foreach (var meshId in Meshes.Keys.Where(k => (k & 0xFFFF0000) == landblockId).ToList())
                Meshes.TryRemove(meshId, out _);

            lock (OutdoorTileSync)
            {
                // a tile build still running for this landblock is discarded when it finishes
                OutdoorTileGenerations.AddOrUpdate(landblockId, 1, (_, generation) => generation + 1);

                foreach (var outdoorMesh in OutdoorMeshes.Values)
                    outdoorMesh.RemoveTile(landblockId);
            }

            OutdoorTilesRequested.TryRemove(landblockId, out _);
            Groups.TryRemove(landblockId, out _);
            OutdoorBuildFailures.TryRemove(landblockId, out _);
        }

        private static void UnloadMesh(uint cell, int group)
        {
            foreach (var agentSize in Enum.GetValues(typeof(AgentSize)).Cast<AgentSize>())
                Meshes.TryRemove(GetMeshId(cell, group, agentSize), out _);
        }

        /// <summary>
        /// Loads (or builds) the mesh of a position's group: its interior group mesh, or the outdoor tile of its
        /// landblock. With rebuildMesh, the mesh is built again from the geometry, ignoring the cache.
        /// </summary>
        public static void TryLoadMesh(Position pos, bool rebuildMesh = false)
        {
            var group = GetGroupKey(pos);

            if (group.IsOutdoor)
                TryLoadOutdoorTile(pos.Cell, rebuildMesh);
            else if (!group.IsNone)
                TryLoadInteriorMesh(group.LandblockId, group.Index, rebuildMesh);
        }

        #endregion

        #region Mesh building and caching

        /// <summary>
        /// The cache file of a mesh. The name includes a hash of the build settings and the geometry version, so a
        /// mesh built with other settings or older geometry is never loaded; it is rebuilt instead.
        /// </summary>
        private static string GetMeshPath(string directory, uint meshId, bool outdoor, AgentSize agentSize)
        {
            return Path.Combine(directory, $"{meshId:X8}_{GetMeshCacheKey(outdoor, agentSize)}.mesh");
        }

        private static string GetMeshCacheKey(bool outdoor, AgentSize agentSize)
        {
            return MeshCacheKeys.GetOrAdd((outdoor, agentSize), key =>
            {
                var s = GetMeshSettings(key.outdoor, key.agentSize);

                // Outdoor tiles have their own version (appended below). They keep the interior version they were
                // keyed with before it went past 1, so bumping MESH_GEOMETRY_VERSION doesn't invalidate them.
                var geometryVersion = key.outdoor ? 1 : MESH_GEOMETRY_VERSION;

                // partitioning is keyed by its number, as when the settings were the DotRecast toolset's
                var text = FormattableString.Invariant(
                    $"{geometryVersion}|{VERTS_PER_POLY}|{s.VertsPerPoly}|{(int)s.Partitioning}|{s.CellSize}|{s.CellHeight}|{s.AgentHeight}|{s.AgentRadius}|{s.AgentMaxClimb}|{s.AgentMaxSlope}|{s.MinRegionSize}|{s.MergedRegionSize}|{s.EdgeMaxLen}|{s.EdgeMaxError}|{s.DetailSampleDist}|{s.DetailSampleMaxError}|{s.FilterLowHangingObstacles}|{s.FilterLedgeSpans}|{s.FilterWalkableLowHeightSpans}|drops|{DropLinkFinder.Version}|{DropLinkFinder.MaxDropHeight}");

                if (key.outdoor)
                    text += FormattableString.Invariant($"|tiled|{OUTDOOR_MESH_GEOMETRY_VERSION}|{BlockLength}|{LandblockGeometryExporter.SurfaceMargin}");

                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 4);
            });
        }

        /// <summary>
        /// Reads cached mesh data. A file that cannot be read (e.g. truncated) is deleted, so the mesh gets rebuilt.
        /// </summary>
        private static bool TryReadMeshData(string meshPath, out DtMeshData meshData)
        {
            meshData = null;

            if (!File.Exists(meshPath))
                return false;

            try
            {
                var bytes = File.ReadAllBytes(meshPath);
                if (bytes.Length == 0)
                    throw new InvalidDataException("empty file");

                meshData = new DtMeshDataReader().Read(new RcByteBuffer(bytes), VERTS_PER_POLY, true);
                if (meshData?.header == null)
                    throw new InvalidDataException("no mesh data");

                return true;
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: discarding unreadable mesh file {meshPath}: {e.Message}");
                TryDeleteFile(meshPath);
                meshData = null;
                return false;
            }
        }

        /// <summary>
        /// Reads a cached single-tile (interior) mesh.
        /// </summary>
        private static bool TryReadMeshFile(string meshPath, out DtNavMesh? mesh)
        {
            mesh = null;

            if (!TryReadMeshData(meshPath, out var meshData))
                return false;

            mesh = CreateSingleTileMesh(meshData);
            if (mesh == null)
            {
                log.Warn($"Pathfinder: discarding mesh file {meshPath}: mesh init failed");
                TryDeleteFile(meshPath);
                return false;
            }
            return true;
        }

        private static DtNavMesh CreateSingleTileMesh(DtMeshData meshData)
        {
            var mesh = new DtNavMesh();
            var status = mesh.Init(meshData, VERTS_PER_POLY, 0);
            return status.Failed() ? null : mesh;
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

        /// <summary>
        /// Saves mesh data to its cache file, and deletes the cache files of the same mesh built with other settings or
        /// geometry, which are stale now.
        /// </summary>
        private static void SaveMeshData(DtMeshData meshData, uint meshId, string meshPath)
        {
            // write to a temporary file and move it into place, so an interrupted write never leaves a truncated mesh
            var tempPath = $"{meshPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var stream = File.Create(tempPath))
                using (var writer = new BinaryWriter(stream))
                {
                    new DtMeshDataWriter().Write(writer, meshData, RcByteOrder.LITTLE_ENDIAN, false);
                }
                File.Move(tempPath, meshPath, true);

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
        }

        private static DtNavMesh? BuildAndSaveMesh(CellGeometryProvider geom, MeshBuildSettings settings, uint meshId, string meshPath)
        {
            var builder = new NavMeshBuilder();
            var res = builder.Build(geom, settings);
            if (res is null)
                return null;

            var mesh = CreateSingleTileMesh(res);
            if (mesh == null)
            {
                log.Warn($"Pathfinder: could not initialize the built mesh 0x{meshId:X8}");
                return null;
            }

            SaveMeshData(res, meshId, meshPath);
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
        /// then the mesh is present as a placeholder (see InteriorMesh) and queries return no result.
        /// </summary>
        private static void TryLoadInteriorMesh(uint landblockId, int group, bool rebuildMesh = false)
        {
            landblockId &= 0xFFFF0000;
            var location = $"landblock 0x{landblockId:X8}";

            if (rebuildMesh)
                UnloadMesh(landblockId, group);

            if (!GetGroups(landblockId).ByIndex.TryGetValue(group, out var cellGroup))
                return;

            // Doorways in a wall have a door apron, which keeps their component by itself when it can be reached (see
            // MarkIslands). Seeding with them would also keep an apron that can't be reached, so only the others seed.
            var seeds = cellGroup.Doors.Where(d => !d.IsInWall).Select(d => ToMeshPoint(landblockId, d.Position, false)).ToList();
            var toBuild = new List<(AgentSize agentSize, uint meshId, string meshPath, InteriorMesh placeholder)>();

            foreach (var agentSize in Enum.GetValues(typeof(AgentSize)).Cast<AgentSize>())
            {
                var meshId = GetMeshId(landblockId, group, agentSize);
                var placeholder = InteriorMesh.CreatePlaceholder();
                if (!Meshes.TryAdd(meshId, placeholder))
                    continue;

                var meshPath = GetMeshPath(InteriorMeshDirectory, meshId, false, agentSize);
                try
                {
                    if (!rebuildMesh && TryReadMeshFile(meshPath, out var cachedMesh))
                    {
                        MarkIslands(cachedMesh, seeds, false);
                        Meshes.TryUpdate(meshId, new InteriorMesh(cachedMesh, FindPassableDoors(cachedMesh, cellGroup, agentSize)), placeholder);
                        continue;
                    }
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: could not load the {agentSize} mesh for cell group {group} at {location}, rebuilding it: {e.Message}");
                }

                toBuild.Add((agentSize, meshId, meshPath, placeholder));
            }

            if (toBuild.Count == 0)
                return;

            // a mesh unloaded or rebuilt since its build was queued no longer has this build's placeholder
            bool IsCurrent(uint meshId, InteriorMesh placeholder) => Meshes.TryGetValue(meshId, out var current) && current == placeholder;

            RunMeshBuild(() =>
            {
                // the cells are loaded once; each size gets its own door aprons (see DoorApron)
                Dictionary<AgentSize, CellGeometryProvider> geoms = null;

                foreach (var (agentSize, meshId, meshPath, placeholder) in toBuild)
                {
                    if (!IsCurrent(meshId, placeholder))
                        continue;

                    // On failure the placeholder stays, so no retry is made: the same cells would fail the same way
                    // until a rebuild is requested.
                    try
                    {
                        geoms ??= CellGeometryProvider.LoadGeometry(new LandblockGeometry(landblockId), cellGroup.Cells, cellGroup.Doors, toBuild.Select(b => b.agentSize));
                        // sealed rooms and orphan cells often have nothing walkable; that is expected, so only debug logging
                        if (geoms is null)
                        {
                            log.Debug($"Pathfinder: no geometry for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            return;
                        }

                        var stopwatch = Stopwatch.StartNew();
                        var mesh = BuildAndSaveMesh(geoms[agentSize], GetMeshSettings(false, agentSize), meshId, meshPath);
                        if (mesh is null)
                        {
                            log.Debug($"Pathfinder: could not build the {agentSize} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            continue;
                        }

                        MarkIslands(mesh, seeds, false);
                        if (!Meshes.TryUpdate(meshId, new InteriorMesh(mesh, FindPassableDoors(mesh, cellGroup, agentSize)), placeholder))
                        {
                            log.Debug($"Pathfinder: dropped the {agentSize} nav mesh built for cell group {group} at {location}: it was unloaded or rebuilt meanwhile");
                            continue;
                        }

                        log.Debug($"Pathfinder: built {agentSize} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location} in {stopwatch.ElapsedMilliseconds}ms ({mesh.GetTile(0)?.data?.header?.offMeshConCount ?? 0} drops)");
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Pathfinder: failed to build the {agentSize} mesh for cell group {group} at {location}: {e}");
                    }
                }
            });
        }

        /// <summary>
        /// Loads (or builds) the outdoor tiles of a loaded landblock in the background, for both agent sizes. Until it
        /// is done, the tile is missing from the outdoor mesh: routes from it find no mesh, routes through it end at its
        /// edge. Tiles are only added for loaded landblocks, and removed when they unload. Their geometry comes from
        /// the dat alone, so it doesn't depend on which landblocks are loaded.
        /// </summary>
        /// <param name="force">Build the tile from the geometry even if it is loaded or cached, and replace it</param>
        private static void TryLoadOutdoorTile(uint landblockId, bool force = false)
        {
            landblockId &= 0xFFFF0000;

            if (!LandblockManager.IsLoaded(new LandblockId(landblockId | 0xFFFF)))
                return;

            if (force)
            {
                OutdoorBuildFailures.TryRemove(landblockId, out _);
                OutdoorTilesRequested[landblockId] = 0;
            }
            else
            {
                if (OutdoorBuildFailures.TryGetValue(landblockId, out var failedAt) && DateTime.UtcNow - failedAt < OutdoorBuildRetryDelay)
                    return;

                if (!OutdoorTilesRequested.TryAdd(landblockId, 0))
                    return; // loaded or loading
            }

            var generation = OutdoorTileGenerations.GetOrAdd(landblockId, 0);

            RunMeshBuild(() => BuildOutdoorTile(landblockId, generation, force));
        }

        private static void BuildOutdoorTile(uint landblockId, int generation, bool force)
        {
            var (tileX, tileY) = GetLandblockCoords(landblockId);
            var seeds = GetOutdoorDoorPositions(landblockId);

            CellGeometryProvider geom = null;
            var geometryLoaded = false;

            foreach (var agentSize in Enum.GetValues(typeof(AgentSize)).Cast<AgentSize>())
            {
                var meshId = GetMeshId(landblockId, OutdoorGroup, agentSize);
                var meshPath = GetMeshPath(OutdoorMeshDirectory, meshId, true, agentSize);

                try
                {
                    DtMeshData data = null;
                    if (!force && TryReadMeshData(meshPath, out var cached))
                    {
                        if (cached.header.x == tileX && cached.header.y == tileY)
                            data = cached;
                        else
                            TryDeleteFile(meshPath);
                    }

                    if (data == null)
                    {
                        if (!geometryLoaded)
                        {
                            geometryLoaded = true;
                            geom = CellGeometryProvider.LoadSurfaceGeometry(landblockId, GetSurfaceSources(landblockId));
                        }

                        if (geom is null)
                        {
                            log.Warn($"Pathfinder: could not load outdoor geometry for 0x{landblockId:X8}");
                            FailOutdoorBuild(landblockId);
                            return;
                        }

                        var stopwatch = Stopwatch.StartNew();
                        data = new NavMeshBuilder().BuildTile(geom, GetMeshSettings(true, agentSize), tileX, tileY, BlockLength);
                        if (data == null)
                        {
                            // the same geometry would build the same way, so the request stays and no retry is made
                            log.Warn($"Pathfinder: could not build the {agentSize} outdoor tile for 0x{landblockId:X8}");
                            continue;
                        }

                        SaveMeshData(data, meshId, meshPath);

                        log.Info($"Pathfinder: built {agentSize} outdoor tile for 0x{landblockId:X8} in {stopwatch.ElapsedMilliseconds}ms ({geom.vertices.Length / 3} vertices, {geom.faces.Length / 3} triangles, {data.header.offMeshConCount} drops)");
                    }

                    // islands are flagged on the tile alone, before anything can query it
                    var tileMesh = CreateSingleTileMesh(data);
                    if (tileMesh == null)
                    {
                        log.Warn($"Pathfinder: could not initialize the {agentSize} outdoor tile for 0x{landblockId:X8}");
                        FailOutdoorBuild(landblockId);
                        return;
                    }
                    MarkIslands(tileMesh, seeds, true);

                    lock (OutdoorTileSync)
                    {
                        if (!OutdoorTileGenerations.TryGetValue(landblockId, out var current) || current != generation)
                            return; // the landblock unloaded while this was building

                        if (!OutdoorMeshes[agentSize].SetTile(landblockId, data))
                            log.Warn($"Pathfinder: could not add the {agentSize} outdoor tile for 0x{landblockId:X8}");
                    }
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: failed to load the {agentSize} outdoor tile for 0x{landblockId:X8}: {e}");
                    FailOutdoorBuild(landblockId);
                    return;
                }
            }
        }

        /// <summary>
        /// The landblock and its neighbors, as sources of tile geometry: terrain, buildings, static objects and scenery
        /// as the dat places them (see Physics.Common.Landblock.CreateDetached), whether they are loaded or not. Built
        /// on the build thread from the dat alone, so no landblock in use is read and nothing gets loaded.
        /// </summary>
        private static List<SurfaceSource> GetSurfaceSources(uint landblockId)
        {
            var sources = new List<SurfaceSource>(9);
            var (x, y) = GetLandblockCoords(landblockId);

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || ny < 0 || nx > 0xFF || ny > 0xFF)
                        continue;

                    var sourceId = GetLandblockId(nx, ny);
                    var landblock = ACE.Server.Physics.Common.Landblock.CreateDetached(sourceId);
                    if (landblock == null)
                        continue; // past the edge of the world

                    sources.Add(new SurfaceSource(sourceId, landblock, new SurfaceSnapshot(landblock), GetSurfaceInteriorCells(sourceId)));
                }
            }

            return sources;
        }

        /// <summary>
        /// The interior cells of a landblock's buildings and caves, read from the dat without caching the landblock's
        /// groups (the neighbors may not be loaded).
        /// </summary>
        private static List<CellGeometry> GetSurfaceInteriorCells(uint landblockId)
        {
            if (Groups.TryGetValue(landblockId & 0xFFFF0000, out var groups))
                return groups.ByIndex.Values.Where(g => g.ConnectsToOutside).SelectMany(g => g.Cells).ToList();

            return new LandblockGeometry(landblockId).SurfaceConnectedCells;
        }

        /// <summary>
        /// The doorways from the outdoors into the landblock's buildings and caves, on the outdoor mesh (Recast space),
        /// which anchor the outdoor mesh components around them (see MarkIslands).
        /// </summary>
        private static List<RcVec3f> GetOutdoorDoorPositions(uint landblockId)
        {
            return GetGroups(landblockId).ByIndex.Values
                .Where(g => g.ConnectsToOutside)
                .SelectMany(g => g.Doors)
                .Select(d => ToMeshPoint(landblockId, d.Position, true))
                .ToList();
        }

        /// <summary>
        /// Records a failed outdoor tile build: the tile can be requested again, but is not retried for
        /// OutdoorBuildRetryDelay.
        /// </summary>
        private static void FailOutdoorBuild(uint landblockId)
        {
            OutdoorBuildFailures[landblockId] = DateTime.UtcNow;
            OutdoorTilesRequested.TryRemove(landblockId, out _);
        }

        /// <summary>
        /// The voxel cell size of each mesh and the agent radius in whole cells. Recast erodes the walkable area by
        /// ceil(radius / cellSize) cells, so a radius that isn't a whole number of cells is rounded up (0.65m on
        /// 0.2m cells erodes 0.8m). Each mesh gets a cell size its agent radius divides into:
        ///
        ///   Interior: Small 0.65/7 (~0.093m) x 7 = 0.65m, Medium 0.1 x 10 = 1.0m, Large 0.125 x 14 = 1.75m.
        ///   Outdoor:  Small 0.16 x 4 = 0.64m, Medium 0.2 x 5 = 1.0m, Large 0.25 x 7 = 1.75m.
        ///
        /// Outdoor cell sizes must divide the landblock (192m) into a whole number of cells, as a tile must be; the
        /// only one that fits 0.65m exactly is 0.05m (16x the voxels), so Small erodes 1cm less there. Interiors keep
        /// finer grids for narrow doorways and stairs. Large needs no fine detail (Recast's guideline is a cell size of a
        /// half to a third of the agent radius), so its coarser grid also makes its meshes cheaper to build.
        /// </summary>
        private static (float cellSize, int radiusCells) GetCellSize(bool outdoor, AgentSize type)
        {
            switch (type)
            {
                case AgentSize.Small:
                    return outdoor ? (0.16f, 4) : (SmallAgentRadius / 7f, 7);
                case AgentSize.Medium:
                    return outdoor ? (0.2f, 5) : (0.1f, 10);
                default:
                    return outdoor ? (0.25f, 7) : (0.125f, 14);
            }
        }

        private static MeshBuildSettings GetMeshSettings(bool outdoor, AgentSize type)
        {
            var (cellSize, radiusCells) = GetCellSize(outdoor, type);

            var settings = new MeshBuildSettings()
            {
                AgentHeight = GetAgentHeight(type),
                AgentMaxClimb = GetAgentMaxClimb(type),
                AgentMaxSlope = 48f, // matches PhysicsGlobals.FloorZ (acos(0.664) = ~48.4 degrees), steeper is not walkable
                CellHeight = 0.1f,
                CellSize = cellSize,

                // a hair under the whole number of cells, so float error in radius / cellSize can't round it up a cell
                AgentRadius = (radiusCells - 0.01f) * cellSize,

                DetailSampleDist = 6.0f,
                DetailSampleMaxError = 1.0f,
                EdgeMaxError = 1f,
                EdgeMaxLen = 12.0f,
                VertsPerPoly = VERTS_PER_POLY,
                Partitioning = RcPartition.WATERSHED,
                FilterLowHangingObstacles = true,
                FilterLedgeSpans = true,
                FilterWalkableLowHeightSpans = true
            };

            // Recast's own conversion (RcConfig.WalkableRadius)
            if ((int)MathF.Ceiling(settings.AgentRadius / settings.CellSize) != radiusCells)
            {
                log.Error($"Pathfinder: the {type} {(outdoor ? "outdoor" : "interior")} agent radius {settings.AgentRadius} is not {radiusCells} cells of {settings.CellSize}m");
            }

            if (outdoor)
            {
                // longer edges keep the poly count down over a whole landblock
                settings.EdgeMaxLen = 24.0f;
            }

            // Recast takes the region sizes as the side, in cells, of a square of that area
            settings.MinRegionSize = GetRegionSize(MIN_REGION_AREA, settings.CellSize);
            settings.MergedRegionSize = GetRegionSize(MERGE_REGION_AREA, settings.CellSize);

            return settings;
        }

        private static int GetRegionSize(float area, float cellSize)
        {
            return Math.Max(1, (int)MathF.Round(MathF.Sqrt(area) / cellSize));
        }

        #endregion
    }
}
