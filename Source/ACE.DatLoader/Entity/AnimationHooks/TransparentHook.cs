using System.IO;

using ACE.Entity.Enum;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class TransparentHook : AnimationHook
    {
        public float Start { get; private set; }
        public float End { get; private set; }
        public float Time { get; private set; }

        public static TransparentHook Create(AnimationHookDir direction, float start, float end, float time)
        {
            return new TransparentHook { HookType = AnimationHookType.Transparent, Direction = direction, Start = start, End = end, Time = time };
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(Start);
            writer.Write(End);
            writer.Write(Time);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            Start   = reader.ReadSingle();
            End     = reader.ReadSingle();
            Time    = reader.ReadSingle();
        }
    }
}
