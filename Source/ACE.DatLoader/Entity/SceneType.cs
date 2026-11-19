using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SceneType : IUnpackable
    {
        public uint StbIndex { get; private set; }
        public List<uint> Scenes { get; } = new List<uint>();

        public void Unpack(BinaryReader reader)
        {
            StbIndex = reader.ReadUInt32();

            Scenes.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(StbIndex);

            writer.Write(Scenes.Count);

            foreach (var item in Scenes)
                writer.Write(item);
        }
    }
}
