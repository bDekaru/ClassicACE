using System.IO;

namespace ACE.DatLoader
{
    static class BinaryWriterExtensions
    {
        /// <summary>
        /// Pads the stream with zeros up to the next DWORD boundary (the counterpart of the reader's AlignBoundary).
        /// </summary>
        public static void AlignBoundary(this BinaryWriter writer)
        {
            var alignDelta = writer.BaseStream.Position % 4;

            if (alignDelta != 0)
                writer.Write(new byte[4 - alignDelta]);
        }

        /// <summary>
        /// Writes a string behind a UInt16 length (the counterpart of the reader's ReadPString).
        /// </summary>
        public static void WritePString(this BinaryWriter writer, string value)
        {
            var bytes = System.Text.Encoding.Default.GetBytes(value ?? "");

            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }
    }
}
