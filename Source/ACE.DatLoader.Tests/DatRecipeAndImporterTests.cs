using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatRecipeAndImporterTests
    {
        [TestMethod]
        public void ParseIds_ExpandsRangesAndSingles()
        {
            var ids = DatPortalImporter.ParseIds("0D000418-0D00041B,0100281E").ToArray();

            CollectionAssert.AreEqual(new uint[] { 0x0D000418, 0x0D000419, 0x0D00041A, 0x0D00041B, 0x0100281E }, ids);
            Assert.AreEqual(0, DatPortalImporter.ParseIds(null).Count());
        }

        [TestMethod]
        public void CellRecipes_FollowTheirRulesetsPortal()
        {
            Assert.AreSame(DatBuildRecipes.PortalCustomDM, DatBuildRecipes.CellCustomDM.Portal);
            Assert.AreSame(DatBuildRecipes.PortalInfiltration, DatBuildRecipes.CellInfiltration.Portal);
            Assert.AreEqual("cell-customdm", DatBuildRecipes.CellCustomDM.Key);
        }
    }
}
