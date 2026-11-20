using System;
using System.Collections.Generic;

using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>Surface textures (0x05) of the EoR dat that are pointed at another texture (0x06), replacing the EoR record.</summary>
    public static class SurfaceTextureRecords
    {
        public const string GroundFile = "ground-surface-textures.json";
        public const string BlendFile = "blend-surface-textures.json";
        public const string RockFile = "rock-surface-textures.json";

        public class Entry
        {
            /// <summary>The surface texture that is written.</summary>
            public string Id { get; set; }

            /// <summary>The texture (0x06) it refers to.</summary>
            public string Texture { get; set; }
        }

        public static DatRecordStep Step(string name, string date, string dataFile)
        {
            return new DatRecordStep(name, date, c =>
            {
                foreach (var entry in DatRecipeData.Load<List<Entry>>(dataFile))
                {
                    var id = Convert.ToUInt32(entry.Id, 16);
                    var texture = Convert.ToUInt32(entry.Texture, 16);
                    c.Write(id, SurfaceTexture.Create(id, texture).Pack(), $"surface texture for texture {texture:X8}");
                }
            });
        }
    }
}
