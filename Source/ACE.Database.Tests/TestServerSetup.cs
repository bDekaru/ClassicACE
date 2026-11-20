using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Common;

namespace ACE.TestSupport
{
    /// <summary>
    /// Server configuration for the tests that touch the databases. ACE.Server.Tests links this file.
    /// </summary>
    public static class TestServerSetup
    {
        /// <summary>The ACE.Server source folder, found by walking up from the test folder, so it doesn't depend on how deep the output folder is.</summary>
        public static readonly string Folder = FindFolder();

        /// <summary>
        /// Copies the server's Config.js (or its Config.js.example when there is none) to <paramref name="destination"/> and loads it.
        /// The built server's config is never used: it points at the live databases.
        /// A test that writes needs databases with "test" in their names; otherwise it is skipped.
        /// </summary>
        public static void InitializeConfig(string destination, bool writesToDatabases)
        {
            var source = Path.Combine(Folder, "Config.js");

            if (!File.Exists(source))
                source += ".example";

            File.Copy(source, destination, true);
            ConfigManager.Initialize();

            if (!writesToDatabases)
                return;

            var mySql = ConfigManager.Config.MySql;

            foreach (var database in new[] { mySql.Authentication.Database, mySql.Shard.Database, mySql.World.Database })
            {
                if (!database.Contains("test", StringComparison.OrdinalIgnoreCase))
                    Assert.Inconclusive($"These tests write to the databases, and '{database}' is not a test database. Name the test databases with 'test' in ACE.Server/Config.js.");
            }
        }

        private static string FindFolder()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var folder = Path.Combine(directory.FullName, "ACE.Server");

                if (File.Exists(Path.Combine(folder, "ACE.Server.csproj")))
                    return folder;
            }

            throw new InvalidOperationException($"The ACE.Server folder was not found above {AppContext.BaseDirectory}.");
        }
    }
}
