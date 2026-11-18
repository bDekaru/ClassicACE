using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

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

    /// <summary>One row of dat_vital: the formula that adds to a vital from the primary attributes.</summary>
    public class DatVitalRow
    {
        public uint Id { get; set; }
        public uint FormulaW { get; set; }
        public uint FormulaX { get; set; }
        public uint FormulaY { get; set; }
        public uint FormulaZ { get; set; }
        public uint FormulaAttr1 { get; set; }
        public uint FormulaAttr2 { get; set; }
    }

    /// <summary>One row of dat_character_title: a title id, its name in the portal dat's title enum mapper and its display text in the language dat's title string table.</summary>
    public class DatTitleRow
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }
    }

    /// <summary>The row of dat_chargen: the credits every heritage group starts character creation with.</summary>
    public class DatChargenRow
    {
        public uint Id { get; set; }
        public uint AttributeCredits { get; set; }
        public uint SkillCredits { get; set; }
    }

    /// <summary>One row of dat_chargen_heritage_skill_override: the trained and specialized cost of a skill for one heritage group, instead of the skill table's. The specialized cost is the total, trained cost included.</summary>
    public class DatChargenHeritageSkillOverrideRow
    {
        public uint HeritageGroup { get; set; }
        public uint SkillId { get; set; }
        public int TrainedCost { get; set; }
        public int SpecializedCost { get; set; }
    }

    /// <summary>One row of dat_chargen_template: a character creation template of a heritage group.</summary>
    public class DatChargenTemplateRow
    {
        public uint HeritageGroup { get; set; }
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint IconId { get; set; }
        public uint Title { get; set; }
        public uint Strength { get; set; }
        public uint Endurance { get; set; }
        public uint Coordination { get; set; }
        public uint Quickness { get; set; }
        public uint Focus { get; set; }
        public uint Self { get; set; }
    }

    /// <summary>One row of dat_chargen_template_skill: a skill a template trains or specializes.</summary>
    public class DatChargenTemplateSkillRow
    {
        public uint HeritageGroup { get; set; }
        public uint TemplateId { get; set; }
        public uint SkillId { get; set; }
        public uint AdvancementClass { get; set; }
    }

    /// <summary>The rows of the four dat_chargen* tables.</summary>
    public class DatChargenRows
    {
        public DatChargenRow Chargen { get; set; }
        public List<DatChargenHeritageSkillOverrideRow> Overrides { get; set; } = new List<DatChargenHeritageSkillOverrideRow>();
        public List<DatChargenTemplateRow> Templates { get; set; } = new List<DatChargenTemplateRow>();
        public List<DatChargenTemplateSkillRow> TemplateSkills { get; set; } = new List<DatChargenTemplateSkillRow>();
    }

    /// <summary>One row of dat_spell. The formula is stored decrypted, one component id per slot; a slot without a component is null and only trailing slots may be null.</summary>
    public class DatSpellRow
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public uint School { get; set; }
        public uint IconId { get; set; }
        public uint Category { get; set; }
        public uint Bitfield { get; set; }
        public uint BaseMana { get; set; }
        public float BaseRangeConstant { get; set; }
        public float BaseRangeMod { get; set; }
        public uint Power { get; set; }
        public float SpellEconomyMod { get; set; }
        public uint FormulaVersion { get; set; }
        public float ComponentLoss { get; set; }
        public uint MetaSpellType { get; set; }
        public uint MetaSpellId { get; set; }
        public double? Duration { get; set; }
        public float? DegradeModifier { get; set; }
        public float? DegradeLimit { get; set; }
        public double? PortalLifetime { get; set; }
        public uint?[] Components { get; set; } = new uint?[SpellBase.FormulaSlots];
        public uint CasterEffect { get; set; }
        public uint TargetEffect { get; set; }
        public uint FizzleEffect { get; set; }
        public double RecoveryInterval { get; set; }
        public float RecoveryAmount { get; set; }
        public uint DisplayOrder { get; set; }
        public uint NonComponentTargetType { get; set; }
        public uint ManaMod { get; set; }
    }

    /// <summary>One row of dat_spell_set_spell: a spell that is active at a tier of an equipment set.</summary>
    public class DatSpellSetSpellRow
    {
        public uint SetId { get; set; }
        public uint Tier { get; set; }
        public uint Position { get; set; }
        public uint SpellId { get; set; }
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
        /// Builds the skill table from the rows, in id order; only the bucket size is taken from the base record. The order in the record means nothing to the client, which hashes every entry into its bucket as it reads it.
        /// </summary>
        public static SkillTable ToSkillTable(IEnumerable<DatSkillRow> rows, SkillTable baseTable = null)
        {
            var byId = rows.ToDictionary(r => r.Id);
            var table = new SkillTable();

            if (baseTable != null)
                table.BucketSize = baseTable.BucketSize;

            foreach (var id in byId.Keys.OrderBy(key => key))
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

        // ------------------------------------------------------------------ vital table

        public const string VitalName = "dat_vital";

        /// <summary>The vitals the record has a formula for, as PropertyAttribute2nd values.</summary>
        public static readonly uint[] VitalIds = { (uint)PropertyAttribute2nd.MaxHealth, (uint)PropertyAttribute2nd.MaxStamina, (uint)PropertyAttribute2nd.MaxMana };

        public static List<DatVitalRow> ToRows(SecondaryAttributeTable table)
        {
            return new[]
            {
                VitalRow(PropertyAttribute2nd.MaxHealth, table.MaxHealth),
                VitalRow(PropertyAttribute2nd.MaxStamina, table.MaxStamina),
                VitalRow(PropertyAttribute2nd.MaxMana, table.MaxMana)
            }.ToList();
        }

        private static DatVitalRow VitalRow(PropertyAttribute2nd vital, Attribute2ndBase value)
        {
            return new DatVitalRow
            {
                Id = (uint)vital,
                FormulaW = value.Formula.W,
                FormulaX = value.Formula.X,
                FormulaY = value.Formula.Y,
                FormulaZ = value.Formula.Z,
                FormulaAttr1 = value.Formula.Attr1,
                FormulaAttr2 = value.Formula.Attr2
            };
        }

        /// <exception cref="InvalidDataException">A vital has no row, or a row is for another vital.</exception>
        public static SecondaryAttributeTable ToVitalTable(IEnumerable<DatVitalRow> rows)
        {
            var byId = rows.ToDictionary(r => r.Id);

            foreach (var id in VitalIds)
                if (!byId.ContainsKey(id))
                    throw new InvalidDataException($"{VitalName}: the row of vital {id} is missing.");

            var unknown = byId.Keys.Except(VitalIds).ToList();

            if (unknown.Count > 0)
                throw new InvalidDataException($"{VitalName}: row {unknown[0]} is not a vital with a formula (only {string.Join(", ", VitalIds)}).");

            return new SecondaryAttributeTable
            {
                MaxHealth = VitalValue(byId[(uint)PropertyAttribute2nd.MaxHealth]),
                MaxStamina = VitalValue(byId[(uint)PropertyAttribute2nd.MaxStamina]),
                MaxMana = VitalValue(byId[(uint)PropertyAttribute2nd.MaxMana])
            };
        }

        private static Attribute2ndBase VitalValue(DatVitalRow row)
        {
            return new Attribute2ndBase { Formula = new SkillFormula { W = row.FormulaW, X = row.FormulaX, Y = row.FormulaY, Z = row.FormulaZ, Attr1 = row.FormulaAttr1, Attr2 = row.FormulaAttr2 } };
        }

        // ------------------------------------------------------------------ character titles

        public const string TitleName = "dat_character_title";
        public const uint TitleMapperId = 0x22000041;

        /// <summary>The key of a title's text in the language dat: the hash of its name in the enum mapper.</summary>
        public static uint TitleKey(string name)
        {
            return SpellTable.ComputeHash(name);
        }

        /// <exception cref="InvalidDataException">A mapper entry has no text in the string table.</exception>
        public static List<DatTitleRow> ToRows(EnumMapper mapper, StringTable strings)
        {
            var texts = strings.StringTableData.ToDictionary(d => d.Id);
            var rows = new List<DatTitleRow>();

            foreach (var entry in mapper.IdToStringMap.OrderBy(e => e.Key))
            {
                if (!texts.TryGetValue(TitleKey(entry.Value), out var data) || data.Strings.Count == 0)
                    throw new InvalidDataException($"{TitleName}: title {entry.Key} '{entry.Value}' has no text in the string table.");

                rows.Add(new DatTitleRow { Id = entry.Key, Name = entry.Value, Text = data.Strings[0] });
            }

            return rows;
        }

        /// <summary>
        /// The base mapper with its entries replaced by the rows': titles it already has keep their position, new ones follow in id order, titles without a row are dropped. The base mapper is changed and returned.
        /// </summary>
        /// <exception cref="InvalidDataException">A row repeats an id or name, or has an empty or over-long name.</exception>
        public static EnumMapper ToTitleMapper(IEnumerable<DatTitleRow> rows, EnumMapper baseMapper)
        {
            var byId = CheckTitles(rows);

            var order = baseMapper.IdToStringMap.Keys.Where(byId.ContainsKey).Concat(byId.Keys.Where(k => !baseMapper.IdToStringMap.ContainsKey(k)).OrderBy(k => k)).ToList();

            baseMapper.IdToStringMap = new Dictionary<uint, string>();

            foreach (var id in order)
                baseMapper.IdToStringMap.Add(id, byId[id].Name);

            return baseMapper;
        }

        /// <summary>
        /// The base string table with the rows' texts keyed by the hash of their names: entries it already has keep their position and fields, new ones follow in id order, entries without a row are dropped. The base table is changed and returned.
        /// </summary>
        /// <exception cref="InvalidDataException">A row repeats an id or name, or two names have the same key.</exception>
        public static StringTable ToTitleStringTable(IEnumerable<DatTitleRow> rows, StringTable baseTable)
        {
            var byId = CheckTitles(rows);
            var byKey = new Dictionary<uint, DatTitleRow>();

            foreach (var row in byId.Values.OrderBy(r => r.Id))
            {
                var key = TitleKey(row.Name);

                if (byKey.ContainsKey(key))
                    throw new InvalidDataException($"{TitleName}: '{row.Name}' and '{byKey[key].Name}' have the same text key.");

                byKey.Add(key, row);
            }

            var existing = baseTable.StringTableData.ToList();
            var template = existing.FirstOrDefault();
            var added = new HashSet<uint>();

            baseTable.StringTableData.Clear();

            foreach (var data in existing)
            {
                if (!byKey.TryGetValue(data.Id, out var row))
                    continue;

                if (data.Strings.Count != 1 || data.Strings[0] != row.Text)
                {
                    data.Strings.Clear();
                    data.Strings.Add(row.Text);
                }

                baseTable.StringTableData.Add(data);
                added.Add(data.Id);
            }

            foreach (var pair in byKey.Where(p => !added.Contains(p.Key)).OrderBy(p => p.Value.Id))
            {
                var data = new StringTableData { Id = pair.Key, Unknown = template?.Unknown ?? 0 };
                data.Strings.Add(pair.Value.Text);
                baseTable.StringTableData.Add(data);
            }

            return baseTable;
        }

        private static Dictionary<uint, DatTitleRow> CheckTitles(IEnumerable<DatTitleRow> rows)
        {
            var byId = new Dictionary<uint, DatTitleRow>();
            var names = new HashSet<string>();

            foreach (var row in rows)
            {
                if (string.IsNullOrEmpty(row.Name) || row.Name.Length > 255)
                    throw new InvalidDataException($"{TitleName}: title {row.Id} has an empty or over-long name.");

                if (byId.ContainsKey(row.Id))
                    throw new InvalidDataException($"{TitleName}: id {row.Id} appears twice.");

                byId.Add(row.Id, row);

                if (!names.Add(row.Name))
                    throw new InvalidDataException($"{TitleName}: name '{row.Name}' appears twice.");
            }

            return byId;
        }

        // ------------------------------------------------------------------ character generation

        public const string ChargenName = "dat_chargen";
        public const string ChargenHeritageSkillOverrideName = "dat_chargen_heritage_skill_override";
        public const string ChargenTemplateName = "dat_chargen_template";
        public const string ChargenTemplateSkillName = "dat_chargen_template_skill";

        /// <summary>Id of the single row of dat_chargen.</summary>
        public const uint ChargenRowId = 1;

        /// <summary>The rows of a character generation record. The credits are those of the lowest heritage group; the builder applies them to every group. A skill a template specializes is listed once, as Specialized.</summary>
        public static DatChargenRows ToRows(CharGen record)
        {
            var rows = new DatChargenRows();
            var first = record.HeritageGroups.OrderBy(g => g.Key).First().Value;

            rows.Chargen = new DatChargenRow { Id = ChargenRowId, AttributeCredits = first.AttributeCredits, SkillCredits = first.SkillCredits };

            foreach (var group in record.HeritageGroups.OrderBy(g => g.Key))
            {
                foreach (var skill in group.Value.Skills.OrderBy(s => s.SkillNum))
                    rows.Overrides.Add(new DatChargenHeritageSkillOverrideRow { HeritageGroup = group.Key, SkillId = skill.SkillNum, TrainedCost = skill.NormalCost, SpecializedCost = skill.PrimaryCost });

                for (var i = 0; i < group.Value.Templates.Count; i++)
                {
                    var t = group.Value.Templates[i];

                    rows.Templates.Add(new DatChargenTemplateRow
                    {
                        HeritageGroup = group.Key, Id = (uint)i, Name = t.Name, IconId = t.IconImage, Title = t.Title,
                        Strength = t.Strength, Endurance = t.Endurance, Coordination = t.Coordination, Quickness = t.Quickness, Focus = t.Focus, Self = t.Self
                    });

                    foreach (var skillId in t.PrimarySkillsList.Distinct().OrderBy(s => s))
                        rows.TemplateSkills.Add(new DatChargenTemplateSkillRow { HeritageGroup = group.Key, TemplateId = (uint)i, SkillId = skillId, AdvancementClass = (uint)SkillAdvancementClass.Specialized });

                    foreach (var skillId in t.NormalSkillsList.Distinct().Except(t.PrimarySkillsList).OrderBy(s => s))
                        rows.TemplateSkills.Add(new DatChargenTemplateSkillRow { HeritageGroup = group.Key, TemplateId = (uint)i, SkillId = skillId, AdvancementClass = (uint)SkillAdvancementClass.Trained });
                }
            }

            rows.TemplateSkills = rows.TemplateSkills.OrderBy(r => r.HeritageGroup).ThenBy(r => r.TemplateId).ThenBy(r => r.SkillId).ToList();

            return rows;
        }

        /// <summary>Whether every heritage group of the record has the row's credits. <see cref="ToRows(CharGen)"/> reads them from one group only, so a comparison by hash alone would not see a group that differs.</summary>
        public static bool CreditsApplied(CharGen record, DatChargenRow row)
        {
            return record.HeritageGroups.Values.All(g => g.AttributeCredits == row.AttributeCredits && g.SkillCredits == row.SkillCredits);
        }

        /// <summary>
        /// The base record with the rows' values: the credits on every heritage group, each group's skill cost overrides and templates replaced (a template's skills are listed by skill id). Names, icons, start areas and genders stay as the base has them. The base record is changed and returned.
        /// </summary>
        /// <exception cref="InvalidDataException">A heritage group is unknown or has no template, template ids are not contiguous from 0, a skill is not in the skill table, is repeated, or has an advancement class other than Trained or Specialized.</exception>
        public static CharGen ToCharGen(DatChargenRows rows, CharGen baseRecord, IEnumerable<uint> skillIds)
        {
            if (rows.Chargen == null)
                throw new InvalidDataException($"{ChargenName}: the row is missing.");

            var skills = new HashSet<uint>(skillIds);

            foreach (var id in rows.Overrides.Select(r => r.HeritageGroup).Concat(rows.Templates.Select(r => r.HeritageGroup)).Concat(rows.TemplateSkills.Select(r => r.HeritageGroup)).Distinct())
                if (!baseRecord.HeritageGroups.ContainsKey(id))
                    throw new InvalidDataException($"{ChargenName}: heritage group {id} is not in the character generation record.");

            var overrides = new HashSet<(uint, uint)>();

            foreach (var row in rows.Overrides)
            {
                if (!skills.Contains(row.SkillId))
                    throw new InvalidDataException($"{ChargenHeritageSkillOverrideName}: skill {row.SkillId} of heritage group {row.HeritageGroup} is not in the skill table.");

                if (!overrides.Add((row.HeritageGroup, row.SkillId)))
                    throw new InvalidDataException($"{ChargenHeritageSkillOverrideName}: skill {row.SkillId} of heritage group {row.HeritageGroup} appears twice.");
            }

            var templateKeys = new HashSet<(uint, uint)>();

            foreach (var row in rows.Templates)
            {
                if (string.IsNullOrEmpty(row.Name))
                    throw new InvalidDataException($"{ChargenTemplateName}: template {row.Id} of heritage group {row.HeritageGroup} has no name.");

                if (!templateKeys.Add((row.HeritageGroup, row.Id)))
                    throw new InvalidDataException($"{ChargenTemplateName}: template {row.Id} of heritage group {row.HeritageGroup} appears twice.");
            }

            var templateSkills = new HashSet<(uint, uint, uint)>();

            foreach (var row in rows.TemplateSkills)
            {
                if (!templateKeys.Contains((row.HeritageGroup, row.TemplateId)))
                    throw new InvalidDataException($"{ChargenTemplateSkillName}: template {row.TemplateId} of heritage group {row.HeritageGroup} has no row in {ChargenTemplateName}.");

                if (!skills.Contains(row.SkillId))
                    throw new InvalidDataException($"{ChargenTemplateSkillName}: skill {row.SkillId} of template {row.TemplateId} (heritage group {row.HeritageGroup}) is not in the skill table.");

                if (row.AdvancementClass != (uint)SkillAdvancementClass.Trained && row.AdvancementClass != (uint)SkillAdvancementClass.Specialized)
                    throw new InvalidDataException($"{ChargenTemplateSkillName}: skill {row.SkillId} of template {row.TemplateId} (heritage group {row.HeritageGroup}) has advancement class {row.AdvancementClass}; only Trained ({(uint)SkillAdvancementClass.Trained}) and Specialized ({(uint)SkillAdvancementClass.Specialized}) are allowed.");

                if (!templateSkills.Add((row.HeritageGroup, row.TemplateId, row.SkillId)))
                    throw new InvalidDataException($"{ChargenTemplateSkillName}: skill {row.SkillId} of template {row.TemplateId} (heritage group {row.HeritageGroup}) appears twice.");
            }

            foreach (var group in baseRecord.HeritageGroups)
            {
                var templates = rows.Templates.Where(t => t.HeritageGroup == group.Key).OrderBy(t => t.Id).ToList();

                if (templates.Count == 0)
                    throw new InvalidDataException($"{ChargenTemplateName}: heritage group {group.Key} has no template.");

                for (var i = 0; i < templates.Count; i++)
                    if (templates[i].Id != (uint)i)
                        throw new InvalidDataException($"{ChargenTemplateName}: the template ids of heritage group {group.Key} must run 0, 1, 2, ... without gaps (found {templates[i].Id} at position {i}).");

                var value = group.Value;

                value.AttributeCredits = rows.Chargen.AttributeCredits;
                value.SkillCredits = rows.Chargen.SkillCredits;

                value.Skills.Clear();

                foreach (var row in rows.Overrides.Where(r => r.HeritageGroup == group.Key).OrderBy(r => r.SkillId))
                    value.Skills.Add(new SkillCG { SkillNum = row.SkillId, NormalCost = row.TrainedCost, PrimaryCost = row.SpecializedCost });

                value.Templates.Clear();

                foreach (var row in templates)
                {
                    var template = new TemplateCG
                    {
                        Name = row.Name, IconImage = row.IconId, Title = row.Title,
                        Strength = row.Strength, Endurance = row.Endurance, Coordination = row.Coordination, Quickness = row.Quickness, Focus = row.Focus, Self = row.Self
                    };

                    foreach (var skill in rows.TemplateSkills.Where(s => s.HeritageGroup == group.Key && s.TemplateId == row.Id).OrderBy(s => s.SkillId))
                    {
                        if (skill.AdvancementClass == (uint)SkillAdvancementClass.Specialized)
                            template.PrimarySkillsList.Add(skill.SkillId);
                        else
                            template.NormalSkillsList.Add(skill.SkillId);
                    }

                    value.Templates.Add(template);
                }
            }

            return baseRecord;
        }

        // ------------------------------------------------------------------ spell table

        public const string SpellName = "dat_spell";
        public const string SpellSetSpellName = "dat_spell_set_spell";

        public static List<DatSpellRow> ToRows(SpellTable table)
        {
            return table.Spells.OrderBy(s => s.Key).Select(s =>
            {
                var spell = s.Value;
                var enchantment = spell.MetaSpellType == SpellType.Enchantment || spell.MetaSpellType == SpellType.FellowEnchantment;
                var row = new DatSpellRow
                {
                    Id = s.Key,
                    Name = spell.Name,
                    Description = spell.Desc,
                    School = (uint)spell.School,
                    IconId = spell.Icon,
                    Category = (uint)spell.Category,
                    Bitfield = spell.Bitfield,
                    BaseMana = spell.BaseMana,
                    BaseRangeConstant = spell.BaseRangeConstant,
                    BaseRangeMod = spell.BaseRangeMod,
                    Power = spell.Power,
                    SpellEconomyMod = spell.SpellEconomyMod,
                    FormulaVersion = spell.FormulaVersion,
                    ComponentLoss = spell.ComponentLoss,
                    MetaSpellType = (uint)spell.MetaSpellType,
                    MetaSpellId = spell.MetaSpellId,
                    Duration = enchantment ? spell.Duration : (double?)null,
                    DegradeModifier = enchantment ? spell.DegradeModifier : (float?)null,
                    DegradeLimit = enchantment ? spell.DegradeLimit : (float?)null,
                    PortalLifetime = spell.MetaSpellType == SpellType.PortalSummon ? spell.PortalLifetime : (double?)null,
                    CasterEffect = spell.CasterEffect,
                    TargetEffect = spell.TargetEffect,
                    FizzleEffect = spell.FizzleEffect,
                    RecoveryInterval = spell.RecoveryInterval,
                    RecoveryAmount = spell.RecoveryAmount,
                    DisplayOrder = spell.DisplayOrder,
                    NonComponentTargetType = spell.NonComponentTargetType,
                    ManaMod = spell.ManaMod
                };

                for (var i = 0; i < spell.Formula.Count; i++)
                    row.Components[i] = spell.Formula[i];

                return row;
            }).ToList();
        }

        public static List<DatSpellSetSpellRow> ToSetRows(SpellTable table)
        {
            var rows = new List<DatSpellSetSpellRow>();

            foreach (var set in table.SpellSet.OrderBy(s => s.Key))
                foreach (var tier in set.Value.SpellSetTiers)
                    for (var position = 0; position < tier.Value.Spells.Count; position++)
                        rows.Add(new DatSpellSetSpellRow { SetId = set.Key, Tier = tier.Key, Position = (uint)position, SpellId = tier.Value.Spells[position] });

            return rows;
        }

        /// <summary>
        /// Builds the spell record from the rows, spells and sets in id order; only the bucket sizes are taken from the base record. The order in the record means nothing to the client, which hashes every entry into its bucket as it reads it.
        /// </summary>
        /// <exception cref="InvalidDataException">A formula has a gap, a tier has a position missing or repeated, or a tier names a spell without a row.</exception>
        public static SpellTable ToSpellTable(IEnumerable<DatSpellRow> spellRows, IEnumerable<DatSpellSetSpellRow> setRows, SpellTable baseTable = null)
        {
            var byId = spellRows.ToDictionary(r => r.Id);
            var table = new SpellTable();

            if (baseTable != null)
            {
                table.SpellBucketSize = baseTable.SpellBucketSize;
                table.SetBucketSize = baseTable.SetBucketSize;
            }

            foreach (var id in byId.Keys.OrderBy(key => key))
            {
                var row = byId[id];
                var formula = new List<uint>();
                var ended = false;

                for (var i = 0; i < SpellBase.FormulaSlots; i++)
                {
                    var component = i < row.Components.Length ? row.Components[i] : null;

                    if (component == null)
                        ended = true;
                    else if (ended)
                        throw new InvalidDataException($"{SpellName}: spell {id} has component_{i + 1} after an empty slot.");
                    else
                        formula.Add(component.Value);
                }

                table.Spells[id] = new SpellBase
                {
                    Name = row.Name,
                    Desc = row.Description,
                    School = (MagicSchool)row.School,
                    Icon = row.IconId,
                    Category = (SpellCategory)row.Category,
                    Bitfield = row.Bitfield,
                    BaseMana = row.BaseMana,
                    BaseRangeConstant = row.BaseRangeConstant,
                    BaseRangeMod = row.BaseRangeMod,
                    Power = row.Power,
                    SpellEconomyMod = row.SpellEconomyMod,
                    FormulaVersion = row.FormulaVersion,
                    ComponentLoss = row.ComponentLoss,
                    MetaSpellType = (SpellType)row.MetaSpellType,
                    MetaSpellId = row.MetaSpellId,
                    Duration = row.Duration ?? 0,
                    DegradeModifier = row.DegradeModifier ?? 0,
                    DegradeLimit = row.DegradeLimit ?? 0,
                    PortalLifetime = row.PortalLifetime ?? 0,
                    Formula = formula,
                    CasterEffect = row.CasterEffect,
                    TargetEffect = row.TargetEffect,
                    FizzleEffect = row.FizzleEffect,
                    RecoveryInterval = row.RecoveryInterval,
                    RecoveryAmount = row.RecoveryAmount,
                    DisplayOrder = row.DisplayOrder,
                    NonComponentTargetType = row.NonComponentTargetType,
                    ManaMod = row.ManaMod
                };
            }

            var setsById = setRows.GroupBy(r => r.SetId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var setId in setsById.Keys.OrderBy(key => key))
            {
                var set = new SpellSet();

                if (baseTable != null && baseTable.SpellSet.TryGetValue(setId, out var baseSet))
                    set.BucketSize = baseSet.BucketSize;

                foreach (var tierGroup in setsById[setId].GroupBy(r => r.Tier).OrderBy(g => g.Key))
                {
                    var tier = new SpellSetTiers();
                    var ordered = tierGroup.OrderBy(r => r.Position).ToList();

                    for (var i = 0; i < ordered.Count; i++)
                    {
                        if (ordered[i].Position != i)
                            throw new InvalidDataException($"{SpellSetSpellName}: set {setId} tier {tierGroup.Key} has position {i} missing or repeated (next row is position {ordered[i].Position}).");

                        if (!byId.ContainsKey(ordered[i].SpellId))
                            throw new InvalidDataException($"{SpellSetSpellName}: set {setId} tier {tierGroup.Key} names spell {ordered[i].SpellId}, which has no row in {SpellName}.");

                        tier.Spells.Add(ordered[i].SpellId);
                    }

                    set.SpellSetTiers[tierGroup.Key] = tier;
                }

                table.SpellSet[setId] = set;
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

        public static string Hash(IEnumerable<DatVitalRow> vitals)
        {
            var text = new StringBuilder();

            foreach (var v in vitals.OrderBy(v => v.Id))
                text.Append(Inv($"I|{v.Id}|{v.FormulaW}|{v.FormulaX}|{v.FormulaY}|{v.FormulaZ}|{v.FormulaAttr1}|{v.FormulaAttr2}\n"));

            return Sha256(text.ToString());
        }

        public static string Hash(IEnumerable<DatTitleRow> titles)
        {
            var text = new StringBuilder();

            foreach (var t in titles.OrderBy(t => t.Id))
                text.Append(Inv($"T|{t.Id}|{t.Name}|{t.Text}\n"));

            return Sha256(text.ToString());
        }

        public static string Hash(DatChargenRows rows)
        {
            var text = new StringBuilder();

            text.Append(Inv($"C|{rows.Chargen.Id}|{rows.Chargen.AttributeCredits}|{rows.Chargen.SkillCredits}\n"));

            foreach (var o in rows.Overrides.OrderBy(o => o.HeritageGroup).ThenBy(o => o.SkillId))
                text.Append(Inv($"O|{o.HeritageGroup}|{o.SkillId}|{o.TrainedCost}|{o.SpecializedCost}\n"));

            foreach (var t in rows.Templates.OrderBy(t => t.HeritageGroup).ThenBy(t => t.Id))
                text.Append(Inv($"T|{t.HeritageGroup}|{t.Id}|{t.Name}|{t.IconId}|{t.Title}|{t.Strength}|{t.Endurance}|{t.Coordination}|{t.Quickness}|{t.Focus}|{t.Self}\n"));

            foreach (var s in rows.TemplateSkills.OrderBy(s => s.HeritageGroup).ThenBy(s => s.TemplateId).ThenBy(s => s.SkillId))
                text.Append(Inv($"K|{s.HeritageGroup}|{s.TemplateId}|{s.SkillId}|{s.AdvancementClass}\n"));

            return Sha256(text.ToString());
        }

        public static string Hash(IEnumerable<DatSpellRow> spells, IEnumerable<DatSpellSetSpellRow> setSpells)
        {
            var text = new StringBuilder();

            foreach (var s in spells.OrderBy(s => s.Id))
                text.Append(Inv($"P|{s.Id}|{s.Name}|{s.Description}|{s.School}|{s.IconId}|{s.Category}|{s.Bitfield}|{s.BaseMana}|{s.BaseRangeConstant:R}|{s.BaseRangeMod:R}|{s.Power}|{s.SpellEconomyMod:R}|{s.FormulaVersion}|{s.ComponentLoss:R}|{s.MetaSpellType}|{s.MetaSpellId}|{Opt(s.Duration)}|{Opt(s.DegradeModifier)}|{Opt(s.DegradeLimit)}|{Opt(s.PortalLifetime)}|{string.Join(",", s.Components.Select(c => c?.ToString() ?? "-"))}|{s.CasterEffect}|{s.TargetEffect}|{s.FizzleEffect}|{s.RecoveryInterval:R}|{s.RecoveryAmount:R}|{s.DisplayOrder}|{s.NonComponentTargetType}|{s.ManaMod}\n"));

            foreach (var r in setSpells.OrderBy(r => r.SetId).ThenBy(r => r.Tier).ThenBy(r => r.Position))
                text.Append(Inv($"E|{r.SetId}|{r.Tier}|{r.Position}|{r.SpellId}\n"));

            return Sha256(text.ToString());
        }

        private static string Opt<T>(T? value) where T : struct, IFormattable
        {
            return value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "-";
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

        /// <summary>The CREATE TABLE statement of the vital table.</summary>
        public const string VitalCreateScript =
@"-- Portal dat table (vital formulas): the source the dat builder writes the secondary attribute table (0x0E000003) from.
-- Every column is client-visible: changing a value needs a dat build, a server restart and a dat release.

CREATE TABLE IF NOT EXISTS `dat_vital` (
  `id` int(10) unsigned NOT NULL COMMENT 'PropertyAttribute2nd: MaxHealth, MaxStamina or MaxMana',
  `formula_W` int(10) unsigned NOT NULL COMMENT 'Vital formula: (attr1 * x + attr2 * y) / z + w',
  `formula_X` int(10) unsigned NOT NULL,
  `formula_Y` int(10) unsigned NOT NULL,
  `formula_Z` int(10) unsigned NOT NULL,
  `formula_Attr1` int(10) unsigned NOT NULL COMMENT 'PropertyAttribute',
  `formula_Attr2` int(10) unsigned NOT NULL COMMENT 'PropertyAttribute',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat vital table';
";

        /// <summary>INSERT statements that load the rows into the vital table, after emptying it.</summary>
        public static string VitalInsertScript(IEnumerable<DatVitalRow> vitals)
        {
            var sql = new StringBuilder();

            sql.AppendLine($"DELETE FROM `{VitalName}`;");

            foreach (var v in vitals.OrderBy(v => v.Id))
                sql.AppendLine(Inv($"INSERT INTO `{VitalName}` (`id`, `formula_W`, `formula_X`, `formula_Y`, `formula_Z`, `formula_Attr1`, `formula_Attr2`) VALUES ({v.Id}, {v.FormulaW}, {v.FormulaX}, {v.FormulaY}, {v.FormulaZ}, {v.FormulaAttr1}, {v.FormulaAttr2});"));

            return sql.ToString();
        }

        /// <summary>The CREATE TABLE statement of the character title table.</summary>
        public const string TitleCreateScript =
@"-- Portal and language dat table (character titles): the source the dat builder writes the title enum mapper (portal 0x22000041) and the title string table (language 0x2300000E) from.
-- The name is the mapper's string 'ID_CharacterTitle_...'; the text is keyed in the language dat by the hash of the name. Both are client-visible: changing a value needs a dat build, a server restart and a dat release.

CREATE TABLE IF NOT EXISTS `dat_character_title` (
  `id` int(10) unsigned NOT NULL COMMENT 'Title id as sent to the client (the CharacterTitle enum value)',
  `name` varchar(255) NOT NULL COMMENT 'Name in the title enum mapper, e.g. ID_CharacterTitle_Adventurer',
  `text` text NOT NULL COMMENT 'Display text',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `name_UNIQUE` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Character titles of the portal and language dats';
";

        /// <summary>INSERT statements that load the rows into the title table, after emptying it.</summary>
        public static string TitleInsertScript(IEnumerable<DatTitleRow> titles)
        {
            var sql = new StringBuilder();

            sql.AppendLine($"DELETE FROM `{TitleName}`;");

            foreach (var t in titles.OrderBy(t => t.Id))
                sql.AppendLine(Inv($"INSERT INTO `{TitleName}` (`id`, `name`, `text`) VALUES ({t.Id}, {Quote(t.Name)}, {Quote(t.Text)});"));

            return sql.ToString();
        }

        /// <summary>The CREATE TABLE statements of the character generation tables.</summary>
        public const string ChargenCreateScript =
@"-- Portal dat tables (character generation): the source the dat builder writes the character generation record (0x0E000002) from. Heritage group names, icons, start areas and genders stay in the dat record.
-- Every column is client-visible: changing a value needs a dat build, a server restart and a dat release.

CREATE TABLE IF NOT EXISTS `dat_chargen` (
  `id` int(10) unsigned NOT NULL COMMENT 'Always 1: one row for the whole record',
  `attribute_Credits` int(10) unsigned NOT NULL COMMENT 'Attribute credits of every heritage group',
  `skill_Credits` int(10) unsigned NOT NULL COMMENT 'Skill credits of every heritage group',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat character generation credits';

CREATE TABLE IF NOT EXISTS `dat_chargen_heritage_skill_override` (
  `heritage_Group` int(10) unsigned NOT NULL COMMENT 'HeritageGroup',
  `skill_Id` int(10) unsigned NOT NULL COMMENT 'Skill',
  `trained_Cost` int(11) NOT NULL COMMENT 'Credits to train the skill, instead of dat_skill.trained_Cost',
  `specialized_Cost` int(11) NOT NULL COMMENT 'Credits to specialize the skill, trained cost included (as in dat_skill)',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`heritage_Group`,`skill_Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat character generation skill costs of a heritage group';

CREATE TABLE IF NOT EXISTS `dat_chargen_template` (
  `heritage_Group` int(10) unsigned NOT NULL COMMENT 'HeritageGroup',
  `id` int(10) unsigned NOT NULL COMMENT 'Template index of the heritage group: 0, 1, 2, ... without gaps',
  `name` varchar(255) NOT NULL,
  `icon_Id` int(10) unsigned NOT NULL,
  `title` int(10) unsigned NOT NULL COMMENT 'CharacterTitle',
  `strength` int(10) unsigned NOT NULL,
  `endurance` int(10) unsigned NOT NULL,
  `coordination` int(10) unsigned NOT NULL,
  `quickness` int(10) unsigned NOT NULL,
  `focus` int(10) unsigned NOT NULL,
  `self` int(10) unsigned NOT NULL,
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`heritage_Group`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat character generation templates';

CREATE TABLE IF NOT EXISTS `dat_chargen_template_skill` (
  `heritage_Group` int(10) unsigned NOT NULL COMMENT 'HeritageGroup',
  `template_Id` int(10) unsigned NOT NULL COMMENT 'dat_chargen_template.id',
  `skill_Id` int(10) unsigned NOT NULL COMMENT 'Skill',
  `advancement_Class` int(10) unsigned NOT NULL COMMENT 'SkillAdvancementClass: 2 Trained or 3 Specialized',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`heritage_Group`,`template_Id`,`skill_Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat character generation template skills';
";

        /// <summary>INSERT statements that load the rows into the character generation tables, after emptying them.</summary>
        public static string ChargenInsertScript(DatChargenRows rows)
        {
            var sql = new StringBuilder();

            sql.AppendLine($"DELETE FROM `{ChargenTemplateSkillName}`;");
            sql.AppendLine($"DELETE FROM `{ChargenTemplateName}`;");
            sql.AppendLine($"DELETE FROM `{ChargenHeritageSkillOverrideName}`;");
            sql.AppendLine($"DELETE FROM `{ChargenName}`;");

            sql.AppendLine(Inv($"INSERT INTO `{ChargenName}` (`id`, `attribute_Credits`, `skill_Credits`) VALUES ({rows.Chargen.Id}, {rows.Chargen.AttributeCredits}, {rows.Chargen.SkillCredits});"));

            foreach (var o in rows.Overrides.OrderBy(o => o.HeritageGroup).ThenBy(o => o.SkillId))
                sql.AppendLine(Inv($"INSERT INTO `{ChargenHeritageSkillOverrideName}` (`heritage_Group`, `skill_Id`, `trained_Cost`, `specialized_Cost`) VALUES ({o.HeritageGroup}, {o.SkillId}, {o.TrainedCost}, {o.SpecializedCost});"));

            foreach (var t in rows.Templates.OrderBy(t => t.HeritageGroup).ThenBy(t => t.Id))
                sql.AppendLine(Inv($"INSERT INTO `{ChargenTemplateName}` (`heritage_Group`, `id`, `name`, `icon_Id`, `title`, `strength`, `endurance`, `coordination`, `quickness`, `focus`, `self`) VALUES ({t.HeritageGroup}, {t.Id}, {Quote(t.Name)}, {t.IconId}, {t.Title}, {t.Strength}, {t.Endurance}, {t.Coordination}, {t.Quickness}, {t.Focus}, {t.Self});"));

            foreach (var s in rows.TemplateSkills.OrderBy(s => s.HeritageGroup).ThenBy(s => s.TemplateId).ThenBy(s => s.SkillId))
                sql.AppendLine(Inv($"INSERT INTO `{ChargenTemplateSkillName}` (`heritage_Group`, `template_Id`, `skill_Id`, `advancement_Class`) VALUES ({s.HeritageGroup}, {s.TemplateId}, {s.SkillId}, {s.AdvancementClass});"));

            return sql.ToString();
        }

        /// <summary>The CREATE TABLE statements of the spell tables.</summary>
        public const string SpellCreateScript =
@"-- Portal dat tables (spells and equipment sets): the source the dat builder writes the spell table (0x0E00000E) from.
-- Every column is client-visible: changing a value needs a dat build, a server restart and a dat release.

CREATE TABLE IF NOT EXISTS `dat_spell` (
  `id` int(10) unsigned NOT NULL COMMENT 'Unique Id of this Spell',
  `name` text NOT NULL,
  `description` text NOT NULL,
  `school` int(10) unsigned NOT NULL COMMENT 'MagicSchool',
  `icon_Id` int(10) unsigned NOT NULL,
  `category` int(10) unsigned NOT NULL COMMENT 'SpellCategory: spells of one category do not stack',
  `bitfield` int(10) unsigned NOT NULL COMMENT 'SpellFlags',
  `base_Mana` int(10) unsigned NOT NULL,
  `base_Range_Constant` float NOT NULL,
  `base_Range_Mod` float NOT NULL,
  `power` int(10) unsigned NOT NULL COMMENT 'Tells the strongest spell of a category',
  `spell_Economy_Mod` float NOT NULL,
  `formula_Version` int(10) unsigned NOT NULL COMMENT 'How the taper slots of the formula are randomized per account',
  `component_Loss` float NOT NULL COMMENT 'Burn rate of the components',
  `meta_Spell_Type` int(10) unsigned NOT NULL COMMENT 'SpellType: which group of the spell table columns the server reads',
  `meta_Spell_Id` int(10) unsigned NOT NULL,
  `duration` double DEFAULT NULL COMMENT 'Enchantment and FellowEnchantment only',
  `degrade_Modifier` float DEFAULT NULL COMMENT 'Enchantment and FellowEnchantment only',
  `degrade_Limit` float DEFAULT NULL COMMENT 'Enchantment and FellowEnchantment only',
  `portal_Lifetime` double DEFAULT NULL COMMENT 'PortalSummon only',
  `component_1` int(10) unsigned DEFAULT NULL COMMENT 'Formula: id of a component of the dat spell component table, empty slots are NULL and only the last slots may be empty',
  `component_2` int(10) unsigned DEFAULT NULL,
  `component_3` int(10) unsigned DEFAULT NULL,
  `component_4` int(10) unsigned DEFAULT NULL,
  `component_5` int(10) unsigned DEFAULT NULL,
  `component_6` int(10) unsigned DEFAULT NULL,
  `component_7` int(10) unsigned DEFAULT NULL,
  `component_8` int(10) unsigned DEFAULT NULL,
  `caster_Effect` int(10) unsigned NOT NULL COMMENT 'PlayScript',
  `target_Effect` int(10) unsigned NOT NULL COMMENT 'PlayScript',
  `fizzle_Effect` int(10) unsigned NOT NULL COMMENT 'PlayScript',
  `recovery_Interval` double NOT NULL,
  `recovery_Amount` float NOT NULL,
  `display_Order` int(10) unsigned NOT NULL COMMENT 'Sort order in the client spell list',
  `non_Component_Target_Type` int(10) unsigned NOT NULL COMMENT 'ItemType',
  `mana_Mod` int(10) unsigned NOT NULL COMMENT 'Additional mana cost per target',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat spell table';

CREATE TABLE IF NOT EXISTS `dat_spell_set_spell` (
  `set_Id` int(10) unsigned NOT NULL COMMENT 'EquipmentSet',
  `tier` int(10) unsigned NOT NULL COMMENT 'Piece count (or sum of item levels) at which the spells start to be active',
  `position` int(10) unsigned NOT NULL COMMENT 'Order of the spell within the tier, from 0',
  `spell_Id` int(10) unsigned NOT NULL COMMENT 'Id of a dat_spell row',
  `last_Modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`set_Id`,`tier`,`position`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Portal dat spell table: the spells of each equipment set tier';
";

        /// <summary>INSERT statements that load the rows into the two spell tables, after emptying them.</summary>
        public static string SpellInsertScript(IEnumerable<DatSpellRow> spells, IEnumerable<DatSpellSetSpellRow> setSpells)
        {
            var sql = new StringBuilder();

            sql.AppendLine($"DELETE FROM `{SpellSetSpellName}`;");
            sql.AppendLine($"DELETE FROM `{SpellName}`;");

            foreach (var s in spells.OrderBy(s => s.Id))
                sql.AppendLine(Inv($"INSERT INTO `{SpellName}` (`id`, `name`, `description`, `school`, `icon_Id`, `category`, `bitfield`, `base_Mana`, `base_Range_Constant`, `base_Range_Mod`, `power`, `spell_Economy_Mod`, `formula_Version`, `component_Loss`, `meta_Spell_Type`, `meta_Spell_Id`, `duration`, `degrade_Modifier`, `degrade_Limit`, `portal_Lifetime`, `component_1`, `component_2`, `component_3`, `component_4`, `component_5`, `component_6`, `component_7`, `component_8`, `caster_Effect`, `target_Effect`, `fizzle_Effect`, `recovery_Interval`, `recovery_Amount`, `display_Order`, `non_Component_Target_Type`, `mana_Mod`) VALUES ({s.Id}, {Quote(s.Name)}, {Quote(s.Description)}, {s.School}, {s.IconId}, {s.Category}, {s.Bitfield}, {s.BaseMana}, {s.BaseRangeConstant:R}, {s.BaseRangeMod:R}, {s.Power}, {s.SpellEconomyMod:R}, {s.FormulaVersion}, {s.ComponentLoss:R}, {s.MetaSpellType}, {s.MetaSpellId}, {SqlOpt(s.Duration)}, {SqlOpt(s.DegradeModifier)}, {SqlOpt(s.DegradeLimit)}, {SqlOpt(s.PortalLifetime)}, {string.Join(", ", s.Components.Select(c => c?.ToString() ?? "NULL"))}, {s.CasterEffect}, {s.TargetEffect}, {s.FizzleEffect}, {s.RecoveryInterval:R}, {s.RecoveryAmount:R}, {s.DisplayOrder}, {s.NonComponentTargetType}, {s.ManaMod});"));

            foreach (var r in setSpells.OrderBy(r => r.SetId).ThenBy(r => r.Tier).ThenBy(r => r.Position))
                sql.AppendLine(Inv($"INSERT INTO `{SpellSetSpellName}` (`set_Id`, `tier`, `position`, `spell_Id`) VALUES ({r.SetId}, {r.Tier}, {r.Position}, {r.SpellId});"));

            return sql.ToString();
        }

        private static string SqlOpt<T>(T? value) where T : struct, IFormattable
        {
            return value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "NULL";
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
