using System;
using System.Collections.Generic;
using System.Numerics;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Physics.Common;

using Position = ACE.Entity.Position;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The roads around a landblock as a graph: the terrain's road vertices, linked the way the client paints a road
    /// between them (see Physics.Common.Landblock.OnRoad), plus the desert path markers. All positions are world
    /// coordinates. A route follows the road's centre line, so it is what the road looks like, not the nearest road
    /// cell.
    /// </summary>
    public sealed class RoadNetwork
    {
        private const uint DesertPathMarker = 0x02000451;

        /// <summary>
        /// Desert path markers this close to each other are one path.
        /// </summary>
        private const float MarkerLinkDistance = 55f;

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
        /// Points of a trace whose path bends by less than this (cos) are dropped as collinear.
        /// </summary>
        private const float CollinearCos = 0.98f; // about 11 degrees

        private readonly List<Vector3> nodes = new List<Vector3>();
        private readonly List<List<int>> links = new List<List<int>>();

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
        }

        /// <summary>
        /// Builds the roads of the 3x3 landblocks around the center. A trace of at most a landblock stays inside.
        /// </summary>
        public static RoadNetwork Build(LandblockId center)
        {
            var network = new RoadNetwork();

            var terrain = new Dictionary<(int x, int y), ushort[]>();
            var heights = new Dictionary<(int x, int y), byte[]>();
            var markers = new List<Vector3>();

            for (var lx = center.LandblockX - 1; lx <= center.LandblockX + 1; lx++)
            {
                for (var ly = center.LandblockY - 1; ly <= center.LandblockY + 1; ly++)
                {
                    if (lx < 0 || ly < 0 || lx > 0xFE || ly > 0xFE)
                        continue;

                    var id = (uint)(lx << 24 | ly << 16);
                    var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(id | 0xFFFF);
                    if (cellLandblock == null)
                        continue;

                    terrain[(lx, ly)] = cellLandblock.Terrain.ToArray();
                    heights[(lx, ly)] = cellLandblock.Height.ToArray();

                    var info = DatManager.CellDat.ReadFromDat<LandblockInfo>(id | 0xFFFE);
                    if (info == null)
                        continue;

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

            return network;
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

        private Vector2 Flat(int node) => new Vector2(nodes[node].X, nodes[node].Y);

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

            // along the road in the direction we face; a road across us goes the way we face most
            var toB = Vector2.Normalize(Flat(b) - Flat(a));
            var (previous, current) = Vector2.Dot(toB, direction) >= 0 ? (a, b) : (b, a);

            var points = new List<Vector3> { entry, nodes[current] };
            var visited = new HashSet<int> { previous, current };
            var toCurrent = Flat(current) - new Vector2(entry.X, entry.Y);
            var length = toCurrent.Length();
            var heading = length > 0.01f ? toCurrent / length : (current == b ? toB : -toB);
            var continues = true;

            while (length < maxLength)
            {
                var best = -1;
                var bestCos = MaxTurnCos;
                var bestDistance = 0f;

                foreach (var next in links[current])
                {
                    if (visited.Contains(next))
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

            return new RoadTrace { Points = DropCollinear(points), Direction = heading, Continues = continues };
        }

        /// <summary>
        /// The point of the road nearest to the position, on a link between two nodes.
        /// </summary>
        private bool TryFindNearestRoadPoint(Vector3 position, float maxApproach, out int a, out int b, out Vector3 point)
        {
            a = b = -1;
            point = default;

            var p = new Vector2(position.X, position.Y);
            var closest = maxApproach;

            for (var i = 0; i < nodes.Count; i++)
            {
                foreach (var j in links[i])
                {
                    if (j < i)
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
