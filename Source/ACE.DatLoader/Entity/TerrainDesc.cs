using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TerrainDesc : IUnpackable
    {
        public List<TerrainType> TerrainTypes { get; } = new List<TerrainType>();
        public LandSurf LandSurfaces { get; } = new LandSurf();

        public void Unpack(BinaryReader reader)
        {
            TerrainTypes.Unpack(reader);
            LandSurfaces.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write((uint)TerrainTypes.Count);

            foreach (var item in TerrainTypes)
                item.Pack(writer);

            LandSurfaces.Pack(writer);
        }
    }
}
