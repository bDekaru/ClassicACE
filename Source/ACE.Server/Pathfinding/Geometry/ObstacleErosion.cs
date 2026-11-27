using DotRecast.Core;
using DotRecast.Recast;
using System;
using System.Collections.Generic;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Recast's filtering and erosion, keeping the walkable area an agent radius from obstacles (walls, steps too high
    /// to climb, low ceilings) and from drops the agent steps down, but not from deeper drops. Recast's own erosion
    /// keeps it from every drop, which leaves nothing of a narrow ledge an agent can walk along.
    /// A creature walks down a drop no deeper than its step-down height, so one that strays past the edge of the mesh
    /// there ends up on the floor below; at a deeper drop EdgeSlide holds it at the edge (about half its radius past
    /// it). The mesh can only run to the edge of deep drops because of that: it is meant for creatures with EdgeSlide.
    /// Same as RcAreas.ErodeWalkableArea, except for what the distance is measured from: a span with a neighbor it can't
    /// walk to, where something solid rises above its floor and below the agent's head, in the next column or, past a
    /// gap, within the agent's radius, or where it steps down to a floor within AgentMaxStepDown.
    /// Keeping deep drops leaves floor on high wall tops too, which physics lets a creature walk on: floor with deep
    /// drops on both sides closer together than NarrowWidth is marked Narrow, so routes can price it.
    /// Run on the solid heightfield, so the build is then given a config with no filters and no erosion (see
    /// NavMeshBuilder).
    /// </summary>
    internal static class ObstacleErosion
    {
        /// <summary>
        /// Bump when the walkable area or the areas of the same geometry change, so cached meshes are rebuilt.
        /// </summary>
        public const int Version = 4; // 2: radius kept from drops within the step-down height, 3: grid edge is an obstacle, single-tile grids padded, 4: steep floor removed after the scan

        /// <summary>
        /// Floor narrower than about this (to within a cell) is Narrow.
        /// </summary>
        private const float NarrowWidth = 0.8f;

        /// <summary>
        /// Something this many voxels above a floor is still level with it: voxel rounding, or a gentle rise to an edge.
        /// </summary>
        private const int FlushVoxels = 1;

        // Recast's neighbor directions (-x, +z, +x, -z) and chamfer distance steps
        private static readonly int[] OffsetX = { -1, 0, 1, 0 };
        private static readonly int[] OffsetZ = { 0, 1, 0, -1 };
        private const int OrthogonalStep = 2;
        private const int DiagonalStep = 3;

        /// <summary>
        /// Applies the filters the settings ask for, then the erosion, then marks the narrow floor.
        /// </summary>
        /// <param name="config">The build's config, for the agent's height and climb in voxels</param>
        public static void FilterAndErode(RcContext context, RcHeightfield solid, RcConfig config, MeshBuildSettings settings)
        {
            var height = config.WalkableHeight;
            var climb = config.WalkableClimb;
            var radius = (int)MathF.Ceiling(settings.AgentRadius / settings.CellSize);
            var stepDown = (int)MathF.Ceiling(settings.AgentMaxStepDown / settings.CellHeight);

            // EdgeSlide holds a creature half its radius past the edge, so it steps down to a floor under that
            var stepDownReach = Math.Max(1, (radius + 1) / 2);

            if (settings.FilterLowHangingObstacles)
                RcFilters.FilterLowHangingWalkableObstacles(context, climb, solid);

            // every span of every column; the filters only change their areas
            var columns = solid.width * solid.height;
            var first = new int[columns + 1];
            var spans = new List<RcSpan>();
            for (var column = 0; column < columns; column++)
            {
                first[column] = spans.Count;
                for (var span = solid.spans[column]; span != null; span = span.next)
                    spans.Add(span);
            }
            first[columns] = spans.Count;

            // The floor the distances run over, with the steep spans removed below: those are not obstacles
            var floor = new bool[spans.Count];
            for (var i = 0; i < spans.Count; i++)
                floor[i] = spans[i].area != RcRecast.RC_NULL_AREA && Ceiling(spans[i]) - spans[i].smax >= height;

            if (settings.FilterWalkableLowHeightSpans)
                RcFilters.FilterWalkableLowHeightSpans(context, height, solid);

            // the neighbor each floor span walks to in each direction, -1 for none
            var neighbors = new int[spans.Count * 4];
            Array.Fill(neighbors, -1);

            var fromObstacle = new int[spans.Count];
            Array.Fill(fromObstacle, byte.MaxValue);

            var fromDrop = new int[spans.Count];
            Array.Fill(fromDrop, byte.MaxValue);

            // removed after the scan, so the step-down test sees the same floor in every direction
            var steep = new List<RcSpan>();

            for (var z = 0; z < solid.height; z++)
            {
                for (var x = 0; x < solid.width; x++)
                {
                    var column = x + z * solid.width;
                    for (var i = first[column]; i < first[column + 1]; i++)
                    {
                        if (!floor[i])
                            continue;

                        var y = spans[i].smax;
                        var ceiling = Ceiling(spans[i]);

                        // lowest and highest floor it can walk to
                        var low = y;
                        var high = y;

                        for (var direction = 0; direction < 4; direction++)
                        {
                            var nx = x + OffsetX[direction];
                            var nz = z + OffsetZ[direction];

                            // the edge of the grid is an obstacle, as in Recast (a tile's border, trimmed afterwards)
                            if (nx < 0 || nz < 0 || nx >= solid.width || nz >= solid.height)
                            {
                                fromObstacle[i] = 0;
                                continue;
                            }

                            var neighborColumn = nx + nz * solid.width;
                            for (var n = first[neighborColumn]; n < first[neighborColumn + 1]; n++)
                            {
                                if (Math.Abs(spans[n].smax - y) <= climb && Math.Min(ceiling, Ceiling(spans[n])) - Math.Max(y, spans[n].smax) >= height)
                                {
                                    low = Math.Min(low, spans[n].smax);
                                    high = Math.Max(high, spans[n].smax);
                                    if (floor[n])
                                        neighbors[i * 4 + direction] = n;
                                    break;
                                }
                            }

                            if (neighbors[i * 4 + direction] >= 0)
                                continue;

                            // an obstacle if something is in the way of the body right there, else a drop, which may have
                            // an obstacle across it within the radius
                            var isDrop = true;
                            for (var step = 1; step <= radius; step++, nx += OffsetX[direction], nz += OffsetZ[direction])
                            {
                                if (nx < 0 || nz < 0 || nx >= solid.width || nz >= solid.height)
                                    break;

                                // past a gap, only what is too high to step over counts
                                if (Blocks(solid.spans[nx + nz * solid.width], y + (step == 1 ? FlushVoxels : climb), y + height))
                                {
                                    fromObstacle[i] = Math.Min(fromObstacle[i], (step - 1) * OrthogonalStep);
                                    isDrop = step > 1;
                                    break;
                                }
                            }

                            // a drop it steps down is kept the radius from, as an obstacle
                            if (isDrop && IsStepDown(solid, x, z, direction, y, stepDown, stepDownReach))
                                fromObstacle[i] = 0;
                            else if (isDrop)
                                fromDrop[i] = 0;
                        }

                        // Only the steep slope half of Recast's ledge filter: the other half trims along every drop
                        if (settings.FilterLedgeSpans && high - low > climb)
                            steep.Add(spans[i]);
                    }
                }
            }

            foreach (var span in steep)
                span.area = RcRecast.RC_NULL_AREA;

            Chamfer(fromObstacle, neighbors);

            // Narrow floor has drops on both sides; along a wall it is not the top of anything. Wide floor is within half
            // the width (corners included) of a span at least that far from every drop.
            var halfWidth = ((int)MathF.Round(NarrowWidth / settings.CellSize) + 1) / 2;
            Chamfer(fromDrop, neighbors);

            var fromWide = new int[spans.Count];
            for (var i = 0; i < spans.Count; i++)
                fromWide[i] = fromDrop[i] >= halfWidth * OrthogonalStep ? 0 : byte.MaxValue;
            Chamfer(fromWide, neighbors);

            for (var i = 0; i < spans.Count; i++)
            {
                if (floor[i] && fromObstacle[i] < radius * OrthogonalStep)
                    spans[i].area = RcRecast.RC_NULL_AREA;
                else if (spans[i].area != RcRecast.RC_NULL_AREA && fromWide[i] > halfWidth * DiagonalStep)
                    spans[i].area = NavArea.Narrow;
            }
        }

        /// <summary>
        /// Recast's two chamfer passes: spreads the distances over the neighbor links. The spans are in z, x order.
        /// </summary>
        private static void Chamfer(int[] distance, int[] neighbors)
        {
            void Relax(int i, int direction, int diagonal)
            {
                var neighbor = neighbors[i * 4 + direction];
                if (neighbor < 0)
                    return;

                distance[i] = Math.Min(distance[i], distance[neighbor] + OrthogonalStep);

                var corner = neighbors[neighbor * 4 + diagonal];
                if (corner >= 0)
                    distance[i] = Math.Min(distance[i], distance[corner] + DiagonalStep);
            }

            for (var i = 0; i < distance.Length; i++)
            {
                Relax(i, 0, 3);
                Relax(i, 3, 2);
            }

            for (var i = distance.Length - 1; i >= 0; i--)
            {
                Relax(i, 2, 1);
                Relax(i, 1, 0);
            }
        }

        /// <summary>
        /// True if a column has something solid between the two heights.
        /// </summary>
        private static bool Blocks(RcSpan column, int bottom, int top)
        {
            for (var span = column; span != null; span = span.next)
                if (span.smax > bottom && span.smin < top)
                    return true;

            return false;
        }

        /// <summary>
        /// True if there is a walkable surface no more than stepDown below floor y in any of the reach columns past the
        /// span at (x, z) in the direction. Physics only steps down onto walkable floor (EdgeSlide holds a creature at
        /// the edge of a steeper face), and the face of the ledge itself, rasterized into the next column when it lies
        /// on a cell boundary, must not count.
        /// </summary>
        private static bool IsStepDown(RcHeightfield solid, int x, int z, int direction, int y, int stepDown, int reach)
        {
            for (var step = 1; step <= reach; step++)
            {
                var nx = x + OffsetX[direction] * step;
                var nz = z + OffsetZ[direction] * step;
                if (nx < 0 || nz < 0 || nx >= solid.width || nz >= solid.height)
                    return false;

                for (var span = solid.spans[nx + nz * solid.width]; span != null; span = span.next)
                    if (span.area != RcRecast.RC_NULL_AREA && span.smax <= y + FlushVoxels && span.smax >= y - stepDown)
                        return true;
            }
            return false;
        }

        private static int Ceiling(RcSpan span) => span.next?.smin ?? RcRecast.RC_SPAN_MAX_HEIGHT;
    }
}
