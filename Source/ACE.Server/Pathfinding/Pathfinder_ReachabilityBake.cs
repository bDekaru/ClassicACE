using ACE.DatLoader;
using ACE.Server.Factories;
using ACE.Server.Pathfinding.Reachability;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The world reachability bake in the background: the summaries still missing are built, nearest the start spots
    /// first, and the world graph is put in use as they come.
    /// </summary>
    public static partial class Pathfinder
    {
        public const int StartupBakeThreads = 1;

        /// <summary>
        /// How often a running bake saves its progress and puts a fresh graph in use.
        /// </summary>
        private static readonly TimeSpan BakeCheckpointInterval = TimeSpan.FromMinutes(5);

        private static string ReachabilitySummariesPath => Path.Combine(ReachabilityDirectory, "summaries.bin");

        private class BakeRun
        {
            public readonly int Threads;
            public readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            public readonly Stopwatch Watch = Stopwatch.StartNew();
            public readonly Stopwatch TileWatch = new Stopwatch();
            public volatile string Phase = "checking the dats";

            /// <summary>
            /// Recast tiles only: terrain summaries take seconds for the whole world.
            /// </summary>
            public int Total, Built, Failed;

            public BakeRun(int threads)
            {
                Threads = threads;
            }
        }

        private static readonly object BakeSync = new object();

        private static BakeRun bakeRun;

        private static volatile ReachabilityManifest reachabilityManifest;

        private static DateTime reachabilityGraphTime;

        /// <summary>
        /// Starts a bake on threads of below-normal priority, false if one is running. Landblocks with an up to date
        /// summary are skipped, so with nothing changed it takes seconds and only puts the graph in use.
        /// </summary>
        public static bool StartReachabilityBake(int threads)
        {
            BakeRun run;
            lock (BakeSync)
            {
                if (bakeRun != null)
                    return false;

                run = bakeRun = new BakeRun(Math.Max(1, threads));
            }

            new Thread(() => RunReachabilityBake(run)) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Reachability bake" }.Start();
            return true;
        }

        /// <summary>
        /// Stops a running bake after the tiles in progress; what it built is saved, so the next bake resumes there.
        /// False if none is running.
        /// </summary>
        public static bool StopReachabilityBake()
        {
            lock (BakeSync)
            {
                bakeRun?.Cancel.Cancel();
                return bakeRun != null;
            }
        }

        private static void RunReachabilityBake(BakeRun run)
        {
            try
            {
                var manifest = UpdateReachabilityManifest();
                reachabilityManifest = manifest;

                run.Phase = "loading summaries";
                var summaries = TileSummary.LoadAll(ReachabilitySummariesPath) ?? new TileSummary[ReachabilityManifest.LandblockCount];
                var pending = new List<int>();
                for (var i = 0; i < summaries.Length; i++)
                {
                    var entry = manifest.Entries[i];

                    // a summary of other input, or of a pending entry (a new world key leaves the fingerprints alike), is
                    // dropped, so the landblock is Unknown until it is built again
                    if (summaries[i] != null && (summaries[i].Method != entry.Method || summaries[i].Fingerprint != entry.Fingerprint || entry.Status == ReachabilityStatus.Pending))
                        summaries[i] = null;

                    if (summaries[i] != null)
                        manifest.Entries[i].Status = ReachabilityStatus.Done;
                    else if ((entry.Method == ReachabilityMethod.Terrain || entry.Method == ReachabilityMethod.Recast) && entry.Status != ReachabilityStatus.Failed)
                        pending.Add(i);
                }

                var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);

                run.Phase = "summarizing terrain";
                foreach (var i in pending.Where(i => manifest.Entries[i].Method == ReachabilityMethod.Terrain))
                {
                    if (run.Cancel.IsCancellationRequested)
                        break;

                    BakeTile(manifest, summaries, i);
                }
                PublishReachability(manifest, summaries, pending.Count > 0);

                var seeds = PlayerFactory.StarterTownLocations.Values.SelectMany(spots => spots).Select(spot => GetLandblockCoords(spot.Cell)).ToList();
                var recast = pending.Where(i => manifest.Entries[i].Method == ReachabilityMethod.Recast)
                    .OrderBy(i => seeds.Min(s => Math.Max(Math.Abs((i >> 8) - s.x), Math.Abs((i & 0xFF) - s.y))))
                    .ToList();

                if (recast.Count > 0 && !run.Cancel.IsCancellationRequested)
                {
                    run.Phase = "building tiles";
                    run.Total = recast.Count;
                    run.TileWatch.Start();

                    var queue = new ConcurrentQueue<int>(recast);
                    using (var workersDone = new CountdownEvent(run.Threads))
                    {
                        for (var t = 0; t < run.Threads; t++)
                        {
                            new Thread(() =>
                            {
                                try
                                {
                                    while (!run.Cancel.IsCancellationRequested && queue.TryDequeue(out var i))
                                    {
                                        if (BakeTile(manifest, summaries, i))
                                            Interlocked.Increment(ref run.Built);
                                        else
                                            Interlocked.Increment(ref run.Failed);
                                    }
                                }
                                finally
                                {
                                    workersDone.Signal();
                                }
                            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Reachability bake tiles" }.Start();
                        }

                        try
                        {
                            while (!workersDone.Wait(BakeCheckpointInterval))
                                PublishReachability(manifest, summaries, true);
                        }
                        catch
                        {
                            // the workers signal the event as they end, so it must outlive them
                            run.Cancel.Cancel();
                            workersDone.Wait();
                            throw;
                        }
                    }

                    PublishReachability(manifest, summaries, true);
                }

                // only at the end: a landblock still loading isn't counted as loaded, and would lose its fresh records
                DropAddedCellDatEntries(cachedBefore);

                log.Info($"Pathfinder: reachability bake {(run.Cancel.IsCancellationRequested ? "stopped" : "done")} in {FormatDuration(run.Watch.Elapsed)}, {run.Built} tiles built, {run.Failed} failed, {recast.Count - run.Built - run.Failed} left");
            }
            catch (Exception e)
            {
                log.Error($"Pathfinder: reachability bake failed: {e}");
            }
            finally
            {
                lock (BakeSync)
                    bakeRun = null;
            }
        }

        /// <summary>
        /// Builds a landblock's summary and records the outcome in the manifest. A failed landblock stays Unknown and
        /// isn't tried again until its input changes.
        /// </summary>
        private static bool BakeTile(ReachabilityManifest manifest, TileSummary[] summaries, int index)
        {
            var landblockId = (uint)index << 16;
            try
            {
                summaries[index] = BuildTileSummary(landblockId, manifest.Entries[index].Method);
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: could not summarize 0x{landblockId:X8} for reachability: {e}");
            }

            manifest.Entries[index].Status = summaries[index] != null ? ReachabilityStatus.Done : ReachabilityStatus.Failed;
            return summaries[index] != null;
        }

        /// <summary>
        /// Saves the bake's progress if asked, and puts a graph of the summaries so far in use.
        /// </summary>
        private static void PublishReachability(ReachabilityManifest manifest, TileSummary[] summaries, bool save)
        {
            // the workers keep filling the array: save and build from one snapshot
            summaries = (TileSummary[])summaries.Clone();

            if (save)
            {
                TileSummary.SaveAll(ReachabilitySummariesPath, summaries);
                manifest.Save(ReachabilityManifestPath);
            }

            BuildReachabilityGraph(summaries, manifest);
            reachabilityGraphTime = DateTime.UtcNow;
        }

        private static string FormatDuration(TimeSpan time) => $"{(int)time.TotalHours}:{time:mm\\:ss}";

        /// <summary>
        /// The running bake's progress, the manifest's counts and the graph in use, for /reachability status.
        /// </summary>
        public static string GetReachabilityStatus()
        {
            var lines = new List<string>();

            var run = bakeRun;
            if (run == null)
                lines.Add("No bake running.");
            else
            {
                var line = $"Bake running on {run.Threads} threads for {FormatDuration(run.Watch.Elapsed)}: {run.Phase}";
                var done = run.Built + run.Failed;
                if (run.Total > 0)
                {
                    line += $", tiles {done}/{run.Total} ({run.Failed} failed)";
                    if (done > 0)
                        line += $", about {FormatDuration(TimeSpan.FromTicks(run.TileWatch.Elapsed.Ticks / done * (run.Total - done)))} left";
                }
                lines.Add(line);
            }

            var manifest = reachabilityManifest;
            if (manifest != null)
            {
                foreach (var method in new[] { ReachabilityMethod.Terrain, ReachabilityMethod.Recast })
                {
                    var entries = manifest.Entries.Where(e => e.Method == method).ToList();
                    lines.Add($"{method}: {entries.Count} landblocks, " + string.Join(", ", entries.GroupBy(e => e.Status).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}")));
                }
            }

            var graph = reachabilityGraph;
            if (graph == null)
                lines.Add("No graph in use: every spot is Unknown.");
            else
                lines.Add($"Graph from {reachabilityGraphTime:yyyy-MM-dd HH:mm:ss} UTC: {graph.Summaries.Count(s => s != null)} landblocks summarized, {graph.GroupCount} groups, start spots in {graph.SeedGroupCount}");

            return string.Join("\n", lines);
        }

        /// <summary>
        /// What the graph says about a spot and the piece of ground under it, for /reachability at.
        /// </summary>
        public static string DescribeReachability(ACE.Entity.Position position)
        {
            var landblock = (int)(position.Cell >> 16);
            var text = $"{GetReachability(position)}: landblock {landblock:X4}";

            var manifest = reachabilityManifest;
            if (manifest != null)
                text += $" ({manifest.Entries[landblock].Method}, {manifest.Entries[landblock].Status.ToString().ToLowerInvariant()})";

            var summary = reachabilityGraph?.Summaries[landblock];
            if (position.Indoors)
                return text + ", indoors: not in the graph yet";
            if (summary == null)
                return text + ", no summary";

            var piece = GetSummaryPiece(summary, position.Pos);
            if (piece == TileSummary.NoPiece)
                return text + ", on no walkable piece";

            var p = summary.Pieces[piece];
            var sides = string.Concat(summary.Spans.Where(s => s.Piece == piece).Select(s => "ENWS"[s.Side]).Distinct());
            return text + $", piece {piece} of {summary.Pieces.Count}: {p.Area:F0} m2, z {p.MinZ:F1} to {p.MaxZ:F1}, borders {(sides.Length > 0 ? sides : "none")}, " +
                $"ledge drops out {summary.LedgeDrops.Count(d => d.from == piece)}, in {summary.LedgeDrops.Count(d => d.to == piece)}";
        }
    }
}
