using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class DayGroup : IUnpackable
    {
        public float ChanceOfOccur { get; private set; }
        public string DayName { get; private set; }
        public List<SkyObject> SkyObjects { get; } = new List<SkyObject>();
        public List<SkyTimeOfDay> SkyTime { get; } = new List<SkyTimeOfDay>();

        public void Unpack(BinaryReader reader)
        {
            ChanceOfOccur   = reader.ReadSingle();
            DayName         = reader.ReadPString();
            reader.AlignBoundary();

            SkyObjects.Unpack(reader);
            SkyTime.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(ChanceOfOccur);
            writer.WritePString(DayName);
            writer.AlignBoundary();

            writer.Write((uint)SkyObjects.Count);

            foreach (var item in SkyObjects)
                item.Pack(writer);

            writer.Write((uint)SkyTime.Count);

            foreach (var item in SkyTime)
                item.Pack(writer);
        }
    }
}
