using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatBuildingGroupsTests
    {
        private static readonly string SourceDats = TestSourceDats.Folder;

        private static DatBuildingGroups LoadGroups()
        {
            var path = Path.Combine(SourceDats, DatBuildRecipes.PreToDCellDat);

            if (!File.Exists(path))
                Assert.Inconclusive($"Source dat not found: {path}");

            return new DatBuildingGroups(new DatDatabase(path, true));
        }

        [TestMethod]
        public void Resolve_GroupsAModelWithTheEnvironmentsOfItsCells()
        {
            var group = LoadGroups().Resolve(0x01002EF1);

            CollectionAssert.Contains(group.Models.ToList(), 0x01002EF1u);
            CollectionAssert.Contains(group.Environments.ToList(), 0x0D000574u);
            Assert.IsTrue(group.Buildings >= 4);
        }

        [TestMethod]
        public void Resolve_AModelWithoutCellsHasNoEnvironments()
        {
            var group = LoadGroups().Resolve(0x01002EF6);

            CollectionAssert.AreEqual(new[] { 0x01002EF6u }, group.Models.ToList());
            Assert.AreEqual(0, group.Environments.Count);
        }

        [TestMethod]
        public void Resolve_EveryReshapedEnvironmentBelongsToAReplacedBuilding()
        {
            var groups = LoadGroups();
            var environments = new[] { "01000830", "01002EF1", "01002EF2", "01002EF6", "010034FA" }
                .SelectMany(m => groups.Resolve(Convert.ToUInt32(m, 16)).Environments)
                .ToList();

            foreach (var reshaped in new[] { 0x0D0002FBu, 0x0D000574u, 0x0D000575u, 0x0D0005BFu })
                CollectionAssert.Contains(environments, reshaped);
        }
    }
}
