//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Geometry;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The meshes in memory and on disk: loading, building, saving and unloading interior meshes and outdoor tiles.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Part of the interior mesh cache file names. Build setting changes are picked up automatically; bump this
        /// when the exported geometry changes, so cached meshes built from the old geometry are not loaded.
        /// </summary>
        private const int MeshGeometryVersion = 6; // 2: door aprons, 3: cell statics clipped to the cells they collide in, aprons built per agent size, 4: cell statics shaped as the server's physics objects, spheres at their centers, 5: cells only grouped through two-way portals, rim doors for portals not in a wall, 6: no small sphere obstacles

        /// <summary>
        /// Like MeshGeometryVersion, for the outdoor tiles. 2: tiles in world coordinates with a border from the
        /// neighboring landblocks. 3: the objects of every landblock come from the dat, loaded or not. 4: spheres at
        /// their centers. 5: no small sphere obstacles.
        /// </summary>
        private const int OutdoorMeshGeometryVersion = 5;

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
        /// A query holds per-search state and is not thread safe, while a mesh can be shared. Queries are pooled per
        /// mesh instead of created per call. The table holds meshes weakly, so an unloaded mesh's pool goes with it.
        /// </summary>
        private static readonly ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>> QueryPools = new ConditionalWeakTable<DtNavMesh, ConcurrentQueue<DtNavMeshQuery>>();

        private const int MaxPooledQueriesPerMesh = 16;

        /// <summary>
        /// Smallest walkable region kept, and the size below which regions are merged into their neighbors, in m².
        /// In world units so they don't change with the cell size.
        /// </summary>
        private const float RegionMinArea = 0.64f;

        private const float RegionMergeArea = 4.0f;

        public static string InteriorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Interiors");

        public static string OutdoorMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Outdoors");

        /// <summary>
        /// The interior group meshes, by mesh id (see GetMeshId): a placeholder while a mesh is loading, or an empty or
        /// failed entry for a load that ended without one (see InteriorMesh).
        /// </summary>
        private static readonly ConcurrentDictionary<uint, InteriorMesh> Meshes = new ConcurrentDictionary<uint, InteriorMesh>();

        /// <summary>
        /// An interior group mesh and the doorways of its cell group an agent of the mesh's size can get through: those
        /// whose door apron (see DoorApron) is connected to the rest of the mesh. Published together, so the doorways
        /// always belong to the mesh.
        /// A load claims its mesh id with a placeholder of its own, and only replaces that placeholder with the mesh: a
        /// mesh that was unloaded or rebuilt while it was loading has another placeholder or none, and the stale result
        /// is dropped. ConcurrentDictionary.TryUpdate compares by reference here, as Equals isn't overridden.
        /// A load that ends without a mesh (nothing walkable for the size, or a failed build) replaces its placeholder
        /// the same way, with an empty or failed one, which stays until the landblock unloads or the mesh is rebuilt.
        /// </summary>
        private sealed class InteriorMesh
        {
            public readonly DtNavMesh Mesh;
            public readonly List<CellGroupDoor> PassableDoors;

            /// <summary>
            /// Loaded with a mesh, Loading for a placeholder, or Empty or Failed for a load that ended without one.
            /// </summary>
            public readonly MeshLoadStatus Status;

            /// <summary>
            /// No mesh to query: still loading, empty or failed.
            /// </summary>
            public bool IsPlaceholder => Mesh == null;

            public bool IsLoading => Status == MeshLoadStatus.Loading;

            public InteriorMesh(DtNavMesh mesh, List<CellGroupDoor> passableDoors)
            {
                Mesh = mesh;
                PassableDoors = passableDoors;
                Status = MeshLoadStatus.Loaded;
            }

            private InteriorMesh(MeshLoadStatus status)
            {
                Status = status;
            }

            public static InteriorMesh CreatePlaceholder() => new InteriorMesh(MeshLoadStatus.Loading);

            public static InteriorMesh CreateEmpty() => new InteriorMesh(MeshLoadStatus.Empty);

            public static InteriorMesh CreateFailed() => new InteriorMesh(MeshLoadStatus.Failed);
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
        /// Requested landblocks (0xAAAA0000) whose outdoor tile build has finished: a tile of an agent size that is
        /// not in the mesh then was built without one (see GetMeshLoadStatus).
        /// </summary>
        private static readonly ConcurrentDictionary<uint, byte> OutdoorTilesBuilt = new ConcurrentDictionary<uint, byte>();

        /// <summary>
        /// Per landblock, bumped when it unloads. A tile build that finishes after its landblock unloaded is discarded.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, int> OutdoorTileGenerations = new ConcurrentDictionary<uint, int>();

        /// <summary>
        /// Guards the generation check and the tile change that depends on it, against an unload in between.
        /// </summary>
        private static readonly object OutdoorTileSync = new object();

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

                // last, so a failure above leaves no lock behind
                outdoorMesh?.EnterRead();
            }

            /// <summary>
            /// A mesh on its own, e.g. one being prepared before it is published: no group, no lock.
            /// </summary>
            public MeshLease(DtNavMesh mesh) : this(mesh, MeshGroupKey.None, null)
            {
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

                if (pool.Count < MaxPooledQueriesPerMesh)
                    pool.Enqueue(Query);

                outdoorMesh?.ExitRead();
            }
        }

        // No file system work here: a static constructor that throws leaves the whole type unusable, PathfindingEnabled
        // included. The mesh folders are created when a mesh is saved (see SaveMeshData).
        static Pathfinder()
        {
            foreach (var agentSize in AgentSizes)
                OutdoorMeshes[agentSize] = new OutdoorNavMesh(agentSize, Position.BlockLength, MaxVertsPerPoly);
        }

        /// <summary>
        /// Where the mesh a position is on stands in loading, for an agent size: its interior group mesh, or the
        /// outdoor tile of its landblock (see MeshLoadStatus). Asking loads nothing: a mesh that is NotLoaded stays so
        /// until it is requested (see TryLoadMesh).
        /// </summary>
        public static MeshLoadStatus GetMeshLoadStatus(Position pos, AgentSize agentSize)
        {
            var group = GetGroupKey(pos);
            if (group.IsNone)
                return MeshLoadStatus.NotCovered;

            if (group.IsOutdoor)
                return GetOutdoorTileStatus(pos.Cell & 0xFFFF0000, agentSize);

            if (!Meshes.TryGetValue(GetMeshId(group.LandblockId, group.Index, agentSize), out var interiorMesh))
                return MeshLoadStatus.NotLoaded;

            return interiorMesh.Status;
        }

        /// <summary>
        /// Where a landblock's outdoor tile of an agent size stands in loading (see TryLoadOutdoorTile). A requested
        /// tile is loading until the landblock's build finishes, and empty if that build added none for the size.
        /// A failed build is retried when the tile is requested after OutdoorBuildRetryDelay.
        /// </summary>
        private static MeshLoadStatus GetOutdoorTileStatus(uint landblockId, AgentSize agentSize)
        {
            if (OutdoorMeshes[agentSize].HasTile(landblockId))
                return MeshLoadStatus.Loaded;

            if (OutdoorTilesRequested.ContainsKey(landblockId))
                return OutdoorTilesBuilt.ContainsKey(landblockId) ? MeshLoadStatus.Empty : MeshLoadStatus.Loading;

            if (OutdoorBuildFailures.ContainsKey(landblockId))
                return MeshLoadStatus.Failed;

            return MeshLoadStatus.NotLoaded;
        }

        /// <summary>
        /// The mesh of a position's group, ready to query, or null if it is not loaded (yet). A mesh that isn't
        /// loaded is loaded in the background (see TryLoadInteriorMesh), as outdoor tiles are.
        /// </summary>
        private static MeshLease AcquireMesh(Position pos, MeshGroupKey group, AgentSize agentSize)
        {
            if (group.IsNone)
                return null;

            if (group.IsOutdoor)
                return AcquireOutdoorMesh(pos.Cell, agentSize);

            if (!Meshes.TryGetValue(GetMeshId(group.LandblockId, group.Index, agentSize), out var interiorMesh))
            {
                TryLoadInteriorMesh(group.LandblockId, group.Index);
                return null;
            }

            return interiorMesh.IsPlaceholder ? null : new MeshLease(interiorMesh.Mesh, group, null);
        }

        /// <summary>
        /// The outdoor mesh, under its read lock, if the tile of the given landblock is in it. Otherwise requests the
        /// tile and returns null.
        /// </summary>
        private static MeshLease AcquireOutdoorMesh(uint cell, AgentSize agentSize)
        {
            var landblockId = cell & 0xFFFF0000;
            var outdoorMesh = OutdoorMeshes[agentSize];

            var lease = new MeshLease(outdoorMesh.Mesh, MeshGroupKey.Outdoors, outdoorMesh);
            if (outdoorMesh.HasTile(landblockId))
                return lease;

            lease.Dispose();
            TryLoadOutdoorTile(landblockId);
            return null;
        }

        /// <summary>
        /// Requests the outdoor tiles of the landblocks between two positions (inclusive), as far as a route search
        /// reaches (MaxRouteLandblockSpan).
        /// </summary>
        private static void RequestOutdoorTilesBetween(Position a, Position b)
        {
            var (ax, ay) = GetLandblockCoords(a.Cell);
            var (bx, by) = GetLandblockCoords(b.Cell);

            bx = Math.Clamp(bx, ax - MaxRouteLandblockSpan, ax + MaxRouteLandblockSpan);
            by = Math.Clamp(by, ay - MaxRouteLandblockSpan, ay + MaxRouteLandblockSpan);

            for (var x = Math.Max(0, Math.Min(ax, bx)); x <= Math.Min(0xFF, Math.Max(ax, bx)); x++)
            {
                for (var y = Math.Max(0, Math.Min(ay, by)); y <= Math.Min(0xFF, Math.Max(ay, by)); y++)
                    TryLoadOutdoorTile(GetLandblockId(x, y));
            }
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
            OutdoorTilesBuilt.TryRemove(landblockId, out _);
            Groups.TryRemove(landblockId, out _);
            OutdoorBuildFailures.TryRemove(landblockId, out _);
        }

        private static void UnloadMesh(uint cell, int group)
        {
            foreach (var agentSize in AgentSizes)
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
                // keyed with before it went past 1, so bumping MeshGeometryVersion doesn't invalidate them.
                var geometryVersion = key.outdoor ? 1 : MeshGeometryVersion;

                // partitioning is keyed by its number, as when the settings were the DotRecast toolset's
                var text = FormattableString.Invariant(
                    $"{geometryVersion}|{MaxVertsPerPoly}|{s.VertsPerPoly}|{(int)s.Partitioning}|{s.CellSize}|{s.CellHeight}|{s.AgentHeight}|{s.AgentRadius}|{s.AgentMaxClimb}|{s.AgentMaxSlope}|{s.MinRegionArea}|{s.MergedRegionArea}|{s.EdgeMaxLen}|{s.EdgeMaxError}|{s.DetailSampleDist}|{s.DetailSampleMaxError}|{s.FilterLowHangingObstacles}|{s.FilterLedgeSpans}|{s.FilterWalkableLowHeightSpans}|ledgedrops|{LedgeDropFinder.Version}|{LedgeDropFinder.MaxLedgeDropHeight}");

                if (key.outdoor)
                    text += FormattableString.Invariant($"|tiled|{OutdoorMeshGeometryVersion}|{Position.BlockLength}|{MeshGeometryCollector.SurfaceMargin}");

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

                meshData = new DtMeshDataReader().Read(new RcByteBuffer(bytes), MaxVertsPerPoly, true);
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
        private static bool TryReadMeshFile(string meshPath, out DtNavMesh mesh)
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
            var status = mesh.Init(meshData, MaxVertsPerPoly, 0);
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
                // nothing if it exists; recreated if it was deleted while the server runs
                Directory.CreateDirectory(Path.GetDirectoryName(meshPath));

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

        private static DtNavMesh BuildAndSaveMesh(NavMeshGeometry geom, MeshBuildSettings settings, uint meshId, string meshPath)
        {
            var data = NavMeshBuilder.Build(geom, settings);
            if (data is null)
                return null;

            var mesh = CreateSingleTileMesh(data);
            if (mesh == null)
            {
                log.Warn($"Pathfinder: could not initialize the built mesh 0x{meshId:X8}");
                return null;
            }

            SaveMeshData(data, meshId, meshPath);
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
        /// Loads the meshes of one interior cell group: only that group's cells and their static objects. Everything
        /// runs in the background: cached meshes load in tens of ms for a large dungeon, too long for the tick that asks
        /// for them, and missing ones are built, which can take seconds. Until then the mesh is present as a
        /// placeholder (see InteriorMesh) and queries return no result.
        /// </summary>
        private static void TryLoadInteriorMesh(uint landblockId, int group, bool rebuildMesh = false)
        {
            landblockId &= 0xFFFF0000;

            // only a landblock's unload frees its meshes (see TryUnloadMesh)
            if (!LandblockManager.IsLoaded(new LandblockId(landblockId | 0xFFFF)))
                return;

            var location = $"landblock 0x{landblockId:X8}";

            if (rebuildMesh)
                UnloadMesh(landblockId, group);

            if (!GetGroups(landblockId).ByIndex.TryGetValue(group, out var cellGroup))
                return;

            // Doorways in a wall have a door apron, which keeps their component by itself when it can be reached (see
            // MarkIslands). Seeding with them would also keep an apron that can't be reached, so only the others seed.
            var seeds = cellGroup.Doors.Where(d => !d.IsInWall).Select(d => ToMeshPoint(landblockId, d.Position, false)).ToList();
            var toLoad = new List<(AgentSize agentSize, uint meshId, string meshPath, InteriorMesh placeholder)>();

            foreach (var agentSize in AgentSizes)
            {
                var meshId = GetMeshId(landblockId, group, agentSize);
                var placeholder = InteriorMesh.CreatePlaceholder();
                if (Meshes.TryAdd(meshId, placeholder))
                    toLoad.Add((agentSize, meshId, GetMeshPath(InteriorMeshDirectory, meshId, false, agentSize), placeholder));
            }

            if (toLoad.Count == 0)
                return;

            // a mesh unloaded or rebuilt since its load was queued no longer has this load's placeholder
            bool IsCurrent(uint meshId, InteriorMesh placeholder) => Meshes.TryGetValue(meshId, out var current) && current == placeholder;

            // cached meshes load right away, outside the build slots, so they don't wait for other landblocks' builds
            Task.Run(() =>
            {
                var toBuild = new List<(AgentSize agentSize, uint meshId, string meshPath, InteriorMesh placeholder)>();

                foreach (var entry in toLoad)
                {
                    var (agentSize, meshId, meshPath, placeholder) = entry;
                    if (!IsCurrent(meshId, placeholder))
                        continue;

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

                    toBuild.Add(entry);
                }

                if (toBuild.Count > 0)
                    BuildInteriorMeshes(toBuild);
            });

            void BuildInteriorMeshes(List<(AgentSize agentSize, uint meshId, string meshPath, InteriorMesh placeholder)> toBuild) => RunMeshBuild(() =>
            {
                // the cells are loaded once; each size gets its own door aprons (see DoorApron)
                Dictionary<AgentSize, NavMeshGeometry> geoms = null;

                foreach (var (agentSize, meshId, meshPath, placeholder) in toBuild)
                {
                    if (!IsCurrent(meshId, placeholder))
                        continue;

                    // A mesh that comes out empty or fails is marked so, and no retry is made: the same cells would fail the same way
                    // until a rebuild is requested.
                    try
                    {
                        geoms ??= NavMeshGeometry.LoadGeometry(cellGroup.Cells, cellGroup.Doors, toBuild.Select(b => b.agentSize));
                        // sealed rooms and orphan cells often have nothing walkable; that is expected, so only debug logging
                        if (geoms is null)
                        {
                            log.Debug($"Pathfinder: no geometry for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            foreach (var entry in toBuild)
                                MarkEmpty(entry.meshId, entry.placeholder);
                            return;
                        }

                        var stopwatch = Stopwatch.StartNew();
                        var mesh = BuildAndSaveMesh(geoms[agentSize], GetMeshSettings(false, agentSize), meshId, meshPath);
                        if (mesh is null)
                        {
                            log.Debug($"Pathfinder: could not build the {agentSize} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location}");
                            MarkEmpty(meshId, placeholder);
                            continue;
                        }

                        MarkIslands(mesh, seeds, false);
                        if (!Meshes.TryUpdate(meshId, new InteriorMesh(mesh, FindPassableDoors(mesh, cellGroup, agentSize)), placeholder))
                        {
                            log.Debug($"Pathfinder: dropped the {agentSize} nav mesh built for cell group {group} at {location}: it was unloaded or rebuilt meanwhile");
                            continue;
                        }

                        log.Debug($"Pathfinder: built {agentSize} nav mesh for cell group {group} ({cellGroup.Cells.Count} cells) at {location} in {stopwatch.ElapsedMilliseconds}ms ({mesh.GetTile(0)?.data?.header?.offMeshConCount ?? 0} ledge drops)");
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Pathfinder: failed to build the {agentSize} mesh for cell group {group} at {location}: {e}");
                        MarkFailed(meshId, placeholder);
                    }
                }
            });

            // only while the load's own placeholder is there: a mesh unloaded or rebuilt meanwhile is left alone
            void MarkEmpty(uint meshId, InteriorMesh placeholder) => Meshes.TryUpdate(meshId, InteriorMesh.CreateEmpty(), placeholder);

            void MarkFailed(uint meshId, InteriorMesh placeholder) => Meshes.TryUpdate(meshId, InteriorMesh.CreateFailed(), placeholder);
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
                OutdoorTilesBuilt.TryRemove(landblockId, out _);
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
            // false once the landblock has unloaded since the build was queued
            bool IsCurrent() => OutdoorTileGenerations.TryGetValue(landblockId, out var current) && current == generation;

            if (!IsCurrent())
                return;

            var (tileX, tileY) = GetLandblockCoords(landblockId);

            NavMeshGeometry geom = null;
            var geometryLoaded = false;

            foreach (var agentSize in AgentSizes)
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
                            geom = NavMeshGeometry.LoadSurfaceGeometry(landblockId, GetSurfaceSources(landblockId));
                        }

                        if (geom is null)
                        {
                            log.Warn($"Pathfinder: could not load outdoor geometry for 0x{landblockId:X8}");
                            FailOutdoorBuild(landblockId);
                            return;
                        }

                        var stopwatch = Stopwatch.StartNew();
                        data = NavMeshBuilder.BuildTile(geom, GetMeshSettings(true, agentSize), tileX, tileY, Position.BlockLength);
                        if (data == null)
                        {
                            // the same geometry would build the same way, so the request stays and no retry is made
                            log.Warn($"Pathfinder: could not build the {agentSize} outdoor tile for 0x{landblockId:X8}");
                            continue;
                        }

                        SaveMeshData(data, meshId, meshPath);

                        log.Info($"Pathfinder: built {agentSize} outdoor tile for 0x{landblockId:X8} in {stopwatch.ElapsedMilliseconds}ms ({geom.Vertices.Length / 3} vertices, {geom.Faces.Length / 3} triangles, {data.header.offMeshConCount} ledge drops)");
                    }

                    // islands are flagged on the tile alone, before anything can query it
                    var tileMesh = CreateSingleTileMesh(data);
                    if (tileMesh == null)
                    {
                        log.Warn($"Pathfinder: could not initialize the {agentSize} outdoor tile for 0x{landblockId:X8}");
                        FailOutdoorBuild(landblockId);
                        return;
                    }
                    MarkIslands(tileMesh, GetOutdoorDoorPositions(landblockId, agentSize), true);

                    lock (OutdoorTileSync)
                    {
                        if (!IsCurrent())
                            return;

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

            // a size whose tile is still not in the mesh now was built without one (see GetOutdoorTileStatus)
            lock (OutdoorTileSync)
            {
                if (IsCurrent())
                    OutdoorTilesBuilt[landblockId] = 0;
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
        /// The doorways from the outdoors into the landblock's buildings and caves, where they meet the outdoor mesh of
        /// an agent size (see GetDoorOutsidePoint), in Recast space. They anchor the outdoor mesh components around them
        /// (see MarkIslands).
        /// </summary>
        private static List<RcVec3f> GetOutdoorDoorPositions(uint landblockId, AgentSize agentSize)
        {
            return GetGroups(landblockId).ByIndex.Values
                .Where(g => g.ConnectsToOutside)
                .SelectMany(g => g.Doors)
                .Select(d => ToMeshPoint(landblockId, GetDoorOutsidePoint(d, agentSize), true))
                .ToList();
        }

        /// <summary>
        /// Records a failed outdoor tile build: the tile can be requested again, but is not retried for
        /// OutdoorBuildRetryDelay.
        /// </summary>
        private static void FailOutdoorBuild(uint landblockId)
        {
            OutdoorBuildFailures[landblockId] = DateTime.UtcNow;
            OutdoorTilesBuilt.TryRemove(landblockId, out _);
            OutdoorTilesRequested.TryRemove(landblockId, out _);
        }

        private static MeshBuildSettings GetMeshSettings(bool outdoor, AgentSize type)
        {
            // the cell size and the radius in whole cells (see AgentProfile)
            var profile = GetProfile(type);
            var cellSize = outdoor ? profile.OutdoorCellSize : profile.InteriorCellSize;
            var radiusCells = outdoor ? profile.OutdoorRadiusCells : profile.InteriorRadiusCells;

            var settings = new MeshBuildSettings
            {
                AgentHeight = profile.Height,
                AgentMaxClimb = profile.MaxClimb,
                AgentMaxSlope = 48f, // matches PhysicsGlobals.FloorZ (acos(0.664) = ~48.4 degrees), steeper is not walkable
                CellHeight = 0.1f,
                CellSize = cellSize,

                // a hair under the whole number of cells, so float error in radius / cellSize can't round it up a cell
                AgentRadius = (radiusCells - 0.01f) * cellSize,

                DetailSampleDist = 6.0f,
                DetailSampleMaxError = 1.0f,
                EdgeMaxError = 1f,

                // longer edges outdoors keep the poly count down over a whole landblock
                EdgeMaxLen = outdoor ? 24.0f : 12.0f,

                VertsPerPoly = MaxVertsPerPoly,
                MinRegionArea = RegionMinArea,
                MergedRegionArea = RegionMergeArea,
                Partitioning = RcPartition.WATERSHED,
                FilterLowHangingObstacles = true,
                FilterLedgeSpans = true,
                FilterWalkableLowHeightSpans = true
            };

            // Recast's own conversion (RcConfig.WalkableRadius)
            if ((int)MathF.Ceiling(settings.AgentRadius / settings.CellSize) != radiusCells)
                log.Error($"Pathfinder: the {type} {(outdoor ? "outdoor" : "interior")} agent radius {settings.AgentRadius} is not {radiusCells} cells of {settings.CellSize}m");

            return settings;
        }
    }
}
