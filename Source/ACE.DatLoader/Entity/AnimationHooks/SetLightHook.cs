using System.IO;

namespace ACE.DatLoader.Entity.AnimationHooks
{
    public class SetLightHook : AnimationHook
    {
        public int LightsOn { get; private set; }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);

            writer.Write(LightsOn);
        }

        public override void Unpack(BinaryReader reader)
        {
            base.Unpack(reader);

            LightsOn = reader.ReadInt32();
        }
    }
}
