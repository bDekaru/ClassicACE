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

        private static string InteriorSummariesPath => Path.Combine(ReachabilityDirectory, "interiors.bin");

        /// <summary>
        /// A background reachability job (a bake or a verify); one runs at a time.
        /// </summary>
        private class BakeRun
        {
            public readonly string Name;
            public readonly int Threads;
            public readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            public readonly Stopwatch Watch = Stopwatch.StartNew();
            public readonly Stopwatch TileWatch = new Stopwatch();
            public volatile string Phase = "starting";

            /// <summary>
            /// The work items run on the job's threads (a bake's Recast tiles and interiors: terrain summaries take seconds for the
            /// whole world).
            /// </summary>
            public int Total, Built, Failed;

            public BakeRun(string name, int threads)
            {
                Name = name;
                Threads = threads;
            }
        }

        private static readonly object BakeSync = new object();

        private static BakeRun bakeRun;

        private static volatile ReachabilityManifest reachabilityManifest;

        private static DateTime reachabilityGraphTime;

        /// <summary>
        /// Starts a bake on threads of below-normal priority, false if a reachability job is running. Landblocks with an
        /// up to date summary are skipped, so with nothing changed it takes seconds and only puts the graph in use.
        /// </summary>
        public static bool StartReachabilityBake(int threads) => TryStartReachabilityJob("Bake", threads, RunReachabilityBake);

        /// <summary>
        /// Runs a job on a thread of below-normal priority, unless one is running. An exception ends the job, logged.
        /// </summary>
        private static bool TryStartReachabilityJob(string name, int threads, Action<BakeRun> job)
        {
            BakeRun run;
            lock (BakeSync)
            {
                if (bakeRun != null)
                    return false;

                run = bakeRun = new BakeRun(name, Math.Max(1, threads));
            }

            new Thread(() =>
            {
                try
                {
                    job(run);
                }
                catch (Exception e)
                {
                    log.Error($"Pathfinder: reachability {name.ToLowerInvariant()} failed: {e}");
                }
                finally
                {
                    lock (BakeSync)
                        bakeRun = null;
                }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = $"Reachability {name}" }.Start();
            return true;
        }

        /// <summary>
        /// Runs the work for every item on the job's threads until all are done or the job is stopped, and the
        /// checkpoint every BakeCheckpointInterval meanwhile. An item whose work throws is logged and skipped.
        /// </summary>
        private static void RunOnJobThreads(BakeRun run, List<int> items, Action<int> work, Action checkpoint)
        {
            var queue = new ConcurrentQueue<int>(items);
            using (var workersDone = new CountdownEvent(run.Threads))
            {
                var started = 0;
                try
                {
                    for (var t = 0; t < run.Threads; t++, started++)
                    {
                        new Thread(() =>
                        {
                            try
                            {
                                while (!run.Cancel.IsCancellationRequested && queue.TryDequeue(out var i))
                                {
                                    try
                                    {
                                        work(i);
                                    }
                                    catch (Exception e)
                                    {
                                        Interlocked.Increment(ref run.Failed);
                                        log.Warn($"Pathfinder: reachability {run.Name.ToLowerInvariant()} failed on landblock {i:X4}: {e}");
                                    }
                                }
                            }
                            finally
                            {
                                workersDone.Signal();
                            }
                        }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = $"Reachability {run.Name} work" }.Start();
                    }

                    while (!workersDone.Wait(BakeCheckpointInterval))
                        checkpoint?.Invoke();
                }
                catch
                {
                    // the workers signal the event as they end, so it must outlive them; unstarted ones never will
                    run.Cancel.Cancel();
                    if (started < run.Threads)
                        workersDone.Signal(run.Threads - started);
                    workersDone.Wait();
                    throw;
                }
            }
        }

        /// <summary>
        /// Stops a running bake or verify after the items in progress; what a bake built is saved, so the next bake
        /// resumes there. False if none is running.
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
            run.Phase = "checking the dats";
            var manifest = UpdateReachabilityManifest(run.Cancel.Token);
            if (manifest == null)
            {
                log.Info("Pathfinder: reachability bake stopped while checking the dats");
                return;
            }
            reachabilityManifest = manifest;

            run.Phase = "loading summaries";
            var summaries = TileSummary.LoadAll(ReachabilitySummariesPath) ?? new TileSummary[ReachabilityManifest.LandblockCount];
            var interiors = InteriorSummary.LoadAll(InteriorSummariesPath) ?? new InteriorSummary[ReachabilityManifest.LandblockCount];
            var pending = new List<int>();
            var pendingInteriors = new List<int>();
            for (var i = 0; i < summaries.Length; i++)
            {
                ref var entry = ref manifest.Entries[i];

                // a summary of other input, or of a pending entry (a new world key leaves the fingerprints alike), is
                // dropped, so the landblock is Unknown until it is built again
                if (summaries[i] != null && (summaries[i].Method != entry.Method || summaries[i].Fingerprint != entry.Fingerprint || entry.Status == ReachabilityStatus.Pending))
                    summaries[i] = null;

                if (summaries[i] != null)
                    entry.Status = ReachabilityStatus.Done;
                else if ((entry.Method == ReachabilityMethod.Terrain || entry.Method == ReachabilityMethod.Recast) && entry.Status != ReachabilityStatus.Failed)
                    pending.Add(i);

                if (interiors[i] != null && (entry.InteriorMethod != ReachabilityMethod.Recast || interiors[i].Fingerprint != entry.InteriorFingerprint || entry.InteriorStatus == ReachabilityStatus.Pending))
                    interiors[i] = null;

                if (interiors[i] != null)
                    entry.InteriorStatus = ReachabilityStatus.Done;
                else if (entry.InteriorMethod == ReachabilityMethod.Recast && entry.InteriorStatus != ReachabilityStatus.Failed)
                    pendingInteriors.Add(i);
            }

            var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);

            run.Phase = "summarizing terrain";
            foreach (var i in pending.Where(i => manifest.Entries[i].Method == ReachabilityMethod.Terrain))
            {
                if (run.Cancel.IsCancellationRequested)
                    break;

                BakeTile(manifest, summaries, i);
            }
            PublishReachability(manifest, summaries, interiors, pending.Count > 0);

            var seeds = PlayerFactory.StarterTownLocations.Values.SelectMany(spots => spots).Select(spot => GetLandblockCoords(spot.Cell)).ToList();
            var recast = pending.Where(i => manifest.Entries[i].Method == ReachabilityMethod.Recast)
                .OrderBy(i => seeds.Min(s => Math.Max(Math.Abs((i >> 8) - s.x), Math.Abs((i & 0xFF) - s.y))))
                .ToList();

            // the outdoor tiles first: their pieces are what the interiors join. The counters are per phase, as a tile
            // and an interior take very different times.
            int built = 0, failed = 0;
            foreach (var (phase, items, bake) in new (string, List<int>, Func<int, bool>)[]
            {
                ("building tiles", recast, i => BakeTile(manifest, summaries, i)),
                ("building interiors", pendingInteriors, i => BakeInterior(manifest, interiors, i))
            })
            {
                if (items.Count == 0 || run.Cancel.IsCancellationRequested)
                    continue;

                run.Phase = phase;
                run.Total = items.Count;
                run.Built = run.Failed = 0;
                run.TileWatch.Restart();

                RunOnJobThreads(run, items, i =>
                {
                    if (bake(i))
                        Interlocked.Increment(ref run.Built);
                    else
                        Interlocked.Increment(ref run.Failed);
                }, () => PublishReachability(manifest, summaries, interiors, true));

                PublishReachability(manifest, summaries, interiors, true);
                built += run.Built;
                failed += run.Failed;
            }

            // only at the end: a landblock still loading isn't counted as loaded, and would lose its fresh records
            DropAddedCellDatEntries(cachedBefore);

            log.Info($"Pathfinder: reachability bake {(run.Cancel.IsCancellationRequested ? "stopped" : "done")} in {FormatDuration(run.Watch.Elapsed)}, {built} tiles and interiors built, {failed} failed, {recast.Count + pendingInteriors.Count - built - failed} left");
        }

        /// <summary>
        /// Builds a landblock's interior summary and records the outcome in the manifest, as BakeTile does.
        /// </summary>
        private static bool BakeInterior(ReachabilityManifest manifest, InteriorSummary[] interiors, int index)
        {
            var landblockId = (uint)index << 16;
            try
            {
                interiors[index] = BuildInteriorSummary(landblockId);
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: could not summarize the interiors of 0x{landblockId:X8} for reachability: {e}");
            }

            manifest.Entries[index].InteriorStatus = interiors[index] != null ? ReachabilityStatus.Done : ReachabilityStatus.Failed;
            return interiors[index] != null;
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
        private static void PublishReachability(ReachabilityManifest manifest, TileSummary[] summaries, InteriorSummary[] interiors, bool save)
        {
            // the workers keep filling the arrays: save and build from one snapshot
            summaries = (TileSummary[])summaries.Clone();
            interiors = (InteriorSummary[])interiors.Clone();

            if (save)
            {
                TileSummary.SaveAll(ReachabilitySummariesPath, summaries);
                InteriorSummary.SaveAll(InteriorSummariesPath, interiors);
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
                lines.Add("No bake or verify running.");
            else
            {
                var line = $"{run.Name} running on {run.Threads} threads for {FormatDuration(run.Watch.Elapsed)}: {run.Phase}";
                var done = run.Built + run.Failed;
                if (run.Total > 0)
                {
                    line += $", {done}/{run.Total} done ({run.Failed} failed)";
                    if (done > 0)
                        line += $", about {FormatDuration(TimeSpan.FromTicks(run.TileWatch.Elapsed.Ticks / done * (run.Total - done)))} left";
                }
                lines.Add(line);
            }

            var manifest = reachabilityManifest;
            if (manifest != null)
            {
                string CountStatuses(IEnumerable<ReachabilityStatus> statuses) => string.Join(", ", statuses.GroupBy(s => s).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}"));

                foreach (var method in new[] { ReachabilityMethod.Terrain, ReachabilityMethod.Recast })
                {
                    var entries = manifest.Entries.Where(e => e.Method == method).ToList();
                    lines.Add($"{method}: {entries.Count} landblocks, {CountStatuses(entries.Select(e => e.Status))}");
                }

                var interiors = manifest.Entries.Where(e => e.InteriorMethod == ReachabilityMethod.Recast).ToList();
                lines.Add($"Interiors: {interiors.Count} landblocks, {CountStatuses(interiors.Select(e => e.InteriorStatus))}");
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
