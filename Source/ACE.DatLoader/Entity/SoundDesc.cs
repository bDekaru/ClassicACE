using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SoundDesc : IUnpackable, IPackable
    {
        public List<AmbientSTBDesc> STBDesc { get; } = new List<AmbientSTBDesc>();

        public void Unpack(BinaryReader reader)
        {
            STBDesc.Unpack(reader);
        }

        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var stb in STBDesc)
                stb.Remap(map);
        }

        public void Pack(BinaryWriter writer)
        {
            STBDesc.Pack(writer);
        }
    }
}
