using System.IO;

namespace ACE.DatLoader.Entity
{
    public class PhysicsScriptData : IUnpackable, IPackable
    {
        public double StartTime { get; private set; }
        public AnimationHook Hook { get; private set; } = new AnimationHook();

        public static PhysicsScriptData Create(double startTime, AnimationHook hook)
        {
            return new PhysicsScriptData { StartTime = startTime, Hook = hook };
        }

        public void Unpack(BinaryReader reader)
        {
            StartTime = reader.ReadDouble();

            Hook = AnimationHook.ReadHook(reader);
        }

        public void Remap(System.Func<uint, uint> map)
        {
            Hook.Remap(map);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(StartTime);

            Hook.Pack(writer);
        }
    }
}
