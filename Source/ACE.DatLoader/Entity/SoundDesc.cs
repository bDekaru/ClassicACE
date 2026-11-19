using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SoundDesc : IUnpackable
    {
        public List<AmbientSTBDesc> STBDesc { get; } = new List<AmbientSTBDesc>();

        public void Unpack(BinaryReader reader)
        {
            STBDesc.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write((uint)STBDesc.Count);

            foreach (var item in STBDesc)
                item.Pack(writer);
        }
    }
}
