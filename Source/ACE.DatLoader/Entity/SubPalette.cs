using System.IO;

namespace ACE.DatLoader.Entity
{
    // TODO: refactor to use existing PaletteOverride object
    public class SubPalette : IUnpackable, IPackable
    {
        public uint SubID { get; set; }
        public uint Offset { get; set; }
        public uint NumColors { get; set; }

        public void Unpack(BinaryReader reader)
        {
            SubID       = reader.ReadAsDataIDOfKnownType(0x04000000);
            Offset      = (uint)(reader.ReadByte() * 8);
            NumColors   = reader.ReadByte();

            if (NumColors == 0)
                NumColors = 256;

            NumColors *= 8;
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WriteAsDataIDOfKnownType(0x04000000, SubID);
            writer.Write((byte)(Offset / 8));

            var colors = NumColors / 8;
            writer.Write((byte)(colors == 256 ? 0 : colors));
        }
    }
}
