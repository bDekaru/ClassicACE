using System.Collections.Generic;
using System.IO;
using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// DbProperty files are 0x78 in the client_portal.dat
    /// Some basic UI related items related to window Opacity.
    /// </summary>
    [DatFileType(DatFileType.DbProperties)]
    public class DbProperty : FileType
    {
        Dictionary<uint, BaseProperty> PropertyCollection = new Dictionary<uint, BaseProperty>();

        private byte bucketSize;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            // This packed list uses a little different format not handled in the unpackable extensions
            bucketSize = reader.ReadByte();
            var totalObjects = reader.ReadByte();
            for (int i = 0; i < totalObjects; i++)
            {
                var key = reader.ReadUInt32();

                var item = new BaseProperty();
                item.Unpack(reader);
                PropertyCollection.Add(key, item);
            }
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);

            writer.Write(bucketSize);
            writer.Write((byte)PropertyCollection.Count);
            PropertyCollection.PackFixedQuantity(writer);
        }
    }
}
