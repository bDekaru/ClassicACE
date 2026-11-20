using System;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    public abstract class FileType : IUnpackable, IPackable
    {
        public uint Id { get; set; }

        public abstract void Unpack(BinaryReader reader);

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
