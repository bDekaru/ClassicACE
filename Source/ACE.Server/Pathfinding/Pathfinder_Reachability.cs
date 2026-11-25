using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Geometry;
using ACE.Server.Pathfinding.Reachability;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Polygon = ACE.Server.Physics.Polygon;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The world reachability bake: which landblocks need their connectivity found again, from fingerprints of what
    /// each one is read from.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Part of the reachability world key: bump it when the terrain pass changes what it finds.
        /// </summary>
        private const int TerrainPassVersion = 1;

        public static string ReachabilityDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Reachability");

        private static string ReachabilityManifestPath => Path.Combine(ReachabilityDirectory, "manifest.bin");

        /// <summary>
        /// Brings the reachability manifest up to date with the dats and saves it: an entry whose method or fingerprint
        /// changed, or every entry if the world key changed, is pending again. While the dats are unchanged nothing is
        /// read. Reads the whole world (about a minute and a half), so it runs off the world thread.
        /// </summary>
        public static ReachabilityManifest UpdateReachabilityManifest()
        {
            var manifest = ReachabilityManifest.Load(ReachabilityManifestPath) ?? new ReachabilityManifest();

            var worldKey = FormattableString.Invariant($"{TerrainPassVersion}|{PhysicsGlobals.FloorZ}|{GetMeshCacheKey(true, AgentSize.Small)}");

            // the server build too: the fingerprints change with the code that collects the geometry
            var datIdentity = string.Join("|", new[] { DatManager.CellDat.FilePath, DatManager.PortalDat.FilePath }.Select(path =>
            {
                var file = new FileInfo(path);
                return $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
            })) + $"|{typeof(Pathfinder).Assembly.ManifestModule.ModuleVersionId}";

            if (manifest.WorldKey == worldKey && manifest.DatIdentity == datIdentity && !manifest.Entries.Any(e => e.Method == ReachabilityMethod.Unchecked))
                return manifest;

            var worldChanged = manifest.WorldKey != worldKey;
            var changed = 0;

            // the scan reads the whole world, so what it adds to the cell dat cache is dropped afterwards, except for
            // landblocks in use
            var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);

            for (var i = 0; i < ReachabilityManifest.LandblockCount; i++)
            {
                var landblockId = (uint)i << 16;
                ReachabilityMethod method;
                ulong fingerprint;
                try
                {
                    (method, fingerprint) = GetReachabilityInput(landblockId);
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: could not fingerprint 0x{landblockId:X8} for reachability: {e}");
                    manifest.Entries[i] = new ReachabilityEntry { Method = ReachabilityMethod.Unchecked, Status = ReachabilityStatus.Failed };
                    continue;
                }

                var entry = manifest.Entries[i];
                if (!worldChanged && entry.Method == method && entry.Fingerprint == fingerprint && entry.Status != ReachabilityStatus.Failed)
                    continue;

                manifest.Entries[i] = new ReachabilityEntry
                {
                    Method = method,
                    Status = method == ReachabilityMethod.None ? ReachabilityStatus.Done : ReachabilityStatus.Pending,
                    Fingerprint = fingerprint
                };
                changed++;
            }

            DropAddedCellDatEntries(cachedBefore);

            manifest.WorldKey = worldKey;
            manifest.DatIdentity = datIdentity;
            manifest.Save(ReachabilityManifestPath);

            log.Info($"Pathfinder: reachability manifest updated, {changed} landblocks changed{(worldChanged ? " (new world key)" : "")}");
            return manifest;
        }

        /// <summary>
        /// Drops the cell dat cache entries added since the snapshot, except for landblocks in use: a pass over the
        /// world would otherwise keep the whole world's records.
        /// </summary>
        private static void DropAddedCellDatEntries(HashSet<uint> cachedBefore)
        {
            foreach (var fileId in DatManager.CellDat.FileCache.Keys)
            {
                // x or y 0xFF is past the world (neighbors of the last row), never loaded
                var outsideWorld = fileId >> 24 == 0xFF || (fileId >> 16 & 0xFF) == 0xFF;
                if (!cachedBefore.Contains(fileId) && (outsideWorld || !LandblockManager.IsLoaded(new LandblockId(fileId | 0xFFFF))))
                    DatManager.CellDat.FileCache.TryRemove(fileId, out _);
            }
        }

        /// <summary>
        /// The summary of a landblock's outdoor surface, by its method: from the terrain, or from its Small (player)
        /// tile, cached or built. Null if the tile can't be built.
        /// </summary>
        private static TileSummary BuildTileSummary(uint landblockId, ReachabilityMethod method)
        {
            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(landblockId | 0xFFFF, false);
            if ((method != ReachabilityMethod.Terrain && method != ReachabilityMethod.Recast) || !HasFullTerrain(cellLandblock))
                return null;

            var landblock = new LandblockStruct(cellLandblock);
            var terrain = landblock.Polygons;
            var summary = new TileSummary { LandblockId = landblockId, Method = method };

            if (method == ReachabilityMethod.Terrain)
            {
                summary.Fingerprint = GetTerrainFingerprint(cellLandblock, landblock);
                SummarizeTerrain(summary, terrain);
                return summary;
            }

            var geom = LoadOutdoorGeometry(landblockId);
            if (geom == null)
                return null;

            summary.Fingerprint = geom.GetFingerprint();
            var data = LoadOutdoorTileData(landblockId, AgentSize.Small, geom, summary.Fingerprint, false);
            return data != null && SummarizeRecast(summary, data, terrain) ? summary : null;
        }

        /// <summary>
        /// Walkable terrain triangles (as physics takes them, normal.Z >= FloorZ) joined over shared edges.
        /// </summary>
        private static void SummarizeTerrain(TileSummary summary, List<Polygon> polys)
        {
            var parent = Enumerable.Range(0, polys.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

            var walkable = polys.Select(p => p.Plane.Normal.Z >= PhysicsGlobals.FloorZ).ToArray();
            var edges = new Dictionary<(int, int), int>();
            for (var i = 0; i < polys.Count; i++)
            {
                if (!walkable[i])
                    continue;

                for (var e = 0; e < 3; e++)
                {
                    int a = polys[i].VertexIDs[e], b = polys[i].VertexIDs[(e + 1) % 3];
                    var key = (Math.Min(a, b), Math.Max(a, b));
                    if (edges.TryGetValue(key, out var other))
                        parent[Find(i)] = Find(other);
                    else
                        edges[key] = i;
                }
            }

            summary.Map = new ushort[polys.Count];
            Array.Fill(summary.Map, TileSummary.NoPiece);
            var pieceOfRoot = new Dictionary<int, ushort>();
            for (var i = 0; i < polys.Count; i++)
            {
                if (!walkable[i])
                    continue;

                var root = Find(i);
                if (!pieceOfRoot.TryGetValue(root, out var piece))
                {
                    piece = (ushort)summary.Pieces.Count;
                    pieceOfRoot[root] = piece;
                    summary.Pieces.Add(new SummaryPiece { MinZ = float.MaxValue, MaxZ = float.MinValue });
                }
                summary.Map[i] = piece;

                var v = polys[i].Vertices;
                ref var p = ref CollectionsMarshal.AsSpan(summary.Pieces)[piece];
                p.Area += Vector3.Cross(v[1].Origin - v[0].Origin, v[2].Origin - v[0].Origin).Length() * 0.5f;
                p.MinZ = Math.Min(p.MinZ, Math.Min(v[0].Origin.Z, Math.Min(v[1].Origin.Z, v[2].Origin.Z)));
                p.MaxZ = Math.Max(p.MaxZ, Math.Max(v[0].Origin.Z, Math.Max(v[1].Origin.Z, v[2].Origin.Z)));

                for (var e = 0; e < 3; e++)
                {
                    var a = v[e].Origin;
                    var b = v[(e + 1) % 3].Origin;
                    var side = a.X == LandDefs.BlockLength && b.X == LandDefs.BlockLength ? 0
                        : a.Y == LandDefs.BlockLength && b.Y == LandDefs.BlockLength ? 1
                        : a.X == 0 && b.X == 0 ? 2
                        : a.Y == 0 && b.Y == 0 ? 3 : -1;
                    if (side >= 0)
                        AddSpan(summary, piece, side, a, b);
                }
            }
        }

        /// <summary>
        /// The tile's connected components, the poly edges Detour links to the neighbor tiles, its ledge drops, and a
        /// map of the piece found near terrain height. False if the tile can't be read as a mesh.
        /// </summary>
        private static bool SummarizeRecast(TileSummary summary, DtMeshData data, List<Polygon> terrain)
        {
            var mesh = CreateTileMesh(data);
            if (mesh == null)
                return false;

            var tile = mesh.GetTile(0);
            var (component, area, hasDoor) = FindComponents(mesh, tile);
            for (var c = 0; c < area.Count; c++)
                summary.Pieces.Add(new SummaryPiece { Area = area[c], MinZ = float.MaxValue, MaxZ = float.MinValue, Flags = hasDoor[c] ? PieceFlags.HasDoor : PieceFlags.None });

            var origin = GeometryHelpers.GetLandblockOrigin(summary.LandblockId);
            Vector3 GetLocalVertex(int v) => FromMeshPoint(new RcVec3f(data.verts[v * 3], data.verts[v * 3 + 1], data.verts[v * 3 + 2])) - origin;

            for (var i = 0; i < data.header.polyCount; i++)
            {
                if (component[i] < 0)
                    continue;

                var poly = data.polys[i];
                var piece = (ushort)component[i];
                ref var p = ref CollectionsMarshal.AsSpan(summary.Pieces)[piece];
                for (var j = 0; j < poly.vertCount; j++)
                {
                    var z = GetLocalVertex(poly.verts[j]).Z;
                    p.MinZ = Math.Min(p.MinZ, z);
                    p.MaxZ = Math.Max(p.MaxZ, z);
                }

                // Detour's portal sides 0, 2, 4, 6 (+x, +z, -x, -z) are east, north, west, south
                for (var j = 0; j < poly.vertCount; j++)
                {
                    if ((poly.neis[j] & DtDetour.DT_EXT_LINK) != 0)
                        AddSpan(summary, piece, (poly.neis[j] & 0xFF) / 2, GetLocalVertex(poly.verts[j]), GetLocalVertex(poly.verts[(j + 1) % poly.vertCount]));
                }
            }

            foreach (var (_, from, to) in FindLedgeDrops(mesh, tile))
            {
                if (from < 0 || to < 0 || component[from] == component[to])
                    continue;

                var drop = ((ushort)component[from], (ushort)component[to]);
                if (!summary.LedgeDrops.Contains(drop))
                    summary.LedgeDrops.Add(drop);
            }

            summary.Map = new ushort[TileSummary.MapSide * TileSummary.MapSide];
            var halfExtents = new RcVec3f(0.05f, 1.5f, 0.05f);
            using (var lease = new MeshLease(mesh))
            {
                for (var x = 0; x < TileSummary.MapSide; x++)
                {
                    for (var y = 0; y < TileSummary.MapSide; y++)
                    {
                        var point = new Vector3((x + 0.5f) * TileSummary.MapCellSize, (y + 0.5f) * TileSummary.MapCellSize, 0f);
                        var ground = terrain[GetTerrainPolygon(terrain, point)];
                        point.Z = -(ground.Plane.Normal.X * point.X + ground.Plane.Normal.Y * point.Y + ground.Plane.D) / ground.Plane.Normal.Z;

                        var status = lease.Query.FindNearestPoly(ToMeshPoint(summary.LandblockId, point, true), halfExtents, AllPolysFilter, out var polyRef, out _, out _);
                        DtPoly poly = null;
                        if (status.Succeeded() && polyRef != 0)
                            mesh.GetTileAndPolyByRefUnsafe(polyRef, out _, out poly);

                        summary.Map[x * TileSummary.MapSide + y] = poly != null && component[poly.index] >= 0 ? (ushort)component[poly.index] : TileSummary.NoPiece;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// The index of the terrain triangle under a landblock-local spot, in LandblockStruct.Polygons order.
        /// </summary>
        private static int GetTerrainPolygon(List<Polygon> terrain, Vector3 local)
        {
            var cell = GetTerrainCell(local);
            return terrain[cell * 2].point_in_poly2D(local, Sidedness.Positive) ? cell * 2 : cell * 2 + 1;
        }

        /// <summary>
        /// The terrain cell under a landblock-local spot; its two triangles are polygons cell * 2 and cell * 2 + 1.
        /// </summary>
        private static int GetTerrainCell(Vector3 local) => (int)(local.X / LandDefs.CellLength) * LandDefs.BlockSide + (int)(local.Y / LandDefs.CellLength);

        /// <summary>
        /// The piece at ground level under a landblock-local spot, NoPiece if there is none.
        /// </summary>
        /// <param name="terrain">The landblock's terrain polygons, if the caller has them: read from the dat otherwise</param>
        private static ushort GetSummaryPiece(TileSummary summary, Vector3 local, List<Polygon> terrain = null)
        {
            var max = LandDefs.BlockLength - 0.001f;
            var spot = new Vector3(Math.Clamp(local.X, 0f, max), Math.Clamp(local.Y, 0f, max), 0f);

            if (summary.Method == ReachabilityMethod.Recast)
                return summary.Map[(int)(spot.X / TileSummary.MapCellSize) * TileSummary.MapSide + (int)(spot.Y / TileSummary.MapCellSize)];

            // a cell's two triangles are nearly always the same piece; only when they differ does the split matter
            var cell = GetTerrainCell(spot);
            if (summary.Map[cell * 2] == summary.Map[cell * 2 + 1])
                return summary.Map[cell * 2];

            if (terrain != null)
                return summary.Map[GetTerrainPolygon(terrain, spot)];

            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(summary.LandblockId | 0xFFFF, false);
            return HasFullTerrain(cellLandblock) ? summary.Map[GetTerrainPolygon(new LandblockStruct(cellLandblock).Polygons, spot)] : TileSummary.NoPiece;
        }

        private static volatile WorldGraph reachabilityGraph;

        /// <summary>
        /// Builds the walking graph of the world from the summaries (by landblock index, null where there is none) and
        /// puts it in use. Landblocks the manifest gives walkable ground but that have no summary are Unknown.
        /// </summary>
        public static WorldGraph BuildReachabilityGraph(TileSummary[] summaries, ReachabilityManifest manifest)
        {
            var unknown = manifest.Entries.Select((entry, i) => summaries[i] == null && entry.Method != ReachabilityMethod.None).ToArray();

            var seeds = PlayerFactory.StarterTownLocations.Values.SelectMany(spots => spots).Select(spot =>
            {
                var landblock = (int)(spot.Cell >> 16);
                if (summaries[landblock] == null)
                    return (landblock, TileSummary.NoPiece);

                var piece = GetSummaryPiece(summaries[landblock], spot.Pos);
                if (piece == TileSummary.NoPiece)
                    log.Warn($"Pathfinder: start spot 0x{spot.Cell:X8} {spot.Pos} is on no walkable piece, so it seeds nothing");
                return (landblock, piece);
            }).ToList();

            var graph = WorldGraph.Build(summaries, unknown, seeds, SmallProfile.MaxClimb);
            reachabilityGraph = graph;
            return graph;
        }

        /// <summary>
        /// Whether a spot can be walked to from a start spot and back. Answers for the ground under the spot (a roof or
        /// platform gets the ground's label). Unknown until the graph is built, and for interiors (not in the graph
        /// yet).
        /// </summary>
        public static ReachabilityLabel GetReachability(ACE.Entity.Position position)
        {
            var graph = reachabilityGraph;
            if (graph == null || position.Indoors)
                return ReachabilityLabel.Unknown;

            var landblock = (int)(position.Cell >> 16);
            var summary = graph.Summaries[landblock];
            return graph.GetLabel(landblock, summary == null ? TileSummary.NoPiece : GetSummaryPiece(summary, position.Pos));
        }

        private static void AddSpan(TileSummary summary, ushort piece, int side, Vector3 a, Vector3 b)
        {
            // east and west run along y, north and south along x
            float alongA = side % 2 == 0 ? a.Y : a.X, alongB = side % 2 == 0 ? b.Y : b.X;
            if (alongA > alongB)
                (alongA, alongB, a, b) = (alongB, alongA, b, a);

            summary.Spans.Add(new SummarySpan { Piece = piece, Side = (byte)side, Start = alongA, End = alongB, StartZ = a.Z, EndZ = b.Z });
            CollectionsMarshal.AsSpan(summary.Pieces)[piece].Flags |= PieceFlags.TouchesEdge;
        }

        /// <summary>
        /// How a landblock's connectivity is found, and the fingerprint of what that reads: the Recast tile's geometry
        /// (the same fingerprint as its cache file, see GetMeshPath), or the terrain's heights and types. Recast for a
        /// landblock with objects, or one a neighbor's object reaches into.
        /// </summary>
        private static (ReachabilityMethod method, ulong fingerprint) GetReachabilityInput(uint landblockId)
        {
            // a scan of the whole world, so the landblocks are left out of the dat cache
            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(landblockId | 0xFFFF, false);
            if (!HasFullTerrain(cellLandblock))
                return (ReachabilityMethod.None, 0);

            // physics blocks entirely water landblocks (see LandCell)
            var terrain = new LandblockStruct(cellLandblock);
            if (terrain.WaterType == LandDefs.WaterType.EntirelyWater)
                return (ReachabilityMethod.None, 0);

            if (cellLandblock.HasObjects || NeighborObjectMayReachIn(landblockId))
            {
                var geom = LoadOutdoorGeometry(landblockId);
                if (geom != null && (cellLandblock.HasObjects || geom.NeighborObjectsInside))
                    return (ReachabilityMethod.Recast, geom.GetFingerprint());

                if (cellLandblock.HasObjects)
                    return (ReachabilityMethod.None, 0);
            }

            return (ReachabilityMethod.Terrain, GetTerrainFingerprint(cellLandblock, terrain));
        }

        private static bool HasFullTerrain(CellLandblock cellLandblock)
        {
            return cellLandblock != null && cellLandblock.Height.Count == LandDefs.VertexDim * LandDefs.VertexDim && cellLandblock.Terrain.Count == LandDefs.VertexDim * LandDefs.VertexDim;
        }

        private static ulong GetTerrainFingerprint(CellLandblock cellLandblock, LandblockStruct terrain)
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                hash.AppendData(MemoryMarshal.AsBytes(terrain.VertexArray.Vertices.Select(v => v.Origin.Z).ToArray().AsSpan()));
                hash.AppendData(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(cellLandblock.Terrain)));
                return BitConverter.ToUInt64(hash.GetHashAndReset(), 0);
            }
        }

        /// <summary>
        /// Slack on GetModelReach for the gap between a model's shape and how it is placed and posed in the physics.
        /// </summary>
        private const float ModelReachTolerance = 1.0f;

        /// <summary>
        /// Whether a neighbor has a building or static object close enough, for its size, to reach into the landblock: a
        /// cheap test from the dat records before the tile geometry, which says for sure (NeighborObjectsInside).
        /// </summary>
        private static bool NeighborObjectMayReachIn(uint landblockId)
        {
            var (x, y) = GetLandblockCoords(landblockId);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx > 0xFE || ny > 0xFE)
                        continue;

                    var neighborId = (uint)(nx << 24 | ny << 16);
                    if (DatManager.CellDat.ReadFromDat<CellLandblock>(neighborId | 0xFFFF, false)?.HasObjects != true)
                        continue;

                    var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(neighborId | 0xFFFE, false);
                    if (info == null)
                        return true;

                    // the neighbor's local frame, moved into this landblock's: the distance to the rect [0, BlockLength]
                    var offset = new Vector2(dx * LandDefs.BlockLength, dy * LandDefs.BlockLength);
                    bool Reaches(Vector3 origin, uint modelId)
                    {
                        var at = new Vector2(origin.X, origin.Y) + offset;
                        var outside = at - Vector2.Clamp(at, Vector2.Zero, new Vector2(LandDefs.BlockLength));
                        return outside.Length() < GetModelReach(modelId) + ModelReachTolerance;
                    }

                    if (info.Buildings.Any(b => Reaches(b.Frame.Origin, b.ModelId)) || info.Objects.Any(o => Reaches(o.Frame.Origin, o.Id)))
                        return true;
                }
            }
            return false;
        }

        private static readonly ConcurrentDictionary<uint, float> ModelReach = new ConcurrentDictionary<uint, float>();

        /// <summary>
        /// How far a model (GfxObj or Setup) can reach from its origin in any orientation: an upper bound from its
        /// vertices, collision spheres and part placements. Infinite if it can't be read, so it always counts.
        /// </summary>
        private static float GetModelReach(uint modelId)
        {
            return ModelReach.GetOrAdd(modelId, id =>
            {
                if (id >> 24 == 0x01)
                    return GetGfxObjReach(id);

                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(id);
                if (setup == null)
                    return float.PositiveInfinity;

                var reach = 0f;
                foreach (var sphere in setup.Spheres)
                    reach = Math.Max(reach, sphere.Origin.Length() + sphere.Radius);
                foreach (var cylSphere in setup.CylSpheres)
                    reach = Math.Max(reach, cylSphere.Origin.Length() + cylSphere.Radius + cylSphere.Height);

                var offsets = Enumerable.Range(0, setup.Parts.Count)
                    .Select(part => setup.PlacementFrames.Values.Select(placement => part < placement.AnimFrame.Frames.Count ? placement.AnimFrame.Frames[part].Origin.Length() : 0f).DefaultIfEmpty(0f).Max())
                    .ToArray();

                for (var i = 0; i < setup.Parts.Count; i++)
                {
                    // a part's frame is relative to its parent's: the distances add up along the chain
                    var placed = 0f;
                    for (int p = i, depth = 0; p >= 0 && p < setup.Parts.Count && depth <= setup.Parts.Count; depth++)
                    {
                        placed += offsets[p];
                        p = p < setup.ParentIndex.Count ? (int)setup.ParentIndex[p] : -1;
                    }

                    var scale = i < setup.DefaultScale.Count ? Math.Max(setup.DefaultScale[i].X, Math.Max(setup.DefaultScale[i].Y, setup.DefaultScale[i].Z)) : 1f;
                    reach = Math.Max(reach, placed + GetGfxObjReach(setup.Parts[i]) * scale);
                }
                return reach;
            });
        }

        private static float GetGfxObjReach(uint gfxObjId)
        {
            var gfxObj = DatManager.PortalDat.ReadFromDat<GfxObj>(gfxObjId);
            return gfxObj == null ? float.PositiveInfinity : gfxObj.VertexArray.Vertices.Values.Select(v => v.Origin.Length()).DefaultIfEmpty(0f).Max();
        }
    }
}
