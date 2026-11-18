using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace ACE.DatLoader.Entity
{
    public class BaseProperty : IUnpackable
    {
        // If this is in a collection/hashtable, this will actually match the key in that table.
        // The enum value of this can be looked up in MasterProperty
        public uint Id;

        public BasePropertyType PropertyType; // This is read from the MasterProperty table

        // Depending on the PropertyType, one of these values will be populated
        public uint ValueEnum;
        public bool ValueBool;
        public uint ValueDataFile;
        public float ValueFloat;
        public StringInfo ValueString;
        public uint ValueColor;
        public List<BaseProperty> ValueArray;
        public int ValueInt;
        public Dictionary<uint, BaseProperty> ValueStruct;
        public Vector3 ValueVector;
        public uint ValueBitfield32;
        public ulong ValueBitfield64;
        public uint ValueInstanceId;

        // The first byte of the packed table of a Struct value, kept so that a record is written back as it was read.
        public byte ValueStructBucketSize;

        /// <summary>The property type of a property id, as the MasterProperty table of the portal dat defines it.</summary>
        public static BasePropertyType DefaultTypeLookup(uint id)
        {
            if (!DatManager.PortalDat.MasterProperty.m_properties.ContainsKey(id))
                throw (new Exception("Unable to locate BaseProperty type in the MasterProperty table"));

            return DatManager.PortalDat.MasterProperty.m_properties[id].m_propertyType;
        }

        public void Unpack(BinaryReader reader)
        {
            Unpack(reader, DefaultTypeLookup);
        }

        /// <param name="typeLookup">Gives the property type of a property id (see <see cref="DefaultTypeLookup"/>), for a caller that has no portal dat loaded in <see cref="DatManager"/>.</param>
        public void Unpack(BinaryReader reader, Func<uint, BasePropertyType> typeLookup)
        {
            Id = reader.ReadUInt32();

            PropertyType = typeLookup(Id);
            switch(PropertyType)
            {
                case BasePropertyType.Enum:
                    ValueEnum = reader.ReadUInt32();
                    break;
                case BasePropertyType.Bool:
                    ValueBool = reader.ReadBoolean();
                    break;
                case BasePropertyType.DataFile:
                    ValueDataFile = reader.ReadUInt32();
                    break;
                case BasePropertyType.Float:
                    ValueFloat = reader.ReadSingle();
                    break;
                case BasePropertyType.StringInfo:
                    ValueString = new StringInfo();
                    ValueString.Unpack(reader);
                    break;
                case BasePropertyType.Color:
                    ValueColor = reader.ReadUInt32();
                    break;
                case BasePropertyType.Array:
                    ValueArray = new List<BaseProperty>();

                    var totalItems = reader.ReadUInt32();
                    for (int i = 0; i < totalItems; i++)
                    {
                        var element = new BaseProperty();
                        element.Unpack(reader, typeLookup);
                        ValueArray.Add(element);
                    }
                    break;
                case BasePropertyType.Integer:
                    ValueInt = reader.ReadInt32();
                    break;
                case BasePropertyType.Struct:
                    ValueStruct = new Dictionary<uint, BaseProperty>();

                    // This packed list uses a little different format not handled in the unpackable extensions
                    ValueStructBucketSize = reader.ReadByte();
                    var totalObjects = reader.ReadByte();
                    for (int i = 0; i < totalObjects; i++)
                    {
                        var key = reader.ReadUInt32();

                        var item = new BaseProperty();
                        item.Unpack(reader, typeLookup);
                        ValueStruct.Add(key, item);
                    }
                    break;
                case BasePropertyType.String:
                    // No use cases in end-of-retail Dat files.
                    // While there are a couple of MasterProperty entries of this type, there are no BaseProperty entries that use them.
                    break;
                case BasePropertyType.Vector:
                    ValueVector = reader.ReadVector3();
                    break;
                case BasePropertyType.Bitfield32:
                    ValueBitfield32 = reader.ReadUInt32();
                    break;
                case BasePropertyType.Bitfield64:
                    ValueBitfield64 = reader.ReadUInt64();
                    break;
                case BasePropertyType.InstanceID:
                    ValueInstanceId = reader.ReadUInt32();
                    break;
                default:
                    // Should never hit this!
                    throw new NotImplementedException();
                    //break;
            }


        }

        /// <summary>Writes the property as <see cref="Unpack(BinaryReader, Func{uint, BasePropertyType})"/> reads it; the value written depends on <see cref="PropertyType"/>.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(Id);

            switch (PropertyType)
            {
                case BasePropertyType.Enum:
                    writer.Write(ValueEnum);
                    break;
                case BasePropertyType.Bool:
                    writer.Write(ValueBool);
                    break;
                case BasePropertyType.DataFile:
                    writer.Write(ValueDataFile);
                    break;
                case BasePropertyType.Float:
                    writer.Write(ValueFloat);
                    break;
                case BasePropertyType.StringInfo:
                    ValueString.Pack(writer);
                    break;
                case BasePropertyType.Color:
                    writer.Write(ValueColor);
                    break;
                case BasePropertyType.Array:
                    writer.Write((uint)ValueArray.Count);
                    foreach (var element in ValueArray)
                        element.Pack(writer);
                    break;
                case BasePropertyType.Integer:
                    writer.Write(ValueInt);
                    break;
                case BasePropertyType.Struct:
                    writer.Write(ValueStructBucketSize);
                    writer.Write((byte)ValueStruct.Count);
                    foreach (var item in ValueStruct)
                    {
                        writer.Write(item.Key);
                        item.Value.Pack(writer);
                    }
                    break;
                case BasePropertyType.String:
                    break;
                case BasePropertyType.Vector:
                    writer.Write(ValueVector.X);
                    writer.Write(ValueVector.Y);
                    writer.Write(ValueVector.Z);
                    break;
                case BasePropertyType.Bitfield32:
                    writer.Write(ValueBitfield32);
                    break;
                case BasePropertyType.Bitfield64:
                    writer.Write(ValueBitfield64);
                    break;
                case BasePropertyType.InstanceID:
                    writer.Write(ValueInstanceId);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
