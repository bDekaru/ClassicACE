using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader
{
    static class BinaryWriterExtensions
    {
        /// <summary>
        /// Pads the stream with zeros up to the next DWORD boundary (the counterpart of the reader's AlignBoundary).
        /// </summary>
        public static void AlignBoundary(this BinaryWriter writer)
        {
            var alignDelta = writer.BaseStream.Position % 4;

            if (alignDelta != 0)
                writer.Write(new byte[4 - alignDelta]);
        }

        /// <summary>
        /// Writes a compressed UInt32 in its shortest form: 1 byte below 0x80, 2 bytes below 0x4000, else 4 bytes (the counterpart of the reader's ReadCompressedUInt32).
        /// </summary>
        public static void WriteCompressedUInt32(this BinaryWriter writer, uint value)
        {
            if (value < 0x80)
                writer.Write((byte)value);
            else if (value < 0x4000)
            {
                writer.Write((byte)(0x80 | (value >> 8)));
                writer.Write((byte)value);
            }
            else
            {
                writer.Write((byte)(0xC0 | ((value >> 24) & 0x3F)));
                writer.Write((byte)(value >> 16));
                writer.Write((ushort)value);
            }
        }

        /// <summary>
        /// Writes a string behind a compressed length, two bytes per character (the counterpart of the reader's ReadUnicodeString).
        /// </summary>
        public static void WriteUnicodeString(this BinaryWriter writer, string value)
        {
            value = value ?? "";

            writer.WriteCompressedUInt32((uint)value.Length);

            foreach (var c in value)
                writer.Write((ushort)c);
        }

        /// <summary>
        /// Writes a string behind a UInt16 length (the counterpart of the reader's ReadPString).
        /// </summary>
        public static void WritePString(this BinaryWriter writer, string value)
        {
            var bytes = System.Text.Encoding.Default.GetBytes(value ?? "");

            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// Writes a string behind a UInt16 length with the nibbles of every byte swapped (the counterpart of the reader's ReadObfuscatedString).
        /// </summary>
        public static void WriteObfuscatedString(this BinaryWriter writer, string value)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var bytes = System.Text.Encoding.GetEncoding(1252).GetBytes(value ?? "");

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)((bytes[i] >> 4) | (bytes[i] << 4));

            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// Writes a data id relative to its known type: 2 bytes below 0x4000, else 4 bytes with 0x8000 set in the first (the counterpart of the reader's ReadAsDataIDOfKnownType).
        /// </summary>
        public static void WriteAsDataIDOfKnownType(this BinaryWriter writer, uint knownType, uint value)
        {
            var offset = value - knownType;

            if (offset < 0x4000)
                writer.Write((ushort)offset);
            else
            {
                writer.Write((ushort)(0x8000 | ((offset >> 16) & 0x3FFF)));
                writer.Write((ushort)offset);
            }
        }

        /// <summary>
        /// Writes a SmartArray of UInt32 behind a compressed length (the counterpart of UnpackSmartArray).
        /// </summary>
        public static void WriteSmartArray(this BinaryWriter writer, List<uint> values)
        {
            writer.WriteCompressedUInt32((uint)values.Count);

            foreach (var value in values)
                writer.Write(value);
        }

        /// <summary>
        /// Writes a SmartArray of Int32 behind a compressed length (the counterpart of UnpackSmartArray).
        /// </summary>
        public static void WriteSmartArray(this BinaryWriter writer, List<int> values)
        {
            writer.WriteCompressedUInt32((uint)values.Count);

            foreach (var value in values)
                writer.Write(value);
        }

        /// <summary>
        /// Writes a SmartArray of objects behind a compressed length (the counterpart of UnpackSmartArray).
        /// </summary>
        public static void WriteSmartArray<T>(this BinaryWriter writer, List<T> values, Action<T, BinaryWriter> pack)
        {
            writer.WriteCompressedUInt32((uint)values.Count);

            foreach (var value in values)
                pack(value, writer);
        }
    }
}
