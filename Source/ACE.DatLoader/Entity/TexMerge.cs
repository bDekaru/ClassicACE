using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TexMerge : IUnpackable
    {
        public uint BaseTexSize { get; private set; }
        public List<TerrainAlphaMap> CornerTerrainMaps { get; } = new List<TerrainAlphaMap>();
        public List<TerrainAlphaMap> SideTerrainMaps { get; } = new List<TerrainAlphaMap>();
        public List<RoadAlphaMap> RoadMaps { get; } = new List<RoadAlphaMap>();
        public List<TMTerrainDesc> TerrainDesc { get; } = new List<TMTerrainDesc>();

        public void Unpack(BinaryReader reader)
        {
            BaseTexSize = reader.ReadUInt32();

            CornerTerrainMaps.Unpack(reader);
            SideTerrainMaps.Unpack(reader);
            RoadMaps.Unpack(reader);
            TerrainDesc.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(BaseTexSize);

            writer.Write((uint)CornerTerrainMaps.Count);

            foreach (var item in CornerTerrainMaps)
                item.Pack(writer);

            writer.Write((uint)SideTerrainMaps.Count);

            foreach (var item in SideTerrainMaps)
                item.Pack(writer);

            writer.Write((uint)RoadMaps.Count);

            foreach (var item in RoadMaps)
                item.Pack(writer);

            writer.Write((uint)TerrainDesc.Count);

            foreach (var item in TerrainDesc)
                item.Pack(writer);
        }
    }
}
