using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects;

using Position = ACE.Entity.Position;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The roads of the whole world as a graph: the terrain's road vertices, linked the way the client paints a road
    /// between them (see Physics.Common.Landblock.OnRoad), plus the desert path markers. All positions are world
    /// coordinates. A route follows the road's centre line, so it is what the road looks like, not the nearest road
    /// cell.
    /// </summary>
    public sealed class RoadNetwork
    {
        private const uint DesertPathMarker = 0x02000451;

        /// <summary>
        /// Bridge decks are buildings, made of segments 24 m long: the Holtburg to Arwic bridge and the Rithwic bridge.
        /// The road vertices beside them lie off the deck, so the roads leave them out and the pathfinder, which knows
        /// the deck, walks the walker across.
        /// </summary>
        private static readonly HashSet<uint> BridgeSegments = new HashSet<uint>
        {
            0x01001967, 0x01001968, 0x0100196A, 0x0100196B, 0x0100196E, 0x01001971,
            0x01000D6F, 0x01000D70, 0x01000D71, 0x01000D72
        };

        /// <summary>
        /// Road vertices this close to a bridge segment's center are left out.
        /// </summary>
        private const float BridgeClearance = 20f;

        /// <summary>
        /// Bridge segments this close to each other are one bridge.
        /// </summary>
        private const float BridgeJoinDistance = 60f;

        /// <summary>
        /// A bridge's end nodes lie this far beyond its end segments' centers, and join the nearest road within
        /// BridgeApproach.
        /// </summary>
        private const float BridgeEndOffset = 24f;
        private const float BridgeApproach = 70f;
        private const float BridgeLinkDistance = 40f;

        /// <summary>
        /// A position this close to the line between a bridge's ends is on the bridge (or at its end).
        /// </summary>
        private const float BridgeReach = 15f;

        /// <summary>
        /// Desert path markers this close to each other are one path.
        /// </summary>
        private const float MarkerLinkDistance = 55f;

        /// <summary>
        /// A desert path marker this close to a road vertex is linked to the nearest one.
        /// </summary>
        private const float MarkerRoadLinkDistance = 50f;

        /// <summary>
        /// Taking a portal costs this many metres of road: the walk onto it and the arrival.
        /// </summary>
        private const float TeleportCost = 40f;

        /// <summary>
        /// A place nearer than this is walked to directly when the road route would cost more.
        /// </summary>
        public const float MaxDirectWalk = 400f;

        /// <summary>
        /// A trace turning more than this away from its direction ends: the road turns back on itself, or what is
        /// left isn't a continuation of it.
        /// </summary>
        private const float MaxTurnCos = -0.17f; // cos(100 degrees)

        /// <summary>
        /// A road point this much above or below the start is another level (a bridge, a cliff), not the road under it.
        /// </summary>
        private const float MaxHeightDifference = 15f;

        /// <summary>
        /// A nearest road link at more than about 45 degrees across the way we face is ambiguous (cos of the angle to
        /// the link is below this): both ways along the road are traced.
        /// </summary>
        private const float AcrossCos = 0.7f;

        /// <summary>
        /// A point of a trace is centered on the road nodes connected to it within this distance and within this
        /// distance along the heading (a stretch of road).
        /// </summary>
        private const float CentreRadius = 60f;

        /// <summary>
        /// The direction a trace ends in is that of its last stretch of this length.
        /// </summary>
        private const float DirectionSpan = 30f;
        private const float CentreHalfLength = 12f;

        /// <summary>
        /// Points of a trace whose path bends by less than this (cos) are dropped as collinear.
        /// </summary>
        private const float CollinearCos = 0.98f; // about 11 degrees

        private readonly List<Vector3> nodes = new List<Vector3>();
        private readonly List<List<int>> links = new List<List<int>>();

        /// <summary>
        /// The road a bridge's end nodes join, where the walker leaves the bridge. The nodes themselves are not on
        /// the pathfinder's mesh: a segment's origin is not at the height of its deck.
        /// </summary>
        private readonly Dictionary<(int, int), (Vector3 nearExit, Vector3 farExit, bool nearOnRoad, bool farOnRoad)> bridgeEdges = new Dictionary<(int, int), (Vector3, Vector3, bool, bool)>();

        /// <summary>
        /// Each bridge end node and the node at the other end of its bridge.
        /// </summary>
        private readonly Dictionary<int, int> otherBridgeEnds = new Dictionary<int, int>();

        /// <summary>
        /// The piece of connected roads each node belongs to.
        /// </summary>
        private int[] pieces;

        /// <summary>
        /// A path along a road, from a point on it.
        /// </summary>
        public class RoadTrace
        {
            public List<Vector3> Points;

            /// <summary>
            /// The direction the trace ends in.
            /// </summary>
            public Vector2 Direction;

            /// <summary>
            /// True if the road goes on beyond the end of the trace (the length limit stopped it), false if it ends or
            /// turns back.
            /// </summary>
            public bool Continues;

            /// <summary>
            /// Set when the trace ends at a bridge: the way across, which the walker is taken along.
            /// </summary>
            public BridgeCrossing Bridge;
        }

        /// <summary>
        /// A way across a bridge, in the direction of travel.
        /// </summary>
        public class BridgeCrossing
        {
            /// <summary>
            /// The road beyond the far end, on the pathfinder's mesh: where the walk across leads to.
            /// </summary>
            public Vector3 Exit;

            /// <summary>
            /// False if no road is near the bridge's far end: the exit is then the end node, which is only a place on
            /// the ground (its height is the segment's, not the ground's).
            /// </summary>
            public bool ExitOnRoad;

            /// <summary>
            /// The line between the bridge's ends.
            /// </summary>
            public Vector2 AxisFrom;
            public Vector2 AxisTo;
        }

        private static readonly Lazy<RoadNetwork> World = new Lazy<RoadNetwork>(Build, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// The roads of the whole world, built once on first use (about a second).
        /// </summary>
        public static RoadNetwork GetWorld() => World.Value;

        public static bool IsBuilt => World.IsValueCreated;

        private static RoadNetwork Build()
        {
            var network = new RoadNetwork();

            var terrain = new Dictionary<(int x, int y), ushort[]>();
            var heights = new Dictionary<(int x, int y), byte[]>();
            var markers = new List<Vector3>();
            var bridges = new List<Vector3>();

            for (var lx = 0; lx <= 0xFE; lx++)
            {
                for (var ly = 0; ly <= 0xFE; ly++)
                {
                    var id = (uint)(lx << 24 | ly << 16);
                    var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(id | 0xFFFF);
                    if (cellLandblock == null)
                        continue;

                    terrain[(lx, ly)] = cellLandblock.Terrain.ToArray();
                    heights[(lx, ly)] = cellLandblock.Height.ToArray();

                    var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(id | 0xFFFE);
                    if (info == null)
                        continue;

                    foreach (var building in info.Buildings)
                    {
                        if (BridgeSegments.Contains(building.ModelId))
                            bridges.Add(new Vector3(lx * LandDefs.BlockLength + building.Frame.Origin.X, ly * LandDefs.BlockLength + building.Frame.Origin.Y, building.Frame.Origin.Z));
                    }

                    foreach (var obj in info.Objects)
                    {
                        if (obj.Id == DesertPathMarker)
                            markers.Add(new Vector3(lx * LandDefs.BlockLength + obj.Frame.Origin.X, ly * LandDefs.BlockLength + obj.Frame.Origin.Y, obj.Frame.Origin.Z));
                    }
                }
            }

            // a vertex is the corner of a cell; the landblock's last row and column belong to its neighbors
            bool IsRoad(int gx, int gy)
            {
                var block = (gx / LandDefs.BlockSide, gy / LandDefs.BlockSide);
                return terrain.TryGetValue(block, out var t) && (t[gx % LandDefs.BlockSide * (LandDefs.BlockSide + 1) + gy % LandDefs.BlockSide] & CellLandblock.TerrainMask_Road) != 0;
            }

            var gridNodes = new Dictionary<(int x, int y), int>();
            foreach (var block in terrain.Keys)
            {
                for (var vx = 0; vx < LandDefs.BlockSide; vx++)
                {
                    for (var vy = 0; vy < LandDefs.BlockSide; vy++)
                    {
                        var gx = block.x * LandDefs.BlockSide + vx;
                        var gy = block.y * LandDefs.BlockSide + vy;
                        if (!IsRoad(gx, gy))
                            continue;

                        var vertex = new Vector2(gx * LandDefs.CellLength, gy * LandDefs.CellLength);
                        if (bridges.Any(bridge => Vector2.Distance(Flat(bridge), vertex) < BridgeClearance))
                            continue;

                        var z = DatManager.PortalDat.RegionDesc.LandDefs.LandHeightTable[heights[block][vx * (LandDefs.BlockSide + 1) + vy]];
                        gridNodes[(gx, gy)] = network.AddNode(new Vector3(gx * LandDefs.CellLength, gy * LandDefs.CellLength, z));
                    }
                }
            }

            foreach (var ((gx, gy), index) in gridNodes)
            {
                // each link once, from its lower end
                foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
                {
                    if (!gridNodes.TryGetValue((gx + dx, gy + dy), out var other))
                        continue;

                    // The client paints a diagonal between two vertices only across a cell whose other corners are
                    // both road (a wide road) or both not; with one of them road the cell is bent round it.
                    if (dx != 0 && dy != 0 && IsRoad(gx + dx, gy) != IsRoad(gx, gy + dy))
                        continue;

                    network.Link(index, other);
                }
            }

            var firstMarker = network.nodes.Count;
            foreach (var marker in markers)
                network.AddNode(marker);

            for (var i = firstMarker; i < network.nodes.Count; i++)
            {
                for (var j = i + 1; j < network.nodes.Count; j++)
                {
                    if (Vector2.Distance(network.Flat(i), network.Flat(j)) <= MarkerLinkDistance)
                        network.Link(i, j);
                }
            }

            // a desert path that ends at a road joins it
            for (var i = firstMarker; i < network.nodes.Count; i++)
            {
                var nearest = -1;
                var nearestDistance = MarkerRoadLinkDistance;
                for (var j = 0; j < firstMarker; j++)
                {
                    var distance = Vector2.Distance(network.Flat(i), network.Flat(j));
                    if (distance <= nearestDistance)
                    {
                        nearest = j;
                        nearestDistance = distance;
                    }
                }

                if (nearest >= 0)
                    network.Link(i, nearest);
            }

            network.AddBridges(bridges);
            network.LabelPieces();
            return network;
        }

        /// <summary>
        /// Each bridge is two nodes, one at each end, joined by a bridge edge, and each joined to the road on its side.
        /// The edge is walked with the pathfinder, which knows the deck.
        /// </summary>
        private void AddBridges(List<Vector3> segments)
        {
            var roadNodes = nodes.Count;
            var cluster = Enumerable.Range(0, segments.Count).ToArray();
            int Root(int i)
            {
                while (cluster[i] != i)
                    i = cluster[i] = cluster[cluster[i]];
                return i;
            }

            for (var i = 0; i < segments.Count; i++)
            {
                for (var j = i + 1; j < segments.Count; j++)
                {
                    if (Vector2.Distance(Flat(segments[i]), Flat(segments[j])) <= BridgeJoinDistance)
                        cluster[Root(i)] = Root(j);
                }
            }

            foreach (var bridge in Enumerable.Range(0, segments.Count).GroupBy(Root))
            {
                var members = bridge.Select(i => segments[i]).ToList();
                var (a, b) = members.SelectMany(p => members, (p, q) => (p, q)).OrderByDescending(pair => Vector2.DistanceSquared(Flat(pair.p), Flat(pair.q))).First();
                if (a == b)
                    continue;

                var direction = Vector2.Normalize(new Vector2(b.X - a.X, b.Y - a.Y));
                var near = AddNode(new Vector3(a.X - direction.X * BridgeEndOffset, a.Y - direction.Y * BridgeEndOffset, a.Z));
                var far = AddNode(new Vector3(b.X + direction.X * BridgeEndOffset, b.Y + direction.Y * BridgeEndOffset, b.Z));

                otherBridgeEnds[near] = far;
                otherBridgeEnds[far] = near;

                var exits = new List<Vector3>();
                var onRoad = new List<bool>();
                foreach (var end in new[] { near, far })
                {
                    var road = FindNearestNode(Flat(end), roadNodes, false);
                    var joined = road >= 0 && Vector2.Distance(Flat(road), Flat(end)) <= BridgeApproach;

                    // every vertex of the patch of road at the end: a trace reaching one of them can't always turn to the next
                    foreach (var vertex in Enumerable.Range(0, roadNodes).Where(i => Vector2.Distance(Flat(i), Flat(end)) <= BridgeLinkDistance || (joined && i == road)))
                        Link(end, vertex);

                    // a road's exit is its middle (where the trace goes on), not the vertex nearest the end node, which may lie at the road's edge
                    exits.Add(joined ? Centre(nodes[road], direction) : nodes[end]);
                    onRoad.Add(joined);
                }

                Link(near, far);
                bridgeEdges[(near, far)] = (exits[0], exits[1], onRoad[0], onRoad[1]);
            }
        }

        private bool IsBridgeEnd(int node) => otherBridgeEnds.ContainsKey(node);

        private int GetOtherBridgeEnd(int end) => otherBridgeEnds[end];

        /// <summary>
        /// The node nearest a point among the first count nodes, or -1 if there is none.
        /// </summary>
        private int FindNearestNode(Vector2 point, int count, bool skipBridgeEnds)
        {
            var nearest = -1;
            var nearestDistance = float.MaxValue;

            for (var i = 0; i < count; i++)
            {
                var distance = Vector2.DistanceSquared(Flat(i), point);
                if (distance < nearestDistance && !(skipBridgeEnds && IsBridgeEnd(i)))
                {
                    nearest = i;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The crossing of a bridge from one end node to the other.
        /// </summary>
        private BridgeCrossing GetCrossing(int from, int to)
        {
            var (nearExit, farExit, nearOnRoad, farOnRoad) = bridgeEdges[(Math.Min(from, to), Math.Max(from, to))];
            return new BridgeCrossing { Exit = from < to ? farExit : nearExit, ExitOnRoad = from < to ? farOnRoad : nearOnRoad, AxisFrom = Flat(from), AxisTo = Flat(to) };
        }

        /// <summary>
        /// If the position is within reach of a bridge, the crossing in the direction the direction faces, unless the
        /// position is already at its exit. The road under or beside a bridge is on another level, so tracing it from the
        /// deck leads off the deck. Null if not at a bridge or nothing is left to cross.
        /// </summary>
        public BridgeCrossing FindBridgeAhead(Vector3 position, Vector2 direction)
        {
            var here = Flat(position);

            foreach (var edge in bridgeEdges.Keys)
            {
                var (near, far) = (Flat(edge.Item1), Flat(edge.Item2));
                var axis = far - near;
                var along = Math.Clamp(Vector2.Dot(here - near, axis) / axis.LengthSquared(), 0f, 1f);
                if (Vector2.Distance(here, near + axis * along) > BridgeReach)
                    continue;

                var crossing = Vector2.Dot(axis, direction) >= 0 ? GetCrossing(edge.Item1, edge.Item2) : GetCrossing(edge.Item2, edge.Item1);
                if (Vector2.Dot(Flat(crossing.Exit) - here, Vector2.Normalize(crossing.AxisTo - crossing.AxisFrom)) > BridgeReach / 2)
                    return crossing;
            }

            return null;
        }

        private void LabelPieces()
        {
            pieces = new int[nodes.Count];
            Array.Fill(pieces, -1);

            var piece = 0;
            var pending = new Stack<int>();
            for (var start = 0; start < nodes.Count; start++)
            {
                if (pieces[start] >= 0)
                    continue;

                pieces[start] = piece;
                pending.Push(start);
                while (pending.Count > 0)
                {
                    foreach (var next in links[pending.Pop()])
                    {
                        if (pieces[next] < 0)
                        {
                            pieces[next] = piece;
                            pending.Push(next);
                        }
                    }
                }

                piece++;
            }
        }

        private int AddNode(Vector3 position)
        {
            nodes.Add(position);
            links.Add(new List<int>());
            return nodes.Count - 1;
        }

        private void Link(int a, int b)
        {
            links[a].Add(b);
            links[b].Add(a);
        }

        private Vector2 Flat(int node) => Flat(nodes[node]);

        private static Vector2 Flat(Vector3 position) => new Vector2(position.X, position.Y);

        /// <summary>
        /// Follows the road from the point on it nearest to the position, in the direction the player faces (or
        /// continues in, when given), always taking the straightest way on. Returns null if no road is within reach.
        /// </summary>
        /// <param name="direction">The direction to go in: the player's facing, or where an earlier trace ended</param>
        /// <param name="maxApproach">How far the position may be from the road</param>
        /// <param name="maxLength">The trace ends after about this length (a trace is planned again from its end)</param>
        public RoadTrace Trace(Vector3 position, Vector2 direction, float maxApproach, float maxLength)
        {
            if (!TryFindNearestRoadPoint(position, maxApproach, out var a, out var b, out var entry))
                return null;

            // along the road in the direction we face
            var toB = Vector2.Normalize(Flat(b) - Flat(a));
            var facingB = Vector2.Dot(toB, direction);
            var trace = facingB >= 0 ? Walk(entry, a, b, maxLength, direction) : Walk(entry, b, a, maxLength, direction);

            // The nearest link of a wide road may run across it, saying little about which way along it we face: both
            // ways are walked then, and the one that gets further the way we face wins.
            if (Math.Abs(facingB) < AcrossCos)
            {
                var other = facingB >= 0 ? Walk(entry, b, a, maxLength, direction) : Walk(entry, a, b, maxLength, direction);
                float Progress(RoadTrace t) => Vector2.Dot(new Vector2(t.Points[^1].X - entry.X, t.Points[^1].Y - entry.Y), direction);
                if (Progress(other) > Progress(trace))
                    trace = other;
            }

            return trace;
        }

        /// <summary>
        /// The trace's points are moved to the middle of the road: a wide road is many parallel links, and following
        /// the straightest of them wanders between its rows.
        /// </summary>
        private RoadTrace Walk(Vector3 entry, int previous, int current, float maxLength, Vector2 direction)
        {
            var points = new List<Vector3> { entry, nodes[current] };
            var visited = new HashSet<int> { previous, current };
            var toCurrent = Flat(current) - Flat(entry);
            var length = toCurrent.Length();
            var heading = length > 0.01f ? toCurrent / length : Vector2.Normalize(Flat(current) - Flat(previous));
            var continues = true;
            BridgeCrossing bridge = null;
            int? lastVertex = null;
            var bridgeDirection = Vector2.Zero;

            while (length < maxLength)
            {
                // A bridge: the trace ends before its end node (not on the mesh, see bridgeEdges), and the walker is taken
                // across. The turn to the node doesn't count, the square patches the roads end in can be left at any angle.
                // (not one that leads back the way the walker faces away from: the road just beyond a bridge links to its far end)
                var entrance = links[current].Where(IsBridgeEnd).Cast<int?>().FirstOrDefault(end => Vector2.Dot(Flat(GetOtherBridgeEnd(end.Value)) - Flat(end.Value), direction) >= 0);
                if (entrance != null)
                {
                    bridge = GetCrossing(entrance.Value, GetOtherBridgeEnd(entrance.Value));

                    // the road goes on to the vertex nearest the end node: the walk leaves it there, not at the first vertex linked to the node
                    var closest = links[entrance.Value].Where(vertex => !visited.Contains(vertex) && !IsBridgeEnd(vertex) && Vector2.Dot(Flat(vertex) - Flat(current), heading) > 0)
                        .OrderBy(vertex => Vector2.DistanceSquared(Flat(vertex), Flat(entrance.Value))).Cast<int?>().FirstOrDefault();
                    if (closest != null && Vector2.DistanceSquared(Flat(closest.Value), Flat(entrance.Value)) < Vector2.DistanceSquared(Flat(current), Flat(entrance.Value)))
                    {
                        lastVertex = closest.Value;
                        current = closest.Value;
                    }

                    heading = Vector2.Normalize(Flat(entrance.Value) - Flat(current));
                    bridgeDirection = Vector2.Normalize(Flat(GetOtherBridgeEnd(entrance.Value)) - Flat(entrance.Value));
                    break;
                }

                var best = -1;
                var bestCos = MaxTurnCos;
                var bestDistance = 0f;

                foreach (var next in links[current])
                {
                    if (visited.Contains(next) || IsBridgeEnd(next))
                        continue;

                    var step = Flat(next) - Flat(current);
                    var cos = Vector2.Dot(Vector2.Normalize(step), heading);

                    // the straightest way on; of equally straight ones the nearer, so a wide road isn't walked in jumps
                    if (cos > bestCos + 0.01f || (cos > bestCos - 0.01f && best >= 0 && step.Length() < bestDistance))
                    {
                        best = next;
                        bestCos = cos;
                        bestDistance = step.Length();
                    }
                }

                if (best < 0)
                {
                    continues = false;
                    break;
                }

                var stepVector = Flat(best) - Flat(current);
                heading = Vector2.Normalize(stepVector);
                length += stepVector.Length();
                visited.Add(best);
                points.Add(nodes[best]);
                current = best;
            }

            // the first point is where the walker joins the road, the others are put in the middle of it
            for (var i = 1; i < points.Count; i++)
            {
                var along = points[Math.Min(i + 1, points.Count - 1)] - points[i - 1];
                points[i] = Centre(points[i], Flat(along));
            }

            if (lastVertex != null)
                points.Add(Centre(nodes[lastVertex.Value], bridgeDirection));

            // the last link of a wide road can run across it: the road's direction is that of its last stretch
            for (var i = points.Count - 2; i >= 0; i--)
            {
                var back = new Vector2(points[^1].X - points[i].X, points[^1].Y - points[i].Y);
                if (back.Length() >= DirectionSpan)
                {
                    heading = Vector2.Normalize(back);
                    break;
                }
            }

            return new RoadTrace { Points = DropCollinear(points), Direction = heading, Continues = continues, Bridge = bridge };
        }

        /// <summary>
        /// The point moved across the road (at right angles to the heading) to the middle of the road around it, the
        /// mean of the connected road nodes within a short stretch of road along the heading.
        /// </summary>
        private Vector3 Centre(Vector3 point, Vector2 heading)
        {
            if (heading.LengthSquared() < 0.0001f)
                return point;

            heading = Vector2.Normalize(heading);
            var across = new Vector2(-heading.Y, heading.X);
            var here = Flat(point);

            var start = FindNearestNode(here, nodes.Count, true);
            if (start < 0 || Vector2.Distance(Flat(start), here) > CentreRadius)
                return point;

            var seen = new HashSet<int> { start };
            var pending = new Stack<int>();
            pending.Push(start);
            var count = 0;
            var offset = 0f;
            var z = 0f;

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                var relative = Flat(node) - here;
                if (Math.Abs(Vector2.Dot(relative, heading)) <= CentreHalfLength)
                {
                    count++;
                    offset += Vector2.Dot(relative, across);
                    z += nodes[node].Z;
                }

                foreach (var next in links[node])
                {
                    if (seen.Contains(next) || IsBridgeEnd(next) || Vector2.Distance(Flat(next), here) > CentreRadius)
                        continue;

                    seen.Add(next);
                    pending.Push(next);
                }
            }

            if (count == 0)
                return point;

            var middle = here + across * (offset / count);
            return new Vector3(middle.X, middle.Y, z / count);
        }

        /// <summary>
        /// The nearest road nodes ahead of a point, in a cone around the direction: where the road goes on after a gap
        /// the roads' own links don't cross (a bridge deck is a building, not road terrain).
        /// </summary>
        public List<Vector3> FindNodesAhead(Vector3 from, Vector2 direction, float minDistance, float maxDistance, float minCos, int count)
        {
            var origin = Flat(from);

            // road of the piece the walker is on is not across the gap: a wide road's other side would be picked
            var own = FindNearestNode(origin, nodes.Count, false);
            var ownPiece = own >= 0 && Vector2.Distance(Flat(own), origin) <= minDistance * 4 ? pieces[own] : -1;

            return Enumerable.Range(0, nodes.Count)
                .Where(i => pieces[i] != ownPiece && !IsBridgeEnd(i))
                .Select(i => (node: nodes[i], offset: Flat(i) - origin))
                .Where(n => n.offset.Length() >= minDistance && n.offset.Length() <= maxDistance && Vector2.Dot(Vector2.Normalize(n.offset), direction) >= minCos)
                .OrderBy(n => n.offset.Length())
                .Take(count)
                .Select(n => n.node)
                .ToList();
        }

        /// <summary>
        /// A portal standing on the ground, from where it is to where it leads, for a route to take.
        /// </summary>
        public class PortalLink
        {
            public Vector3 From, To;

            /// <summary>
            /// Where the portal stands, with its cell: From is only its place on the world's grid.
            /// </summary>
            public Position Location;
            public Portal Portal;
        }

        /// <summary>
        /// The portals a route may take, by the road node nearest the portal, with what taking one costs.
        /// </summary>
        private readonly Dictionary<int, List<(PortalLink link, int to, float cost)>> portalsAt = new Dictionary<int, List<(PortalLink, int, float)>>();

        /// <summary>
        /// Joins the portals to the roads: each is reached from the road node nearest it and leads to the one nearest
        /// where it ends. A portal with no road within maxApproach of either end is left out.
        /// </summary>
        public void SetPortals(IEnumerable<PortalLink> portals, float maxApproach)
        {
            portalsAt.Clear();

            foreach (var link in portals)
            {
                var from = FindNearestNode(Flat(link.From), nodes.Count, true);
                var to = FindNearestNode(Flat(link.To), nodes.Count, true);
                if (from < 0 || to < 0)
                    continue;

                var fromLeg = Vector2.Distance(Flat(from), Flat(link.From));
                var toLeg = Vector2.Distance(Flat(to), Flat(link.To));
                if (fromLeg > maxApproach || toLeg > maxApproach)
                    continue;

                if (!portalsAt.TryGetValue(from, out var hops))
                    portalsAt[from] = hops = new List<(PortalLink, int, float)>();

                hops.Add((link, to, TeleportCost + Pathfinder.OffRoadCostFactor * (fromLeg + toLeg)));
            }
        }

        /// <summary>
        /// A route over the roads to a place.
        /// </summary>
        public class RoadRoute
        {
            /// <summary>
            /// The points of the way to walk: up to the end of the route, or to the first portal on it if it takes any.
            /// </summary>
            public List<Vector3> Points;

            /// <summary>
            /// Metres of road (not the portals' cost) over the whole route.
            /// </summary>
            public float RoadLength;

            /// <summary>
            /// The first portal the route takes, where Points end, and how many it takes in all.
            /// </summary>
            public PortalLink Portal;
            public int PortalCount;

            /// <summary>
            /// Metres off the road at the start (to reach it) and at the end (from its end to the place).
            /// </summary>
            public float StartLeg, EndLeg;
        }

        /// <summary>
        /// The cheapest way over the roads from the road point nearest to a position to the road node nearest to the
        /// place, counting the off-road metres at both ends double. Returns null if no road is within reach of either
        /// end, they are not joined by road, or walking straight to the place would cost less. The portals canUsePortal
        /// accepts may be taken on the way; none are when it is null.
        /// </summary>
        public RoadRoute FindRoute(Vector3 from, Vector3 to, float maxApproach, float maxOffRoad, Func<PortalLink, bool> canUsePortal = null)
        {
            if (!TryFindNearestRoadPoint(from, maxApproach, out var a, out var b, out var entry))
                return null;

            var cost = new float[nodes.Count];
            var previous = new int[nodes.Count];
            var via = new PortalLink[nodes.Count];
            Array.Fill(cost, float.MaxValue);
            Array.Fill(previous, -1);

            var queue = new PriorityQueue<int, float>();
            foreach (var seed in new[] { a, b })
            {
                cost[seed] = Vector2.Distance(Flat(seed), Flat(entry));
                queue.Enqueue(seed, cost[seed]);
            }

            while (queue.TryDequeue(out var node, out var nodeCost))
            {
                if (nodeCost > cost[node])
                    continue;

                foreach (var next in links[node])
                {
                    var nextCost = nodeCost + Vector2.Distance(Flat(node), Flat(next));
                    if (nextCost < cost[next])
                    {
                        cost[next] = nextCost;
                        previous[next] = node;
                        via[next] = null;
                        queue.Enqueue(next, nextCost);
                    }
                }

                if (canUsePortal == null || !portalsAt.TryGetValue(node, out var hops))
                    continue;

                foreach (var (link, next, hopCost) in hops)
                {
                    if (nodeCost + hopCost < cost[next] && canUsePortal(link))
                    {
                        cost[next] = nodeCost + hopCost;
                        previous[next] = node;
                        via[next] = link;
                        queue.Enqueue(next, cost[next]);
                    }
                }
            }

            var target = Flat(to);
            var end = -1;
            var endTotal = float.MaxValue;
            for (var i = 0; i < nodes.Count; i++)
            {
                var offRoad = Vector2.Distance(Flat(i), target);
                if (cost[i] == float.MaxValue || offRoad > maxOffRoad || IsBridgeEnd(i))
                    continue;

                if (cost[i] + Pathfinder.OffRoadCostFactor * offRoad < endTotal)
                {
                    end = i;
                    endTotal = cost[i] + Pathfinder.OffRoadCostFactor * offRoad;
                }
            }

            if (end < 0)
                return null;

            var startLeg = Vector2.Distance(Flat(from), Flat(entry));

            // a place within walking distance is not worth a detour over the roads
            var straight = Vector2.Distance(Flat(from), target);
            if (straight <= MaxDirectWalk && Pathfinder.OffRoadCostFactor * startLeg + endTotal >= Pathfinder.OffRoadCostFactor * straight)
                return null;

            var points = new List<Vector3>();
            PortalLink portal = null;
            var portalCount = 0;
            var portalCost = 0f;
            for (var node = end; node >= 0; node = previous[node])
            {
                // what was gathered lies beyond a portal: only the way to the first one is walked
                if (via[node] != null)
                {
                    points.Clear();
                    portal = via[node];
                    portalCount++;
                    portalCost += cost[node] - cost[previous[node]];
                    continue;
                }

                // a bridge's end nodes are not on the mesh: the pathfinder's route crosses the bridge between its road points
                if (!IsBridgeEnd(node))
                    points.Add(nodes[node]);
            }
            points.Add(entry);
            points.Reverse();
            if (portal != null)
                points.Add(portal.From);

            return new RoadRoute
            {
                Points = DropCollinear(points), RoadLength = cost[end] - portalCost, Portal = portal, PortalCount = portalCount,
                StartLeg = startLeg, EndLeg = Vector2.Distance(Flat(end), target)
            };
        }

        /// <summary>
        /// The point of the road nearest to the position, on a link between two nodes.
        /// </summary>
        private bool TryFindNearestRoadPoint(Vector3 position, float maxApproach, out int a, out int b, out Vector3 point)
        {
            a = b = -1;
            point = default;

            var p = Flat(position);
            var closest = maxApproach;

            for (var i = 0; i < nodes.Count; i++)
            {
                foreach (var j in links[i])
                {
                    if (j < i)
                        continue;

                    // a bridge's end nodes are not on the mesh (see bridgeEdges), so the walk never starts at them
                    if (IsBridgeEnd(i) || IsBridgeEnd(j))
                        continue;

                    var start = Flat(i);
                    var edge = Flat(j) - start;
                    var t = Math.Clamp(Vector2.Dot(p - start, edge) / edge.LengthSquared(), 0f, 1f);
                    var distance = Vector2.Distance(p, start + edge * t);
                    var z = nodes[i].Z + (nodes[j].Z - nodes[i].Z) * t;

                    if (distance >= closest || Math.Abs(z - position.Z) > MaxHeightDifference)
                        continue;

                    closest = distance;
                    a = i;
                    b = j;
                    point = new Vector3(start.X + edge.X * t, start.Y + edge.Y * t, z);
                }
            }

            return a >= 0;
        }

        private static List<Vector3> DropCollinear(List<Vector3> points)
        {
            var result = new List<Vector3> { points[0] };

            for (var i = 1; i < points.Count - 1; i++)
            {
                var before = new Vector2(points[i].X - result[^1].X, points[i].Y - result[^1].Y);
                var after = new Vector2(points[i + 1].X - points[i].X, points[i + 1].Y - points[i].Y);

                if (before.LengthSquared() > 0 && after.LengthSquared() > 0 && Vector2.Dot(Vector2.Normalize(before), Vector2.Normalize(after)) < CollinearCos)
                    result.Add(points[i]);
            }

            result.Add(points[^1]);
            return result;
        }

        /// <summary>
        /// The outdoor position of a world point, on the landscape's cell grid.
        /// </summary>
        public static Position ToPosition(Vector3 world)
        {
            var x = Math.Clamp((int)MathF.Floor(world.X / LandDefs.BlockLength), 0, 0xFE);
            var y = Math.Clamp((int)MathF.Floor(world.Y / LandDefs.BlockLength), 0, 0xFE);
            var local = new Vector3(Math.Clamp(world.X - x * LandDefs.BlockLength, 0f, LandDefs.BlockLength - 0.01f), Math.Clamp(world.Y - y * LandDefs.BlockLength, 0f, LandDefs.BlockLength - 0.01f), world.Z);

            var position = new Position((uint)(x << 24 | y << 16 | 1), local, Quaternion.Identity);
            position.LandblockId = new LandblockId(position.GetOutdoorCell());
            return position;
        }
    }
}
