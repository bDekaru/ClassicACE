using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast.Geom;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using System;
using System.Collections.Generic;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Finds the ledge drops of a mesh: the ledges an agent can drop down from, as one-way off-mesh connections from
    /// the edge of the upper floor to the floor below. Recast splits floors at any step taller than the agent's max
    /// climb, so without these a creature on a balcony walks around to the stairs, or can't reach the floor below at
    /// all.
    ///
    /// A ledge drop runs from a point just inside a boundary edge of the mesh, straight out over the edge, to the floor
    /// where the agent comes down. Creatures walk off a ledge drop with EdgeSlide cleared (see Creature.ContinueRoute),
    /// so every one leaves it at walking speed. The mesh reaches the edge of a deep drop to within about a cell and
    /// keeps the agent radius from a shallow one (see ObstacleErosion), so the real edge is found in the geometry. Past
    /// it, a creature that steps the drop down (its StepDownHeight) comes straight down once clear of the ledge's face,
    /// a radius out; one that can't leaves the ground between the edge and a radius past it, as its foot sphere rolls
    /// off, and falls with no control in the air (see FallDistance). The lower floor's mesh keeps the radius from the
    /// ledge's face, so for a sheer ledge it starts about a radius out. A ledge drop is only kept if the geometry agrees
    /// for every one of them: nothing in the way of the agent's body along its walk to the edge and its fall (walls,
    /// railings, ceilings), and the first surface below every touchdown is the landing's floor (not a slope, crate or
    /// ledge in between).
    /// </summary>
    public static class LedgeDropFinder
    {
        /// <summary>
        /// Bump when the ledge drops or drop edges found from the same mesh change, so cached meshes are rebuilt.
        /// </summary>
        public const int Version = 8; // 2: clearance tested up the whole body and along the path, 3: clearance from the agent's climb up, 4: landing tested from there down, 5: tested along a walking agent's fall, 6: the mesh reaches the ledge, 7: the real edge found, step-downs and the whole touchdown range tested, 8: drop edges flagged

        /// <summary>
        /// The highest ledge drop a creature takes, from floor to floor.
        /// </summary>
        public const float MaxLedgeDropHeight = 6.0f;

        /// <summary>
        /// The spacing of the points tested along each boundary edge.
        /// </summary>
        private const float SampleSpacing = 0.75f;

        /// <summary>
        /// Ledge drops whose starts are closer together than this (and at about the same height) count as one.
        /// </summary>
        private const float LinkSpacing = 2.0f;

        /// <summary>
        /// How far past the lower floor's eroded edge the landing is looked for.
        /// </summary>
        private const float LandingOvershoot = 0.3f;

        /// <summary>
        /// How far (horizontally) the landing may be from where the agent is expected to come down.
        /// </summary>
        private const float LandingSearchRadius = 0.75f;

        /// <summary>
        /// How far the first surface hit below the landing may be from the landing's mesh height (the detail mesh only
        /// approximates the floor).
        /// </summary>
        private const float SurfaceTolerance = 0.5f;

        /// <summary>
        /// How far inside the boundary edge a ledge drop starts, so the start is on its poly.
        /// </summary>
        private const float StartInset = 0.05f;

        /// <summary>
        /// The spacing of the floor tests that find the real edge past a ledge drop's start (see TryFindEdge), and how
        /// much further than the agent radius they look.
        /// </summary>
        private const float EdgeProbeStep = 0.05f;
        private const float EdgeSearchMargin = 0.5f;

        /// <summary>
        /// The spacing of the touchdowns tested between the nearest and farthest one.
        /// </summary>
        private const float TouchdownSpacing = 0.5f;

        /// <summary>
        /// How far below the agent's height the highest clearance test is, so a surface the agent just fits under
        /// doesn't count as in the way.
        /// </summary>
        private const float ClearanceTopMargin = 0.2f;

        /// <summary>
        /// The spacing of the horizontal clearance tests up the agent's body.
        /// </summary>
        private const float ClearanceHeightSpacing = 0.4f;

        /// <summary>
        /// The spacing of the vertical clearance tests along a ledge drop's path.
        /// </summary>
        private const float ClearancePathSpacing = 0.25f;

        /// <summary>
        /// How far inside the tile's max x and z edges a landing must be.
        /// </summary>
        private const float TileEdgeMargin = 0.05f;

        private const int MaxLandingCandidates = 32;

        /// <summary>
        /// Finds the ledge drops of a single tile mesh, built from geom (in the same coordinates).
        /// </summary>
        /// <param name="agentMaxStepDown">The deepest drop the agent steps down instead of falling</param>
        public static List<RcOffMeshConnection> Find(DtMeshData data, IRcInputGeomProvider geom, float agentRadius, float agentHeight, float agentMaxClimb, float agentMaxStepDown)
        {
            var ledgeDrops = new List<RcOffMeshConnection>();

            if (!TryCreateQuery(data, out var query, out var baseRef))
                return ledgeDrops;

            var filter = new DtQueryDefaultFilter();

            var minLedgeDrop = agentMaxClimb;
            // a lip lower than the agent's climb is stepped over on the way off the ledge; a higher one stops it
            var clearanceBottom = agentMaxClimb;
            var clearanceTop = Math.Max(clearanceBottom, agentHeight - ClearanceTopMargin);
            var landingCandidates = new long[MaxLandingCandidates];
            var starts = new List<RcVec3f>();

            foreach (var (polyIndex, _, start, normal) in GetEdgeSamples(data, query, baseRef))
            {
                if (IsNearAccepted(starts, start))
                    continue;

                var polyRef = baseRef | (long)polyIndex;

                if (!TryFindEdge(geom, start, normal, agentMaxClimb, agentRadius + EdgeSearchMargin, out var ledgeEdge))
                    continue;

                // the floor below, just past the lower floor's eroded edge
                var landingDistance = ledgeEdge + agentRadius + LandingOvershoot;
                if (!TryFindLanding(query, filter, landingCandidates, polyRef, start, Along(start, normal, landingDistance), minLedgeDrop, data.header.bmax, out var landing))
                    continue;

                // Where the agent comes down, from the start: a radius past the edge if it steps the drop down,
                // else FallDistance past where it leaves the ground, between the edge and a radius past it
                var dropHeight = start.Y - landing.Y;
                var stepsDown = dropHeight <= agentMaxStepDown;
                var fallDistance = FallDistance(dropHeight);
                var nearest = ledgeEdge + (stepsDown ? agentRadius : Math.Max(agentRadius, fallDistance));
                var farthest = ledgeEdge + agentRadius + fallDistance;

                // Further out than the floor found above: the landing is there, and must be the same floor.
                if (farthest > landingDistance)
                {
                    if (!TryFindLanding(query, filter, landingCandidates, polyRef, start, Along(start, normal, farthest), minLedgeDrop, data.header.bmax, out var farLanding) ||
                        Math.Abs(farLanding.Y - landing.Y) > SurfaceTolerance)
                        continue;

                    landing = farLanding;
                }

                // falling from the edge and from a radius past it, and coming straight down by the face
                var stepDownAt = Along(start, normal, nearest);
                if (!HasFallClearance(geom, start, normal, ledgeEdge, farthest, landing.Y, clearanceBottom, clearanceTop) ||
                    !HasFallClearance(geom, start, normal, ledgeEdge + agentRadius, farthest, landing.Y, clearanceBottom, clearanceTop) ||
                    stepsDown && SegmentHitsGeometry(geom, Offset(stepDownAt, clearanceTop), new RcVec3f(stepDownAt.X, landing.Y + clearanceBottom, stepDownAt.Z)))
                    continue;

                if (!LandsBetween(geom, start, normal, nearest, farthest, landing.Y, landing.Y + clearanceBottom, agentRadius))
                    continue;

                starts.Add(start);
                ledgeDrops.Add(new RcOffMeshConnection(start, landing, agentRadius * 0.5f, false,
                    NavArea.LedgeDrop, NavPolyFlags.LedgeDrop));
            }

            return ledgeDrops;
        }

        /// <summary>
        /// Flags the boundary edges of a single tile mesh that are drops (see NavPolyFlags.DropEdge): somewhere along
        /// them the floor ends within the erosion's reach, with nothing in the way of the agent's body before that. The
        /// others have a wall, or anything else, past them.
        /// </summary>
        public static void MarkDropEdges(DtMeshData data, IRcInputGeomProvider geom, float agentRadius, float agentHeight, float agentMaxClimb)
        {
            if (!TryCreateQuery(data, out var query, out var baseRef))
                return;

            var reach = agentRadius + EdgeSearchMargin;
            var top = Math.Max(agentMaxClimb, agentHeight - ClearanceTopMargin);

            foreach (var (polyIndex, edge, start, normal) in GetEdgeSamples(data, query, baseRef))
            {
                var poly = data.polys[polyIndex];
                var flag = NavPolyFlags.DropEdge << edge;
                if ((poly.flags & flag) == 0 && IsDropAt(geom, start, normal, agentMaxClimb, top, reach))
                    poly.flags |= flag;
            }
        }

        private static bool TryCreateQuery(DtMeshData data, out DtNavMeshQuery query, out long baseRef)
        {
            query = null;
            baseRef = 0;

            if (data?.header == null || data.header.polyCount == 0)
                return false;

            var mesh = Pathfinder.CreateTileMesh(data);
            var tile = mesh?.GetTile(0);
            if (tile?.data == null)
                return false;

            query = new DtNavMeshQuery(mesh);
            baseRef = mesh.GetPolyRefBase(tile);
            return true;
        }

        /// <summary>
        /// Points to probe along the boundary edges of the ground polys, SampleSpacing apart, each with its edge's
        /// normal out of the poly.
        /// </summary>
        private static IEnumerable<(int polyIndex, int edge, RcVec3f start, RcVec3f normal)> GetEdgeSamples(DtMeshData data, DtNavMeshQuery query, long baseRef)
        {
            var verts = data.verts;

            for (var i = 0; i < data.header.polyCount; i++)
            {
                var poly = data.polys[i];
                if (poly.GetPolyType() != DtPolyTypes.DT_POLYTYPE_GROUND)
                    continue;

                var centroid = GetCentroid(verts, poly);

                for (var j = 0; j < poly.vertCount; j++)
                {
                    if (!TryGetBoundaryEdge(verts, poly, j, centroid, out var a, out var b, out var edgeLength, out var normal))
                        continue;

                    var samples = Math.Max(1, (int)MathF.Ceiling(edgeLength / SampleSpacing));
                    for (var k = 0; k < samples; k++)
                    {
                        if (TryGetStart(query, baseRef | (long)i, a, b, (k + 0.5f) / samples, normal, out var start))
                            yield return (i, j, start, normal);
                    }
                }
            }
        }

        private static bool IsDropAt(IRcInputGeomProvider geom, RcVec3f start, RcVec3f normal, float climb, float top, float reach)
        {
            // the floor must end short of whatever is in the way of the body, from a lip too high to step over up
            var floorReach = reach;
            for (var height = climb; height <= top; height += ClearanceHeightSpacing)
            {
                if (RaycastGeometry(geom, Offset(start, height), Offset(Along(start, normal, reach), height), out var t))
                    floorReach = Math.Min(floorReach, t * reach - EdgeProbeStep);
            }

            return TryFindEdge(geom, start, normal, climb, floorReach, out _);
        }

        /// <summary>
        /// A poly's edge, if it is on the mesh boundary (no neighbor in the tile, and no portal to another tile), with
        /// its horizontal normal pointing out of the poly.
        /// </summary>
        private static bool TryGetBoundaryEdge(float[] verts, DtPoly poly, int index, RcVec3f centroid, out RcVec3f a, out RcVec3f b, out float length, out RcVec3f normal)
        {
            a = GetVertex(verts, poly.verts[index]);
            b = GetVertex(verts, poly.verts[(index + 1) % poly.vertCount]);
            normal = default;

            var x = b.X - a.X;
            var z = b.Z - a.Z;
            length = MathF.Sqrt(x * x + z * z);
            if (poly.neis[index] != 0 || length < 0.01f)
                return false;

            normal = new RcVec3f(z / length, 0f, -x / length);
            if (normal.X * ((a.X + b.X) * 0.5f - centroid.X) + normal.Z * ((a.Z + b.Z) * 0.5f - centroid.Z) < 0f)
                normal = new RcVec3f(-normal.X, 0f, -normal.Z);

            return true;
        }

        /// <summary>
        /// The point StartInset inside a boundary edge, fraction f along it, on the poly's surface.
        /// </summary>
        private static bool TryGetStart(DtNavMeshQuery query, long polyRef, RcVec3f a, RcVec3f b, float f, RcVec3f normal, out RcVec3f start)
        {
            var onEdge = new RcVec3f(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f);
            start = new RcVec3f(onEdge.X - normal.X * StartInset, onEdge.Y, onEdge.Z - normal.Z * StartInset);

            if (query.GetPolyHeight(polyRef, start, out var height).Succeeded())
                start.Y = height;
            else if (query.ClosestPointOnPoly(polyRef, start, out var closest, out _).Succeeded())
                start = closest;
            else
                return false;

            return true;
        }

        /// <summary>
        /// Finds the floor the agent lands on below the probe point: the highest poly under it (within
        /// LandingSearchRadius) that is between minLedgeDrop and MaxLedgeDropHeight below the start.
        /// </summary>
        private static bool TryFindLanding(DtNavMeshQuery query, IDtQueryFilter filter, long[] candidates, long startRef, RcVec3f start, RcVec3f probe, float minLedgeDrop, RcVec3f tileMax, out RcVec3f landing)
        {
            landing = default;

            var halfHeight = (MaxLedgeDropHeight - minLedgeDrop) * 0.5f;
            var center = new RcVec3f(probe.X, start.Y - minLedgeDrop - halfHeight, probe.Z);
            var halfExtents = new RcVec3f(LandingSearchRadius, halfHeight, LandingSearchRadius);

            if (query.QueryPolygons(center, halfExtents, filter, candidates, out var count, candidates.Length).Failed())
                return false;

            var found = false;
            for (var i = 0; i < count; i++)
            {
                if (candidates[i] == startRef)
                    continue;

                if (query.ClosestPointOnPoly(candidates[i], probe, out var point, out _).Failed())
                    continue;

                var dx = point.X - probe.X;
                var dz = point.Z - probe.Z;
                if (dx * dx + dz * dz > LandingSearchRadius * LandingSearchRadius)
                    continue;

                var dropHeight = start.Y - point.Y;
                if (dropHeight < minLedgeDrop || dropHeight > MaxLedgeDropHeight)
                    continue;

                // Detour puts a point on the tile's max x or z edge in the next tile, so its end would not link here
                if (point.X > tileMax.X - TileEdgeMargin || point.Z > tileMax.Z - TileEdgeMargin)
                    continue;

                // the first floor below is the one landed on
                if (!found || point.Y > landing.Y)
                {
                    landing = point;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// How far out (horizontally) an agent that walks off a ledge comes down, from where it leaves the ground: it
        /// keeps the walking speed it had (MotionInterp.LeaveGround), with no control in the air and no drag, so it
        /// falls a plain arc under gravity.
        /// </summary>
        private static float FallDistance(float dropHeight)
        {
            return MotionInterp.WalkAnimSpeed * MathF.Sqrt(2f * dropHeight / -PhysicsGlobals.Gravity);
        }

        /// <summary>
        /// The height of a walking agent's feet when it is horizontally this far out from the ledge drop's start: at the
        /// start's height until it leaves the ground, leave out, then falling (see FallDistance), down to the landing's.
        /// </summary>
        private static float FallHeight(float startY, float landingY, float leave, float distance)
        {
            var time = Math.Max(0f, distance - leave) / MotionInterp.WalkAnimSpeed;
            return Math.Max(landingY, startY + 0.5f * PhysicsGlobals.Gravity * time * time);
        }

        /// <summary>
        /// Finds how far out from the ledge drop's start the floor under it ends, following it in EdgeProbeStep steps,
        /// each within the agent's climb of the last: 0 if there is no floor under the start itself (the mesh can run
        /// past the edge by about a cell). False if the floor goes on past maxDistance.
        /// </summary>
        private static bool TryFindEdge(IRcInputGeomProvider geom, RcVec3f start, RcVec3f normal, float climb, float maxDistance, out float edge)
        {
            edge = 0f;

            // the detail mesh only approximates the floor under the start
            var floorY = start.Y;
            var below = climb + SurfaceTolerance;

            for (var distance = 0f; distance <= maxDistance; distance += EdgeProbeStep)
            {
                var at = Along(start, normal, distance);
                var from = new RcVec3f(at.X, floorY + climb, at.Z);
                var to = new RcVec3f(at.X, floorY - below, at.Z);
                if (!RaycastGeometry(geom, from, to, out var t))
                    return true;

                edge = distance;
                floorY = from.Y + (to.Y - from.Y) * t;
                below = climb;
            }

            return false;
        }

        /// <summary>
        /// True if the agent comes down on the landing floor wherever it touches down from nearest to farthest out from
        /// the ledge drop's start: at its center and at either side of its body (see FallsOnto).
        /// </summary>
        private static bool LandsBetween(IRcInputGeomProvider geom, RcVec3f start, RcVec3f normal, float nearest, float farthest, float landingY, float fallFrom, float agentRadius)
        {
            var side = new RcVec3f(-normal.Z * agentRadius * 0.6f, 0f, normal.X * agentRadius * 0.6f);
            var samples = Math.Max(1, (int)MathF.Ceiling((farthest - nearest) / TouchdownSpacing));

            for (var i = 0; i <= samples; i++)
            {
                var touchdown = Along(start, normal, nearest + (farthest - nearest) * i / samples);
                if (!FallsOnto(geom, touchdown, fallFrom, landingY) ||
                    !FallsOnto(geom, new RcVec3f(touchdown.X + side.X, touchdown.Y, touchdown.Z + side.Z), fallFrom, landingY) ||
                    !FallsOnto(geom, new RcVec3f(touchdown.X - side.X, touchdown.Y, touchdown.Z - side.Z), fallFrom, landingY))
                    return false;
            }

            return true;
        }

        private static RcVec3f Along(RcVec3f start, RcVec3f normal, float distance) =>
            new RcVec3f(start.X + normal.X * distance, start.Y, start.Z + normal.Z * distance);

        /// <summary>
        /// True if something falling straight down from fromY at this point first hits a surface at the landing height.
        /// fromY is where the fall clearance test (see HasFallClearance) ends at the touchdown, the agent's climb above
        /// the landing: anything higher is in the agent's way, and anything lower is what it comes down on.
        /// </summary>
        private static bool FallsOnto(IRcInputGeomProvider geom, RcVec3f at, float fromY, float landingY)
        {
            var from = new RcVec3f(at.X, fromY, at.Z);
            var to = new RcVec3f(at.X, landingY - SurfaceTolerance * 2f, at.Z);

            if (!RaycastGeometry(geom, from, to, out var t))
                return false; // nothing to land on

            var hitY = from.Y + (to.Y - from.Y) * t;
            return Math.Abs(hitY - landingY) <= SurfaceTolerance;
        }

        /// <summary>
        /// True if nothing is in the way of the agent's body as it walks from the ledge drop's start, leaves the ground
        /// leave out and falls to the touchdown (touchdownDistance out), from bottom (its climb: a lower lip is
        /// stepped over) up to top above its feet (see FallHeight). Between points along the way, tests at heights up the
        /// body catch walls and railings across the path; at each point, a vertical test catches a ceiling or overhang
        /// above it. Tests at fixed heights alone miss a ceiling edge that lies between two of them (e.g. landblock
        /// 0xCE95, where a ledge drop off the side of a stair ran under the ground floor's ceiling).
        /// </summary>
        private static bool HasFallClearance(IRcInputGeomProvider geom, RcVec3f start, RcVec3f normal, float leave, float touchdownDistance, float landingY, float bottom, float top)
        {
            var samples = Math.Max(1, (int)MathF.Ceiling(touchdownDistance / ClearancePathSpacing));
            var previous = start;

            for (var i = 0; i <= samples; i++)
            {
                var distance = touchdownDistance * i / samples;
                var at = Along(start, normal, distance);
                at.Y = FallHeight(start.Y, landingY, leave, distance);

                if (SegmentHitsGeometry(geom, Offset(at, bottom), Offset(at, top)))
                    return false;

                if (i > 0)
                {
                    for (var height = bottom; height < top; height += ClearanceHeightSpacing)
                    {
                        if (SegmentHitsGeometry(geom, Offset(previous, height), Offset(at, height)))
                            return false;
                    }

                    if (SegmentHitsGeometry(geom, Offset(previous, top), Offset(at, top)))
                        return false;
                }

                previous = at;
            }

            return true;
        }

        private static bool SegmentHitsGeometry(IRcInputGeomProvider geom, RcVec3f from, RcVec3f to)
        {
            return RaycastGeometry(geom, from, to, out _);
        }

        /// <summary>
        /// The nearest hit of a segment with the geometry's triangles, from either side, as a fraction of the segment.
        /// </summary>
        private static bool RaycastGeometry(IRcInputGeomProvider geom, RcVec3f from, RcVec3f to, out float nearest)
        {
            nearest = float.MaxValue;

            var rectMin = new RcVec2f(Math.Min(from.X, to.X), Math.Min(from.Z, to.Z));
            var rectMax = new RcVec2f(Math.Max(from.X, to.X), Math.Max(from.Z, to.Z));

            foreach (var triMesh in geom.Meshes())
            {
                var meshVerts = triMesh.GetVerts();
                var chunks = triMesh.GetChunksOverlappingRect(rectMin, rectMax);

                foreach (var chunk in chunks)
                {
                    var tris = chunk.tris;
                    for (var j = 0; j + 2 < tris.Length; j += 3)
                    {
                        var v0 = GetVertex(meshVerts, tris[j]);
                        var v1 = GetVertex(meshVerts, tris[j + 1]);
                        var v2 = GetVertex(meshVerts, tris[j + 2]);

                        // most of a chunk is nowhere near the segment, and this is much cheaper than the test
                        if (IsApart(v0.X, v1.X, v2.X, from.X, to.X) || IsApart(v0.Y, v1.Y, v2.Y, from.Y, to.Y) || IsApart(v0.Z, v1.Z, v2.Z, from.Z, to.Z))
                            continue;

                        // the test is one-sided, and the geometry's winding is not consistent
                        if ((RcIntersections.IntersectSegmentTriangle(from, to, v0, v1, v2, out var t) ||
                             RcIntersections.IntersectSegmentTriangle(from, to, v0, v2, v1, out t)) && t < nearest)
                            nearest = t;
                    }
                }
            }

            return nearest <= 1f;
        }

        /// <summary>
        /// True if a triangle's and a segment's coordinates on one axis don't overlap.
        /// </summary>
        private static bool IsApart(float a, float b, float c, float from, float to) =>
            a < from && a < to && b < from && b < to && c < from && c < to ||
            a > from && a > to && b > from && b > to && c > from && c > to;

        private static bool IsNearAccepted(List<RcVec3f> starts, RcVec3f start)
        {
            foreach (var other in starts)
            {
                var dx = other.X - start.X;
                var dz = other.Z - start.Z;
                if (dx * dx + dz * dz < LinkSpacing * LinkSpacing && Math.Abs(other.Y - start.Y) < 1f)
                    return true;
            }
            return false;
        }

        private static RcVec3f Offset(RcVec3f point, float height) => new RcVec3f(point.X, point.Y + height, point.Z);

        internal static RcVec3f GetVertex(float[] verts, int index) => new RcVec3f(verts[index * 3], verts[index * 3 + 1], verts[index * 3 + 2]);

        private static RcVec3f GetCentroid(float[] verts, DtPoly poly)
        {
            var x = 0f;
            var z = 0f;
            for (var v = 0; v < poly.vertCount; v++)
            {
                x += verts[poly.verts[v] * 3];
                z += verts[poly.verts[v] * 3 + 2];
            }
            return new RcVec3f(x / poly.vertCount, 0f, z / poly.vertCount);
        }
    }
}
