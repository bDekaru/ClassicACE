using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatPreToDImportTests
    {
        [TestMethod]
        public void EmbeddedSurfaceMap_HasAllRows()
        {
            var map = DatCellConverter.LoadEmbeddedSurfaceMap(DatBuildRecipes.PreToDSurfaceMap);

            Assert.AreEqual(686, map.Count);
            Assert.AreEqual(0x0139, map[0x0077]);
        }

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

        [TestMethod]
        public void PortalRecipes_ImportFromThePreToDDat()
        {
            Assert.IsNotNull(DatBuildRecipes.PortalCustomDM.PreToDImport);
            Assert.IsNotNull(DatBuildRecipes.PortalInfiltration.PreToDImport);
            Assert.IsNull(DatBuildRecipes.LanguageCustomDM.PreToDImport);
        }
    }
}
