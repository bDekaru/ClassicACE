using System;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        public const string CellDatName = "cell - 2016-12-23 - Iteration 982 - 100% - EoR.dat";
        public const string PortalDatName = "portal - 2017-01-29 - Iteration 2072 - EoR.dat";
        public const string LanguageDatName = "language - 2015-06-18 - Iteration 994 - EoR.dat";

        private static readonly Lazy<string> clientFolder = new Lazy<string>(MakeClientFolder);

        /// <summary>The path of one of the original dats; the test is inconclusive when the file is missing.</summary>
        public static string Dat(string name)
        {
            return Require(Path.Combine(Folder, name));
        }

        /// <summary>The path itself; the test is inconclusive when the file is missing.</summary>
        public static string Require(string path)
        {
            if (!File.Exists(path))
                Assert.Inconclusive($"Test file not found: {path}");

            return path;
        }

        /// <summary>A new path in the temp folder for a file or folder the test makes and removes itself.</summary>
        public static string TempPath(string prefix, string extension = "")
        {
            return Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}{extension}");
        }

        /// <summary>
        /// A folder with the original portal and language dats under the client's file names, for DatManager.Initialize (the cell dat is not needed).
        /// It is made once in the temp folder, with hard links to the originals (copies when the temp folder is on another drive).
        /// </summary>
        public static string ClientFolder()
        {
            return clientFolder.Value;
        }

        private static string MakeClientFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), "ace-datloader-tests");

            Directory.CreateDirectory(folder);

            Link(Dat(PortalDatName), Path.Combine(folder, "client_portal.dat"));
            Link(Dat(LanguageDatName), Path.Combine(folder, "client_local_English.dat"));

            return folder;
        }

        private static void Link(string original, string link)
        {
            if (File.Exists(link) && new FileInfo(link).Length == new FileInfo(original).Length)
                return;

            File.Delete(link);

            if (!CreateHardLink(link, original, IntPtr.Zero))
                File.Copy(original, link);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        /// <summary>The folder, or a folder that does not exist when the setting cannot be read, so the tests that need the dats end up inconclusive instead of every test class failing to initialize.</summary>
        private static string Read()
        {
            try
            {
                for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                {
                    var config = Path.Combine(directory.FullName, ConfigRelativePath);

                    if (!File.Exists(config))
                        continue;

                    return DatSourceSetting.Read(config) ?? throw new InvalidOperationException($"The {DatSourceSetting.Key} setting is empty in the shard database named in {config}.");
                }

                throw new InvalidOperationException($"{ConfigRelativePath} was not found above {AppContext.BaseDirectory}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test source dats unavailable: {ex.Message}");
                return Path.Combine(Path.GetTempPath(), "ace-datloader-tests-no-source-dats");
            }
        }
    }
}
