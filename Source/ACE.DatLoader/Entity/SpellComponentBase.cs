using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SpellComponentBase : IUnpackable
    {
        public string Name { get; set; }
        public uint Category { get; set; }
        public uint Icon { get; set; }
        public uint Type { get; set; }
        public uint Gesture { get; set; }
        public float Time { get; set; }
        public string Text { get; set; }
        public float CDM { get; set; } // Component destruction modifier: multiplies the chance that the component burns when a spell is cast

        public void Unpack(BinaryReader reader)
        {
            Name        = reader.ReadObfuscatedString();
            reader.AlignBoundary();
            Category    = reader.ReadUInt32();
            Icon        = reader.ReadUInt32();
            Type        = reader.ReadUInt32();
            Gesture     = reader.ReadUInt32();
            Time        = reader.ReadSingle();
            Text        = reader.ReadObfuscatedString();
            reader.AlignBoundary();
            CDM         = reader.ReadSingle();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WriteObfuscatedString(Name);
            writer.AlignBoundary();
            writer.Write(Category);
            writer.Write(Icon);
            writer.Write(Type);
            writer.Write(Gesture);
            writer.Write(Time);
            writer.WriteObfuscatedString(Text);
            writer.AlignBoundary();
            writer.Write(CDM);
        }
    }
}
