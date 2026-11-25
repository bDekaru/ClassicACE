using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Pathfinding.Reachability
{
    public enum ReachabilityLabel : byte
    {
        /// <summary>
        /// Depends on landblocks not summarized yet.
        /// </summary>
        Unknown,

        /// <summary>
        /// Can't be walked to from a start spot, nor back to one; also a spot with no walkable ground.
        /// </summary>
        Unreachable,

        /// <summary>
        /// Can be walked to (a ledge drop on the way), but not back to a start spot.
        /// </summary>
        OneWay,

        /// <summary>
        /// Can be walked back to a start spot from, but not to.
        /// </summary>
        EscapeOnly,

        /// <summary>
        /// Can be walked to from a start spot and back to one.
        /// </summary>
        Walkable
    }

    /// <summary>
    /// Walking connectivity of the world's outdoor surface, from the tile summaries alone: pieces joined across
    /// landblock borders as Detour links tiles, ledge drops one way, and labels from the start spots. A landblock with
    /// no summary yet is solved both as a wall and as joining all its borders; a piece whose labels differ between the
    /// two is Unknown.
    /// </summary>
    public class WorldGraph
    {
        public TileSummary[] Summaries { get; }

        /// <summary>
        /// Index of each landblock's first piece; a landblock waiting for its summary has one node standing for it.
        /// </summary>
        private readonly int[] nodeBase;

        private readonly bool[] unknown;

        private readonly ReachabilityLabel[] labels;

        public int NodeCount => labels.Length;

        /// <summary>
        /// Walking groups, missing landblocks taken as walls (each still counts as a group of its own).
        /// </summary>
        public int GroupCount { get; private set; }

        public int SeedGroupCount { get; private set; }

        private WorldGraph(TileSummary[] summaries, bool[] unknown)
        {
            // copies: the node numbering must not change under the caller filling in summaries later
            Summaries = (TileSummary[])summaries.Clone();
            this.unknown = (bool[])unknown.Clone();

            nodeBase = new int[summaries.Length + 1];
            for (var i = 0; i < summaries.Length; i++)
                nodeBase[i + 1] = nodeBase[i] + (summaries[i]?.Pieces.Count ?? (unknown[i] ? 1 : 0));

            labels = new ReachabilityLabel[nodeBase[summaries.Length]];
        }

        public ReachabilityLabel GetLabel(int landblock, ushort piece)
        {
            if (Summaries[landblock] == null)
                return unknown[landblock] ? ReachabilityLabel.Unknown : ReachabilityLabel.Unreachable;

            return piece == TileSummary.NoPiece ? ReachabilityLabel.Unreachable : labels[nodeBase[landblock] + piece];
        }

        /// <summary>
        /// Summaries by landblock index (id >> 16). Unknown: the landblocks that have walkable ground but no summary yet
        /// (the rest without one are walls). Seeds: the start spots' pieces, NoPiece where the landblock has no summary.
        /// Climb: the mesh's walkableClimb, which sets the height tolerance at a border.
        /// </summary>
        public static WorldGraph Build(TileSummary[] summaries, bool[] unknown, IEnumerable<(int landblock, ushort piece)> seeds, float climb)
        {
            var graph = new WorldGraph(summaries, unknown);
            var nodeBase = graph.nodeBase;
            summaries = graph.Summaries;
            unknown = graph.unknown;

            var known = new DisjointSet(graph.NodeCount);
            var optimisticJoins = new List<(int, int)>();

            void JoinBorder(int a, int b, int sideA)
            {
                var sideB = (sideA + 2) % 4;
                TileSummary summaryA = summaries[a], summaryB = summaries[b];

                if (summaryA != null && summaryB != null)
                {
                    var spansB = summaryB.Spans.Where(s => s.Side == sideB).ToList();
                    foreach (var spanA in summaryA.Spans.Where(s => s.Side == sideA))
                        foreach (var spanB in spansB)
                            if (OverlapSlabs(spanA, spanB, climb))
                                known.Union(nodeBase[a] + spanA.Piece, nodeBase[b] + spanB.Piece);
                }
                else if (summaryA != null && unknown[b])
                    optimisticJoins.AddRange(summaryA.Spans.Where(s => s.Side == sideA).Select(s => (nodeBase[a] + s.Piece, nodeBase[b])));
                else if (summaryB != null && unknown[a])
                    optimisticJoins.AddRange(summaryB.Spans.Where(s => s.Side == sideB).Select(s => (nodeBase[b] + s.Piece, nodeBase[a])));
                else if (unknown[a] && unknown[b])
                    optimisticJoins.Add((nodeBase[a], nodeBase[b]));
            }

            // index = x << 8 | y: the east neighbor is +0x100, the north one +1
            for (var i = 0; i < summaries.Length; i++)
            {
                if (i >> 8 < 0xFF)
                    JoinBorder(i, i + 0x100, 0);
                if ((i & 0xFF) < 0xFF)
                    JoinBorder(i, i + 1, 1);
            }

            var drops = new List<(int from, int to)>();
            for (var i = 0; i < summaries.Length; i++)
                if (summaries[i] != null)
                    drops.AddRange(summaries[i].LedgeDrops.Select(d => (nodeBase[i] + d.from, nodeBase[i] + d.to)));

            var seedList = seeds.ToList();
            var knownSeeds = seedList.Where(s => summaries[s.landblock] != null && s.piece != TileSummary.NoPiece).Select(s => nodeBase[s.landblock] + s.piece).ToList();
            var optimisticSeeds = knownSeeds.Concat(seedList.Where(s => summaries[s.landblock] == null && unknown[s.landblock]).Select(s => nodeBase[s.landblock])).ToList();

            var optimistic = known.Clone();
            foreach (var (a, b) in optimisticJoins)
                optimistic.Union(a, b);

            var (reach, escape, groupCount, seedGroupCount) = Solve(known, drops, knownSeeds);
            var (maybeReach, maybeEscape, _, _) = Solve(optimistic, drops, optimisticSeeds);
            graph.GroupCount = groupCount;
            graph.SeedGroupCount = seedGroupCount;

            for (var n = 0; n < graph.NodeCount; n++)
            {
                graph.labels[n] = reach[n] && escape[n] ? ReachabilityLabel.Walkable
                    : reach[n] != maybeReach[n] || escape[n] != maybeEscape[n] ? ReachabilityLabel.Unknown
                    : reach[n] ? ReachabilityLabel.OneWay
                    : escape[n] ? ReachabilityLabel.EscapeOnly : ReachabilityLabel.Unreachable;
            }

            return graph;
        }

        /// <summary>
        /// Per node, whether its group can be reached from a seed group and can reach one, over the ledge drops between
        /// groups.
        /// </summary>
        private static (bool[] reach, bool[] escape, int groupCount, int seedGroupCount) Solve(DisjointSet set, List<(int from, int to)> drops, List<int> seeds)
        {
            var group = new int[set.Count];
            var groupOfRoot = new int[set.Count];
            Array.Fill(groupOfRoot, -1);
            var groupCount = 0;
            for (var n = 0; n < set.Count; n++)
            {
                var root = set.Find(n);
                if (groupOfRoot[root] < 0)
                    groupOfRoot[root] = groupCount++;
                group[n] = groupOfRoot[root];
            }

            var forward = new List<int>[groupCount];
            var backward = new List<int>[groupCount];
            foreach (var (from, to) in drops)
            {
                int a = group[from], b = group[to];
                if (a == b)
                    continue;

                (forward[a] ??= new List<int>()).Add(b);
                (backward[b] ??= new List<int>()).Add(a);
            }

            var seedGroups = seeds.Select(s => group[s]).Distinct().ToList();
            var reachGroup = Flood(seedGroups, forward);
            var escapeGroup = Flood(seedGroups, backward);

            return (group.Select(g => reachGroup[g]).ToArray(), group.Select(g => escapeGroup[g]).ToArray(), groupCount, seedGroups.Count);
        }

        private static bool[] Flood(List<int> start, List<int>[] edges)
        {
            var visited = new bool[edges.Length];
            var queue = new Queue<int>();
            foreach (var g in start)
            {
                visited[g] = true;
                queue.Enqueue(g);
            }

            while (queue.Count > 0)
            {
                var g = queue.Dequeue();
                if (edges[g] == null)
                    continue;

                foreach (var next in edges[g])
                    if (!visited[next])
                    {
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
            }
            return visited;
        }

        /// <summary>
        /// Detour's dtNavMesh overlapSlabs on two facing border spans (DotRecast DtNavMesh.OverlapSlabs): they overlap
        /// along the edge by more than the 0.01 shrink at each end, and at one end of the overlap their heights are
        /// within twice the climb, or they cross.
        /// </summary>
        internal static bool OverlapSlabs(SummarySpan a, SummarySpan b, float climb)
        {
            const float px = 0.01f;
            var min = Math.Max(a.Start + px, b.Start + px);
            var max = Math.Min(a.End - px, b.End - px);
            if (min > max)
                return false;

            float HeightAt(SummarySpan s, float along) => s.StartZ + (s.EndZ - s.StartZ) / (s.End - s.Start) * (along - s.Start);

            var dmin = HeightAt(b, min) - HeightAt(a, min);
            var dmax = HeightAt(b, max) - HeightAt(a, max);
            if (dmin * dmax < 0)
                return true;

            var threshold = climb * 2 * (climb * 2);
            return dmin * dmin <= threshold || dmax * dmax <= threshold;
        }

        private class DisjointSet
        {
            private readonly int[] parent;

            public int Count => parent.Length;

            public DisjointSet(int count)
            {
                parent = Enumerable.Range(0, count).ToArray();
            }

            private DisjointSet(int[] parent)
            {
                this.parent = parent;
            }

            public DisjointSet Clone() => new DisjointSet((int[])parent.Clone());

            public int Find(int i)
            {
                while (parent[i] != i)
                    i = parent[i] = parent[parent[i]];
                return i;
            }

            public void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b)
                    parent[a] = b;
            }
        }
    }
}
