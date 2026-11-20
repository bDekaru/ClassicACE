using System.Collections.Generic;
using System.IO;
using System.Numerics;

using ACE.Entity.Enum;

namespace ACE.DatLoader.Entity
{
    public class MotionData : IUnpackable, IPackable
    {
        public byte Bitfield { get; private set; }
        public MotionDataFlags Flags { get; private set; }
        public List<AnimData> Anims { get; } = new List<AnimData>();
        public Vector3 Velocity { get; private set; }
        public Vector3 Omega { get; private set; }

        public void Pack(BinaryWriter writer)
        {
            writer.Write((byte)Anims.Count);
            writer.Write(Bitfield);
            writer.Write((byte)Flags);
            writer.AlignBoundary();

            foreach (var anim in Anims)
                anim.Pack(writer);

            if ((Flags & MotionDataFlags.HasVelocity) != 0)
            {
                writer.WriteVector3(Velocity);
            }

            if ((Flags & MotionDataFlags.HasOmega) != 0)
            {
                writer.WriteVector3(Omega);
            }
        }

        public void Unpack(BinaryReader reader)
        {
            var numAnims    = reader.ReadByte();
            Bitfield        = reader.ReadByte();
            Flags           = (MotionDataFlags)reader.ReadByte();
            reader.AlignBoundary();

            Anims.Unpack(reader, numAnims);

            if ((Flags & MotionDataFlags.HasVelocity) != 0)
                Velocity = reader.ReadVector3();

            if ((Flags & MotionDataFlags.HasOmega) != 0)
                Omega = reader.ReadVector3();
        }
    }
}
