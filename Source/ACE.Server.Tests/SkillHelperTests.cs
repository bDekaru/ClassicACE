using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Common;
using ACE.Entity.Enum;

namespace ACE.Server.Tests
{
    [TestClass]
    public class SkillHelperTests
    {
        [DataTestMethod]
        [DataRow(Ruleset.EoR, 38)]
        [DataRow(Ruleset.Infiltration, 36)]
        [DataRow(Ruleset.CustomDM, 38)]
        public void ValidSkillCountPerRuleset(Ruleset ruleset, int expected)
        {
            var skills = SkillHelper.GetValidSkills(ruleset);

            Console.WriteLine($"{ruleset}: {skills.Count} valid skills ({string.Join(", ", skills)})");

            Assert.AreEqual(expected, skills.Count);
            Assert.IsFalse(skills.Contains(Skill.None));
        }
    }
}
