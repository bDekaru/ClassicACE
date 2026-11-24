using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.DatBuilder
{
    /// <summary>The data files of the recipes (Recipes/Data/*.json), embedded in this assembly: the long lists of edits that the record steps apply.</summary>
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
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Data/" + name) ?? throw new FileNotFoundException($"Recipe data file {name} is not embedded."))
                return JsonSerializer.Deserialize<T>(stream, Options);
        }
    }
}
