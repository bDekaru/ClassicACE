using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TMTerrainDesc : IUnpackable
    {
        public uint TerrainType { get; private set; }
        public TerrainTex TerrainTex { get; } = new TerrainTex();

        public void Unpack(BinaryReader reader)
        {
            TerrainType = reader.ReadUInt32();
            TerrainTex.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(TerrainType);
            TerrainTex.Pack(writer);
        }
    }
}
