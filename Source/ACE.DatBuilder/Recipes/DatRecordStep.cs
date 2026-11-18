using System;
using System.Collections.Generic;
using System.IO;

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
            if (!_sources.TryGetValue(datFile, out var database))
            {
                var path = Path.Combine(SourceDats, datFile);

                if (!File.Exists(path))
                    throw new FileNotFoundException($"Source dat not found: {path}");

                database = new DatDatabase(path);
                _sources[datFile] = database;
            }

            return database.GetReaderForFile(id)?.Buffer ?? throw new InvalidDataException($"{datFile} has no record {id:X8}.");
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

        /// <summary>Writes the record with the step's date.</summary>
        public void Write(uint id, byte[] record, string what)
        {
            Writer.RecordDate = Step.Time;
            var outcome = Writer.Save(id, record);

            Log.Add($"{id:X8} {what}: {(outcome == DatSaveOutcome.Added ? "added" : "replaced")} ({record.Length:N0} bytes)");
        }
    }
}
