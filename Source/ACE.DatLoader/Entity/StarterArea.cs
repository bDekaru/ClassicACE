using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class StarterArea : IUnpackable, IPackable
    {
        public string Name { get; set; }
        public List<Position> Locations { get; } = new List<Position>();

        public void Unpack(BinaryReader reader)
        {
            Name = reader.ReadString();

            Locations.UnpackSmartArray(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Name);
            Locations.PackSmartArray(writer);
        }
    }
}
