using System.IO;

namespace ACE.DatLoader.Entity
{
    public class CloSubPaletteRange : IUnpackable, IPackable
    {
        public uint Offset { get; set; }
        public uint NumColors { get; set; }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Offset);
            writer.Write(NumColors);
        }

        public void Unpack(BinaryReader reader)
        {
            Offset      = reader.ReadUInt32();
            NumColors   = reader.ReadUInt32();
        }
    }
}
