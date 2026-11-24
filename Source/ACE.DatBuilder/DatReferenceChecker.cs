using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Checks the references of a built portal dat: ids that nothing provides (dangling), and which records the world, the cells and the engine records reach (reachability).
    /// Typed references come from <see cref="DatReferences"/>; the records without a walker that are not leaves (tables, layouts) are scanned for every word that is the id of a record, which can only make more records look reachable.
    /// </summary>
    public static class DatReferenceChecker
    {
        private const int Examples = 3;

        // Palettes, waves and environments hold no ids; cells and the other records reach them.
        private static bool IsLeaf(uint id) => (id >> 24) == 0x04 || (id >> 24) == 0x0A || (id >> 24) == 0x0D;

        private static bool IsDatId(uint id) => id >> 24 != 0 && id >> 24 < 0x80;

        /// <param name="highresPath">The high-resolution texture dat, which provides textures the portal dat lacks; null when there is none.</param>
        /// <param name="worldIds">The ids the world database names; null skips that root.</param>
        /// <param name="listed">Named id lists (the data files of the recipes); each entry that is not reachable is reported as unneeded.</param>
        /// <param name="languagePath">The language dat, which holds the UI layouts (and string tables, fonts) that refer to the portal's images; null skips the UI.</param>
        public static string Run(string portalPath, string cellPath, string highresPath, string languagePath, ISet<uint> worldIds, IReadOnlyDictionary<string, List<uint>> listed)
        {
            var portal = new DatDatabase(portalPath);
            var present = portal.AllFiles.Keys.ToHashSet();
            var provided = new HashSet<uint>(present);
            var language = languagePath != null ? new DatDatabase(languagePath) : null;

            if (highresPath != null)
                provided.UnionWith(new DatDatabase(highresPath).AllFiles.Keys);

            if (language != null)
                provided.UnionWith(language.AllFiles.Keys);

            var text = new StringBuilder();
            var unreadable = new List<string>();
            var dangling = new SortedDictionary<string, HashSet<uint>>();

            void Dangling(string source, uint id)
            {
                if (IsDatId(id) && !provided.Contains(id))
                {
                    var key = $"{source} -> type {id >> 24:X2}";

                    if (!dangling.TryGetValue(key, out var ids))
                        dangling[key] = ids = new HashSet<uint>();

                    ids.Add(id);
                }
            }

            var edges = new Dictionary<uint, List<uint>>();
            var roots = new HashSet<uint>();
            var scanned = 0;

            foreach (var id in present)
            {
                List<uint> refs;

                try
                {
                    if (DatReferences.HasWalker(id))
                        refs = DatReferences.OfPortalRecord(portal, id);
                    else if (IsLeaf(id))
                        continue;
                    else
                    {
                        refs = ScanWords(portal.GetReaderForFile(id).Buffer, provided);
                        roots.Add(id);
                        scanned++;
                    }
                }
                catch (Exception ex)
                {
                    unreadable.Add($"{id:X8} {ex.GetType().Name}");
                    continue;
                }

                edges[id] = refs;

                foreach (var reference in refs)
                    Dangling($"portal type {id >> 24:X2}", reference);
            }

            roots.Add(0x13000000);

            var cellRefs = 0;

            if (cellPath != null)
            {
                var cell = new DatDatabase(cellPath);
                var referenced = new HashSet<uint>();

                foreach (var id in cell.AllFiles.Keys.Where(k => k >> 16 != 0xFFFF))
                {
                    try
                    {
                        referenced.UnionWith(DatReferences.OfCellRecord(cell, id));
                    }
                    catch (Exception ex)
                    {
                        unreadable.Add($"cell {id:X8} {ex.GetType().Name}");
                    }
                }

                foreach (var reference in referenced)
                    Dangling("cell dat", reference);

                roots.UnionWith(referenced);
                cellRefs = referenced.Count;
            }

            var languageRecords = 0;

            if (language != null)
            {
                var master = portal.ReadFromDat<MasterProperty>(DatLayoutEdits.MasterPropertyId);

                foreach (var id in language.AllFiles.Keys)
                {
                    try
                    {
                        var buffer = language.GetReaderForFile(id).Buffer;
                        var isLayout = id >> 24 == 0x21;
                        var refs = isLayout ? DatReferences.OfLayout(DatLayoutEdits.Unpack(buffer, master)) : ScanWords(buffer, provided);

                        foreach (var reference in refs)
                            Dangling(isLayout ? "language dat layout" : "language dat record", reference);

                        roots.UnionWith(refs);
                        languageRecords++;
                    }
                    catch (Exception ex)
                    {
                        unreadable.Add($"language {id:X8} {ex.GetType().Name}");
                    }
                }
            }

            if (worldIds != null)
            {
                foreach (var id in worldIds)
                    Dangling("world database", id);

                roots.UnionWith(worldIds);
            }

            var reachable = new HashSet<uint>();
            var queue = new Queue<uint>(roots.Where(present.Contains));

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();

                if (!reachable.Add(id))
                    continue;

                if (edges.TryGetValue(id, out var refs))
                    foreach (var reference in refs.Where(present.Contains))
                        queue.Enqueue(reference);
            }

            text.AppendLine($"Reference check of {portalPath}: {present.Count:N0} records, {scanned:N0} table/layout records scanned by word, {cellRefs:N0} ids referenced by the cell dat, {languageRecords:N0} language dat records read, {worldIds?.Count ?? 0:N0} ids named by the world database, {reachable.Count:N0} records reachable from them and the engine records");
            text.AppendLine($"Dangling (not in the portal{(highresPath != null ? " or the high-res dat" : "")}): {dangling.Values.Sum(ids => ids.Count):N0} ids");

            foreach (var entry in dangling)
                text.AppendLine($"  {entry.Key}: {entry.Value.Count:N0} ids, e.g. {string.Join(" ", entry.Value.OrderBy(i => i).Take(Examples).Select(i => i.ToString("X8")))}");

            if (unreadable.Count > 0)
                text.AppendLine($"Unreadable records: {unreadable.Count:N0}, e.g. {string.Join("; ", unreadable.Take(Examples))}");

            text.AppendLine("Records not reachable, by type (tables and layouts are roots, leaves are reached through their users):");

            foreach (var group in present.Where(id => !reachable.Contains(id)).GroupBy(id => id >> 24).OrderBy(g => g.Key))
                text.AppendLine($"  type {group.Key:X2}: {group.Count():N0}, e.g. {string.Join(" ", group.OrderBy(i => i).Take(Examples).Select(i => i.ToString("X8")))}");

            foreach (var list in listed)
            {
                var notInPortal = list.Value.Where(id => !present.Contains(id)).ToList();
                var unneeded = list.Value.Where(id => present.Contains(id) && !reachable.Contains(id)).ToList();

                text.AppendLine($"{list.Key}: {list.Value.Count:N0} ids, {notInPortal.Count:N0} not in the portal, {unneeded.Count:N0} not reachable (unneeded)");

                if (unneeded.Count > 0)
                    text.AppendLine($"  unneeded: {string.Join(" ", unneeded.OrderBy(i => i).Select(i => i.ToString("X8")))}");
            }

            return text.ToString();
        }

        private static List<uint> ScanWords(byte[] record, ISet<uint> ids)
        {
            var refs = new List<uint>();

            for (var offset = 0; offset + 4 <= record.Length; offset++)
            {
                var word = BitConverter.ToUInt32(record, offset);

                if (IsDatId(word) && ids.Contains(word))
                    refs.Add(word);
            }

            return refs;
        }
    }
}
