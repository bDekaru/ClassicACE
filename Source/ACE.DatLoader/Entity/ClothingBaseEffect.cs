using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ClothingBaseEffect : IUnpackable
    {
        public List<CloObjectEffect> CloObjectEffects { get; } = new List<CloObjectEffect>();

        public void Pack(BinaryWriter writer)
        {
            writer.Write((uint)CloObjectEffects.Count);
            foreach (var effect in CloObjectEffects)
                effect.Pack(writer);
        }

        public void Unpack(BinaryReader reader)
        {
            CloObjectEffects.Unpack(reader);
        }
    }
}
