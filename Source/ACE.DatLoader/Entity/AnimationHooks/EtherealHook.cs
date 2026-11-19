using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class EtherealHook : AnimationHook
    {
        public int Ethereal { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(Ethereal);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            Ethereal = reader.ReadInt32();
        }
    }
}
