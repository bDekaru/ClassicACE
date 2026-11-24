using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// One step that makes records from other records: it runs a list of calls against a <see cref="DatRecordContext"/> (read a record of a source dat or of the dat being built, change it through its codec, write it back)
    /// and carries the date the records it writes carry. Like a folder step, its date is chosen by hand when the step is added and never changed afterwards.
    /// </summary>
    public class DatRecordStep
    {
        public string Name { get; }
        public string Date { get; }
        public uint Time { get; }
        public Action<DatRecordContext> Run { get; }

        public DatRecordStep(string name, string date, Action<DatRecordContext> run)
        {
            Name = name;
            Date = date;
            Time = DatStepDate.Parse(date);
            Run = run;
        }
    }

    /// <summary>What a record step can read and write: the dat being built, the source dats folder, and the data files of the recipes. Every record goes through a ClassicACE file type, never through hand-made byte handling.</summary>
    public class DatRecordContext
    {
        private readonly Dictionary<string, DatDatabase> _sources = new Dictionary<string, DatDatabase>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The dat being built.</summary>
        public DatWriter Writer { get; }

        /// <summary>The folder with the original dats (read only).</summary>
        public string SourceDats { get; }

        /// <summary>The step being run.</summary>
        public DatRecordStep Step { get; }

        /// <summary>One line per record written.</summary>
        public List<string> Log { get; } = new List<string>();

        /// <summary>The ids of the records written.</summary>
        public HashSet<uint> Written { get; } = new HashSet<uint>();

        public DatRecordContext(DatWriter writer, string sourceDats, DatRecordStep step)
        {
            Writer = writer;
            SourceDats = sourceDats;
            Step = step;
        }

        /// <summary>The raw record of a source dat.</summary>
        /// <exception cref="InvalidDataException">The dat has no such record.</exception>
        public byte[] ReadSource(string datFile, uint id)
        {
            return Source(datFile).GetReaderForFile(id)?.Buffer ?? throw new InvalidDataException($"{datFile} has no record {id:X8}.");
        }

        private DatDatabase Source(string datFile)
        {
            if (!_sources.TryGetValue(datFile, out var database))
            {
                var path = Path.Combine(SourceDats, datFile);

                if (!File.Exists(path))
                    throw new ArgumentException($"Source dat not found: {path}");

                database = new DatDatabase(path, true);
                _sources[datFile] = database;
            }

            return database;
        }

        /// <summary>The ids of the EoR portal dat, which every record that is not a replacement has to stay clear of.</summary>
        private HashSet<uint> _eorIds;

        /// <exception cref="InvalidDataException">An id is in the EoR portal dat.</exception>
        private void CheckNew(IEnumerable<uint> ids, string what)
        {
            _eorIds ??= new HashSet<uint>(Source(DatBuildRecipes.PortalBaseDat).AllFiles.Keys);

            var taken = ids.Distinct().Where(_eorIds.Contains).OrderBy(id => id).ToList();

            if (taken.Count > 0)
                throw new InvalidDataException($"{what}: records that are not replacements take ids of the EoR portal dat ({string.Join(", ", taken.Select(id => id.ToString("X8")))}); pass replacement: true if they replace.");
        }

        /// <summary>The raw record of the dat being built, as it is at this point of the build.</summary>
        /// <exception cref="InvalidDataException">The dat has no such record.</exception>
        public byte[] ReadTarget(uint id)
        {
            return Writer.Read(id) ?? throw new InvalidDataException($"The dat being built has no record {id:X8}.");
        }

        public T ReadSource<T>(string datFile, uint id) where T : FileType, new()
        {
            return Unpack<T>(ReadSource(datFile, id));
        }

        public T ReadTarget<T>(uint id) where T : FileType, new()
        {
            return Unpack<T>(ReadTarget(id));
        }

        public static T Unpack<T>(byte[] record) where T : FileType, new()
        {
            var item = new T();

            using (var reader = new BinaryReader(new MemoryStream(record)))
                item.Unpack(reader);

            return item;
        }

        /// <summary>Removes the record from the dat being built. A record that is not there is an error, so a step cannot silently stop matching.</summary>
        public void Delete(uint id, string what)
        {
            if (!Writer.Delete(id))
                throw new InvalidDataException($"The dat being built has no record {id:X8} to remove.");

            Written.Remove(id);
            Log.Add($"{id:X8} {what}: removed");
        }

        /// <summary>Writes many records with the step's date, logging one line for all of them. Unless they are replacements, their ids must not be in the EoR portal dat.</summary>
        public void Write(IEnumerable<KeyValuePair<uint, byte[]>> records, string what, bool replacement = false)
        {
            if (!replacement)
            {
                records = records.ToList();
                CheckNew(records.Select(r => r.Key), what);
            }

            Writer.RecordDate = Step.Time;
            int added = 0, replaced = 0;

            foreach (var record in records)
            {
                if (Writer.Save(record.Key, record.Value) == DatSaveOutcome.Added)
                    added++;
                else
                    replaced++;

                Written.Add(record.Key);
            }

            Log.Add($"{what}: {added:N0} added, {replaced:N0} replaced");
        }

        /// <summary>Writes the record with the step's date. Unless it is a replacement, its id must not be in the EoR portal dat.</summary>
        public void Write(uint id, byte[] record, string what, bool replacement = false)
        {
            if (!replacement)
                CheckNew(new[] { id }, what);

            Writer.RecordDate = Step.Time;
            var outcome = Writer.Save(id, record);
            Written.Add(id);

            Log.Add($"{id:X8} {what}: {(outcome == DatSaveOutcome.Added ? "added" : "replaced")} ({record.Length:N0} bytes)");
        }
    }
}
