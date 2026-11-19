using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SceneDesc : IUnpackable
    {
        public List<SceneType> SceneTypes { get; } = new List<SceneType>();

        public void Unpack(BinaryReader reader)
        {
            SceneTypes.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write((uint)SceneTypes.Count);

            foreach (var item in SceneTypes)
                item.Pack(writer);
        }
    }
}
