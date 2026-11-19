using System.IO;

namespace ACE.DatLoader.Entity
{
    public class CloTextureEffect : IUnpackable
    {
        /// <summary>
        /// Texture portal.dat 0x05000000
        /// </summary>
        public uint OldTexture { get; set; }

        /// <summary>
        /// Texture portal.dat 0x05000000
        /// </summary>
        public uint NewTexture { get; set; }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(OldTexture);
            writer.Write(NewTexture);
        }

        public void Unpack(BinaryReader reader)
        {
            OldTexture = reader.ReadUInt32();
            NewTexture = reader.ReadUInt32();
        }
    }
}
