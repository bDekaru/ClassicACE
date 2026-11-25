using System.Collections.Generic;
using System.IO;

namespace ACE.Server.Pathfinding.Reachability
{
    /// <summary>
    /// The pieces of an interior cell that has walkable floor, with the heights they span in it.
    /// </summary>
    public struct SummaryCellPiece
    {
        /// <summary>
        /// The cell, in the format 0xBBBB (the landblock's part left out).
        /// </summary>
        public ushort Cell;
        public ushort Piece;
        public float MinZ;
        public float MaxZ;
    }

    /// <summary>
    /// What the world graph needs of one interior cell group, from its Small (player) mesh: the connected pieces, the
    /// ledge drops between them, the piece each passable doorway opens onto, and the pieces of each cell.
    /// </summary>
    public class GroupSummary
    {
        public ushort Index { get; set; }

        public List<SummaryPiece> Pieces { get; } = new List<SummaryPiece>();

        public List<(ushort from, ushort to)> LedgeDrops { get; } = new List<(ushort, ushort)>();

        /// <summary>
        /// The doorways a player gets through, with the piece inside (Group is this group's Index, not written).
        /// </summary>
        public List<SummaryDoor> Doors { get; } = new List<SummaryDoor>();

        /// <summary>
        /// Ordered by cell. A cell without floor of its own has its nearest cell's pieces (through the cell portals).
        /// </summary>
        public List<SummaryCellPiece> Cells { get; } = new List<SummaryCellPiece>();

        /// <summary>
        /// The piece at a height in a cell (cell id 0xBBBB): the one whose heights there are nearest. NoPiece if the
        /// cell has none.
        /// </summary>
        public ushort GetPiece(ushort cell, float z)
        {
            var best = TileSummary.NoPiece;
            var bestDistance = float.MaxValue;

            var i = Cells.BinarySearch(new SummaryCellPiece { Cell = cell, Piece = 0 }, CellOrder);
            for (i = i < 0 ? ~i : i; i < Cells.Count && Cells[i].Cell == cell; i++)
            {
                var distance = z < Cells[i].MinZ ? Cells[i].MinZ - z : z > Cells[i].MaxZ ? z - Cells[i].MaxZ : 0f;
                if (distance < bestDistance)
                {
                    best = Cells[i].Piece;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// By cell, then piece: the order Cells is kept in.
        /// </summary>
        internal static readonly Comparer<SummaryCellPiece> CellOrder = Comparer<SummaryCellPiece>.Create((a, b) => a.Cell != b.Cell ? a.Cell.CompareTo(b.Cell) : a.Piece.CompareTo(b.Piece));

        public void Write(BinaryWriter writer)
        {
            writer.Write(Index);
            SummaryFile.WritePieces(writer, Pieces, LedgeDrops);

            writer.Write(Doors.Count);
            foreach (var door in Doors)
            {
                writer.Write(door.Door);
                writer.Write(door.Piece);
            }

            writer.Write(Cells.Count);
            foreach (var cell in Cells)
            {
                writer.Write(cell.Cell);
                writer.Write(cell.Piece);
                writer.Write(cell.MinZ);
                writer.Write(cell.MaxZ);
            }
        }

        public static GroupSummary Read(BinaryReader reader, string label)
        {
            var group = new GroupSummary { Index = reader.ReadUInt16() };
            label += $" group {group.Index}";

            var pieceCount = SummaryFile.ReadPieces(reader, label, group.Pieces, group.LedgeDrops);

            var doorCount = SummaryFile.ReadCount(reader, label, ushort.MaxValue);
            for (var i = 0; i < doorCount; i++)
                group.Doors.Add(new SummaryDoor { Group = group.Index, Door = reader.ReadUInt16(), Piece = SummaryFile.ReadPiece(reader, label, pieceCount) });

            var cellCount = SummaryFile.ReadCount(reader, label, ushort.MaxValue);
            for (var i = 0; i < cellCount; i++)
            {
                var cell = new SummaryCellPiece { Cell = reader.ReadUInt16(), Piece = SummaryFile.ReadPiece(reader, label, pieceCount), MinZ = reader.ReadSingle(), MaxZ = reader.ReadSingle() };
                if (i > 0 && CellOrder.Compare(group.Cells[i - 1], cell) >= 0)
                    throw new InvalidDataException($"{label} has its cells out of order");
                group.Cells.Add(cell);
            }

            return group;
        }
    }

    /// <summary>
    /// The interior cell groups of one landblock (see GroupSummary), built and replaced together.
    /// </summary>
    public class InteriorSummary
    {
        /// <summary>
        /// Bump when the summaries built from the same input change, so they are built again.
        /// </summary>
        public const int Version = 1;

        private const uint FileMagic = 0x49524341; // "ACRI"

        public uint LandblockId { get; set; }

        /// <summary>
        /// The input fingerprint it was built from (see ReachabilityEntry.InteriorFingerprint).
        /// </summary>
        public ulong Fingerprint { get; set; }

        /// <summary>
        /// Ordered by group index; a group with no walkable floor has no pieces.
        /// </summary>
        public List<GroupSummary> Groups { get; } = new List<GroupSummary>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(LandblockId);
            writer.Write(Fingerprint);

            writer.Write(Groups.Count);
            foreach (var group in Groups)
                group.Write(writer);
        }

        public static InteriorSummary Read(BinaryReader reader)
        {
            var summary = new InteriorSummary { LandblockId = reader.ReadUInt32(), Fingerprint = reader.ReadUInt64() };
            var label = $"interior summary of 0x{summary.LandblockId:X8}";

            var groupCount = SummaryFile.ReadCount(reader, label, ushort.MaxValue);
            for (var i = 0; i < groupCount; i++)
                summary.Groups.Add(GroupSummary.Read(reader, label));

            return summary;
        }

        /// <summary>
        /// The summaries of the world by landblock index (see TileSummary.LoadAll).
        /// </summary>
        public static InteriorSummary[] LoadAll(string path) => SummaryFile.Load(path, FileMagic, Version, Read, s => s.LandblockId);

        public static bool SaveAll(string path, InteriorSummary[] summaries) => SummaryFile.Save(path, FileMagic, Version, summaries, (s, writer) => s.Write(writer));
    }
}
