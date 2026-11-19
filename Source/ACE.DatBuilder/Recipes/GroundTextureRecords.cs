using System;
using System.Collections.Generic;
using System.IO;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>The terrain textures and terrain blend maps of the EoR dat, replaced by the original images of the 2005 dat at their own size.</summary>
    public static class GroundTextureRecords
    {
        public const string TexturesFile = "ground-textures.json";

        private const uint RegionId = 0x13000000;

        /// <summary>The second header value of the EoR terrain images, used for an image the dat does not have yet.</summary>
        private const uint DefaultHeaderValue = 2;

        public class GroundTexture
        {
            /// <summary>The ToD image record (0x06) that is written.</summary>
            public string Image { get; set; }

            /// <summary>The 2005 image record (0x05) it is made from.</summary>
            public string Source { get; set; }
        }

        // Source dat: portal - 2005-02-XX - Iteration 2112 - Infiltration.dat, MD5 304275cb743dc28f37410e06e91cb17c (the images); the formats and header values are those of the EoR dat's own records, portal - 2017-01-29 - Iteration 2072 - EoR.dat, MD5 2c89662a44fcdc2a3c31fe8f6677d265.
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

        /// <summary>The merge size of the 2005 client's hardware region (130F0000): a terrain texture is repeated BaseTexSize / width times per landcell.</summary>
        public const uint BaseTexSize = 128;

        /// <summary>Sets the terrain merge to the size of the 2005 client and the tiling of each terrain texture to fit its (original) width. Runs after the surface texture steps, which can change a terrain's texture.</summary>
        /// <exception cref="InvalidDataException">A terrain texture is not an original-size image that divides the merge size.</exception>
        public static DatRecordStep RegionStep(string date)
        {
            return new DatRecordStep("Terrain merge size and tiling of the 2005 client", date, c =>
            {
                var region = c.ReadTarget<RegionDesc>(RegionId);
                var merge = region.TerrainInfo.LandSurfaces.TexMerge;
                merge.BaseTexSize = BaseTexSize;

                foreach (var description in merge.TerrainDesc)
                {
                    var surfaceTextureId = description.TerrainTex.TexGID;
                    var textureId = c.ReadTarget<SurfaceTexture>(surfaceTextureId).Textures[0];
                    var width = (uint)c.ReadTarget<Texture>(textureId).Width;

                    if (width == 0 || BaseTexSize % width != 0)
                        throw new InvalidDataException($"Terrain texture {textureId:X8} ({surfaceTextureId:X8}) is {width} px wide, which does not divide the merge size {BaseTexSize}.");

                    description.TerrainTex.TexTiling = BaseTexSize / width;
                }

                c.Write(RegionId, region.Pack(), $"region merge size {BaseTexSize}");
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
