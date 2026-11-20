using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE.DatLoader.Entity
{
    public class Attribute2ndBase : IUnpackable, IPackable
    {
        public SkillFormula Formula { get; set; } = new SkillFormula();

        public void Unpack(BinaryReader reader)
        {
            Formula.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            Formula.Pack(writer);
        }
    }
}
