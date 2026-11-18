using System.IO;

namespace ACE.DatLoader.Entity
{
    public class LocationType : IUnpackable
    {
        public int PartId { get; private set; }
        public Frame Frame { get; } = new Frame();

        public void Unpack(BinaryReader reader)
        {
            PartId = reader.ReadInt32();
            Frame.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(PartId);
            Frame.Pack(writer);
        }
    }
}
