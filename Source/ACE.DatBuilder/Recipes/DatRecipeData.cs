using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.DatBuilder
{
    /// <summary>The data files of the recipes (Recipes/Data), embedded in this assembly: the long lists of edits that the record steps apply (*.json), the custom images (Portal) and the settlement list of the cell dat (Cell).</summary>
    public static class DatRecipeData
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>The id a recipe data file gives as hex text.</summary>
        public static uint Hex(string id)
        {
            return uint.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public static T Load<T>(string name)
        {
            using (var stream = Open(name))
                return JsonSerializer.Deserialize<T>(stream, Options);
        }

        /// <summary>A data file by its path under Recipes/Data, written with '/'.</summary>
        /// <exception cref="FileNotFoundException">No such file is embedded.</exception>
        public static Stream Open(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("Data/" + name) ?? throw new FileNotFoundException($"Recipe data file {name} is not embedded.");
        }

        /// <summary>The paths of the data files in a folder under Recipes/Data and its subfolders, in path order.</summary>
        public static IEnumerable<string> Files(string folder)
        {
            var prefix = "Data/" + folder + "/";

            return Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Select(n => n.Substring("Data/".Length)).OrderBy(n => n, StringComparer.Ordinal);
        }
    }
}
