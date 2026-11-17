using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader
{
    /// <summary>The rows of the dat_* tables of a ruleset's world database.</summary>
    public class DatTableRows
    {
        public DatXpRows Xp { get; set; }
        public List<DatSkillRow> Skills { get; set; }
    }

    /// <summary>Where the dat builder reads the dat_* tables from (the world database).</summary>
    public interface IDatTableSource
    {
        /// <summary>The rows, or null when the ruleset has no dat_* tables or they are all empty (the dat records are then kept as they are).</summary>
        /// <exception cref="InvalidDataException">Only some of the tables exist or have rows.</exception>
        DatTableRows Read();
    }

    public class DatTablesReport
    {
        /// <summary>Set when the database has no dat_* rows; the dat records were left as they are.</summary>
        public bool Skipped { get; set; }

        public List<string> Written { get; } = new List<string>();
        public List<string> Unchanged { get; } = new List<string>();

        public string XpHash { get; set; }
        public string SkillHash { get; set; }

        public string ToText()
        {
            if (Skipped)
                return "  dat tables: none in the database, records kept";

            return $"  dat tables: written {(Written.Count == 0 ? "none" : string.Join(", ", Written))}; unchanged {(Unchanged.Count == 0 ? "none" : string.Join(", ", Unchanged))}; xp {XpHash.Substring(0, 12)}, skill {SkillHash.Substring(0, 12)}";
        }
    }

    /// <summary>
    /// Writes the XP table (0x0E000018) and the skill table (0x0E000004) of a portal dat from the rows of the dat_* tables. A record whose bytes already equal the built ones is left alone,
    /// so the entry keeps its iteration and date; a changed one is written with the step's date.
    /// </summary>
    public static class DatTableStep
    {
        public const uint XpRecordId = 0x0E000018;
        public const uint SkillRecordId = 0x0E000004;

        public static DatTablesReport Apply(DatWriter writer, IDatTableSource source, uint date)
        {
            var rows = source.Read();
            var report = new DatTablesReport();

            if (rows == null)
            {
                report.Skipped = true;
                return report;
            }

            report.XpHash = DatTables.Hash(rows.Xp);
            report.SkillHash = DatTables.Hash(rows.Skills);

            Save(writer, XpRecordId, "XP table", DatTables.ToXpTable(rows.Xp).Pack(), date, report);

            SkillTable baseTable = null;
            var existing = writer.Read(SkillRecordId);

            if (existing != null)
            {
                baseTable = new SkillTable();

                using (var reader = new BinaryReader(new MemoryStream(existing)))
                    baseTable.Unpack(reader);
            }

            Save(writer, SkillRecordId, "skill table", DatTables.ToSkillTable(rows.Skills, baseTable).Pack(), date, report);

            return report;
        }

        private static void Save(DatWriter writer, uint id, string name, byte[] payload, uint date, DatTablesReport report)
        {
            var existing = writer.Read(id);

            if (existing != null && existing.SequenceEqual(payload))
            {
                report.Unchanged.Add(name);
                return;
            }

            writer.RecordDate = date;
            writer.Save(id, payload);
            report.Written.Add(name);
        }
    }
}
