using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class CloObjectEffect : IUnpackable
    {
        public uint Index { get; private set; }
        public uint ModelId { get; set; }
        public List<CloTextureEffect> CloTextureEffects { get; } = new List<CloTextureEffect>();

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(ModelId);
            writer.Write((uint)CloTextureEffects.Count);
            foreach (var effect in CloTextureEffects)
                effect.Pack(writer);
        }

        public void Unpack(BinaryReader reader)
        {
            Index   = reader.ReadUInt32();
            ModelId = reader.ReadUInt32();

            CloTextureEffects.Unpack(reader);
        }
    }
}
