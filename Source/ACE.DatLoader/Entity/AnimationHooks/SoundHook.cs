using System;
using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class SoundHook : AnimationHook
    {
        public uint Id { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);

            writer.Write(Id);
        }

        public override void Remap(Func<uint, uint> map)
        {
            Id = map(Id);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            Id = reader.ReadUInt32();
        }
    }
}
