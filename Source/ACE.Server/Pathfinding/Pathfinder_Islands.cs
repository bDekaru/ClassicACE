//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
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
    /// The parts of a mesh nothing can walk to (islands), and the doorways each agent size can get through.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Outdoor tile components with a vertex this close to the tile edge are not islands: they may continue into
        /// the neighboring tile. Tiles are built with a border, so the walkable area reaches the edge exactly.
        /// </summary>
        private const float TileEdgeTolerance = 0.25f;

        /// <summary>
        /// How far around a doorway to look for the mesh poly that anchors its component (doorways are not on the
        /// eroded mesh themselves).
        /// </summary>
        private static readonly RcVec3f DoorSeedHalfExtents = new RcVec3f(2.0f, 2.0f, 2.0f);

        /// <summary>
        /// The door polys of reachable door aprons.
        /// </summary>
        private static readonly DtQueryDefaultFilter PassableDoorFilter = new DtQueryDefaultFilter(NavPolyFlags.Door, NavPolyFlags.Island, Array.Empty<float>());

        /// <summary>
        /// How far around the center of a door apron to look for its door polys, on the mesh of an agent size. Erosion
        /// leaves them well inside the apron; vertically they are on the threshold.
        /// </summary>
        private static RcVec3f GetDoorPolyHalfExtents(AgentSize agentSize)
        {
            var horizontal = DoorApron.GetDepth(agentSize) * 0.5f + 0.25f;
            return new RcVec3f(horizontal, 0.6f, horizontal);
        }

        /// <summary>
        /// Flags the islands of a mesh with a single tile, before it is published: connected components of polys that
        /// cannot be walked to. A component is kept (not an island) if it is the largest one, holds one of the seed
        /// points (doorways), has floor connected to a door apron (see DoorApron), or, for an outdoor tile, reaches
        /// the tile edge where it may continue into the neighbor. The rest are tabletops, the tops of crates and rocks,
        /// pits you can only drop into, door aprons that can't be reached (windows) and the like. Sizes are compared
        /// without the door aprons, and a component that is only a door apron is never kept.
        /// Must run before the tile is published, since it changes poly flags.
        /// </summary>
        /// <param name="seeds">Points known to be reachable, on the mesh (Recast space)</param>
        private static void MarkIslands(DtNavMesh mesh, IEnumerable<RcVec3f> seeds, bool outdoor)
        {
            var tile = mesh.GetMaxTiles() > 0 ? mesh.GetTile(0) : null;
            var data = tile?.data;
            if (data?.header == null || data.polys == null || data.header.polyCount == 0)
                return;

            var polyCount = data.header.polyCount;

            // the component of each poly (-1 for a drop), the area of each component without its door aprons and
            // whether it has any door apron polys, and the drops (one-way off-mesh connections) between components as
            // (drop, from, to) poly indices
            var component = new int[polyCount];
            var componentArea = new List<float>();
            var componentHasDoor = new List<bool>();
            var drops = new List<(int dropPoly, int from, int to)>();

            FindComponents();
            FindDrops();

            // all connected: everything is kept, and only a drop that isn't attached to the floor on both ends leads nowhere
            var keep = componentArea.Count <= 1 ? Enumerable.Repeat(true, componentArea.Count).ToArray() : GetKeptComponents();
            FlagIslands(keep);

            // connected components over the poly links
            void FindComponents()
            {
                Array.Fill(component, -1);
                var stack = new Stack<int>();

                for (var i = 0; i < polyCount; i++)
                {
                    // drops are not part of any component, see FindDrops
                    if (component[i] != -1 || data.polys[i].GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                        continue;

                    var id = componentArea.Count;
                    var area = 0f;
                    var hasDoor = false;
                    component[i] = id;
                    stack.Push(i);

                    while (stack.Count > 0)
                    {
                        var poly = data.polys[stack.Pop()];
                        if (poly.GetArea() == NavArea.Door)
                            hasDoor = true;
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
                    componentHasDoor.Add(hasDoor);
                }
            }

            // A drop links to the poly at its start (edge 0) and the one it lands on (edge 1).
            void FindDrops()
            {
                for (var i = 0; i < polyCount; i++)
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

                    drops.Add((i, from, to));
                }
            }

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

                // A component with a door apron connected to its floor can be walked to through that doorway. A door
                // apron on its own (the doorway can't be reached from the floor, e.g. a window) is never kept.
                for (var c = 0; c < componentArea.Count; c++)
                {
                    if (componentHasDoor[c] && componentArea[c] > 0f)
                        kept[c] = true;
                }

                using (var lease = new MeshLease(mesh))
                {
                    foreach (var seed in seeds)
                    {
                        var status = lease.Query.FindNearestPoly(seed, DoorSeedHalfExtents, AllPolysFilter, out var seedRef, out _, out _);
                        if (status.Failed() || seedRef == 0)
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

                // What a kept component drops down onto can be reached too (a pit, the floor below a balcony). Drops
                // only go down, so only the way down: a component kept this way can still be left only by its own drops.
                for (var changed = true; changed;)
                {
                    changed = false;
                    foreach (var (_, from, to) in drops)
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
                // a drop is usable if its start is
                foreach (var (dropPoly, from, to) in drops)
                {
                    if (from < 0 || to < 0 || !kept[component[from]])
                        data.polys[dropPoly].flags |= NavPolyFlags.Island;
                }

                var islandPolys = 0;
                for (var i = 0; i < polyCount; i++)
                {
                    if (component[i] < 0)
                        continue; // a drop, flagged above

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
        /// The doorways of a cell group an agent can get through on the group's mesh (built for that agent's size), from
        /// the door aprons it has left (see DoorApron). Islands must be marked first. Doorways that are not in a wall
        /// have no apron and are kept.
        /// </summary>
        private static List<CellGroupDoor> FindPassableDoors(DtNavMesh mesh, CellGroup cellGroup, AgentSize agentSize)
        {
            var passable = new List<CellGroupDoor>(cellGroup.Doors.Count);
            var aprons = cellGroup.Doors.Where(d => d.IsInWall).ToList();
            var halfExtents = GetDoorPolyHalfExtents(agentSize);

            using var lease = new MeshLease(mesh);
            foreach (var door in cellGroup.Doors)
            {
                if (!door.IsInWall)
                {
                    passable.Add(door);
                    continue;
                }

                var center = DoorApron.GetCenter(door, agentSize);
                var status = lease.Query.FindNearestPoly(ToMeshPoint(0, center, false), halfExtents, PassableDoorFilter, out var doorRef, out var doorPt, out _);
                if (status.Failed() || doorRef == 0)
                    continue;

                // the door poly found could belong to the apron of a doorway next to this one
                var found = FromMeshPoint(doorPt);
                var owner = aprons.OrderBy(d => Vector3.DistanceSquared(DoorApron.GetCenter(d, agentSize), found)).First();
                if (owner.CellId == door.CellId && owner.Position == door.Position)
                    passable.Add(door);
            }

            return passable;
        }

        /// <summary>
        /// The doorways of a cell group an agent of the given size can get through (see FindPassableDoors). Loads the
        /// group's mesh if it isn't yet. While the mesh is still loading that isn't known, and all doorways are returned.
        /// </summary>
        private static List<CellGroupDoor> GetDoors(MeshGroupKey key, CellGroup cellGroup, AgentSize agentSize)
        {
            if (!Meshes.TryGetValue(GetMeshId(key.LandblockId, key.Index, agentSize), out var interiorMesh))
            {
                TryLoadInteriorMesh(key.LandblockId, key.Index);
                return cellGroup.Doors;
            }

            return interiorMesh.PassableDoors ?? cellGroup.Doors;
        }

        /// <summary>
        /// The horizontal area of a poly, in m².
        /// </summary>
        private static float GetPolyArea(DtMeshData data, DtPoly poly)
        {
            var area = 0f;
            for (int i = 0, j = poly.vertCount - 1; i < poly.vertCount; j = i++)
            {
                var a = poly.verts[j] * 3;
                var b = poly.verts[i] * 3;
                area += data.verts[a] * data.verts[b + 2] - data.verts[b] * data.verts[a + 2];
            }
            return Math.Abs(area) * 0.5f;
        }
    }
}
