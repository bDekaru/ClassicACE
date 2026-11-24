using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>One changed string of a string table: the entry's id as hex text, the index in its strings and the new text. An edit with a Name instead of an Id adds a new entry whose id is the hash of that name (as the client looks up its option labels).</summary>
    public class DatStringEdit
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Index { get; set; }
        public string Text { get; set; }
    }

    /// <summary>Edits of a string table record (language dat, 0x23......), read from a recipe data file.</summary>
    public static class DatStringEdits
    {
        /// <exception cref="InvalidDataException">An edit names an entry or a string the table does not have.</exception>
        public static void Apply(StringTable table, List<DatStringEdit> edits)
        {
            var entries = table.StringTableData.ToDictionary(e => e.Id);

            foreach (var edit in edits)
            {
                if (edit.Name != null)
                {
                    var added = new StringTableData { Id = SpellTable.ComputeHash(edit.Name) };
                    added.Strings.Add(edit.Text);
                    LanguageRecords.InsertInBucketOrder(table, added);
                    continue;
                }

                var id = DatRecipeData.Hex(edit.Id);

                if (!entries.TryGetValue(id, out var entry))
                    throw new InvalidDataException($"String edit: the table has no entry {edit.Id}.");

                if (edit.Index < 0 || edit.Index >= entry.Strings.Count)
                    throw new InvalidDataException($"String edit {edit.Id}: the entry has no string {edit.Index}.");

                entry.Strings[edit.Index] = edit.Text;
            }
        }

        /// <summary>The edits that turn <paramref name="source"/> into <paramref name="target"/>, in the order of the target's entries (the tool that writes a data file from an existing record).</summary>
        /// <exception cref="InvalidDataException">The tables differ in anything but the text of their strings.</exception>
        public static List<DatStringEdit> Diff(StringTable source, StringTable target)
        {
            if (source.StringTableData.Count != target.StringTableData.Count)
                throw new InvalidDataException("String edits can only be made between tables with the same entries.");

            var edits = new List<DatStringEdit>();

            for (var i = 0; i < source.StringTableData.Count; i++)
            {
                var before = source.StringTableData[i];
                var after = target.StringTableData[i];

                if (before.Id != after.Id || before.Strings.Count != after.Strings.Count || before.Unknown != after.Unknown
                    || !before.VarNames.SequenceEqual(after.VarNames) || !before.Vars.SequenceEqual(after.Vars) || !before.Comments.SequenceEqual(after.Comments))
                    throw new InvalidDataException($"String edits: entry {after.Id:X8} differs in more than the text of its strings.");

                for (var s = 0; s < after.Strings.Count; s++)
                    if (before.Strings[s] != after.Strings[s])
                        edits.Add(new DatStringEdit { Id = after.Id.ToString("X8"), Index = s, Text = after.Strings[s] });
            }

            return edits;
        }
    }
}
