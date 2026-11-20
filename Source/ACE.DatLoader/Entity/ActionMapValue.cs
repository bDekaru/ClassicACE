using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ActionMapValue : IUnpackable, IPackable
    {
        public byte UnknownByte { get; private set; }
        public uint UnknownInt { get; private set; }
        public uint UnknownInt2 { get; private set; }
        public uint ToggleType { get; private set; }
        public UserBindingValue UserBindingData { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            UnknownByte = reader.ReadByte();
            UnknownInt = reader.ReadUInt32();
            UnknownInt2 = reader.ReadUInt32();
            ToggleType = reader.ReadUInt32();
            UserBindingData = new UserBindingValue();
            UserBindingData.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(UnknownByte);
            writer.Write(UnknownInt);
            writer.Write(UnknownInt2);
            writer.Write(ToggleType);
            UserBindingData.Pack(writer);
        }
    }
}
