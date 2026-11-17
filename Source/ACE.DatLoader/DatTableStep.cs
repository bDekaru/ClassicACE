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

        /// <summary>The spell tables; null when the database has none of them (the spell record is then kept as it is).</summary>
        public List<DatSpellRow> Spells { get; set; }
        public List<DatSpellSetSpellRow> SetSpells { get; set; }

        /// <summary>The vital formulas; null when the database has no vital table (the secondary attribute record is then kept as it is).</summary>
        public List<DatVitalRow> Vitals { get; set; }

        /// <summary>The character titles; null when the database has no title table (the title records of both dats are then kept as they are).</summary>
        public List<DatTitleRow> Titles { get; set; }

        /// <summary>The character generation tables; null when the database has none of them (the character generation record is then kept as it is).</summary>
        public DatChargenRows Chargen { get; set; }
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

        /// <summary>Null when the database has no spell tables.</summary>
        public string SpellHash { get; set; }

        /// <summary>Null when the database has no vital table.</summary>
        public string VitalHash { get; set; }

        /// <summary>Null when the database has no title table.</summary>
        public string TitleHash { get; set; }

        /// <summary>Null when the database has no character generation tables.</summary>
        public string ChargenHash { get; set; }

        public string ToText()
        {
            if (Skipped)
                return "  dat tables: none in the database, records kept";

            if (XpHash == null)
                return $"  dat tables: written {(Written.Count == 0 ? "none" : string.Join(", ", Written))}; unchanged {(Unchanged.Count == 0 ? "none" : string.Join(", ", Unchanged))}; title {TitleHash.Substring(0, 12)}";

            return $"  dat tables: written {(Written.Count == 0 ? "none" : string.Join(", ", Written))}; unchanged {(Unchanged.Count == 0 ? "none" : string.Join(", ", Unchanged))}; xp {XpHash.Substring(0, 12)}, skill {SkillHash.Substring(0, 12)}{(SpellHash == null ? ", spell tables not in the database" : ", spell " + SpellHash.Substring(0, 12))}{(VitalHash == null ? ", vital table not in the database" : ", vital " + VitalHash.Substring(0, 12))}{(TitleHash == null ? ", title table not in the database" : ", title " + TitleHash.Substring(0, 12))}{(ChargenHash == null ? ", chargen tables not in the database" : ", chargen " + ChargenHash.Substring(0, 12))}";
        }
    }

    /// <summary>
    /// Writes the XP table (0x0E000018), the vital table (0x0E000003), the skill table (0x0E000004) and the spell table (0x0E00000E) of a portal dat from the rows of the dat_* tables. A record whose bytes already equal the built ones is left alone,
    /// so the entry keeps its iteration and date; a changed one is written with the step's date.
    /// </summary>
    public static class DatTableStep
    {
        public const uint XpRecordId = 0x0E000018;
        public const uint ChargenRecordId = 0x0E000002;
        public const uint VitalRecordId = 0x0E000003;
        public const uint SkillRecordId = 0x0E000004;
        public const uint SpellRecordId = 0x0E00000E;
        public const uint ComponentRecordId = 0x0E00000F;
        public const uint TitleMapperRecordId = DatTables.TitleMapperId;
        public const uint TitleStringRecordId = 0x2300000E;

        /// <summary>
        /// The language dat's step: writes the title string table (0x2300000E) from the title rows. A database without a title table, or without any dat tables, leaves the record as it is.
        /// </summary>
        public static DatTablesReport ApplyLanguage(DatWriter writer, IDatTableSource source, uint date)
        {
            var rows = source.Read();
            var report = new DatTablesReport();

            if (rows?.Titles == null)
            {
                report.Skipped = true;
                return report;
            }

            report.TitleHash = DatTables.Hash(rows.Titles);

            var existing = writer.Read(TitleStringRecordId);

            if (existing == null)
                throw new InvalidDataException($"The language dat has no title string table (0x{TitleStringRecordId:X8}).");

            var baseTable = new StringTable();

            using (var reader = new BinaryReader(new MemoryStream(existing)))
                baseTable.Unpack(reader);

            Save(writer, TitleStringRecordId, "title strings", DatTables.ToTitleStringTable(rows.Titles, baseTable).Pack(), date, report);

            return report;
        }

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

            if (rows.Vitals != null)
            {
                report.VitalHash = DatTables.Hash(rows.Vitals);

                Save(writer, VitalRecordId, "vital table", DatTables.ToVitalTable(rows.Vitals).Pack(), date, report);
            }

            if (rows.Titles != null)
            {
                report.TitleHash = DatTables.Hash(rows.Titles);

                var mapperRecord = writer.Read(TitleMapperRecordId);

                if (mapperRecord == null)
                    throw new InvalidDataException($"The portal dat has no title enum mapper (0x{TitleMapperRecordId:X8}).");

                var baseMapper = new EnumMapper();

                using (var reader = new BinaryReader(new MemoryStream(mapperRecord)))
                    baseMapper.Unpack(reader);

                Save(writer, TitleMapperRecordId, "title mapper", DatTables.ToTitleMapper(rows.Titles, baseMapper).Pack(), date, report);
            }

            if (rows.Chargen != null)
            {
                report.ChargenHash = DatTables.Hash(rows.Chargen);

                var chargenRecord = writer.Read(ChargenRecordId);

                if (chargenRecord == null)
                    throw new InvalidDataException($"The portal dat has no character generation record (0x{ChargenRecordId:X8}).");

                var baseChargen = new CharGen();

                using (var reader = new BinaryReader(new MemoryStream(chargenRecord)))
                    baseChargen.Unpack(reader);

                Save(writer, ChargenRecordId, "character generation", DatTables.ToCharGen(rows.Chargen, baseChargen, rows.Skills.Select(s => s.Id)).Pack(), date, report);
            }

            if (rows.Spells != null)
            {
                report.SpellHash = DatTables.Hash(rows.Spells, rows.SetSpells);

                CheckComponents(writer, rows.Spells);

                SpellTable baseSpells = null;
                var existingSpells = writer.Read(SpellRecordId);

                if (existingSpells != null)
                {
                    baseSpells = new SpellTable();

                    using (var reader = new BinaryReader(new MemoryStream(existingSpells)))
                        baseSpells.Unpack(reader);
                }

                Save(writer, SpellRecordId, "spell table", DatTables.ToSpellTable(rows.Spells, rows.SetSpells, baseSpells).Pack(), date, report);
            }

            return report;
        }

        /// <exception cref="InvalidDataException">A spell formula names a component the dat's component table does not have.</exception>
        private static void CheckComponents(DatWriter writer, List<DatSpellRow> spells)
        {
            var record = writer.Read(ComponentRecordId);

            if (record == null)
                return;

            var components = new SpellComponentsTable();

            using (var reader = new BinaryReader(new MemoryStream(record)))
                components.Unpack(reader);

            foreach (var spell in spells)
                foreach (var component in spell.Components)
                    if (component != null && !components.SpellComponents.ContainsKey(component.Value))
                        throw new InvalidDataException($"{DatTables.SpellName}: spell {spell.Id} names component {component.Value}, which the dat's spell component table does not have.");
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
