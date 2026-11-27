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
                        sceneType.Scenes.Add(DatRecipeData.Hex(scene));
                }
            }
        }

        public const string IslandSceneryFile = "customdm-island-scenery.json";

        public class TerrainSlot
        {
            public int TerrainType { get; set; }
            public int Slot { get; set; }
        }

        public class IslandSceneryEdits
        {
            /// <summary>Terrain slots that take the scene type the EoR region has there.</summary>
            public List<TerrainSlot> Slots { get; set; }

            /// <summary>Added scene to the scene of the dat whose objects it gets as well.</summary>
            public Dictionary<string, string> Emitters { get; set; }
        }

        /// <summary>The scenery of the EoR islands: the EoR scene types the region lacks, appended so that their positions match EoR, and the terrain slots that use them. The scenes come with the EoR record list.</summary>
        public static DatRecordStep IslandScenery(string date)
        {
            return new DatRecordStep("EoR island scenery", date, c =>
            {
                var edits = DatRecipeData.Load<IslandSceneryEdits>(IslandSceneryFile);
                var region = c.ReadTarget<RegionDesc>(RegionDesc.FILE_ID);
                var eor = c.ReadSource<RegionDesc>(DatBuildRecipes.PortalBaseDat, RegionDesc.FILE_ID);
                var sceneTypes = region.SceneInfo.SceneTypes;
                var wanted = edits.Slots.Select(s => (s.TerrainType, s.Slot, SceneType: (int)eor.TerrainInfo.TerrainTypes[s.TerrainType].SceneTypes[s.Slot])).ToList();
                var count = sceneTypes.Count;

                if (wanted.Any(w => w.SceneType < count))
                    throw new InvalidDataException($"The region already has {count} scene types, so an EoR scene type of an island slot would not be the one added.");

                sceneTypes.AddRange(eor.SceneInfo.SceneTypes.GetRange(count, wanted.Max(w => w.SceneType) + 1 - count));

                foreach (var (terrainType, slot, sceneType) in wanted)
                    region.TerrainInfo.TerrainTypes[terrainType].SceneTypes[slot] = (uint)sceneType;

                c.Write(RegionDesc.FILE_ID, region.Pack(), "region", true);

                foreach (var pair in edits.Emitters)
                {
                    var id = DatRecipeData.Hex(pair.Key);
                    var scene = c.ReadTarget<Scene>(id);
                    scene.Objects.AddRange(c.ReadTarget<Scene>(DatRecipeData.Hex(pair.Value)).Objects.Where(o => o.ObjId != 0));
                    c.Write(id, scene.Pack(), "EoR scene with the emitters", true);
                }
            });
        }

        public static DatRecordStep[] All(string date)
        {
            return new[]
            {
                ModelRecords.Step("Frozen Fields models from the 2001 dat", date, ModelsFile, DatBuildRecipes.Portal2001Dat),
                new DatRecordStep("Frozen Fields scene in the region from the EoR record", date, c =>
                {
                    var region = c.ReadTarget<RegionDesc>(RegionDesc.FILE_ID);
                    DatRecipeData.Load<RegionEdits>(RegionEditsFile).Apply(region);
                    c.Write(RegionDesc.FILE_ID, region.Pack(), "region", true);
                })
            };
        }
    }
}
