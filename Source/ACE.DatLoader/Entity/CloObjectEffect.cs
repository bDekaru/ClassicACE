using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class CloObjectEffect : IUnpackable, IPackable
    {
        public uint Index { get; private set; }
        public uint ModelId { get; set; }
        public List<CloTextureEffect> CloTextureEffects { get; } = new List<CloTextureEffect>();

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(ModelId);
            CloTextureEffects.Pack(writer);
        }

        public void Unpack(BinaryReader reader)
        {
            Index   = reader.ReadUInt32();
            ModelId = reader.ReadUInt32();

            CloTextureEffects.Unpack(reader);
        }
    }
}
