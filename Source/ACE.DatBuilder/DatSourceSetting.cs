using ACE.Common;
using ACE.Database;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The dat_source_folder server setting: the folder with the original dats. It is read the way the server reads its settings, from config_properties_string
    /// of the shard database named in the Config.js that <see cref="ConfigManager"/> loads.
    /// </summary>
    public static class DatSourceSetting
    {
        public const string Key = "dat_source_folder";

        /// <summary>The folder, or null when the table has no row for the setting or the row is empty.</summary>
        public static string Read(string configPath)
        {
            ConfigManager.Initialize(configPath);

            var value = new ShardConfigDatabase().GetString(Key)?.Value;

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
