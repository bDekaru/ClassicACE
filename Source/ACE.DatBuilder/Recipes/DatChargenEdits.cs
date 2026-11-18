using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>Edits of a character generation record (0x0E000002), read from a recipe data file.</summary>
    public class DatChargenEdits
    {
        /// <summary>New names of start areas, by index.</summary>
        public Dictionary<int, string> StartAreaNames { get; set; }

        /// <summary>The skill credits every heritage group gets.</summary>
        public uint? SkillCredits { get; set; }

        /// <summary>The attribute credits of the heritage groups of those names.</summary>
        public Dictionary<string, uint> HeritageAttributeCredits { get; set; }

        /// <summary>Replaces a heritage group's own skill costs, by heritage name.</summary>
        public Dictionary<string, List<ChargenHeritageSkill>> HeritageSkills { get; set; }

        /// <summary>Replaces the skill lists (and optionally the attributes) of the templates of that name; later entries override earlier ones.</summary>
        public List<ChargenTemplateEdit> Templates { get; set; }

        public class ChargenHeritageSkill
        {
            public Skill Skill { get; set; }
            public int Normal { get; set; }
            public int Primary { get; set; }
        }

        public class ChargenTemplateEdit
        {
            public string Name { get; set; }

            /// <summary>Only the heritage groups of these names (all when empty).</summary>
            public List<string> Heritages { get; set; }

            /// <summary>All heritage groups except those of these names.</summary>
            public List<string> ExceptHeritages { get; set; }

            public List<Skill> Primary { get; set; }
            public List<Skill> Normal { get; set; }

            public uint? Strength { get; set; }
            public uint? Endurance { get; set; }
            public uint? Coordination { get; set; }
            public uint? Quickness { get; set; }
            public uint? Focus { get; set; }
            public uint? Self { get; set; }
        }

        /// <summary>
        /// The edits that turn <paramref name="source"/> into <paramref name="target"/> (both with the same start areas, heritage groups and templates). Heritage groups that get the same template edit share one entry.
        /// </summary>
        /// <exception cref="InvalidDataException">The skill credits are not the same in all target heritage groups, or a heritage group has two templates of one name.</exception>
        public static DatChargenEdits Diff(CharGen source, CharGen target)
        {
            var edits = new DatChargenEdits();

            for (var i = 0; i < target.StarterAreas.Count; i++)
                if (source.StarterAreas[i].Name != target.StarterAreas[i].Name)
                    (edits.StartAreaNames ??= new Dictionary<int, string>())[i] = target.StarterAreas[i].Name;

            var credits = target.HeritageGroups.Values.Select(h => h.SkillCredits).Distinct().ToList();

            if (credits.Count > 1)
                throw new InvalidDataException("Chargen diff: the target heritage groups have different skill credits.");

            if (credits.Count == 1 && source.HeritageGroups.Values.Any(h => h.SkillCredits != credits[0]))
                edits.SkillCredits = credits[0];

            var templateEdits = new List<(ChargenTemplateEdit Edit, string Heritage)>();

            foreach (var key in target.HeritageGroups.Keys)
            {
                var from = source.HeritageGroups[key];
                var to = target.HeritageGroups[key];

                if (from.AttributeCredits != to.AttributeCredits)
                    (edits.HeritageAttributeCredits ??= new Dictionary<string, uint>())[to.Name] = to.AttributeCredits;

                if (!from.Skills.Select(s => (s.SkillNum, s.NormalCost, s.PrimaryCost)).SequenceEqual(to.Skills.Select(s => (s.SkillNum, s.NormalCost, s.PrimaryCost))))
                    (edits.HeritageSkills ??= new Dictionary<string, List<ChargenHeritageSkill>>())[to.Name] = to.Skills.Select(s => new ChargenHeritageSkill { Skill = (Skill)s.SkillNum, Normal = s.NormalCost, Primary = s.PrimaryCost }).ToList();

                if (to.Templates.Select(t => t.Name).Distinct().Count() != to.Templates.Count)
                    throw new InvalidDataException($"Chargen diff: heritage group {to.Name} has two templates of one name.");

                foreach (var template in to.Templates)
                {
                    var old = from.Templates.Single(t => t.Name == template.Name);
                    var edit = new ChargenTemplateEdit { Name = template.Name };

                    if (!old.PrimarySkillsList.SequenceEqual(template.PrimarySkillsList))
                        edit.Primary = template.PrimarySkillsList.Select(s => (Skill)s).ToList();

                    if (!old.NormalSkillsList.SequenceEqual(template.NormalSkillsList))
                        edit.Normal = template.NormalSkillsList.Select(s => (Skill)s).ToList();

                    edit.Strength = template.Strength != old.Strength ? template.Strength : (uint?)null;
                    edit.Endurance = template.Endurance != old.Endurance ? template.Endurance : (uint?)null;
                    edit.Coordination = template.Coordination != old.Coordination ? template.Coordination : (uint?)null;
                    edit.Quickness = template.Quickness != old.Quickness ? template.Quickness : (uint?)null;
                    edit.Focus = template.Focus != old.Focus ? template.Focus : (uint?)null;
                    edit.Self = template.Self != old.Self ? template.Self : (uint?)null;

                    if (edit.Primary != null || edit.Normal != null || edit.Strength != null || edit.Endurance != null || edit.Coordination != null || edit.Quickness != null || edit.Focus != null || edit.Self != null)
                        templateEdits.Add((edit, to.Name));
                }
            }

            var allHeritages = target.HeritageGroups.Values.Select(h => h.Name).ToList();
            var groups = templateEdits.GroupBy(e => System.Text.Json.JsonSerializer.Serialize(e.Edit));

            foreach (var group in groups)
            {
                var edit = group.First().Edit;
                var heritages = group.Select(e => e.Heritage).ToList();

                if (heritages.Count != allHeritages.Count || !heritages.OrderBy(h => h).SequenceEqual(allHeritages.OrderBy(h => h)))
                    edit.Heritages = heritages;

                (edits.Templates ??= new List<ChargenTemplateEdit>()).Add(edit);
            }

            return edits;
        }

        /// <exception cref="InvalidDataException">An edit names a start area, heritage group or template the record does not have.</exception>
        public void Apply(CharGen chargen)
        {
            if (StartAreaNames != null)
                foreach (var area in StartAreaNames)
                {
                    if (area.Key < 0 || area.Key >= chargen.StarterAreas.Count)
                        throw new InvalidDataException($"Chargen edit: there is no start area {area.Key}.");

                    chargen.StarterAreas[area.Key].Name = area.Value;
                }

            foreach (var heritage in chargen.HeritageGroups.Values)
                if (SkillCredits != null)
                    heritage.SkillCredits = SkillCredits.Value;

            if (HeritageAttributeCredits != null)
                foreach (var entry in HeritageAttributeCredits)
                    (chargen.HeritageGroups.Values.FirstOrDefault(h => h.Name == entry.Key) ?? throw new InvalidDataException($"Chargen edit: there is no heritage group {entry.Key}.")).AttributeCredits = entry.Value;

            if (HeritageSkills != null)
                foreach (var entry in HeritageSkills)
                {
                    var heritage = chargen.HeritageGroups.Values.FirstOrDefault(h => h.Name == entry.Key) ?? throw new InvalidDataException($"Chargen edit: there is no heritage group {entry.Key}.");

                    heritage.Skills.Clear();

                    foreach (var skill in entry.Value)
                        heritage.Skills.Add(new SkillCG { SkillNum = (uint)skill.Skill, NormalCost = skill.Normal, PrimaryCost = skill.Primary });
                }

            if (Templates == null)
                return;

            foreach (var edit in Templates)
            {
                var matched = false;

                foreach (var heritage in chargen.HeritageGroups.Values)
                {
                    if (edit.Heritages != null && edit.Heritages.Count > 0 && !edit.Heritages.Contains(heritage.Name))
                        continue;

                    if (edit.ExceptHeritages != null && edit.ExceptHeritages.Contains(heritage.Name))
                        continue;

                    foreach (var template in heritage.Templates.Where(t => t.Name == edit.Name))
                    {
                        matched = true;

                        if (edit.Primary != null)
                        {
                            template.PrimarySkillsList.Clear();
                            template.PrimarySkillsList.AddRange(edit.Primary.Select(s => (uint)s));
                        }

                        if (edit.Normal != null)
                        {
                            template.NormalSkillsList.Clear();
                            template.NormalSkillsList.AddRange(edit.Normal.Select(s => (uint)s));
                        }

                        template.Strength = edit.Strength ?? template.Strength;
                        template.Endurance = edit.Endurance ?? template.Endurance;
                        template.Coordination = edit.Coordination ?? template.Coordination;
                        template.Quickness = edit.Quickness ?? template.Quickness;
                        template.Focus = edit.Focus ?? template.Focus;
                        template.Self = edit.Self ?? template.Self;
                    }
                }

                if (!matched)
                    throw new InvalidDataException($"Chargen edit: no template named {edit.Name} in the selected heritage groups.");
            }
        }
    }
}
