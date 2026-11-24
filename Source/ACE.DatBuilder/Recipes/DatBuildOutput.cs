using System;
using System.IO;

namespace ACE.DatBuilder
{
    /// <summary>What the portal and cell build runners share: where the output goes and the temporary file it is built in.</summary>
    public static class DatBuildOutput
    {
        /// <summary>The path of the dat to build, after checking that it is not inside the source folder and does not exist yet (unless <paramref name="force"/>).</summary>
        /// <exception cref="ArgumentException">The output would overwrite something it must not.</exception>
        public static string Prepare(string sourceDats, string outputRoot, string outputFolder, string outputFileName, bool force)
        {
            var folder = Path.GetFullPath(Path.Combine(outputRoot, outputFolder));
            var output = Path.Combine(folder, outputFileName);
            var sourceFull = Path.GetFullPath(sourceDats).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if ((folder + Path.DirectorySeparatorChar).StartsWith(sourceFull, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"The output folder {folder} is inside the source folder {sourceFull}; the source dats are never written.");

            if (File.Exists(output) && !force)
                throw new ArgumentException($"{output} already exists (use --force to replace it).");

            return output;
        }

        /// <summary>Builds the dat in a temporary file next to <paramref name="output"/> and moves it into place when <paramref name="build"/> returns, so a failed build leaves no partial dat.</summary>
        public static T BuildInTemp<T>(string output, Func<string, T> build)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var temp = output + ".building";

            try
            {
                var result = build(temp);

                File.Move(temp, output, true);

                return result;
            }
            catch
            {
                File.Delete(temp);

                throw;
            }
        }
    }
}
