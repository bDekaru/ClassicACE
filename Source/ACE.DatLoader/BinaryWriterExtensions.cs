using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace ACE.DatLoader
{
    static class BinaryWriterExtensions
    {
        /// <summary>
        /// Pads the stream with zeros up to the next DWORD boundary.
        /// </summary>
        public static void AlignBoundary(this BinaryWriter writer)
        {
            var alignDelta = writer.BaseStream.Position % 4;

            if (alignDelta != 0)
                writer.Write(new byte[4 - alignDelta]);
        }

        /// <summary>
        /// Writes a compressed UInt32 in its shortest form: 1 byte below 0x80, 2 bytes below 0x4000, else 4 bytes.
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
        /// Writes a string behind a compressed length, two bytes per character.
        /// </summary>
        public static void WriteUnicodeString(this BinaryWriter writer, string value)
        {
            value = value ?? "";

            writer.WriteCompressedUInt32((uint)value.Length);

            foreach (var c in value)
                writer.Write((ushort)c);
        }

        /// <summary>
        /// Writes a string behind a one- or two-byte length. Encoding.Default is used like ReadPString does, so that the bytes written are the bytes read.
        /// </summary>
        public static void WritePString(this BinaryWriter writer, string value, uint sizeOfLength = 2)
        {
            var bytes = System.Text.Encoding.Default.GetBytes(value ?? "");

            if (sizeOfLength == 1)
                writer.Write((byte)bytes.Length);
            else
                writer.Write((ushort)bytes.Length);

            writer.Write(bytes);
        }

        /// <summary>
        /// Writes a string behind a UInt16 length with the nibbles of every byte swapped.
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
        /// Writes a data id relative to its known type: 2 bytes below 0x4000, else 4 bytes with 0x8000 set in the first.
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
        /// Writes three floats, x y z.
        /// </summary>
        public static void WriteVector3(this BinaryWriter writer, Vector3 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
        }
    }
}
