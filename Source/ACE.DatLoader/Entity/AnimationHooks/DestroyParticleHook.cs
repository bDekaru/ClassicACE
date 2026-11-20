using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class DestroyParticleHook : AnimationHook
    {
        public uint EmitterId { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);

            writer.Write(EmitterId);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            EmitterId = reader.ReadUInt32();
        }
    }
}
