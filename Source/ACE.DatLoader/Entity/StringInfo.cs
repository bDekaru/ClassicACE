using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ACE.DatLoader.Entity
{
    public class StringInfo : IUnpackable
    {
        public byte m_strToken;

        // This is the Hashed string value, the key in the TableId
        public uint StringId;
        // The StringTable (0x23) to lookup the StringId in.
        public uint TableId;
        public byte m_Override;
        public byte unknown1;
        public byte unknown2;

        public List<string> Strings;

        public void Unpack(BinaryReader reader)
        {
            m_strToken = reader.ReadByte();
            StringId = reader.ReadUInt32();
            TableId = reader.ReadUInt32();
            m_Override = reader.ReadByte();
            unknown1 = reader.ReadByte();
            unknown2 = reader.ReadByte();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(m_strToken);
            writer.Write(StringId);
            writer.Write(TableId);
            writer.Write(m_Override);
            writer.Write(unknown1);
            writer.Write(unknown2);
        }
    }
}
