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
    /// where the agent comes down. Creatures walk off a ledge drop (see Creature.ContinueRoute), so every one leaves it
    /// at walking speed and falls the same arc: it has no control in the air (see FallDistance). The mesh is eroded by
    /// the agent radius on both floors, so for a sheer ledge the agent leaves the ground about a radius past the upper
    /// floor's mesh, and the lower floor's mesh starts about two radii out. A ledge drop is only kept if the geometry
    /// agrees: nothing in the way of the agent's body along its walk to the edge and its fall (walls, railings,
    /// ceilings), and the first surface below where it comes down is the landing's floor (not a slope, crate or ledge
    /// in between).
    /// </summary>
    public static class LedgeDropFinder
    {
        /// <summary>
        /// Bump when the ledge drops found from the same mesh change, so cached meshes are rebuilt.
        /// </summary>
        public const int Version = 5; // 2: clearance tested up the whole body and along the path, 3: clearance from the agent's climb up, 4: landing tested from there down, 5: tested along a walking agent's fall

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

        private const int MaxLandingCandidates = 32;

        /// <summary>
        /// Finds the ledge drops of a single tile mesh, built from geom (in the same coordinates).
        /// </summary>
        public static List<RcOffMeshConnection> Find(DtMeshData data, IRcInputGeomProvider geom, int maxVertsPerPoly, float agentRadius, float agentHeight, float agentMaxClimb)
        {
            var ledgeDrops = new List<RcOffMeshConnection>();

            if (data?.header == null || data.header.polyCount == 0)
                return ledgeDrops;

            var mesh = new DtNavMesh();
            if (mesh.Init(data, maxVertsPerPoly, 0).Failed())
                return ledgeDrops;

            var tile = mesh.GetTile(0);
            if (tile?.data == null)
                return ledgeDrops;

            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();
            var baseRef = mesh.GetPolyRefBase(tile);

            var minLedgeDrop = agentMaxClimb;
            var landingDistance = agentRadius * 2f + LandingOvershoot;
            // a lip lower than the agent's climb is stepped over on the way off the ledge; a higher one stops it
            var clearanceBottom = agentMaxClimb;
            var clearanceTop = Math.Max(clearanceBottom, agentHeight - ClearanceTopMargin);
            var landingCandidates = new long[MaxLandingCandidates];
            var starts = new List<RcVec3f>();

            var verts = data.verts;

            for (var i = 0; i < data.header.polyCount; i++)
            {
                var poly = data.polys[i];
                if (poly.GetPolyType() != DtPolyTypes.DT_POLYTYPE_GROUND)
                    continue;

                var polyRef = baseRef | (long)i;
                var centroid = GetCentroid(verts, poly);

                for (var j = 0; j < poly.vertCount; j++)
                {
                    // only the mesh boundary: no neighbor in this tile, and no portal to another tile
                    if (poly.neis[j] != 0)
                        continue;

                    var a = GetVertex(verts, poly.verts[j]);
                    var b = GetVertex(verts, poly.verts[(j + 1) % poly.vertCount]);

                    var edge = new RcVec3f(b.X - a.X, 0f, b.Z - a.Z);
                    var edgeLength = MathF.Sqrt(edge.X * edge.X + edge.Z * edge.Z);
                    if (edgeLength < 0.01f)
                        continue;

                    // the horizontal normal of the edge, pointing out of the poly
                    var normal = new RcVec3f(edge.Z / edgeLength, 0f, -edge.X / edgeLength);
                    var mid = new RcVec3f((a.X + b.X) * 0.5f, 0f, (a.Z + b.Z) * 0.5f);
                    if (normal.X * (mid.X - centroid.X) + normal.Z * (mid.Z - centroid.Z) < 0f)
                        normal = new RcVec3f(-normal.X, 0f, -normal.Z);

                    var samples = Math.Max(1, (int)MathF.Ceiling(edgeLength / SampleSpacing));
                    for (var k = 0; k < samples; k++)
                    {
                        var f = (k + 0.5f) / samples;
                        var onEdge = new RcVec3f(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f);
                        var start = new RcVec3f(onEdge.X - normal.X * StartInset, onEdge.Y, onEdge.Z - normal.Z * StartInset);

                        if (query.GetPolyHeight(polyRef, start, out var startHeight).Succeeded())
                            start.Y = startHeight;
                        else if (query.ClosestPointOnPoly(polyRef, start, out var closest, out _).Succeeded())
                            start = closest;
                        else
                            continue;

                        if (IsNearAccepted(starts, start))
                            continue;

                        // the floor below, just past the lower floor's eroded edge
                        if (!TryFindLanding(query, filter, landingCandidates, polyRef, start, Along(start, normal, landingDistance), minLedgeDrop, out var landing))
                            continue;

                        // Where a walking agent comes down: it leaves the ground at the ledge's edge, about a radius past
                        // the mesh's, and falls FallDistance further out, but no nearer the ledge's face than its radius.
                        var dropHeight = start.Y - landing.Y;
                        var touchdownDistance = Math.Max(agentRadius + FallDistance(dropHeight), agentRadius * 2f);

                        // Further out than the floor found above: the landing is there, and must be the same floor.
                        if (touchdownDistance > landingDistance)
                        {
                            if (!TryFindLanding(query, filter, landingCandidates, polyRef, start, Along(start, normal, touchdownDistance), minLedgeDrop, out var farLanding) ||
                                Math.Abs(farLanding.Y - landing.Y) > SurfaceTolerance)
                                continue;

                            landing = farLanding;
                        }

                        if (!HasFallClearance(geom, start, normal, agentRadius, touchdownDistance, landing.Y, clearanceBottom, clearanceTop))
                            continue;

                        // the agent comes down on the landing floor, at its center and at either side of its body
                        var touchdown = Along(start, normal, touchdownDistance);
                        var side = new RcVec3f(-normal.Z * agentRadius * 0.6f, 0f, normal.X * agentRadius * 0.6f);
                        var fallFrom = landing.Y + clearanceBottom;
                        if (!FallsOnto(geom, touchdown, fallFrom, landing.Y) ||
                            !FallsOnto(geom, new RcVec3f(touchdown.X + side.X, touchdown.Y, touchdown.Z + side.Z), fallFrom, landing.Y) ||
                            !FallsOnto(geom, new RcVec3f(touchdown.X - side.X, touchdown.Y, touchdown.Z - side.Z), fallFrom, landing.Y))
                            continue;

                        starts.Add(start);
                        ledgeDrops.Add(new RcOffMeshConnection(start, landing, agentRadius * 0.5f, false,
                            NavArea.LedgeDrop, NavPolyFlags.LedgeDrop));
                    }
                }
            }

            return ledgeDrops;
        }

        /// <summary>
        /// Finds the floor the agent lands on below the probe point: the highest poly under it (within
        /// LandingSearchRadius) that is between minLedgeDrop and MaxLedgeDropHeight below the start.
        /// </summary>
        private static bool TryFindLanding(DtNavMeshQuery query, IDtQueryFilter filter, long[] candidates, long startRef, RcVec3f start, RcVec3f probe, float minLedgeDrop, out RcVec3f landing)
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
        /// start's height until it leaves the ground at edgeDistance, then falling (see FallDistance), down to the
        /// landing's.
        /// </summary>
        private static float FallHeight(float startY, float landingY, float edgeDistance, float distance)
        {
            if (distance <= edgeDistance)
                return startY;

            var time = (distance - edgeDistance) / MotionInterp.WalkAnimSpeed;
            return Math.Max(landingY, startY + 0.5f * PhysicsGlobals.Gravity * time * time);
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
        /// True if nothing is in the way of the agent's body as it walks from the ledge drop's start to the edge
        /// (edgeDistance out) and falls to the touchdown (touchdownDistance out), from bottom (its climb: a lower lip is
        /// stepped over) up to top above its feet (see FallHeight). Between points along the way, tests at heights up the
        /// body catch walls and railings across the path; at each point, a vertical test catches a ceiling or overhang
        /// above it. Tests at fixed heights alone miss a ceiling edge that lies between two of them (e.g. landblock
        /// 0xCE95, where a ledge drop off the side of a stair ran under the ground floor's ceiling).
        /// </summary>
        private static bool HasFallClearance(IRcInputGeomProvider geom, RcVec3f start, RcVec3f normal, float edgeDistance, float touchdownDistance, float landingY, float bottom, float top)
        {
            var samples = Math.Max(1, (int)MathF.Ceiling(touchdownDistance / ClearancePathSpacing));
            var previous = start;

            for (var i = 0; i <= samples; i++)
            {
                var distance = touchdownDistance * i / samples;
                var at = Along(start, normal, distance);
                at.Y = FallHeight(start.Y, landingY, edgeDistance, distance);

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

                        // the test is one-sided, and the geometry's winding is not consistent
                        if ((RcIntersections.IntersectSegmentTriangle(from, to, v0, v1, v2, out var t) ||
                             RcIntersections.IntersectSegmentTriangle(from, to, v0, v2, v1, out t)) && t < nearest)
                            nearest = t;
                    }
                }
            }

            return nearest <= 1f;
        }

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

        private static RcVec3f GetVertex(float[] verts, int index) => new RcVec3f(verts[index * 3], verts[index * 3 + 1], verts[index * 3 + 2]);

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
