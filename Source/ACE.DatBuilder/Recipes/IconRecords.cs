using System;
using System.Collections.Generic;

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

        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Icons from the 2005 dat", date, c =>
            {
                foreach (var icon in DatRecipeData.Load<List<Icon>>(IconsFile))
                {
                    var imageId = Convert.ToUInt32(icon.Image, 16);
                    var sourceId = Convert.ToUInt32(icon.Source, 16);

                    c.Write(imageId, ConvertIcon(c.ReadSource(DatBuildRecipes.PreToDPortalDat, sourceId), imageId), $"icon from {sourceId:X8}");
                }
            });
        }

        /// <summary>A pre-ToD icon record as a ToD A8R8G8B8 image record with the given id.</summary>
        public static byte[] ConvertIcon(byte[] source, uint imageId)
        {
            return Texture.UnpackPreToD(source, SurfacePixelFormat.PFID_A8R8G8B8, imageId).Pack();
        }
    }
}
