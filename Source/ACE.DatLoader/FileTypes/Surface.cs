using System.IO;
using ACE.Entity.Enum;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x08.
    /// As the name implies this contains surface info for an object. Either texture reference or color and whatever effects applied to it.
    /// </summary>
    [DatFileType(DatFileType.Surface)]
    public class Surface : FileType
    {
        public SurfaceType Type { get; private set; }
        public uint OrigTextureId { get; private set; }
        public uint OrigPaletteId { get; private set; }
        public uint ColorValue { get; private set; }
        public float Translucency { get; private set; }
        public float Luminosity { get; private set; }
        public float Diffuse { get; private set; }

        public override void Unpack(BinaryReader reader)
        {
            Type = (SurfaceType)reader.ReadUInt32();

            if (Type.HasFlag(SurfaceType.Base1Image) || Type.HasFlag(SurfaceType.Base1ClipMap))
            {
                // image or clipmap
                OrigTextureId = reader.ReadUInt32();
                OrigPaletteId = reader.ReadUInt32();
            }
            else
            {
                // solid color
                ColorValue = reader.ReadUInt32();
            }

            Translucency    = reader.ReadSingle();
            Luminosity      = reader.ReadSingle();
            Diffuse         = reader.ReadSingle();
        }

        /// <summary>Writes the record in the layout <see cref="Unpack"/> reads (the portal record carries no id prefix).</summary>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write((uint)Type);

            if (Type.HasFlag(SurfaceType.Base1Image) || Type.HasFlag(SurfaceType.Base1ClipMap))
            {
                writer.Write(OrigTextureId);
                writer.Write(OrigPaletteId);
            }
            else
                writer.Write(ColorValue);

            writer.Write(Translucency);
            writer.Write(Luminosity);
            writer.Write(Diffuse);
        }

        /// <summary>Replaces the referenced texture and palette ids (when set) by mapTexture(id) and mapPalette(id).</summary>
        public void Remap(System.Func<uint, uint> mapTexture, System.Func<uint, uint> mapPalette)
        {
            if (OrigTextureId != 0)
                OrigTextureId = mapTexture(OrigTextureId);

            if (OrigPaletteId != 0)
                OrigPaletteId = mapPalette(OrigPaletteId);
        }
    }
}
