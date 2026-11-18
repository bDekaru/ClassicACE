using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Reads the dat_* tables from a ruleset's world database through <see cref="WorldDbContext"/>. The connection comes from the MySql.World section of a server Config.js
    /// loaded by <see cref="ConfigManager"/>, so the password never appears on a command line.
    /// </summary>
    public class WorldDatTableSource : IDatTableSource
    {
        public string Description { get; }

        public WorldDatTableSource(string configPath, string databaseOverride = null)
        {
            ConfigManager.Initialize(configPath);

            var world = ConfigManager.Config.MySql.World;

            if (databaseOverride != null)
                world.Database = databaseOverride;

            Description = $"{world.Database} on {world.Host}:{world.Port}";
        }

        public DatTableRows Read()
        {
            using (var context = new WorldDbContext())
                return DatTableReader.Read(context, Description);
        }
    }
}
