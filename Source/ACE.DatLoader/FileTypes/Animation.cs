using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;
using ACE.Entity.Enum;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x03. 
    /// Special thanks to Dan Skorupski for his work on Bael'Zharon's Respite, which helped fill in some of the gaps https://github.com/boardwalk/bzr
    /// </summary>
    [DatFileType(DatFileType.Animation)]
    public class Animation : FileType
    {
        public AnimationFlags Flags { get; private set; }
        public uint NumParts { get; private set; }
        public uint NumFrames { get; private set; }
        public List<Frame> PosFrames { get; } = new List<Frame>();
        public List<AnimationFrame> PartFrames { get; } = new List<AnimationFrame>();

        /// <summary>Replaces the dat ids the hooks refer to by map(id).</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var partFrame in PartFrames)
                foreach (var hook in partFrame.Hooks)
                    hook.Remap(map);
        }

        /// <summary>Writes the record in the layout <see cref="Unpack"/> reads.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write((uint)Flags);
                writer.Write(NumParts);
                writer.Write(NumFrames);

                if ((Flags & AnimationFlags.PosFrames) != 0)
                    foreach (var frame in PosFrames)
                        frame.Pack(writer);

                foreach (var partFrame in PartFrames)
                    partFrame.Pack(writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        public override void Unpack(BinaryReader reader)
        {
            Id          = reader.ReadUInt32();
            Flags       = (AnimationFlags)reader.ReadUInt32();
            NumParts    = reader.ReadUInt32();
            NumFrames   = reader.ReadUInt32();

            if ((Flags & AnimationFlags.PosFrames) != 0)
                PosFrames.Unpack(reader, NumFrames);

            for (uint i = 0; i < NumFrames; i++)
            {
                var animationFrame = new AnimationFrame();
                animationFrame.Unpack(reader, NumParts);
                PartFrames.Add(animationFrame);
            }
        }
    }
}
