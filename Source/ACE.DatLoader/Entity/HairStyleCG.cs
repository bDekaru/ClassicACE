using System.IO;

namespace ACE.DatLoader.Entity
{
    public class HairStyleCG : IUnpackable
    {
        public uint IconImage { get; private set; }
        public bool Bald { get; private set; }
        public uint AlternateSetup { get; private set; }
        public ObjDesc ObjDesc { get; } = new ObjDesc();

        public void Unpack(BinaryReader reader)
        {
            IconImage       = reader.ReadUInt32();
            Bald            = (reader.ReadByte() == 1);
            AlternateSetup  = reader.ReadUInt32();

            ObjDesc.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(IconImage);
            writer.Write((byte)(Bald ? 1 : 0));
            writer.Write(AlternateSetup);
            ObjDesc.Pack(writer);
        }
    }
}
