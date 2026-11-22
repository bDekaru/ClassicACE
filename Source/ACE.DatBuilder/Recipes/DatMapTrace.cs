using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using ACE.DatLoader;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Attributes the changes of a map edit run to the recipe source line that made them, to find edits that do nothing, are overridden or are reverted by a later one.
    /// </summary>
    public class DatMapTrace
    {
        private static readonly string[] RecipeFiles = { "DatMapRecipes.cs", "DatBuildRecipes.cs" };

        private Dictionary<uint, byte[]> _baseline;
        private readonly Dictionary<(string File, int Line), int> _changes = new Dictionary<(string, int), int>();
        private readonly Dictionary<uint, (string File, int Line)> _lastLine = new Dictionary<uint, (string, int)>();
        private readonly Dictionary<string, string> _paths = new Dictionary<string, string>();

        /// <summary>Takes the baseline (the store before any edit) and follows the store's changes.</summary>
        public void Attach(DatCellStore store)
        {
            _baseline = store.Records.ToDictionary(r => r.Key, r => r.Value);
            store.Changed += OnChanged;
        }

        private void OnChanged(uint id, byte[] data)
        {
            var frame = new StackTrace(true).GetFrames().FirstOrDefault(f => f.GetFileName() != null && RecipeFiles.Contains(Path.GetFileName(f.GetFileName())));

            if (frame == null)
                return;

            var key = (Path.GetFileName(frame.GetFileName()), frame.GetFileLineNumber());

            _paths[key.Item1] = frame.GetFileName();
            _changes[key] = _changes.GetValueOrDefault(key) + 1;
            _lastLine[id] = key;
        }

        /// <summary>
        /// One row per recipe line that changed a record: the changes it made, and the records of the finished store that differ from the baseline and whose last change was this line (0 owned means overridden, reverted or without effect).
        /// Source lines that call a map edit and changed nothing are listed after them.
        /// </summary>
        public string ToText(DatCellStore store)
        {
            var owned = new Dictionary<(string File, int Line), int>();

            foreach (var id in store.Records.Select(r => r.Key).Concat(_baseline.Keys).Distinct())
            {
                var inStore = store.TryGet(id, out var data);
                var inBaseline = _baseline.TryGetValue(id, out var baseline);

                if (inStore == inBaseline && (!inStore || data.SequenceEqual(baseline)))
                    continue;

                if (_lastLine.TryGetValue(id, out var line))
                    owned[line] = owned.GetValueOrDefault(line) + 1;
            }

            var text = $"Map edit trace: {_baseline.Count} baseline records, {store.Count} records at the end, {_changes.Values.Sum()} record changes by {_changes.Count} source lines\n"
                + "  line                         changes   net owned\n"
                + string.Concat(_changes.OrderBy(c => c.Key.File).ThenBy(c => c.Key.Line).Select(c => $"  {c.Key.File + ":" + c.Key.Line,-28} {c.Value,7}   {owned.GetValueOrDefault(c.Key),9}{(owned.GetValueOrDefault(c.Key) == 0 ? "   <- no net effect" : "")}\n"));

            foreach (var file in _paths.OrderBy(p => p.Key))
            {
                var lines = File.ReadAllLines(file.Value);

                var silent = Enumerable.Range(0, lines.Length)
                    .Where(i => lines[i].TrimStart().StartsWith("map.") && !_changes.ContainsKey((file.Key, i + 1)))
                    .Select(i => $"{file.Key}:{i + 1}");

                text += $"  map edit calls without any change in {file.Key} (also lines this recipe does not run): {string.Join(" ", silent)}\n";
            }

            return text;
        }
    }
}
