using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>The Frozen Fields records: the models and scene made from the 2001 dat, and the scene added to the region record of the EoR dat.</summary>
    public static class RegionRecords
    {
        public const string ModelsFile = "frozen-fields.json";
        public const string RegionEditsFile = "frozen-fields-region.json";

        public const uint RegionId = 0x13000000;

        public class SceneTypeEdit
        {
            /// <summary>The scene type of the region that is changed, by its position in the region's list (the StbIndex is not unique).</summary>
            public int Index { get; set; }

            /// <summary>Scenes (0x12) added to the scene type.</summary>
            public List<string> Add { get; set; } = new List<string>();
        }

        public class RegionEdits
        {
            public List<SceneTypeEdit> SceneTypes { get; set; } = new List<SceneTypeEdit>();

            /// <exception cref="InvalidDataException">The region has no scene type at the position.</exception>
            public void Apply(RegionDesc region)
            {
                foreach (var edit in SceneTypes)
                {
                    if (edit.Index < 0 || edit.Index >= region.SceneInfo.SceneTypes.Count)
                        throw new InvalidDataException($"The region has no scene type at the position {edit.Index}.");

                    var sceneType = region.SceneInfo.SceneTypes[edit.Index];

                    foreach (var scene in edit.Add)
                        sceneType.Scenes.Add(Convert.ToUInt32(scene, 16));
                }
            }
        }

        public static DatRecordStep[] All(string date)
        {
            return new[]
            {
                ModelRecords.Step("Frozen Fields models from the 2001 dat", date, ModelsFile, DatBuildRecipes.Portal2001Dat),
                new DatRecordStep("Frozen Fields scene in the region from the EoR record", date, c =>
                {
                    var region = c.ReadTarget<RegionDesc>(RegionId);
                    DatRecipeData.Load<RegionEdits>(RegionEditsFile).Apply(region);
                    c.Write(RegionId, region.Pack(), "region");
                })
            };
        }
    }
}
