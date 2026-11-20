using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class SpellFormulaTests
    {
        private const uint LowestTaper = 63;
        private const uint HighestTaper = 74;
        private const uint Banyan = 190;

        private static SpellTable BuildTable(params uint[] formula)
        {
            var table = new SpellTable();
            table.Spells[1] = new SpellBase { FormulaVersion = 4, Formula = formula.ToList() };
            return table;
        }

        [TestMethod]
        public void Version4_KeepsRealComponentsAndFillsTheThreeTaperSlots()
        {
            var table = BuildTable(2, LowestTaper, LowestTaper, 22, 31, LowestTaper, Banyan);

            foreach (var account in new[] { "", "a", "claude", "claude2", "SomeLongAccountName" })
            {
                var formula = SpellTable.GetSpellFormula(table, 1, account);

                Assert.AreEqual(7, formula.Count);
                Assert.AreEqual(2u, formula[0]);
                Assert.AreEqual(22u, formula[3]);
                Assert.AreEqual(31u, formula[4]);
                Assert.AreEqual(Banyan, formula[6]);

                foreach (var slot in new[] { 1, 2, 5 })
                    Assert.IsTrue(formula[slot] >= LowestTaper && formula[slot] <= HighestTaper, $"slot {slot} = {formula[slot]} for '{account}'");
            }
        }

        [TestMethod]
        public void Version4_MatchesTheClientPatchVectors()
        {
            var table = BuildTable(2, LowestTaper, LowestTaper, 22, 31, LowestTaper, Banyan);

            CollectionAssert.AreEqual(new uint[] { 2, 65, 65, 22, 31, 64, Banyan }, SpellTable.GetSpellFormula(table, 1, "claude"));
            CollectionAssert.AreEqual(new uint[] { 2, 68, 65, 22, 31, 69, Banyan }, SpellTable.GetSpellFormula(table, 1, "claude2"));
        }

        [TestMethod]
        public void Version4_IsStablePerAccountAndVariesBetweenAccounts()
        {
            var table = BuildTable(4, LowestTaper, LowestTaper, 20, 27, LowestTaper, Banyan);

            var first = SpellTable.GetSpellFormula(table, 1, "claude");
            var again = SpellTable.GetSpellFormula(table, 1, "claude");
            CollectionAssert.AreEqual(first, again);

            var distinct = new HashSet<string>();
            for (var i = 0; i < 200; i++)
                distinct.Add(string.Join(",", SpellTable.GetSpellFormula(table, 1, "account" + i)));

            Assert.IsTrue(distinct.Count > 20, $"only {distinct.Count} distinct taper layouts");
        }
    }
}
