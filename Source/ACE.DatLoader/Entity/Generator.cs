using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class Generator : IUnpackable, IPackable
    {
        public string Name { get; private set; }
        public uint Id { get; private set; }
        public List<Generator> Items { get; } = new List<Generator>();

        public void Unpack(BinaryReader reader)
        {
            Name = reader.ReadObfuscatedString();
            reader.AlignBoundary();

            Id = reader.ReadUInt32();

            Items.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WriteObfuscatedString(Name);
            writer.AlignBoundary();

            writer.Write(Id);

            Items.Pack(writer);
        }
    }
}
