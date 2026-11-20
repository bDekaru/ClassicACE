using ACE.DatLoader.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.MasterProperty)]
    public class MasterProperty : FileType
    {
        internal const uint FILE_ID = 0x39000001;

        public Dictionary<uint, string> m_emapper { get; } = new Dictionary<uint, string>();
        public Dictionary<uint, BasePropertyDesc> m_properties { get; } = new Dictionary<uint, BasePropertyDesc>();

        private uint unknown1;
        private uint unknown2;
        private byte enumBucketSize;
        private byte propertiesBucketSize;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            unknown1 = reader.ReadUInt32(); // 0, Unknown
            unknown2 = reader.ReadUInt32(); // 0, Unknown

            enumBucketSize = reader.ReadByte(); // = 5, Unknown (Bucket?)

            uint numEnums = reader.ReadCompressedUInt32();
            for(var i = 0; i < numEnums; i++)
            {
                var key = reader.ReadUInt32();
                var value = reader.ReadPString(1);
                m_emapper.Add(key, value);
            }

            propertiesBucketSize = reader.ReadByte(); // = 6, Unknown (Bucket?)

            m_properties.UnpackSmartArray(reader);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);

            writer.Write(unknown1);
            writer.Write(unknown2);

            writer.Write(enumBucketSize);

            writer.WriteCompressedUInt32((uint)m_emapper.Count);
            foreach (var entry in m_emapper)
            {
                writer.Write(entry.Key);
                writer.WritePString(entry.Value, 1);
            }

            writer.Write(propertiesBucketSize);

            m_properties.PackSmartArray(writer);
        }
    }
}
