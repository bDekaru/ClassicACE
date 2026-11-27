using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Server.Physics.Common;
using ACE.Server.Pathfinding.Reachability;
using DotRecast.Detour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Checks of the world reachability graph, for /reachability: a report of what it says, and a verify of the
    /// summaries against Detour's own links and against Recast tiles of terrain-pass landblocks.
    /// </summary>
    public static partial class Pathfinder
    {
        public const int DefaultVerifySamples = 150;

        /// <summary>
        /// The fewest 2 m cells two pieces must share before they count as overlapping (40 m2), so slivers along piece
        /// edges don't.
        /// </summary>
        private const int VerifyMinCells = 10;

        public static string ReachabilityReportPath => Path.Combine(ReachabilityDirectory, "report.txt");

        public static string ReachabilityVerifyPath => Path.Combine(ReachabilityDirectory, "verify.txt");

        public static bool HasReachabilityGraph => reachabilityGraph != null;

        /// <summary>
        /// Writes the report file and returns its first lines (labels and the reachability center). Null without a graph.
        /// </summary>
        public static string WriteReachabilityReport()
        {
            var graph = reachabilityGraph;
            var manifest = reachabilityManifest;
            if (graph == null || manifest == null)
                return null;

            var summaries = graph.Summaries;
            var lines = new List<string> { $"Graph from {reachabilityGraphTime:yyyy-MM-dd HH:mm:ss} UTC: {graph.GroupCount} groups, center in {graph.SeedGroupCount}, level bands from {string.Join(" ", graph.BandLevels)}" };

            var reportLevels = new int?[] { 1, 50, WorldGraph.MaxLevel, null };
            string LevelName(int? level) => level == null ? "best at any level" : $"level {level}";

            var pieces = Enumerable.Range(0, summaries.Length).Where(i => summaries[i] != null)
                .SelectMany(i => summaries[i].Pieces.Select((piece, k) => (landblock: i, index: k, piece, spot: new GraphSpot(i, GraphSpot.Outdoors, (ushort)k))))
                .ToList();
            string OutdoorTotals(int? level, TravelProfile profile) =>
                string.Join(", ", pieces.GroupBy(p => graph.GetLabel(p.spot, level, profile)).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()} pieces {g.Sum(p => p.piece.Area) / 1e6:F1} km2"));
            foreach (var level in reportLevels)
                lines.Add($"Outdoors, {LevelName(level)}: {OutdoorTotals(level, TravelProfile.Portals)}");
            foreach (var profile in new[] { TravelProfile.Walk, TravelProfile.Recall })
                foreach (var level in new int?[] { 1, null })
                    lines.Add($"Outdoors, {(profile == TravelProfile.Walk ? "on foot only" : "with recall")}, {LevelName(level)}: {OutdoorTotals(level, profile)}");

            lines.Add($"Reachability center {ReachabilityCenter.Cell:X8}: {DescribeReachability(ReachabilityCenter, null)}");
            lines.Add($"Travel links: {graph.Links.Count} ({string.Join(", ", graph.Links.GroupBy(l => l.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}"))}, {graph.Links.Count(l => l.Anywhere)} of them gems from anywhere), {graph.DeadLinks.Count} with an end on no piece");
            var shownLines = lines.Count;

            var unknown = Enumerable.Range(0, summaries.Length).Where(i => summaries[i] == null && manifest.Entries[i].Method != ReachabilityMethod.None).ToList();
            lines.Add($"Landblocks without a summary (Unknown): {unknown.Count}" + (unknown.Count <= 50 ? " " + string.Join(" ", unknown.Select(i => $"{i:X4}({manifest.Entries[i].Status.ToString().ToLowerInvariant()})")) : ""));

            var interiors = graph.Interiors;
            var interiorPieces = Enumerable.Range(0, interiors.Length).Where(i => interiors[i] != null)
                .SelectMany(i => interiors[i].Groups.SelectMany(g => g.Pieces.Select((piece, k) => (landblock: i, group: g.Index, index: k, piece, spot: new GraphSpot(i, g.Index, (ushort)k), hasDoors: g.Doors.Count > 0))))
                .ToList();
            lines.Add($"Interiors: {interiors.Count(s => s != null)} landblocks, {interiorPieces.Count} pieces, {graph.DoorJoinCount} doorways joined to the outside, {graph.DoorUnjoinedCount} not joined to the outside, {graph.TouchingDoorJoinCount} doorway pairs joined to each other");
            foreach (var level in reportLevels)
            {
                lines.Add($"  {LevelName(level)}:");
                foreach (var group in interiorPieces.GroupBy(p => graph.GetLabel(p.spot, level)).OrderBy(g => g.Key))
                    lines.Add($"    {group.Key}: {group.Count()} pieces, {group.Sum(p => p.piece.Area):F0} m2, in {group.Select(p => p.landblock).Distinct().Count()} landblocks");
            }
            lines.Add("  with recall, level 1: " + string.Join(", ", interiorPieces.GroupBy(p => graph.GetLabel(p.spot, 1, TravelProfile.Recall)).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()} pieces")));

            var levelDependent = pieces.Select(p => p.spot).Concat(interiorPieces.Select(p => p.spot)).Count(spot => graph.BandLevels.Select(l => graph.GetLabel(spot, l)).Distinct().Count() > 1);
            lines.Add($"Pieces whose label changes with the level: {levelDependent}");

            string FormatSpot(GraphSpot spot) => $"{spot.Landblock:X4} {(spot.Group == GraphSpot.Outdoors ? "outdoors" : $"group {spot.Group}")} piece {(spot.Piece == TileSummary.NoPiece ? "none" : spot.Piece.ToString())}";
            lines.Add("Travel links with an end on no piece (first 40):");
            foreach (var link in graph.DeadLinks.Take(40))
                lines.Add($"  {link.Kind} {link.Source}: from {(link.Anywhere ? "anywhere" : FormatSpot(link.From))} to {FormatSpot(link.To)}");

            // what quest travel would add: the ones whose destination nothing open reaches
            var dead = graph.DeadLinks.ToHashSet();
            var needed = graph.Links.Where(l => l.Kind == TravelKind.Quest && !dead.Contains(l) && graph.GetLabel(l.To) is not (ReachabilityLabel.Walkable or ReachabilityLabel.OneWay))
                .GroupBy(l => l.Source).ToList();
            lines.Add($"Quest travel to places nothing open reaches: {needed.Count} sources (first 80)");
            foreach (var group in needed.Take(80))
                lines.Add($"  {group.Key}: {group.Count()} links, to {FormatSpot(group.First().To)} ({graph.GetLabel(group.First().To)})");

            var unknownInteriors = Enumerable.Range(0, interiors.Length).Where(i => interiors[i] == null && manifest.Entries[i].InteriorMethod != ReachabilityMethod.None).ToList();
            lines.Add($"Interiors without a summary (Unknown): {unknownInteriors.Count}" + (unknownInteriors.Count <= 50 ? " " + string.Join(" ", unknownInteriors.Select(i => $"{i:X4}({manifest.Entries[i].InteriorStatus.ToString().ToLowerInvariant()})")) : ""));

            var withDrops = summaries.Where(s => s != null && s.LedgeDrops.Count > 0).ToList();
            lines.Add($"Ledge drops: {withDrops.Sum(s => s.LedgeDrops.Count)} in {withDrops.Count} landblocks; most " +
                string.Join(" ", withDrops.OrderByDescending(s => s.LedgeDrops.Count).Take(5).Select(s => $"{s.LandblockId >> 16:X4}:{s.LedgeDrops.Count}")));

            lines.Add("Largest pieces that aren't Walkable at any level:");
            foreach (var p in pieces.Select(p => (p.landblock, p.index, p.piece, label: graph.GetLabel(p.spot))).Where(p => p.label != ReachabilityLabel.Walkable).OrderByDescending(p => p.piece.Area).Take(25))
                lines.Add($"  {p.label} {p.landblock:X4} piece {p.index} ({summaries[p.landblock].Method}): {p.piece.Area:F0} m2, z {p.piece.MinZ:F1} to {p.piece.MaxZ:F1}");

            var interiorLabels = interiorPieces.Select(p => (p.landblock, p.group, p.index, p.piece, p.hasDoors, label: graph.GetLabel(p.spot))).ToList();
            lines.Add("Largest interior pieces with doorways that aren't Walkable at any level:");
            foreach (var p in interiorLabels.Where(p => p.hasDoors && p.label != ReachabilityLabel.Walkable).OrderByDescending(p => p.piece.Area).Take(25))
                lines.Add($"  {p.label} {p.landblock:X4} group {p.group} piece {p.index}: {p.piece.Area:F0} m2, z {p.piece.MinZ:F1} to {p.piece.MaxZ:F1}");

            lines.Add("Largest interior pieces without doorways that aren't reached at any level:");
            foreach (var p in interiorLabels.Where(p => !p.hasDoors && p.label is not (ReachabilityLabel.Walkable or ReachabilityLabel.OneWay)).OrderByDescending(p => p.piece.Area).Take(25))
                lines.Add($"  {p.label} {p.landblock:X4} group {p.group} piece {p.index}: {p.piece.Area:F0} m2, z {p.piece.MinZ:F1} to {p.piece.MaxZ:F1}");

            var shown = string.Join("\n", lines.Take(shownLines));
            try
            {
                File.WriteAllLines(ReachabilityReportPath, lines);
                return shown + $"\nFull report: {ReachabilityReportPath}";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return shown + $"\nCould not write {ReachabilityReportPath}: {e.Message}";
            }
        }

        /// <summary>
        /// Starts a verify on threads of below-normal priority, false if a reachability job is running. It writes its
        /// findings to ReachabilityVerifyPath.
        /// </summary>
        /// <param name="samples">How many terrain-pass landblocks get a Recast tile to compare with, half of them next to a Recast landblock</param>
        public static bool StartReachabilityVerify(int samples, int threads) => TryStartReachabilityJob("Verify", threads, run =>
        {
            var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);
            try
            {
                RunReachabilityVerify(run, samples);
            }
            finally
            {
                DropAddedCellDatEntries(cachedBefore);
            }
        });

        private static void RunReachabilityVerify(BakeRun run, int sampleCount)
        {
            var graph = reachabilityGraph;
            if (graph == null)
            {
                log.Warn("Pathfinder: reachability verify needs a graph, run a bake first");
                return;
            }

            var summaries = graph.Summaries;
            var totals = new List<string>();
            var details = new List<string>();

            // Detour's links between two tiles (null: a tile isn't cached) against the summaries' joins
            void Compare(string border, HashSet<(int, int)> detour, HashSet<(int, int)> joined, JoinTally tally)
            {
                var missed = detour?.Where(j => !joined.Contains(j)).ToList();
                var extra = detour == null ? null : joined.Where(j => !detour.Contains(j)).ToList();
                lock (details)
                {
                    if (detour == null)
                    {
                        details.Add($"  {border}: no tiles");
                        return;
                    }

                    tally.Borders++;
                    tally.Agree += joined.Count(detour.Contains);
                    tally.DetourOnly += missed.Count;
                    tally.SummaryOnly += extra.Count;
                    if (missed.Count > 0 || extra.Count > 0)
                        details.Add($"  {border}: Detour only {string.Join(" ", missed)}; summary only {string.Join(" ", extra)}");
                }
            }

            var recast = Enumerable.Range(0, summaries.Length).Where(i => summaries[i]?.Method == ReachabilityMethod.Recast).ToList();
            var terrain = Enumerable.Range(0, summaries.Length).Where(i => summaries[i]?.Method == ReachabilityMethod.Terrain).ToList();
            var random = new Random();
            var seams = terrain.Where(i => GetNeighbors(i).Any(n => summaries[n.landblock]?.Method == ReachabilityMethod.Recast)).ToList();
            var samples = seams.OrderBy(_ => random.Next()).Take(sampleCount / 2).ToList();
            samples.AddRange(terrain.Except(seams).OrderBy(_ => random.Next()).Take(sampleCount - samples.Count));

            run.Total = recast.Count + samples.Count;
            run.TileWatch.Start();

            // Recast borders: the summaries' joins against Detour's links on the two tiles, east and north of each
            run.Phase = "checking Recast borders";
            var recastTally = new JoinTally();
            RunOnJobThreads(run, recast, a =>
            {
                foreach (var (b, side) in GetNeighbors(a).Where(n => n.side < 2 && summaries[n.landblock]?.Method == ReachabilityMethod.Recast))
                    Compare($"{a:X4}-{b:X4}", GetDetourJoins(a, summaries[a].Fingerprint, b, summaries[b].Fingerprint), GetSummaryJoins(summaries[a], summaries[b], side), recastTally);
                Interlocked.Increment(ref run.Built);
            }, null);
            totals.Add($"Recast borders: {recastTally}");

            // terrain pass: the pieces against those of the landblock's Recast tile over the 2 m grid, and its seams
            // with Recast neighbors against Detour's links between the two Recast tiles
            run.Phase = "checking terrain landblocks";
            int compared = 0, unbuilt = 0, joinsApart = 0, splits = 0;
            var seamTally = new JoinTally();
            RunOnJobThreads(run, samples, t =>
            {
                var terrainSummary = summaries[t];
                var recastSummary = BuildTileSummary((uint)t << 16, ReachabilityMethod.Recast);
                Interlocked.Increment(ref run.Built);
                if (recastSummary == null)
                {
                    Interlocked.Increment(ref unbuilt);
                    return;
                }

                var polygons = new LandblockStruct(DatManager.CellDat.ReadFromDat<CellLandblock>((uint)t << 16 | 0xFFFF, false)).Polygons;
                var overlap = new Dictionary<(ushort terrain, ushort recast), int>();
                for (var x = 0; x < TileSummary.MapSide; x++)
                {
                    for (var y = 0; y < TileSummary.MapSide; y++)
                    {
                        var spot = new Vector3((x + 0.5f) * TileSummary.MapCellSize, (y + 0.5f) * TileSummary.MapCellSize, 0f);
                        var key = (GetSummaryPiece(terrainSummary, spot, polygons), GetSummaryPiece(recastSummary, spot));
                        if (key.Item1 != TileSummary.NoPiece && key.Item2 != TileSummary.NoPiece)
                            overlap[key] = overlap.GetValueOrDefault(key) + 1;
                    }
                }

                var shared = overlap.Where(o => o.Value >= VerifyMinCells).Select(o => o.Key).ToList();
                var apart = shared.GroupBy(k => k.terrain).Count(g => g.Count() > 1); // the graph may join what can't be walked
                var split = shared.GroupBy(k => k.recast).Count(g => g.Count() > 1); // the graph errs on the safe side

                // a Recast piece stands for the terrain piece it overlaps most; raised pieces aren't on the ground map
                var toTerrain = overlap.GroupBy(o => o.Key.recast).ToDictionary(g => (int)g.Key, g => (int)g.OrderByDescending(o => o.Value).First().Key.terrain);

                lock (details)
                {
                    compared++;
                    joinsApart += apart;
                    splits += split;
                    if (apart > 0 || split > 0)
                        details.Add($"  {t:X4}: terrain joins Recast pieces apart {apart}, splits {split}; terrain pieces {terrainSummary.Pieces.Count}, Recast {recastSummary.Pieces.Count}");
                }

                foreach (var (n, side) in GetNeighbors(t).Where(n => summaries[n.landblock]?.Method == ReachabilityMethod.Recast))
                {
                    var detour = GetDetourJoins(t, recastSummary.Fingerprint, n, summaries[n].Fingerprint);
                    var expected = detour?.Where(j => toTerrain.ContainsKey(j.Item1)).Select(j => (toTerrain[j.Item1], j.Item2)).ToHashSet();
                    Compare($"seam {t:X4}-{n:X4}", expected, GetSummaryJoins(terrainSummary, summaries[n], side), seamTally);
                }
            }, null);
            totals.Add($"Terrain landblocks: {compared} compared ({unbuilt} without a Recast tile), terrain joins Recast pieces apart {joinsApart}, splits {splits}");
            totals.Add($"Terrain/Recast seams: {seamTally}");

            var header = $"Verify {(run.Cancel.IsCancellationRequested ? "stopped" : "done")} in {FormatDuration(run.Watch.Elapsed)}; samples {string.Join(" ", samples.Select(s => s.ToString("X4")))}";
            try
            {
                File.WriteAllLines(ReachabilityVerifyPath, new[] { header }.Concat(totals).Concat(details));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                log.Warn($"Pathfinder: could not write {ReachabilityVerifyPath}: {e.Message}");
            }
            log.Info($"Pathfinder: reachability verify {(run.Cancel.IsCancellationRequested ? "stopped" : "done")}: {string.Join("; ", totals)}");
        }

        private class JoinTally
        {
            public int Borders, Agree, DetourOnly, SummaryOnly;

            public override string ToString() => $"{Borders} borders, joins agree {Agree}, Detour only {DetourOnly}, summary only {SummaryOnly}";
        }

        /// <summary>
        /// The landblock indices around one, with the summary side facing each: 0 east, 1 north, 2 west, 3 south.
        /// </summary>
        private static IEnumerable<(int landblock, int side)> GetNeighbors(int landblock)
        {
            var (x, y) = (landblock >> 8, landblock & 0xFF);
            if (x < 0xFF)
                yield return (landblock + 0x100, 0);
            if (y < 0xFF)
                yield return (landblock + 1, 1);
            if (x > 0)
                yield return (landblock - 0x100, 2);
            if (y > 0)
                yield return (landblock - 1, 3);
        }

        /// <summary>
        /// The piece pairs the graph joins across the border on the given side of the first landblock. Kept apart
        /// from WorldGraph's own border join on purpose, as the verify checks it.
        /// </summary>
        private static HashSet<(int, int)> GetSummaryJoins(TileSummary a, TileSummary b, int side)
        {
            var joins = new HashSet<(int, int)>();
            var spansB = b.Spans.Where(s => s.Side == (side + 2) % 4).ToList();
            foreach (var spanA in a.Spans.Where(s => s.Side == side))
            {
                foreach (var spanB in spansB)
                {
                    if (WorldGraph.OverlapSlabs(spanA, spanB, SmallProfile.MaxClimb))
                        joins.Add((spanA.Piece, spanB.Piece));
                }
            }
            return joins;
        }

        /// <summary>
        /// The piece pairs Detour links between two neighboring landblocks' Small tiles, with pieces numbered as the
        /// Recast summaries number them (components of each tile alone). Each tile is the cached one of the given
        /// fingerprint, the one its summary was made from; null if one isn't cached.
        /// </summary>
        private static HashSet<(int, int)> GetDetourJoins(int a, ulong fingerprintA, int b, ulong fingerprintB)
        {
            DtMeshData Load(int i)
            {
                var path = GetMeshPath(OutdoorMeshDirectory, GetMeshId((uint)i << 16, OutdoorGroup, AgentSize.Small), true, AgentSize.Small, i == a ? fingerprintA : fingerprintB);
                return TryReadMeshData(path, out var data) ? data : null;
            }

            int[] Components(DtMeshData data)
            {
                var single = data == null ? null : CreateTileMesh(data);
                return single == null ? null : FindComponents(single, single.GetTile(0)).component;
            }

            // fresh data for the two-tile mesh: AddTile writes the links into the polys
            var componentA = Components(Load(a));
            var componentB = Components(Load(b));
            var dataA = Load(a);
            var dataB = Load(b);
            if (componentA == null || componentB == null || dataA == null || dataB == null)
                return null;

            var mesh = CreateTileMesh(dataA, dataB);
            if (mesh == null)
                return null;

            var tileA = mesh.GetTile(0);
            var tileB = mesh.GetTile(1);
            var joins = new HashSet<(int, int)>();
            for (var k = 0; k < tileA.data.header.polyCount; k++)
            {
                for (var l = tileA.data.polys[k].firstLink; l != DtDetour.DT_NULL_LINK; l = tileA.links[l].next)
                {
                    mesh.GetTileAndPolyByRefUnsafe(tileA.links[l].refs, out var tile, out var poly);
                    if (tile == tileB && componentA[k] >= 0 && componentB[poly.index] >= 0)
                        joins.Add((componentA[k], componentB[poly.index]));
                }
            }
            return joins;
        }
    }
}
