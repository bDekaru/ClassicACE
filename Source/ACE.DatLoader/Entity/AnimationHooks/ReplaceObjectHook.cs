using System;
using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class ReplaceObjectHook : AnimationHook
    {
        public AnimationPartChange APChange { get; } = new AnimationPartChange();

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            APChange.Pack(writer);
            writer.AlignBoundary();
        }

        public override void Remap(Func<uint, uint> map)
        {
            APChange.PartID = map(APChange.PartID);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            APChange.Unpack(reader);
            reader.AlignBoundary();
        }
    }
}
