using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>Inventory icons that only the pre-ToD dats have, written into the ToD dat as A8R8G8B8 image records.</summary>
    public static class IconRecords
    {
        public const string IconsFile = "icons.json";

        public class Icon
        {
            /// <summary>The ToD image record (0x06) that is written.</summary>
            public string Image { get; set; }

            /// <summary>The pre-ToD icon record (0x06) it is made from.</summary>
            public string Source { get; set; }
        }

        // Source dat: portal - 2005-02-XX - Iteration 2112 - Infiltration.dat, MD5 304275cb743dc28f37410e06e91cb17c
        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Icons from the 2005 dat", date, c =>
            {
                foreach (var icon in DatRecipeData.Load<List<Icon>>(IconsFile))
                {
                    var imageId = Convert.ToUInt32(icon.Image, 16);
                    var sourceId = Convert.ToUInt32(icon.Source, 16);

                    c.Write(imageId, ConvertIcon(c.ReadSource(DatBuildRecipes.PreToDPortalDat, sourceId), imageId), $"icon from {sourceId:X8}", true);
                }
            });
        }

        private const int IconSize = 32;

        /// <summary>
        /// The EoR icons (32x32 images) the dat does not hold yet, so Decal plugins that read item and spell icons from the dat by id do not draw a magenta box for items the 2005 dat predates.
        /// Runs after the steps that write icons, so their records stay.
        /// </summary>
        public static DatRecordStep EoRStep(string date)
        {
            return new DatRecordStep("EoR icons the 2005 dat lacks", date, c =>
            {
                var eor = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PortalBaseDat), true);

                var ids = eor.AllFiles.Keys.Where(id => id >> 24 == 0x06 && !c.Writer.Contains(id)).OrderBy(id => id)
                    .Where(id => c.ReadSource<Texture>(DatBuildRecipes.PortalBaseDat, id) is { Width: IconSize, Height: IconSize }).ToList();

                c.Write(ids.Select(id => new KeyValuePair<uint, byte[]>(id, c.ReadSource(DatBuildRecipes.PortalBaseDat, id))), "EoR icons", true);
            });
        }

        /// <summary>A pre-ToD icon record as a ToD A8R8G8B8 image record with the given id.</summary>
        public static byte[] ConvertIcon(byte[] source, uint imageId)
        {
            return Texture.UnpackPreToD(source, SurfacePixelFormat.PFID_A8R8G8B8, imageId).Pack();
        }
    }
}
