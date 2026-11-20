using System.IO;
using ACE.Entity.Enum;

namespace ACE.DatLoader.Entity
{
    public class CellPortal : IUnpackable, IPackable
    {
        public PortalFlags Flags { get; private set; }
        public ushort PolygonId { get; private set; }
        public ushort OtherCellId { get; private set; }
        public ushort OtherPortalId { get; private set; }

        public bool ExactMatch => (Flags & PortalFlags.ExactMatch) != 0;
        public bool PortalSide => (Flags & PortalFlags.PortalSide) == 0;

        public void Unpack(BinaryReader reader)
        {
            Flags           = (PortalFlags)reader.ReadUInt16();
            PolygonId       = reader.ReadUInt16();
            OtherCellId     = reader.ReadUInt16();
            OtherPortalId   = reader.ReadUInt16();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write((ushort)Flags);
            writer.Write(PolygonId);
            writer.Write(OtherCellId);
            writer.Write(OtherPortalId);
        }
    }
}
