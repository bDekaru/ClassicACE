using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x04. 
    /// </summary>
    [DatFileType(DatFileType.Palette)]
    public class Palette : FileType
    {
        /// <summary>
        /// Color data is stored in ARGB format
        /// </summary>
        public List<uint> Colors { get; } = new List<uint>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            Colors.Unpack(reader);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            Colors.Pack(writer);
        }

        /// <summary>
        /// A palette with the given id in which every color of this palette is repeated <paramref name="times"/> times in a row, so that the index i of
        /// this palette becomes the index i * times.
        /// </summary>
        public Palette Repeated(uint id, int times)
        {
            var result = new Palette { Id = id };

            foreach (var color in Colors)
                for (var i = 0; i < times; i++)
                    result.Colors.Add(color);

            return result;
        }
    }
}
