using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Pathfinding.Reachability
{
    /// <summary>
    /// Where a player can get from the reachability center, on foot or through open travel links.
    /// </summary>
    public enum ReachabilityLabel : byte
    {
        /// <summary>
        /// Depends on landblocks not summarized yet.
        /// </summary>
        Unknown,

        /// <summary>
        /// Can't be walked to from the reachability center, nor back to it; also a spot with no walkable ground.
        /// </summary>
        Unreachable,

        /// <summary>
        /// Can be walked to (a ledge drop on the way), but not back to the reachability center.
        /// </summary>
        OneWay,

        /// <summary>
        /// Can be walked back to the reachability center from, but not to.
        /// </summary>
        EscapeOnly,

        /// <summary>
        /// Can be walked to from the reachability center and back.
        /// </summary>
        Walkable
    }

    /// <summary>
    /// The travel a label counts.
    /// </summary>
    public enum TravelProfile : byte
    {
        /// <summary>
        /// On foot, ledge drops included.
        /// </summary>
        Walk,

        /// <summary>
        /// And the open travel links (portals, NPC travel).
        /// </summary>
        Portals,

        /// <summary>
        /// And, from anywhere outside a dungeon (where CustomDM refuses them), /ls back to the start town and the summon
        /// gems vendors sell.
        /// </summary>
        Recall
    }

    /// <summary>
    /// Connectivity of the world, from the summaries and the travel links: outdoor pieces joined across landblock
    /// borders as Detour links tiles, interior pieces joined to the outdoor piece in front of their doorways, ledge drops
    /// and open travel links one way, and labels from the seeds. A landblock or interior with no summary yet is solved
    /// both as a wall and as joining everything around it (all of an interior's doorways through one node, so pieces in
    /// front of different buildings may join); a piece whose labels differ between the two is Unknown. Labels are kept
    /// per travel profile and band of player levels: the levels between two where an open link's level limits start or
    /// end. Recall goes through one extra node that every node outside a dungeon leads to, which leads to the seeds and
    /// to the gems' destinations.
    /// </summary>
    public class WorldGraph
    {
        /// <summary>
        /// The highest player level; a higher one counts as this.
        /// </summary>
        public const int MaxLevel = 126;

        /// <summary>
        /// Rank of each label when the best over all levels is asked for: Walkable, OneWay, EscapeOnly, Unknown, then
        /// Unreachable.
        /// </summary>
        private static readonly int[] LabelRank = { 1, 0, 3, 2, 4 };

        public TileSummary[] Summaries { get; }

        public InteriorSummary[] Interiors { get; }

        /// <summary>
        /// Index of each landblock's first outdoor piece, then of each landblock's first interior piece; a landblock or
        /// interior waiting for its summary has one node standing for it.
        /// </summary>
        private readonly int[] nodeBase;

        private readonly int[] interiorBase;

        /// <summary>
        /// Per landblock with an interior summary, by group index, the group's first piece after interiorBase.
        /// </summary>
        private readonly int[][] groupBase;

        private readonly bool[] unknown;

        private readonly bool[] unknownInterior;

        /// <summary>
        /// By travel profile, level band, then node.
        /// </summary>
        private ReachabilityLabel[][][] labels;

        /// <summary>
        /// By travel profile, then node, the best label over all bands.
        /// </summary>
        private ReachabilityLabel[][] bestLabels;

        /// <summary>
        /// The band of each level from 1 to MaxLevel.
        /// </summary>
        private readonly int[] levelBand = new int[MaxLevel + 1];

        public int NodeCount { get; }

        /// <summary>
        /// The first level of each level band.
        /// </summary>
        public IReadOnlyList<int> BandLevels { get; private set; }

        public IReadOnlyList<TravelLink> Links { get; private set; }

        /// <summary>
        /// Links with an end on no piece: no walkable floor or ground there.
        /// </summary>
        public IReadOnlyList<TravelLink> DeadLinks { get; private set; }

        /// <summary>
        /// Walking groups, missing landblocks taken as walls (each still counts as a group of its own).
        /// </summary>
        public int GroupCount { get; private set; }

        public int SeedGroupCount { get; private set; }

        /// <summary>
        /// Doorways whose outdoor and inside pieces are joined.
        /// </summary>
        public int DoorJoinCount { get; private set; }

        /// <summary>
        /// Doorways passable from inside that join nothing: their landblock's tile summary has no outdoor piece in front
        /// of them. Mostly openings with no floor outside (upper-floor windows and doors, tower doors onto a narrow
        /// ledge), a few whose outside lies in a neighbor. Landblocks without a tile summary aren't counted.
        /// </summary>
        public int DoorUnjoinedCount { get; private set; }

        private WorldGraph(TileSummary[] summaries, bool[] unknown, InteriorSummary[] interiors, bool[] unknownInterior)
        {
            // copies: the node numbering must not change under the caller filling in summaries later
            Summaries = (TileSummary[])summaries.Clone();
            Interiors = (InteriorSummary[])interiors.Clone();
            this.unknown = (bool[])unknown.Clone();
            this.unknownInterior = (bool[])unknownInterior.Clone();

            nodeBase = new int[summaries.Length + 1];
            for (var i = 0; i < summaries.Length; i++)
                nodeBase[i + 1] = nodeBase[i] + (Summaries[i]?.Pieces.Count ?? (unknown[i] ? 1 : 0));

            interiorBase = new int[summaries.Length + 1];
            interiorBase[0] = nodeBase[summaries.Length];
            groupBase = new int[summaries.Length][];
            for (var i = 0; i < summaries.Length; i++)
            {
                var interior = Interiors[i];
                var count = interior == null && unknownInterior[i] ? 1 : 0;
                if (interior != null && interior.Groups.Count > 0)
                {
                    groupBase[i] = new int[interior.Groups[^1].Index + 1];
                    foreach (var group in interior.Groups)
                    {
                        groupBase[i][group.Index] = count;
                        count += group.Pieces.Count;
                    }
                }
                interiorBase[i + 1] = interiorBase[i] + count;
            }

            NodeCount = interiorBase[summaries.Length];
        }

        /// <summary>
        /// The spot's label for a player of the level, or the best one at any level.
        /// </summary>
        public ReachabilityLabel GetLabel(GraphSpot spot, int? level = null, TravelProfile profile = TravelProfile.Portals)
        {
            var (known, optimistic) = GetNodes(spot);
            if (known < 0)
                return optimistic < 0 ? ReachabilityLabel.Unreachable : ReachabilityLabel.Unknown;

            if (level != null)
                return labels[(int)profile][levelBand[Math.Clamp(level.Value, 1, MaxLevel)]][known];

            return bestLabels[(int)profile][known];
        }

        /// <summary>
        /// The spot's node in the graph of what is summarized (-1 if it has none: no summary yet, or no piece), and in
        /// the optimistic one, where a landblock or interior waiting for its summary is one node.
        /// </summary>
        private (int known, int optimistic) GetNodes(GraphSpot spot)
        {
            var landblock = spot.Landblock;
            if (spot.Group == GraphSpot.Outdoors)
            {
                if (Summaries[landblock] == null)
                    return (-1, unknown[landblock] ? nodeBase[landblock] : -1);

                var node = spot.Piece == TileSummary.NoPiece ? -1 : nodeBase[landblock] + spot.Piece;
                return (node, node);
            }

            if (Interiors[landblock] == null)
                return (-1, unknownInterior[landblock] ? interiorBase[landblock] : -1);

            var inside = spot.Piece == TileSummary.NoPiece ? -1 : GetInteriorNode(landblock, spot.Group, spot.Piece);
            return (inside, inside);
        }

        private int GetInteriorNode(int landblock, int group, ushort piece) => interiorBase[landblock] + groupBase[landblock][group] + piece;

        /// <summary>
        /// Summaries by landblock index (id >> 16). Unknown: the landblocks that have walkable ground but no summary yet
        /// (the rest without one are walls), and likewise the interiors. Seeds: the spots labels are measured from.
        /// Links: the travel between spots, of which only the open ones count. Dungeon, dungeonInterior: by landblock,
        /// whether recall is refused outdoors and indoors there. Climb: the mesh's walkableClimb, which sets the height
        /// tolerance at a border.
        /// </summary>
        public static WorldGraph Build(TileSummary[] summaries, bool[] unknown, InteriorSummary[] interiors, bool[] unknownInterior, IEnumerable<GraphSpot> seeds, IEnumerable<TravelLink> links, bool[] dungeon, bool[] dungeonInterior, float climb)
        {
            var graph = new WorldGraph(summaries, unknown, interiors, unknownInterior);
            var nodeBase = graph.nodeBase;
            var interiorBase = graph.interiorBase;
            summaries = graph.Summaries;
            interiors = graph.Interiors;
            unknown = graph.unknown;
            unknownInterior = graph.unknownInterior;

            // the recall node is the one past the last
            var recall = graph.NodeCount;
            var known = new DisjointSet(graph.NodeCount + 1);
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
            {
                var tile = summaries[i];
                var interior = interiors[i];
                if (tile != null)
                    drops.AddRange(tile.LedgeDrops.Select(d => (nodeBase[i] + d.from, nodeBase[i] + d.to)));

                // a doorway joins when both sides found it passable: the tile the piece in front, the group the one inside
                if (interior != null)
                {
                    var outside = tile?.Doors.ToDictionary(d => (d.Group, d.Door), d => d.Piece);
                    foreach (var group in interior.Groups)
                    {
                        drops.AddRange(group.LedgeDrops.Select(d => (graph.GetInteriorNode(i, group.Index, d.from), graph.GetInteriorNode(i, group.Index, d.to))));

                        foreach (var door in group.Doors)
                        {
                            var inside = graph.GetInteriorNode(i, group.Index, door.Piece);
                            if (outside != null && outside.TryGetValue((group.Index, door.Door), out var piece))
                            {
                                known.Union(nodeBase[i] + piece, inside);
                                graph.DoorJoinCount++;
                            }
                            else if (outside != null)
                                graph.DoorUnjoinedCount++;
                            else if (tile == null && unknown[i])
                                optimisticJoins.Add((nodeBase[i], inside));
                        }
                    }
                }
                else if (unknownInterior[i])
                {
                    if (tile != null)
                        optimisticJoins.AddRange(tile.Doors.Select(d => (nodeBase[i] + d.Piece, interiorBase[i])));
                    else if (unknown[i])
                        optimisticJoins.Add((nodeBase[i], interiorBase[i]));
                }
            }

            var seedNodes = seeds.Select(graph.GetNodes).ToList();
            var knownSeeds = seedNodes.Where(s => s.known >= 0).Select(s => s.known).ToList();
            var optimisticSeeds = seedNodes.Where(s => s.optimistic >= 0).Select(s => s.optimistic).ToList();

            // a link with an end on no node at all leads nowhere; one with an end waiting for its summary is only in the
            // optimistic graph
            graph.Links = links.ToList();
            var deadLinks = new List<TravelLink>();
            var open = new List<(TravelLink link, int knownFrom, int knownTo, int from, int to)>();
            foreach (var link in graph.Links)
            {
                var (knownFrom, from) = link.Anywhere ? (recall, recall) : graph.GetNodes(link.From);
                var (knownTo, to) = graph.GetNodes(link.To);
                if (from < 0 || to < 0)
                    deadLinks.Add(link);
                else if (link.Kind == TravelKind.Open)
                    open.Add((link, knownFrom, knownTo, from, to));
            }
            graph.DeadLinks = deadLinks;

            var bandLevels = open.SelectMany(o => new[] { o.link.MinLevel, o.link.MaxLevel > 0 ? o.link.MaxLevel + 1 : 0 })
                .Where(level => level > 1 && level <= MaxLevel).Append(1).Distinct().OrderBy(level => level).ToList();
            graph.BandLevels = bandLevels;
            for (int level = 1, band = 0; level <= MaxLevel; level++)
            {
                if (band + 1 < bandLevels.Count && bandLevels[band + 1] == level)
                    band++;
                graph.levelBand[level] = band;
            }

            var optimistic = known.Clone();
            foreach (var (a, b) in optimisticJoins)
                optimistic.Union(a, b);

            var (knownGroup, knownGroupCount) = GetGroups(known);
            var (optimisticGroup, optimisticGroupCount) = GetGroups(optimistic);
            var knownSeedGroups = knownSeeds.Select(n => knownGroup[n]).Distinct().ToList();
            var optimisticSeedGroups = optimisticSeeds.Select(n => optimisticGroup[n]).Distinct().ToList();
            graph.GroupCount = knownGroupCount - 1;
            graph.SeedGroupCount = knownSeedGroups.Count;

            // into the recall node once per walking group outside a dungeon, and out of it to the seeds
            List<(int, int)> GetRecallEdges(int[] group, List<int> seedNodes)
            {
                var edges = new List<(int, int)>();
                var joined = new HashSet<int>();
                void Add(int start, int end)
                {
                    for (var n = start; n < end; n++)
                        if (joined.Add(group[n]))
                            edges.Add((n, recall));
                }
                for (var i = 0; i < summaries.Length; i++)
                {
                    if (!dungeon[i])
                        Add(nodeBase[i], nodeBase[i + 1]);
                    if (!dungeonInterior[i])
                        Add(interiorBase[i], interiorBase[i + 1]);
                }
                edges.AddRange(seedNodes.Select(s => (recall, s)));
                return edges;
            }
            var knownRecall = GetRecallEdges(knownGroup, knownSeeds);
            var optimisticRecall = GetRecallEdges(optimisticGroup, optimisticSeeds);

            ReachabilityLabel[] Label(IEnumerable<(TravelLink link, int knownFrom, int knownTo, int from, int to)> active, bool withRecall)
            {
                var knownEdges = drops.Concat(active.Where(o => o.knownFrom >= 0 && o.knownTo >= 0).Select(o => (o.knownFrom, o.knownTo)));
                var optimisticEdges = drops.Concat(active.Select(o => (o.from, o.to)));
                var (reach, escape) = Solve(knownGroup, knownGroupCount, withRecall ? knownEdges.Concat(knownRecall) : knownEdges, knownSeedGroups);
                var (maybeReach, maybeEscape) = Solve(optimisticGroup, optimisticGroupCount, withRecall ? optimisticEdges.Concat(optimisticRecall) : optimisticEdges, optimisticSeedGroups);

                var result = new ReachabilityLabel[graph.NodeCount];
                for (var n = 0; n < graph.NodeCount; n++)
                {
                    bool r = reach[knownGroup[n]], e = escape[knownGroup[n]];
                    result[n] = r && e ? ReachabilityLabel.Walkable
                        : r != maybeReach[optimisticGroup[n]] || e != maybeEscape[optimisticGroup[n]] ? ReachabilityLabel.Unknown
                        : r ? ReachabilityLabel.OneWay
                        : e ? ReachabilityLabel.EscapeOnly : ReachabilityLabel.Unreachable;
                }
                return result;
            }

            // walking has no level limits: one array serves every band
            var walk = Label(Enumerable.Empty<(TravelLink, int, int, int, int)>(), false);
            graph.labels = Enum.GetValues<TravelProfile>().Select(_ => new ReachabilityLabel[bandLevels.Count][]).ToArray();
            for (var b = 0; b < bandLevels.Count; b++)
            {
                var level = bandLevels[b];
                var active = open.Where(o => o.link.MinLevel <= level && (o.link.MaxLevel == 0 || level <= o.link.MaxLevel)).ToList();
                graph.labels[(int)TravelProfile.Walk][b] = walk;
                graph.labels[(int)TravelProfile.Portals][b] = Label(active.Where(o => !o.link.Anywhere), false);
                graph.labels[(int)TravelProfile.Recall][b] = Label(active, true);
            }

            graph.bestLabels = graph.labels.Select(bands =>
            {
                var best = new ReachabilityLabel[graph.NodeCount];
                for (var n = 0; n < graph.NodeCount; n++)
                    best[n] = bands.Select(band => band[n]).MaxBy(label => LabelRank[(int)label]);
                return best;
            }).ToArray();

            return graph;
        }

        /// <summary>
        /// Each node's group in the set, and how many groups there are.
        /// </summary>
        private static (int[] group, int count) GetGroups(DisjointSet set)
        {
            var group = new int[set.Count];
            var groupOfRoot = new int[set.Count];
            Array.Fill(groupOfRoot, -1);
            var count = 0;
            for (var n = 0; n < set.Count; n++)
            {
                var root = set.Find(n);
                if (groupOfRoot[root] < 0)
                    groupOfRoot[root] = count++;
                group[n] = groupOfRoot[root];
            }
            return (group, count);
        }

        /// <summary>
        /// Per group, whether it can be reached from a seed group and can reach one, over the one-way edges (ledge drops,
        /// travel links) between nodes.
        /// </summary>
        private static (bool[] reach, bool[] escape) Solve(int[] group, int groupCount, IEnumerable<(int from, int to)> edges, List<int> seedGroups)
        {
            var forward = new List<int>[groupCount];
            var backward = new List<int>[groupCount];
            foreach (var (from, to) in edges)
            {
                int a = group[from], b = group[to];
                if (a == b)
                    continue;

                (forward[a] ??= new List<int>()).Add(b);
                (backward[b] ??= new List<int>()).Add(a);
            }

            return (Flood(seedGroups, forward), Flood(seedGroups, backward));
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
