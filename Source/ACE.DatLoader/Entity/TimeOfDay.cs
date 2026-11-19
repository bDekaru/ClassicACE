using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TimeOfDay : IUnpackable
    {
        public float Start { get; private set; }
        public bool IsNight { get; private set; }
        public string Name { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            Start   = reader.ReadSingle();
            IsNight = (reader.ReadUInt32() == 1);
            Name    = reader.ReadPString();
            reader.AlignBoundary();
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(Start);
            writer.Write(IsNight ? 1u : 0u);
            writer.WritePString(Name);
            writer.AlignBoundary();
        }
    }
}
