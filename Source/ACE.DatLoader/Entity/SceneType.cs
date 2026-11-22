using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SceneType : IUnpackable, IPackable
    {
        public uint StbIndex { get; private set; }
        public List<uint> Scenes { get; } = new List<uint>();

        public void Unpack(BinaryReader reader)
        {
            StbIndex = reader.ReadUInt32();

            Scenes.Unpack(reader);
        }

        public void Remap(System.Func<uint, uint> map)
        {
            for (var i = 0; i < Scenes.Count; i++)
                Scenes[i] = map.RemapIfSet(Scenes[i]);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(StbIndex);

            Scenes.Pack(writer);
        }
    }
}
