using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.StringTable)]
    public class StringTable : FileType
    {
        public static uint CharacterTitle_FileID = 0x2300000E;

        public uint Language { get; set; } // This should always be 1 for English

        public byte Unknown { get; set; }

        public List<StringTableData> StringTableData { get; } = new List<StringTableData>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            Language = reader.ReadUInt32();

            Unknown = reader.ReadByte();

            StringTableData.UnpackSmartArray(reader);
        }

        /// <summary>Writes the record with the entries in the order of <see cref="StringTableData"/>.</summary>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Language);
            writer.Write(Unknown);
            writer.WriteCompressedUInt32((uint)StringTableData.Count);

            foreach (var entry in StringTableData)
                entry.Pack(writer);
        }
    }
}
