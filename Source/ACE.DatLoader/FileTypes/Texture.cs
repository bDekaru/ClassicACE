/************************************************************************
 * Some of the Bitmap/ExportTexture uses code taken from DerethForever.
 * http://www.derethforever.com
 *
 * DerethForever is licensed under the GNU General Public License
 * http://www.gnu.org/licenses/
 ************************************************************************/
using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.Texture)]
    public class Texture : FileType
    {
        public int Unknown { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public SurfacePixelFormat Format { get; set; }
        public int Length { get; set; }
        public byte[] SourceData { get; set; }
        public uint? DefaultPaletteId { get; set; }

        // Used to store a custom palette. Each Key represents the PaletteIndex and the Value is the color.
        // This is used if you want to apply a non-default Palette to the image prior to extraction
        public Dictionary<int, uint> CustomPaletteColors = new Dictionary<int, uint>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            Unknown = reader.ReadInt32();
            Width = reader.ReadInt32();
            Height = reader.ReadInt32();
            Format = (SurfacePixelFormat)reader.ReadUInt32();
            Length = reader.ReadInt32();

            SourceData = reader.ReadBytes(Length);

            switch (Format)
            {
                case SurfacePixelFormat.PFID_INDEX16:
                case SurfacePixelFormat.PFID_P8:
                    DefaultPaletteId = reader.ReadUInt32();
                    break;
                default:
                    DefaultPaletteId = null;
                    break;
            }
        }

        /// <summary>The value ToD records store in the field after the id (<see cref="Unknown"/>) for these images.</summary>
        public const int ToDHeaderValue = 6;

        /// <summary>
        /// Reads a pre-ToD (2005) image record of 24-bit pixels: id, width, height and the pixels in red, green, blue order. The result is a ToD image of
        /// the given format, whose pixels are stored blue, green, red (with an opaque alpha byte after them for A8R8G8B8).
        /// </summary>
        /// <exception cref="InvalidDataException">The record is not of that layout.</exception>
        /// <exception cref="NotSupportedException">The format is neither R8G8B8 nor A8R8G8B8.</exception>
        public static Texture UnpackPreToD(byte[] record, SurfacePixelFormat format = SurfacePixelFormat.PFID_R8G8B8, uint? id = null)
        {
            if (format != SurfacePixelFormat.PFID_R8G8B8 && format != SurfacePixelFormat.PFID_A8R8G8B8)
                throw new NotSupportedException($"Pre-ToD image records are converted to R8G8B8 or A8R8G8B8, not {format}.");

            if (record.Length < 12)
                throw new InvalidDataException("Pre-ToD image record is shorter than its header.");

            var width = BitConverter.ToInt32(record, 4);
            var height = BitConverter.ToInt32(record, 8);

            if (width <= 0 || height <= 0 || record.Length != 12 + (long)width * height * 3)
                throw new InvalidDataException($"Pre-ToD image record of {record.Length} bytes is not a {width}x{height} image of 24-bit pixels.");

            var step = format == SurfacePixelFormat.PFID_A8R8G8B8 ? 4 : 3;
            var pixels = new byte[width * height * step];

            for (var i = 0; i < width * height; i++)
            {
                pixels[i * step] = record[12 + i * 3 + 2];
                pixels[i * step + 1] = record[12 + i * 3 + 1];
                pixels[i * step + 2] = record[12 + i * 3];

                if (step == 4)
                    pixels[i * step + 3] = 0xFF;
            }

            return new Texture
            {
                Id = id ?? BitConverter.ToUInt32(record, 0),
                Unknown = ToDHeaderValue,
                Width = width,
                Height = height,
                Format = format,
                Length = pixels.Length,
                SourceData = pixels
            };
        }

        /// <summary>A new image record with the header value 0 (the value of the palette and plain pixel images the custom files carry).</summary>
        public static Texture Create(uint id, int width, int height, SurfacePixelFormat format, byte[] pixels, uint? defaultPaletteId = null)
        {
            return new Texture
            {
                Id = id,
                Unknown = 0,
                Width = width,
                Height = height,
                Format = format,
                Length = pixels.Length,
                SourceData = pixels,
                DefaultPaletteId = defaultPaletteId
            };
        }

        /// <exception cref="InvalidDataException">The pixel format needs a palette id and none is set.</exception>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Unknown);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write((uint)Format);
            writer.Write(SourceData.Length);
            writer.Write(SourceData);

            if (Format == SurfacePixelFormat.PFID_INDEX16 || Format == SurfacePixelFormat.PFID_P8)
            {
                if (DefaultPaletteId == null)
                    throw new InvalidDataException($"Texture {Id:X8}: format {Format} needs a default palette id.");

                writer.Write(DefaultPaletteId.Value);
            }
        }

        /// <summary>
        /// Exports RenderSurface to a image file
        /// </summary>
        public void ExportTexture(string directory)
        {
            if (Length == 0) return;

            switch (Format)
            {
                case SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG:
                    {
                        string filename = Path.Combine(directory, Id.ToString("X8") + ".jpg");
                        using (BinaryWriter writer = new BinaryWriter(File.Open(filename, FileMode.Create)))
                        {
                            writer.Write(SourceData);
                        }
                    }
                    break;

                default:
                    {
                        var bitmapImage = GetBitmap();
                        string filename = Path.Combine(directory, Id.ToString("X8") + ".png");
                        bitmapImage.Save(filename, ImageFormat.Png);
                    }
                    break;
            }
        }

        /// <summary>
        /// Reads RenderSurface to bitmap structure
        /// </summary>
        public Bitmap GetBitmap()
        {
            switch (Format)
            {
                case SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG:
                    {
                        var stream = new MemoryStream(SourceData);
                        var image = Image.FromStream(stream);
                        return new Bitmap(image);
                    }
                case SurfacePixelFormat.PFID_DXT1:
                    {
                        var image = DxtUtil.DecompressDxt1(SourceData, Width, Height);
                        return GetBitmap(image);
                    }
                case SurfacePixelFormat.PFID_DXT3:
                    {
                        var image = DxtUtil.DecompressDxt3(SourceData, Width, Height);
                        return GetBitmap(image);
                    }
                case SurfacePixelFormat.PFID_DXT5:
                    {
                        var image = DxtUtil.DecompressDxt5(SourceData, Width, Height);
                        return GetBitmap(image);
                    }
                default:
                    {
                        List<int> colors = GetImageColorArray();
                        return GetBitmap(colors);
                    }
            }
        }

        /// <summary>
        /// Converts the byte array SourceData into color values per pixel
        /// </summary>
        private List<int> GetImageColorArray()
        {
            List<int> colors = new List<int>();
            if (Length == 0) return colors;

            switch (Format)
            {
                case SurfacePixelFormat.PFID_R8G8B8: // RGB
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint i = 0; i < Height; i++)
                            for (uint j = 0; j < Width; j++)
                            {
                                byte b = reader.ReadByte();
                                byte g = reader.ReadByte();
                                byte r = reader.ReadByte();
                                int color = (r << 16) | (g << 8) | b;
                                colors.Add(color);
                            }
                    }
                    break;
                case SurfacePixelFormat.PFID_CUSTOM_LSCAPE_R8G8B8:
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint i = 0; i < Height; i++)
                            for (uint j = 0; j < Width; j++)
                            {
                                byte r = reader.ReadByte();
                                byte g = reader.ReadByte();
                                byte b = reader.ReadByte();
                                int color = (r << 16) | (g << 8) | b;
                                colors.Add(color);
                            }
                    }
                    break;
                case SurfacePixelFormat.PFID_A8R8G8B8: // ARGB format. Most UI textures fall into this category
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint i = 0; i < Height; i++)
                            for (uint j = 0; j < Width; j++)
                                colors.Add(reader.ReadInt32());
                    }
                    break;
                case SurfacePixelFormat.PFID_INDEX16: // 16-bit indexed colors. Index references position in a palette;
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint y = 0; y < Height; y++)
                            for (uint x = 0; x < Width; x++)
                                colors.Add(reader.ReadInt16());
                    }
                    break;
                case SurfacePixelFormat.PFID_A8: // Greyscale, also known as Cairo A8.
                case SurfacePixelFormat.PFID_CUSTOM_LSCAPE_ALPHA:
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint y = 0; y < Height; y++)
                            for (uint x = 0; x < Width; x++)
                                colors.Add(reader.ReadByte());
                    }
                    break;
                case SurfacePixelFormat.PFID_P8: // Indexed
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint y = 0; y < Height; y++)
                            for (uint x = 0; x < Width; x++)
                                colors.Add(reader.ReadByte());
                    }
                    break;
                case SurfacePixelFormat.PFID_R5G6B5: // 16-bit RGB
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint y = 0; y < Height; y++)
                            for (uint x = 0; x < Width; x++)
                            {
                                ushort val = reader.ReadUInt16();
                                List<int> color = get565RGB(val);
                                colors.Add(color[0]); // Red
                                colors.Add(color[1]); // Green
                                colors.Add(color[2]); // Blue
                            }
                    }
                    break;
                case SurfacePixelFormat.PFID_A4R4G4B4:
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
                    {
                        for (uint y = 0; y < Height; y++)
                            for (uint x = 0; x < Width; x++)
                            {
                                ushort val = reader.ReadUInt16();
                                // Expand each 4-bit channel to 8 bits. Dividing first is
                                // integer division: nibble / 0xF is 0 for every nibble
                                // 0..14 and 1 only for 15, so every channel collapsed to
                                // 0 or 255. 255 / 15 == 17 exactly, so multiplying by 17
                                // maps 0..15 onto 0..255 with no rounding error.
                                int alpha = (val >> 12 & 0xF) * 17;
                                int red = (val >> 8 & 0xF) * 17;
                                int green = (val >> 4 & 0xF) * 17;
                                int blue = (val & 0xF) * 17;

                                colors.Add(alpha);
                                colors.Add(red);
                                colors.Add(green);
                                colors.Add(blue);
                            }
                    }
                    break;
                default:
                    Console.WriteLine("Unhandled SurfacePixelFormat (" + Format.ToString() + ") in RenderSurface " + Id.ToString("X8"));
                    break;
            }

            return colors;
        }

        private List<int> GetPaletteIndexes()
        {
            List<int> colors = new List<int>();
            using (BinaryReader reader = new BinaryReader(new MemoryStream(SourceData)))
            {
                for (uint y = 0; y < Height; y++)
                    for (uint x = 0; x < Width; x++)
                        colors.Add(reader.ReadInt16());
            }
            return colors;
        }

        /// <summary>
        /// Generates Bitmap data from colorArray.
        /// </summary>
        private Bitmap GetBitmap(List<int> colorArray)
        {
            Bitmap image = new Bitmap(Width, Height);
            switch (this.Format)
            {
                case SurfacePixelFormat.PFID_R8G8B8:
                case SurfacePixelFormat.PFID_CUSTOM_LSCAPE_R8G8B8:
                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = (i * Width) + j;
                            int r = (colorArray[idx] & 0xFF0000) >> 16;
                            int g = (colorArray[idx] & 0xFF00) >> 8;
                            int b = colorArray[idx] & 0xFF;
                            image.SetPixel(j, i, Color.FromArgb(r, g, b));
                        }
                    break;
                case SurfacePixelFormat.PFID_A8R8G8B8:
                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = (i * Width) + j;
                            int a = (int)((colorArray[idx] & 0xFF000000) >> 24);
                            int r = (colorArray[idx] & 0xFF0000) >> 16;
                            int g = (colorArray[idx] & 0xFF00) >> 8;
                            int b = colorArray[idx] & 0xFF;
                            image.SetPixel(j, i, Color.FromArgb(a, r, g, b));
                        }
                    break;
                case SurfacePixelFormat.PFID_INDEX16:
                case SurfacePixelFormat.PFID_P8:
                    Palette pal = DatManager.PortalDat.ReadFromDat<Palette>((uint)DefaultPaletteId);

                    // Apply any custom palette colors, if any, to our loaded palette (note, this may be all of them!)
                    if (CustomPaletteColors.Count > 0)
                        foreach (KeyValuePair<int, uint> entry in CustomPaletteColors)
                            if (entry.Key <= pal.Colors.Count)
                                pal.Colors[entry.Key] = entry.Value;

                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = (i * Width) + j;
                            int a = (int)((pal.Colors[colorArray[idx]] & 0xFF000000) >> 24);
                            int r = (int)(pal.Colors[colorArray[idx]] & 0xFF0000) >> 16;
                            int g = (int)(pal.Colors[colorArray[idx]] & 0xFF00) >> 8;
                            int b = (int)pal.Colors[colorArray[idx]] & 0xFF;
                            image.SetPixel(j, i, Color.FromArgb(a, r, g, b));
                        }
                    break;
                case SurfacePixelFormat.PFID_A8:
                case SurfacePixelFormat.PFID_CUSTOM_LSCAPE_ALPHA:
                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = (i * Width) + j;
                            int r = colorArray[idx];
                            int g = colorArray[idx];
                            int b = colorArray[idx];
                            image.SetPixel(j, i, Color.FromArgb(r, g, b));
                        }
                    break;
                case SurfacePixelFormat.PFID_R5G6B5: // 16-bit RGB
                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = 3 * ((i * Width) + j);
                            int r = (int)(colorArray[idx]);
                            int g = (int)(colorArray[idx + 1]);
                            int b = (int)(colorArray[idx + 2]);
                            image.SetPixel(j, i, Color.FromArgb(r, g, b));
                        }
                    break;
                case SurfacePixelFormat.PFID_A4R4G4B4:
                    for (int i = 0; i < Height; i++)
                        for (int j = 0; j < Width; j++)
                        {
                            int idx = 4 * ((i * Width) + j);
                            int a = (colorArray[idx]);
                            int r = (colorArray[idx + 1]);
                            int g = (colorArray[idx + 2]);
                            int b = (colorArray[idx + 3]);
                            image.SetPixel(j, i, Color.FromArgb(a, r, g, b));
                        }
                    break;
            }
            return image;
        }

        /// <summary>
        /// Generates Bitmap data from byteArray, generated by DXT1, DXT3, and DXT5 image foramts.
        /// </summary>
        private Bitmap GetBitmap(byte[] byteArray)
        {
            Bitmap image = new Bitmap(Width, Height);
            for (int i = 0; i < Height; i++)
                for (int j = 0; j < Width; j++)
                {
                    int idx = 4 * ((i * Width) + j);
                    int r = (int)(byteArray[idx]);
                    int g = (int)(byteArray[idx + 1]);
                    int b = (int)(byteArray[idx + 2]);
                    int a = (int)(byteArray[idx + 3]);
                    image.SetPixel(j, i, Color.FromArgb(a, r, g, b));
                }

            return image;
        }

        // https://docs.microsoft.com/en-us/windows/desktop/DirectShow/working-with-16-bit-rgb
        private List<int> get565RGB(ushort val)
        {
            List<int> color = new List<int>();

            int red_mask = 0xF800;
            int green_mask = 0x7E0;
            int blue_mask = 0x1F;

            int r5 = (val & red_mask) >> 11;   // 0..31
            int g6 = (val & green_mask) >> 5;  // 0..63
            int b5 = val & blue_mask;          // 0..31

            // Expand by bit replication rather than a bare shift. A plain v << 3
            // maps 0..31 onto 0,8,16..248 and can never reach 255, so a fully
            // saturated R5G6B5 white decoded to (248,252,248).
            int red = (r5 << 3) | (r5 >> 2);
            int green = (g6 << 2) | (g6 >> 4);
            int blue = (b5 << 3) | (b5 >> 2);

            color.Add(red); // Red
            color.Add(green); // Green
            color.Add(blue); // Blue

            return color;
        }
    }
}
