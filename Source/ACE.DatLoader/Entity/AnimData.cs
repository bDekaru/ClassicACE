using System.IO;

namespace ACE.DatLoader.Entity
{
    public class AnimData : IUnpackable
    {
        public uint AnimId { get; private set; }
        public int LowFrame { get; private set; }
        public int HighFrame { get; private set; }
        /// <summary>
        /// Negative framerates play animation in reverse
        /// </summary>
        public float Framerate { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            AnimId      = reader.ReadUInt32();
            LowFrame    = reader.ReadInt32();
            HighFrame   = reader.ReadInt32();
            Framerate   = reader.ReadSingle();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(AnimId);
            writer.Write(LowFrame);
            writer.Write(HighFrame);
            writer.Write(Framerate);
        }

        /// <summary>Replaces the animation id by map(id).</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            AnimId = map(AnimId);
        }

        public override string ToString()
        {
            return $"AnimId: {AnimId:X8}, LowFrame: {LowFrame}, HighFrame: {HighFrame}, FrameRate: {Framerate}";
        }
    }
}
