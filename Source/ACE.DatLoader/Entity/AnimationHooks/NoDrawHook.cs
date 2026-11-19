using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class NoDrawHook : AnimationHook
    {
        public uint NoDraw { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(NoDraw);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            NoDraw = reader.ReadUInt32();
        }
    }
}
