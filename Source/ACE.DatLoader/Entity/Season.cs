using System.IO;

namespace ACE.DatLoader.Entity
{
    public class Season : IUnpackable
    {
        public uint StartDate { get; private set; }
        public string Name { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            StartDate = reader.ReadUInt32();
            Name = reader.ReadPString();
            reader.AlignBoundary();
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(StartDate);
            writer.WritePString(Name);
            writer.AlignBoundary();
        }
    }
}
