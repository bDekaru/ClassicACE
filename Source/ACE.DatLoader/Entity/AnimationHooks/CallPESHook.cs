using System;
using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class CallPESHook : AnimationHook
    {
        public uint PES { get; private set; }
        public float Pause { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);

            writer.Write(PES);
            writer.Write(Pause);
        }

        public override void Remap(Func<uint, uint> map)
        {
            PES = map(PES);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            PES     = reader.ReadUInt32();
            Pause   = reader.ReadSingle();
        }
    }
}
