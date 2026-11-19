using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x0F. 
    /// They contain, as the name may imply, a set of palettes (0x04 files)
    /// </summary>
    [DatFileType(DatFileType.PaletteSet)]
    public class PaletteSet : FileType
    {
        public List<uint> PaletteList { get; } = new List<uint>();

        /// <summary>Writes the record in the layout <see cref="Unpack"/> reads.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write(PaletteList.Count);
                foreach (var palette in PaletteList)
                    writer.Write(palette);

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Replaces every palette id by map(id).</summary>
        public void Remap(Func<uint, uint> map)
        {
            for (var i = 0; i < PaletteList.Count; i++)
                PaletteList[i] = map(PaletteList[i]);
        }

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            PaletteList.Unpack(reader);
        }

        /// <summary>
        /// Returns the palette ID (uint, 0x04 file) from the Palette list based on the corresponding hue
        /// Hue is mostly (only?) used in Character Creation data.
        /// "Hue" referred to as "shade" in acclient.c
        /// </summary>
        public uint GetPaletteID(double hue)
        {
            // Make sure the PaletteList has valid data and the hue is within valid ranges
            if (PaletteList.Count == 0 || hue < 0 || hue > 1)
                return 0;

            // This was the original function - had an issue specifically with Aerfalle's Pallium, WCID 8133
            // int palIndex = Convert.ToInt32(Convert.ToDouble(PaletteList.Count - 0.000001) * hue); // Taken from acclient.c (PalSet::GetPaletteID)

            // Hue is stored in DB as a percent of the total, so do some math to figure out the int position
            int palIndex = (int)((PaletteList.Count - 0.000001) * hue); // Taken from acclient.c (PalSet::GetPaletteID)

            // Since the hue numbers are a little odd, make sure we're in the bounds.
            if (palIndex < 0)
                palIndex = 0;

            if (palIndex > PaletteList.Count - 1)
                palIndex = PaletteList.Count - 1;

            return PaletteList[palIndex];
        }
    }
}
