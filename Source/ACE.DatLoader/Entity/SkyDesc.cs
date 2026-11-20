using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyDesc : IUnpackable, IPackable
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

        public void Pack(BinaryWriter writer)
        {
            writer.Write(TickSize);
            writer.Write(LightTickSize);
            writer.AlignBoundary();

            DayGroups.Pack(writer);
        }
    }
}
