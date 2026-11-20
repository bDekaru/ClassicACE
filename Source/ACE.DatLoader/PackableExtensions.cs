using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader
{
    static class PackableExtensions
    {
        /// <summary>
        /// A PackedHashTable uses a UInt16 for length, and a UInt16 for bucket size (the one the table was read with), in the order of the dictionary.
        /// </summary>
        public static void PackPackedHashTable<T>(this Dictionary<uint, T> value, BinaryWriter writer, ushort bucketSize) where T : IPackable
        {
            writer.Write((ushort)value.Count);
            writer.Write(bucketSize);

            foreach (var item in value)
            {
                writer.Write(item.Key);
                item.Value.Pack(writer);
            }
        }

        /// <summary>
        /// A PackedHashTable uses a UInt16 for length, and a UInt16 for bucket size (the one the table was read with), in the order of the dictionary.
        /// </summary>
        public static void PackPackedHashTable(this Dictionary<uint, uint> value, BinaryWriter writer, ushort bucketSize)
        {
            writer.Write((ushort)value.Count);
            writer.Write(bucketSize);

            foreach (var item in value)
            {
                writer.Write(item.Key);
                writer.Write(item.Value);
            }
        }

        /// <summary>
        /// A SmartArray uses a Compressed UInt32 for the length.
        /// </summary>
        public static void PackSmartArray(this List<uint> value, BinaryWriter writer)
        {
            writer.WriteCompressedUInt32((uint)value.Count);

            foreach (var item in value)
                writer.Write(item);
        }

        /// <summary>
        /// A SmartArray uses a Compressed UInt32 for the length.
        /// </summary>
        public static void PackSmartArray(this List<int> value, BinaryWriter writer)
        {
            writer.WriteCompressedUInt32((uint)value.Count);

            foreach (var item in value)
                writer.Write(item);
        }

        /// <summary>
        /// A SmartArray uses a Compressed UInt32 for the length.
        /// </summary>
        public static void PackSmartArray<T>(this List<T> value, BinaryWriter writer) where T : IPackable
        {
            writer.WriteCompressedUInt32((uint)value.Count);

            foreach (var item in value)
                item.Pack(writer);
        }

        /// <summary>
        /// A SmartArray uses a Compressed UInt32 for the length, in the order of the dictionary.
        /// </summary>
        public static void PackSmartArray<T>(this Dictionary<ushort, T> value, BinaryWriter writer) where T : IPackable
        {
            writer.WriteCompressedUInt32((uint)value.Count);

            value.PackFixedQuantity(writer);
        }

        /// <summary>
        /// The entries of a Dictionary without a count (the reader knows it), in the order of the dictionary.
        /// </summary>
        public static void PackFixedQuantity<T>(this Dictionary<ushort, T> value, BinaryWriter writer) where T : IPackable
        {
            foreach (var item in value)
            {
                writer.Write(item.Key);
                item.Value.Pack(writer);
            }
        }

        /// <summary>
        /// A SmartArray uses a Compressed UInt32 for the length, in the order of the dictionary.
        /// </summary>
        public static void PackSmartArray<T>(this Dictionary<uint, T> value, BinaryWriter writer) where T : IPackable
        {
            writer.WriteCompressedUInt32((uint)value.Count);

            value.PackFixedQuantity(writer);
        }

        /// <summary>
        /// The entries of a Dictionary without a count (the reader knows it), in the order of the dictionary.
        /// </summary>
        public static void PackFixedQuantity<T>(this Dictionary<uint, T> value, BinaryWriter writer) where T : IPackable
        {
            foreach (var item in value)
            {
                writer.Write(item.Key);
                item.Value.Pack(writer);
            }
        }

        /// <summary>
        /// A list that uses a four-byte count.
        /// </summary>
        public static void Pack(this List<uint> value, BinaryWriter writer)
        {
            writer.Write(value.Count);

            foreach (var item in value)
                writer.Write(item);
        }

        /// <summary>
        /// A list that uses a four-byte count.
        /// </summary>
        public static void Pack<T>(this List<T> value, BinaryWriter writer) where T : IPackable
        {
            writer.Write(value.Count);

            foreach (var item in value)
                item.Pack(writer);
        }

        /// <summary>
        /// A Dictionary that uses a four-byte count, in the order of the dictionary.
        /// </summary>
        public static void Pack<T>(this Dictionary<uint, T> value, BinaryWriter writer) where T : IPackable
        {
            writer.Write(value.Count);

            foreach (var item in value)
            {
                writer.Write(item.Key);
                item.Value.Pack(writer);
            }
        }

        /// <summary>
        /// A Dictionary that uses a four-byte count, in the order of the dictionary.
        /// </summary>
        public static void Pack<T>(this Dictionary<int, T> value, BinaryWriter writer) where T : IPackable
        {
            writer.Write(value.Count);

            foreach (var item in value)
            {
                writer.Write(item.Key);
                item.Value.Pack(writer);
            }
        }
    }
}
