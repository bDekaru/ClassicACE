using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using ACE.Adapter.GDLE;
using ACE.Adapter.Lifestoned;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Database.SQLFormatters.World;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Physics.Extensions;
using ACE.Server.WorldObjects;
using WeenieClassName = ACE.Server.Factories.Enum.WeenieClassName;
using System.Threading;
using System.Diagnostics;
using ACE.Entity.Models;
using Weenie = ACE.Database.Models.World.Weenie;
using ACE.Server.WorldObjects.Entity;
using ACE.DatLoader.FileTypes;
using ACE.DatLoader;

namespace ACE.Server.Command.Handlers.Processors
{
    public class DeveloperContentCommands
    {
        public enum FileType
        {
            Undefined,
            Encounter,
            LandblockInstance,
            Quest,
            Recipe,
            Spell,
            Weenie,
        }

        public static FileType GetContentType(string[] parameters, ref string param)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                var otherIdx = i == 0 ? 1 : 0;

                param = parameters[otherIdx];

                var fileType = parameters[i].ToLower();

                if (fileType.StartsWith("landblock"))
                    return FileType.LandblockInstance;
                else if (fileType.StartsWith("encounter"))
                    return FileType.Encounter;
                else if (fileType.StartsWith("quest"))
                    return FileType.Quest;
                else if (fileType.StartsWith("recipe"))
                    return FileType.Recipe;
                else if (fileType.StartsWith("weenie"))
                    return FileType.Weenie;
                else if (fileType.StartsWith("spell"))
                    return FileType.Spell;
                else if (fileType.StartsWith("encounter"))
                    return FileType.Encounter;
            }
            return FileType.Undefined;
        }

        [CommandHandler("import-json", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Imports json data from the Content folder", "<type> <wcid>\n<type> - landblock, quest, recipe, spell, weenie (default if not specified)\n<wcid> - filename prefix to search for. can be 'all' to import all files for this content type")]
        public static void HandleImportJson(Session session, params string[] parameters)
        {
            var param = parameters[0];
            var contentType = FileType.Weenie;

            if (parameters.Length > 1)
            {
                contentType = GetContentType(parameters, ref param);

                if (contentType == FileType.Undefined)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type '{parameters[1]}'");
                    return;
                }
            }
            switch (contentType)
            {
                case FileType.LandblockInstance:
                    ImportJsonLandblock(session, param);
                    break;

                case FileType.Quest:
                    ImportJsonQuest(session, param);
                    break;

                case FileType.Recipe:
                    ImportJsonRecipe(session, param);
                    break;

                case FileType.Weenie:
                    ImportJsonWeenie(session, param);
                    break;
            }
        }

        [CommandHandler("convert-json", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Converts json data from the Content folder to sql", "<wcid> or all")]
        public static void HandleConvertJson(Session session, params string[] parameters)
        {
            var param = parameters[0];
            var contentType = FileType.Weenie;

            if (parameters.Length > 1)
            {
                contentType = GetContentType(parameters, ref param);

                if (contentType == FileType.Undefined)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type '{parameters[1]}'");
                    return;
                }
            }
            switch (contentType)
            {
                case FileType.LandblockInstance:
                    ConvertJsonLandblock(session, param);
                    break;

                case FileType.Quest:
                    ConvertJsonQuest(session, param);
                    break;

                case FileType.Recipe:
                    ConvertJsonRecipe(session, param);
                    break;

                case FileType.Weenie:
                    ConvertJsonWeenie(session, param);
                    break;
            }
        }

        public static void ImportJsonWeenie(Session session, string wcid)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}";

            var prefix = wcid + " - ";

            if (wcid.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ImportJsonWeenie(session, json_folder, file.Name);
        }

        public static void ConvertJsonWeenie(Session session, string wcid)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}";

            var prefix = wcid + " - ";

            if (wcid.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ConvertJsonWeenie(session, json_folder, file.Name);
        }

        public static void ImportJsonRecipe(Session session, string recipeId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}recipes{sep}";

            var prefix = recipeId.PadLeft(5, '0') + " - ";

            if (recipeId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ImportJsonRecipe(session, json_folder, file.Name);
        }

        public static void ConvertJsonRecipe(Session session, string recipeId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}recipes{sep}";

            var prefix = recipeId.PadLeft(5, '0') + " - ";

            if (recipeId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ConvertJsonRecipe(session, json_folder, file.Name);
        }

        public static void ImportJsonLandblock(Session session, string landblockId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}landblocks{sep}";

            var prefix = landblockId;

            if (landblockId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ImportJsonLandblock(session, json_folder, file.Name);
        }

        public static void ConvertJsonLandblock(Session session, string landblockId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}landblocks{sep}";

            var prefix = landblockId;

            if (landblockId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ConvertJsonLandblock(session, json_folder, file.Name);
        }

        public static void ImportJsonQuest(Session session, string questName)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}quests{sep}";

            var prefix = questName;

            if (questName.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ImportJsonQuest(session, json_folder, file.Name);
        }

        public static void ConvertJsonQuest(Session session, string questName)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var json_folder = $"{di.FullName}{sep}json{sep}quests{sep}";

            var prefix = questName;

            if (questName.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(json_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.json") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}{prefix}*.json");
                return;
            }

            foreach (var file in files)
                ConvertJsonQuest(session, json_folder, file.Name);
        }

        [CommandHandler("convert-json-worldspawns", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Converts worldspawns.json from the Content folder to sql")]
        public static void HandleConvertJsonWorldspawns(Session session, params string[] parameters)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;
            var json_folder = $"{di.FullName}{sep}json{sep}";
            di = new DirectoryInfo(json_folder);
            var files = di.Exists ? di.GetFiles($"worldspawns.json") : null;

            if (files == null || files.Length != 1)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {json_folder}worldspawns.json");
                return;
            }

            if(GDLELoader.TryLoadWorldSpawnsConverted(files[0].FullName, out var results, out var links))
            {
                var instancesByLandblock = new Dictionary<uint, List<LandblockInstance>>();
                foreach(var entry in results)
                {
                    var landblockId = entry.ObjCellId >> 16;

                    if (instancesByLandblock.TryGetValue(landblockId, out var instancesList))
                        instancesList.Add(entry);
                    else
                        instancesByLandblock.Add(landblockId, new List<LandblockInstance>() {entry});
                }

                var sql_folder = $"{json_folder.Replace("json", "sql")}landblocks{sep}";
                di = new DirectoryInfo(sql_folder);

                if (!di.Exists)
                    di.Create();

                foreach (var landblock in instancesByLandblock)
                {
                    var instances = landblock.Value;

                    var sql_filename = $"{landblock.Key:X4}.sql";

                    try
                    {
                        if (LandblockInstanceWriter == null)
                        {
                            LandblockInstanceWriter = new LandblockInstanceWriter();
                            LandblockInstanceWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                            LandblockInstanceWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                            LandblockInstanceWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                            LandblockInstanceWriter.WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                            LandblockInstanceWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                            LandblockInstanceWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                        }

                        sql_filename = LandblockInstanceWriter.GetDefaultFileName(landblock.Key);
                        var sqlFile = new StreamWriter(sql_folder + sql_filename);

                        LandblockInstanceWriter.CreateSQLDELETEStatement(instances, sqlFile);
                        sqlFile.WriteLine();

                        LandblockInstanceWriter.CreateSQLINSERTStatement(instances, sqlFile);

                        sqlFile.Close();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                        return;
                    }

                    CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
                }
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_folder}worldspawns.json");
                return;
            }
        }

        [CommandHandler("import-sql-folders", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Imports all weenie sql data from the Content folder and all sub-folders", "<wcid>\n<wcid> - wcid prefix to search for. can be 'all' to import everything")]
        public static void HandleImportSQLFolders(Session session, params string[] parameters)
        {
            var param = parameters[0];
            ImportSQLWeenie(session, param, true);
        }

        [CommandHandler("import-sql", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Imports sql data from the Content folder", "<type> <wcid>\n<type> - landblock, encounter, quest, recipe, spell, weenie (default if not specified)\n<wcid> - filename prefix to search for. can be 'all' to import all files for this content type")]
        public static void HandleImportSQL(Session session, params string[] parameters)
        {
            var param = parameters[0];
            var contentType = FileType.Weenie;

            if (parameters.Length > 1)
            {
                contentType = GetContentType(parameters, ref param);

                if (contentType == FileType.Undefined)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type '{parameters[1]}'");
                    return;
                }
            }
            try
            {
                switch (contentType)
                {
                    case FileType.LandblockInstance:
                        ImportSQLLandblock(session, param);
                        break;

                    case FileType.Encounter:
                        ImportSQLEncounter(session, param);
                        break;

                    case FileType.Quest:
                        ImportSQLQuest(session, param);
                        break;

                    case FileType.Recipe:
                        ImportSQLRecipe(session, param);
                        break;

                    case FileType.Spell:
                        ImportSQLSpell(session, param);
                        break;

                    case FileType.Weenie:
                        ImportSQLWeenie(session, param);
                        break;
                }
            }
            catch(Exception e)
            {
                CommandHandlerHelper.WriteOutputError(session, $"There was an error importing the SQL:\n\n{e.Message}");
            }
        }

        public static void ImportSQLWeenie(Session session, string wcid, bool withFolders = false)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}";

            var prefix = wcid.PadLeft(5, '0') + " ";

            if (wcid.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            EnumerationOptions options = new EnumerationOptions();
            options.RecurseSubdirectories = withFolders;
            var files = di.Exists ? di.GetFiles($"{prefix}*.sql", options) : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLWeenie(session, file.DirectoryName + sep, file.Name);

        }

        public static void ImportSQLRecipe(Session session, string recipeId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}recipes{sep}";

            var prefix = recipeId.PadLeft(5, '0') + " ";

            if (recipeId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.sql") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLRecipe(session, sql_folder, file.Name);
        }

        public static void ImportSQLLandblock(Session session, string landblockId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}landblocks{sep}";

            var prefix = landblockId;

            if (landblockId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.sql") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLLandblock(session, sql_folder, file.Name);
        }

        public static void ImportSQLEncounter(Session session, string landblockId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}encounters{sep}";

            var prefix = landblockId;

            if (landblockId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.sql") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLEncounter(session, sql_folder, file.Name);
        }

        public static void ImportSQLQuest(Session session, string questName)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}quests{sep}";

            var prefix = questName;

            if (questName.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.sql") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLQuest(session, sql_folder, file.Name);
        }

        public static void ImportSQLSpell(Session session, string spellId)
        {
            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists) return;

            var sep = Path.DirectorySeparatorChar;

            var sql_folder = $"{di.FullName}{sep}sql{sep}spells{sep}";

            var prefix = spellId.PadLeft(5, '0') + " ";

            if (spellId.Equals("all", StringComparison.OrdinalIgnoreCase))
                prefix = "";

            di = new DirectoryInfo(sql_folder);

            var files = di.Exists ? di.GetFiles($"{prefix}*.sql") : null;

            if (files == null || files.Length == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {sql_folder}{prefix}*.sql");
                return;
            }

            foreach (var file in files)
                ImportSQLSpell(session, sql_folder, file.Name);
        }

        /// <summary>
        /// Returns the absolute content folder path, and verifies it exists
        /// </summary>
        private static DirectoryInfo VerifyContentFolder(Session session, bool showError = true)
        {
            var content_folder = PropertyManager.GetString("content_folder").Item;

            var sep = Path.DirectorySeparatorChar;

            // handle relative path
            if (content_folder.StartsWith("."))
            {
                var cwd = Directory.GetCurrentDirectory() + sep;
                content_folder = cwd + content_folder;
            }

            var di = new DirectoryInfo(content_folder);

            if (!di.Exists && showError)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find content folder: {di.FullName}");
                CommandHandlerHelper.WriteOutputInfo(session, "To set your content folder, /modifystring content_folder <path>");
            }
            return di;
        }

        /// <summary>
        /// Converts JSON to SQL, imports to database, and clears the weenie cache
        /// </summary>
        private static void ImportJsonWeenie(Session session, string json_folder, string json_file)
        {
            if (!uint.TryParse(Regex.Match(json_file, @"\d+").Value, out var wcid))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find wcid from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_weenie(session, json_folder, json_file);
            if (sqlFile == null) return;

            // import sql to db
            var sql_folder = json_folder.Replace("json", "sql");
            ImportSQL(sql_folder + sqlFile);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sqlFile}");

            // clear this weenie out of the cache
            DatabaseManager.World.ClearCachedWeenie(wcid);
        }

        private static void ConvertJsonWeenie(Session session, string json_folder, string json_file)
        {
            if (!uint.TryParse(Regex.Match(json_file, @"\d+").Value, out var wcid))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find wcid from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_weenie(session, json_folder, json_file);
            if (sqlFile == null) return;
        }

        private static void ImportJsonRecipe(Session session, string json_folder, string json_file)
        {
            if (!uint.TryParse(Regex.Match(json_file, @"\d+").Value, out var recipeId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find recipe id from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_recipe(session, json_folder, json_file);
            if (sqlFile == null) return;

            // import sql to db
            var sql_folder = json_folder.Replace("json", "sql");
            ImportSQL(sql_folder + sqlFile);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sqlFile}");

            // clear recipe cache
            DatabaseManager.World.ClearCookbookCache();
        }

        private static void ConvertJsonRecipe(Session session, string json_folder, string json_file)
        {
            if (!uint.TryParse(Regex.Match(json_file, @"\d+").Value, out var recipeId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find recipe id from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_recipe(session, json_folder, json_file);
            if (sqlFile == null) return;
        }

        private static void ImportJsonLandblock(Session session, string json_folder, string json_file)
        {
            if (!ushort.TryParse(Regex.Match(json_file, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock id from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_landblock(session, json_folder, json_file);
            if (sqlFile == null) return;

            // import sql to db
            var sql_folder = json_folder.Replace("json", "sql");
            ImportSQL(sql_folder + sqlFile);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sqlFile}");

            // clear any cached instances for this landblock
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblockId);
        }

        private static void ConvertJsonLandblock(Session session, string json_folder, string json_file)
        {
            if (!ushort.TryParse(Regex.Match(json_file, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock id from {json_file}");
                return;
            }

            // convert json -> sql
            var sqlFile = json2sql_landblock(session, json_folder, json_file);
            if (sqlFile == null) return;
        }

        private static void ImportJsonQuest(Session session, string json_folder, string json_file)
        {
            var questName = json_file.TrimEnd(".json");

            // convert json -> sql
            var sqlFile = json2sql_quest(session, json_folder, json_file);
            if (sqlFile == null) return;

            // import sql to db
            var sql_folder = json_folder.Replace("json", "sql");
            ImportSQL(sql_folder + sqlFile);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sqlFile}");

            // clear cached quest
            DatabaseManager.World.ClearCachedQuest(questName);
        }

        private static void ConvertJsonQuest(Session session, string json_folder, string json_file)
        {
            var questName = json_file.TrimEnd(".json");

            // convert json -> sql
            var sqlFile = json2sql_quest(session, json_folder, json_file);
            if (sqlFile == null) return;
        }

        public static WeenieSQLWriter WeenieSQLWriter;

        /// <summary>
        /// Converts a json file to sql file
        /// </summary>
        public static string json2sql_weenie(Session session, string folder, string json_filename)
        {
            var json_file = folder + json_filename;

            // read json into lsd weenie
            var success = LifestonedLoader.TryLoadWeenie(json_file, out var weenie);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to load {json_file}");
                return null;
            }

            // convert to ace weenie
            success = LifestonedConverter.TryConvert(weenie, out var output);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            // output to sql
            var sqlFolder = folder.Replace("json", "sql");

            var di = new DirectoryInfo(sqlFolder);

            if (!di.Exists)
                di.Create();

            var sqlFilename = "";

            try
            {
                if (WeenieSQLWriter == null)
                {
                    WeenieSQLWriter = new WeenieSQLWriter();
                    WeenieSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    WeenieSQLWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    WeenieSQLWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    WeenieSQLWriter.WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                    WeenieSQLWriter.SpellNames = DatabaseManager.World.GetAllSpellNames();
                    WeenieSQLWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    WeenieSQLWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                }

                if (output.LastModified == DateTime.MinValue)
                    output.LastModified = DateTime.UtcNow;

                sqlFilename = WeenieSQLWriter.GetDefaultFileName(output);
                using (StreamWriter sqlFile = new StreamWriter(sqlFolder + sqlFilename))
                {

                    WeenieSQLWriter.CreateSQLDELETEStatement(output, sqlFile);
                    sqlFile.WriteLine();

                    WeenieSQLWriter.CreateSQLINSERTStatement(output, sqlFile);

                    var metadata = new Adapter.GDLE.Models.Metadata(weenie);
                    if (metadata.HasInfo)
                    {
                        var jsonEx = JsonSerializer.Serialize(metadata, LifestonedConverter.SerializerSettings);
                        sqlFile.WriteLine($"\n/* Lifestoned Changelog:\n{jsonEx}\n*/");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {json_filename} to {sqlFilename}");

            return sqlFilename;
        }

        public static CookBookSQLWriter CookBookSQLWriter;
        public static RecipeSQLWriter RecipeSQLWriter;
        public static SpellSQLWriter SpellSQLWriter;

        public static string json2sql_recipe(Session session, string folder, string json_filename)
        {
            var json_file = folder + json_filename;

            // read json into lsd recipe
            var success = GDLELoader.TryLoadRecipeCombined(json_file, out var result);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to load {json_file}");
                return null;
            }

            // convert to ace cookbooks + recipe
            success = GDLEConverter.TryConvert(result, out var cookbooks, out var recipe);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            // output to sql
            var sqlFolder = folder.Replace("json", "sql");

            var di = new DirectoryInfo(sqlFolder);

            if (!di.Exists)
                di.Create();

            var sqlFilename = "";

            try
            {
                if (RecipeSQLWriter == null)
                {
                    RecipeSQLWriter = new RecipeSQLWriter();
                    RecipeSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                }

                if (CookBookSQLWriter == null)
                {
                    CookBookSQLWriter = new CookBookSQLWriter();
                    CookBookSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                }

                if (recipe.LastModified == DateTime.MinValue)
                    recipe.LastModified = DateTime.UtcNow;

                foreach (var cookbook in cookbooks)
                {
                    if (cookbook.LastModified == DateTime.MinValue)
                        cookbook.LastModified = DateTime.UtcNow;
                }

                sqlFilename = RecipeSQLWriter.GetDefaultFileName(recipe, cookbooks);
                using (StreamWriter sqlFile = new StreamWriter(sqlFolder + sqlFilename)) { 
                    RecipeSQLWriter.CreateSQLDELETEStatement(recipe, sqlFile);
                    sqlFile.WriteLine();

                    RecipeSQLWriter.CreateSQLINSERTStatement(recipe, sqlFile);
                    sqlFile.WriteLine();

                    CookBookSQLWriter.CreateSQLDELETEStatement(cookbooks, sqlFile);
                    sqlFile.WriteLine();

                    CookBookSQLWriter.CreateSQLINSERTStatement(cookbooks, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {json_filename} to {sqlFilename}");

            return sqlFilename;
        }

        public static string json2sql_landblock(Session session, string folder, string json_filename)
        {
            var json_file = folder + json_filename;

            // read json into gdle spawnmap
            var success = GDLELoader.TryLoadLandblock(json_file, out var result);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to load {json_file}");
                return null;
            }

            // convert to ace landblock_instances
            success = GDLEConverter.TryConvert(result, out var landblockInstances, out var landblockInstanceLinks);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            // link up instances
            // TODO: move this to TryConvert
            foreach (var link in landblockInstanceLinks)
            {
                var parent = landblockInstances.FirstOrDefault(i => i.Guid == link.ParentGuid);
                if (parent == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find parent guid for {link.ParentGuid:X8}");
                    continue;
                }
                parent.LandblockInstanceLink.Add(link);

                var child = landblockInstances.FirstOrDefault(i => i.Guid == link.ChildGuid);
                if (child == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find child guid for {link.ChildGuid:X8}");
                    continue;
                }
                child.IsLinkChild = true;
            }

            // output to sql
            var sqlFolder = folder.Replace("spawnmaps", "landblock_instances").Replace("json", "sql");

            var di = new DirectoryInfo(sqlFolder);

            if (!di.Exists)
                di.Create();

            var sqlFilename = "";

            try
            {
                if (LandblockInstanceWriter == null)
                {
                    LandblockInstanceWriter = new LandblockInstanceWriter();
                    LandblockInstanceWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    LandblockInstanceWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    LandblockInstanceWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    LandblockInstanceWriter.WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                    LandblockInstanceWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    LandblockInstanceWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                }

                foreach (var landblockInstance in landblockInstances)
                {
                    if (landblockInstance.LastModified == DateTime.MinValue)
                        landblockInstance.LastModified = DateTime.UtcNow;
                }

                foreach (var landblockInstanceLink in landblockInstanceLinks)
                {
                    if (landblockInstanceLink.LastModified == DateTime.MinValue)
                        landblockInstanceLink.LastModified = DateTime.UtcNow;
                }

                sqlFilename = LandblockInstanceWriter.GetDefaultFileName(landblockInstances[0]);

                using (StreamWriter sqlFile = new StreamWriter(sqlFolder + sqlFilename))
                {
                    LandblockInstanceWriter.CreateSQLDELETEStatement(landblockInstances, sqlFile);
                    sqlFile.WriteLine();

                    LandblockInstanceWriter.CreateSQLINSERTStatement(landblockInstances, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {json_filename} to {sqlFilename}");

            return sqlFilename;
        }

        public static QuestSQLWriter QuestSQLWriter;

        public static string json2sql_quest(Session session, string folder, string json_filename)
        {
            var json_file = folder + json_filename;

            // read json quest
            var success = GDLELoader.TryLoadQuest(json_file, out var result);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to load {json_file}");
                return null;
            }

            // convert to sql quest
            success = GDLEConverter.TryConvert(result, out var quest);

            if (!success)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            // output to sql
            var sqlFolder = folder.Replace("json", "sql");
            var sqlFilename = json_filename.Replace(".json", ".sql");

            var di = new DirectoryInfo(sqlFolder);

            if (!di.Exists)
                di.Create();

            try
            {
                if (QuestSQLWriter == null)
                    QuestSQLWriter = new QuestSQLWriter();

                if (quest.LastModified == DateTime.MinValue)
                    quest.LastModified = DateTime.UtcNow;

                using (StreamWriter sqlFile = new StreamWriter(sqlFolder + sqlFilename))
                {
                    QuestSQLWriter.CreateSQLDELETEStatement(quest, sqlFile);
                    sqlFile.WriteLine();

                    QuestSQLWriter.CreateSQLINSERTStatement(quest, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {json_file}");
                return null;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {json_filename} to {sqlFilename}");

            return sqlFilename;
        }

        /// <summary>
        /// Converts SQL to JSON, imports to database, clears the weenie cache
        /// </summary>
        private static void ImportSQLWeenie(Session session, string sql_folder, string sql_file)
        {
            if (!uint.TryParse(Regex.Match(sql_file, @"\d+").Value, out var wcid))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find wcid from {sql_file}");
                return;
            }

            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear this weenie out of the cache
            DatabaseManager.World.ClearCachedWeenie(wcid);

            // load weenie from database
            var weenie = DatabaseManager.World.GetWeenie(wcid);

            if (weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't load weenie {wcid} from db");
                return;
            }

            //sql2json_weenie(session, weenie, sql_folder, sql_file);
        }

        private static void ImportSQLRecipe(Session session, string sql_folder, string sql_file)
        {
            if (!uint.TryParse(Regex.Match(sql_file, @"\d+").Value, out var recipeId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find recipe id from {sql_file}");
                return;
            }

            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear this recipe out of the cache
            DatabaseManager.World.ClearCookbookCache();

            // load cookbooks + recipe from database
            var cookbooks = DatabaseManager.World.GetCookbooksByRecipeId(recipeId);

            if (cookbooks == null || cookbooks.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't load recipe {recipeId} from db");
                return;
            }

            //sql2json_recipe(session, cookbooks, sql_folder, sql_file);
        }

        private static void ImportSQLLandblock(Session session, string sql_folder, string sql_file)
        {
            if (!ushort.TryParse(Regex.Match(sql_file, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock id from {sql_file}");
                return;
            }

            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear any cached instances for this landblock
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblockId);

            // load landblock instances from database
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);

            // convert to json file
            //sql2json_landblock(session, instances, sql_folder, sql_file);
        }

        private static void ImportSQLEncounter(Session session, string sql_folder, string sql_file)
        {
            if (!ushort.TryParse(Regex.Match(sql_file, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock id from {sql_file}");
                return;
            }

            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear any cached encounters for this landblock
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);
        }

        private static void ImportSQLQuest(Session session, string sql_folder, string sql_file)
        {
            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear cached quest
            var questName = sql_file.TrimEnd(".sql");
            DatabaseManager.World.ClearCachedQuest(questName);

            // load quest from db
            var quest = DatabaseManager.World.GetCachedQuest(questName);

            // convert to json file
            //sql2json_quest(session, quest, sql_folder, sql_file);
        }

        private static void ImportSQLSpell(Session session, string sql_folder, string sql_file)
        {
            if (!uint.TryParse(Regex.Match(sql_file, @"\d+").Value, out var spellId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find spell id from {sql_file}");
                return;
            }

            // import sql to db
            ImportSQL(sql_folder + sql_file);
            CommandHandlerHelper.WriteOutputInfo(session, $"Imported {sql_file}");

            // clear this spell out of the cache (and everything else)
            DatabaseManager.World.ClearSpellCache();
            WorldObject.ClearSpellCache();

            // load spell from db
            var spell = DatabaseManager.World.GetCachedSpell(spellId);
        }

        /// <summary>
        /// Converts a sql file to json file
        /// </summary>
        public static bool sql2json_weenie(Session session, Weenie weenie, string sql_folder, string sql_filename)
        {
            if (!LifestonedConverter.TryConvertACEWeenieToLSDJSON(weenie, out var json, out var json_weenie))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {sql_filename} to json");
                return false;
            }

            var json_folder = sql_folder.Replace("sql", "json");
            var json_filename = sql_filename.Replace(".sql", ".json");

            var match = Regex.Match(json_filename, @"^(\d+)");
            if (match.Success)
            {
                var wcid = match.Groups[1].Value;
                if (!json_filename.StartsWith(wcid + " -"))
                    json_filename = wcid + " -" + json_filename.Substring(wcid.Length);
            }

            var di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            if (File.Exists(json_folder + json_filename) && LifestonedLoader.AppendMetadata(json_folder + json_filename, json_weenie))
            {
                json = JsonSerializer.Serialize(json_weenie, LifestonedConverter.SerializerSettings);
            }

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {sql_filename} to {json_filename}");

            return true;
        }

        public static bool sql2json_recipe(Session session, List<CookBook> cookbooks, string sql_folder, string sql_filename)
        {
            if (!GDLEConverter.TryConvert(cookbooks, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {sql_filename} to json");
                return false;
            }

            var json_folder = sql_folder.Replace("sql", "json");
            var json_filename = sql_filename.Replace(".sql", ".json");

            var match = Regex.Match(json_filename, @"^(\d+)");
            if (match.Success)
            {
                var wcid = match.Groups[1].Value;
                if (!json_filename.StartsWith(wcid + " -"))
                    json_filename = wcid + " -" + json_filename.Substring(wcid.Length);
            }

            var di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {sql_filename} to {json_filename}");

            return true;
        }

        public static bool sql2json_landblock(Session session, List<LandblockInstance> instances, string sql_folder, string sql_filename)
        {
            if (GDLEConverter.WeenieNames == null)
                GDLEConverter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();

            if (GDLEConverter.WeenieClassNames == null)
                GDLEConverter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();

            if (!GDLEConverter.TryConvert(instances, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {sql_filename} to json");
                return false;
            }

            var json_folder = sql_folder.Replace("sql", "json");
            var json_filename = sql_filename.Replace(".sql", ".json");

            var di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {sql_filename} to {json_filename}");

            return true;
        }

        public static bool sql2json_quest(Session session, Quest quest, string sql_folder, string sql_filename)
        {
            if (!GDLEConverter.TryConvert(quest, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {sql_filename} to json");
                return false;
            }

            var json_folder = sql_folder.Replace("sql", "json");
            var json_filename = sql_filename.Replace(".sql", ".json");

            var di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Converted {sql_filename} to {json_filename}");

            return true;
        }

        /// <summary>
        /// Imports an SQL file into the database
        /// </summary>
        public static void ImportSQL(string sqlFile)
        {
            var sqlCommands = File.ReadAllText(sqlFile);

            sqlCommands = sqlCommands.Replace("\r\n", "\n");

            // not sure why ExecuteSqlCommand doesn't parse this correctly..
            var idx = sqlCommands.IndexOf($"/* Lifestoned Changelog:");
            if (idx != -1)
                sqlCommands = sqlCommands.Substring(0, idx);

            using (var ctx = new WorldDbContext())
                ctx.Database.ExecuteSqlRaw(sqlCommands);
        }

        public static LandblockInstanceWriter LandblockInstanceWriter;

        [CommandHandler("replaceInst", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Replaces the selected object from the current landblock instances with a new wcid or classname", "<wcid or classname>")]
        public static void HandleReplaceInst(Session session, params string[] parameters)
        {
            var wo = CommandHandlerHelper.GetQueryTarget(session);

            if (wo?.Location == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target.", ChatMessageType.Broadcast));
                return;
            }

            // ensure landblock instance
            uint objGuid = wo.Guid.Full;
            if (!wo.Guid.IsStatic())
            {
                uint? staticGuid = null;
                if (wo.Generator != null)
                {
                    // if generator child, try getting the "real" guid
                    staticGuid = wo.Generator.GetStaticGuid(objGuid);
                    if (staticGuid != null)
                        objGuid = staticGuid.Value;
                }

                if (staticGuid == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.Name} ({wo.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                    return;
                }
            }

            var landblock = (ushort)wo.Location.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == objGuid);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{objGuid:X8})", ChatMessageType.Broadcast));
                return;
            }

            Weenie weenie = null;

            var WcidOrClassName = parameters[0];
            if (uint.TryParse(WcidOrClassName, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenie = DatabaseManager.World.GetWeenie(WcidOrClassName);  // classname

            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {WcidOrClassName}", ChatMessageType.Broadcast));
                return;
            }

            instance.WeenieClassId = weenie.ClassId;

            SyncInstances(session, landblock, instances);
            DeveloperCommands.HandleReloadLandblock(session);
        }

        private static uint DefaultInstParentGuid = 0;
        [CommandHandler("setDefaultInstParent", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Sets the selected object as the default parent for subsequent calls to createInst and setInstParent")]
        public static void HandleSetDefaultInstParent(Session session, params string[] parameters)
        {
            var wo = CommandHandlerHelper.GetQueryTarget(session);

            if (wo?.Location == null) return;

            var landblock = (ushort)wo.Location.Landblock;

            // if generator child, try getting the "real" guid
            var guid = wo.Guid.Full;
            if (wo.Generator != null)
            {
                var staticGuid = wo.Generator.GetStaticGuid(guid);
                if (staticGuid != null)
                    guid = staticGuid.Value;
            }

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == guid);

            if (instance == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{guid:X8})");
                return;
            }

            DefaultInstParentGuid = instance.Guid;
            CommandHandlerHelper.WriteOutputInfo(session, $"Default parent set to 0x{DefaultInstParentGuid:X8}.");
        }

        [CommandHandler("clearDefaultInstParent", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Clears the default parent for subsequent calls to createInst and setInstParent")]
        public static void HandleClearDefaultInstParent(Session session, params string[] parameters)
        {
            DefaultInstParentGuid = 0;
            CommandHandlerHelper.WriteOutputInfo(session, "Cleared default parent.");
        }

        [CommandHandler("setInstParent", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Makes the selected object from the current landblock instances a child of the specified GUID or DefaultInstParentGuid if no GUID is specified", "<parent GUID>")]
        public static void HandleSetInstParent(Session session, params string[] parameters)
        {
            var wo = CommandHandlerHelper.GetQueryTarget(session);

            if (wo?.Location == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target.", ChatMessageType.Broadcast));
                return;
            }

            // ensure landblock instance
            uint objGuid = wo.Guid.Full;
            if (!wo.Guid.IsStatic())
            {
                uint? staticGuid = null;
                if (wo.Generator != null)
                {
                    // if generator child, try getting the "real" guid
                    staticGuid = wo.Generator.GetStaticGuid(objGuid);
                    if (staticGuid != null)
                        objGuid = staticGuid.Value;
                }

                if (staticGuid == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.Name} ({wo.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                    return;
                }
            }

            var landblock = (ushort)wo.Location.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == objGuid);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{objGuid:X8})", ChatMessageType.Broadcast));
                return;
            }

            uint newParentGuid = 0;

            var firstStaticGuid = 0x70000000 | (uint)landblock << 12;

            if (parameters.Length > 0)
            {
                var parentGuidStr = parameters[0];

                if (parentGuidStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    parentGuidStr = parentGuidStr.Substring(2);

                if (!uint.TryParse(parentGuidStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var _parentGuid))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't parse parent guid {parentGuidStr}", ChatMessageType.Broadcast));
                    return;
                }

                newParentGuid = _parentGuid;

                if (newParentGuid <= 0xFFF)
                    newParentGuid = firstStaticGuid | newParentGuid;

            }
            else if (DefaultInstParentGuid != 0)
                newParentGuid = DefaultInstParentGuid;

            if (wo.ParentLink != null && wo.ParentLink.Guid.Full == newParentGuid)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Parent already set to 0x{newParentGuid:X8}.", ChatMessageType.Broadcast));
                return;
            }

            LandblockInstance newParentInstance = instances.FirstOrDefault(i => i.Guid == newParentGuid);
            if (newParentInstance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for new parent guid 0x{newParentGuid:X8}", ChatMessageType.Broadcast));
                return;
            }

            WorldObject newParentObj = session.Player.CurrentLandblock.GetObject(newParentGuid);
            if (newParentObj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find new parent object 0x{newParentGuid:X8}", ChatMessageType.Broadcast));
                return;
            }

            if (instance.IsLinkChild)
            {
                WorldObject currentParentObj = wo.ParentLink;
                if (currentParentObj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"ParentLink is null even though we're set as IsLinkChild", ChatMessageType.Broadcast));
                    return;
                }

                LandblockInstance currentParentInstance = instances.FirstOrDefault(i => i.Guid == currentParentObj.Guid.Full);
                if (currentParentInstance == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for parent guid 0x{currentParentObj.Guid.Full:X8}", ChatMessageType.Broadcast));
                    return;
                }

                var currentLink = currentParentInstance.LandblockInstanceLink.FirstOrDefault(i => i.ChildGuid == instance.Guid);

                currentParentInstance.LandblockInstanceLink.Remove(currentLink);

                currentParentObj.LinkedInstances.Remove(instance);

                currentParentObj.ChildLinks.Remove(wo);
            }

            var newLink = new LandblockInstanceLink();
            newLink.ParentGuid = newParentGuid;
            newLink.ChildGuid = instance.Guid;
            newLink.LastModified = DateTime.Now;

            newParentInstance.LandblockInstanceLink.Add(newLink);
            newParentObj.LinkedInstances.Add(instance);

            // ActivateLinks?
            newParentObj.SetLinkProperties(wo);
            newParentObj.ChildLinks.Add(wo);

            wo.ParentLink = newParentObj;
            instance.IsLinkChild = true;

            SyncInstances(session, landblock, instances);

            session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.WeenieClassId} - {wo.Name} (0x{instance.Guid:X8}) parent set to 0x{newParentGuid:X8}", ChatMessageType.Broadcast));
        }

        [CommandHandler("clearInstParent", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Clears the parent of the selected object from the current landblock instances")]
        public static void HandleClearInstParent(Session session, params string[] parameters)
        {
            var wo = CommandHandlerHelper.GetQueryTarget(session);

            if (wo?.Location == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target.", ChatMessageType.Broadcast));
                return;
            }

            // ensure landblock instance
            uint objGuid = wo.Guid.Full;
            if (!wo.Guid.IsStatic())
            {
                uint? staticGuid = null;
                if (wo.Generator != null)
                {
                    // if generator child, try getting the "real" guid
                    staticGuid = wo.Generator.GetStaticGuid(objGuid);
                    if (staticGuid != null)
                        objGuid = staticGuid.Value;
                }

                if (staticGuid == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.Name} ({wo.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                    return;
                }
            }

            var landblock = (ushort)wo.Location.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == objGuid);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{objGuid:X8})", ChatMessageType.Broadcast));
                return;
            }

            if (wo.ParentLink == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.WeenieClassId} - {wo.Name} (0x{instance.Guid:X8}) already has no parent", ChatMessageType.Broadcast));
                return;
            }

            WorldObject currentParentObj = wo.ParentLink;
            LandblockInstance currentParentInstance = instances.FirstOrDefault(i => i.Guid == currentParentObj.Guid.Full);
            if (currentParentInstance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for parent guid 0x{currentParentObj.Guid.Full:X8}", ChatMessageType.Broadcast));
                return;
            }

            var currentLink = currentParentInstance.LandblockInstanceLink.FirstOrDefault(i => i.ChildGuid == instance.Guid);

            currentParentInstance.LandblockInstanceLink.Remove(currentLink);

            currentParentObj.LinkedInstances.Remove(instance);

            currentParentObj.ChildLinks.Remove(wo);

            instance.IsLinkChild = false;

            wo.ParentLink = null;

            SyncInstances(session, landblock, instances);

            session.Network.EnqueueSend(new GameMessageSystemChat($"{wo.WeenieClassId} - {wo.Name} (0x{instance.Guid:X8}) parent cleared", ChatMessageType.Broadcast));
        }

        [CommandHandler("createinst", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Spawns a new wcid or classname as a landblock instance", "<wcid or classname>\n\nTo create a parent/child relationship: /createinst -p <parent guid> -c <wcid or classname>\nTo automatically get the parent guid from the last appraised object: /createinst -p -c <wcid or classname>\n\nTo manually specify a start guid: /createinst <wcid or classname> <start guid>\nStart guids can be in the range 0x000-0xFFF, or they can be prefixed with 0x7<landblock id>")]
        public static void HandleCreateInst(Session session, params string[] parameters)
        {
            var loc = new Position(session.Player.Location);

            var param = parameters[0];

            Weenie weenie = null;

            uint parentGuid = 0;

            var landblock = session.Player.CurrentLandblock.Id.Landblock;

            var firstStaticGuid = 0x70000000 | (uint)landblock << 12;

            if (parameters.Length > 1)
            {
                var allParams = string.Join(" ", parameters);

                var match = Regex.Match(allParams, @"-p ([\S]+) -c ([\S]+)", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var parentGuidStr = match.Groups[1].Value;
                    param = match.Groups[2].Value;

                    if (parentGuidStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        parentGuidStr = parentGuidStr.Substring(2);

                    if (!uint.TryParse(parentGuidStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var _parentGuid))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't parse parent guid {match.Groups[1].Value}", ChatMessageType.Broadcast));
                        return;
                    }

                    parentGuid = _parentGuid;

                    if (parentGuid <= 0xFFF)
                        parentGuid = firstStaticGuid | parentGuid;
                }

                else if (parameters[1].StartsWith("-c", StringComparison.OrdinalIgnoreCase))
                {
                    // get parent from last appraised object
                    var parent = CommandHandlerHelper.GetLastAppraisedObject(session);

                    if (parent == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent object", ChatMessageType.Broadcast));
                        return;
                    }

                    parentGuid = parent.Guid.Full;
                }

            }
            else if (DefaultInstParentGuid != 0)
                parentGuid = DefaultInstParentGuid;

            if (uint.TryParse(param, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenie = DatabaseManager.World.GetWeenie(param);  // classname

            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {param}", ChatMessageType.Broadcast));
                return;
            }

            uint startGuid = 0;
            // manually specify a start guid?
            if (parameters.Length == 2)
                uint.TryParse(parameters[1].Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out startGuid);

            CreateLandblockInstance(session, weenie, loc, parentGuid, startGuid);
        }

        public static uint CreateLandblockInstance(Session session, Weenie weenie, Position loc, uint parentGuid = 0, uint nextStaticGuid = 0, bool skipZNudge = false)
        {
            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid weenie.", ChatMessageType.Broadcast));
                return 0;
            }

            if (loc == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid location.", ChatMessageType.Broadcast));
                return 0;
            }

            var landblock = loc.LandblockId.Landblock;

            // clear any cached instances for this landblock
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock);

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            // for link mode, ensure parent guid instance exists
            WorldObject parentObj = null;
            LandblockInstance parentInstance = null;

            if (parentGuid != 0)
            {
                parentInstance = instances.FirstOrDefault(i => i.Guid == parentGuid);

                if (parentInstance == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for parent guid 0x{parentGuid:X8}", ChatMessageType.Broadcast));
                    return 0;
                }

                parentObj = session.Player.CurrentLandblock.GetObject(parentGuid);

                if (parentObj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent object 0x{parentGuid:X8}", ChatMessageType.Broadcast));
                    return 0;
                }
            }

            var firstStaticGuid = 0x70000000 | (uint)landblock << 12;
            var maxStaticGuid = firstStaticGuid | 0xFFF;

            // manually specify a start guid?
            if (nextStaticGuid > 0)
            {
                if (nextStaticGuid <= 0xFFF)
                    nextStaticGuid = firstStaticGuid | nextStaticGuid;

                if (nextStaticGuid < firstStaticGuid || nextStaticGuid > maxStaticGuid)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock instance guid {nextStaticGuid:X8} must be between {firstStaticGuid:X8} and {maxStaticGuid:X8}", ChatMessageType.Broadcast));
                    return 0;
                }

                var existing = instances.FirstOrDefault(i => i.Guid == nextStaticGuid);

                if (existing != null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock instance guid {nextStaticGuid:X8} already exists", ChatMessageType.Broadcast));
                    return 0;
                }
            }
            else
                nextStaticGuid = GetNextStaticGuid(landblock, instances);

            if (nextStaticGuid > maxStaticGuid)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock {landblock:X4} has reached the maximum # of static guids", ChatMessageType.Broadcast));
                return 0;
            }

            // create and spawn object
            var entityWeenie = Database.Adapter.WeenieConverter.ConvertToEntityWeenie(weenie);

            var wo = WorldObjectFactory.CreateWorldObject(entityWeenie, new ObjectGuid(nextStaticGuid));

            if (wo == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to create new object for {weenie.ClassId} - {weenie.ClassName}", ChatMessageType.Broadcast));
                return 0;
            }

            var isLinkChild = parentInstance != null;

            if (!wo.Stuck && !isLinkChild)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{weenie.ClassId} - {weenie.ClassName} is missing PropertyBool.Stuck, cannot spawn as landblock instance unless it is a child object", ChatMessageType.Broadcast));
                return 0;
            }

            // spawn as ethereal temporarily, to spawn directly on player position
            wo.Ethereal = true;
            wo.Location = new Position(loc);

            if (!skipZNudge)
            {
                // even on flat ground, objects can sometimes fail to spawn at the player's current Z
                // Position.Z has some weird thresholds when moving around, but i guess the same logic doesn't apply when trying to spawn in...
                wo.Location.PositionZ += 0.05f;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Creating new landblock instance {(isLinkChild ? "child object " : "")}@ {loc.ToLOCString()}\n{wo.WeenieClassId} - {wo.Name} ({nextStaticGuid:X8})", ChatMessageType.Broadcast));

            if (!wo.EnterWorld())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Failed to spawn new object at this location", ChatMessageType.Broadcast));
                return 0;
            }

            // create new landblock instance
            var instance = CreateLandblockInstanceObj(wo, isLinkChild);

            instances.Add(instance);

            if (isLinkChild)
            {
                var link = new LandblockInstanceLink();

                link.ParentGuid = parentGuid;
                link.ChildGuid = wo.Guid.Full;
                link.LastModified = DateTime.Now;

                parentInstance.LandblockInstanceLink.Add(link);

                parentObj.LinkedInstances.Add(instance);

                // ActivateLinks?
                parentObj.SetLinkProperties(wo);
                parentObj.ChildLinks.Add(wo);
                wo.ParentLink = parentObj;

                wo.EnqueueBroadcast(new GameMessageUpdateObject(wo));
            }

            SyncInstances(session, landblock, instances);

            return nextStaticGuid;
        }

        public static int CreateLandblockInstances(Session session, List<(uint guid, Weenie weenie, Position loc, uint parentGuid)> instancesToCreate, bool skipZNudge = false)
        {
            var createdCounter = 0;
            var landblock = instancesToCreate.First().loc.LandblockId.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            // clear any cached instances for this landblock
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock);

            foreach (var entry in instancesToCreate)
            {
                if (entry.weenie == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid weenie in guid 0x{entry.guid:X8}.", ChatMessageType.Broadcast));
                    continue;
                }

                if (entry.loc == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid location in guid 0x{entry.guid:X8}.", ChatMessageType.Broadcast));
                    continue;
                }

                var entryLandblock = entry.loc.LandblockId.Landblock;

                if(entryLandblock != landblock)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Diverging landblock in guid 0x{entry.guid:X8}.", ChatMessageType.Broadcast));
                    continue;
                }

                // for link mode, ensure parent guid instance exists
                WorldObject parentObj = null;
                LandblockInstance parentInstance = null;

                if (entry.parentGuid != 0)
                {
                    parentInstance = instances.FirstOrDefault(i => i.Guid == entry.parentGuid);

                    if (parentInstance == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock instance for parent guid 0x{entry.parentGuid:X8} in guid 0x{entry.guid:X8}", ChatMessageType.Broadcast));
                        continue;
                    }

                    parentObj = session.Player.CurrentLandblock.GetObject(entry.parentGuid);

                    if (parentObj == null)
                    { 
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent object 0x{entry.parentGuid:X8} in guid 0x{entry.guid:X8}", ChatMessageType.Broadcast));
                        continue;
                    }
                }

                var firstStaticGuid = 0x70000000 | (uint)landblock << 12;
                var maxStaticGuid = firstStaticGuid | 0xFFF;

                var nextStaticGuid = GetNextStaticGuid(landblock, instances);

                if (nextStaticGuid > maxStaticGuid)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Landblock {landblock:X4} has reached the maximum # of static guids", ChatMessageType.Broadcast));
                    continue;
                }

                // create and spawn object
                var entityWeenie = Database.Adapter.WeenieConverter.ConvertToEntityWeenie(entry.weenie);

                var wo = WorldObjectFactory.CreateWorldObject(entityWeenie, new ObjectGuid(nextStaticGuid));

                if (wo == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to create new object for {entry.weenie.ClassId} - {entry.weenie.ClassName}", ChatMessageType.Broadcast));
                    continue;
                }

                var isLinkChild = parentInstance != null;

                if (!wo.Stuck && !isLinkChild)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{entry.weenie.ClassId} - {entry.weenie.ClassName} is missing PropertyBool.Stuck, cannot spawn as landblock instance unless it is a child object", ChatMessageType.Broadcast));
                    continue;
                }

                // spawn as ethereal temporarily, to spawn directly on player position
                wo.Ethereal = true;
                wo.Location = new Position(entry.loc);

                if (!skipZNudge)
                {
                    // even on flat ground, objects can sometimes fail to spawn at the player's current Z
                    // Position.Z has some weird thresholds when moving around, but i guess the same logic doesn't apply when trying to spawn in...
                    wo.Location.PositionZ += 0.05f;
                }

                session.Network.EnqueueSend(new GameMessageSystemChat($"Creating new landblock instance {(isLinkChild ? "child object " : "")}@ {entry.loc.ToLOCString()}\n{wo.WeenieClassId} - {wo.Name} ({nextStaticGuid:X8})", ChatMessageType.Broadcast));

                if (!wo.EnterWorld())
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("Failed to spawn new object at this location", ChatMessageType.Broadcast));
                    continue;
                }

                // create new landblock instance
                var instance = CreateLandblockInstanceObj(wo, isLinkChild);

                instances.Add(instance);

                if (isLinkChild)
                {
                    var link = new LandblockInstanceLink();

                    link.ParentGuid = entry.parentGuid;
                    link.ChildGuid = wo.Guid.Full;
                    link.LastModified = DateTime.Now;

                    parentInstance.LandblockInstanceLink.Add(link);

                    parentObj.LinkedInstances.Add(instance);

                    // ActivateLinks?
                    parentObj.SetLinkProperties(wo);
                    parentObj.ChildLinks.Add(wo);
                    wo.ParentLink = parentObj;

                    wo.EnqueueBroadcast(new GameMessageUpdateObject(wo));
                }

                createdCounter++;
            }

            SyncInstances(session, landblock, instances);

            CommandHandlerHelper.WriteOutputInfo(session, $"Created {createdCounter} of {instancesToCreate.Count} instances.");
            return createdCounter;
        }

        /// <summary>
        /// Serializes landblock instances to XXYY.sql file,
        /// import into database, and clears the cached landblock instances
        /// </summary>
        public static void SyncInstances(Session session, ushort landblock, List<LandblockInstance> instances)
        {
            // serialize to .sql file
            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}sql{sep}landblocks{sep}");

            if (!folder.Exists)
                folder.Create();

            var sqlFilename = $"{folder.FullName}{sep}{LandblockInstanceWriter.GetDefaultFileName(landblock)}";

            if (instances.Count > 0)
            {
                var fileWriter = new StreamWriter(sqlFilename);

                if (LandblockInstanceWriter == null)
                {
                    LandblockInstanceWriter = new LandblockInstanceWriter();
                    LandblockInstanceWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    LandblockInstanceWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    LandblockInstanceWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    LandblockInstanceWriter.WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                    LandblockInstanceWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    LandblockInstanceWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                }

                LandblockInstanceWriter.CreateSQLDELETEStatement(instances, fileWriter);

                fileWriter.WriteLine();

                LandblockInstanceWriter.CreateSQLINSERTStatement(instances, fileWriter);

                fileWriter.Close();

                // import into db
                ImportSQL(sqlFilename);
            }
            else
            {
                // handle special case: deleting the last instance from landblock
                File.Delete(sqlFilename);

                using (var ctx = new WorldDbContext())
                    ctx.Database.ExecuteSqlInterpolated($"DELETE FROM landblock_instance WHERE landblock={landblock};");
            }

            // clear landblock instances for this landblock (again)
            DatabaseManager.World.ClearCachedInstancesByLandblock(landblock);
        }

        public static LandblockInstance CreateLandblockInstanceObj(WorldObject wo, bool isLinkChild = false)
        {
            var instance = new LandblockInstance();

            instance.Guid = wo.Guid.Full;

            instance.Landblock = (int)wo.Location.Landblock;

            instance.WeenieClassId = wo.WeenieClassId;

            instance.ObjCellId = wo.Location.Cell;

            instance.OriginX = wo.Location.PositionX;
            instance.OriginY = wo.Location.PositionY;
            instance.OriginZ = wo.Location.PositionZ;

            instance.AnglesW = wo.Location.RotationW;
            instance.AnglesX = wo.Location.RotationX;
            instance.AnglesY = wo.Location.RotationY;
            instance.AnglesZ = wo.Location.RotationZ;

            instance.IsLinkChild = isLinkChild;

            instance.LastModified = DateTime.Now;

            return instance;
        }

        public static uint GetNextStaticGuid(ushort landblock, List<LandblockInstance> instances)
        {
            var firstGuid = 0x70000000 | ((uint)landblock << 12);
            var lastGuid = firstGuid | 0xFFF;

            var highestLandblockInst = instances.Where(i => i.Landblock == landblock).OrderByDescending(i => i.Guid).FirstOrDefault();

            if (highestLandblockInst == null)
                return firstGuid;

            var nextGuid = highestLandblockInst.Guid + 1;

            if (nextGuid <= lastGuid)
                return nextGuid;

            // try more exhaustive search
            return GetNextStaticGuid_GapFinder(landblock, instances) ?? nextGuid;
        }

        public static uint? GetNextStaticGuid_GapFinder(ushort landblock, List<LandblockInstance> instances)
        {
            var landblockGuids = instances.Where(i => i.Landblock == landblock).Select(i => i.Guid).ToHashSet();

            var firstGuid = 0x70000000 | ((uint)landblock << 12);
            var lastGuid = firstGuid | 0xFFF;

            for (var guid = firstGuid; guid <= lastGuid; guid++)
            {
                if (!landblockGuids.Contains(guid))
                    return guid;
            }
            return null;
        }

        [CommandHandler("removeinst", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Removes the last appraised object from the current landblock instances")]
        public static void HandleRemoveInst(Session session, params string[] parameters)
        {
            RemoveInstance(session);
        }

        public static void RemoveInstance(Session session, bool confirmed = false)
        {
            var wo = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (wo?.Location == null) return;

            var landblock = (ushort)wo.Location.Landblock;

            // if generator child, try getting the "real" guid
            var guid = wo.Guid.Full;
            if (wo.Generator != null)
            {
                var staticGuid = wo.Generator.GetStaticGuid(guid);
                if (staticGuid != null)
                    guid = staticGuid.Value;
            }

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == guid);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{guid:X8})", ChatMessageType.Broadcast));
                return;
            }

            var numChilds = instance.LandblockInstanceLink.Count;

            if (numChilds > 0 && !confirmed)
            {
                // get total numChilds iteratively
                numChilds = 0;
                foreach (var link in instance.LandblockInstanceLink)
                    numChilds += GetNumChilds(session, link, instances);

                // require confirmation for parent objects
                var msg = $"Are you sure you want to delete this parent object, and {numChilds} child object{(numChilds != 1 ? "s" : "")}?";
                if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => RemoveInstance(session, true)), msg))
                    session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            if (instance.IsLinkChild)
            {
                LandblockInstanceLink link = null;

                foreach (var parent in instances.Where(i => i.LandblockInstanceLink.Count > 0))
                {
                    link = parent.LandblockInstanceLink.FirstOrDefault(i => i.ChildGuid == instance.Guid);

                    if (link != null)
                    {
                        parent.LandblockInstanceLink.Remove(link);
                        break;
                    }
                }
                if (link == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent link for child {wo.WeenieClassId} - {wo.Name} (0x{guid:X8})", ChatMessageType.Broadcast));
                    return;
                }
            }

            wo.DeleteObject();

            foreach (var link in instance.LandblockInstanceLink)
                RemoveChild(session, link, instances);

            instances.Remove(instance);

            SyncInstances(session, landblock, instances);

            session.Network.EnqueueSend(new GameMessageSystemChat($"Removed {(instance.IsLinkChild ? "child " : "")}{wo.WeenieClassId} - {wo.Name} (0x{guid:X8}) from landblock instances", ChatMessageType.Broadcast));
        }

        public static int GetNumChilds(Session session, LandblockInstanceLink link, List<LandblockInstance> instances)
        {
            var child = instances.FirstOrDefault(i => i.Guid == link.ChildGuid);

            if (child == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find child instance 0x{link.ChildGuid:X8}", ChatMessageType.Broadcast));
                return 0;
            }

            var numChilds = 1;

            foreach (var subLink in child.LandblockInstanceLink)
                numChilds += GetNumChilds(session, subLink, instances);

            return numChilds;
        }

        public static void RemoveChild(Session session, LandblockInstanceLink link, List<LandblockInstance> instances)
        {
            var child = instances.FirstOrDefault(i => i.Guid == link.ChildGuid);

            if (child == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find child instance 0x{link.ChildGuid:X8}", ChatMessageType.Broadcast));
                return;
            }

            instances.Remove(child);

            var wo = session.Player.CurrentLandblock.GetObject(child.Guid);

            if (wo != null)
            {
                wo.DeleteObject();

                session.Network.EnqueueSend(new GameMessageSystemChat($"Removed child {wo.WeenieClassId} - {wo.Name} (0x{wo.Guid}) from landblock instances", ChatMessageType.Broadcast));
            }
            else
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find child object for 0x{link.ChildGuid:X8}", ChatMessageType.Broadcast));

            foreach (var subLink in child.LandblockInstanceLink)
                RemoveChild(session, subLink, instances);
        }

        [CommandHandler("showEnc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Lists encounters contained in the current landblock")]
        public static void HandleShowEnc(Session session, params string[] parameters)
        {
            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Only the outdoors have encounters!", ChatMessageType.Broadcast));
                return;
            }

            if (session == null)
                return;

            var landblockId = (ushort)pos.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);

            session.Network.EnqueueSend(new GameMessageSystemChat($"--- Landblock 0x{landblockId:X4}: {encounters.Count} Encounters Found ---", ChatMessageType.Broadcast));
            int counter = 1;
            foreach (var entry in encounters)
            {
                Weenie weenie = DatabaseManager.World.GetWeenie(entry.WeenieClassId);
                session.Network.EnqueueSend(new GameMessageSystemChat($"{counter}: {weenie.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}({weenie.ClassId}/{weenie.ClassName}) - cellX: {entry.CellX} - cellY: {entry.CellY}", ChatMessageType.Broadcast));
                counter++;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"---", ChatMessageType.Broadcast));
        }

        [CommandHandler("showEncArea", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Lists encounters contained in a 3x3 area centered on the current landblock")]
        public static void HandleShowEncArea(Session session, params string[] parameters)
        {
            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Only the outdoors have encounters!", ChatMessageType.Broadcast));
                return;
            }

            uint startLandblockId = (ushort)pos.Landblock;

            int startX = (byte)(startLandblockId >> 8);
            int startY = (byte)startLandblockId;
            int endX = startX;
            int endY = startY;

            var radius = 1;
            var erodeCorners = false;

            startX -= radius;
            startY -= radius;
            endX += radius;
            endY += radius;

            startX = Math.Clamp(startX, 0, 255);
            startY = Math.Clamp(startY, 0, 255);
            endX = Math.Clamp(endX, 0, 255);
            endY = Math.Clamp(endY, 0, 255);

            var encounters = new List<Encounter>();
            for (byte x = (byte)startX; x <= endX; x++)
            {
                for (byte y = (byte)startY; y <= endY; y++)
                {
                    if (erodeCorners && ((x == startX && y == startY) || (x == endX && y == endY) || (x == startX && y == endY) || (x == endX && y == startY)))
                        continue;

                    var landblockId = (ushort)(x << 8 | y);

                    // clear any cached encounters for this landblock so we get the unmodified entries
                    DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);

                    // get existing encounters for this landblock
                    encounters.AddRange(DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _));
                }
            }

            var encountersGrouped = new Dictionary<uint, int>();
            foreach (var entry in encounters)
            {
                var count = encountersGrouped.GetValueOrDefault(entry.WeenieClassId, 0);
                encountersGrouped[entry.WeenieClassId] = count + 1;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"--- Landblock 3x3 area centered around 0x{startLandblockId:X4}: {encounters.Count} Encounters Found ---", ChatMessageType.Broadcast));
            foreach (var entry in encountersGrouped)
            {
                Weenie weenie = DatabaseManager.World.GetWeenie(entry.Key);
                session.Network.EnqueueSend(new GameMessageSystemChat($"{entry.Value} {weenie.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}({weenie.ClassId}/{weenie.ClassName})", ChatMessageType.Broadcast));
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"---", ChatMessageType.Broadcast));
        }

        //[CommandHandler("replaceEncRect", AccessLevel.Developer, CommandHandlerFlag.ConsoleInvoke, "")]
        //public static void HandleReplaceEncRect(Session session, params string[] parameters)
        //{
        //    CommandHandlerHelper.WriteOutputInfo(session, "Replacing encounters in landblocks...", ChatMessageType.Broadcast);
        //    var list = new List<ushort>();
        //    int counter = 0;
        //    for (int x = 0x9a; x < 0xba; x++)
        //    {
        //        for (int y = 0xa7; y < 0xc4; y++)
        //        {
        //            ushort landblockId = (ushort)(x << 8 | y);

        //            if (DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _).Count != 0)
        //                list.Add(landblockId);
        //            ReplaceEncounterByLandblock(session, 23185, 23176, landblockId);
        //            counter++;
        //            if (counter % 10 == 0)
        //                CommandHandlerHelper.WriteOutputInfo(session, $"Replacing encounters in landblocks, currently at {counter} landblocks checked, please standby.", ChatMessageType.Broadcast);
        //        }
        //    }
        //    CommandHandlerHelper.WriteOutputInfo(session, $"Replaced encounters in {counter} landblocks.", ChatMessageType.Broadcast);
        //}

        public static void ReplaceEncounterByLandblock(Session session, uint encounterWcidSource, uint encounterWcidTarget, ushort landblockId)
        {
            Weenie weenieSource = null;
            Weenie weenieTarget = null;

            weenieSource = DatabaseManager.World.GetWeenie(encounterWcidSource);
            weenieTarget = DatabaseManager.World.GetWeenie(encounterWcidTarget);

            if (weenieSource == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find weenie {encounterWcidSource}", ChatMessageType.Broadcast);
                return;
            }

            if (weenieTarget == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find weenie {encounterWcidTarget}", ChatMessageType.Broadcast);
                return;
            }

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);

            foreach (var entry in encounters)
            {
                if (entry.WeenieClassId == weenieSource.ClassId)
                {
                    entry.WeenieClassId = weenieTarget.ClassId;
                    entry.LastModified = DateTime.Now;
                }
            }

            if(session != null)
                CommandHandlerHelper.WriteOutputInfo(session, $"Replacing entries of encounter {weenieSource.ClassId}({weenieSource.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) for {weenieTarget.ClassId}({weenieTarget.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) @ landblock 0x{landblockId:X4}", ChatMessageType.Broadcast);

            SyncEncounters(session, landblockId, encounters);
        }

        [CommandHandler("replaceenc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2, "Replaces an encounter in the current outdoor landblock for another", "<wcid or classname of the old encounter> <wcid or classname of the new encounter>")]
        public static void HandleReplaceEncounter(Session session, params string[] parameters)
        {
            var paramSource = parameters[0];
            var paramTarget = parameters[1];

            Weenie weenieSource = null;
            Weenie weenieTarget = null;

            uint wcid;
            if (uint.TryParse(paramSource, out wcid))
                weenieSource = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenieSource = DatabaseManager.World.GetWeenie(paramSource);  // classname

            if (uint.TryParse(paramTarget, out wcid))
                weenieTarget = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenieTarget = DatabaseManager.World.GetWeenie(paramTarget);  // classname

            if (weenieSource == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {paramSource}", ChatMessageType.Broadcast));
                return;
            }

            if (weenieTarget == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {paramTarget}", ChatMessageType.Broadcast));
                return;
            }

            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You must be outdoors to replace an encounter!", ChatMessageType.Broadcast));
                return;
            }

            var landblock = (ushort)pos.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblock);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblock, out _);

            var counter = 0;
            foreach(var entry in encounters)
            {
                if (entry.WeenieClassId == weenieSource.ClassId)
                {
                    entry.WeenieClassId = weenieTarget.ClassId;
                    entry.LastModified = DateTime.Now;
                    counter++;
                }
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Replaced {counter} entries of encounter {weenieSource.ClassId}({weenieSource.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) for {weenieTarget.ClassId}({weenieTarget.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) at landblock 0x{pos.Landblock:X4}", ChatMessageType.Broadcast));

            SyncEncounters(session, landblock, encounters);
            DeveloperCommands.HandleReloadLandblock(session);

        }

        [CommandHandler("replaceEncSelected", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Replace the last appraised encounter for another", "<wcid or classname>")]
        public static void HandleReplaceEncSelected(Session session, params string[] parameters)
        {
            var paramTarget = parameters[0];

            Weenie weenieTarget = null;

            uint wcid;
            if (uint.TryParse(paramTarget, out wcid))
                weenieTarget = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenieTarget = DatabaseManager.World.GetWeenie(paramTarget);  // classname

            if (weenieTarget == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {paramTarget}", ChatMessageType.Broadcast));
                return;
            }

            var obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target!", ChatMessageType.Broadcast));
                return;
            }

            // find root generator
            while (obj.Generator != null)
                obj = obj.Generator;

            if (!(obj.GetProperty(PropertyBool.IsEncounterGenerator) ?? false))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target!", ChatMessageType.Broadcast));
                return;
            }

            var cellX = (int)Math.Floor(obj.Location.PositionX / Physics.Common.LandDefs.CellLength);
            var cellY = (int)Math.Floor(obj.Location.PositionY / Physics.Common.LandDefs.CellLength);

            var landblockId = (ushort)obj.Location.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);

            // check for existing encounter
            var encounter = encounters.FirstOrDefault(i => i.CellX == cellX && i.CellY == cellY && i.WeenieClassId == obj.WeenieClassId);

            if (encounter == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find encounter for {obj.WeenieClassId} - {obj.Name}", ChatMessageType.Broadcast));
                return;
            }

            encounter.WeenieClassId = weenieTarget.ClassId;
            encounter.LastModified = DateTime.Now;

            session.Network.EnqueueSend(new GameMessageSystemChat($"Replacing encounter at landblock {obj.Location.Landblock:X4}, cellX={cellX}, cellY={cellY}\n{obj.WeenieClassId}({obj.Name}) for {weenieTarget.ClassId}({weenieTarget.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value})", ChatMessageType.Broadcast));

            SyncEncounters(session, landblockId, encounters);

            var landblock = LandblockManager.GetLandblock(new LandblockId((uint)(landblockId << 16)), false, false);
            DeveloperCommands.ReloadLandblock(landblock);
        }

        [CommandHandler("replaceEncArea", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 2, "Replaces encounters contained in a 3x3 area centered on the current landblock for another", "<wcid or classname of the old encounter> <wcid or classname of the new encounter>")]
        public static void HandleReplaceEncArea(Session session, params string[] parameters)
        {
            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Only the outdoors have encounters!", ChatMessageType.Broadcast));
                return;
            }

            var paramSource = parameters[0];
            var paramTarget = parameters[1];

            Weenie weenieSource = null;
            Weenie weenieTarget = null;

            uint wcid;
            if (uint.TryParse(paramSource, out wcid))
                weenieSource = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenieSource = DatabaseManager.World.GetWeenie(paramSource);  // classname

            if (uint.TryParse(paramTarget, out wcid))
                weenieTarget = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenieTarget = DatabaseManager.World.GetWeenie(paramTarget);  // classname

            if (weenieSource == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {paramSource}", ChatMessageType.Broadcast));
                return;
            }

            if (weenieTarget == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {paramTarget}", ChatMessageType.Broadcast));
                return;
            }

            uint startLandblockId = (ushort)pos.Landblock;

            int startX = (byte)(startLandblockId >> 8);
            int startY = (byte)startLandblockId;
            int endX = startX;
            int endY = startY;

            var radius = 1;
            var erodeCorners = false;

            startX -= radius;
            startY -= radius;
            endX += radius;
            endY += radius;

            startX = Math.Clamp(startX, 0, 255);
            startY = Math.Clamp(startY, 0, 255);
            endX = Math.Clamp(endX, 0, 255);
            endY = Math.Clamp(endY, 0, 255);

            var counter = 0;
            for (byte x = (byte)startX; x <= endX; x++)
            {
                for (byte y = (byte)startY; y <= endY; y++)
                {
                    if (erodeCorners && ((x == startX && y == startY) || (x == endX && y == endY) || (x == startX && y == endY) || (x == endX && y == startY)))
                        continue;

                    var landblockId = (ushort)(x << 8 | y);

                    // clear any cached encounters for this landblock so we get the unmodified entries
                    DatabaseManager.World.ClearCachedEncountersByLandblock(landblockId);

                    // get existing encounters for this landblock
                    var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);

                    foreach (var entry in encounters)
                    {
                        if (entry.WeenieClassId == weenieSource.ClassId)
                        {
                            entry.WeenieClassId = weenieTarget.ClassId;
                            entry.LastModified = DateTime.Now;
                            counter++;
                        }
                    }

                    if (encounters.Count > 0)
                    {
                        SyncEncounters(session, landblockId, encounters);
                        var landblock = LandblockManager.GetLandblock(new LandblockId(x, y), false, false);
                        DeveloperCommands.ReloadLandblock(landblock);
                    }
                }
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Replaced {counter} entries of encounter {weenieSource.ClassId}({weenieSource.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) for {weenieTarget.ClassId}({weenieTarget.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}) in a 3x3 landblocks area centered around 0x{startLandblockId:X4}", ChatMessageType.Broadcast));
        }

        public static EncounterSQLWriter LandblockEncounterWriter;

        [CommandHandler("addenc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Spawns a new wcid or classname in the current outdoor cell as an encounter", "<wcid or classname>")]
        public static void HandleAddEncounter(Session session, params string[] parameters)
        {
            var param = parameters[0];

            Weenie weenie = null;

            if (uint.TryParse(param, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenie = DatabaseManager.World.GetWeenie(param);  // classname

            if (weenie == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find weenie {param}", ChatMessageType.Broadcast));
                return;
            }

            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You must be outdoors to create an encounter!", ChatMessageType.Broadcast));
                return;
            }

            var cellX = (int)Math.Floor(pos.PositionX / Physics.Common.LandDefs.CellLength);
            var cellY = (int)Math.Floor(pos.PositionY / Physics.Common.LandDefs.CellLength);

            var landblock = (ushort)pos.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblock);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblock, out _);

            // check for existing encounter
            if (encounters.Any(i => i.CellX == cellX && i.CellY == cellY))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("This cell already contains an encounter!", ChatMessageType.Broadcast));
                return;
            }

            // spawn encounter
            var wo = SpawnEncounter(weenie, cellX, cellY, pos, session);

            if (wo == null) return;

            session.Network.EnqueueSend(new GameMessageSystemChat($"Creating new encounter @ landblock 0x{pos.Landblock:X4}, cellX={cellX}, cellY={cellY}\n{wo.WeenieClassId} - {wo.Name}", ChatMessageType.Broadcast));

            // add a new encounter (verifications?)
            var encounter = new Encounter();
            encounter.Landblock = (int)pos.Landblock;
            encounter.CellX = cellX;
            encounter.CellY = cellY;
            encounter.WeenieClassId = weenie.ClassId;
            encounter.LastModified = DateTime.Now;

            encounters.Add(encounter);

            // write encounters to sql file / load into db
            SyncEncounters(session, landblock, encounters);
            DeveloperCommands.HandleReloadLandblock(session);
        }

        public static WorldObject SpawnEncounter(Weenie weenie, int cellX, int cellY, Position pos, Session session)
        {
            var wo = WorldObjectFactory.CreateNewWorldObject(weenie.ClassId);

            if (wo == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to create encounter weenie", ChatMessageType.Broadcast));
                return null;
            }

            if (!wo.IsGenerator)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Encounter must be a Generator", ChatMessageType.Broadcast));
                return null;
            }

            var xPos = Math.Clamp((cellX * 24.0f) + 12.0f, 0.5f, 191.5f);
            var yPos = Math.Clamp((cellY * 24.0f) + 12.0f, 0.5f, 191.5f);

            var newPos = new Physics.Common.Position();
            newPos.ObjCellID = pos.Cell;
            newPos.Frame = new Physics.Animation.AFrame(new Vector3(xPos, yPos, 0), Quaternion.Identity);
            newPos.adjust_to_outside();

            newPos.Frame.Origin.Z = session.Player.CurrentLandblock.PhysicsLandblock.GetZ(newPos.Frame.Origin);

            wo.Location = new Position(newPos.ObjCellID, newPos.Frame.Origin, newPos.Frame.Orientation);

            var sortCell = Physics.Common.LScape.get_landcell(newPos.ObjCellID) as Physics.Common.SortCell;
            if (sortCell != null && sortCell.has_building())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to create encounter near building cell", ChatMessageType.Broadcast));
                return null;
            }

            if (PropertyManager.GetBool("override_encounter_spawn_rates").Item)
            {
                wo.RegenerationInterval = PropertyManager.GetDouble("encounter_regen_interval").Item;

                wo.ReinitializeHeartbeats();

                if (wo.Biota.PropertiesGenerator != null)
                {
                    // While this may be ugly, it's done for performance reasons.
                    // Common weenie properties are not cloned into the bota on creation. Instead, the biota references simply point to the weenie collections.
                    // The problem here is that we want to update one of those common collection properties. If the biota is referencing the weenie collection,
                    // then we'll end up updating the global weenie (from the cache), instead of just this specific biota.
                    if (wo.Biota.PropertiesGenerator == wo.Weenie.PropertiesGenerator)
                    {
                        wo.Biota.PropertiesGenerator = new List<ACE.Entity.Models.PropertiesGenerator>(wo.Weenie.PropertiesGenerator.Count);

                        foreach (var record in wo.Weenie.PropertiesGenerator)
                            wo.Biota.PropertiesGenerator.Add(record.Clone());
                    }
                }
            }

            var success = wo.EnterWorld();

            if (!success)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to spawn encounter", ChatMessageType.Broadcast));
                return null;
            }
            return wo;
        }

        /// <summary>
        /// Serializes encounters to XXYY.sql file,
        /// import into database, and clears the cached encounters
        /// </summary>
        public static void SyncEncounters(Session session, ushort landblock, List<Encounter> encounters)
        {
            // serialize to .sql file
            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}sql{sep}encounters{sep}");

            if (!folder.Exists)
                folder.Create();

            var sqlFilename = $"{folder.FullName}{sep}{landblock:X4}.sql";

            if (encounters.Count > 0)
            {
                var fileWriter = new StreamWriter(sqlFilename);

                if (LandblockEncounterWriter == null)
                {
                    LandblockEncounterWriter = new EncounterSQLWriter();
                    LandblockEncounterWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    LandblockEncounterWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    LandblockEncounterWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    LandblockEncounterWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                }

                LandblockEncounterWriter.CreateSQLDELETEStatement(encounters, fileWriter);

                fileWriter.WriteLine();

                LandblockEncounterWriter.CreateSQLINSERTStatement(encounters, fileWriter);

                fileWriter.Close();

                // import into db
                ImportSQL(sqlFilename);
            }
            else
            {
                // handle special case: deleting the last encounter from landblock
                File.Delete(sqlFilename);

                using (var ctx = new WorldDbContext())
                    ctx.Database.ExecuteSqlInterpolated($"DELETE FROM encounter WHERE landblock={landblock};");
            }

            // clear the encounters for this landblock (again)
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblock);
        }

        [CommandHandler("removeenc", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, "Removes the last appraised object from the encounters table")]
        public static void HandleRemoveEnc(Session session, params string[] parameters)
        {
            var obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target!", ChatMessageType.Broadcast));
                return;
            }

            // find root generator
            while (obj.Generator != null)
                obj = obj.Generator;

            if (!(obj.GetProperty(PropertyBool.IsEncounterGenerator) ?? false))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid target!", ChatMessageType.Broadcast));
                return;
            }

            var cellX = (int)Math.Floor(obj.Location.PositionX / Physics.Common.LandDefs.CellLength);
            var cellY = (int)Math.Floor(obj.Location.PositionY / Physics.Common.LandDefs.CellLength);

            var landblock = (ushort)obj.Location.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblock);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblock, out _);

            // check for existing encounter
            var encounter = encounters.FirstOrDefault(i => i.CellX == cellX && i.CellY == cellY && i.WeenieClassId == obj.WeenieClassId);

            if (encounter == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find encounter for {obj.WeenieClassId} - {obj.Name}", ChatMessageType.Broadcast));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Removing encounter @ landblock {obj.Location.Landblock:X4}, cellX={cellX}, cellY={cellY}\n{obj.WeenieClassId} - {obj.Name}", ChatMessageType.Broadcast));

            encounters.Remove(encounter);

            SyncEncounters(session, landblock, encounters);

            // this is needed for any generators that don't have GeneratorDestructionType
            DestroyAll(obj);
        }

        [CommandHandler("clearlandblockencounters", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Removes all encounters from the current landblock.")]
        public static void HandleClearLandblockEncounters(Session session, params string[] parameters)
        {
            var pos = session.Player.Location;

            if ((pos.Cell & 0xFFFF) >= 0x100)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You must be outdoors to remove encounters!", ChatMessageType.Broadcast));
                return;
            }

            var landblock = (ushort)pos.Landblock;

            // clear any cached encounters for this landblock so we get the unmodified entries
            DatabaseManager.World.ClearCachedEncountersByLandblock(landblock);

            // get existing encounters for this landblock
            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblock, out _);

            if (encounters.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{pos.Landblock:X4} has no encounters!", ChatMessageType.Broadcast));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"Removed {encounters.Count} encounters @ landblock {pos.Landblock:X4}.", ChatMessageType.Broadcast));
            encounters.Clear();

            SyncEncounters(session, landblock, encounters);
            DeveloperCommands.HandleReloadLandblock(session);
        }

        /// <summary>
        /// Destroys a parent generator, and all of its child objects
        /// </summary>
        private static void DestroyAll(WorldObject wo)
        {
            wo.Destroy();

            if (wo.GeneratorProfiles == null)
                return;

            foreach (var profile in wo.GeneratorProfiles)
            {
                foreach (var kvp in profile.Spawned)
                {
                    var child = kvp.Value.TryGetWorldObject();

                    if (child != null)
                        DestroyAll(child);
                }
            }
        }

        [CommandHandler("export-json-folders", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Exports content from database to JSON file in a WeenieType/ItemType folder structure", "<wcid>")]
        public static void HandleExportJsonFolder(Session session, params string[] parameters)
        {
            var param = parameters[0];
            ExportJsonWeenie(session, param, true);
        }

        [CommandHandler("export-json", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Exports content from database to JSON file", "<optional type> <id>\n<optional type> - landblock, quest, recipe, spell, weenie (default if not specified)\n<id> - wcid or content id to export")]
        public static void HandleExportJson(Session session, params string[] parameters)
        {
            var param = parameters[0];
            var contentType = FileType.Weenie;

            if (parameters.Length > 1)
            {
                contentType = GetContentType(parameters, ref param);

                if (contentType == FileType.Undefined)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type '{parameters[1]}'");
                    return;
                }
            }
            switch (contentType)
            {
                case FileType.LandblockInstance:
                    ExportJsonLandblock(session, param);
                    break;

                case FileType.Quest:
                    ExportJsonQuest(session, param);
                    break;

                case FileType.Recipe:
                    ExportJsonRecipe(session, param);
                    break;

                case FileType.Weenie:
                    ExportJsonWeenie(session, param);
                    break;
            }
        }

        public static void ExportJsonWeenie(Session session, string param, bool withFolders = false)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            Weenie weenie = null;

            if (uint.TryParse(param, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);
            else
                weenie = DatabaseManager.World.GetWeenie(param);

            if (weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find weenie {param}");
                return;
            }

            if (!LifestonedConverter.TryConvertACEWeenieToLSDJSON(weenie, out var json, out var json_weenie))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert {weenie.ClassId} - {weenie.ClassName} to json");
                return;
            }

            string json_folder = null;
            if (withFolders)
            {
                var weenieType = (WeenieType)weenie.Type;
                switch (weenieType)
                {
                    case WeenieType.Creature: // Export to the "CreatureType" folder
                        WeeniePropertiesInt cType = (from x in weenie.WeeniePropertiesInt where x.Type == 2 select x).FirstOrDefault();
                        if (cType == null)
                            json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}{weenieType}{sep}";
                        else
                        {
                            CreatureType creatureType = (CreatureType)cType.Value;
                            json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}{weenieType}{sep}{creatureType}{sep}";
                        }
                        break;
                    default: // Otherwise goes to "ItemType" folder
                        WeeniePropertiesInt iType = (from x in weenie.WeeniePropertiesInt where x.Type == 1 select x).FirstOrDefault();
                        if (iType == null)
                            json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}{weenieType}{sep}";
                        else
                        {
                            ItemType itemType = (ItemType)iType.Value;
                            json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}{weenieType}{sep}{itemType}{sep}";
                        }
                        break;
                }
            }
            else
            {
                json_folder = $"{di.FullName}{sep}json{sep}weenies{sep}";
            }

            di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json_filename = $"{weenie.ClassId} - {weenie.WeeniePropertiesString.FirstOrDefault(i => i.Type == (int)PropertyString.Name)?.Value}.json";

            if (File.Exists(json_folder + json_filename) && LifestonedLoader.AppendMetadata(json_folder + json_filename, json_weenie))
            {
                json = JsonSerializer.Serialize(json_weenie, LifestonedConverter.SerializerSettings);
            }

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {json_folder}{json_filename}");
        }

        public static void ExportJsonRecipe(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!uint.TryParse(param, out var recipeId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid recipe id");
                return;
            }

            var cookbooks = DatabaseManager.World.GetCookbooksByRecipeId(recipeId);
            if (cookbooks == null || cookbooks.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find recipe id {recipeId}");
                return;
            }

            if (!GDLEConverter.TryConvert(cookbooks, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert recipe id {recipeId} to json");
                return;
            }

            var json_folder = $"{di.FullName}{sep}json{sep}recipes{sep}";

            di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            if (RecipeSQLWriter == null)
            {
                RecipeSQLWriter = new RecipeSQLWriter();
                RecipeSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
            }

            var desc = RecipeSQLWriter.GetDefaultFileName(cookbooks[0].Recipe, cookbooks, true);

            var json_filename = $"{recipeId.ToString("00000")} - {desc}.json";

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {json_folder}{json_filename}");
        }

        public static void ExportJsonLandblock(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!ushort.TryParse(Regex.Match(param, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid landblock");
                return;
            }

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
            if (instances == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock {landblockId:X4}");
                return;
            }

            if (GDLEConverter.WeenieNames == null)
                GDLEConverter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();

            if (GDLEConverter.WeenieClassNames == null)
                GDLEConverter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();

            if (!GDLEConverter.TryConvert(instances, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert landblock {landblockId:X4} to json");
                return;
            }

            var json_folder = $"{di.FullName}{sep}json{sep}landblocks{sep}";

            di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json_filename = $"{landblockId:X4}.json";

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {json_folder}{json_filename}");
        }

        public static void ExportJsonQuest(Session session, string questName)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            var quest = DatabaseManager.World.GetCachedQuest(questName);

            if (quest == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find quest {questName}");
                return;
            }

            if (!GDLEConverter.TryConvert(quest, out var result))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to convert quest {questName} to json");
                return;
            }

            var json_folder = $"{di.FullName}{sep}json{sep}quests{sep}";

            di = new DirectoryInfo(json_folder);

            if (!di.Exists)
                di.Create();

            var json_filename = $"{questName}.json";

            var json = JsonSerializer.Serialize(result, LifestonedConverter.SerializerSettings);

            File.WriteAllText(json_folder + json_filename, json);

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {json_folder}{json_filename}");
        }

        [CommandHandler("export-sql-folders", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Exports weenie content from database to an SQL file in a WeenieType/ItemType folder structure", "<weenieType>")]
        public static void HandleExportSqlFolder(Session session, params string[] parameters)
        {
            var param = parameters[0];

            WeenieType weenieType = WeenieType.Undef;
            if (param == "all" || Enum.TryParse(param, true, out weenieType))
            {
                var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                if (param == "all")
                {
                    foreach (var entry in WeenieTypes)
                    {
                        ExportSQLWeenie(session, entry.Key.ToString(), true);
                    }
                }
                else if(weenieType != WeenieType.Undef)
                {
                    foreach (var entry in WeenieTypes)
                    {
                        if(entry.Value == (int)weenieType)
                            ExportSQLWeenie(session, entry.Key.ToString(), true);
                    }
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, $"Invalid parameter: '{parameters[0]}'");
            }
            else
                ExportSQLWeenie(session, param, true);
        }

        [CommandHandler("export-sql", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Exports content from database to SQL file", "<optional type> <id>\n<optional type> - landblock, encounter, quest, recipe, spell, weenie (default if not specified)\n<id> - wcid or content id to export")]
        public static void HandleExportSql(Session session, params string[] parameters)
        {
            var param = parameters[0];
            var contentType = FileType.Weenie;

            if (parameters.Length > 1)
            {
                contentType = GetContentType(parameters, ref param);

                if (contentType == FileType.Undefined)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unknown content type '{parameters[1]}'");
                    return;
                }
            }
            switch (contentType)
            {
                case FileType.LandblockInstance:
                    ExportSQLLandblock(session, param);
                    break;

                case FileType.Encounter:
                    ExportSQLEncounter(session, param);
                    break;

                case FileType.Quest:
                    ExportSQLQuest(session, param);
                    break;

                case FileType.Recipe:
                    ExportSQLRecipe(session, param);
                    break;

                case FileType.Spell:
                    ExportSQLSpell(session, param);
                    break;

                case FileType.Weenie:
                    ExportSQLWeenie(session, param);
                    break;
            }
        }

        [CommandHandler("export-sql-landblocks", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Exports all landblocks from database to SQL files", "")]
        public static void HandleExportSqlLandblocks(Session session, params string[] parameters)
        {
            for (ushort landblockId = 0x0000; landblockId < 0xFFFF; landblockId++)
            {
                var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
                if (instances != null && instances.Count > 0)
                    ExportSQLLandblock(session, landblockId.ToString("x4"));
                Thread.Sleep(5); // Avoid overwhelming the SQL server.
            }
        }

        [CommandHandler("export-sql-encounters", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Exports all encounters from database to SQL files", "")]
        public static void HandleExportSqlEncounters(Session session, params string[] parameters)
        {
            for (ushort landblockId = 0x0000; landblockId < 0xFFFF; landblockId++)
            {
                var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
                if (instances != null && instances.Count > 0)
                    ExportSQLEncounter(session, landblockId.ToString("x4"));
                Thread.Sleep(5); // Avoid overwhelming the SQL server.
            }
        }

        [CommandHandler("export-sql-landblocks-and-encounters", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Exports all landblocks and encounters from database to SQL files", "")]
        public static void HandleExportSqlLandblocksAndEncounters(Session session, params string[] parameters)
        {
            for (ushort landblockId = 0x0000; landblockId < 0xFFFF; landblockId++)
            {
                var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
                if (instances != null && instances.Count > 0)
                {
                    ExportSQLLandblock(session, landblockId.ToString("x4"));
                    ExportSQLEncounter(session, landblockId.ToString("x4"));
                }
                Thread.Sleep(5); // Avoid overwhelming the SQL server.
            }
        }

        [CommandHandler("export-sql-spells", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Exports all spells from database to SQL files", "")]
        public static void HandleExportSqlSpells(Session session, params string[] parameters)
        {
            var spells = DatabaseManager.World.GetAllSpellNames();

            foreach (var entry in spells)
            {
                ExportSQLSpell(session, entry.Key.ToString());
                Thread.Sleep(5); // Avoid overwhelming the SQL server.
            }
        }

        [CommandHandler("export-sql-recipes", AccessLevel.Developer, CommandHandlerFlag.None, 0, "Exports all recipes from database to SQL files", "")]
        public static void HandleExportSqlRecipes(Session session, params string[] parameters)
        {
            var cookBooks = DatabaseManager.World.GetAllCookbooks();
            var exported = new List<uint>();

            foreach (var entry in cookBooks)
            {
                if (!exported.Contains(entry.RecipeId))
                {
                    exported.Add(entry.RecipeId);
                    ExportSQLRecipe(session, entry.RecipeId.ToString());
                    Thread.Sleep(5); // Avoid overwhelming the SQL server.
                }
            }
        }

        public static void ExportSQLWeenie(Session session, string param, bool withFolders = false)
        {
            Weenie weenie = null;

            if (uint.TryParse(param, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);
            else
                weenie = DatabaseManager.World.GetWeenie(param);

            if (weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find weenie {param}");
                return;
            }

            ExportSQLWeenie(weenie, session, withFolders, !withFolders && (session == null || session.State != Network.Enum.SessionState.WorldConnected));
        }

        public static void ExportSQLWeenie(Weenie weenie, Session session = null, bool withFolders = false, bool openAfterExport = false)
        {
            if (weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Null weenie.");
                return;
            }

            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            string sql_folder = null;
            if (withFolders)
            {
                var weenieType = (WeenieType)weenie.Type;
                switch (weenieType)
                {
                    case WeenieType.Creature: // Export to the "CreatureType" folder
                        WeeniePropertiesInt cType = (from x in weenie.WeeniePropertiesInt where x.Type == 2 select x).FirstOrDefault();
                        if (cType == null)
                            sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}";
                        else
                        {
                            CreatureType creatureType = (CreatureType)cType.Value;
                            sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}{creatureType}{sep}";
                        }
                        break;
                    default: // Otherwise goes to "ItemType" folder
                        WeeniePropertiesInt iType = (from x in weenie.WeeniePropertiesInt where x.Type == 1 select x).FirstOrDefault();
                        if (iType == null)
                        {
                            WeeniePropertiesInt maxGeneratedObjects = (from x in weenie.WeeniePropertiesInt where x.Type == 81 select x).FirstOrDefault();
                            WeeniePropertiesFloat regenerationInterval = (from x in weenie.WeeniePropertiesFloat where x.Type == 41 select x).FirstOrDefault();
                            if (maxGeneratedObjects != null && regenerationInterval != null)
                            {
                                WeeniePropertiesInt generatorTimeType = (from x in weenie.WeeniePropertiesInt where x.Type == 142 select x).FirstOrDefault();
                                var generators = weenie.WeeniePropertiesGenerator;
                                if(generators == null || generators.Count == 0)
                                    sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}";
                                else if(generatorTimeType != null && (generatorTimeType.Value == (int)GeneratorTimeType.RealTime || generatorTimeType.Value == (int)GeneratorTimeType.Event))
                                    sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}EventGenerator{sep}";
                                else
                                {
                                    var whereCreate = RegenLocationType.Undef;
                                    var generatesCreature = false;
                                    var generatesItem = false;
                                    var isLinkableGenerator = false;
                                    var generatesNPC = false;
                                    var isPossibleEncounterGenerator = false;
                                    var hasGuaranteedSpawns = false;
                                    var tierList = new List<int>();

                                    if (regenerationInterval.Value >= 300)
                                        isPossibleEncounterGenerator = true;

                                    foreach (var entry in generators)
                                    {
                                        if (entry.Probability == -1)
                                            hasGuaranteedSpawns = true;

                                        whereCreate |= (RegenLocationType)entry.WhereCreate;
                                        var generatedWeenie = DatabaseManager.World.GetWeenie(entry.WeenieClassId);
                                        if (generatedWeenie != null)
                                        {
                                            if (generatedWeenie.ClassId == (uint)WeenieClassName.placeholder)
                                                isLinkableGenerator = true;
                                            else
                                            {
                                                switch ((WeenieType)generatedWeenie.Type)
                                                {
                                                    case WeenieType.Vendor:
                                                        generatesNPC = true;
                                                        break;
                                                    case WeenieType.Creature:
                                                    case WeenieType.Cow:
                                                    case WeenieType.Pet:
                                                        WeeniePropertiesInt radarBlipColor = (from x in generatedWeenie.WeeniePropertiesInt where x.Type == 95 select x).FirstOrDefault();
                                                        if (radarBlipColor != null && radarBlipColor.Value == (int)RadarColor.Yellow)
                                                            generatesNPC = true;
                                                        else
                                                            generatesCreature = true;
                                                        break;
                                                    default:
                                                        generatesItem = true;
                                                        break;
                                                }
                                            }
                                        }
                                    }

                                    if (isLinkableGenerator)
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}LinkableGenerator{sep}";
                                    else if (whereCreate == RegenLocationType.Treasure)
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}TreasureGenerator{sep}";
                                    else if (isPossibleEncounterGenerator && generatesCreature && !hasGuaranteedSpawns)
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}EncounterGenerator{sep}";
                                    else if (generatesNPC)
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}NPCGenerator{sep}";
                                    else if (generatesCreature)
                                    {
                                        if (maxGeneratedObjects.Value > 1)
                                            sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}CreatureCampGenerator{sep}";
                                        else
                                            sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}CreatureGenerator{sep}";
                                    }
                                    else if (generatesItem)
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}ItemGenerator{sep}";
                                    else
                                        sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Generator{sep}";
                                }
                            }
                            else
                                sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}";
                        }
                        else
                        {
                            ItemType itemType = (ItemType)iType.Value;
                            if (itemType == ItemType.Armor)
                            {
                                WeeniePropertiesInt combatUse = (from x in weenie.WeeniePropertiesInt where x.Type == 51 select x).FirstOrDefault();
                                if (combatUse != null && combatUse.Value == (int)CombatUse.Shield)
                                {
                                    sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}Shield{sep}";
                                    break;
                                }
                            }
                            sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}{weenieType}{sep}{itemType}{sep}";
                        }
                        break;
                }
            }
            else
            {
                sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}";
            }

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            if (WeenieSQLWriter == null)
            {
                WeenieSQLWriter = new WeenieSQLWriter();
                WeenieSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                WeenieSQLWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                WeenieSQLWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                WeenieSQLWriter.SpellNames = DatabaseManager.World.GetAllSpellNames();
                WeenieSQLWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                WeenieSQLWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                WeenieSQLWriter.PacketOpCodes = PacketOpCodeNames.Values;
            }

            var sql_filename = WeenieSQLWriter.GetDefaultFileName(weenie);

            var writer = new StreamWriter(sql_folder + sql_filename);

            try
            {
                WeenieSQLWriter.CreateSQLDELETEStatement(weenie, writer);
                writer.WriteLine();
                WeenieSQLWriter.CreateSQLINSERTStatement(weenie, writer);
                writer.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");

            if (openAfterExport)
                Process.Start(new ProcessStartInfo(sql_folder + sql_filename) { UseShellExecute = true });
        }

        public static void ExportSQLRecipe(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!uint.TryParse(param, out var recipeId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid recipe id");
                return;
            }

            var cookbooks = DatabaseManager.World.GetCookbooksByRecipeId(recipeId);
            if (cookbooks == null || cookbooks.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find recipe id {recipeId}");
                return;
            }

            var sql_folder = $"{di.FullName}{sep}sql{sep}recipes{sep}";

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            if (RecipeSQLWriter == null)
            {
                RecipeSQLWriter = new RecipeSQLWriter();
                RecipeSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
            }

            if (CookBookSQLWriter == null)
            {
                CookBookSQLWriter = new CookBookSQLWriter();
                CookBookSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
            }

            // same recipe for all cookbooks
            var recipe = cookbooks[0].Recipe;

            var sql_filename = RecipeSQLWriter.GetDefaultFileName(recipe, cookbooks);

            try
            {
                using (StreamWriter sqlFile = new StreamWriter(sql_folder + sql_filename))
                {
                    RecipeSQLWriter.CreateSQLDELETEStatement(recipe, sqlFile);
                    sqlFile.WriteLine();

                    RecipeSQLWriter.CreateSQLINSERTStatement(recipe, sqlFile);
                    sqlFile.WriteLine();

                    CookBookSQLWriter.CreateSQLDELETEStatement(cookbooks, sqlFile);
                    sqlFile.WriteLine();

                    CookBookSQLWriter.CreateSQLINSERTStatement(cookbooks, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        }

        public static void ExportSQLLandblock(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!ushort.TryParse(Regex.Match(param, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid landblock");
                return;
            }

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
            if (instances == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock {landblockId:X4}");
                return;
            }

            var sql_folder = $"{di.FullName}{sep}sql{sep}landblocks{sep}";

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            var sql_filename = LandblockInstanceWriter.GetDefaultFileName(landblockId);

            try
            {
                if (LandblockInstanceWriter == null)
                {
                    LandblockInstanceWriter = new LandblockInstanceWriter();
                    LandblockInstanceWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    LandblockInstanceWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    LandblockInstanceWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    LandblockInstanceWriter.WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
                    LandblockInstanceWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    LandblockInstanceWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                }

                using (StreamWriter sqlFile = new StreamWriter(sql_folder + sql_filename))
                {
                    // Check if the Landblock is empty
                    if(instances.Count > 0)
                        LandblockInstanceWriter.CreateSQLDELETEStatement(instances, sqlFile);
                    else
                    {
                        // We'll just create a dummy list with a fake instance in our landblock so we don't anger CreateSQLDeleteStatement()
                        CommandHandlerHelper.WriteOutputInfo(session, $"Landblock {landblockId:X4} is empty.");
                        List<LandblockInstance> dummyList = new List<LandblockInstance> ();
                        LandblockInstance dummyInstance = new LandblockInstance();
                        dummyInstance.ObjCellId = (uint)(landblockId << 16);
                        dummyList.Add(dummyInstance);
                        LandblockInstanceWriter.CreateSQLDELETEStatement(dummyList, sqlFile);
                    }
                    sqlFile.WriteLine();

                    LandblockInstanceWriter.CreateSQLINSERTStatement(instances, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        }

        public static void ExportSQLEncounter(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!ushort.TryParse(Regex.Match(param, @"[0-9A-F]{4}", RegexOptions.IgnoreCase).Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var landblockId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid landblock");
                return;
            }

            var encounters = DatabaseManager.World.GetCachedEncountersByLandblock(landblockId, out _);

            if (encounters == null || encounters.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find encounters for landblock {landblockId:X4}");
                return;
            }

            var sql_folder = $"{di.FullName}{sep}sql{sep}encounters{sep}";

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            var sql_filename = LandblockInstanceWriter.GetDefaultFileName(landblockId);

            try
            {
                if (LandblockEncounterWriter == null)
                {
                    LandblockEncounterWriter = new EncounterSQLWriter();
                    LandblockEncounterWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    LandblockEncounterWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    LandblockEncounterWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    LandblockEncounterWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                }

                using (var sqlFile = new StreamWriter(sql_folder + sql_filename))
                {
                    LandblockEncounterWriter.CreateSQLDELETEStatement(encounters, sqlFile);

                    sqlFile.WriteLine();

                    LandblockEncounterWriter.CreateSQLINSERTStatement(encounters, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        }

        public static void ExportSQLQuest(Session session, string questName)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            var quest = DatabaseManager.World.GetCachedQuest(questName);

            if (quest == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find quest {questName}");
                return;
            }

            var sql_folder = $"{di.FullName}{sep}sql{sep}quests{sep}";

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            if (QuestSQLWriter == null)
                QuestSQLWriter = new QuestSQLWriter();

            var sql_filename = questName + ".sql";

            try
            {
                using (StreamWriter sqlFile = new StreamWriter(sql_folder + sql_filename))
                {

                    QuestSQLWriter.CreateSQLDELETEStatement(quest, sqlFile);
                    sqlFile.WriteLine();

                    QuestSQLWriter.CreateSQLINSERTStatement(quest, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        }


        public static void ExportSQLSpell(Session session, string param)
        {
            DirectoryInfo di = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;

            if (!uint.TryParse(param, out var spellId))
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{param} not a valid spell id");
                return;
            }

            var spell = DatabaseManager.World.GetCachedSpell(spellId);

            if (spell == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find spell id {spellId}");
                return;
            }

            var sql_folder = $"{di.FullName}{sep}sql{sep}spells{sep}";

            di = new DirectoryInfo(sql_folder);

            if (!di.Exists)
                di.Create();

            if (SpellSQLWriter == null)
                SpellSQLWriter = new SpellSQLWriter();

            var sql_filename = SpellSQLWriter.GetDefaultFileName(spell);

            try
            {
                using (StreamWriter sqlFile = new StreamWriter(sql_folder + sql_filename))
                {
                    SpellSQLWriter.CreateSQLDELETEStatement(spell, sqlFile);
                    sqlFile.WriteLine();

                    SpellSQLWriter.CreateSQLINSERTStatement(spell, sqlFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        }

        [CommandHandler("clearcache", AccessLevel.Developer, CommandHandlerFlag.None, "Clears the various database caches. This enables live editing of the database information")]
        public static void HandleClearCache(Session session, params string[] parameters)
        {
            var mode = CacheType.All;
            if (parameters.Length > 0)
            {
                if (parameters[0].Contains("landblock", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.Landblock;
                if (parameters[0].Contains("recipe", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.Recipe;
                if (parameters[0].Contains("spell", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.Spell;
                if (parameters[0].Contains("weenie", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.Weenie;
                if (parameters[0].Contains("wield", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.WieldedTreasure;
                if (parameters[0].Contains("death", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.DeathTreasure;
                if (parameters[0].Contains("encounter", StringComparison.OrdinalIgnoreCase))
                    mode = CacheType.Encounter;
            }

            if (mode.HasFlag(CacheType.Landblock))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing landblock instance cache");
                DatabaseManager.World.ClearCachedLandblockInstances();
            }

            if (mode.HasFlag(CacheType.Recipe))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing recipe cache");
                DatabaseManager.World.ClearCookbookCache();
            }

            if (mode.HasFlag(CacheType.Spell))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing spell cache");
                DatabaseManager.World.ClearSpellCache();
                WorldObject.ClearSpellCache();
            }

            if (mode.HasFlag(CacheType.Weenie))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing weenie cache");
                DatabaseManager.World.ClearWeenieCache();
            }

            if (mode.HasFlag(CacheType.WieldedTreasure))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing wielded treasure cache");
                DatabaseManager.World.ClearWieldedTreasureCache();
            }

            if (mode.HasFlag(CacheType.DeathTreasure))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing death treasure cache");
                DatabaseManager.World.ClearDeathTreasureCache();
            }

            if (mode.HasFlag(CacheType.Encounter))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Clearing encounter cache");
                DatabaseManager.World.ClearEncountersCache();
            }
        }

        [Flags]
        public enum CacheType
        {
            None            = 0x0,
            Landblock       = 0x1,
            Recipe          = 0x2,
            Spell           = 0x4,
            Weenie          = 0x8,
            WieldedTreasure = 0x10,
            DeathTreasure   = 0x01,
            Encounter       = 0x02,
            All             = 0xFFFF
        };

        public static FileType GetFileType(string filename)
        {
            if (filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return GetJsonFileType(filename);
            }
            else if (filename.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                return GetSQLFileType(filename);
            }
            return FileType.Undefined;
        }

        public static FileType GetJsonFileType(string filename)
        {
            if (!File.Exists(filename))
                return FileType.Undefined;

            // can possibly be indented format
            var json = File.ReadAllText(filename);

            if (json.Contains("\"wcid\":"))
                return FileType.Weenie;
            else if (json.Contains("\"recipe\":"))
                return FileType.Recipe;
            else
                return FileType.Undefined;
        }

        public static FileType GetSQLFileType(string filename)
        {
            if (!File.Exists(filename))
                return FileType.Undefined;

            using (var streamReader = new StreamReader(filename))
            {
                var line = streamReader.ReadLine();

                while (line != null)
                {
                    if (line.Trim().Length == 0)
                        continue;

                    if (line.Contains("`encounter`"))
                        return FileType.Encounter;
                    else if (line.Contains("`landblock_instance`"))
                        return FileType.LandblockInstance;
                    else if (line.Contains("`quest`"))
                        return FileType.Quest;
                    else if (line.Contains("`recipe`"))
                        return FileType.Recipe;
                    else if (line.Contains("`spell`"))
                        return FileType.Spell;
                    else if (line.Contains("`weenie`"))
                        return FileType.Weenie;
                    else
                        break;
                }
                return FileType.Undefined;
            }
        }

        [CommandHandler("nudge", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Adjusts the spawn position of a landblock instance", "<dir> <amount>\nDirections: x, y, z, north, south, west, east, northwest, northeast, southwest, southeast, n, s, w, e, nw, ne, sw, se, up, down, here")]
        public static void HandleNudge(Session session, params string[] parameters)
        {
            WorldObject obj = null;

            var curParam = 0;

            if (parameters.Length == 3)
            {
                if (!uint.TryParse(parameters[curParam++].TrimStart("0x"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var guid))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid guid: {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }

                obj = session.Player.FindObject(guid, Player.SearchLocations.Landblock);

                if (obj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
                obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null) return;

            // ensure landblock instance
            uint objGuid = obj.Guid.Full;
            if (!obj.Guid.IsStatic())
            {
                uint? staticGuid = null;
                if (obj.Generator != null)
                {
                    // if generator child, try getting the "real" guid
                    staticGuid = obj.Generator.GetStaticGuid(objGuid);
                    if (staticGuid != null)
                        objGuid = staticGuid.Value;
                }

                if(staticGuid == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (obj.PhysicsObj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not a physics object", ChatMessageType.Broadcast));
                return;
            }

            // get direction
            var dirname = parameters[curParam++].ToLower();
            var dir = GetNudgeDir(dirname);

            bool curPos = false;

            if (dir == null)
            {
                if (dirname.Equals("here") || dirname.Equals("to me"))
                {
                    dir = Vector3.Zero;
                    curPos = true;
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid direction: {dirname}", ChatMessageType.Broadcast));
                    return;
                }
            }

            // get distance / amount
            var amount = 1.0f;
            if (curParam < parameters.Length)
            {
                if (!float.TryParse(parameters[curParam++], out amount))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid amount: {amount}", ChatMessageType.Broadcast));
                    return;
                }
            }

            var nudge = dir * amount;

            // get landblock for static guid
            var landblock_id = (ushort)(objGuid >> 12);

            // get instances for landblock
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock_id);

            // find instance
            var instance = instances.FirstOrDefault(i => i.Guid == objGuid);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find instance for {obj.Name} ({obj.Guid})", ChatMessageType.Broadcast));
                return;
            }

            if (curPos)
            {
                // ensure same landblock
                if ((instance.ObjCellId >> 16) != (session.Player.Location.Cell >> 16))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to move {obj.Name} ({obj.Guid}) to current location -- different landblock", ChatMessageType.Broadcast));
                    return;
                }

                obj.Ethereal = true;
                obj.EnqueueBroadcastPhysicsState();

                var newLoc = new Position(session.Player.Location);

                // slide?
                var setPos = new Physics.Common.SetPosition(newLoc.PhysPosition(), Physics.Common.SetPositionFlags.Teleport /* | Physics.Common.SetPositionFlags.Slide */);
                var result = obj.PhysicsObj.SetPosition(setPos);

                if (result != Physics.Common.SetPositionError.OK)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to move {obj.Name} ({obj.Guid}) to current location: {result}", ChatMessageType.Broadcast));
                    return;
                }

                instance.AnglesX = obj.Location.RotationX;
                instance.AnglesY = obj.Location.RotationY;
                instance.AnglesZ = obj.Location.RotationZ;
                instance.AnglesW = obj.Location.RotationW;
            }
            else
            {
                // compare current position with home position
                // the nudge should be performed as an offset from home position
                if (instance.OriginX != obj.Location.PositionX || instance.OriginY != obj.Location.PositionY || instance.OriginZ != obj.Location.PositionZ)
                {
                    //session.Network.EnqueueSend(new GameMessageSystemChat($"Moving {obj.Name} ({obj.Guid}) to home position: {obj.Location} to {instance.ObjCellId:X8} [{instance.OriginX} {instance.OriginY} {instance.OriginZ}]", ChatMessageType.Broadcast));

                    var homePos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);

                    // slide?
                    var setPos = new Physics.Common.SetPosition(homePos.PhysPosition(), Physics.Common.SetPositionFlags.Teleport /* | Physics.Common.SetPositionFlags.Slide*/);
                    var result = obj.PhysicsObj.SetPosition(setPos);

                    if (result != Physics.Common.SetPositionError.OK)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to move {obj.Name} ({obj.Guid}) to home position {homePos.ToLOCString()}", ChatMessageType.Broadcast));
                        return;
                    }
                }

                // perform physics transition
                var newPos = new Physics.Common.Position(obj.PhysicsObj.Position);
                newPos.add_offset(nudge.Value);

                var transit = obj.PhysicsObj.transition(obj.PhysicsObj.Position, newPos, true);

                var errorMsg = $"{obj.Name} ({obj.Guid}) failed to move from {obj.PhysicsObj.Position.ACEPosition()} to {newPos.ACEPosition()}";

                if (transit == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(errorMsg, ChatMessageType.Broadcast));
                    return;
                }

                // ensure same landblock
                if ((transit.SpherePath.CurPos.ObjCellID >> 16) != (obj.PhysicsObj.Position.ObjCellID >> 16))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{errorMsg} - cannot change landblock", ChatMessageType.Broadcast));
                    return;
                }

                obj.PhysicsObj.SetPositionInternal(transit);
            }

            // update ace location
            var prevLoc = new Position(obj.Location);
            obj.Location = obj.PhysicsObj.Position.ACEPosition();

            if (prevLoc.Landblock != obj.Location.Landblock)
                LandblockManager.RelocateObjectForPhysics(obj, true);

            // broadcast new position
            obj.SendUpdatePosition(true);

            session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) - moved from {prevLoc} to {obj.Location}", ChatMessageType.Broadcast));

            // update sql
            instance.ObjCellId = obj.Location.Cell;
            instance.OriginX = obj.Location.PositionX;
            instance.OriginY = obj.Location.PositionY;
            instance.OriginZ = obj.Location.PositionZ;

            SyncInstances(session, landblock_id, instances);
        }

        public static Vector3? GetNudgeDir(string dir)
        {
            if (dir.Equals("north") || dir.Equals("n") || dir.Equals("y"))
                return Vector3.UnitY;
            else if (dir.Equals("south") || dir.Equals("s"))
                return -Vector3.UnitY;
            else if (dir.Equals("west") || dir.Equals("w"))
                return -Vector3.UnitX;
            else if (dir.Equals("east") || dir.Equals("e") || dir.Equals("x"))
                return Vector3.UnitX;
            else if (dir.Equals("northwest") || dir.Equals("nw"))
                return Vector3.Normalize(new Vector3(-1, 1, 0));
            else if (dir.Equals("northeast") || dir.Equals("ne"))
                return Vector3.Normalize(new Vector3(1, 1, 0));
            else if (dir.Equals("southwest") || dir.Equals("sw"))
                return Vector3.Normalize(new Vector3(-1, -1, 0));
            else if (dir.Equals("southeast") || dir.Equals("se"))
                return Vector3.Normalize(new Vector3(1, -1, 0));
            else if (dir.Equals("up") || dir.Equals("z"))
                return Vector3.UnitZ;
            else if (dir.Equals("down"))
                return -Vector3.UnitZ;
            else
                return null;
        }

        [CommandHandler("rotate", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Adjusts the rotation of a landblock instance", "<dir>\nDirections: north, south, west, east, northwest, northeast, southwest, southeast, n, s, w, e, nw, ne, sw, se, -or-\n0-360, with 0 being north, and 90 being west")]
        public static void HandleRotate(Session session, params string[] parameters)
        {
            WorldObject obj = null;

            var curParam = 0;

            if (parameters.Length == 2)
            {
                if (!uint.TryParse(parameters[curParam++].TrimStart("0x"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var guid))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid guid: {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }

                obj = session.Player.FindObject(guid, Player.SearchLocations.Landblock);

                if (obj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
                obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null) return;

            // ensure landblock instance
            uint objGuid = obj.Guid.Full;
            if (!obj.Guid.IsStatic())
            {
                uint? staticGuid = null;
                if (obj.Generator != null)
                {
                    // if generator child, try getting the "real" guid
                    staticGuid = obj.Generator.GetStaticGuid(objGuid);
                    if (staticGuid != null)
                        objGuid = staticGuid.Value;
                }

                if (staticGuid == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (obj.PhysicsObj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not a physics object", ChatMessageType.Broadcast));
                return;
            }

            // get direction
            var dirname = parameters[curParam++].ToLower();
            var dir = GetNudgeDir(dirname);

            bool curRotate = false;

            if (dir == null)
            {
                if (float.TryParse(dirname, out var degrees))
                {
                    var rads = degrees.ToRadians();
                    var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rads);
                    dir = Vector3.Transform(Vector3.UnitY, q);
                }
                else if (dirname.Equals("here") || dirname.Equals("me"))
                {
                    dir = Vector3.Zero;
                    curRotate = true;
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid direction: {dirname}", ChatMessageType.Broadcast));
                    return;
                }
            }

            // get quaternion
            var newRotation = Quaternion.Identity;

            if (curRotate)
            {
                newRotation = session.Player.Location.Rotation;
            }
            else
            {
                var angle = Math.Atan2(-dir.Value.X, dir.Value.Y);
                newRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)angle);
            }

            newRotation = Quaternion.Normalize(newRotation);

            // get landblock for static guid
            var landblock_id = (ushort)(obj.Guid.Full >> 12);

            // get instances for landblock
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock_id);

            // find instance
            var instance = instances.FirstOrDefault(i => i.Guid == obj.Guid.Full);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find instance for {obj.Name} ({obj.Guid})", ChatMessageType.Broadcast));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) new rotation: {newRotation}", ChatMessageType.Broadcast));

            // update physics / ace rotation
            obj.PhysicsObj.Position.Frame.Orientation = newRotation;
            obj.Location.Rotation = newRotation;

            // update instance
            instance.AnglesW = newRotation.W;
            instance.AnglesX = newRotation.X;
            instance.AnglesY = newRotation.Y;
            instance.AnglesZ = newRotation.Z;

            SyncInstances(session, landblock_id, instances);

            // broadcast new rotation
            obj.SendUpdatePosition(true);
        }

        [CommandHandler("rotate-x", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Adjusts the rotation of a landblock instance along the x-axis", "<degrees>")]
        public static void HandleRotateX(Session session, params string[] parameters)
        {
            HandleRotateAxis(session, Vector3.UnitX, parameters);
        }

        [CommandHandler("rotate-y", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Adjusts the rotation of a landblock instance along the y-axis", "<degrees>")]
        public static void HandleRotateY(Session session, params string[] parameters)
        {
            HandleRotateAxis(session, Vector3.UnitY, parameters);
        }

        [CommandHandler("rotate-z", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Adjusts the rotation of a landblock instance along the z-axis", "<degrees>")]
        public static void HandleRotateZ(Session session, params string[] parameters)
        {
            HandleRotateAxis(session, Vector3.UnitZ, parameters);
        }

        public static void HandleRotateAxis(Session session, Vector3 axis, params string[] parameters)
        {
            WorldObject obj = null;

            var curParam = 0;

            if (parameters.Length == 2)
            {
                if (!uint.TryParse(parameters[curParam++].TrimStart("0x"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var guid))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid guid: {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }

                obj = session.Player.FindObject(guid, Player.SearchLocations.Landblock);

                if (obj == null)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find {parameters[0]}", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
                obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            if (obj == null) return;

            // ensure landblock instance
            if (!obj.Guid.IsStatic())
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not landblock instance", ChatMessageType.Broadcast));
                return;
            }

            if (obj.PhysicsObj == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) is not a physics object", ChatMessageType.Broadcast));
                return;
            }

            var degrees_str = parameters[curParam];

            if (!float.TryParse(degrees_str, out var degrees))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid angle: {degrees_str}", ChatMessageType.Broadcast));
                return;
            }

            var rads = degrees.ToRadians();
            var q = Quaternion.CreateFromAxisAngle(axis, rads);

            // get quaternion
            var newRotation = Quaternion.Normalize(obj.PhysicsObj.Position.Frame.Orientation * q);

            // get landblock for static guid
            var landblock_id = (ushort)(obj.Guid.Full >> 12);

            // get instances for landblock
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock_id);

            // find instance
            var instance = instances.FirstOrDefault(i => i.Guid == obj.Guid.Full);

            if (instance == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find instance for {obj.Name} ({obj.Guid})", ChatMessageType.Broadcast));
                return;
            }

            session.Network.EnqueueSend(new GameMessageSystemChat($"{obj.Name} ({obj.Guid}) new rotation: {newRotation}", ChatMessageType.Broadcast));

            // update physics / ace rotation
            obj.PhysicsObj.Position.Frame.Orientation = newRotation;
            obj.Location.Rotation = newRotation;

            // update instance
            instance.AnglesW = newRotation.W;
            instance.AnglesX = newRotation.X;
            instance.AnglesY = newRotation.Y;
            instance.AnglesZ = newRotation.Z;

            SyncInstances(session, landblock_id, instances);

            // broadcast new rotation
            obj.SendUpdatePosition(true);
        }

        [CommandHandler("generate-classnames", AccessLevel.Developer, CommandHandlerFlag.None, "Generates WeenieClassName.cs from current world database")]
        public static void HandleGenerateClassNames(Session session, params string[] parameters)
        {
            var lines = new List<string>();

            var replaceChars = new Dictionary<string, string>()
            {
                { " ", "_" },
                { "-", "_" },
                { "!", "" },
                { "#", "" },
                { "?", "" },
            };

            using (var ctx = new WorldDbContext())
            {
                var weenies = ctx.Weenie.OrderBy(i => i.ClassId);

                lines.Add("namespace ACE.Server.Factories.Enum");
                lines.Add("{");
                lines.Add("    public enum WeenieClassName");
                lines.Add("    {");
                lines.Add("        undef = 0,");

                foreach (var weenie in weenies)
                {
                    var className = weenie.ClassName;

                    foreach (var kvp in replaceChars)
                        className = className.Replace(kvp.Key, kvp.Value);

                    if (className[0] >= '0' && className[0] <= '9')
                        className = $"_{className}";

                    lines.Add($"        {className} = {weenie.ClassId},");
                }

                lines.Add("    }");
                lines.Add("}");
            }

            var filename = "WeenieClassName.cs";
            var sep = Path.DirectorySeparatorChar;
            var path = $"..{sep}..{sep}..{sep}..{sep}Factories{sep}Enum{sep}{filename}";
            if (!File.Exists(path))
                path = filename;
            File.WriteAllLines(path, lines);

            CommandHandlerHelper.WriteOutputInfo(session, $"Wrote {path}");
        }

        [CommandHandler("vloc2loc", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Output a set of LOCs for a given landblock found in the VLOCS dataset", "<LandblockID>\nExample: @vloc2loc 0x0007\n         @vloc2loc 0xCE95")]
        public static void HandleVLOCtoLOC(Session session, params string[] parameters)
        {
            var hex = parameters[0];

            if (hex.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
             || hex.StartsWith("&H", StringComparison.CurrentCultureIgnoreCase))
            {
                hex = hex[2..];
            }

            if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var lbid))
            {
                DirectoryInfo di = VerifyContentFolder(session);
                if (!di.Exists) return;

                var sep = Path.DirectorySeparatorChar;

                var vloc_folder = $"{di.FullName}{sep}vlocs{sep}";

                di = new DirectoryInfo(vloc_folder);

                var vlocDB = vloc_folder + "vlocDB.txt";

                var vlocs = di.Exists ? new FileInfo(vlocDB).Exists ? File.ReadLines(vlocDB).ToArray() : null : null;

                if (vlocs == null)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unable to read VLOC database file located here: {vlocDB}");
                    return;
                }

                // Name,ObjectClass,LandCell,X,Y
                // Master MacTavish,37,-114359889,97.14075000286103,-63.93749958674113

                if (vlocs.Length == 0 || !vlocs[0].Equals("Name,ObjectClass,LandCell,X,Y"))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"{vlocDB} does not appear to be a valid VLOC database file.");
                    return;
                }

                var vlocFile = vloc_folder + $"{lbid:X4}.txt";

                var vi = new FileInfo(vlocFile);
                if (vi.Exists)
                    vi.Delete();

                for (var i = 1; i < vlocs.Length; i++)
                {
                    var split = vlocs[i].Split(",");

                    var name = split[0].Trim();
                    var objectClass = split[1].Trim();
                    var strLandCell = split[2].Trim();
                    var strX = split[3].Trim();
                    var strY = split[4].Trim();

                    if (!int.TryParse(strLandCell, out var landCell))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Unable to parse LandCell ({strLandCell}) value from line {i} in vlocDB: {vlocs[i]}");
                        continue;
                    }
                    var objCellId = (uint)landCell;
                    if (!float.TryParse(strX, out var x))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Unable to parse X ({strX}) value from line {i} in vlocDB: {vlocs[i]}");
                        continue;
                    }
                    if (!float.TryParse(strY, out var y))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Unable to parse Y ({strY}) value from line {i} in vlocDB: {vlocs[i]}");
                        continue;
                    }

                    if ((objCellId >> 16) != lbid) continue;

                    try
                    {
                        var pos = new Position(new Vector2(x, y));
                        pos.AdjustMapCoords();
                        pos.TranslateLandblockId(objCellId);
                        pos.FindZ();

                        using (StreamWriter sw = File.AppendText(vlocFile))
                        {
                            sw.WriteLine($"{name} - @teleloc {pos.ToLOCString()}");
                        }
                    }
                    catch (Exception)
                    {
                        using (StreamWriter sw = File.AppendText(vlocFile))
                        {
                            sw.WriteLine($"Unable to parse {name} - 0x{objCellId:X8} {strX}, {strY}");
                        }
                    }
                }

                vi = new FileInfo(vlocFile);
                if (vi.Exists)
                    CommandHandlerHelper.WriteOutputInfo(session, $"Successfully wrote VLOCs for 0x{lbid:X4} to {vlocFile}");
                else
                    CommandHandlerHelper.WriteOutputInfo(session, $"No VLOCs able to be written for 0x{lbid:X4}");
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Invalid Landblock ID: {parameters[0]}\nLandblock ID should be in the hex format such as this: @vloc2loc 0xAB94");
            }
        }

        [CommandHandler("scaleweeniestats", AccessLevel.Developer, CommandHandlerFlag.None, 2, "Scales the level and all attributes/skills of the a weenieId/WeenieClassName", "<weenieId/WeenieClassName> <newLevel> [keepLevel = false]")]
        public static void HandleScaleWeenieStats(Session session, params string[] parameters)
        {
            var weenie = AdminCommands.GetWeenieForCreate(session, parameters[0]);
            if(weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid weenie.");
                return;
            }

            var obj = WorldObjectFactory.CreateNewWorldObject(weenie);

            Creature creature = obj as Creature;

            if (creature == null)
            {
                if(obj != null)
                    obj.Destroy();
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid weenie.");
                return;
            }

            uint newLevel;
            if (parameters.Length < 2 || !uint.TryParse(parameters[1], out newLevel))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid parameters.");
                return;
            }

            bool keepLevel = false;
            if (parameters.Length > 2)
                bool.TryParse(parameters[2], out keepLevel);

            uint oldLevel = (uint)(creature.Level ?? 1);
            if (newLevel == oldLevel)
            {
                if (obj != null)
                    obj.Destroy();
                CommandHandlerHelper.WriteOutputInfo(session, "Can't scale a creature to the same level it already is.");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Scaling {creature.Name}({creature.WeenieClassId}) stats from level {oldLevel} to {newLevel}" + (keepLevel ? $" but keeping its level at {oldLevel}." : "."));
            ScaleStats(creature, newLevel, true, keepLevel, session);

            creature.Destroy();
        }

        [CommandHandler("scalecreaturestats", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Scales the level and all attributes/skills of the last appraised creature", "<newLevel> [keepLevel = false]")]
        public static void HandleScaleCreatureStats(Session session, params string[] parameters)
        {
            var obj = CommandHandlerHelper.GetLastAppraisedObject(session);

            Creature creature = obj as Creature;

            if (creature == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid target.");
                return;
            }

            uint newLevel;
            if (parameters.Length < 1 || !uint.TryParse(parameters[0], out newLevel))
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid parameters.");
                return;
            }

            bool keepLevel = false;
            if (parameters.Length > 1)
                bool.TryParse(parameters[1], out keepLevel);

            uint oldLevel = (uint)(creature.Level ?? 1);
            if (newLevel == oldLevel)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Can't scale a creature to the same level it already is.");
                return;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Scaling {creature.Name}({creature.WeenieClassId}) stats from level {oldLevel} to {newLevel}" + (keepLevel ? $" but keeping its level at {oldLevel}." : "."));
            ScaleStats(creature, newLevel, false, keepLevel, session);
        }

        public static void ScaleStats(Creature creature, uint newLevel, bool toSql, bool keepLevel, Session session)
        {
            if (creature == null)
                return;

            if (creature.Level == newLevel)
                return;

            var xpTable = DatLoader.DatManager.PortalDat.XpTable.CharacterLevelXPList;
            var attributeTable = DatLoader.DatManager.PortalDat.XpTable.AttributeXpList;
            var vitalTable = DatLoader.DatManager.PortalDat.XpTable.VitalXpList;
            var skillTable = DatLoader.DatManager.PortalDat.XpTable.SpecializedSkillXpList;

            ulong newLevelTotalXp = 0;
            ulong totalXp = 0;
            uint highestCombatSkillValue = 0;
            ulong highestCombatSkillTotalXp = 0;

            uint health = creature.Health.StartingValue;
            uint stamina = creature.Stamina.StartingValue;
            uint mana = creature.Mana.StartingValue;

            uint strength = creature.Strength.StartingValue;
            uint endurance = creature.Endurance.StartingValue;
            uint coordination = creature.Coordination.StartingValue;
            uint quickness = creature.Quickness.StartingValue;
            uint focus = creature.Focus.StartingValue;
            uint self = creature.Self.StartingValue;

            ulong healthTotalXp = GetTotalXP(vitalTable, health);
            ulong staminaTotalXp = GetTotalXP(vitalTable, stamina);
            ulong manaTotalXp = GetTotalXP(vitalTable, mana);
            totalXp += healthTotalXp;
            totalXp += staminaTotalXp;
            totalXp += manaTotalXp;

            ulong strengthTotalXp = GetTotalXP(attributeTable, strength);
            ulong enduranceTotalXp = GetTotalXP(attributeTable, endurance);
            ulong coordinationTotalXp = GetTotalXP(attributeTable, coordination);
            ulong quicknessTotalXp = GetTotalXP(attributeTable, quickness);
            ulong focusTotalXp = GetTotalXP(attributeTable, focus);
            ulong selfTotalXp = GetTotalXP(attributeTable, self);

            totalXp += strengthTotalXp;
            totalXp += enduranceTotalXp;
            totalXp += coordinationTotalXp;
            totalXp += quicknessTotalXp;
            totalXp += focusTotalXp;
            totalXp += selfTotalXp;

            Dictionary<Skill, ulong> skillTotalXpMap = new Dictionary<Skill, ulong>();

            ulong skillTotalXp = 0;
            foreach (var skillEntry in creature.Skills)
            {
                var skill = skillEntry.Value;
                switch (skill.Skill)
                {
                    case Skill.Axe:
                    case Skill.Dagger:
                    case Skill.Mace:
                    case Skill.Spear:
                    case Skill.Staff:
                    case Skill.Sword:
                    case Skill.UnarmedCombat:
                    case Skill.Bow:
                    case Skill.Crossbow:
                    case Skill.ThrownWeapon:
                        var skillValue = skill.InitLevel;
                        skillTotalXp = GetTotalXP(skillTable, skillValue);
                        skillTotalXpMap.Add(skill.Skill, skillTotalXp);
                        //Do not add this to totalXp, we will only add the highest one below.

                        if (skillValue > highestCombatSkillValue)
                        {
                            highestCombatSkillValue = skillValue;
                            highestCombatSkillTotalXp = skillTotalXp;
                        }
                        break;
                    default:
                        skillTotalXp = GetTotalXP(skillTable, skill.InitLevel);
                        totalXp += skillTotalXp;
                        skillTotalXpMap.Add(skill.Skill, skillTotalXp);
                        break;
                }
            }

            if (highestCombatSkillValue > 0)
                totalXp += highestCombatSkillTotalXp;

            double levelXpRatio = GetXpRatioBetweenLevels(xpTable, (uint)(creature.Level ?? 1), newLevel);
            newLevelTotalXp = (ulong)(totalXp * levelXpRatio);

            double healthRatio = (double)healthTotalXp / totalXp;
            double staminaRatio = (double)staminaTotalXp / totalXp;
            double manaRatio = (double)manaTotalXp / totalXp;

            double strengthRatio = (double)strengthTotalXp / totalXp;
            double enduranceRatio = (double)enduranceTotalXp / totalXp;
            double coordinationRatio = (double)coordinationTotalXp / totalXp;
            double quicknessRatio = (double)quicknessTotalXp / totalXp;
            double focusRatio = (double)focusTotalXp / totalXp;
            double selfRatio = (double)selfTotalXp / totalXp;

            Dictionary<Skill, double> skillRatioMap = new Dictionary<Skill, double>();
            foreach (var entry in skillTotalXpMap)
            {
                double skillRatio = (double)entry.Value / totalXp;
                skillRatioMap.Add(entry.Key, skillRatio);
            }

            uint newHealth = GetLevel(vitalTable, (ulong)Math.Round(healthRatio * newLevelTotalXp));
            uint newStamina = GetLevel(vitalTable, (ulong)Math.Round(staminaRatio * newLevelTotalXp));
            uint newMana = GetLevel(vitalTable, (ulong)Math.Round(manaRatio * newLevelTotalXp));

            uint newStrength = GetLevel(attributeTable, (ulong)Math.Round(strengthRatio * newLevelTotalXp));
            uint newEndurance = GetLevel(attributeTable, (ulong)Math.Round(enduranceRatio * newLevelTotalXp));
            uint newCoordination = GetLevel(attributeTable, (ulong)Math.Round(coordinationRatio * newLevelTotalXp));
            uint newQuickness = GetLevel(attributeTable, (ulong)Math.Round(quicknessRatio * newLevelTotalXp));
            uint newFocus = GetLevel(attributeTable, (ulong)Math.Round(focusRatio * newLevelTotalXp));
            uint newSelf = GetLevel(attributeTable, (ulong)Math.Round(selfRatio * newLevelTotalXp));

            Dictionary<Skill, uint> skillNewValueMap = new Dictionary<Skill, uint>();
            foreach (var entry in skillRatioMap)
            {
                uint newValue = GetLevel(skillTable, (ulong)Math.Round(entry.Value * newLevelTotalXp));
                skillNewValueMap.Add(entry.Key, newValue);
            }

            if (!toSql)
            {
                creature.IsModified = true;

                creature.PlayParticleEffect(PlayScript.AetheriaLevelUp, creature.Guid);

                if(!keepLevel)
                    creature.Level = (int)newLevel;

                creature.Strength.StartingValue = newStrength;
                creature.Endurance.StartingValue = newEndurance;
                creature.Coordination.StartingValue = newCoordination;
                creature.Quickness.StartingValue = newQuickness;
                creature.Focus.StartingValue = newFocus;
                creature.Self.StartingValue = newSelf;

                creature.Health.StartingValue = newHealth;
                creature.Stamina.StartingValue = newStamina;
                creature.Mana.StartingValue = newMana;
                creature.SetMaxVitals();

                foreach (var entry in skillNewValueMap)
                {
                    if (creature.Skills.TryGetValue(entry.Key, out var skill))
                        skill.InitLevel = entry.Value;
                }

                var newBodyParts = new Dictionary<CombatBodyPart, ACE.Entity.Models.PropertiesBodyPart>(creature.Weenie.PropertiesBodyPart);
                foreach (var entry in newBodyParts)
                {
                    var bodyPart = entry.Value;
                    if (bodyPart.BaseArmor != 0)
                    {
                        double armorRatio = (double)bodyPart.BaseArmor / (creature.Level ?? 1);
                        bodyPart.BaseArmor = (int)Math.Round(newLevel * armorRatio);
                        bodyPart.ArmorVsSlash = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsSlash ?? 0.0));
                        bodyPart.ArmorVsPierce = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsPierce ?? 0.0));
                        bodyPart.ArmorVsBludgeon = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsBludgeon ?? 0.0));
                        bodyPart.ArmorVsCold = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsCold ?? 0.0));
                        bodyPart.ArmorVsFire = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsFire ?? 0.0));
                        bodyPart.ArmorVsElectric = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsElectric ?? 0.0));
                        bodyPart.ArmorVsAcid = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsAcid ?? 0.0));
                        bodyPart.ArmorVsNether = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsNether ?? 0.0));
                    }

                    if (bodyPart.DVal != 0)
                    {
                        double damageRatio = (double)bodyPart.DVal / (creature.Level ?? 1);
                        bodyPart.DVal = (int)Math.Round(newLevel * damageRatio);
                    }
                }

                creature.Biota.PropertiesBodyPart = newBodyParts;
                creature.ClearModifiedBodyPartTable();

                if (creature.Biota.PropertiesSpellBook != null)
                {
                    uint warMagicSkill = 0;
                    uint lifeMagicSkill = 0;
                    uint creatureEnchantmentSkill = 0;
                    uint itemEnchantmentSkill = 0;
                    skillNewValueMap.TryGetValue(Skill.WarMagic, out warMagicSkill);
                    skillNewValueMap.TryGetValue(Skill.LifeMagic, out lifeMagicSkill);
                    skillNewValueMap.TryGetValue(Skill.CreatureEnchantment, out creatureEnchantmentSkill);
                    skillNewValueMap.TryGetValue(Skill.ItemEnchantment, out itemEnchantmentSkill);

                    var newSpells = new Dictionary<int, float>();

                    foreach (var entry in creature.Biota.PropertiesSpellBook)
                    {
                        Entity.Spell currentSpell = new Entity.Spell(entry.Key);

                        uint magicSkill = 0;
                        switch (currentSpell.School)
                        {
                            case MagicSchool.WarMagic:
                                magicSkill = warMagicSkill;
                                break;
                            case MagicSchool.LifeMagic:
                                magicSkill = lifeMagicSkill;
                                break;
                            case MagicSchool.CreatureEnchantment:
                                magicSkill = creatureEnchantmentSkill;
                                break;
                            case MagicSchool.ItemEnchantment:
                                magicSkill = itemEnchantmentSkill;
                                break;
                        }

                        var level1SpellId = SpellLevelProgression.GetLevel1SpellId((SpellId)entry.Key);
                        if (level1SpellId != SpellId.Undef)
                        {
                            SpellId newSpellId = SpellId.Undef;
                            int newSpellLevel = 0;

                            for (int level = 1; level <= 7; level++)
                            {
                                SpellId newSpellIdAttempt = SpellLevelProgression.GetSpellAtLevel(level1SpellId, level, true);
                                Entity.Spell newSpellAttempt = new Entity.Spell(newSpellIdAttempt);

                                if (newSelf + newMana >= newSpellAttempt.BaseMana && magicSkill >= (currentSpell.IsSelfTargeted ? (int)newSpellAttempt.Power + 50 : (int)newSpellAttempt.Power + 30)) // Creatures tend to cast lower level self spells.
                                {
                                    newSpellId = newSpellIdAttempt;
                                    newSpellLevel = level;
                                }
                                else
                                    break;
                            }

                            while (newSpellId != SpellId.Undef && newSpellLevel > 0)
                            {
                                if (newSpells.ContainsKey((int)newSpellId))
                                {
                                    // Let's try a lower level version of the same spell.
                                    newSpellLevel--;
                                    if (newSpellLevel > 0)
                                        newSpellId = SpellLevelProgression.GetSpellAtLevel(level1SpellId, newSpellLevel, true);
                                    else
                                        newSpells.Add((int)level1SpellId, entry.Value); // Let's go ahead and add it at level 1 anyway to keep it in the spellbook.
                                }
                                else
                                {
                                    newSpells.Add((int)newSpellId, entry.Value);
                                    break;
                                }
                            }
                        }
                    }

                    creature.Biota.PropertiesSpellBook = newSpells;
                }
            }
            else
            {
                DirectoryInfo di = VerifyContentFolder(session, false);

                var sep = Path.DirectorySeparatorChar;

                var sql_folder = $"{di.FullName}{sep}sql{sep}weenies{sep}";

                di = new DirectoryInfo(sql_folder);

                if (!di.Exists)
                    di.Create();

                if (WeenieSQLWriter == null)
                {
                    WeenieSQLWriter = new WeenieSQLWriter();
                    WeenieSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    WeenieSQLWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
                    WeenieSQLWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
                    WeenieSQLWriter.SpellNames = DatabaseManager.World.GetAllSpellNames();
                    WeenieSQLWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    WeenieSQLWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                    WeenieSQLWriter.PacketOpCodes = PacketOpCodeNames.Values;
                }

                Weenie weenie = DatabaseManager.World.GetWeenie(creature.WeenieClassId);
                Weenie weenieOriginal = DatabaseManager.World.GetWeenie(creature.WeenieClassId);

                foreach (var bodyPart in weenie.WeeniePropertiesBodyPart)
                {
                    if (bodyPart.BaseArmor != 0)
                    {
                        double armorRatio = (double)bodyPart.BaseArmor / (creature.Level ?? 1);
                        bodyPart.BaseArmor = (int)Math.Round(newLevel * armorRatio);
                        bodyPart.ArmorVsSlash = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsSlash ?? 0.0));
                        bodyPart.ArmorVsPierce = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsPierce ?? 0.0));
                        bodyPart.ArmorVsBludgeon = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsBludgeon ?? 0.0));
                        bodyPart.ArmorVsCold = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsCold ?? 0.0));
                        bodyPart.ArmorVsFire = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsFire ?? 0.0));
                        bodyPart.ArmorVsElectric = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsElectric ?? 0.0));
                        bodyPart.ArmorVsAcid = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsAcid ?? 0.0));
                        bodyPart.ArmorVsNether = (int)Math.Round(bodyPart.BaseArmor * (creature.ArmorModVsNether ?? 0.0));
                    }

                    if (bodyPart.DVal != 0)
                    {
                        double damageRatio = (double)bodyPart.DVal / (creature.Level ?? 1);
                        bodyPart.DVal = (int)Math.Round(newLevel * damageRatio);
                    }
                }

                if (weenie.WeeniePropertiesSpellBook != null)
                {
                    uint warMagicSkill = 0;
                    uint lifeMagicSkill = 0;
                    uint creatureEnchantmentSkill = 0;
                    uint itemEnchantmentSkill = 0;
                    skillNewValueMap.TryGetValue(Skill.WarMagic, out warMagicSkill);
                    skillNewValueMap.TryGetValue(Skill.LifeMagic, out lifeMagicSkill);
                    skillNewValueMap.TryGetValue(Skill.CreatureEnchantment, out creatureEnchantmentSkill);
                    skillNewValueMap.TryGetValue(Skill.ItemEnchantment, out itemEnchantmentSkill);

                    var newSpells = new List<SpellId>();

                    foreach (var entry in weenie.WeeniePropertiesSpellBook)
                    {
                        Entity.Spell currentSpell = new Entity.Spell(entry.Spell);

                        uint magicSkill = 0;
                        switch (currentSpell.School)
                        {
                            case MagicSchool.WarMagic:
                                magicSkill = warMagicSkill;
                                break;
                            case MagicSchool.LifeMagic:
                                magicSkill = lifeMagicSkill;
                                break;
                            case MagicSchool.CreatureEnchantment:
                                magicSkill = creatureEnchantmentSkill;
                                break;
                            case MagicSchool.ItemEnchantment:
                                magicSkill = itemEnchantmentSkill;
                                break;
                        }

                        var level1SpellId = SpellLevelProgression.GetLevel1SpellId((SpellId)entry.Spell);
                        if (level1SpellId != SpellId.Undef)
                        {
                            SpellId newSpellId = SpellId.Undef;
                            int newSpellLevel = 0;

                            for (int level = 1; level <= 7; level++)
                            {
                                SpellId newSpellIdAttempt = SpellLevelProgression.GetSpellAtLevel(level1SpellId, level, true);
                                Entity.Spell newSpellAttempt = new Entity.Spell(newSpellIdAttempt);

                                if (newSelf + newMana >= newSpellAttempt.BaseMana && magicSkill >= (currentSpell.IsSelfTargeted ? (int)newSpellAttempt.Power + 50 : (int)newSpellAttempt.Power + 30)) // Creatures tend to cast lower level self spells.
                                {
                                    newSpellId = newSpellIdAttempt;
                                    newSpellLevel = level;
                                }
                                else
                                    break;
                            }

                            while (newSpellId != SpellId.Undef && newSpellLevel > 0)
                            {
                                if (newSpells.Contains(newSpellId))
                                {
                                    // Let's try a lower level version of the same spell.
                                    newSpellLevel--;
                                    if (newSpellLevel > 0)
                                        newSpellId = SpellLevelProgression.GetSpellAtLevel(level1SpellId, newSpellLevel, true);
                                    else
                                        entry.Spell = (int)level1SpellId; // Let's go ahead and add it at level 1 anyway to keep it in the spellbook.
                                }
                                else
                                {
                                    entry.Spell = (int)newSpellId;
                                    newSpells.Add(newSpellId);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!keepLevel)
                {
                    WeeniePropertiesInt weenieLevel = (from x in weenie.WeeniePropertiesInt where x.Type == (int)PropertyInt.Level select x).FirstOrDefault();
                    if (weenieLevel != null)
                        weenieLevel.Value = (int)newLevel;
                }

                WeeniePropertiesAttribute2nd weenieHealth = (from x in weenie.WeeniePropertiesAttribute2nd where x.Type == (int)PropertyAttribute2nd.MaxHealth select x).FirstOrDefault();
                if (weenieHealth != null)
                {
                    weenieHealth.InitLevel = newHealth;
                    weenieHealth.CurrentLevel = newHealth + AttributeFormula.GetFormula(DatLoader.DatManager.PortalDat.SecondaryAttributeTable.MaxHealth.Formula, newEndurance);
                }
                WeeniePropertiesAttribute2nd weenieStamina = (from x in weenie.WeeniePropertiesAttribute2nd where x.Type == (int)PropertyAttribute2nd.MaxStamina select x).FirstOrDefault();
                if (weenieStamina != null)
                {
                    weenieStamina.InitLevel = newStamina;
                    weenieStamina.CurrentLevel = newStamina + AttributeFormula.GetFormula(DatLoader.DatManager.PortalDat.SecondaryAttributeTable.MaxStamina.Formula, newEndurance);
                }
                WeeniePropertiesAttribute2nd weenieMana = (from x in weenie.WeeniePropertiesAttribute2nd where x.Type == (int)PropertyAttribute2nd.MaxMana select x).FirstOrDefault();
                if (weenieMana != null)
                {
                    weenieMana.InitLevel = newMana;
                    weenieMana.CurrentLevel = newMana + AttributeFormula.GetFormula(DatLoader.DatManager.PortalDat.SecondaryAttributeTable.MaxMana.Formula, newSelf);
                }

                WeeniePropertiesAttribute weenieStrength = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Strength select x).FirstOrDefault();
                if (weenieStrength != null)
                    weenieStrength.InitLevel = newStrength;
                WeeniePropertiesAttribute weenieEndurance = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Endurance select x).FirstOrDefault();
                if (weenieEndurance != null)
                    weenieEndurance.InitLevel = newEndurance;
                WeeniePropertiesAttribute weenieCoordination = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Coordination select x).FirstOrDefault();
                if (weenieCoordination != null)
                    weenieCoordination.InitLevel = newCoordination;
                WeeniePropertiesAttribute weenieQuickness = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Quickness select x).FirstOrDefault();
                if (weenieQuickness != null)
                    weenieQuickness.InitLevel = newQuickness;
                WeeniePropertiesAttribute weenieFocus = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Focus select x).FirstOrDefault();
                if (weenieFocus != null)
                    weenieFocus.InitLevel = newFocus;
                WeeniePropertiesAttribute weenieSelf = (from x in weenie.WeeniePropertiesAttribute where x.Type == (int)PropertyAttribute.Self select x).FirstOrDefault();
                if (weenieSelf != null)
                    weenieSelf.InitLevel = newSelf;

                foreach (var entry in skillNewValueMap)
                {
                    WeeniePropertiesSkill weenieSkill = (from x in weenie.WeeniePropertiesSkill where x.Type == (int)entry.Key select x).FirstOrDefault();
                    if (weenieSkill != null)
                        weenieSkill.InitLevel = entry.Value;
                }


                // Export the original weenie for comparison/backup purposes.
                var sql_filename = WeenieSQLWriter.GetDefaultFileName(weenieOriginal);
                var writer = new StreamWriter(sql_folder + sql_filename);

                try
                {
                    WeenieSQLWriter.CreateSQLDELETEStatement(weenieOriginal, writer);
                    writer.WriteLine();
                    WeenieSQLWriter.CreateSQLINSERTStatement(weenieOriginal, writer);
                    writer.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
                    return;
                }

                // And finally export the modified weenie.
                var sql_filename_new = WeenieSQLWriter.GetDefaultFileName(weenie, $" - Scaled from level {creature.Level} to {newLevel}" + (keepLevel ? " - keepLevel" : ""));
                writer = new StreamWriter(sql_folder + sql_filename_new);

                try
                {
                    WeenieSQLWriter.CreateSQLDELETEStatement(weenie, writer);
                    writer.WriteLine();
                    WeenieSQLWriter.CreateSQLINSERTStatement(weenie, writer);
                    writer.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename_new}");
                    return;
                }

                CommandHandlerHelper.WriteOutputInfo(session, $"Exported \"{sql_folder}{sql_filename}\" and \"{sql_filename_new}\"");
            }
        }

        public static double GetXpRatioBetweenLevels(List<ulong> table, uint levelA, uint levelB)
        {
            uint maxLevel = (uint)(table.Count - 1);

            levelA = Math.Clamp(levelA, 1, maxLevel);
            levelB = Math.Clamp(levelB, 1, maxLevel);

            var levelA_totalXP = table[(int)levelA];
            var levelB_totalXP = table[(int)levelB];

            return (double)levelB_totalXP / levelA_totalXP;
        }

        private static uint GetLevel(List<uint> table, ulong totalXp)
        {
            int level = 0;
            while (totalXp >= table[level + 1])
            {
                level++;

                if (level == table.Count - 1)
                {
                    ulong lastLevelTotalXP = table[table.Count - 1];
                    ulong lastLevelXpDiff = table[table.Count - 1] - table[table.Count - 2];
                    int extraLevels = (int)((totalXp - lastLevelTotalXP) / lastLevelXpDiff);

                    level += extraLevels;
                    break;
                }
            }

            return (uint)level * 2;
        }

        private static ulong GetTotalXP(List<uint> table, uint level)
        {
            level /= 2;

            if (level < table.Count - 1)
            {
                return table[(int)level];
            }
            else
            {
                uint lastLevelXp = table[table.Count - 1] - table[table.Count - 2];
                int extraLevels = (int)level - (table.Count - 1);
                ulong extraXp = (ulong)(lastLevelXp * extraLevels);

                return table[table.Count - 1] + extraXp;
            }
        }

        private static List<uint> RecursedList;
        private static string GetEntranceDirections(uint nameSourceWcid, bool recursing = false)
        {
            if (nameSourceWcid == 0)
                return "";

            if (!recursing)
                RecursedList = new List<uint>();

            var directions = "";
            var instance = DatabaseManager.World.GetLandblockInstancesByWcid(nameSourceWcid).FirstOrDefault();
            if (instance != null)
            {
                var pos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                var landblock = ACE.Server.Physics.Common.LScape.get_landblock(pos.Landblock << 16);
                if (!landblock.IsDungeon && (pos.Landblock & 0x000000FF) < 0xFA && ((pos.Landblock & 0x0000FF00) >> 8) > 0x02)
                    directions = $"at {pos.GetMapCoordStr(true)}";
                else
                {
                    var name = LandblockInstanceWriter.GetNameFromPortalDestination(pos.Landblock, out var newNameSourceWcid);
                    if (RecursedList.Contains(newNameSourceWcid))
                        return "";
                    else
                    {
                        RecursedList.Add(newNameSourceWcid);
                        return $"inside {name} {GetEntranceDirections(newNameSourceWcid, true)}";
                    }
                }
            }

            return directions.Trim();
        }

        [CommandHandler("export-landblock-levels", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportLandblockLevels(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting landblock levels to reports/LandblockLevels.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}LandblockLevels.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("Landblock Id\tName\tDirections\tWeighted Average Level\tContent Description\tEntrance Level Min\tEntrance Level Max\tTotal Creature Count\tEntrance Quest Restriction\tContainer Count\tMin Creature Level\tMax Creature Level\tCreatures\tLevels");

            var weenieTypes = DatabaseManager.World.GetAllWeenieTypes();

            for (ushort landblockIdX = 0x00; landblockIdX < 0xFF; landblockIdX++)
            {
                for (ushort landblockIdY = 0x00; landblockIdY < 0xFF; landblockIdY++)
                {
                    ushort landblockId = (ushort)(landblockIdX << 8 | landblockIdY);
                    var landblockInstances = DatabaseManager.World.GetCachedInstancesByLandblock(landblockId);
                    var name = LandblockInstanceWriter.GetNameFromPortalDestination(landblockId, out var nameSourceWcid);
                    var minLevel = int.MaxValue;
                    var maxLevel = 0;
                    var creatureFamilyList = new SortedDictionary<ACE.Entity.Enum.CreatureType,int>();
                    var creatureLevelCountList = new SortedDictionary<int, int>();
                    var containerCount = 0;
                    var totalCreatureCount = 0;
                    var directions = GetEntranceDirections(nameSourceWcid);
                    var entranceLevelMin = 0;
                    var entranceLevelMax = 0;
                    var entranceQuestRestriction = "";
                    var contentDescription = "";
                    if(nameSourceWcid != 0)
                    {
                        var entranceWeenie = DatabaseManager.World.GetWeenie(nameSourceWcid);

                        if (entranceWeenie != null)
                        {
                            entranceLevelMin = entranceWeenie.GetProperty(PropertyInt.MinLevel) ?? 0;
                            entranceLevelMax = entranceWeenie.GetProperty(PropertyInt.MaxLevel) ?? 0;
                            entranceQuestRestriction = entranceWeenie.GetProperty(PropertyString.QuestRestriction) ?? "";
                        }
                    }

                    if (landblockInstances != null)
                    {
                        foreach (var instance in landblockInstances)
                        {
                            var weenie = DatabaseManager.World.GetWeenie(instance.WeenieClassId);
                            if (weenie == null)
                                continue;

                            var entriesList = new List<uint>();
                            entriesList.Add(instance.WeenieClassId);
                            foreach (var generatorEntry in weenie.WeeniePropertiesGenerator)
                                entriesList.Add(generatorEntry.WeenieClassId);

                            foreach (var entry in entriesList)
                            {
                                if (weenieTypes.TryGetValue(entry, out var weenieType))
                                {
                                    var entryWeenie = DatabaseManager.World.GetWeenie(entry);
                                    if (entryWeenie == null)
                                        continue;

                                    var playerKillerStatus = (PlayerKillerStatus?)entryWeenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                                    var npcLooksLikeObject = entryWeenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                                    if (playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                                    {
                                        if (weenieType == (int)ACE.Entity.Enum.WeenieType.Chest || weenieType == (int)ACE.Entity.Enum.WeenieType.Container)
                                            containerCount++;

                                        var creatureType = entryWeenie.GetProperty(PropertyInt.CreatureType) ?? 0;
                                        if (creatureType != 0)
                                        {
                                            if (creatureFamilyList.TryGetValue((ACE.Entity.Enum.CreatureType)creatureType, out var creatureFamilyCount))
                                                creatureFamilyList[(ACE.Entity.Enum.CreatureType)creatureType] = creatureFamilyCount + 1;
                                            else
                                                creatureFamilyList.Add((ACE.Entity.Enum.CreatureType)creatureType, 1);
                                        }

                                        var level = entryWeenie.GetProperty(PropertyInt.Level) ?? 0;
                                        if (level != 0)
                                        {
                                            if (level < minLevel)
                                                minLevel = level;
                                            if (level > maxLevel)
                                                maxLevel = level;

                                            totalCreatureCount++;

                                            if (creatureLevelCountList.TryGetValue(level, out var creatureLevelCount))
                                                creatureLevelCountList[level] = creatureLevelCount + 1;
                                            else
                                                creatureLevelCountList.Add(level, 1);
                                        }
                                    }
                                }
                            }
                        }

                        if (minLevel != int.MaxValue)
                        {
                            var weightedAverageLevel = 0;
                            foreach(var entry in creatureLevelCountList)
                                weightedAverageLevel += entry.Key * entry.Value;

                            weightedAverageLevel /= totalCreatureCount;

                            var lastEntry = "";
                            foreach (var creatureType in creatureFamilyList)
                            {
                                if (creatureType.Value >= totalCreatureCount * 0.1)
                                {
                                    var creatureTypeString = creatureType.Key.ToString();
                                    var friendlyName = string.Concat(creatureTypeString.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(" ");
                                    if (contentDescription.Length > 0)
                                        lastEntry = $", {friendlyName}{(friendlyName.EndsWith("s") ? "es" : "s")}";
                                    else
                                        lastEntry = $"{friendlyName}{(friendlyName.EndsWith("s") ? "es" : "s")}";
                                    contentDescription += lastEntry;

                                }
                            }

                            contentDescription = contentDescription.Replace(lastEntry, lastEntry.Replace(",", " and"));

                            fileWriter.WriteLine($"{landblockId.ToString("x4")}\t{name}\t{directions}\t{weightedAverageLevel}\t{contentDescription}\t{entranceLevelMin}\t{entranceLevelMax}\t{totalCreatureCount}\t{entranceQuestRestriction}\t{containerCount}\t{minLevel}\t{maxLevel}\t{string.Join(",", creatureFamilyList)}\t{string.Join(",", creatureLevelCountList)}");
                            fileWriter.Flush();
                        }
                    }
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-landblock-description", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportLandblockDescription(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting landblock description to reports/LandblockDescription.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}LandblockDescription.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("Landblock Id\tName\tIsDungeon\tHasDungeon\tDirections");

            var weenieTypes = DatabaseManager.World.GetAllWeenieTypes();

            for (byte landblockIdX = 0x00; landblockIdX < 0xFF; landblockIdX++)
            {
                for (byte landblockIdY = 0x00; landblockIdY < 0xFF; landblockIdY++)
                {
                    ushort landblockId = (ushort)(landblockIdX << 8 | landblockIdY);
                    var name = LandblockInstanceWriter.GetNameFromPortalDestination(landblockId, out var nameSourceWcid);
                    var landblock = LandblockManager.GetLandblock(new LandblockId(landblockIdX, landblockIdY), false, false);
                    var isDungeon = landblock.IsDungeon;
                    var hasDungeon = landblock.HasDungeon;

                    var directions = "";
                    if(isDungeon || hasDungeon)
                        directions = GetEntranceDirections(nameSourceWcid);

                    fileWriter.WriteLine($"{landblockId.ToString("x4")}\t{name}\t{isDungeon}\t{hasDungeon}\t{directions}\t");
                    fileWriter.Flush();
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-creature-clothing", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportCreatureClothing(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting creature clothing ids to reports/CreatureClothingIdList.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}CreatureClothingIdList.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("ClothingId\tType\tWeenieClassId\tWeenieClassName");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var list = new List<uint>();
            foreach (var entry in WeenieTypes)
            {
                if (entry.Value != (int)WeenieType.Creature && entry.Value != (int)WeenieType.Cow)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(entry.Key);
                var clothingId = weenie.GetProperty(PropertyDataId.ClothingBase) ?? 0;
                var creatureType = (CreatureType?)weenie.GetProperty(PropertyInt.CreatureType);
                if (clothingId != 0 && !list.Contains(clothingId))
                {
                    list.Add(clothingId);
                    fileWriter.WriteLine($"{clothingId.ToString("x8")}\t{creatureType}\t{weenie.ClassId}\t{weenie.ClassName}");
                    fileWriter.Flush();
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-creature-levels", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportCreatureLevels(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting creature levels to reports/CreatureLevels.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}CreatureLevels.txt";

            var fileWriter = new StreamWriter(filename);

            //fileWriter.WriteLine("level\tcalculated level\tlevel difference\tname\tweenieClassId\tweenieClassName\ttype");
            fileWriter.WriteLine("Name\tLevel\tType\tWeenieClassId\tWeenieClassName");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            foreach (var entry in WeenieTypes)
            {
                if (entry.Value != (int)WeenieType.Creature && entry.Value != (int)WeenieType.Cow)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(entry.Key);
                var playerKillerStatus = (PlayerKillerStatus?)weenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                var npcLooksLikeObject = weenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                if (weenie.ClassId != 1 && playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                {
                    var name = weenie.GetProperty(PropertyString.Name);
                    var level = weenie.GetProperty(PropertyInt.Level);
                    var creatureType = (CreatureType?)weenie.GetProperty(PropertyInt.CreatureType);

                    fileWriter.WriteLine($"{name}\t{level}\t{creatureType}\t{weenie.ClassId}\t{weenie.ClassName}");
                    fileWriter.Flush();
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-creature-calculated-levels", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportCreatureCalculatedLevels(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting creature calculated levels to reports/CreatureCalculatedLevels.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}CreatureCalculatedLevels.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("Name\tLevel\tCalculatedLevel\tCalculatedTier\tType\tWeenieClassId\tWeenieClassName");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            foreach (var entry in WeenieTypes)
            {
                if (entry.Value != (int)WeenieType.Creature && entry.Value != (int)WeenieType.Cow)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(entry.Key);
                var playerKillerStatus = (PlayerKillerStatus?)weenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                var npcLooksLikeObject = weenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                if (weenie.ClassId != 1 && playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                {
                    var name = weenie.GetProperty(PropertyString.Name);
                    var level = weenie.GetProperty(PropertyInt.Level);
                    var calculatedLevel = 0;
                    var calculatedTier = 0d;

                    var obj = WorldObjectFactory.CreateNewWorldObject(weenie.ClassId);
                    if (obj != null)
                    {
                        calculatedLevel = CalculateLevel(obj as Creature);
                        calculatedTier = Creature.CalculateExtendedTier(calculatedLevel);
                        obj.Destroy();
                    }

                    var creatureType = (CreatureType?)weenie.GetProperty(PropertyInt.CreatureType);

                    fileWriter.WriteLine($"{name}\t{level}\t{calculatedLevel}\t{calculatedTier:0.00}\t{creatureType}\t{weenie.ClassId}\t{weenie.ClassName}");
                    fileWriter.Flush();
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("calculatelevel", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "", "")]
        public static void HandleCalculateLevel(Session session, params string[] parameters)
        {
            var obj = CommandHandlerHelper.GetQueryTarget(session);

            Creature creature = obj as Creature;

            if (creature == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid target.");
                return;
            }

            var level = CalculateLevel(creature);

            CommandHandlerHelper.WriteOutputInfo(session, $"-- Creature: {creature.Name}({creature.WeenieClassId}) --");
            CommandHandlerHelper.WriteOutputInfo(session, $"Current Level: {creature.Level}");
            CommandHandlerHelper.WriteOutputInfo(session, $"Calculated Level: {level}");
        }

        private static int CalculateLevelInternal(Creature creature, double simulatedBuffedAmount)
        {
            var xpTable = DatLoader.DatManager.PortalDat.XpTable.CharacterLevelXPList;
            var attributeTable = DatLoader.DatManager.PortalDat.XpTable.AttributeXpList;
            var vitalTable = DatLoader.DatManager.PortalDat.XpTable.VitalXpList;
            var skillTableTrained = DatLoader.DatManager.PortalDat.XpTable.TrainedSkillXpList;
            //var skillTableSpecialized = DatLoader.DatManager.PortalDat.XpTable.SpecializedSkillXpList;t;

            ulong totalXp = 0;

            var melee = creature.GetCreatureSkill(Skill.MeleeDefense);
            var missile = creature.GetCreatureSkill(Skill.MissileDefense);
            var magic = creature.GetCreatureSkill(Skill.MagicDefense);

            var lowestDefenseSkillXP = ulong.MaxValue;
            var meleeXP = GetTotalXP(skillTableTrained, (uint)Math.Max((int)(melee.InitLevel + melee.Ranks) - simulatedBuffedAmount, 0));
            var missileXP = GetTotalXP(skillTableTrained, (uint)Math.Max((int)(missile.InitLevel + missile.Ranks) - simulatedBuffedAmount, 0));
            var magicXP = GetTotalXP(skillTableTrained, (uint)Math.Max((int)(magic.InitLevel + magic.Ranks) - simulatedBuffedAmount, 0));

            if (meleeXP < lowestDefenseSkillXP)
                lowestDefenseSkillXP = meleeXP;
            if (missileXP < lowestDefenseSkillXP)
                lowestDefenseSkillXP = missileXP;
            if (magicXP < lowestDefenseSkillXP)
                lowestDefenseSkillXP = magicXP;

            if (lowestDefenseSkillXP == uint.MaxValue)
                lowestDefenseSkillXP = 0;

            totalXp += lowestDefenseSkillXP;

            uint highestCombatSkillValue = 0;
            uint highestMagicSkillValue = 0;
            foreach (var skillEntry in creature.Skills)
            {
                var skill = skillEntry.Value;
                var value = (uint)Math.Max((int)(skill.InitLevel + skill.Ranks) - simulatedBuffedAmount, 0);
                switch (skill.Skill)
                {
                    case Skill.Axe:
                    case Skill.Mace:
                    case Skill.Spear:
                    case Skill.Staff:
                    case Skill.Dagger:
                    case Skill.Sword:
                    case Skill.UnarmedCombat:
                    case Skill.Bow:
                    case Skill.Crossbow:
                    case Skill.ThrownWeapon:
                        if (value > highestCombatSkillValue)
                            highestCombatSkillValue = value;
                        break;
                    case Skill.CreatureEnchantment:
                    case Skill.ItemEnchantment:
                    case Skill.LifeMagic:
                    case Skill.WarMagic:
                        if (value > highestMagicSkillValue)
                            highestMagicSkillValue = value;
                        break;
                }
            }

            var combatSkillXP = GetTotalXP(skillTableTrained, highestCombatSkillValue);
            var magicSkillXP = GetTotalXP(skillTableTrained, highestMagicSkillValue);

            if (combatSkillXP > magicSkillXP)
                totalXp += combatSkillXP;
            else
                totalXp += magicSkillXP;

            var attributesCopy = new Dictionary<PropertyAttribute, CreatureAttribute>(creature.Attributes);
            attributesCopy = attributesCopy.OrderByDescending(a => a.Value.StartingValue + a.Value.Ranks).ToDictionary();
            var highestAttribute = attributesCopy.First().Value;
            var highestAttributeValue = highestAttribute.StartingValue + highestAttribute.Ranks;

            var innatePool = 270;
            //if (highestAttributeValue < 100)
            //    innatePool = (int)(highestAttributeValue * 2);

            foreach (var attribute in attributesCopy.Values)
            {
                var value = (uint)Math.Max((int)(attribute.StartingValue + attribute.Ranks) - simulatedBuffedAmount, 0);

                if (innatePool > 0)
                {
                    var innateValue = Math.Min(innatePool, 100);
                    if (value >= innateValue)
                    {
                        value -= (uint)innateValue;
                        innatePool -= innateValue;
                    }
                    else
                    {
                        innatePool -= (int)value;
                        value = 0;
                    }
                }

                if (value > 0)
                    totalXp += GetTotalXP(attributeTable, value);
            }

            var level = 1;
            while (totalXp >= xpTable[level + 1])
            {
                level++;

                if (level == xpTable.Count - 1)
                {
                    ulong lastLevelTotalXP = xpTable[xpTable.Count - 1];
                    ulong lastLevelXpDiff = xpTable[xpTable.Count - 1] - xpTable[xpTable.Count - 2];
                    int levelsAboveTable = (int)((totalXp - lastLevelTotalXP) / lastLevelXpDiff);

                    level += levelsAboveTable;
                    break;
                }
            }

            if (level < 10)
            {
                // For the lowest level creatures lets use HP to create more granularity between creature levels.
                var healthXP = GetTotalXP(vitalTable, Math.Min(creature.Health.Base, 100));
                healthXP = (ulong)(healthXP * ((10.0 - level) / 9.0));
                totalXp += healthXP;

                while (totalXp >= xpTable[level + 1])
                {
                    level++;

                    if (level == xpTable.Count - 1)
                    {
                        ulong lastLevelTotalXP = xpTable[xpTable.Count - 1];
                        ulong lastLevelXpDiff = xpTable[xpTable.Count - 1] - xpTable[xpTable.Count - 2];
                        int levelsAboveTable = (int)((totalXp - lastLevelTotalXP) / lastLevelXpDiff);

                        level += levelsAboveTable;
                        break;
                    }
                }
            }

            return level;
        }

        public static int CalculateLevel(Creature creature)
        {
            if (creature == null)
                return 0;

            // First we determine out general tier.
            var level = CalculateLevelInternal(creature, 0);
            var floatTier = Creature.CalculateExtendedTier(level);
            var tier = (int)floatTier;

            // Now that we have our tier we restart for the proper calculations.
            var simulatedBuffedAmount = 5 + ((floatTier - 0.5) * 5);
            level = CalculateLevelInternal(creature, simulatedBuffedAmount);

            // Add extra levels on top depending on the creature's damage, spells and other properties.
            var damageList = new List<Tuple<int, ACE.Entity.Models.Weenie>>();

            var hasSpecialAttack = false;
            if (creature.CombatTableDID.HasValue)
            {
                var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(creature.MotionTableId);
                var combatTable = DatManager.PortalDat.ReadFromDat<CombatManeuverTable>(creature.CombatTableDID.Value);
                combatTable.Stances.TryGetValue(MotionStance.HandCombat, out var stanceManeuvers);

                foreach (var combatManeuvers in stanceManeuvers.Table.Values)
                {
                    foreach (var attackTypes in combatManeuvers.Table)
                    {
                        foreach (var motion in attackTypes.Value)
                        {
                            var attackFrames = motionTable.GetAttackFrames(creature.MotionTableId, MotionStance.HandCombat, motion);
                            foreach (var attackFrame in attackFrames)
                            {
                                var attackPart = creature.GetAttackPart(motion, attackFrame.attackHook);
                                if (attackPart.Key == CombatBodyPart.Breath)
                                    hasSpecialAttack = true;
                                damageList.Add(Tuple.Create(attackPart.Value.DVal, (ACE.Entity.Models.Weenie)null));
                            }
                        }
                    }
                }
            }

            var meleeWeapons = new List<ACE.Entity.Models.Weenie>();
            var missileWeapons = new List<ACE.Entity.Models.Weenie>();
            var ammo = new List<ACE.Entity.Models.Weenie>();
            if (creature.WieldedTreasure != null)
            {
                foreach (var item in creature.WieldedTreasure)
                {
                    if (item.WeenieClassId == 0)
                        continue;

                    var weenie = DatabaseManager.World.GetCachedWeenie(item.WeenieClassId);
                    switch(weenie.WeenieType)
                    {
                        case WeenieType.MeleeWeapon:
                        case WeenieType.Missile:
                            meleeWeapons.Add(weenie);
                            break;
                        case WeenieType.MissileLauncher:
                            missileWeapons.Add(weenie);
                            break;
                        case WeenieType.Ammunition:
                            ammo.Add(weenie);
                            break;
                    }
                }
            }

            if (creature.Biota.PropertiesCreateList != null)
            {
                foreach (var item in creature.Biota.PropertiesCreateList.Where(x => x.DestinationType == DestinationType.Wield || x.DestinationType == DestinationType.WieldTreasure))
                {
                    if (item.WeenieClassId == 0)
                        continue;

                    var weenie = DatabaseManager.World.GetCachedWeenie(item.WeenieClassId);
                    switch (weenie.WeenieType)
                    {
                        case WeenieType.MeleeWeapon:
                        case WeenieType.Missile:
                            meleeWeapons.Add(weenie);
                            break;
                        case WeenieType.MissileLauncher:
                            missileWeapons.Add(weenie);
                            break;
                        case WeenieType.Ammunition:
                            ammo.Add(weenie);
                            break;
                    }
                }
            }

            if (creature.Biota.PropertiesGenerator != null)
            {
                foreach (var item in creature.Biota.PropertiesGenerator.Where(x => x.WhereCreate == RegenLocationType.Wield || x.WhereCreate == RegenLocationType.WieldTreasure))
                {
                    if (item.WeenieClassId == 0)
                        continue;

                    var weenie = DatabaseManager.World.GetCachedWeenie(item.WeenieClassId);
                    switch (weenie.WeenieType)
                    {
                        case WeenieType.MeleeWeapon:
                        case WeenieType.Missile:
                            meleeWeapons.Add(weenie);
                            break;
                        case WeenieType.MissileLauncher:
                            missileWeapons.Add(weenie);
                            break;
                        case WeenieType.Ammunition:
                            ammo.Add(weenie);
                            break;
                    }
                }
            }

            foreach (var weapon in meleeWeapons)
            {
                var damage = weapon.GetProperty(PropertyInt.Damage).Value;
                var attackType = (AttackType)(weapon.GetProperty(PropertyInt.AttackType) ?? 0);
                var weaponType = (WeaponType)(weapon.GetProperty(PropertyInt.WeaponType) ?? 0);
                var cleaveTargets = weapon.GetProperty(PropertyInt.Cleaving);

                if ((attackType & AttackType.TripleStrike) != 0)
                    damage *= 3;
                else if ((attackType & AttackType.DoubleStrike) != 0)
                    damage *= 2;

                if (weaponType == WeaponType.TwoHanded)
                    damage *= 2;

                if (cleaveTargets != null)
                    damage *= cleaveTargets.Value - 1;

                if (weapon.PropertiesSpellBook != null)
                {
                    var highestDamageRaising = 0;
                    var highestDamageRaisingRare = 0;
                    var highestMaxDamageRaising = 0;
                    foreach (var spellId in weapon.PropertiesSpellBook)
                    {
                        var spell = new Entity.Spell(spellId.Key);
                        if(spell.Category == SpellCategory.DamageRaising && spell.StatModVal > highestDamageRaising)
                            highestDamageRaising = (int)spell.StatModVal;
                        else if (spell.Category == SpellCategory.DamageRaisingRare && spell.StatModVal > highestDamageRaisingRare)
                            highestDamageRaisingRare = (int)spell.StatModVal;
                        else if (spell.Category == SpellCategory.MaxDamageRaising && spell.StatModVal > highestMaxDamageRaising)
                            highestMaxDamageRaising = (int)spell.StatModVal;
                    }

                    damage += highestDamageRaising + highestDamageRaisingRare + highestMaxDamageRaising;
                }

                damageList.Add(Tuple.Create(damage, weapon));
            }

            var highestMissileWeaponMod = 0.0;
            var highestMissileWeaponDamage = 0;
            if (missileWeapons.Count > 0)
            {
                var bestMissileWeapon = missileWeapons.OrderByDescending(w => w.GetProperty(PropertyFloat.DamageMod)).First();
                highestMissileWeaponMod = bestMissileWeapon.GetProperty(PropertyFloat.DamageMod).Value;
                highestMissileWeaponDamage = bestMissileWeapon.GetProperty(PropertyInt.Damage).Value;

                if (bestMissileWeapon.PropertiesSpellBook != null)
                {
                    var highestDamageRaising = 0;
                    var highestDamageRaisingRare = 0;
                    var highestMaxDamageRaising = 0;
                    foreach (var spellId in bestMissileWeapon.PropertiesSpellBook)
                    {
                        var spell = new Entity.Spell(spellId.Key);
                        if (spell.Category == SpellCategory.DamageRaising && spell.StatModVal > highestDamageRaising)
                            highestDamageRaising = (int)spell.StatModVal;
                        else if (spell.Category == SpellCategory.DamageRaisingRare && spell.StatModVal > highestDamageRaisingRare)
                            highestDamageRaisingRare = (int)spell.StatModVal;
                        else if (spell.Category == SpellCategory.MaxDamageRaising && spell.StatModVal > highestMaxDamageRaising)
                            highestMaxDamageRaising = (int)spell.StatModVal;
                    }

                    highestMissileWeaponDamage += highestDamageRaising + highestDamageRaisingRare + highestMaxDamageRaising;
                }

                var highestAmmoDamage = 0;
                if (ammo.Count > 0)
                    highestAmmoDamage = ammo.OrderByDescending(w => w.GetProperty(PropertyInt.Damage)).First().GetProperty(PropertyInt.Damage).Value;

                damageList.Add(Tuple.Create((int)(highestMissileWeaponMod * (highestMissileWeaponDamage + highestAmmoDamage)), bestMissileWeapon));
            }

            bool? weaponIgnoreMagicArmor = null;
            bool? weaponIgnoreMagicResist = null;
            double? weaponIgnoreArmor = null;
            double? weaponIgnoreShield = null;
            double? weaponResistanceModifier = null;
            double? weaponSlayerDamageBonus = null;

            var highestMeleeAndMissileDamage = 0;
            if (damageList.Count > 0)
            {
                damageList = damageList.OrderByDescending(w => w.Item1).ToList();
                foreach(var entry in damageList)
                {
                    if (highestMeleeAndMissileDamage == 0)
                        highestMeleeAndMissileDamage = entry.Item1;

                    var weapon = entry.Item2;
                    if (weapon != null)
                    {
                        if (weapon.GetProperty(PropertyBool.IgnoreMagicArmor) == true) // Bypasses Banes
                            weaponIgnoreMagicArmor = true;

                        if (weapon.GetProperty(PropertyBool.IgnoreMagicResist) == true) // Bypasses Protections
                            weaponIgnoreMagicResist = true;

                        var property = weapon.GetProperty(PropertyFloat.IgnoreArmor);
                        if (property.HasValue && property < 1.0 && (!weaponIgnoreArmor.HasValue || property < weaponIgnoreArmor.Value)) // Armor Cleaving: 0.0 = ignore 100% of armor AL.
                            weaponIgnoreArmor = property;

                        property = weapon.GetProperty(PropertyFloat.IgnoreShield);
                        if (property.HasValue && property > 0.0 && (!weaponIgnoreShield.HasValue || property > weaponIgnoreShield.Value)) // Shield Cleaving: 1.0 = Ignore 100% shield AL.
                            weaponIgnoreShield = property;

                        property = weapon.GetProperty(PropertyFloat.ResistanceModifier);
                        if (property.HasValue && property < 1.0 && (!weaponResistanceModifier.HasValue || property < weaponResistanceModifier.Value)) // Resistance Cleaving: 0.0 = Ignore 100% resists.
                            weaponResistanceModifier = property;

                        property = weapon.GetProperty(PropertyInt.SlayerCreatureType).HasValue && (CreatureType)weapon.GetProperty(PropertyInt.SlayerCreatureType) == CreatureType.Human ? weapon.GetProperty(PropertyFloat.SlayerDamageBonus) : null;
                        if (property.HasValue && property > 1.0 && (!weaponSlayerDamageBonus.HasValue || property > weaponSlayerDamageBonus.Value)) // Slayer: 2.0 = 200% damage against SlayerCreatureType.
                            weaponSlayerDamageBonus = property;
                    }
                }
            }

            var highestLevelProjectileSpellKnown = 0;
            if (creature.Biota.PropertiesSpellBook != null)
            {
                foreach (var item in creature.Biota.PropertiesSpellBook)
                {
                    var spell = new Entity.Spell(item.Key);
                    if (!spell.IsProjectile)
                        continue;

                    if (spell.Level > highestLevelProjectileSpellKnown)
                        highestLevelProjectileSpellKnown = (int)spell.Level;
                }
            }

            var attacksCauseBleeding = creature.AttacksCauseBleedChance.HasValue ? creature.AttacksCauseBleedChance > 0 : false;

            var extraLevels = 0d;

            var averageMaxDamage = 16 + (tier > 1 ? 4 : 0) + ((tier - 1) * 4);
            var damageLevelMod = (highestMeleeAndMissileDamage - averageMaxDamage) / 2;
            if (damageLevelMod < 0)
                damageLevelMod /= 2;

            // Very low level creatures with projectile spells or bleeding attacks are difficult enough that their below average physical damage does not matter.
            if (level < 10 && damageLevelMod < 0 && (highestLevelProjectileSpellKnown > 0) || attacksCauseBleeding)
                damageLevelMod = 0;

            extraLevels += damageLevelMod;

            if (hasSpecialAttack)
                extraLevels += 2.5f;

            extraLevels += highestLevelProjectileSpellKnown;

            if (creature.IgnoreMagicArmor || weaponIgnoreMagicArmor == true) // Bypasses Banes
                extraLevels += 2.5f;

            if (creature.IgnoreMagicResist || weaponIgnoreMagicResist == true) // Bypasses Protections
                extraLevels += 2.5f;

            var ignoreArmor = creature.IgnoreArmor.HasValue ? creature.IgnoreArmor : weaponIgnoreArmor;
            if (ignoreArmor.HasValue && ignoreArmor < 1.0) // Armor Cleaving: 0.0 = ignore 100% of armor AL.
                extraLevels += (1.0 - ignoreArmor.Value) * 5;

            var ignoreShield = creature.IgnoreShield.HasValue ? creature.IgnoreShield : weaponIgnoreShield;
            if (ignoreShield.HasValue) // Shield Cleaving: 1.0 = Ignore 100% shield AL.
                extraLevels += ignoreShield.Value * 5;

            var resistanceModifier = creature.ResistanceModifier.HasValue ? creature.ResistanceModifier : weaponResistanceModifier;
            if (resistanceModifier.HasValue && resistanceModifier < 1.0) // Resistance Cleaving: 0.0 = Ignore 100% resists.
                extraLevels += (1.0 - resistanceModifier.Value) * 5;

            var slayerDamageBonus = creature.SlayerCreatureType == CreatureType.Human && creature.SlayerDamageBonus.HasValue ? creature.SlayerDamageBonus : weaponSlayerDamageBonus;
            if (slayerDamageBonus.HasValue && slayerDamageBonus > 1.0f) // Slayer: 2.0 = 200% damage against SlayerCreatureType.
                extraLevels += (slayerDamageBonus.Value - 1.0f) / 0.5 * 5;

            level += (int)extraLevels;
            level = Math.Max(level, 1);

            return level;
        }

        [CommandHandler("export-creature-tiers", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExporCreatureTiers(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting creature tiers to reports/CreatureTiers.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}CreatureTiers.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("Name\tLevel\tTier\tCalculatedTier\tType\tWeenieClassId\tWeenieClassName\tDeathTreasureId");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var treasureDeath = DatabaseManager.World.GetAllTreasureDeath();

            foreach (var weenieTypeEntry in WeenieTypes)
            {
                if (weenieTypeEntry.Value != (int)WeenieType.Creature)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);

                var playerKillerStatus = (PlayerKillerStatus?)weenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                var npcLooksLikeObject = weenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                if (weenie.ClassId != 1 && playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                {
                    var name = weenie.GetProperty(PropertyString.Name);
                    var level = weenie.GetProperty(PropertyInt.Level);
                    var creatureType = (CreatureType?)weenie.GetProperty(PropertyInt.CreatureType);
                    var deathTreasure = weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;

                    if (level != 0 && deathTreasure != 0 && treasureDeath.ContainsKey(deathTreasure))
                    {
                        var creatureLootTier = treasureDeath[deathTreasure].Tier;

                        var calculatedTier = Creature.CalculateExtendedTier(level ?? 1);
                        fileWriter.WriteLine($"{name}\t{level}\t{creatureLootTier}\t{calculatedTier}\t{creatureType}\t{weenie.ClassId}\t{weenie.ClassName}\t{deathTreasure}");
                    }
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-creature-tier-report", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExporCreatureTierReport(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting creature tier discrepancies to reports/CreatureTierDiscrepancies.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}CreatureTierDiscrepancies.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("ClassId\tClassName\tLevel\tTier\tDeathTreasureId");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var treasureDeath = DatabaseManager.World.GetAllTreasureDeath();

            foreach (var weenieTypeEntry in WeenieTypes)
            {
                if (weenieTypeEntry.Value != (int)WeenieType.Creature)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);
                var deathTreasure = weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                var level = weenie.GetProperty(PropertyInt.Level) ?? 0;

                if (level != 0 && deathTreasure != 0 && treasureDeath.ContainsKey(deathTreasure))
                {
                    var creatureLootTier = treasureDeath[deathTreasure].Tier;

                    bool discrepant = false;
                    switch (creatureLootTier)
                    {
                        case 1:
                            if (level > 40)
                                discrepant = true;
                            break;
                        case 2:
                            if (level < 28 || level > 65)
                                discrepant = true;
                            break;
                        case 3:
                            if (level < 55 || level > 95)
                                discrepant = true;
                            break;
                        case 4:
                            if (level < 85 || level > 115)
                                discrepant = true;
                            break;
                        case 5:
                            if (level < 100 || level >= 210)
                                discrepant = true;
                            break;
                        case 6:
                            if (level < 120)
                                discrepant = true;
                            break;
                    }

                    if (discrepant)
                    {
                        fileWriter.WriteLine($"{weenie.ClassId}\t{weenie.ClassName}\t{level}\t{creatureLootTier}\t{deathTreasure}");
                        fileWriter.Flush();
                    }
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        [CommandHandler("export-chest-tier-report", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportChestTierReport(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting chest tier discrepancies to reports/ChestTierDiscrepancies.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}ChestTierDiscrepancies.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine("ClassId\tClassName\tTier\tHighestCreatureTier\tCount\tLandblock\tLocation");

            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var treasureDeath = DatabaseManager.World.GetAllTreasureDeath();

            foreach (var weenieTypeEntry in WeenieTypes)
            {
                if (weenieTypeEntry.Value != (int)WeenieType.Chest && weenieTypeEntry.Value != (int)WeenieType.Container && weenieTypeEntry.Value != (int)WeenieType.Generic)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);
                if (weenie.WeeniePropertiesGenerator == null || weenie.WeeniePropertiesGenerator.Count == 0)
                    continue;

                var chestLootTier = 0;
                foreach (var generatorEntry in weenie.WeeniePropertiesGenerator)
                {
                    if (((RegenLocationType)generatorEntry.WhereCreate).HasFlag(RegenLocationType.Treasure))
                    {
                        if (treasureDeath.ContainsKey(generatorEntry.WeenieClassId))
                        {
                            var generatorEntryLootTier = treasureDeath[generatorEntry.WeenieClassId].Tier;
                            if (chestLootTier < generatorEntryLootTier)
                                chestLootTier = generatorEntryLootTier;
                        }
                    }
                }

                if (chestLootTier == 0)
                    continue;

                var instances = DatabaseManager.World.GetLandblockInstancesByWcid(weenieTypeEntry.Key);

                foreach(var instance in instances)
                {
                    var landblockInstances = DatabaseManager.World.GetCachedInstancesByLandblock((ushort)instance.Landblock.Value);

                    Dictionary<int, int> creatureLootTiersCount = new Dictionary<int, int>();
                    foreach (var landblockEntry in landblockInstances)
                    {
                        if(WeenieTypes.TryGetValue(landblockEntry.WeenieClassId, out var instanceWeenieType))
                        {
                            uint deathTreasure = 0;

                            if (instanceWeenieType == (int)WeenieType.Generic)
                            {
                                var instanceWeenie = DatabaseManager.World.GetWeenie(landblockEntry.WeenieClassId);

                                if (instanceWeenie.WeeniePropertiesGenerator == null || instanceWeenie.WeeniePropertiesGenerator.Count == 0)
                                    continue;

                                var highestLootTier = 0;
                                uint highestLootTierDeathTreasure = 0;
                                foreach (var generatorEntry in instanceWeenie.WeeniePropertiesGenerator)
                                {
                                    if (!((RegenLocationType)generatorEntry.WhereCreate).HasFlag(RegenLocationType.Treasure))
                                    {
                                        if (WeenieTypes.TryGetValue(generatorEntry.WeenieClassId, out var generatorEntryWeenieType) && generatorEntryWeenieType == (int)WeenieType.Creature)
                                        {
                                            var generatorEntryWeenie = DatabaseManager.World.GetWeenie(generatorEntry.WeenieClassId);
                                            var generatorEntryDeathTreasure = generatorEntryWeenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;

                                            if (treasureDeath.ContainsKey(generatorEntryDeathTreasure))
                                            {
                                                var generatorEntryLootTier = treasureDeath[generatorEntryDeathTreasure].Tier;
                                                if (highestLootTier < generatorEntryLootTier)
                                                {
                                                    highestLootTier = generatorEntryLootTier;
                                                    highestLootTierDeathTreasure = generatorEntryDeathTreasure;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (highestLootTierDeathTreasure == 0)
                                    continue;

                                deathTreasure = highestLootTierDeathTreasure;
                            }
                            else if (instanceWeenieType == (int)WeenieType.Creature)
                            {
                                var instanceWeenie = DatabaseManager.World.GetWeenie(landblockEntry.WeenieClassId);
                                deathTreasure = instanceWeenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0;
                            }

                            if (deathTreasure != 0 && treasureDeath.ContainsKey(deathTreasure))
                            {
                                var creatureLootTier = treasureDeath[deathTreasure].Tier;
                                if (creatureLootTiersCount.ContainsKey(creatureLootTier))
                                    creatureLootTiersCount[creatureLootTier]++;
                                else
                                    creatureLootTiersCount.Add(creatureLootTier, 1);
                            }
                        }
                    }

                    var creatureCountThreshold = 5;
                    var highestCreatureLootTier = 0;
                    var highestCreatureLootTierCount = 0;
                    while (creatureCountThreshold > 0)
                    {
                        foreach (var entry in creatureLootTiersCount)
                        {
                            if (entry.Value < creatureCountThreshold)
                                continue;

                            if (highestCreatureLootTier < entry.Key)
                            {
                                highestCreatureLootTier = entry.Key;
                                highestCreatureLootTierCount = entry.Value;
                            }
                        }

                        if (highestCreatureLootTier == 0)
                            creatureCountThreshold--;
                        else
                            break;
                    }

                    if (chestLootTier > highestCreatureLootTier)
                    {
                        fileWriter.WriteLine($"{weenie.ClassId}\t{weenie.ClassName}\t{chestLootTier}\t{highestCreatureLootTier}\t{highestCreatureLootTierCount}\t0x{instance.ObjCellId:X8}\t@teleloc 0x{instance.ObjCellId:X8} [{instance.OriginX:F6} {instance.OriginY:F6} {instance.OriginZ:F6}] {instance.AnglesW:F6} {instance.AnglesX:F6} {instance.AnglesY:F6} {instance.AnglesZ:F6}");
                        fileWriter.Flush();
                    }
                }
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        private enum HeritageProfileDesc
        {
            A = 1,
            G = 2,
            S = 3,
            A75G25 = 4,
            A75S25 = 5,
            G75A25 = 6,
            G75S25 = 7,
            S75A25 = 8,
            S75G25 = 9,
            A50G50 = 10,
            A50S50 = 11,
            G50S50 = 12,
            A80G10S10 = 13,
            A10G80S10 = 14,
            A10G10S80 = 15,
            A50G25S25 = 16,
            A25G50S25 = 17,
            A25G25S50 = 18,
            A34G33S33 = 19,
            V = 20,
            A25G25S25V25 = 21,
        }

        [CommandHandler("export-treasuredeath-profiles", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleExportDeathTreasureProfiles(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, "Exporting deathTreasure profiles to reports/TreasureDeath.txt...");

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}reports{sep}");

            if (!folder.Exists)
                folder.Create();

            var filename = $"{folder.FullName}{sep}TreasureDeath.txt";

            var fileWriter = new StreamWriter(filename);

            fileWriter.WriteLine($"TreasureType\tTreasureTypeDesc\tTier\tLootQualityMod\tHeritageChance\tHeritageChanceDesc\t" +
                $"ItemChance\tItemMinAmount\tItemMaxAmount\tItemTreasureTypeSelectionChances\t" +
                $"MagicItemChance\tMagicItemMinAmount\tMagicItemMaxAmount\tMagicItemTreasureTypeSelectionChances\t" +
                $"MundaneItemChance\tMundaneItemMinAmount\tMundaneItemMaxAmount\tMundaneItemTypeSelectionChances");

            var treasureDeaths = DatabaseManager.World.GetAllTreasureDeath();
            foreach (var entryPair in treasureDeaths)
            {
                var entry = entryPair.Value;
                fileWriter.WriteLine($"{entry.TreasureType}\t{(TreasureDeathDesc)entry.TreasureType}\t{entry.Tier}\t{entry.LootQualityMod}\t{entry.UnknownChances}\t{(HeritageProfileDesc)entry.UnknownChances}\t" +
                    $"{entry.ItemChance}\t{entry.ItemMinAmount}\t{entry.ItemMaxAmount}\t{entry.ItemTreasureTypeSelectionChances}\t" +
                    $"{entry.MagicItemChance}\t{entry.MagicItemMinAmount}\t{entry.MagicItemMaxAmount}\t{entry.MagicItemTreasureTypeSelectionChances}\t" +
                    $"{entry.MundaneItemChance}\t{entry.MundaneItemMinAmount}\t{entry.MundaneItemMaxAmount}\t{entry.MundaneItemTypeSelectionChances}");
                fileWriter.Flush();
            }

            fileWriter.Close();
            CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        }

        //[CommandHandler("fixCreatureWeaponSkills", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        //public static void HandleFixCreatureWeaponSkills(Session session, params string[] parameters)
        //{
        //    CommandHandlerHelper.WriteOutputInfo(session, "Fixing creature weapon skills...");

        //    var contentFolder = VerifyContentFolder(session, false);

        //    var sep = Path.DirectorySeparatorChar;
        //    var sql_folder = new DirectoryInfo($"{contentFolder.FullName}{sep}sql{sep}weenies{sep}");

        //    if (!sql_folder.Exists)
        //        sql_folder.Create();

        //    var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();

        //    foreach (var weenieTypeEntry in WeenieTypes)
        //    {
        //        if (weenieTypeEntry.Value != (int)WeenieType.Creature)
        //            continue;

        //        var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);

        //        if (weenie.WeeniePropertiesSkill == null || weenie.WeeniePropertiesSkill.Count == 0)
        //            continue;

        //        WeeniePropertiesSkill highestExistingMeleeSkill = null;
        //        WeeniePropertiesSkill highestExistingMissileSkill = null;
        //        foreach (var skill in weenie.WeeniePropertiesSkill)
        //        {
        //            switch ((Skill)skill.Type)
        //            {
        //                case Skill.Axe:
        //                case Skill.Dagger:
        //                case Skill.Mace:
        //                case Skill.Spear:
        //                case Skill.Staff:
        //                case Skill.Sword:
        //                case Skill.UnarmedCombat:
        //                    skill.LastUsedTime = 0;
        //                    if (highestExistingMeleeSkill == null || skill.InitLevel > highestExistingMeleeSkill.InitLevel)
        //                            highestExistingMeleeSkill = new WeeniePropertiesSkill(skill);
        //                    break;
        //                case Skill.Bow:
        //                case Skill.Crossbow:
        //                case Skill.ThrownWeapon:
        //                    skill.LastUsedTime = 0;
        //                    if (highestExistingMissileSkill == null || skill.InitLevel > highestExistingMissileSkill.InitLevel)
        //                        highestExistingMissileSkill = new WeeniePropertiesSkill(skill);
        //                    break;
        //                default:
        //                    skill.LastUsedTime = 0;
        //                    break;
        //            }
        //        }

        //        if (highestExistingMeleeSkill == null && highestExistingMissileSkill == null)
        //            continue;

        //        if (highestExistingMeleeSkill == null)
        //        {
        //            highestExistingMeleeSkill = new WeeniePropertiesSkill();
        //            highestExistingMeleeSkill.SAC = (uint)SkillAdvancementClass.Trained;
        //        }

        //        if (highestExistingMissileSkill == null)
        //        {
        //            highestExistingMissileSkill = new WeeniePropertiesSkill();
        //            highestExistingMissileSkill.SAC = (uint)SkillAdvancementClass.Trained;
        //        }

        //        // Add all combat skills so we pick the best one correctly
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Axe) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Axe;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Dagger) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Dagger;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Mace) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Mace;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Spear) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Spear;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Staff) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Staff;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Sword) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.Sword;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.UnarmedCombat) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMeleeSkill);
        //            newSkill.Type = (ushort)Skill.UnarmedCombat;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Bow) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMissileSkill);
        //            newSkill.Type = (ushort)Skill.Bow;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.Crossbow) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMissileSkill);
        //            newSkill.Type = (ushort)Skill.Crossbow;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }
        //        if (weenie.WeeniePropertiesSkill.FirstOrDefault(x => x.Type == (ushort)Skill.ThrownWeapon) == null)
        //        {
        //            var newSkill = new WeeniePropertiesSkill(highestExistingMissileSkill);
        //            newSkill.Type = (ushort)Skill.ThrownWeapon;
        //            weenie.WeeniePropertiesSkill.Add(newSkill);
        //        }

        //        foreach (var skill in weenie.WeeniePropertiesSkill)
        //        {
        //            switch ((Skill)skill.Type)
        //            {
        //                case Skill.Axe:
        //                case Skill.Dagger:
        //                case Skill.Spear:
        //                case Skill.Sword:
        //                case Skill.Staff:
        //                case Skill.Mace:
        //                case Skill.UnarmedCombat:
        //                    skill.InitLevel = highestExistingMeleeSkill.InitLevel;
        //                    break;
        //                case Skill.Bow:
        //                case Skill.ThrownWeapon:
        //                case Skill.Crossbow:
        //                    skill.InitLevel = highestExistingMissileSkill.InitLevel;
        //                    break;
        //            }
        //        }

        //        var creature = WorldObjectFactory.CreateNewWorldObject(WeenieConverter.ConvertToEntityWeenie(weenie)) as Creature;
        //        var highestMeleeSkill = creature.GetHighestMeleeSkill();
        //        var highestMissileSkill = creature.GetHighestMissileSkill();
        //        creature.Destroy();

        //        var newSkills = new HashSet<WeeniePropertiesSkill>();
        //        foreach (var skill in weenie.WeeniePropertiesSkill)
        //        {
        //            switch ((Skill)skill.Type)
        //            {
        //                case Skill.Axe:
        //                case Skill.Dagger:
        //                case Skill.Spear:
        //                case Skill.Sword:
        //                case Skill.UnarmedCombat:
        //                    if ((Skill)skill.Type == highestMeleeSkill)
        //                    {
        //                        newSkills.Add(skill);
        //                        if (skill.InitLevel > highestExistingMeleeSkill.InitLevel)
        //                            highestExistingMeleeSkill = skill;
        //                    }
        //                    break;
        //                case Skill.Staff:
        //                    if ((Skill)skill.Type == highestMeleeSkill)
        //                    {
        //                        skill.Type = (ushort)Skill.Spear;
        //                        newSkills.Add(skill);
        //                        if (skill.InitLevel > highestExistingMeleeSkill.InitLevel)
        //                            highestExistingMeleeSkill = skill;
        //                    }
        //                    break;
        //                case Skill.Mace:
        //                    if ((Skill)skill.Type == highestMeleeSkill)
        //                    {
        //                        skill.Type = (ushort)Skill.Axe;
        //                        newSkills.Add(skill);
        //                        if (skill.InitLevel > highestExistingMeleeSkill.InitLevel)
        //                            highestExistingMeleeSkill = skill;
        //                    }
        //                    break;
        //                case Skill.Bow:
        //                case Skill.ThrownWeapon:
        //                    if ((Skill)skill.Type == highestMissileSkill)
        //                    {
        //                        newSkills.Add(skill);
        //                        if (skill.InitLevel > highestExistingMissileSkill.InitLevel)
        //                            highestExistingMissileSkill = skill;
        //                    }
        //                    break;
        //                case Skill.Crossbow:
        //                    if ((Skill)skill.Type == highestMeleeSkill)
        //                    {
        //                        skill.Type = (ushort)Skill.Bow;
        //                        newSkills.Add(skill);
        //                        if (skill.InitLevel > highestExistingMissileSkill.InitLevel)
        //                            highestExistingMissileSkill = skill;
        //                    }
        //                    break;
        //                default:
        //                    newSkills.Add(skill);
        //                    break;
        //            }
        //        }

        //        weenie.WeeniePropertiesSkill = newSkills;

        //        if (WeenieSQLWriter == null)
        //        {
        //            WeenieSQLWriter = new WeenieSQLWriter();
        //            WeenieSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
        //            WeenieSQLWriter.WeenieClassNames = DatabaseManager.World.GetAllWeenieClassNames();
        //            WeenieSQLWriter.WeenieLevels = DatabaseManager.World.GetAllWeenieLevels();
        //            WeenieSQLWriter.SpellNames = DatabaseManager.World.GetAllSpellNames();
        //            WeenieSQLWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
        //            WeenieSQLWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
        //            WeenieSQLWriter.PacketOpCodes = PacketOpCodeNames.Values;
        //        }

        //        var sql_filename = WeenieSQLWriter.GetDefaultFileName(weenie);

        //        var writer = new StreamWriter(sql_folder + sql_filename);

        //        try
        //        {
        //            WeenieSQLWriter.CreateSQLDELETEStatement(weenie, writer);
        //            writer.WriteLine();
        //            WeenieSQLWriter.CreateSQLINSERTStatement(weenie, writer);
        //            writer.Close();
        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine(e);
        //            CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {sql_folder}{sql_filename}");
        //            return;
        //        }

        //        CommandHandlerHelper.WriteOutputInfo(session, $"Exported {sql_folder}{sql_filename}");
        //    }

        //    CommandHandlerHelper.WriteOutputInfo(session, "Done.");
        //}


        [CommandHandler("ConvertGenToWeightBasedProbabilities", AccessLevel.Developer, CommandHandlerFlag.None, 1, "Converts a weenie's generator table to use weight based probabilities.", "<wcid or classname>")]
        public static void HandleConvertGenToWeightBasedProbabilities(Session session, params string[] parameters)
        {
            var param = parameters[0];

            Weenie weenie = null;

            if (uint.TryParse(param, out var wcid))
                weenie = DatabaseManager.World.GetWeenie(wcid);   // wcid
            else
                weenie = DatabaseManager.World.GetWeenie(param);  // classname

            if (weenie == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find weenie {param}", ChatMessageType.Broadcast);
                return;
            }

            if (weenie.GetProperty(PropertyBool.UsesWeightAsGeneratorProbabilities) ?? false)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{weenie.ClassName}({weenie.ClassId}) already uses weight based probabilities.", ChatMessageType.Broadcast);
                return;
            }

            if (weenie.WeeniePropertiesGenerator == null || weenie.WeeniePropertiesGenerator.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{weenie.ClassName}({weenie.ClassId}) has no generator table.", ChatMessageType.Broadcast);
                return;
            }

            var chancesTable = new Dictionary<float, int>();
            var weightsTable = new Dictionary<float, float>();

            var previousProbability = 0f;
            foreach (var entry in weenie.WeeniePropertiesGenerator)
            {
                if (entry.Probability == -1)
                    continue;

                var chance = (float)Math.Round(entry.Probability - previousProbability, 5);

                previousProbability = entry.Probability;

                if (!chancesTable.TryGetValue(chance, out var count))
                    chancesTable.Add(chance, 1);
                else
                    chancesTable[chance] = ++count;
            }

            if (chancesTable.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{weenie.ClassName}({weenie.ClassId}) has no entries that are not -1.", ChatMessageType.Broadcast);
                return;
            }

            var mostCommonChance = chancesTable.OrderByDescending(x => x.Value).First().Key;

            foreach (var entry in chancesTable)
            {
                var weight = (float)Math.Round(entry.Key / mostCommonChance, 5);
                weightsTable.Add(entry.Key, weight);
            }

            weenie.WeeniePropertiesBool.Add(new WeeniePropertiesBool { Type = (ushort)PropertyBool.UsesWeightAsGeneratorProbabilities, Value = true });

            previousProbability = 0f;
            foreach (var entry in weenie.WeeniePropertiesGenerator)
            {
                if (entry.Probability == -1)
                    continue;

                var chance = (float)Math.Round(entry.Probability - previousProbability, 5);
                previousProbability = entry.Probability;

                if (weightsTable.TryGetValue(chance, out var weight))
                    entry.Probability = weight;
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"{weenie.ClassName}({weenie.ClassId}): Error translating probability to weight: {chance}.", ChatMessageType.Broadcast);
                    return;
                }
            }

            //weenie.WeeniePropertiesGenerator = weenie.WeeniePropertiesGenerator.OrderByDescending(x => x.Probability == -1 ? float.MaxValue : x.Probability).ToList();

            ExportSQLWeenie(weenie, session);
        }

        [CommandHandler("updateCreatureLootTiers", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleUpdateCreatureLootTiers(Session session, params string[] parameters)
        {
            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var treasureDeath = DatabaseManager.World.GetAllTreasureDeath();

            foreach (var weenieTypeEntry in WeenieTypes)
            {
                if (weenieTypeEntry.Value != (int)WeenieType.Creature)
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);
                var deathTreasure = (TreasureDeathDesc)(weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0);
                var level = weenie.GetProperty(PropertyInt.Level) ?? 0;

                if (level != 0 && deathTreasure != 0)
                {
                    if(!treasureDeath.ContainsKey((uint)deathTreasure))
                    {
                        CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Invalid deathTreasure: {deathTreasure}.", ChatMessageType.Broadcast);
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(TreasureDeathDesc), deathTreasure))
                    {
                        CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Non-mapped deathTreasure: {deathTreasure}.", ChatMessageType.Broadcast);
                        continue;
                    }

                    var maxHealth = weenie.GetProperty(PropertyAttribute2nd.MaxHealth);
                    int calculatedTier;
                    if(maxHealth != null && maxHealth.CurrentLevel >= 1500)
                        calculatedTier = (int)Math.Ceiling(Creature.CalculateExtendedTier(level));
                    else
                        calculatedTier = (int)Math.Floor(Creature.CalculateExtendedTier(level));
                    var deathTreasureString = deathTreasure.ToString();
                    var calculatedDeathTreasureString = $"T{calculatedTier}_{deathTreasureString.Remove(0, 3)}";

                    if (deathTreasureString == calculatedDeathTreasureString)
                        continue;

                    if (Enum.TryParse(calculatedDeathTreasureString, out TreasureDeathDesc calculatedDeathTreasure))
                    {
                        var property = weenie.WeeniePropertiesDID.FirstOrDefault(x => x.Type == (uint)PropertyDataId.DeathTreasureType);
                        property.Value = (uint)calculatedDeathTreasure;

                        CommandHandlerHelper.WriteOutputInfo(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Updated deathTreasure: {deathTreasureString} -> {calculatedDeathTreasureString}.", ChatMessageType.Broadcast);

                        ExportSQLWeenie(weenie, session);
                    }
                    else
                        CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Invalid deathTreasure conversion: {deathTreasureString} -> {calculatedDeathTreasureString}.", ChatMessageType.Broadcast);
                }
            }
        }

        [CommandHandler("updateCreatureLevelsAndTiers", AccessLevel.Developer, CommandHandlerFlag.None, 0, "", "")]
        public static void HandleUpdateCreatureLevelsAndTiers(Session session, params string[] parameters)
        {
            var WeenieTypes = DatabaseManager.World.GetAllWeenieTypes();
            var treasureDeath = DatabaseManager.World.GetAllTreasureDeath();

            var ignoreList = new List<uint>()
            {
                (uint)WeenieClassName.human,
                (uint)WeenieClassName.admin,
                (uint)WeenieClassName.sentinel,
                (uint)WeenieClassName.chickenrooster,
                (uint)WeenieClassName.undeadhauntedmansionwandering,
                (uint)WeenieClassName.mysterioussarcophagus,
                (uint)WeenieClassName.rabbitdancingsteele,
                (uint)WeenieClassName.rabbitwhite,
                (uint)WeenieClassName.pillarice,
                (uint)WeenieClassName.pillarfire,
                (uint)WeenieClassName.pillaracid,
                (uint)WeenieClassName.pillarlightning,
            };

            foreach (var weenieTypeEntry in WeenieTypes)
            {
                if (weenieTypeEntry.Value != (int)WeenieType.Creature && weenieTypeEntry.Value != (int)WeenieType.Cow)
                    continue;

                if (ignoreList.Contains(weenieTypeEntry.Key))
                    continue;

                var weenie = DatabaseManager.World.GetWeenie(weenieTypeEntry.Key);
                var playerKillerStatus = (PlayerKillerStatus?)weenie.GetProperty(PropertyInt.PlayerKillerStatus) ?? PlayerKillerStatus.NPK;
                var npcLooksLikeObject = weenie.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;
                if (playerKillerStatus != PlayerKillerStatus.RubberGlue && playerKillerStatus != PlayerKillerStatus.Protected && !npcLooksLikeObject)
                {
                    var deathTreasure = (TreasureDeathDesc)(weenie.GetProperty(PropertyDataId.DeathTreasureType) ?? 0);
                    var level = 0;

                    var obj = WorldObjectFactory.CreateNewWorldObject(weenie.ClassId);
                    if (obj != null)
                    {
                        level = CalculateLevel(obj as Creature);
                        obj.Destroy();
                    }

                    if (level != 0)
                    {
                        var levelProperty = weenie.WeeniePropertiesInt.FirstOrDefault(x => x.Type == (uint)PropertyInt.Level);
                        levelProperty.Value = level;

                        if (deathTreasure != 0)
                        {
                            if (!treasureDeath.ContainsKey((uint)deathTreasure))
                                CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Invalid deathTreasure: {deathTreasure}.", ChatMessageType.Broadcast);
                            else if (!Enum.IsDefined(typeof(TreasureDeathDesc), deathTreasure))
                                CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Non-mapped deathTreasure: {deathTreasure}.", ChatMessageType.Broadcast);
                            else
                            {
                                var maxHealth = weenie.GetProperty(PropertyAttribute2nd.MaxHealth);
                                int calculatedTier;
                                if (maxHealth != null && maxHealth.CurrentLevel >= 1500)
                                    calculatedTier = (int)Math.Ceiling(Creature.CalculateExtendedTier(level));
                                else
                                    calculatedTier = (int)Math.Floor(Creature.CalculateExtendedTier(level));
                                var deathTreasureString = deathTreasure.ToString();
                                if (deathTreasureString.Length < 3)
                                    CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Cannot convert deathTreasure: {deathTreasureString}.", ChatMessageType.Broadcast);
                                else
                                {
                                    var calculatedDeathTreasureString = $"T{calculatedTier}_{deathTreasureString.Remove(0, 3)}";

                                    if (deathTreasureString != calculatedDeathTreasureString)
                                    {
                                        if (Enum.TryParse(calculatedDeathTreasureString, out TreasureDeathDesc calculatedDeathTreasure))
                                        {
                                            var property = weenie.WeeniePropertiesDID.FirstOrDefault(x => x.Type == (uint)PropertyDataId.DeathTreasureType);
                                            property.Value = (uint)calculatedDeathTreasure;
                                        }
                                        else
                                            CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}) - Level {level}: Invalid deathTreasure conversion: {deathTreasureString} -> {calculatedDeathTreasureString}.", ChatMessageType.Broadcast);
                                    }
                                }
                            }
                        }

                        ExportSQLWeenie(weenie, session);
                    }
                    else
                        CommandHandlerHelper.WriteOutputWarn(session, $"{weenie.ClassName}({weenie.ClassId}): Error calculating level.", ChatMessageType.Broadcast);
                }
            }
        }

        [CommandHandler("createHouse", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Create a custom house.", "<House Type> The type of house to be created. Valid values are bunk, room, cottage, villa and mansion.")]
        public static void HandleCreateHouse(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"This command is only available in the CustomDM ruleset.", ChatMessageType.Help));
                return;
            }

            var houseTypeParam = parameters[0].ToLower();
            var slumlordWeenie = WeenieClassName.undef;
            var houseWeenie = WeenieClassName.undef;

            switch (houseTypeParam)
            {
                case "bunk":
                    slumlordWeenie = WeenieClassName.slumlordCustomApartmentBunk;
                    houseWeenie = WeenieClassName.houseCustomApartmentBunk;
                    break;
                case "room":
                    slumlordWeenie = WeenieClassName.slumlordCustomApartmentRoom;
                    houseWeenie = WeenieClassName.houseCustomApartmentRoom;
                    break;
                case "cottage":
                    slumlordWeenie = WeenieClassName.slumlordCustomCottage;
                    houseWeenie = WeenieClassName.houseCustomCottage;
                    break;
                case "villa":
                    slumlordWeenie = WeenieClassName.slumlordCustomVilla;
                    houseWeenie = WeenieClassName.houseCustomVilla;
                    break;
                case "mansion":
                    slumlordWeenie = WeenieClassName.slumlordCustomMansion;
                    houseWeenie = WeenieClassName.houseCustomMansion;
                    break;
            }

            if (slumlordWeenie == WeenieClassName.undef)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Invalid house type! Valid values are bunk, room, cottage, villa and mansion.", ChatMessageType.Help));
                return;
            }

            if (HouseList.HasCustomHouse(session.Player.Location))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"There is already a custom house at this location.", ChatMessageType.Help));
                return;
            }

            var slumlordLoc = new Position(session.Player.Location);

            var houseLoc = new Position(session.Player.Location);
            houseLoc.Rotate(0, 0, -90);

            var bootspotLoc = new Position(session.Player.Location);
            bootspotLoc.Translate(1, 0, 0);

            var chest1Loc = new Position(session.Player.Location);
            chest1Loc.Translate(0, 1.5f, 0);
            chest1Loc.Rotate(0, 0, -90);

            var house = DatabaseManager.World.GetWeenie((uint)houseWeenie);
            var slumlord = DatabaseManager.World.GetWeenie((uint)slumlordWeenie);
            var bootspot = DatabaseManager.World.GetWeenie((uint)WeenieClassName.bootspot);
            var chest = DatabaseManager.World.GetWeenie((uint)WeenieClassName.storage);

            var guid = CreateLandblockInstance(session, house, houseLoc);

            if (guid != 0)
            {
                CreateLandblockInstance(session, slumlord, slumlordLoc, guid);
                CreateLandblockInstance(session, bootspot, bootspotLoc, guid);
                CreateLandblockInstance(session, chest, chest1Loc, guid);

                HouseManager.DoHandleHouseCreation(guid);
            }
        }

        [CommandHandler("houseInfo", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Shows information about the selected house.")]
        public static void HandleHouseInfo(Session session, params string[] parameters)
        {
            var wo = CommandHandlerHelper.GetQueryTarget(session);

            if(wo == null)
                return;

            if (wo.ParentLink != null)
                wo = wo.ParentLink;

            var guid = wo.Guid.Full;

            if (wo.WeenieType != WeenieType.House)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{wo.Name} (0x{guid:X8}) is not a house.");
                return;
            }

            var landblock = (ushort)wo.Location.Landblock;

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var instance = instances.FirstOrDefault(i => i.Guid == guid);

            if (instance == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find landblock_instance for {wo.WeenieClassId} - {wo.Name} (0x{guid:X8})");
                return;
            }

            if (instance.LandblockInstanceLink.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"The {wo.Name} (0x{guid:X8}) contains: nothing.");
                return;
            }

            var childObjects = new Dictionary<string, int>();
            var hookCount = 0;
            var storageCount = 0;
            foreach (var link in instance.LandblockInstanceLink)
            {
                var child = instances.FirstOrDefault(i => i.Guid == link.ChildGuid);
                if (child == null)
                {
                    CommandHandlerHelper.WriteOutputWarn(session, $"Couldn't find child guid for {link.ChildGuid:X8}");
                    continue;
                }

                var weenie = DatabaseManager.World.GetWeenie(child.WeenieClassId);

                if (!childObjects.TryGetValue(weenie.ClassName, out var value))
                    childObjects.Add(weenie.ClassName, 1);
                else
                    childObjects[weenie.ClassName] = value + 1;

                if (weenie.Type == (int)WeenieType.Hook)
                    hookCount++;
                else if (weenie.Type == (int)WeenieType.Storage)
                    storageCount++;
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"The {wo.Name} (0x{guid:X8}) contains:");
            foreach (var entry in childObjects)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"{entry.Key}: {entry.Value}");
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Total hook count: {hookCount}");
            CommandHandlerHelper.WriteOutputInfo(session, $"Total storage count: {storageCount}");
        }

        [CommandHandler("exportBuildingHouses", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Export the house(s) in the current building as a template so they can be imported later at a similar building.", "<Filename>")]
        public static void HandleExportBuildingHouses(Session session, params string[] parameters)
        {
            var exportName = parameters[0];

            ExportBuildingHouses(session, exportName);
        }

        public static void ExportBuildingHouses(Session session, string exportName, bool confirmed = false)
        {
            Physics.Common.BuildingObj building = null;
            uint outdoorCellId = 0;

            if (session.Player.Indoors)
            {
                outdoorCellId = session.Player.Location.GetOutdoorCell();

                foreach (var buildingEntry in session.Player.PhysicsObj.CurLandblock.Buildings)
                {
                    if (buildingEntry.CurCell.ID == outdoorCellId)
                    {
                        building = buildingEntry;
                        break;
                    }
                }
            }

            if (building == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must be in a building for this command to work.");
                return;
            }

            var buildingPos = building.Position.ACEPosition();
            var buildingRotation = buildingPos.GetYaw();
            var buildingType = building.ID;

            var landblock = (ushort)buildingPos.Landblock;

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            var houseObjects = new List<(uint wcid, bool isHouse, int houseId, Position pos)>();
            var houseCounter = 0;
            foreach (var instance in instances)
            {
                var instanceWeenie = DatabaseManager.World.GetWeenie(instance.WeenieClassId);
                if (instanceWeenie.Type != (int)WeenieType.House)
                    continue;

                var housePos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                if (housePos.GetOutdoorCell() != outdoorCellId)
                    continue;
                houseCounter++;

                housePos.RotateAroundPivot(buildingPos, -buildingRotation);

                var houseOffset = buildingPos.GetOffset(housePos);
                housePos = new Position(housePos.LandblockId.Raw, houseOffset.X, houseOffset.Y, houseOffset.Z, housePos.RotationX, housePos.RotationY, housePos.RotationZ, housePos.RotationW, true);

                houseObjects.Add((instance.WeenieClassId, true, houseCounter, housePos));

                foreach (var link in instance.LandblockInstanceLink)
                {
                    var child = instances.FirstOrDefault(i => i.Guid == link.ChildGuid);
                    if (child == null)
                    {
                        CommandHandlerHelper.WriteOutputWarn(session, $"Couldn't find child guid for {link.ChildGuid:X8}");
                        continue;
                    }

                    var weenie = DatabaseManager.World.GetWeenie(child.WeenieClassId);

                    var childPos = new Position(child.ObjCellId, child.OriginX, child.OriginY, child.OriginZ, child.AnglesX, child.AnglesY, child.AnglesZ, child.AnglesW);
                    childPos.RotateAroundPivot(buildingPos, -buildingRotation);

                    var childOffset = buildingPos.GetOffset(childPos);

                    houseObjects.Add((child.WeenieClassId, false, houseCounter, new Position(child.ObjCellId, childOffset.X, childOffset.Y, childOffset.Z, childPos.RotationX, childPos.RotationY, childPos.RotationZ, childPos.RotationW, true)));
                }
            }

            if(houseObjects.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "This building has no houses.");
                return;
            }

            var contentFolder = VerifyContentFolder(session, false);

            var sep = Path.DirectorySeparatorChar;
            var folder = new DirectoryInfo($"{contentFolder.FullName}{sep}housing templates");

            var filename = $"{folder.FullName}{sep}{exportName}.txt";

            try
            {
                if (!folder.Exists)
                    folder.Create();

                if (!confirmed && File.Exists(filename))
                {
                    var msg = $"{exportName} already exists, overwrite?";
                    if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => ExportBuildingHouses(session, exportName, true)), msg))
                        session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                    return;
                }

                var output = new List<string>();

                CommandHandlerHelper.WriteOutputInfo(session, $"Exporting housing template {exportName}...");

                output.Add(buildingType.ToString());

                foreach (var entry in houseObjects)
                {
                    output.Add($"{entry.wcid}\t{entry.isHouse}\t{entry.houseId}\t{entry.pos.ToLOCStringAlt()}");
                }

                File.WriteAllLines(filename, output);

                CommandHandlerHelper.WriteOutputInfo(session, $"Exported {filename}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to export {filename}");
                return;
            }
        }

        [CommandHandler("importBuildingHouses", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 1, "Create house(s) in the current building from a template.", "<Filename>")]
        public static void HandleImportBuildingHouse(Session session, params string[] parameters)
        {
            ImportBuildingHouse(session, parameters[0]);
        }

        public static void ImportBuildingHouse(Session session, string importName, bool confirmed = false)
        {
            Physics.Common.BuildingObj building = null;
            uint outdoorCellId = 0;

            if (session.Player.Indoors)
            {
                outdoorCellId = session.Player.Location.GetOutdoorCell();

                foreach (var buildingEntry in session.Player.PhysicsObj.CurLandblock.Buildings)
                {
                    if (buildingEntry.CurCell.ID == outdoorCellId)
                    {
                        building = buildingEntry;
                        break;
                    }
                }
            }

            if (building == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must be in a building for this command to work.");
                return;
            }

            var buildingPos = building.Position.ACEPosition();
            var buildingRotation = buildingPos.GetYaw();

            var landblock = (ushort)buildingPos.Landblock;
            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            if (!confirmed)
            {
                var houseCounter = 0;
                foreach (var instance in instances)
                {
                    var instanceWeenie = DatabaseManager.World.GetWeenie(instance.WeenieClassId);
                    if (instanceWeenie.Type != (int)WeenieType.House)
                        continue;

                    var housePos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                    if (housePos.GetOutdoorCell() != outdoorCellId)
                        continue;
                    houseCounter++;
                }

                if (houseCounter > 0)
                {
                    var msg = $"This building already contains {houseCounter} house{(houseCounter != 1 ? "s" : "")}. Import anyways?";
                    if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => ImportBuildingHouse(session, importName, true)), msg))
                        session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                    return;
                }
            }

            uint buildingType = 0;
            var houseObjects = new List<(uint wcid, bool isHouse, int houseId, Position pos)>();

            DirectoryInfo di = VerifyContentFolder(session);
            if (!di.Exists)
                return;

            var sep = Path.DirectorySeparatorChar;

            var folder = $"{di.FullName}{sep}housing templates{sep}";

            di = new DirectoryInfo(folder);

            try
            {
                var files = di.Exists ? di.GetFiles($"{importName}.txt") : null;

                if (files == null || files.Length == 0)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Couldn't find {folder}{importName}.txt");
                    return;
                }

                var file = files.First();

                CommandHandlerHelper.WriteOutputInfo(session, $"Importing housing template {importName}...");

                string[] input = File.ReadAllLines(file.FullName);
                foreach (string line in input)
                {
                    if(buildingType == 0)
                        buildingType = uint.Parse(line);
                    else
                    {
                        var splitLine = line.Split('\t');
                        var wcid = uint.Parse(splitLine[0]);
                        var isHouse = bool.Parse(splitLine[1]);
                        var houseId = int.Parse(splitLine[2]);
                        var splitPositionString = splitLine[3].Split(',');
                        var position = new Position(uint.Parse(splitPositionString[0].Replace("0x", ""), NumberStyles.HexNumber), float.Parse(splitPositionString[1]), float.Parse(splitPositionString[2]), float.Parse(splitPositionString[3]), float.Parse(splitPositionString[5]), float.Parse(splitPositionString[6]), float.Parse(splitPositionString[7]), float.Parse(splitPositionString[4]), true);

                        houseObjects.Add((wcid, isHouse, houseId, position));
                    }
                }

                CommandHandlerHelper.WriteOutputInfo(session, $"Imported {file.FullName}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                CommandHandlerHelper.WriteOutputInfo(session, $"Failed to import {folder}{importName}.txt");
                return;
            }

            if (buildingType == 0 || houseObjects.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "Invalid file.");
                return;
            }

            if (buildingType != building.ID)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Current building type(0x{building.ID:X8}) doesn't match imported building type(0x{buildingType:X8}).");
                return;
            }

            var instancesToCreate = new List<(uint guid, Weenie weenie, Position loc, uint parentGuid)>();
            var houseGuids = new List<uint>();

            var nextGuid = GetNextStaticGuid(landblock, instances);

            var bumpHeight = 0.05f;
            foreach (var entry in houseObjects)
            {
                if (!entry.Item2)
                    continue;
                else
                {
                    var position = new Position(building.LandblockID, entry.pos.PositionX + buildingPos.PositionX, entry.pos.PositionY + buildingPos.PositionY, entry.pos.PositionZ + buildingPos.PositionZ, entry.pos.RotationX, entry.pos.RotationY, entry.pos.RotationZ, entry.pos.RotationW);
                    position.RotateAroundPivot(buildingPos, buildingRotation);

                    // Bump height by a tad to make sure we get the correct cell, afterwards we can return to the original height.
                    position.PositionZ += bumpHeight;
                    position.LandblockId = new LandblockId(position.GetCell());
                    position.PositionZ -= bumpHeight;

                    var houseGuid = nextGuid++;
                    houseGuids.Add(houseGuid);

                    var weenie = DatabaseManager.World.GetWeenie(entry.wcid);
                    instancesToCreate.Add((houseGuid, weenie, position, 0));

                    var childList = houseObjects.Where(i => !i.isHouse && i.houseId == entry.houseId).ToList();

                    foreach (var childEntry in childList)
                    {
                        position = new Position(building.Position.ObjCellID, childEntry.pos.PositionX + buildingPos.PositionX, childEntry.pos.PositionY + buildingPos.PositionY, childEntry.pos.PositionZ + buildingPos.PositionZ, childEntry.pos.RotationX, childEntry.pos.RotationY, childEntry.pos.RotationZ, childEntry.pos.RotationW);
                        position.RotateAroundPivot(buildingPos, buildingRotation);

                        // Bump height by a tad to make sure we get the correct cell, afterwards we can return to the original height.
                        position.PositionZ += bumpHeight;
                        position.LandblockId = new LandblockId(position.GetCell());
                        position.PositionZ -= bumpHeight;

                        var childWeenie = DatabaseManager.World.GetWeenie(childEntry.wcid);
                        instancesToCreate.Add((nextGuid++, childWeenie, position, houseGuid));
                    }
                }
            }

            var result = CreateLandblockInstances(session, instancesToCreate, true);
            foreach (var entry in houseGuids)
            {
                HouseManager.DoHandleHouseCreation(entry);
            }
        }

        [CommandHandler("clearBuildingHouses", AccessLevel.Developer, CommandHandlerFlag.RequiresWorld, 0, "Removes all houses from the current building")]
        public static void HandleClearBuildingHouses(Session session, params string[] parameters)
        {
            ClearBuildingHouses(session);
        }

        public static void ClearBuildingHouses(Session session, bool confirmed = false)
        {
            Physics.Common.BuildingObj building = null;
            uint outdoorCellId = 0;

            if (session.Player.Indoors)
            {
                outdoorCellId = session.Player.Location.GetOutdoorCell();

                foreach (var buildingEntry in session.Player.PhysicsObj.CurLandblock.Buildings)
                {
                    if (buildingEntry.CurCell.ID == outdoorCellId)
                    {
                        building = buildingEntry;
                        break;
                    }
                }
            }

            if (building == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "You must be in a building for this command to work.");
                return;
            }

            var buildingPos = building.Position.ACEPosition();
            var buildingRotation = buildingPos.GetYaw();
            var buildingType = building.ID;

            var landblock = (ushort)buildingPos.Landblock;

            var instances = DatabaseManager.World.GetCachedInstancesByLandblock(landblock);

            List<LandblockInstance> houseObjectsInstances = new List<LandblockInstance>();
            foreach (var instance in instances)
            {
                var instanceWeenie = DatabaseManager.World.GetWeenie(instance.WeenieClassId);
                if (instanceWeenie.Type != (int)WeenieType.House)
                    continue;

                var housePos = new Position(instance.ObjCellId, instance.OriginX, instance.OriginY, instance.OriginZ, instance.AnglesX, instance.AnglesY, instance.AnglesZ, instance.AnglesW);
                if (housePos.GetOutdoorCell() != outdoorCellId)
                    continue;

                houseObjectsInstances.Add(instance);
            }

            if (houseObjectsInstances.Count == 0)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "This building has no houses.");
                return;
            }

            if (!confirmed)
            {
                var msg = $"This building contains {houseObjectsInstances.Count} house{(houseObjectsInstances.Count != 1 ? "s" : "")}. Proceed?";
                if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => ClearBuildingHouses(session, true)), msg))
                    session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            foreach(var instance in houseObjectsInstances)
            {
                if (instance.IsLinkChild)
                {
                    LandblockInstanceLink link = null;

                    foreach (var parent in instances.Where(i => i.LandblockInstanceLink.Count > 0))
                    {
                        link = parent.LandblockInstanceLink.FirstOrDefault(i => i.ChildGuid == instance.Guid);

                        if (link != null)
                        {
                            parent.LandblockInstanceLink.Remove(link);
                            break;
                        }
                    }
                    if (link == null)
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find parent link for child {instance.WeenieClassId} - (0x{instance.Guid:X8})", ChatMessageType.Broadcast));
                        return;
                    }
                }

                foreach (var link in instance.LandblockInstanceLink)
                    RemoveChild(session, link, instances);

                var wo = session.Player.CurrentLandblock.GetObject(instance.Guid);

                if (wo != null)
                {
                    wo.DeleteObject();

                    session.Network.EnqueueSend(new GameMessageSystemChat($"Removed {(instance.IsLinkChild ? "child " : "")}{wo.WeenieClassId} - {wo.Name} (0x{instance.Guid:X8}) from landblock instances", ChatMessageType.Broadcast));
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Couldn't find object for 0x{instance.Guid:X8}", ChatMessageType.Broadcast));

                instances.Remove(instance);
            }

            SyncInstances(session, landblock, instances);
        }
    }
}
