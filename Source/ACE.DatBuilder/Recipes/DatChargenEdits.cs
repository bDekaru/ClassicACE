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
