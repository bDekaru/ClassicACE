using System.IO;

namespace ACE.DatLoader.Entity
{
    public class FontCharDesc : IUnpackable, IPackable
    {
        public ushort Unicode;
        public ushort OffsetX;
        public ushort OffsetY;
        public byte Width;
        public byte Height;
        public byte HorizontalOffsetBefore;
        public byte HorizontalOffsetAfter;
        public byte VerticalOffsetBefore;

        public void Unpack(BinaryReader reader)
        {
            Unicode = reader.ReadUInt16();
            OffsetX = reader.ReadUInt16();
            OffsetY = reader.ReadUInt16();
            Width = reader.ReadByte();
            Height = reader.ReadByte();
            HorizontalOffsetBefore = reader.ReadByte();
            HorizontalOffsetAfter = reader.ReadByte();
            VerticalOffsetBefore = reader.ReadByte();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Unicode);
            writer.Write(OffsetX);
            writer.Write(OffsetY);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(HorizontalOffsetBefore);
            writer.Write(HorizontalOffsetAfter);
            writer.Write(VerticalOffsetBefore);
        }
    }
}
