using System.IO;
using ACE.Entity.Enum.Properties;

namespace ACE.DatLoader.Entity
{
    public class SkillFormula : IUnpackable
    {
        public uint W;
        public uint X;
        public uint Y;
        public uint Z;
        public uint Attr1;
        public uint Attr2;

        public void Unpack(BinaryReader reader)
        {
            W = reader.ReadUInt32();
            X = reader.ReadUInt32();
            Y = reader.ReadUInt32();
            Z = reader.ReadUInt32();

            Attr1 = reader.ReadUInt32();
            Attr2 = reader.ReadUInt32();
        }

        public SkillFormula() { }

        public SkillFormula(PropertyAttribute attr1, PropertyAttribute attr2, uint divisor)
        {
            Attr1 = (uint)attr1;
            Attr2 = (uint)attr2;
            Z = divisor;
            X = 1;
            if (attr2 != PropertyAttribute.Undef)
                Y = 1;
        }

        public SkillFormula(PropertyAttribute attr1, uint attr1Multiplier, PropertyAttribute attr2, uint attr2Multiplier, uint divisor, uint addition)
        {
            Attr1 = (uint)attr1;
            X = attr1Multiplier;
            Attr2 = (uint)attr2;
            Y = attr2Multiplier;
            Z = divisor;
            W = addition;
        }
    }
}
