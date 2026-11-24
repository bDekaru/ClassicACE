using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Edits of the spell table (0x0E00000E), read from a recipe data file: a list of objects with the spell's "id" and the properties of <see cref="SpellBase"/> to change (property names in camel case, enums by name).
    /// </summary>
    public static class DatSpellEdits
    {
        private const string IdKey = "id";

        private static readonly PropertyInfo[] Properties = typeof(SpellBase).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.CanWrite).ToArray();

        private static string Key(PropertyInfo property)
        {
            return char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1);
        }

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

        /// <exception cref="InvalidDataException">An edit names a spell the table does not have, or a property the spell does not have.</exception>
        public static void Apply(SpellTable table, List<Dictionary<string, JsonElement>> edits)
        {
            foreach (var edit in edits)
            {
                var id = edit[IdKey].GetUInt32();

                if (!table.Spells.TryGetValue(id, out var spell))
                    throw new InvalidDataException($"Spell edit: the table has no spell {id}.");

                foreach (var field in edit.Where(f => f.Key != IdKey))
                {
                    var property = Properties.FirstOrDefault(p => Key(p) == field.Key) ?? throw new InvalidDataException($"Spell edit {id}: a spell has no property {field.Key}.");

                    property.SetValue(spell, field.Value.Deserialize(property.PropertyType, Options));
                }
            }
        }
    }
}
