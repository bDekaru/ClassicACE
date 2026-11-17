using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ObjDesc : IUnpackable
    {
        /// <summary>The leading byte of the record, kept so a rewritten ObjDesc equals the original.</summary>
        public byte Marker { get; private set; } = 0x11;

        public uint PaletteID { get; private set; }
        public List<SubPalette> SubPalettes { get; } = new List<SubPalette>();
        public List<TextureMapChange> TextureChanges { get; } = new List<TextureMapChange>();
        public List<AnimationPartChange> AnimPartChanges { get; } = new List<AnimationPartChange>();

        public void Unpack(BinaryReader reader)
        {
            reader.AlignBoundary();

            Marker = reader.ReadByte();

            var numPalettes             = reader.ReadByte();
            var numTextureMapChanges    = reader.ReadByte();
            var numAnimPartChanges      = reader.ReadByte();

            if (numPalettes > 0)
                PaletteID = reader.ReadAsDataIDOfKnownType(0x04000000);

            SubPalettes.Unpack(reader, numPalettes);
            TextureChanges.Unpack(reader, numTextureMapChanges);
            AnimPartChanges.Unpack(reader, numAnimPartChanges);

            reader.AlignBoundary();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.AlignBoundary();
            writer.Write(Marker);
            writer.Write((byte)SubPalettes.Count);
            writer.Write((byte)TextureChanges.Count);
            writer.Write((byte)AnimPartChanges.Count);

            if (SubPalettes.Count > 0)
                writer.WriteAsDataIDOfKnownType(0x04000000, PaletteID);

            foreach (var subPalette in SubPalettes)
                subPalette.Pack(writer);

            foreach (var textureChange in TextureChanges)
                textureChange.Pack(writer);

            foreach (var animPartChange in AnimPartChanges)
                animPartChange.Pack(writer);

            writer.AlignBoundary();
        }
    }
}
