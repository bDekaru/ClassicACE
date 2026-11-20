using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TerrainAlphaMap : IUnpackable, IPackable
    {
        public uint TCode { get; private set; }
        public uint TexGID { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            TCode   = reader.ReadUInt32();
            TexGID  = reader.ReadUInt32();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(TCode);
            writer.Write(TexGID);
        }
    }
}
