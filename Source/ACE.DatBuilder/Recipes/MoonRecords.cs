using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The moon of the regular sky (01001F6A) on the dats of the Dereth client. The moon model draws a clip-map disc and then an additive glare over it; the Dereth client (Direct3D 9) draws the sky in surface order with the depth test off,
    /// so the glare washes the disc out, which the 2005 client (drawing by BSP, far to near) did not do. Reordering the surfaces does not help. What does is what the makers of the EoR Child of Daralet dat did: a second sky object right after the moon
    /// that shows a copy of the model without the glare layer, so the disc is drawn again over the glare. The copy's glare surface is a fully translucent one, made from nothing (<see cref="CoverRecords"/>).
    /// </summary>
    public static class MoonRecords
    {
        public const uint Moon = 0x01001F6A;

        /// <summary>The moon without the glare layer.</summary>
        private const uint MoonCover = 0x01007087;

        private const uint CoverSurface = 0x08007087;
        private const uint CoverSurfaceTexture = 0x05007087;
        private const uint CoverTexture = 0x06009F00;
        private const uint CoverPalette = 0x04007087;

        public static DatRecordStep Overlay(string date)
        {
            return new DatRecordStep("Moon glare overlay", date, c =>
            {
                var model = c.ReadTarget<GfxObj>(Moon);

                for (var i = 0; i < model.Surfaces.Count; i++)
                    if (c.ReadTarget<Surface>(model.Surfaces[i]).Type.HasFlag(SurfaceType.Additive))
                        model.Surfaces[i] = CoverSurface;

                model.Id = MoonCover;

                c.Write(CoverRecords(CoverSurface, CoverSurfaceTexture, CoverTexture, CoverPalette), "moon glare cover");
                c.Write(MoonCover, model.Pack(), "moon without the glare layer");

                var region = c.ReadTarget<RegionDesc>(RegionRecords.RegionId);

                foreach (var group in region.SkyInfo.DayGroups)
                    AddCover(group);

                c.Write(RegionRecords.RegionId, region.Pack(), "region with the moon overlay");
            });
        }

        /// <summary>Puts the moon without the glare layer right after the moon of the day group, if it has one.</summary>
        public static void AddCover(DayGroup group)
        {
            var index = group.SkyObjects.FindIndex(s => s.DefaultGFXObjectId == Moon);

            if (index >= 0)
                group.InsertSkyObject(index + 1, group.SkyObjects[index].CloneWith(MoonCover));
        }

        /// <summary>
        /// The records of a clip-map surface that is fully translucent (the same surface EoR's 08000015 is), over an 8x8 image of palette index 0 and a black palette.
        /// </summary>
        public static List<KeyValuePair<uint, byte[]>> CoverRecords(uint surfaceId, uint surfaceTextureId, uint textureId, uint paletteId)
        {
            const int imageSize = 8;

            var palette = new Palette { Id = paletteId };
            palette.Colors.AddRange(Enumerable.Repeat(0xFF000000, 2048));

            var texture = Texture.Create(textureId, imageSize, imageSize, SurfacePixelFormat.PFID_INDEX16, new byte[2 * imageSize * imageSize], paletteId);
            texture.DataCategory = 7;

            return new List<KeyValuePair<uint, byte[]>>
            {
                new KeyValuePair<uint, byte[]>(surfaceId, Surface.Create(surfaceId, SurfaceType.Base1ClipMap | SurfaceType.Translucent, surfaceTextureId, 0, 1f, 0f, 1f).Pack()),
                new KeyValuePair<uint, byte[]>(surfaceTextureId, SurfaceTexture.Create(surfaceTextureId, textureId).Pack()),
                new KeyValuePair<uint, byte[]>(textureId, texture.Pack()),
                new KeyValuePair<uint, byte[]>(paletteId, palette.Pack())
            };
        }
    }
}
