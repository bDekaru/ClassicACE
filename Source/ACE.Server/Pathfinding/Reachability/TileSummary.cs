using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.Server.Pathfinding.Reachability
{
    [Flags]
    public enum PieceFlags : byte
    {
        None = 0,

        /// <summary>
        /// Has door apron polys: a doorway into a building or cave (phase 2 links interiors through it).
        /// </summary>
        HasDoor = 0x1,

        /// <summary>
        /// Reaches the tile edge (has spans), where it may continue into the neighbor.
        /// </summary>
        TouchesEdge = 0x2
    }

    public struct SummaryPiece
    {
        public float Area;
        public float MinZ;
        public float MaxZ;
        public PieceFlags Flags;
    }

    /// <summary>
    /// A stretch of the tile edge a piece reaches, where it can join the neighbor's piece. Side: 0 east, 1 north, 2
    /// west, 3 south. Start and End run along the edge in landblock-local metres (y on the east and west sides, x on
    /// the others), with the heights there.
    /// </summary>
    public struct SummarySpan
    {
        public ushort Piece;
        public byte Side;
        public float Start;
        public float End;
        public float StartZ;
        public float EndZ;
    }

    /// <summary>
    /// What the world graph needs of one landblock's outdoor walkable surface, without its mesh: the connected pieces,
    /// where they reach the tile edge, the ledge drops between them, and a ground-level map of which piece a spot is on.
    /// </summary>
    public class TileSummary
    {
        /// <summary>
        /// Bump when the summaries built from the same input change, so they are built again.
        /// </summary>
        public const int Version = 1;

        public const ushort NoPiece = ushort.MaxValue;

        /// <summary>
        /// The Recast map's cell size in metres, and its cells per side.
        /// </summary>
        public const int MapCellSize = 2;

        public const int MapSide = LandDefs.BlockLength / MapCellSize;

        public uint LandblockId { get; set; }

        public ReachabilityMethod Method { get; set; }

        /// <summary>
        /// The input fingerprint it was built from (see ReachabilityEntry.Fingerprint).
        /// </summary>
        public ulong Fingerprint { get; set; }

        public List<SummaryPiece> Pieces { get; } = new List<SummaryPiece>();

        public List<SummarySpan> Spans { get; } = new List<SummarySpan>();

        /// <summary>
        /// One-way, from piece to piece.
        /// </summary>
        public List<(ushort from, ushort to)> LedgeDrops { get; } = new List<(ushort, ushort)>();

        /// <summary>
        /// The piece at ground level, or NoPiece. Terrain: per terrain triangle, in LandblockStruct.Polygons order.
        /// Recast: per map cell, x * MapSide + y.
        /// </summary>
        public ushort[] Map { get; set; }

        public void Write(BinaryWriter writer)
        {
            writer.Write(LandblockId);
            writer.Write((byte)Method);
            writer.Write(Fingerprint);

            writer.Write(Pieces.Count);
            foreach (var piece in Pieces)
            {
                writer.Write(piece.Area);
                writer.Write(piece.MinZ);
                writer.Write(piece.MaxZ);
                writer.Write((byte)piece.Flags);
            }

            writer.Write(Spans.Count);
            foreach (var span in Spans)
            {
                writer.Write(span.Piece);
                writer.Write(span.Side);
                writer.Write(span.Start);
                writer.Write(span.End);
                writer.Write(span.StartZ);
                writer.Write(span.EndZ);
            }

            writer.Write(LedgeDrops.Count);
            foreach (var (from, to) in LedgeDrops)
            {
                writer.Write(from);
                writer.Write(to);
            }

            // run-length encoded: a tile of one piece is a single run
            writer.Write(Map.Length);
            for (var i = 0; i < Map.Length;)
            {
                var run = 1;
                while (i + run < Map.Length && Map[i + run] == Map[i] && run < ushort.MaxValue)
                    run++;

                writer.Write(Map[i]);
                writer.Write((ushort)run);
                i += run;
            }
        }

        /// <summary>
        /// Reads a summary written by Write. InvalidDataException if a count or piece index doesn't fit (a damaged file).
        /// </summary>
        public static TileSummary Read(BinaryReader reader)
        {
            var summary = new TileSummary
            {
                LandblockId = reader.ReadUInt32(),
                Method = (ReachabilityMethod)reader.ReadByte(),
                Fingerprint = reader.ReadUInt64()
            };

            var mapLength = summary.Method switch
            {
                ReachabilityMethod.Terrain => LandDefs.BlockSide * LandDefs.BlockSide * 2,
                ReachabilityMethod.Recast => MapSide * MapSide,
                _ => throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has method {summary.Method}")
            };

            int ReadCount(int max)
            {
                var count = reader.ReadInt32();
                if (count < 0 || count > max)
                    throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has a count of {count}");
                return count;
            }

            var pieceCount = ReadCount(NoPiece);
            for (var i = 0; i < pieceCount; i++)
                summary.Pieces.Add(new SummaryPiece { Area = reader.ReadSingle(), MinZ = reader.ReadSingle(), MaxZ = reader.ReadSingle(), Flags = (PieceFlags)reader.ReadByte() });

            ushort ReadPiece(bool noPieceAllowed = false)
            {
                var piece = reader.ReadUInt16();
                if (piece >= pieceCount && !(noPieceAllowed && piece == NoPiece))
                    throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has piece {piece} of {pieceCount}");
                return piece;
            }

            var spanCount = ReadCount(ushort.MaxValue);
            for (var i = 0; i < spanCount; i++)
            {
                var span = new SummarySpan { Piece = ReadPiece(), Side = reader.ReadByte(), Start = reader.ReadSingle(), End = reader.ReadSingle(), StartZ = reader.ReadSingle(), EndZ = reader.ReadSingle() };
                if (span.Side > 3)
                    throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has span side {span.Side}");
                summary.Spans.Add(span);
            }

            var ledgeDropCount = ReadCount(ushort.MaxValue);
            for (var i = 0; i < ledgeDropCount; i++)
                summary.LedgeDrops.Add((ReadPiece(), ReadPiece()));

            if (ReadCount(mapLength) != mapLength)
                throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has a map of the wrong size");

            summary.Map = new ushort[mapLength];
            for (var i = 0; i < mapLength;)
            {
                var value = ReadPiece(true);
                var run = reader.ReadUInt16();
                if (run == 0 || run > mapLength - i)
                    throw new InvalidDataException($"summary of 0x{summary.LandblockId:X8} has a map run of {run} at {i}");

                Array.Fill(summary.Map, value, i, run);
                i += run;
            }

            return summary;
        }

        private const uint FileMagic = 0x53524341; // "ACRS"

        /// <summary>
        /// The summaries of the world by landblock index (landblock id >> 16), null where there is none. Null if the
        /// file is missing, unreadable or of another version.
        /// </summary>
        public static TileSummary[] LoadAll(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != FileMagic || reader.ReadInt32() != Version || reader.ReadInt32() != ReachabilityManifest.LandblockCount)
                        return null;

                    var summaries = new TileSummary[ReachabilityManifest.LandblockCount];
                    for (var i = 0; i < summaries.Length; i++)
                    {
                        if (!reader.ReadBoolean())
                            continue;

                        summaries[i] = Read(reader);
                        if (summaries[i].LandblockId != (uint)i << 16)
                            return null;
                    }
                    return summaries;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is InvalidDataException)
            {
                return null;
            }
        }

        /// <summary>
        /// False (logged) if it could not be saved; an interrupted save leaves the previous file (see
        /// Pathfinder.TryWriteFile).
        /// </summary>
        public static bool SaveAll(string path, TileSummary[] summaries)
        {
            return Pathfinder.TryWriteFile(path, writer =>
            {
                writer.Write(FileMagic);
                writer.Write(Version);
                writer.Write(summaries.Length);

                foreach (var summary in summaries)
                {
                    writer.Write(summary != null);
                    summary?.Write(writer);
                }
            });
        }
    }
}
