using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyDesc : IUnpackable
    {
        public double TickSize { get; private set; }
        public double LightTickSize { get; private set; }
        public List<DayGroup> DayGroups { get; } = new List<DayGroup>();

        public void Unpack(BinaryReader reader)
        {
            TickSize        = reader.ReadDouble();
            LightTickSize   = reader.ReadDouble();

            reader.AlignBoundary();

            DayGroups.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(TickSize);
            writer.Write(LightTickSize);
            writer.AlignBoundary();

            writer.Write((uint)DayGroups.Count);

            foreach (var item in DayGroups)
                item.Pack(writer);
        }
    }
}
