using ACE.DatLoader.Entity;
using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are in the client_local_English.dat in the 0x21 range and have details on different UI Elements
    /// The enum for these LayoutDesc can be looked up in DidMapper file 0x2500000E
    /// </summary>
    [DatFileType(DatFileType.UiLayout)]
    public class LayoutDesc : FileType
    {
        public int DisplayWidth;
        public int DisplayHeight;

        // Enum name for the Keys can be looked up in EnumMapper file 0x2200001B
        public Dictionary<uint, ElementDesc> Elements = new Dictionary<uint, ElementDesc>();

        // First byte of the packed table of the elements, kept so that a record is written back as it was read.
        public byte ElementsBucketSize;

        public override void Unpack(BinaryReader reader)
        {
            Unpack(reader, BaseProperty.DefaultTypeLookup);
        }

        /// <param name="typeLookup">Gives the property type of a property id (see <see cref="BaseProperty.DefaultTypeLookup"/>), for a caller that has no portal dat loaded in <see cref="DatManager"/>.</param>
        public void Unpack(BinaryReader reader, Func<uint, BasePropertyType> typeLookup)
        {
            Id = reader.ReadUInt32();

            DisplayWidth = reader.ReadInt32();
            DisplayHeight = reader.ReadInt32();

            ElementsBucketSize = reader.ReadByte();
            var totalObjects = reader.ReadByte();
            for (int i = 0; i < totalObjects; i++)
            {
                var key = reader.ReadUInt32();

                var item = new ElementDesc();
                item.Unpack(reader, typeLookup);
                Elements.Add(key, item);
            }
        }

        /// <summary>Writes the record as <see cref="Unpack(BinaryReader, Func{uint, BasePropertyType})"/> reads it, with the elements in the order of <see cref="Elements"/>.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write(DisplayWidth);
                writer.Write(DisplayHeight);

                writer.Write(ElementsBucketSize);
                writer.Write((byte)Elements.Count);
                foreach (var element in Elements)
                {
                    writer.Write(element.Key);
                    element.Value.Pack(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
