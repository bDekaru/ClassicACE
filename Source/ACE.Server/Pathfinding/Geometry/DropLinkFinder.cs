using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast.Geom;
using System;
using System.Collections.Generic;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Finds the ledges an agent can drop down from, as one-way off-mesh connections from the edge of the upper floor
    /// to the floor below. Recast splits floors at any step taller than the agent's max climb, so without these a
    /// creature on a balcony walks around to the stairs, or can't reach the floor below at all.
    ///
    /// A drop runs from a point just inside a boundary edge of the mesh, straight out over the edge, to the floor a
    /// little past where the agent comes down. The mesh is eroded by the agent radius on both floors, so for a sheer
    /// ledge the lower floor's mesh starts about two radii out from the upper floor's edge. A drop is only kept if the
    /// geometry agrees: nothing in the way at body height between the edge and the landing (walls, railings), and the
    /// first surface below the landing is that floor (not a slope, crate or ledge in between).
    /// </summary>
    public static class DropLinkFinder
    {
        /// <summary>
        /// Bump when the drops found from the same mesh change, so cached meshes are rebuilt.
        /// </summary>
        public const int Version = 1;

        /// <summary>
        /// The highest drop a creature takes, from floor to floor.
        /// </summary>
        public const float MaxDropHeight = 6.0f;

        /// <summary>
        /// The spacing of the points tested along each boundary edge.
        /// </summary>
        private const float SampleSpacing = 0.75f;

        /// <summary>
        /// Drops whose starts are closer together than this (and at about the same height) count as one.
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
        /// How far inside the boundary edge a drop starts, so the start is on its poly.
        /// </summary>
        private const float StartInset = 0.05f;

        /// <summary>
        /// The height above the upper floor of the lowest clearance test: a lip lower than this is stepped over.
        /// </summary>
        private const float LowClearanceHeight = 0.5f;

        private const int MaxLandingCandidates = 32;

        /// <summary>
        /// Finds the drops of a single tile mesh, built from geom (in the same coordinates).
        /// </summary>
        public static List<RcOffMeshConnection> Find(DtMeshData data, IRcInputGeomProvider geom, int maxVertsPerPoly, float agentRadius, float agentHeight, float agentMaxClimb)
        {
            var drops = new List<RcOffMeshConnection>();

            if (data?.header == null || data.header.polyCount == 0)
                return drops;

            var mesh = new DtNavMesh();
            if (mesh.Init(data, maxVertsPerPoly, 0).Failed())
                return drops;

            var tile = mesh.GetTile(0);
            if (tile?.data == null)
                return drops;

            var query = new DtNavMeshQuery(mesh);
            var filter = new DtQueryDefaultFilter();
            var baseRef = mesh.GetPolyRefBase(tile);

            var minDrop = agentMaxClimb;
            var landingDistance = agentRadius * 2f + LandingOvershoot;
            var highClearanceHeight = Math.Max(LowClearanceHeight, Math.Min(agentHeight - 0.2f, 1.6f));
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

                        var probe = new RcVec3f(start.X + normal.X * landingDistance, start.Y, start.Z + normal.Z * landingDistance);

                        if (!TryFindLanding(query, filter, landingCandidates, polyRef, start, probe, minDrop, out var landing))
                            continue;

                        // nothing in the way at body height between the edge and over the landing
                        if (SegmentHitsGeometry(geom, Offset(start, LowClearanceHeight), Offset(probe, LowClearanceHeight)) ||
                            SegmentHitsGeometry(geom, Offset(start, highClearanceHeight), Offset(probe, highClearanceHeight)))
                            continue;

                        // the agent comes down on the landing floor, at its center and at either side of its body
                        var side = new RcVec3f(-normal.Z * agentRadius * 0.6f, 0f, normal.X * agentRadius * 0.6f);
                        if (!FallsOnto(geom, probe, start.Y, landing.Y) ||
                            !FallsOnto(geom, new RcVec3f(probe.X + side.X, probe.Y, probe.Z + side.Z), start.Y, landing.Y) ||
                            !FallsOnto(geom, new RcVec3f(probe.X - side.X, probe.Y, probe.Z - side.Z), start.Y, landing.Y))
                            continue;

                        starts.Add(start);
                        drops.Add(new RcOffMeshConnection(start, landing, agentRadius * 0.5f, false,
                            NavArea.Drop, NavPolyFlags.Drop));
                    }
                }
            }

            return drops;
        }

        /// <summary>
        /// Finds the floor the agent lands on below the probe point: the highest poly under it (within
        /// LandingSearchRadius) that is between minDrop and MaxDropHeight below the start.
        /// </summary>
        private static bool TryFindLanding(DtNavMeshQuery query, IDtQueryFilter filter, long[] candidates, long startRef, RcVec3f start, RcVec3f probe, float minDrop, out RcVec3f landing)
        {
            landing = default;

            var halfHeight = (MaxDropHeight - minDrop) * 0.5f;
            var center = new RcVec3f(probe.X, start.Y - minDrop - halfHeight, probe.Z);
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

                var drop = start.Y - point.Y;
                if (drop < minDrop || drop > MaxDropHeight)
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
        /// True if something falling straight down from above the upper floor at this point first hits a surface at
        /// the landing height.
        /// </summary>
        private static bool FallsOnto(IRcInputGeomProvider geom, RcVec3f at, float upperY, float landingY)
        {
            var from = new RcVec3f(at.X, upperY + LowClearanceHeight, at.Z);
            var to = new RcVec3f(at.X, landingY - SurfaceTolerance * 2f, at.Z);

            if (!RaycastGeometry(geom, from, to, out var t))
                return false; // nothing to land on

            var hitY = from.Y + (to.Y - from.Y) * t;
            return Math.Abs(hitY - landingY) <= SurfaceTolerance;
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
