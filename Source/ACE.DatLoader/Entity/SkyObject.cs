using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyObject : IUnpackable, IPackable
    {
        public float BeginTime { get; private set; }
        public float EndTime { get; private set; }
        public float BeginAngle { get; private set; }
        public float EndAngle { get; private set; }
        public float TexVelocityX { get; private set; }
        public float TexVelocityY { get; private set; }
        public float TexVelocityZ { get; } = 0;
        public uint DefaultGFXObjectId { get; private set; }
        public uint DefaultPESObjectId { get; private set; }
        public uint Properties { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            UnpackPreToD(reader);

            Properties = reader.ReadUInt32();

            reader.AlignBoundary();
        }

        /// <summary>Reads the pre-ToD (2005) layout: the same fields without <see cref="Properties"/>, which stays 0.</summary>
        public void UnpackPreToD(BinaryReader reader)
        {
            BeginTime           = reader.ReadSingle();
            EndTime             = reader.ReadSingle();
            BeginAngle          = reader.ReadSingle();
            EndAngle            = reader.ReadSingle();
            TexVelocityX        = reader.ReadSingle();
            TexVelocityY        = reader.ReadSingle();
            DefaultGFXObjectId  = reader.ReadUInt32();
            DefaultPESObjectId  = reader.ReadUInt32();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(BeginTime);
            writer.Write(EndTime);
            writer.Write(BeginAngle);
            writer.Write(EndAngle);
            writer.Write(TexVelocityX);
            writer.Write(TexVelocityY);
            writer.Write(DefaultGFXObjectId);
            writer.Write(DefaultPESObjectId);
            writer.Write(Properties);
            writer.AlignBoundary();
        }
    }
}
