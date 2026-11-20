using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace ACE.DatLoader.Entity
{
    public class StateDesc : IUnpackable, IPackable
    {

        // Enum name for the StateId can be looked up in EnumMapper file 0x2200001C
        public uint StateId;
        public bool PassToChildren;
        public IncorporationFlags UiIncorporationFlags;
        public Dictionary<uint, BaseProperty> Properties = new Dictionary<uint, BaseProperty>();
        public byte PropertiesBucketSize; // first byte of the packed table, kept so that a record is written back as it was read
        public List<MediaDesc> Media = new List<MediaDesc>();

        public void Unpack(BinaryReader reader)
        {
            Unpack(reader, BaseProperty.DefaultTypeLookup);
        }

        /// <param name="typeLookup">Gives the property type of a property id (see <see cref="BaseProperty.DefaultTypeLookup"/>).</param>
        public virtual void Unpack(BinaryReader reader, Func<uint, BasePropertyType> typeLookup)
        {
            StateId = reader.ReadUInt32();
            PassToChildren = reader.ReadBoolean();
            UiIncorporationFlags = (IncorporationFlags)reader.ReadUInt32();

            PropertiesBucketSize = reader.ReadByte();
            var totalObjects = reader.ReadByte();
            for (int i = 0; i < totalObjects; i++)
            {
                var key = reader.ReadUInt32();

                var item = new BaseProperty();
                item.Unpack(reader, typeLookup);
                Properties.Add(key, item);
            }

            var totalMedia = reader.ReadByte();
            for(var i  = 0; i < totalMedia; i++)
            {
                var item = new MediaDesc();
                item.Unpack(reader);
                Media.Add(item);
            }
        }

        public virtual void Pack(BinaryWriter writer)
        {
            writer.Write(StateId);
            writer.Write(PassToChildren);
            writer.Write((uint)UiIncorporationFlags);

            writer.Write(PropertiesBucketSize);
            writer.Write((byte)Properties.Count);
            foreach (var property in Properties)
            {
                writer.Write(property.Key);
                property.Value.Pack(writer);
            }

            writer.Write((byte)Media.Count);
            foreach (var media in Media)
                media.Pack(writer);
        }

    }
}
