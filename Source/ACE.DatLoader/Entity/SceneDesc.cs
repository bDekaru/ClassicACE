using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SceneDesc : IUnpackable, IPackable
    {
        public List<SceneType> SceneTypes { get; } = new List<SceneType>();

        public void Unpack(BinaryReader reader)
        {
            SceneTypes.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            SceneTypes.Pack(writer);
        }
    }
}
