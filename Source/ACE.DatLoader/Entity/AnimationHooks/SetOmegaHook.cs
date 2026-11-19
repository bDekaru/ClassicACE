using System.IO;
using System.Numerics;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class SetOmegaHook : AnimationHook
    {
        public Vector3 Axis { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)HookType);
            writer.Write((int)Direction);
            writer.Write(Axis.X);
            writer.Write(Axis.Y);
            writer.Write(Axis.Z);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            Axis = reader.ReadVector3();
        }
    }
}
