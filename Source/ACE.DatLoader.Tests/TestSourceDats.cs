using System;
using System.IO;

using ACE.DatBuilder;

namespace ACE.DatLoader.Tests
{
    /// <summary>
    /// The folder with the original dats, read from the dat_source_folder setting of the shard database named in the server's Config.js
    /// (ACE.Server/bin/x64/Debug/net10.0/Config.js, found by walking up from the test folder). The database has to be running.
    /// </summary>
    public static class TestSourceDats
    {
        private const string ConfigRelativePath = @"ACE.Server\bin\x64\Debug\net10.0\Config.js";

        public static readonly string Folder = Read();

        private static string Read()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var config = Path.Combine(directory.FullName, ConfigRelativePath);

                if (!File.Exists(config))
                    continue;

                var folder = DatSourceSetting.Read(config);

                if (folder == null)
                    throw new InvalidOperationException($"The {DatSourceSetting.Key} setting is empty in the shard database named in {config}.");

                return folder;
            }

            throw new InvalidOperationException($"{ConfigRelativePath} was not found above {AppContext.BaseDirectory}.");
        }
    }
}
