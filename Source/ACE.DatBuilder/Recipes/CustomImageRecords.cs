using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The images of a ruleset that no dat has (the user's own work): Recipes/Data/Portal/&lt;ruleset&gt;/&lt;pixel format&gt;/&lt;id&gt; - &lt;name&gt;.png, written as image records of the folder's
    /// format (the name of a <see cref="SurfacePixelFormat"/> without PFID_). CUSTOM_RAW_JPEG holds .jpg files, whose bytes are the record's pixels as they are.
    /// </summary>
    public static class CustomImageRecords
    {
        /// <summary>Runs after the other record steps, so these images replace the records they share an id with.</summary>
        /// <exception cref="InvalidDataException">The ruleset has no images, or a file is not named, placed or typed as above, or two files have the same id.</exception>
        public static DatRecordStep Step(string ruleset, string date)
        {
            return new DatRecordStep($"{ruleset} custom images", date, c =>
            {
                var written = new Dictionary<uint, string>();

                foreach (var file in DatRecipeData.Files($"Portal/{ruleset}"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);

                    if (!Enum.TryParse<SurfacePixelFormat>("PFID_" + Path.GetFileName(Path.GetDirectoryName(file)), out var format))
                        throw new InvalidDataException($"Recipes/Data/{file}: images go in a folder named after their pixel format, such as A8R8G8B8.");

                    if (!Path.GetExtension(file).Equals(format == SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG ? ".jpg" : ".png", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Recipes/Data/{file}: a {format} folder holds only {(format == SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG ? ".jpg" : ".png")} files.");

                    if (name.Length < 8 || !uint.TryParse(name.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
                        throw new InvalidDataException($"Recipes/Data/{file}: the name must start with the record's 8-digit hex id.");

                    if (written.TryGetValue(id, out var other))
                        throw new InvalidDataException($"Recipes/Data/{file}: id {id:X8} is also in Recipes/Data/{other}.");

                    written[id] = file;

                    Texture image;

                    using (var stream = DatRecipeData.Open(file))
                    {
                        if (format == SurfacePixelFormat.PFID_CUSTOM_RAW_JPEG)
                        {
                            var jpeg = new MemoryStream();
                            stream.CopyTo(jpeg);
                            image = Texture.Create(id, 0, 0, format, jpeg.ToArray());
                            image.DataCategory = Texture.ToDHeaderValue;
                        }
                        else
                        {
                            using (var picture = new Bitmap(stream))
                                image = Texture.FromBitmap(id, picture, format);
                        }
                    }

                    c.Write(id, image.Pack(), name.Substring(8).TrimStart(' ', '-'), true);
                }

                if (written.Count == 0)
                    throw new InvalidDataException($"Recipes/Data/Portal/{ruleset} holds no images.");
            });
        }
    }
}
