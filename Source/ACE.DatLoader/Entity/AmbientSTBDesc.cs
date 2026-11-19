using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class AmbientSTBDesc : IUnpackable
    {
        public uint STBId { get; private set; }
        public List<AmbientSoundDesc> AmbientSounds { get; } = new List<AmbientSoundDesc>();

        public void Unpack(BinaryReader reader)
        {
            STBId = reader.ReadUInt32();

            AmbientSounds.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(STBId);

            writer.Write((uint)AmbientSounds.Count);

            foreach (var item in AmbientSounds)
                item.Pack(writer);
        }
    }
}
