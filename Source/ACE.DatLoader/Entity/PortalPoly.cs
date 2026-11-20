using System.IO;

namespace ACE.DatLoader.Entity
{
    public class PortalPoly : IUnpackable, IPackable
    {
        public short PortalIndex { get; set; }
        public short PolygonId { get; set; }

        public void Unpack(BinaryReader reader)
        {
            // Native BSPPORTAL layout: polygon ID, then portal index.
            PolygonId   = reader.ReadInt16();
            PortalIndex = reader.ReadInt16();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(PolygonId);
            writer.Write(PortalIndex);
        }
    }
}
