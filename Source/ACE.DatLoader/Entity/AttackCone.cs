using System.IO;

namespace ACE.DatLoader.Entity
{
    public class AttackCone : IUnpackable
    {
        public uint PartIndex { get; set; }
        
        // these Left and Right are technically Vec2D types
        public float LeftX { get; private set; }
        public float LeftY { get; private set; }

        public float RightX { get; private set; }
        public float RightY { get; private set; }

        public float Radius { get; private set; }
        public float Height { get; private set; }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(PartIndex);
            writer.Write(LeftX);
            writer.Write(LeftY);
            writer.Write(RightX);
            writer.Write(RightY);
            writer.Write(Radius);
            writer.Write(Height);
        }

        public void Unpack(BinaryReader reader)
        {
            PartIndex   = reader.ReadUInt32();

            LeftX       = reader.ReadSingle();
            LeftY       = reader.ReadSingle();

            RightX      = reader.ReadSingle();
            RightY      = reader.ReadSingle();
            
            Radius      = reader.ReadSingle();
            Height      = reader.ReadSingle();
        }
    }
}
