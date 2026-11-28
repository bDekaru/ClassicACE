//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Server.Pathfinding.Geometry;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The parts of a mesh nothing can walk to (islands), and the openings each agent size can get through.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Outdoor tile components with a vertex this close to the tile edge are not islands: they may continue into
        /// the neighboring tile. Tiles are built with a border, so the walkable area reaches the edge exactly.
        /// </summary>
        private const float TileEdgeTolerance = 0.25f;

        /// <summary>
        /// How far around an opening to look for the mesh poly that anchors its component (openings are not on the
        /// eroded mesh themselves).
        /// </summary>
        private static readonly RcVec3f OpeningSeedHalfExtents = new RcVec3f(2.0f, 2.0f, 2.0f);

        /// <summary>
        /// How far the mesh in front of an opening may be below or above its threshold for the opening to lead onto it.
        /// Measured at the outside point, a small ramp down from a doorway is ~0.7 m below, while the ground under a
        /// window is 1 m or more below the sill.
        /// </summary>
        private const float OpeningSeedMaxDrop = 0.85f;

        /// <summary>
        /// The poly an opening leads onto, around a point just outside it at threshold height (Recast space), or 0.
        /// </summary>
        private static long FindOpeningSeedPoly(DtNavMeshQuery query, RcVec3f seed)
        {
            var status = query.FindNearestPoly(seed, OpeningSeedHalfExtents, AllPolysFilter, out var polyRef, out var point, out _);
            return status.Succeeded() && Math.Abs(point.Y - seed.Y) <= OpeningSeedMaxDrop ? polyRef : 0;
        }

        private static readonly RcVec3f[] NoSeeds = Array.Empty<RcVec3f>();

        /// <summary>
        /// The opening polys of reachable opening aprons.
        /// </summary>
        private static readonly DtQueryDefaultFilter PassableOpeningFilter = new DtQueryDefaultFilter(NavPolyFlags.Opening, NavPolyFlags.Island, Array.Empty<float>());

        /// <summary>
        /// The opening polys of all opening aprons, on a mesh whose islands aren't marked.
        /// </summary>
        private static readonly DtQueryDefaultFilter OpeningFilter = new DtQueryDefaultFilter(NavPolyFlags.Opening, 0, Array.Empty<float>());

        /// <summary>
        /// How far around the center of an opening apron to look for its opening polys, on the mesh of an agent size:
        /// they reach across the apron, or end the radius short of its outer edge over a floor the agent steps down to
        /// (see OpeningApron); vertically they are on the threshold.
        /// </summary>
        private static RcVec3f GetOpeningPolyHalfExtents(AgentSize agentSize)
        {
            var horizontal = OpeningApron.GetDepth(agentSize) * 0.5f + 0.25f;
            return new RcVec3f(horizontal, 0.6f, horizontal);
        }

        /// <summary>
        /// Flags the islands of a mesh with a single tile, before it is published: connected components of polys that
        /// cannot be walked to. A component is kept (not an island) if it is the largest one, holds one of the seed
        /// points (openings), has floor connected to an opening apron (see OpeningApron), or, for an outdoor tile,
        /// reaches the tile edge where it may continue into the neighbor. The rest are tabletops, the tops of crates and
        /// rocks, pits you can only drop into, opening aprons that can't be reached (windows) and the like. Sizes are
        /// compared without the opening aprons, and a component that is only an opening apron is never kept. Must run
        /// before the tile is published, since it changes poly flags.
        /// </summary>
        /// <param name="seeds">Points known to be reachable, on the mesh (Recast space). Interior meshes have none: their
        /// openings have opening aprons, which keep their component by themselves when it can be reached.</param>
        private static void MarkIslands(DtNavMesh mesh, IEnumerable<RcVec3f> seeds, bool outdoor)
        {
            var tile = mesh.GetMaxTiles() > 0 ? mesh.GetTile(0) : null;
            var data = tile?.data;
            if (data?.header == null || data.polys == null || data.header.polyCount == 0)
                return;

            var polyCount = data.header.polyCount;

            var (component, componentArea, componentHasOpening) = FindComponents(mesh, tile);
            var ledgeDrops = FindLedgeDrops(mesh, tile);

            // all connected: everything is kept, and only a ledge drop that isn't attached to the floor on both ends leads nowhere
            var keep = componentArea.Count <= 1 ? Enumerable.Repeat(true, componentArea.Count).ToArray() : GetKeptComponents();
            FlagIslands(keep);

            // the components that can be walked to, of several
            bool[] GetKeptComponents()
            {
                var kept = new bool[componentArea.Count];

                var largest = 0;
                for (var c = 1; c < componentArea.Count; c++)
                {
                    if (componentArea[c] > componentArea[largest])
                        largest = c;
                }
                kept[largest] = true;

                // A component with an opening apron connected to its floor can be walked to through that opening. An
                // opening apron on its own (the opening can't be reached from the floor, e.g. a window) is never kept.
                for (var c = 0; c < componentArea.Count; c++)
                {
                    if (componentHasOpening[c] && componentArea[c] > 0f)
                        kept[c] = true;
                }

                using (var lease = new MeshLease(mesh))
                {
                    foreach (var seed in seeds)
                    {
                        var seedRef = FindOpeningSeedPoly(lease.Query, seed);
                        if (seedRef == 0)
                            continue;

                        mesh.GetTileAndPolyByRefUnsafe(seedRef, out var seedTile, out var seedPoly);
                        if (seedTile == tile && seedPoly != null && component[seedPoly.index] >= 0 && componentArea[component[seedPoly.index]] > 0f)
                            kept[component[seedPoly.index]] = true;
                    }
                }

                if (outdoor)
                {
                    // Recast space: x and z are the horizontal axes
                    var bmin = data.header.bmin;
                    var bmax = data.header.bmax;
                    for (var i = 0; i < polyCount; i++)
                    {
                        if (component[i] < 0 || kept[component[i]])
                            continue;

                        var poly = data.polys[i];
                        for (var v = 0; v < poly.vertCount; v++)
                        {
                            var x = data.verts[poly.verts[v] * 3];
                            var z = data.verts[poly.verts[v] * 3 + 2];
                            if (x <= bmin.X + TileEdgeTolerance || z <= bmin.Z + TileEdgeTolerance || x >= bmax.X - TileEdgeTolerance || z >= bmax.Z - TileEdgeTolerance)
                            {
                                kept[component[i]] = true;
                                break;
                            }
                        }
                    }
                }

                // What a kept component drops down onto can be reached too (a pit, the floor below a balcony). Ledge drops
                // only go down, so only the way down: a component kept this way can still be left only by its own ledge drops.
                for (var changed = true; changed;)
                {
                    changed = false;
                    foreach (var (_, from, to) in ledgeDrops)
                    {
                        if (from < 0 || to < 0)
                            continue;

                        var fromComponent = component[from];
                        var toComponent = component[to];
                        if (kept[fromComponent] && !kept[toComponent] && componentArea[toComponent] > 0f)
                        {
                            kept[toComponent] = true;
                            changed = true;
                        }
                    }
                }

                return kept;
            }

            void FlagIslands(bool[] kept)
            {
                // a ledge drop is usable if its start is
                foreach (var (ledgeDropPoly, from, to) in ledgeDrops)
                {
                    if (from < 0 || to < 0 || !kept[component[from]])
                        data.polys[ledgeDropPoly].flags |= NavPolyFlags.Island;
                }

                var islandPolys = 0;
                for (var i = 0; i < polyCount; i++)
                {
                    if (component[i] < 0)
                        continue; // a ledge drop, flagged above

                    if (kept[component[i]])
                        continue;

                    data.polys[i].flags |= NavPolyFlags.Island;
                    islandPolys++;
                }

                if (islandPolys > 0)
                    log.Debug($"Pathfinder: flagged {islandPolys} of {polyCount} polys as islands ({kept.Count(k => !k)} of {kept.Length} components)");
            }
        }

        /// <summary>
        /// The ledge drops (one-way off-mesh connections) of a tile as (ledge drop, from, to) poly indices: the poly at
        /// its start (link edge 0) and the one it lands on (edge 1), -1 for an end that isn't attached.
        /// </summary>
        private static List<(int ledgeDropPoly, int from, int to)> FindLedgeDrops(DtNavMesh mesh, DtMeshTile tile)
        {
            var data = tile.data;
            var ledgeDrops = new List<(int ledgeDropPoly, int from, int to)>();

            for (var i = 0; i < data.header.polyCount; i++)
            {
                var poly = data.polys[i];
                if (poly.GetPolyType() != DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    continue;

                int from = -1, to = -1;
                for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                {
                    mesh.GetTileAndPolyByRefUnsafe(tile.links[link].refs, out var endTile, out var endPoly);
                    if (endTile != tile || endPoly == null)
                        continue;

                    if (tile.links[link].edge == 0)
                        from = endPoly.index;
                    else
                        to = endPoly.index;
                }

                ledgeDrops.Add((i, from, to));
            }

            return ledgeDrops;
        }

        /// <summary>
        /// The connected components of a tile over its poly links, within the tile: the component of each poly (-1 for
        /// a ledge drop, which belongs to none), and for each component its area without opening aprons and whether it
        /// has opening apron polys.
        /// </summary>
        private static (int[] component, List<float> area, List<bool> hasOpening) FindComponents(DtNavMesh mesh, DtMeshTile tile)
        {
            var data = tile.data;
            var polyCount = data.header.polyCount;
            var component = new int[polyCount];
            var componentArea = new List<float>();
            var componentHasOpening = new List<bool>();

            Array.Fill(component, -1);
            var stack = new Stack<int>();

            for (var i = 0; i < polyCount; i++)
            {
                if (component[i] != -1 || data.polys[i].GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    continue;

                var id = componentArea.Count;
                var area = 0f;
                var hasOpening = false;
                component[i] = id;
                stack.Push(i);

                while (stack.Count > 0)
                {
                    var poly = data.polys[stack.Pop()];
                    if (poly.GetArea() == NavArea.Opening)
                        hasOpening = true;
                    else
                        area += GetPolyArea(data, poly);

                    for (var link = poly.firstLink; link != DtDetour.DT_NULL_LINK; link = tile.links[link].next)
                    {
                        mesh.GetTileAndPolyByRefUnsafe(tile.links[link].refs, out var neighborTile, out var neighbor);
                        if (neighborTile != tile || neighbor == null || component[neighbor.index] != -1 ||
                            neighbor.GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                            continue;

                        component[neighbor.index] = id;
                        stack.Push(neighbor.index);
                    }
                }

                componentArea.Add(area);
                componentHasOpening.Add(hasOpening);
            }

            return (component, componentArea, componentHasOpening);
        }

        /// <summary>
        /// The openings of a cell group an agent can get through on the group's mesh (built for that agent's size), from
        /// the opening aprons it has left (see OpeningApron), each with the mesh component its opening poly is in (see
        /// FindComponents): two openings are connected inside the group only if they share one. Islands must be marked
        /// first.
        /// </summary>
        private static List<(CellGroupOpening opening, int component)> FindPassableOpenings(DtNavMesh mesh, CellGroup cellGroup, AgentSize agentSize)
        {
            var tile = mesh.GetMaxTiles() > 0 ? mesh.GetTile(0) : null;
            var component = tile?.data?.header != null ? FindComponents(mesh, tile).component : null;

            using var lease = new MeshLease(mesh);
            var openings = new List<(CellGroupOpening opening, int component)>();
            foreach (var opening in cellGroup.Openings)
            {
                var openingRef = FindOpeningPoly(lease.Query, cellGroup, opening, agentSize, PassableOpeningFilter);
                if (openingRef == 0)
                    continue;

                mesh.GetTileAndPolyByRefUnsafe(openingRef, out _, out var poly);
                openings.Add((opening, component != null && poly != null ? component[poly.index] : -1));
            }
            return openings;
        }

        /// <summary>
        /// The opening poly of an opening's apron on a cell group's mesh of an agent size, among the polys the filter
        /// passes; 0 if there is none or the opening is too narrow or too low for the agent.
        /// </summary>
        private static long FindOpeningPoly(DtNavMeshQuery query, CellGroup cellGroup, CellGroupOpening opening, AgentSize agentSize, IDtQueryFilter filter)
        {
            // Recast can join an apron to the floor inside under a lintel thinner than a voxel (a low window)
            if (opening.Width + AgentSizeTolerance < 2f * GetAgentRadius(agentSize) || (!opening.IsHoleEdge && opening.Top - opening.Position.Z + AgentSizeTolerance < GetAgentHeight(agentSize)))
                return 0;

            var center = OpeningApron.GetCenter(opening, agentSize);
            var status = query.FindNearestPoly(ToMeshPoint(0, center, false), GetOpeningPolyHalfExtents(agentSize), filter, out var openingRef, out var openingPt, out _);
            if (status.Failed() || openingRef == 0)
                return 0;

            // the opening poly found could belong to the apron of an opening next to this one
            var found = FromMeshPoint(openingPt);
            var owner = cellGroup.Openings.MinBy(d => Vector3.DistanceSquared(OpeningApron.GetCenter(d, agentSize), found));
            return IsSameOpening(owner, opening) ? openingRef : 0;
        }

        private static bool IsSameOpening(CellGroupOpening a, CellGroupOpening b) => a.CellId == b.CellId && a.Position == b.Position;

        /// <summary>
        /// The openings of a cell group an agent of the given size can get through (see FindPassableOpenings). Loads the
        /// group's mesh if it isn't yet. While the mesh is still loading that isn't known, and all openings are
        /// returned. An empty or failed mesh (see InteriorMesh.Status) also returns all openings: a route from outside
        /// then leads to the opening and ends there, so a creature too big for the group still heads for it.
        /// </summary>
        /// <param name="components">The mesh component of each opening returned (see FindPassableOpenings), or null when
        /// that isn't known and every opening counts as connected</param>
        private static List<CellGroupOpening> GetOpenings(MeshGroupKey key, CellGroup cellGroup, AgentSize agentSize, out int[] components)
        {
            components = null;

            if (!Meshes.TryGetValue(GetMeshId(key.LandblockId, key.Index, agentSize), out var interiorMesh))
            {
                TryLoadInteriorMesh(key.LandblockId, key.Index);
                return cellGroup.Openings;
            }

            if (interiorMesh.PassableOpenings == null)
                return cellGroup.Openings;

            components = interiorMesh.PassableOpeningComponents;
            return interiorMesh.PassableOpenings;
        }

        /// <summary>
        /// The horizontal area of a poly, in m².
        /// </summary>
        private static float GetPolyArea(DtMeshData data, DtPoly poly)
        {
            // relative to the first vertex: outdoor coordinates are ~30000 m, where the products lose all precision
            var area = 0f;
            var originX = data.verts[poly.verts[0] * 3];
            var originZ = data.verts[poly.verts[0] * 3 + 2];
            for (int i = 0, j = poly.vertCount - 1; i < poly.vertCount; j = i++)
            {
                var a = poly.verts[j] * 3;
                var b = poly.verts[i] * 3;
                area += (data.verts[a] - originX) * (data.verts[b + 2] - originZ) - (data.verts[b] - originX) * (data.verts[a + 2] - originZ);
            }
            return Math.Abs(area) * 0.5f;
        }
    }
}
