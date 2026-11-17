using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Reads the dat_* tables from a ruleset's world database through <see cref="WorldDbContext"/>. The connection comes from the MySql.World section of a server Config.js,
    /// so the password never appears on a command line.
    /// </summary>
    public class WorldDatTableSource : IDatTableSource
    {
        private readonly DbContextOptions<WorldDbContext> options;

        public string Description { get; }

        public WorldDatTableSource(string configPath, string databaseOverride = null)
        {
            if (!File.Exists(configPath))
                throw new ArgumentException($"Config not found: {configPath}");

            var jsonOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

            using (var document = JsonDocument.Parse(File.ReadAllText(configPath), jsonOptions))
            {
                if (!document.RootElement.TryGetProperty("MySql", out var mysql) || !mysql.TryGetProperty("World", out var world))
                    throw new ArgumentException($"{configPath} has no MySql.World section.");

                var host = world.GetProperty("Host").GetString();
                var port = world.GetProperty("Port").GetUInt32();
                var database = databaseOverride ?? world.GetProperty("Database").GetString();
                var user = world.GetProperty("Username").GetString();
                var password = world.GetProperty("Password").GetString();

                var connectionString = $"server={host};port={port};user={user};password={password};database={database};DefaultCommandTimeout=60;SslMode=None;AllowPublicKeyRetrieval=true";

                options = new DbContextOptionsBuilder<WorldDbContext>()
                    .UseMySql(connectionString, DatabaseManager.CachedServerVersionAutoDetect(database, connectionString))
                    .Options;

                Description = $"{database} on {host}:{port}";
            }
        }

        public DatTableRows Read()
        {
            using (var context = new WorldDbContext(options))
                return DatTableReader.Read(context, Description);
        }
    }
}
