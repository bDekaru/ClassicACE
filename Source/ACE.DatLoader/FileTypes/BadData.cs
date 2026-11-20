using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.BadData)]
    public class BadData : FileType
    {
        internal const uint FILE_ID = 0x0E00001A;

        // Key is a list of a WCIDs that are "bad" and should not exist. The value is always 1 (could be a bool?)
        public Dictionary<uint, uint> Bad = new Dictionary<uint, uint>();

        private ushort bucketSize;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            bucketSize = Bad.UnpackPackedHashTable(reader);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            Bad.PackPackedHashTable(writer, bucketSize);
        }
    }
}
