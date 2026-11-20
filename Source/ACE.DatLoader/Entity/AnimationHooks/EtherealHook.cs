using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class EtherealHook : AnimationHook
    {
        public int Ethereal { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);

            writer.Write(Ethereal);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            Ethereal = reader.ReadInt32();
        }
    }
}
