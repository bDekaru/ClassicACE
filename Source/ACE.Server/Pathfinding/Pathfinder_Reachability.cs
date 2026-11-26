using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
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
using System.Threading;
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
        /// <param name="cancel">Stops the scan: null is returned, and the saved manifest is left as it was</param>
        public static ReachabilityManifest UpdateReachabilityManifest(CancellationToken cancel)
        {
            var manifest = ReachabilityManifest.Load(ReachabilityManifestPath) ?? new ReachabilityManifest();

            var worldKey = FormattableString.Invariant($"{TerrainPassVersion}|{PhysicsGlobals.FloorZ}|{GetMeshCacheKey(true, AgentSize.Small)}|{GetMeshCacheKey(false, AgentSize.Small)}");

            // the server build too: the fingerprints change with the code that collects the geometry
            var datIdentity = string.Join("|", new[] { DatManager.CellDat.FilePath, DatManager.PortalDat.FilePath }.Select(path =>
            {
                var file = new FileInfo(path);
                return $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
            })) + $"|{typeof(Pathfinder).Assembly.ManifestModule.ModuleVersionId}";

            // an input that couldn't be read (Unchecked) is read again only with other dats or another build
            if (manifest.WorldKey == worldKey && manifest.DatIdentity == datIdentity)
                return manifest;

            var worldChanged = manifest.WorldKey != worldKey;
            var changed = 0;

            // the scan reads the whole world, so what it adds to the cell dat cache is dropped afterwards, except for
            // landblocks in use
            var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);

            for (var i = 0; i < ReachabilityManifest.LandblockCount; i++)
            {
                if (cancel.IsCancellationRequested)
                {
                    DropAddedCellDatEntries(cachedBefore);
                    return null;
                }

                var landblockId = (uint)i << 16;
                ref var entry = ref manifest.Entries[i];

                // an input that can't be read is Unchecked and failed, so the next scan looks again
                (ReachabilityMethod method, ulong fingerprint) Read(Func<uint, (ReachabilityMethod, ulong)> getInput, string what)
                {
                    try
                    {
                        return getInput(landblockId);
                    }
                    catch (Exception e)
                    {
                        log.Warn($"Pathfinder: could not fingerprint the {what} of 0x{landblockId:X8} for reachability: {e}");
                        return (ReachabilityMethod.Unchecked, 0);
                    }
                }

                static ReachabilityStatus GetStatus(ReachabilityMethod method) => method switch
                {
                    ReachabilityMethod.None => ReachabilityStatus.Done,
                    ReachabilityMethod.Unchecked => ReachabilityStatus.Failed,
                    _ => ReachabilityStatus.Pending
                };

                var (method, fingerprint) = Read(GetReachabilityInput, "outdoors");
                if (worldChanged || entry.Method != method || entry.Fingerprint != fingerprint || entry.Status == ReachabilityStatus.Failed)
                {
                    (entry.Method, entry.Fingerprint, entry.Status) = (method, fingerprint, GetStatus(method));
                    changed++;
                }

                var (interiorMethod, interiorFingerprint) = Read(GetInteriorInput, "interiors");
                if (worldChanged || entry.InteriorMethod != interiorMethod || entry.InteriorFingerprint != interiorFingerprint || entry.InteriorStatus == ReachabilityStatus.Failed)
                {
                    (entry.InteriorMethod, entry.InteriorFingerprint, entry.InteriorStatus) = (interiorMethod, interiorFingerprint, GetStatus(interiorMethod));
                    changed++;
                }
            }

            DropAddedCellDatEntries(cachedBefore);

            manifest.WorldKey = worldKey;
            manifest.DatIdentity = datIdentity;
            manifest.Save(ReachabilityManifestPath);

            log.Info($"Pathfinder: reachability manifest updated, {changed} outdoor or interior inputs changed{(worldChanged ? " (new world key)" : "")}");
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
        /// The tile's connected components, the poly edges Detour links to the neighbor tiles, its ledge drops, the
        /// pieces in front of the doorways, and a map of the piece found near terrain height and the layers above it.
        /// False if the tile can't be read as a mesh.
        /// </summary>
        private static bool SummarizeRecast(TileSummary summary, DtMeshData data, List<Polygon> terrain)
        {
            var mesh = CreateTileMesh(data);
            if (mesh == null)
                return false;

            var tile = mesh.GetTile(0);
            var origin = GeometryHelpers.GetLandblockOrigin(summary.LandblockId);
            var component = AddSummaryPieces(mesh, tile, origin, summary.Pieces, summary.LedgeDrops);

            for (var i = 0; i < data.header.polyCount; i++)
            {
                if (component[i] < 0)
                    continue;

                // Detour's portal sides 0, 2, 4, 6 (+x, +z, -x, -z) are east, north, west, south
                var poly = data.polys[i];
                for (var j = 0; j < poly.vertCount; j++)
                {
                    if ((poly.neis[j] & DtDetour.DT_EXT_LINK) != 0)
                        AddSpan(summary, (ushort)component[i], (poly.neis[j] & 0xFF) / 2, GetLocalVertex(data, poly.verts[j], origin), GetLocalVertex(data, poly.verts[(j + 1) % poly.vertCount], origin));
                }
            }

            summary.Map = new ushort[TileSummary.MapSide * TileSummary.MapSide];
            var groundZ = new float[summary.Map.Length];
            var halfExtents = new RcVec3f(0.05f, 1.5f, 0.05f);
            using (var lease = new MeshLease(mesh))
            {
                // the piece the doorway's outdoor side is on, found as the door seeds of MarkIslands are
                foreach (var group in CellGroupLoader.LoadCellGroups(summary.LandblockId).Where(g => g.ConnectsToOutside && g.Index <= MaxGroup))
                {
                    for (var d = 0; d < group.Doors.Count; d++)
                    {
                        var point = ToMeshPoint(summary.LandblockId, GetDoorOutsidePoint(group.Doors[d], AgentSize.Small), true);
                        var status = lease.Query.FindNearestPoly(point, DoorSeedHalfExtents, AllPolysFilter, out var polyRef, out _, out _);
                        if (status.Failed() || polyRef == 0)
                            continue;

                        mesh.GetTileAndPolyByRefUnsafe(polyRef, out _, out var poly);
                        if (poly != null && component[poly.index] >= 0)
                            summary.Doors.Add(new SummaryDoor { Group = (ushort)group.Index, Door = (ushort)d, Piece = (ushort)component[poly.index] });
                    }
                }

                for (var x = 0; x < TileSummary.MapSide; x++)
                {
                    for (var y = 0; y < TileSummary.MapSide; y++)
                    {
                        var point = new Vector3((x + 0.5f) * TileSummary.MapCellSize, (y + 0.5f) * TileSummary.MapCellSize, 0f);
                        point.Z = GetTerrainZ(terrain, point);

                        var status = lease.Query.FindNearestPoly(ToMeshPoint(summary.LandblockId, point, true), halfExtents, AllPolysFilter, out var polyRef, out _, out _);
                        DtPoly poly = null;
                        if (status.Succeeded() && polyRef != 0)
                            mesh.GetTileAndPolyByRefUnsafe(polyRef, out _, out poly);

                        var cell = x * TileSummary.MapSide + y;
                        groundZ[cell] = point.Z;
                        summary.Map[cell] = poly != null && component[poly.index] >= 0 ? (ushort)component[poly.index] : TileSummary.NoPiece;
                    }
                }

                // the ground sample keeps only the poly nearest the terrain: every poly clearly above it is a layer of the
                // cells its bounds cover, at its height over the cell
                var baseRef = mesh.GetPolyRefBase(tile);
                var halfCell = TileSummary.MapCellSize * 0.5f;
                for (var i = 0; i < data.header.polyCount; i++)
                {
                    if (component[i] < 0)
                        continue;

                    var poly = data.polys[i];
                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    for (var j = 0; j < poly.vertCount; j++)
                    {
                        var v = GetLocalVertex(data, poly.verts[j], origin);
                        (minX, minY, maxX, maxY) = (Math.Min(minX, v.X), Math.Min(minY, v.Y), Math.Max(maxX, v.X), Math.Max(maxY, v.Y));
                    }

                    var piece = (ushort)component[i];
                    for (var x = GetMapIndex(minX); x <= GetMapIndex(maxX); x++)
                    {
                        for (var y = GetMapIndex(minY); y <= GetMapIndex(maxY); y++)
                        {
                            var cell = x * TileSummary.MapSide + y;
                            var center = new Vector3((x + 0.5f) * TileSummary.MapCellSize, (y + 0.5f) * TileSummary.MapCellSize, groundZ[cell]);
                            if (lease.Query.ClosestPointOnPoly(baseRef | (long)i, ToMeshPoint(summary.LandblockId, center, true), out var closest, out _).Failed())
                                continue;

                            var local = FromMeshPoint(closest) - origin;
                            if (local.Z <= groundZ[cell] + LayerTolerance || Math.Abs(local.X - center.X) > halfCell || Math.Abs(local.Y - center.Y) > halfCell)
                                continue;

                            if (!summary.Layers.TryGetValue(cell, out var layers))
                                summary.Layers[cell] = layers = new List<SummaryLayer>();
                            if (layers.Count < byte.MaxValue && !layers.Any(l => l.Piece == piece && Math.Abs(l.Z - local.Z) < LayerTolerance))
                                layers.Add(new SummaryLayer { Piece = piece, Z = local.Z });
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Adds a single-tile mesh's connected components (see FindComponents) as pieces, with their heights, and the
        /// ledge drops between them. Returns the piece of each poly, -1 for a ledge drop.
        /// </summary>
        /// <param name="origin">Where the landblock starts on the mesh: an outdoor tile is in world coordinates, an
        /// interior mesh in the landblock's own</param>
        private static int[] AddSummaryPieces(DtNavMesh mesh, DtMeshTile tile, Vector3 origin, List<SummaryPiece> pieces, List<(ushort from, ushort to)> ledgeDrops)
        {
            var data = tile.data;
            var (component, area, hasDoor) = FindComponents(mesh, tile);
            for (var c = 0; c < area.Count; c++)
                pieces.Add(new SummaryPiece { Area = area[c], MinZ = float.MaxValue, MaxZ = float.MinValue, Flags = hasDoor[c] ? PieceFlags.HasDoor : PieceFlags.None });

            for (var i = 0; i < data.header.polyCount; i++)
            {
                if (component[i] < 0)
                    continue;

                var poly = data.polys[i];
                ref var p = ref CollectionsMarshal.AsSpan(pieces)[component[i]];
                for (var j = 0; j < poly.vertCount; j++)
                {
                    var z = GetLocalVertex(data, poly.verts[j], origin).Z;
                    p.MinZ = Math.Min(p.MinZ, z);
                    p.MaxZ = Math.Max(p.MaxZ, z);
                }
            }

            foreach (var (_, from, to) in FindLedgeDrops(mesh, tile))
            {
                if (from < 0 || to < 0 || component[from] == component[to])
                    continue;

                var drop = ((ushort)component[from], (ushort)component[to]);
                if (!ledgeDrops.Contains(drop))
                    ledgeDrops.Add(drop);
            }

            return component;
        }

        private static Vector3 GetLocalVertex(DtMeshData data, int v, Vector3 origin) => FromMeshPoint(new RcVec3f(data.verts[v * 3], data.verts[v * 3 + 1], data.verts[v * 3 + 2])) - origin;

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
        /// How far a surface must be above the ground to be a layer, and how far a spot may be off a layer's height
        /// (Detour's surface error, a teleport destination a little above or below it) and still be on it.
        /// </summary>
        private const float LayerTolerance = 0.5f;

        private static int GetMapIndex(float local) => Math.Clamp((int)(local / TileSummary.MapCellSize), 0, TileSummary.MapSide - 1);

        private static float GetTerrainZ(List<Polygon> terrain, Vector3 local)
        {
            var plane = terrain[GetTerrainPolygon(terrain, local)].Plane;
            return -(plane.Normal.X * local.X + plane.Normal.Y * local.Y + plane.D) / plane.Normal.Z;
        }

        /// <summary>
        /// The piece under a landblock-local spot, NoPiece if there is none: for a spot clearly above the ground, the
        /// highest layer at or just below it, else the piece at ground level (always for z 0).
        /// </summary>
        /// <param name="terrain">The landblock's terrain polygons, if the caller has them: read from the dat otherwise</param>
        private static ushort GetSummaryPiece(TileSummary summary, Vector3 local, List<Polygon> terrain = null)
        {
            var max = LandDefs.BlockLength - 0.001f;
            var spot = new Vector3(Math.Clamp(local.X, 0f, max), Math.Clamp(local.Y, 0f, max), 0f);

            if (summary.Method == ReachabilityMethod.Recast)
            {
                var mapCell = GetMapIndex(spot.X) * TileSummary.MapSide + GetMapIndex(spot.Y);
                if (local.Z > LayerTolerance && summary.Layers.TryGetValue(mapCell, out var layers))
                {
                    terrain ??= ReadTerrain(summary.LandblockId);
                    if (terrain != null && local.Z > GetTerrainZ(terrain, spot) + LayerTolerance)
                    {
                        var best = -1;
                        for (var i = 0; i < layers.Count; i++)
                        {
                            if (layers[i].Z <= local.Z + LayerTolerance && (best < 0 || layers[i].Z > layers[best].Z))
                                best = i;
                        }
                        if (best >= 0)
                            return layers[best].Piece;
                    }
                }
                return summary.Map[mapCell];
            }

            // a cell's two triangles are nearly always the same piece; only when they differ does the split matter
            var cell = GetTerrainCell(spot);
            if (summary.Map[cell * 2] == summary.Map[cell * 2 + 1])
                return summary.Map[cell * 2];

            terrain ??= ReadTerrain(summary.LandblockId);
            return terrain != null ? summary.Map[GetTerrainPolygon(terrain, spot)] : TileSummary.NoPiece;
        }

        /// <summary>
        /// The landblock's terrain polygons from the dat, uncached; null if its terrain isn't complete.
        /// </summary>
        private static List<Polygon> ReadTerrain(uint landblockId)
        {
            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(landblockId | 0xFFFF, false);
            return HasFullTerrain(cellLandblock) ? new LandblockStruct(cellLandblock).Polygons : null;
        }

        private static volatile WorldGraph reachabilityGraph;

        /// <summary>
        /// The spot reachability is measured from: the surface outside the Abandoned Mine near Arwic, where the mine's
        /// Surface portal (wcid 1596) lands.
        /// </summary>
        public static readonly ACE.Entity.Position ReachabilityCenter = new ACE.Entity.Position(0xC3AB0021, 109.6f, 7.4f, 116.1f, 0f, 0f, -1f, 0f);

        /// <summary>
        /// Builds the graph of the world from the summaries (by landblock index, null where there is none) and the
        /// travel links, and puts it in use. Landblocks the manifest gives walkable ground or interiors but that have no
        /// summary are Unknown.
        /// </summary>
        public static WorldGraph BuildReachabilityGraph(TileSummary[] summaries, InteriorSummary[] interiors, ReachabilityManifest manifest)
        {
            var unknown = manifest.Entries.Select((entry, i) => summaries[i] == null && entry.Method != ReachabilityMethod.None).ToArray();
            var unknownInterior = manifest.Entries.Select((entry, i) => interiors[i] == null && entry.InteriorMethod != ReachabilityMethod.None).ToArray();

            var center = GetGraphSpot(summaries, interiors, ReachabilityCenter);
            if (summaries[center.Landblock] != null && center.Piece == TileSummary.NoPiece)
                log.Warn($"Pathfinder: reachability center 0x{ReachabilityCenter.Cell:X8} {ReachabilityCenter.Pos} is on no walkable piece, so it seeds nothing");

            // only landblocks with cells can be dungeons; one whose dat can't be read refuses recall, the safe side
            var dungeon = new bool[summaries.Length];
            var dungeonInterior = new bool[summaries.Length];
            for (var i = 0; i < summaries.Length; i++)
            {
                if (manifest.Entries[i].InteriorMethod == ReachabilityMethod.None)
                    continue;
                try
                {
                    (dungeon[i], dungeonInterior[i]) = GetDungeonFlags((uint)i << 16);
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: reading landblock {i:X4} for its dungeon flags failed, so recall is refused there: {e.Message}");
                    (dungeon[i], dungeonInterior[i]) = (true, true);
                }
            }

            var graph = WorldGraph.Build(summaries, unknown, interiors, unknownInterior, new[] { center }, GetTravelLinks(summaries, interiors), dungeon, dungeonInterior, SmallProfile.MaxClimb);
            reachabilityGraph = graph;
            return graph;
        }

        /// <summary>
        /// Whether WorldObject.InDungeon holds outdoors and indoors on a landblock, from the dat as Landblock.IsDungeon and
        /// HasDungeon read it: cells and no buildings, and outdoors also all heights 0 (but the northwest island).
        /// </summary>
        private static (bool outdoors, bool indoors) GetDungeonFlags(uint landblockId)
        {
            var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(landblockId | 0xFFFE, false);
            if (info == null || info.NumCells == 0 || info.Buildings == null || info.Buildings.Count > 0)
                return (false, false);

            if (landblockId >> 24 < 0x08 && (landblockId >> 16 & 0xFF) > 0xF8)
                return (false, true);

            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(landblockId | 0xFFFF, false);
            return (cellLandblock.Height.All(h => h == 0), true);
        }

        /// <summary>
        /// Whether a player of the level (any level if null) can get to a spot from the reachability center and back with
        /// the profile's travel. Outdoors it answers for the walkable surface at or just below the spot (the ground for a
        /// spot at ground level), indoors for the floor of the cell nearest the spot's height. Unknown until the graph is
        /// built.
        /// </summary>
        public static ReachabilityLabel GetReachability(ACE.Entity.Position position, int? level, TravelProfile profile) => GetReachability(reachabilityGraph, position, level, profile);

        private static ReachabilityLabel GetReachability(WorldGraph graph, ACE.Entity.Position position, int? level, TravelProfile profile = TravelProfile.Portals) =>
            graph?.GetLabel(GetGraphSpot(graph.Summaries, graph.Interiors, position), level, profile) ?? ReachabilityLabel.Unknown;

        /// <summary>
        /// The graph spot of a position: indoors the piece of its cell nearest its height, outdoors the surface under it.
        /// </summary>
        private static GraphSpot GetGraphSpot(TileSummary[] summaries, InteriorSummary[] interiors, ACE.Entity.Position position)
        {
            var landblock = (int)(position.Cell >> 16);
            if (position.Indoors)
            {
                var (group, piece) = interiors[landblock]?.FindPiece((ushort)position.Cell, position.Pos.Z) ?? (null, TileSummary.NoPiece);
                return new GraphSpot(landblock, group?.Index ?? 0, piece);
            }

            var summary = summaries[landblock];
            return new GraphSpot(landblock, GraphSpot.Outdoors, summary == null ? TileSummary.NoPiece : GetSummaryPiece(summary, position.Pos));
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
