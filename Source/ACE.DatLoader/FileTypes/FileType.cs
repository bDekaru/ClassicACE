using System;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    public abstract class FileType : IUnpackable, IPackable
    {
        public uint Id { get; set; }

        public abstract void Unpack(BinaryReader reader);

        /// <summary>Reads a record the dat stores as <paramref name="record"/>.</summary>
        public static T Unpack<T>(byte[] record) where T : FileType, new()
        {
            var item = new T();

            using (var reader = new BinaryReader(new MemoryStream(record)))
                item.Unpack(reader);

            return item;
        }

        /// <exception cref="NotSupportedException">The file type has no writer yet.</exception>
        public virtual void Pack(BinaryWriter writer)
        {
            throw new NotSupportedException($"Writing {GetType().Name} records is not supported.");
        }

        /// <summary>The record as the dat stores it.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Pack(writer);

                return stream.ToArray();
            }
        }
    }
}
