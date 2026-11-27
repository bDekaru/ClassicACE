using ACE.Server.Pathfinding.Geometry;
using ACE.Server.Pathfinding.Reachability;
using DotRecast.Detour;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The reachability of interiors: each landblock's cell groups summarized from their Small (player) meshes.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Whether a landblock has interior cell groups, and the fingerprint of their Small mesh geometry, the same one
        /// their cache files are named by (see TryLoadInteriorMesh).
        /// </summary>
        private static (ReachabilityMethod method, ulong fingerprint) GetInteriorInput(uint landblockId)
        {
            var groups = CellGroupLoader.LoadCellGroups(landblockId).Where(g => g.Index <= MaxGroup).ToList();
            if (groups.Count == 0)
                return (ReachabilityMethod.None, 0);

            return (ReachabilityMethod.Recast, GetInteriorFingerprint(groups.Select(g => (g.Index, LoadSmallInteriorGeometry(g)?.GetFingerprint() ?? 0))));
        }

        private static NavMeshGeometry LoadSmallInteriorGeometry(CellGroup group)
        {
            return NavMeshGeometry.LoadGeometry(group.Cells, group.Doors, new[] { AgentSize.Small })?[AgentSize.Small];
        }

        /// <summary>
        /// One fingerprint for a landblock's groups, from each group's index and geometry fingerprint (0 for none).
        /// </summary>
        private static ulong GetInteriorFingerprint(IEnumerable<(int index, ulong fingerprint)> groups)
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                Span<byte> entry = stackalloc byte[sizeof(int) + sizeof(ulong)];
                foreach (var (index, fingerprint) in groups)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(entry, index);
                    BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(sizeof(int)), fingerprint);
                    hash.AppendData(entry);
                }
                return BitConverter.ToUInt64(hash.GetHashAndReset(), 0);
            }
        }

        /// <summary>
        /// The summary of a landblock's interior cell groups, from their Small meshes, cached or built (and saved for
        /// the creatures' routing too). A group with no geometry or no walkable floor has no pieces.
        /// </summary>
        private static InteriorSummary BuildInteriorSummary(uint landblockId)
        {
            // not the cached groups: the bake reads landblocks that aren't loaded
            var groups = new LandblockGroups(landblockId);
            var summary = new InteriorSummary { LandblockId = landblockId };
            var fingerprints = new List<(int, ulong)>();

            foreach (var group in groups.ByIndex.Values.OrderBy(g => g.Index))
            {
                var groupSummary = new GroupSummary { Index = (ushort)group.Index };
                summary.Groups.Add(groupSummary);

                var geom = LoadSmallInteriorGeometry(group);
                var fingerprint = geom?.GetFingerprint() ?? 0;
                fingerprints.Add((group.Index, fingerprint));
                if (geom == null)
                    continue;

                var meshId = GetMeshId(landblockId, group.Index, AgentSize.Small);
                var meshPath = GetMeshPath(InteriorMeshDirectory, meshId, false, AgentSize.Small, fingerprint);
                if (!TryReadMeshFile(meshPath, out var mesh))
                    mesh = BuildAndSaveMesh(geom, GetMeshSettings(false, AgentSize.Small), meshId, meshPath);

                if (mesh != null)
                    SummarizeGroup(groupSummary, mesh, group, groups);
            }

            summary.Fingerprint = GetInteriorFingerprint(fingerprints);
            return summary;
        }

        /// <summary>
        /// A group's connected components and ledge drops, the piece inside each doorway a player gets through, and the
        /// pieces of each cell, found from the cell around each poly's middle.
        /// </summary>
        private static void SummarizeGroup(GroupSummary summary, DtNavMesh mesh, CellGroup group, LandblockGroups groups)
        {
            var tile = mesh.GetTile(0);
            var data = tile.data;
            var component = AddSummaryPieces(mesh, tile, Vector3.Zero, summary.Pieces, summary.LedgeDrops);

            var heights = new Dictionary<(uint cell, ushort piece), (float min, float max)>();
            var vertices = new Vector3[DtDetour.DT_VERTS_PER_POLYGON];
            uint lastCell = 0;
            for (var i = 0; i < data.header.polyCount; i++)
            {
                if (component[i] < 0)
                    continue;

                var poly = data.polys[i];
                var middle = Vector3.Zero;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                for (var j = 0; j < poly.vertCount; j++)
                {
                    vertices[j] = GetLocalVertex(data, poly.verts[j], Vector3.Zero);
                    middle += vertices[j];
                    minZ = Math.Min(minZ, vertices[j].Z);
                    maxZ = Math.Max(maxZ, vertices[j].Z);
                }
                middle /= poly.vertCount;

                // a poly can span cells: each cell under its middle or a corner (pulled in a little, off the cell walls)
                // gets it. The cell of the last point is searched from first: neighboring points are in the same area.
                for (var j = -1; j < poly.vertCount; j++)
                {
                    var point = j < 0 ? middle : Vector3.Lerp(vertices[j], middle, 0.1f);
                    var cell = groups.FindCell(group.Index, lastCell, point + new Vector3(0f, 0f, CellProbeHeight));
                    if (cell == 0)
                        continue; // on a door apron outside, or over a gap between cells

                    lastCell = cell;
                    var key = (cell, (ushort)component[i]);
                    var (min, max) = heights.TryGetValue(key, out var known) ? known : (float.MaxValue, float.MinValue);
                    heights[key] = (Math.Min(min, minZ), Math.Max(max, maxZ));
                }
            }

            // a cell without floor of its own (a doorway cell, a ramp over a gap) takes its nearest cell's pieces
            var cellPieces = heights.GroupBy(h => h.Key.cell).ToDictionary(g => g.Key, g => g.Select(h => (piece: h.Key.piece, h.Value.min, h.Value.max)).ToList());
            var queue = new Queue<uint>(cellPieces.Keys);
            while (queue.Count > 0)
            {
                var cellId = queue.Dequeue();
                foreach (var portal in groups.CellsById[cellId].CellPortals)
                {
                    var other = (cellId & 0xFFFF0000) | portal.OtherCellId;
                    if (group.CellIds.Contains(other) && !cellPieces.ContainsKey(other))
                    {
                        cellPieces[other] = cellPieces[cellId];
                        queue.Enqueue(other);
                    }
                }
            }

            summary.Cells.AddRange(cellPieces.SelectMany(c => c.Value.Select(p => new SummaryCellPiece { Cell = (ushort)c.Key, Piece = p.piece, MinZ = p.min, MaxZ = p.max })));
            summary.Cells.Sort(GroupSummary.CellOrder);

            using (var lease = new MeshLease(mesh))
            {
                for (var d = 0; d < group.Doors.Count; d++)
                {
                    var doorRef = FindDoorPoly(lease.Query, group, group.Doors[d], AgentSize.Small, DoorFilter);
                    if (doorRef == 0)
                        continue;

                    mesh.GetTileAndPolyByRefUnsafe(doorRef, out _, out var poly);
                    // a door apron with no floor of its own can't be reached from inside (a window), as MarkIslands judges it
                    if (poly != null && component[poly.index] >= 0 && summary.Pieces[component[poly.index]].Area > 0f)
                        summary.Doors.Add(new SummaryDoor { Group = summary.Index, Door = (ushort)d, Piece = (ushort)component[poly.index], Position = group.Doors[d].Position, Normal = group.Doors[d].Normal });
                }
            }
        }
    }
}
