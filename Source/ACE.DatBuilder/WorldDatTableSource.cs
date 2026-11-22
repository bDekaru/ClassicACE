using System.Collections.Generic;
using System.Linq;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.Entity.Enum.Properties;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Reads the dat_* tables from a ruleset's world database through <see cref="WorldDbContext"/>. The connection comes from the MySql.World section of a server Config.js
    /// loaded by <see cref="ConfigManager"/>, so the password never appears on a command line.
    /// </summary>
    public class WorldDatTableSource : IDatTableSource
    {
        public string Description { get; }

        /// <summary>The WorldRuleset of the Config.js, the ruleset whose database the tables come from.</summary>
        public string Ruleset { get; }

        public WorldDatTableSource(string configPath, string databaseOverride = null)
        {
            ConfigManager.Initialize(configPath);

            var world = ConfigManager.Config.MySql.World;

            Ruleset = ConfigManager.Config.Server.WorldRuleset.ToString();

            if (databaseOverride != null)
                world.Database = databaseOverride;

            Description = $"{world.Database} on {world.Host}:{world.Port}";
        }

        public DatTableRows Read()
        {
            using (var context = new WorldDbContext())
                return DatTableReader.Read(context, Description);
        }

        // The other DID properties hold no dat id: MotionCommand, sound and PlayScript enums, spell and skill ids, treasure and weenie class ids, the server's mutation filter keys, and QualityFilter (0E000005/6, in no dat; never sent to the client, read by no code).
        private static readonly ushort[] DatIdProperties = new[]
        {
            PropertyDataId.Setup, PropertyDataId.MotionTable, PropertyDataId.SoundTable, PropertyDataId.CombatTable, PropertyDataId.PaletteBase,
            PropertyDataId.ClothingBase, PropertyDataId.Icon, PropertyDataId.EyesTexture, PropertyDataId.NoseTexture, PropertyDataId.MouthTexture, PropertyDataId.DefaultEyesTexture,
            PropertyDataId.DefaultNoseTexture, PropertyDataId.DefaultMouthTexture, PropertyDataId.HairPalette, PropertyDataId.EyesPalette, PropertyDataId.SkinPalette,
            PropertyDataId.HeadObject, PropertyDataId.InitMotion, PropertyDataId.PhysicsEffectTable, PropertyDataId.IconOverlay, PropertyDataId.IconOverlaySecondary, PropertyDataId.IconUnderlay,
        }.Select(property => (ushort)property).ToArray();

        /// <summary>Every dat id the weenies name: the DID properties that hold one, palettes, texture swaps and animation parts.</summary>
        public HashSet<uint> ReadDatIds()
        {
            using (var context = new WorldDbContext())
            {
                var ids = new HashSet<uint>(context.WeeniePropertiesDID.Where(p => DatIdProperties.Contains(p.Type)).Select(p => p.Value));

                ids.UnionWith(context.WeeniePropertiesPalette.Select(p => p.SubPaletteId));
                ids.UnionWith(context.WeeniePropertiesTextureMap.Select(p => p.OldId));
                ids.UnionWith(context.WeeniePropertiesTextureMap.Select(p => p.NewId));
                ids.UnionWith(context.WeeniePropertiesAnimPart.Select(p => p.AnimationId));

                return ids;
            }
        }
    }
}
