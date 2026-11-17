using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader
{
    /// <summary>One row of dat_xp_attribute, dat_xp_vital, dat_xp_trained_skill or dat_xp_specialized_skill: the total XP spent to reach a rank.</summary>
    public class DatXpRow
    {
        public uint Rank { get; set; }
        public uint TotalXp { get; set; }
    }

    /// <summary>The rows of the five XP tables.</summary>
    public class DatXpRows
    {
        public List<DatXpRow> Attribute { get; set; } = new List<DatXpRow>();
        public List<DatXpRow> Vital { get; set; } = new List<DatXpRow>();
        public List<DatXpRow> TrainedSkill { get; set; } = new List<DatXpRow>();
        public List<DatXpRow> SpecializedSkill { get; set; } = new List<DatXpRow>();
        public List<DatXpLevelRow> Levels { get; set; } = new List<DatXpLevelRow>();
    }

    /// <summary>One row of dat_xp_level.</summary>
    public class DatXpLevelRow
    {
        public uint Level { get; set; }
        public ulong TotalXp { get; set; }
        public uint SkillCredits { get; set; }
    }

    /// <summary>One row of dat_skill.</summary>
    public class DatSkillRow
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public uint IconId { get; set; }
        public int TrainedCost { get; set; }
        public int SpecializedCost { get; set; }
        public uint Category { get; set; }
        public uint ChargenUse { get; set; }
        public uint MinLevel { get; set; }
        public uint FormulaW { get; set; }
        public uint FormulaX { get; set; }
        public uint FormulaY { get; set; }
        public uint FormulaZ { get; set; }
        public uint FormulaAttr1 { get; set; }
        public uint FormulaAttr2 { get; set; }
        public double UpperBound { get; set; }
        public double LowerBound { get; set; }
        public double LearnMod { get; set; }
    }

    /// <summary>
    /// The database form of the XP and skill tables of the portal dat (tables dat_xp_attribute, dat_xp_vital, dat_xp_trained_skill, dat_xp_specialized_skill, dat_xp_level and dat_skill) and the conversions to and from the dat records.
    /// The database is the source: a table built from the rows replaces the dat record's values, and anything the rows do not model (the bucket size, the order of existing entries) is kept from the base record.
    /// </summary>
    public static class DatTables
    {
        public const string XpAttributeTable = "dat_xp_attribute";
        public const string XpVitalTable = "dat_xp_vital";
        public const string XpTrainedSkillTable = "dat_xp_trained_skill";
        public const string XpSpecializedSkillTable = "dat_xp_specialized_skill";
        public const string XpLevelTable = "dat_xp_level";
        public const string SkillName = "dat_skill";

        // ------------------------------------------------------------------ XP table

        public static DatXpRows ToRows(XpTable table)
        {
            var rows = new DatXpRows
            {
                Attribute = RankRows(table.AttributeXpList),
                Vital = RankRows(table.VitalXpList),
                TrainedSkill = RankRows(table.TrainedSkillXpList),
                SpecializedSkill = RankRows(table.SpecializedSkillXpList)
            };

            for (var level = 0; level < table.CharacterLevelXPList.Count; level++)
                rows.Levels.Add(new DatXpLevelRow { Level = (uint)level, TotalXp = table.CharacterLevelXPList[level], SkillCredits = table.CharacterLevelSkillCreditList[level] });

            return rows;
        }

        private static List<DatXpRow> RankRows(List<uint> list)
        {
            return list.Select((xp, rank) => new DatXpRow { Rank = (uint)rank, TotalXp = xp }).ToList();
        }

        /// <exception cref="InvalidDataException">A table is empty or has a rank or level missing.</exception>
        public static XpTable ToXpTable(DatXpRows rows)
        {
            var table = new XpTable();

            table.AttributeXpList.AddRange(RankList(rows.Attribute, XpAttributeTable));
            table.VitalXpList.AddRange(RankList(rows.Vital, XpVitalTable));
            table.TrainedSkillXpList.AddRange(RankList(rows.TrainedSkill, XpTrainedSkillTable));
            table.SpecializedSkillXpList.AddRange(RankList(rows.SpecializedSkill, XpSpecializedSkillTable));

            var levels = rows.Levels.OrderBy(l => l.Level).ToList();

            for (var i = 0; i < levels.Count; i++)
            {
                if (levels[i].Level != i)
                    throw new InvalidDataException($"{XpLevelTable}: level {i} is missing (next row is level {levels[i].Level}).");

                table.CharacterLevelXPList.Add(levels[i].TotalXp);
                table.CharacterLevelSkillCreditList.Add(levels[i].SkillCredits);
            }

            return table;
        }

        private static List<uint> RankList(List<DatXpRow> rows, string tableName)
        {
            if (rows.Count == 0)
                throw new InvalidDataException($"{tableName} has no rows.");

            var ordered = rows.OrderBy(r => r.Rank).ToList();

            for (var i = 0; i < ordered.Count; i++)
                if (ordered[i].Rank != i)
                    throw new InvalidDataException($"{tableName}: rank {i} is missing (next row is rank {ordered[i].Rank}).");

            return ordered.Select(r => r.TotalXp).ToList();
        }

        // ------------------------------------------------------------------ skill table

        public static List<DatSkillRow> ToRows(SkillTable table)
        {
            return table.SkillBaseHash.OrderBy(s => s.Key).Select(s => new DatSkillRow
            {
                Id = s.Key,
                Name = s.Value.Name,
                Description = s.Value.Description,
                IconId = s.Value.IconId,
                TrainedCost = s.Value.TrainedCost,
                SpecializedCost = s.Value.SpecializedCost,
                Category = s.Value.Category,
                ChargenUse = s.Value.ChargenUse,
                MinLevel = s.Value.MinLevel,
                FormulaW = s.Value.Formula.W,
                FormulaX = s.Value.Formula.X,
                FormulaY = s.Value.Formula.Y,
                FormulaZ = s.Value.Formula.Z,
                FormulaAttr1 = s.Value.Formula.Attr1,
                FormulaAttr2 = s.Value.Formula.Attr2,
                UpperBound = s.Value.UpperBound,
                LowerBound = s.Value.LowerBound,
                LearnMod = s.Value.LearnMod
            }).ToList();
        }

        /// <summary>
        /// Builds the skill table from the rows. Skills the base record has keep their position and the bucket size is kept; new skills follow in id order; skills without a row are dropped.
        /// </summary>
        public static SkillTable ToSkillTable(IEnumerable<DatSkillRow> rows, SkillTable baseTable = null)
        {
            var byId = rows.ToDictionary(r => r.Id);
            var table = new SkillTable();

            if (baseTable != null)
                table.BucketSize = baseTable.BucketSize;

            var order = new List<uint>();

            if (baseTable != null)
                order.AddRange(baseTable.SkillBaseHash.Keys.Where(byId.ContainsKey));

            order.AddRange(byId.Keys.Where(id => !order.Contains(id)).OrderBy(id => id));

            foreach (var id in order)
            {
                var row = byId[id];

                table.SkillBaseHash[id] = new SkillBase
                {
                    Name = row.Name,
                    Description = row.Description,
                    IconId = row.IconId,
                    TrainedCost = row.TrainedCost,
                    SpecializedCost = row.SpecializedCost,
                    Category = row.Category,
                    ChargenUse = row.ChargenUse,
                    MinLevel = row.MinLevel,
                    Formula = new SkillFormula { W = row.FormulaW, X = row.FormulaX, Y = row.FormulaY, Z = row.FormulaZ, Attr1 = row.FormulaAttr1, Attr2 = row.FormulaAttr2 },
                    UpperBound = row.UpperBound,
                    LowerBound = row.LowerBound,
                    LearnMod = row.LearnMod
                };
            }

            return table;
        }

        // ------------------------------------------------------------------ hash

        /// <summary>
        /// Hash of the table content, the same whether the rows come from the database or from a decoded dat record
        /// (the ordering inside the record and the bucket size are not part of it).
        /// </summary>
        public static string Hash(DatXpRows rows)
        {
            var text = new StringBuilder();

            AppendRanks(text, "A", rows.Attribute);
            AppendRanks(text, "V", rows.Vital);
            AppendRanks(text, "T", rows.TrainedSkill);
            AppendRanks(text, "P", rows.SpecializedSkill);

            foreach (var l in rows.Levels.OrderBy(l => l.Level))
                text.Append(Inv($"L|{l.Level}|{l.TotalXp}|{l.SkillCredits}\n"));

            return Sha256(text.ToString());
        }

        private static void AppendRanks(StringBuilder text, string kind, IEnumerable<DatXpRow> rows)
        {
            foreach (var r in rows.OrderBy(r => r.Rank))
                text.Append(Inv($"{kind}|{r.Rank}|{r.TotalXp}\n"));
        }

        public static string Hash(IEnumerable<DatSkillRow> skills)
        {
            var text = new StringBuilder();

            foreach (var s in skills.OrderBy(s => s.Id))
                text.Append(Inv($"S|{s.Id}|{s.Name}|{s.Description}|{s.IconId}|{s.TrainedCost}|{s.SpecializedCost}|{s.Category}|{s.ChargenUse}|{s.MinLevel}|{s.FormulaW}|{s.FormulaX}|{s.FormulaY}|{s.FormulaZ}|{s.FormulaAttr1}|{s.FormulaAttr2}|{s.UpperBound:R}|{s.LowerBound:R}|{s.LearnMod:R}\n"));

            return Sha256(text.ToString());
        }

        private static string Inv(FormattableString value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }

        // ------------------------------------------------------------------ SQL

        /// <summary>The CREATE TABLE statements of the six tables.</summary>
        public const string CreateScript =
@"-- Portal dat tables (XP and skills): the source the dat builder writes the XP table (0x0E000018) and the skill table (0x0E000004) from.
-- Every column is client-visible: changing a value needs a dat build, a server restart and a dat release.

CREATE TABLE IF NOT EXISTS `dat_xp_attribute` (
  `rank` int(10) unsigned NOT NULL COMMENT 'Rank: the number of times the attribute has been raised; rank 0 is not raised',
  `total_Xp` int(10) unsigned NOT NULL COMMENT 'Total XP spent on an attribute to reach this rank (the cost of the next point is the difference to the previous rank)',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat XP table: attribute ranks';

CREATE TABLE IF NOT EXISTS `dat_xp_vital` (
  `rank` int(10) unsigned NOT NULL COMMENT 'Rank: the number of times the vital has been raised; rank 0 is not raised',
  `total_Xp` int(10) unsigned NOT NULL COMMENT 'Total XP spent on a vital to reach this rank',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat XP table: vital ranks';

CREATE TABLE IF NOT EXISTS `dat_xp_trained_skill` (
  `rank` int(10) unsigned NOT NULL COMMENT 'Rank: the number of times the trained skill has been raised; rank 0 is not raised',
  `total_Xp` int(10) unsigned NOT NULL COMMENT 'Total XP spent on a trained skill to reach this rank',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat XP table: trained skill ranks';

CREATE TABLE IF NOT EXISTS `dat_xp_specialized_skill` (
  `rank` int(10) unsigned NOT NULL COMMENT 'Rank: the number of times the specialized skill has been raised; rank 0 is not raised',
  `total_Xp` int(10) unsigned NOT NULL COMMENT 'Total XP spent on a specialized skill to reach this rank',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat XP table: specialized skill ranks';

CREATE TABLE IF NOT EXISTS `dat_xp_level` (
  `level` int(10) unsigned NOT NULL COMMENT 'Character level',
  `total_Xp` bigint(20) unsigned NOT NULL COMMENT 'Total XP needed to reach this level',
  `skill_Credits` int(10) unsigned NOT NULL COMMENT 'Skill credits granted at this level',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat XP table: character levels';

CREATE TABLE IF NOT EXISTS `dat_skill` (
  `id` int(10) unsigned NOT NULL COMMENT 'Unique Id of this Skill',
  `name` text NOT NULL,
  `description` text NOT NULL,
  `icon_Id` int(10) unsigned NOT NULL,
  `trained_Cost` int(11) NOT NULL COMMENT 'Skill credits to train',
  `specialized_Cost` int(11) NOT NULL COMMENT 'Total skill credits to specialize, including the trained cost',
  `category` int(10) unsigned NOT NULL COMMENT '1 = combat, 2 = other, 3 = magic',
  `chargen_Use` int(10) unsigned NOT NULL,
  `min_Level` int(10) unsigned NOT NULL COMMENT '1 = usable untrained, 2 = trained or specialized needed',
  `formula_W` int(10) unsigned NOT NULL COMMENT 'Skill formula: (attr1 * x + attr2 * y) / z + w',
  `formula_X` int(10) unsigned NOT NULL,
  `formula_Y` int(10) unsigned NOT NULL,
  `formula_Z` int(10) unsigned NOT NULL,
  `formula_Attr1` int(10) unsigned NOT NULL,
  `formula_Attr2` int(10) unsigned NOT NULL,
  `upper_Bound` double NOT NULL,
  `lower_Bound` double NOT NULL,
  `learn_Mod` double NOT NULL,
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat skill table';
";

        /// <summary>INSERT statements that load the rows into the six tables, after emptying them.</summary>
        public static string InsertScript(DatXpRows xp, IEnumerable<DatSkillRow> skills)
        {
            var sql = new StringBuilder();

            foreach (var name in new[] { XpAttributeTable, XpVitalTable, XpTrainedSkillTable, XpSpecializedSkillTable, XpLevelTable, SkillName })
                sql.AppendLine($"DELETE FROM `{name}`;");

            AppendRankInserts(sql, XpAttributeTable, xp.Attribute);
            AppendRankInserts(sql, XpVitalTable, xp.Vital);
            AppendRankInserts(sql, XpTrainedSkillTable, xp.TrainedSkill);
            AppendRankInserts(sql, XpSpecializedSkillTable, xp.SpecializedSkill);

            foreach (var l in xp.Levels.OrderBy(l => l.Level))
                sql.AppendLine(Inv($"INSERT INTO `{XpLevelTable}` (`level`, `total_Xp`, `skill_Credits`) VALUES ({l.Level}, {l.TotalXp}, {l.SkillCredits});"));

            foreach (var s in skills.OrderBy(s => s.Id))
                sql.AppendLine(Inv($"INSERT INTO `{SkillName}` (`id`, `name`, `description`, `icon_Id`, `trained_Cost`, `specialized_Cost`, `category`, `chargen_Use`, `min_Level`, `formula_W`, `formula_X`, `formula_Y`, `formula_Z`, `formula_Attr1`, `formula_Attr2`, `upper_Bound`, `lower_Bound`, `learn_Mod`) VALUES ({s.Id}, {Quote(s.Name)}, {Quote(s.Description)}, {s.IconId}, {s.TrainedCost}, {s.SpecializedCost}, {s.Category}, {s.ChargenUse}, {s.MinLevel}, {s.FormulaW}, {s.FormulaX}, {s.FormulaY}, {s.FormulaZ}, {s.FormulaAttr1}, {s.FormulaAttr2}, {s.UpperBound:R}, {s.LowerBound:R}, {s.LearnMod:R});"));

            return sql.ToString();
        }

        private static void AppendRankInserts(StringBuilder sql, string table, IEnumerable<DatXpRow> rows)
        {
            foreach (var r in rows.OrderBy(r => r.Rank))
                sql.AppendLine(Inv($"INSERT INTO `{table}` (`rank`, `total_Xp`) VALUES ({r.Rank}, {r.TotalXp});"));
        }

        private static string Quote(string value)
        {
            var text = new StringBuilder("'");

            foreach (var c in value ?? "")
                switch (c)
                {
                    case '\'': text.Append("''"); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\0': text.Append("\\0"); break;
                    default: text.Append(c); break;
                }

            return text.Append('\'').ToString();
        }
    }
}
