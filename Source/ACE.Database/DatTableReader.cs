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

            return new DatTableRows
            {
                Xp = new DatXpRows { Attribute = attribute, Vital = vital, TrainedSkill = trained, SpecializedSkill = specialized, Levels = levels },
                Skills = skills
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
