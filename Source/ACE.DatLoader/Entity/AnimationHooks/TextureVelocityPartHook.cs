using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class TextureVelocityPartHook : AnimationHook
    {
        public uint PartIndex { get; private set; }
        public float USpeed { get; private set; }
        public float VSpeed { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(PartIndex);
            writer.Write(USpeed);
            writer.Write(VSpeed);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            PartIndex   = reader.ReadUInt32();
            USpeed      = reader.ReadSingle();
            VSpeed      = reader.ReadSingle();
        }
    }
}
