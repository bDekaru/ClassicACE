using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class DefaultScriptPartHook : AnimationHook
    {
        public uint PartIndex { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(PartIndex);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            PartIndex = reader.ReadUInt32();
        }
    }
}
