using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ClothingBaseEffect : IUnpackable, IPackable
    {
        public List<CloObjectEffect> CloObjectEffects { get; } = new List<CloObjectEffect>();

        public void Pack(BinaryWriter writer)
        {
            CloObjectEffects.Pack(writer);
        }

        public void Unpack(BinaryReader reader)
        {
            CloObjectEffects.Unpack(reader);
        }
    }
}
