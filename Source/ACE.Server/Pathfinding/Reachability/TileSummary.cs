using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ACE.Server.Pathfinding.Reachability
{
    [Flags]
    public enum PieceFlags : byte
    {
        None = 0,

        /// <summary>
        /// Has door apron polys: a doorway between a building or cave and the outdoors.
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
    /// The piece at one doorway of a cell group, by the doorway's index in CellGroup.Doors: on a tile the outdoor piece
    /// in front of it, in an interior the piece inside.
    /// </summary>
    public struct SummaryDoor
    {
        public ushort Group;
        public ushort Door;
        public ushort Piece;

        /// <summary>
        /// Interiors only: the doorway's threshold in landblock-local metres and the way it faces out, to join doorways
        /// that open straight into another group's (see WorldGraph.Build).
        /// </summary>
        public Vector3 Position;
        public Vector3 Normal;
    }

    /// <summary>
    /// A piece over a Recast map cell clearly above the ground there (a roof, a rim, a platform), with its surface height
    /// over the cell in landblock-local metres.
    /// </summary>
    public struct SummaryLayer
    {
        public ushort Piece;
        public float Z;
    }

    /// <summary>
    /// What the world graph needs of one landblock's outdoor walkable surface, without its mesh: the connected pieces,
    /// where they reach the tile edge, the ledge drops between them, the pieces in front of the doorways, and a map of
    /// which piece a spot is on, at ground level and on the raised layers.
    /// </summary>
    public class TileSummary
    {
        /// <summary>
        /// Bump when the summaries built from the same input change, so they are built again.
        /// </summary>
        public const int Version = 4;

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
        /// Recast only: the doorways of the landblock's buildings and caves that have an outdoor piece in front.
        /// </summary>
        public List<SummaryDoor> Doors { get; } = new List<SummaryDoor>();

        /// <summary>
        /// The piece at ground level, or NoPiece. Terrain: per terrain triangle, in LandblockStruct.Polygons order.
        /// Recast: per map cell, x * MapSide + y.
        /// </summary>
        public ushort[] Map { get; set; }

        /// <summary>
        /// Recast only: the raised layers by map cell, for the cells that have any.
        /// </summary>
        public Dictionary<int, List<SummaryLayer>> Layers { get; } = new Dictionary<int, List<SummaryLayer>>();

        public void Write(BinaryWriter writer)
        {
            writer.Write(LandblockId);
            writer.Write((byte)Method);
            writer.Write(Fingerprint);

            SummaryFile.WritePieces(writer, Pieces, LedgeDrops);

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

            writer.Write(Doors.Count);
            foreach (var door in Doors)
            {
                writer.Write(door.Group);
                writer.Write(door.Door);
                writer.Write(door.Piece);
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

            writer.Write(Layers.Count);
            foreach (var (cell, layers) in Layers.OrderBy(l => l.Key))
            {
                writer.Write((ushort)cell);
                writer.Write((byte)layers.Count);
                foreach (var layer in layers)
                {
                    writer.Write(layer.Piece);
                    writer.Write(layer.Z);
                }
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

            var label = $"summary of 0x{summary.LandblockId:X8}";
            var pieceCount = SummaryFile.ReadPieces(reader, label, summary.Pieces, summary.LedgeDrops);

            var spanCount = SummaryFile.ReadCount(reader, label, ushort.MaxValue);
            for (var i = 0; i < spanCount; i++)
            {
                var span = new SummarySpan { Piece = SummaryFile.ReadPiece(reader, label, pieceCount), Side = reader.ReadByte(), Start = reader.ReadSingle(), End = reader.ReadSingle(), StartZ = reader.ReadSingle(), EndZ = reader.ReadSingle() };
                if (span.Side > 3)
                    throw new InvalidDataException($"{label} has span side {span.Side}");
                summary.Spans.Add(span);
            }

            var doorCount = SummaryFile.ReadCount(reader, label, ushort.MaxValue);
            var doorways = new HashSet<(ushort, ushort)>();
            for (var i = 0; i < doorCount; i++)
            {
                var door = new SummaryDoor { Group = reader.ReadUInt16(), Door = reader.ReadUInt16(), Piece = SummaryFile.ReadPiece(reader, label, pieceCount) };
                if (!doorways.Add((door.Group, door.Door)))
                    throw new InvalidDataException($"{label} has doorway {door.Door} of group {door.Group} twice");
                summary.Doors.Add(door);
            }

            if (SummaryFile.ReadCount(reader, label, mapLength) != mapLength)
                throw new InvalidDataException($"{label} has a map of the wrong size");

            summary.Map = new ushort[mapLength];
            for (var i = 0; i < mapLength;)
            {
                var value = SummaryFile.ReadPiece(reader, label, pieceCount, true);
                var run = reader.ReadUInt16();
                if (run == 0 || run > mapLength - i)
                    throw new InvalidDataException($"{label} has a map run of {run} at {i}");

                Array.Fill(summary.Map, value, i, run);
                i += run;
            }

            var layerCells = SummaryFile.ReadCount(reader, label, mapLength);
            for (var i = 0; i < layerCells; i++)
            {
                var cell = reader.ReadUInt16();
                var count = reader.ReadByte();
                if (cell >= mapLength || count == 0 || summary.Layers.ContainsKey(cell))
                    throw new InvalidDataException($"{label} has {count} layers at map cell {cell}");

                var layers = new List<SummaryLayer>(count);
                for (var j = 0; j < count; j++)
                    layers.Add(new SummaryLayer { Piece = SummaryFile.ReadPiece(reader, label, pieceCount), Z = reader.ReadSingle() });
                summary.Layers[cell] = layers;
            }

            return summary;
        }

        private const uint FileMagic = 0x53524341; // "ACRS"

        /// <summary>
        /// The summaries of the world by landblock index (landblock id >> 16), null where there is none. Null if the
        /// file is missing, unreadable or of another version.
        /// </summary>
        public static TileSummary[] LoadAll(string path) => SummaryFile.Load(path, FileMagic, Version, Read, s => s.LandblockId);

        /// <summary>
        /// False (logged) if it could not be saved; an interrupted save leaves the previous file (see
        /// Pathfinder.TryWriteFile).
        /// </summary>
        public static bool SaveAll(string path, TileSummary[] summaries) => SummaryFile.Save(path, FileMagic, Version, summaries, (s, writer) => s.Write(writer));
    }
}
