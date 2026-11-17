using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.EntityFrameworkCore;

using MySqlConnector;

using ACE.Database.Models.World;
using ACE.DatLoader;

namespace ACE.Database
{
    /// <summary>
    /// Reads the dat_* tables of a world database. Used by the dat builder (to write the dat records) and by the server's start-up check (to compare the database with the loaded dat).
    /// </summary>
    public static class DatTableReader
    {
        /// <summary>
        /// The rows, or null when the database has no dat_* tables or they are all empty.
        /// </summary>
        /// <exception cref="InvalidDataException">Only some of the tables exist or have rows.</exception>
        public static DatTableRows Read(WorldDbContext context, string description)
        {
            var attribute = Query(context.DatXpAttribute, r => new DatXpRow { Rank = r.Rank, TotalXp = r.TotalXp });
            var vital = Query(context.DatXpVital, r => new DatXpRow { Rank = r.Rank, TotalXp = r.TotalXp });
            var trained = Query(context.DatXpTrainedSkill, r => new DatXpRow { Rank = r.Rank, TotalXp = r.TotalXp });
            var specialized = Query(context.DatXpSpecializedSkill, r => new DatXpRow { Rank = r.Rank, TotalXp = r.TotalXp });
            var levels = Query(context.DatXpLevel, r => new DatXpLevelRow { Level = r.Level, TotalXp = r.TotalXp, SkillCredits = r.SkillCredits });

            var skills = Query(context.DatSkill, s => new DatSkillRow
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                IconId = s.IconId,
                TrainedCost = s.TrainedCost,
                SpecializedCost = s.SpecializedCost,
                Category = s.Category,
                ChargenUse = s.ChargenUse,
                MinLevel = s.MinLevel,
                FormulaW = s.FormulaW,
                FormulaX = s.FormulaX,
                FormulaY = s.FormulaY,
                FormulaZ = s.FormulaZ,
                FormulaAttr1 = s.FormulaAttr1,
                FormulaAttr2 = s.FormulaAttr2,
                UpperBound = s.UpperBound,
                LowerBound = s.LowerBound,
                LearnMod = s.LearnMod
            });

            var counts = new[]
            {
                (DatTables.XpAttributeTable, attribute?.Count ?? 0),
                (DatTables.XpVitalTable, vital?.Count ?? 0),
                (DatTables.XpTrainedSkillTable, trained?.Count ?? 0),
                (DatTables.XpSpecializedSkillTable, specialized?.Count ?? 0),
                (DatTables.XpLevelTable, levels?.Count ?? 0),
                (DatTables.SkillName, skills?.Count ?? 0)
            };

            if (counts.All(c => c.Item2 == 0))
                return null;

            var empty = counts.Where(c => c.Item2 == 0).Select(c => c.Item1).ToList();

            if (empty.Count > 0)
                throw new InvalidDataException($"The dat tables are only partly filled in {description}: {string.Join(", ", empty)} missing or empty.");

            var rows = new DatTableRows
            {
                Xp = new DatXpRows { Attribute = attribute, Vital = vital, TrainedSkill = trained, SpecializedSkill = specialized, Levels = levels },
                Skills = skills
            };

            // The vital table is a group of its own like the spell tables: a database from before it existed reads as having none.
            var vitals = Query(context.DatVital, v => new DatVitalRow { Id = v.Id, FormulaW = v.FormulaW, FormulaX = v.FormulaX, FormulaY = v.FormulaY, FormulaZ = v.FormulaZ, FormulaAttr1 = v.FormulaAttr1, FormulaAttr2 = v.FormulaAttr2 });

            if (vitals != null && vitals.Count > 0)
                rows.Vitals = vitals;

            // The title table is a group of its own as well.
            var titles = Query(context.DatCharacterTitle, t => new DatTitleRow { Id = t.Id, Name = t.Name, Text = t.Text });

            if (titles != null && titles.Count > 0)
                rows.Titles = titles;

            // The character generation tables are a group of their own: a database without any of them keeps the record, one with only some is refused.
            var chargen = Query(context.DatChargen, c => new DatChargenRow { Id = c.Id, AttributeCredits = c.AttributeCredits, SkillCredits = c.SkillCredits });
            var overrides = Query(context.DatChargenHeritageSkillOverride, o => new DatChargenHeritageSkillOverrideRow { HeritageGroup = o.HeritageGroup, SkillId = o.SkillId, TrainedCost = o.TrainedCost, SpecializedCost = o.SpecializedCost });
            var templates = Query(context.DatChargenTemplate, t => new DatChargenTemplateRow { HeritageGroup = t.HeritageGroup, Id = t.Id, Name = t.Name, IconId = t.IconId, Title = t.Title, Strength = t.Strength, Endurance = t.Endurance, Coordination = t.Coordination, Quickness = t.Quickness, Focus = t.Focus, Self = t.Self });
            var templateSkills = Query(context.DatChargenTemplateSkill, s => new DatChargenTemplateSkillRow { HeritageGroup = s.HeritageGroup, TemplateId = s.TemplateId, SkillId = s.SkillId, AdvancementClass = s.AdvancementClass });

            // The override table may be empty (no skill has a heritage cost); the others may not.
            if ((chargen?.Count ?? 0) > 0 || (templates?.Count ?? 0) > 0 || (templateSkills?.Count ?? 0) > 0 || (overrides?.Count ?? 0) > 0)
            {
                var missing = new[] { (DatTables.ChargenName, chargen?.Count ?? 0), (DatTables.ChargenTemplateName, templates?.Count ?? 0), (DatTables.ChargenTemplateSkillName, templateSkills?.Count ?? 0) }.Where(c => c.Item2 == 0).Select(c => c.Item1).ToList();

                if (missing.Count > 0)
                    throw new InvalidDataException($"The dat tables are only partly filled in {description}: {string.Join(", ", missing)} missing or empty.");

                if (chargen.Count != 1)
                    throw new InvalidDataException($"{DatTables.ChargenName} must have exactly one row in {description} (it has {chargen.Count}).");

                rows.Chargen = new DatChargenRows { Chargen = chargen[0], Overrides = overrides ?? new List<DatChargenHeritageSkillOverrideRow>(), Templates = templates, TemplateSkills = templateSkills };
            }

            var spellEntities = Query(context.DatSpell, s => s);
            var setSpells = Query(context.DatSpellSetSpell, r => new DatSpellSetSpellRow { SetId = r.SetId, Tier = r.Tier, Position = r.Position, SpellId = r.SpellId });

            // The spell tables are a group of their own: a database from before they existed reads as having none, so the XP and skill tables keep working.
            if ((spellEntities?.Count ?? 0) == 0 && (setSpells?.Count ?? 0) == 0)
                return rows;

            if ((spellEntities?.Count ?? 0) == 0)
                throw new InvalidDataException($"The dat tables are only partly filled in {description}: {DatTables.SpellName} missing or empty.");

            rows.Spells = spellEntities.Select(ToRow).ToList();
            rows.SetSpells = setSpells ?? new List<DatSpellSetSpellRow>();

            return rows;
        }

        private static DatSpellRow ToRow(DatSpell s)
        {
            return new DatSpellRow
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                School = s.School,
                IconId = s.IconId,
                Category = s.Category,
                Bitfield = s.Bitfield,
                BaseMana = s.BaseMana,
                BaseRangeConstant = s.BaseRangeConstant,
                BaseRangeMod = s.BaseRangeMod,
                Power = s.Power,
                SpellEconomyMod = s.SpellEconomyMod,
                FormulaVersion = s.FormulaVersion,
                ComponentLoss = s.ComponentLoss,
                MetaSpellType = s.MetaSpellType,
                MetaSpellId = s.MetaSpellId,
                Duration = s.Duration,
                DegradeModifier = s.DegradeModifier,
                DegradeLimit = s.DegradeLimit,
                PortalLifetime = s.PortalLifetime,
                Components = new[] { s.Component1, s.Component2, s.Component3, s.Component4, s.Component5, s.Component6, s.Component7, s.Component8 },
                CasterEffect = s.CasterEffect,
                TargetEffect = s.TargetEffect,
                FizzleEffect = s.FizzleEffect,
                RecoveryInterval = s.RecoveryInterval,
                RecoveryAmount = s.RecoveryAmount,
                DisplayOrder = s.DisplayOrder,
                NonComponentTargetType = s.NonComponentTargetType,
                ManaMod = s.ManaMod
            };
        }

        // A missing table reads as null; an existing one as a list (possibly empty).
        private static List<TRow> Query<TEntity, TRow>(DbSet<TEntity> set, System.Linq.Expressions.Expression<Func<TEntity, TRow>> row) where TEntity : class
        {
            try
            {
                return set.AsNoTracking().Select(row).ToList();
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
            {
                return null;
            }
        }
    }
}
