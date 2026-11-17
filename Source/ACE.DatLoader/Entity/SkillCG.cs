using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkillCG : IUnpackable
    {
        public uint SkillNum { get; set; }
        public int NormalCost { get; set; }
        public int PrimaryCost { get; set; }

        public void Unpack(BinaryReader reader)
        {
            SkillNum    = reader.ReadUInt32();
            NormalCost  = reader.ReadInt32();
            PrimaryCost = reader.ReadInt32();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(SkillNum);
            writer.Write(NormalCost);
            writer.Write(PrimaryCost);
        }
    }
}
