using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACE.ClientPatcher
{
    public enum ClientVariant
    {
        CustomDM,
        Infiltration
    }

    /// <summary>One in-place byte change: the bytes the retail client has at the address (checked before writing) and the bytes that replace them.</summary>
    public sealed class PatchEdit
    {
        /// <summary>Virtual address (image base 0x400000), as hex text.</summary>
        public string Address { get; set; }

        /// <summary>Offset in the file instead of an address, for the PE headers, which no section maps.</summary>
        public string FileOffset { get; set; }
        public string Was { get; set; }
        public string Now { get; set; }

        /// <summary>The client variant this edit is for; null for every variant of the item.</summary>
        public ClientVariant? Variant { get; set; }

        /// <summary>What the change does and where, for the generated documentation.</summary>
        public string Note { get; set; }

        public uint Va => ParseHex(Address);

        public int Offset => (int)ParseHex(FileOffset);

        /// <summary>The number of bytes the edit changes.</summary>
        public int Length => Now.Length / 2;

        private static uint ParseHex(string text) => uint.Parse(text.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>One change of the client, in the words of the change list, with the clients that have it.</summary>
    public sealed class PatchItem
    {
        public string Change { get; set; }
        public List<ClientVariant> Variants { get; set; }
        public List<PatchEdit> Edits { get; set; }

        /// <summary>The edits of this item in the given client: none when the client does not have the item, and without the edits meant for the other client.</summary>
        public IEnumerable<PatchEdit> EditsFor(ClientVariant variant)
        {
            return Variants.Contains(variant) ? Edits.Where(edit => edit.Variant == null || edit.Variant == variant) : Enumerable.Empty<PatchEdit>();
        }
    }

    /// <summary>A group of related changes: one theme, one data file.</summary>
    public sealed class PatchEntry
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<PatchItem> Items { get; set; }

        /// <summary>The edits this entry makes in the given client, in file order.</summary>
        public IEnumerable<PatchEdit> EditsFor(ClientVariant variant)
        {
            return Items.SelectMany(item => item.EditsFor(variant));
        }
    }

    /// <summary>The patch entries (Patches/Data/*.json, embedded in this assembly), in the order they are applied.</summary>
    public static class PatchCatalogue
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static List<PatchEntry> Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var entries = new List<PatchEntry>();

            foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Data/") && n.EndsWith(".json")).OrderBy(n => n, StringComparer.Ordinal))
            {
                using (var stream = assembly.GetManifestResourceStream(name))
                    entries.Add(JsonSerializer.Deserialize<PatchEntry>(stream, Options));
            }

            return entries;
        }
    }
}
