using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.Server.Pathfinding.Reachability
{
    /// <summary>
    /// The parts of the binary format the tile and interior summaries share, and their files of summaries by landblock.
    /// </summary>
    internal static class SummaryFile
    {
        /// <summary>
        /// Writes pieces and the ledge drops between them.
        /// </summary>
        public static void WritePieces(BinaryWriter writer, List<SummaryPiece> pieces, List<(ushort from, ushort to)> ledgeDrops)
        {
            writer.Write(pieces.Count);
            foreach (var piece in pieces)
            {
                writer.Write(piece.Area);
                writer.Write(piece.MinZ);
                writer.Write(piece.MaxZ);
                writer.Write((byte)piece.Flags);
            }

            writer.Write(ledgeDrops.Count);
            foreach (var (from, to) in ledgeDrops)
            {
                writer.Write(from);
                writer.Write(to);
            }
        }

        /// <summary>
        /// Reads what WritePieces wrote into the lists, and returns the piece count.
        /// </summary>
        public static int ReadPieces(BinaryReader reader, string label, List<SummaryPiece> pieces, List<(ushort from, ushort to)> ledgeDrops)
        {
            var pieceCount = ReadCount(reader, label, TileSummary.NoPiece);
            for (var i = 0; i < pieceCount; i++)
                pieces.Add(new SummaryPiece { Area = reader.ReadSingle(), MinZ = reader.ReadSingle(), MaxZ = reader.ReadSingle(), Flags = (PieceFlags)reader.ReadByte() });

            var ledgeDropCount = ReadCount(reader, label, ushort.MaxValue);
            for (var i = 0; i < ledgeDropCount; i++)
                ledgeDrops.Add((ReadPiece(reader, label, pieceCount), ReadPiece(reader, label, pieceCount)));

            return pieceCount;
        }

        public static int ReadCount(BinaryReader reader, string label, int max)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > max)
                throw new InvalidDataException($"{label} has a count of {count}");
            return count;
        }

        public static ushort ReadPiece(BinaryReader reader, string label, int pieceCount, bool noPieceAllowed = false)
        {
            var piece = reader.ReadUInt16();
            if (piece >= pieceCount && !(noPieceAllowed && piece == TileSummary.NoPiece))
                throw new InvalidDataException($"{label} has piece {piece} of {pieceCount}");
            return piece;
        }

        /// <summary>
        /// A file of summaries by landblock index (landblock id >> 16), null where there is none. Null if the file is
        /// missing, unreadable, of another version, or has a summary at the wrong index.
        /// </summary>
        public static T[] Load<T>(string path, uint magic, int version, Func<BinaryReader, T> read, Func<T, uint> landblockOf) where T : class
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != magic || reader.ReadInt32() != version || reader.ReadInt32() != ReachabilityManifest.LandblockCount)
                        return null;

                    var summaries = new T[ReachabilityManifest.LandblockCount];
                    for (var i = 0; i < summaries.Length; i++)
                    {
                        if (!reader.ReadBoolean())
                            continue;

                        summaries[i] = read(reader);
                        if (landblockOf(summaries[i]) != (uint)i << 16)
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
        public static bool Save<T>(string path, uint magic, int version, T[] summaries, Action<T, BinaryWriter> write) where T : class
        {
            return Pathfinder.TryWriteFile(path, writer =>
            {
                writer.Write(magic);
                writer.Write(version);
                writer.Write(summaries.Length);

                foreach (var summary in summaries)
                {
                    writer.Write(summary != null);
                    if (summary != null)
                        write(summary, writer);
                }
            });
        }
    }
}
