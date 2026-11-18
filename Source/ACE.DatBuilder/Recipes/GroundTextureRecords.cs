using System;
using System.Collections.Generic;

using ACE.DatLoader;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>The terrain textures and terrain blend maps of the EoR dat, replaced by the original images of the 2005 dat at their own size.</summary>
    public static class GroundTextureRecords
    {
        public const string TexturesFile = "ground-textures.json";

        /// <summary>The second header value of the EoR terrain images, used for an image the dat does not have yet.</summary>
        private const uint DefaultHeaderValue = 2;

        public class GroundTexture
        {
            /// <summary>The ToD image record (0x06) that is written.</summary>
            public string Image { get; set; }

            /// <summary>The 2005 image record (0x05) it is made from.</summary>
            public string Source { get; set; }
        }

        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Ground textures from the 2005 dat", date, c =>
            {
                foreach (var texture in DatRecipeData.Load<List<GroundTexture>>(TexturesFile))
                {
                    var imageId = Convert.ToUInt32(texture.Image, 16);
                    var sourceId = Convert.ToUInt32(texture.Source, 16);
                    var image = DatPortalImporter.ConvertImage(c.ReadSource(DatBuildRecipes.PreToDPortalDat, sourceId), imageId, id => c.ReadSource(DatBuildRecipes.PreToDPortalDat, id), out var width, out var height, out _);

                    var existing = c.Writer.Read(imageId);

                    c.Write(imageId, ToTerrainFormat(image, existing != null ? BitConverter.ToUInt32(existing, 4) : DefaultHeaderValue),$"ground texture from {sourceId:X8} ({width}x{height})");
                }
            });
        }

        // The client's terrain merge reads a terrain texture as 32-bit pixels and a blend map as one byte per pixel (the EoR records are A8R8G8B8 and PFID_CUSTOM_LSCAPE_ALPHA), whatever size they have.
        public static byte[] ToTerrainFormat(byte[] image, uint headerValue)
        {
            const int HeaderSize = 24;
            const uint R8G8B8 = 20;
            const uint A8 = 28;

            var format = BitConverter.ToUInt32(image, 16);
            var output = new List<byte>(image.Length * 2);

            if (format == R8G8B8)
            {
                for (var i = HeaderSize; i + 2 < image.Length; i += 3)
                {
                    output.Add(image[i]);
                    output.Add(image[i + 1]);
                    output.Add(image[i + 2]);
                    output.Add(0xFF);
                }

                format = (uint)SurfacePixelFormat.PFID_A8R8G8B8;
            }
            else if (format == A8)
            {
                output.AddRange(new ArraySegment<byte>(image, HeaderSize, image.Length - HeaderSize));
                format = (uint)SurfacePixelFormat.PFID_CUSTOM_LSCAPE_ALPHA;
            }
            else
                throw new NotSupportedException($"Ground texture image format {format} is not converted.");

            var result = new byte[HeaderSize + output.Count];
            Array.Copy(image, result, HeaderSize);
            output.CopyTo(result, HeaderSize);
            Array.Copy(BitConverter.GetBytes(headerValue), 0, result, 4, 4);
            Array.Copy(BitConverter.GetBytes(format), 0, result, 16, 4);
            Array.Copy(BitConverter.GetBytes(output.Count), 0, result, 20, 4);

            return result;
        }
    }
}
