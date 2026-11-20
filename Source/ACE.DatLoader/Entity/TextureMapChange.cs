using System.IO;

namespace ACE.DatLoader.Entity
{
    // TODO: refactor to merge with existing TextureMapOverride object
    public class TextureMapChange : IUnpackable, IPackable
    {
        public byte PartIndex { get; set; }
        public uint OldTexture { get; set; }
        public uint NewTexture { get; set; }

        public void Unpack(BinaryReader reader)
        {
            PartIndex   = reader.ReadByte();
            OldTexture  = reader.ReadAsDataIDOfKnownType(0x05000000);
            NewTexture  = reader.ReadAsDataIDOfKnownType(0x05000000);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(PartIndex);
            writer.WriteAsDataIDOfKnownType(0x05000000, OldTexture);
            writer.WriteAsDataIDOfKnownType(0x05000000, NewTexture);
        }
    }
}
