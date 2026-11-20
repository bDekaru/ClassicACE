using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TerrainType : IUnpackable, IPackable
    {
        public string TerrainName { get; private set; }
        public uint TerrainColor { get; private set; }
        public List<uint> SceneTypes { get; } = new List<uint>();

        public void Unpack(BinaryReader reader)
        {
            TerrainName = reader.ReadPString();
            reader.AlignBoundary();

            TerrainColor = reader.ReadUInt32();

            SceneTypes.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WritePString(TerrainName);
            writer.AlignBoundary();
            writer.Write(TerrainColor);

            SceneTypes.Pack(writer);
        }
    }
}
