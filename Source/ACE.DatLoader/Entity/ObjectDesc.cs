using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ObjectDesc : IUnpackable, IPackable
    {
        public uint ObjId { get; private set; }
        public Frame BaseLoc { get; } = new Frame();
        public float Freq { get; set; }
        public float DisplaceX { get; private set; }
        public float DisplaceY { get; private set; }
        public float MinScale { get; private set; }
        public float MaxScale { get; private set; }
        public float MaxRotation { get; private set; }
        public float MinSlope { get; private set; }
        public float MaxSlope { get; private set; }
        public uint Align { get; private set; }
        public uint Orient { get; private set; }
        public uint WeenieObj { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            ObjId       = reader.ReadUInt32();

            BaseLoc.Unpack(reader);

            Freq        = reader.ReadSingle();

            DisplaceX   = reader.ReadSingle();
            DisplaceY   = reader.ReadSingle();

            MinScale    = reader.ReadSingle();
            MaxScale    = reader.ReadSingle();

            MaxRotation = reader.ReadSingle();

            MinSlope    = reader.ReadSingle();
            MaxSlope    = reader.ReadSingle();

            Align       = reader.ReadUInt32();
            Orient      = reader.ReadUInt32();

            WeenieObj   = reader.ReadUInt32();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(ObjId);

            BaseLoc.Pack(writer);

            writer.Write(Freq);
            writer.Write(DisplaceX);
            writer.Write(DisplaceY);
            writer.Write(MinScale);
            writer.Write(MaxScale);
            writer.Write(MaxRotation);
            writer.Write(MinSlope);
            writer.Write(MaxSlope);
            writer.Write(Align);
            writer.Write(Orient);
            writer.Write(WeenieObj);
        }

        public void Remap(System.Func<uint, uint> map)
        {
            ObjId = map(ObjId);
        }
    }
}
