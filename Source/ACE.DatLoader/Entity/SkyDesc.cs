using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyDesc : IUnpackable, IPackable
    {
        public double TickSize { get; private set; }
        public double LightTickSize { get; private set; }
        public List<DayGroup> DayGroups { get; } = new List<DayGroup>();

        public void Unpack(BinaryReader reader) => Unpack(reader, false);

        /// <summary>Reads the pre-ToD (2005) layout, whose sky objects have no properties field.</summary>
        public void UnpackPreToD(BinaryReader reader) => Unpack(reader, true);

        private void Unpack(BinaryReader reader, bool preToD)
        {
            TickSize        = reader.ReadDouble();
            LightTickSize   = reader.ReadDouble();

            reader.AlignBoundary();

            if (preToD)
            {
                var count = reader.ReadUInt32();

                for (int i = 0; i < count; i++)
                {
                    var dayGroup = new DayGroup();
                    dayGroup.UnpackPreToD(reader);
                    DayGroups.Add(dayGroup);
                }
            }
            else
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
