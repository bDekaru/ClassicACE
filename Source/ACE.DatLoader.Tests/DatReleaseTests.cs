using System;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.DatBuilder;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatReleaseTests
    {
        private static readonly string SourceDats = TestSourceDats.Folder;

        private const string GuardVariable = "ACE_TEST_RELEASE_GUARD";

        private static DatReleaseSet SampleSet(params DatRelease[] releases)
        {
            return new DatReleaseSet("CustomDM", "Portal", 20000, 29999, "2026-11-16", releases);
        }

        [TestMethod]
        public void StepDate_ParsesDatesAndTimes_AndCutoffsTakeInTheWholeDayOrMinute()
        {
            Assert.AreEqual(1794787200u, DatStepDate.Parse("2026-11-16"));
            Assert.AreEqual(1794787200u + 13 * 3600 + 30 * 60, DatStepDate.Parse("2026-11-16 13:30"));
            Assert.AreEqual(1794787200u + 86399, DatStepDate.ParseCutoff("2026-11-16"));
            Assert.AreEqual(1794787200u + 13 * 3600 + 30 * 60 + 59, DatStepDate.ParseCutoff("2026-11-16 13:30"));
            Assert.ThrowsExactly<ArgumentException>(() => DatStepDate.Parse("16.11.2026"));
        }

        [TestMethod]
        public void IterationForDate_ReturnsTheReleaseTheDateFallsIn()
        {
            var set = SampleSet(new DatRelease(20045, "2026-11-16", "AA"), new DatRelease(20046, "2026-12-01"));

            Assert.AreEqual(0u, set.IterationForDate(DatStepDate.Parse("2026-11-15")));
            Assert.AreEqual(20045u, set.IterationForDate(DatStepDate.Parse("2026-11-16")));
            Assert.AreEqual(20045u, set.IterationForDate(DatStepDate.Parse("2026-11-16 23:59")));
            Assert.AreEqual(20046u, set.IterationForDate(DatStepDate.Parse("2026-11-17")));
            Assert.AreEqual(20046u, set.IterationForDate(DatStepDate.Parse("2026-12-01")));
            Assert.ThrowsExactly<InvalidOperationException>(() => set.IterationForDate(DatStepDate.Parse("2026-12-02")));
        }

        [TestMethod]
        public void Validation_RejectsNumbersOutsideTheBlock_DisorderedReleasesAndAnUnlockedEarlierRelease()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(19999, "2026-11-16")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(30000, "2026-11-16")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(20045, "2026-11-15")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(20045, "2026-11-16", "AA"), new DatRelease(20045, "2026-12-01")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(20045, "2026-11-16", "AA"), new DatRelease(20046, "2026-11-16")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet(new DatRelease(20045, "2026-11-16"), new DatRelease(20046, "2026-12-01")));
            Assert.ThrowsExactly<InvalidDataException>(() => SampleSet());
        }

        [TestMethod]
        public void Classify_SeparatesPatchableClientsFromRetailOtherRulesetsAndNewerDats()
        {
            var set = DatReleases.CustomDMPortal;
            var total = set.CurrentNumber;

            Assert.AreEqual(DatClientStatus.UpToDate, set.Classify(total, total));
            Assert.AreEqual(DatClientStatus.Patchable, set.Classify(total, total + 3));
            Assert.AreEqual(DatClientStatus.TooOld, set.Classify(2072, total));
            Assert.AreEqual(DatClientStatus.TooOld, set.Classify(19980, total));
            Assert.AreEqual(DatClientStatus.TooOld, set.Classify(DatReleases.InfiltrationPortal.CurrentNumber, total));
            Assert.AreEqual(DatClientStatus.NewerThanServer, set.Classify(total + 1, total));
            Assert.AreEqual(DatClientStatus.OtherRuleset, set.Classify(30002, total));
            Assert.AreEqual(DatClientStatus.OtherRuleset, set.Classify(40001, total));
        }

        [TestMethod]
        public void ReleaseTables_ValidAndInsideTheirRulesetBlocks()
        {
            foreach (var set in DatReleases.All)
            {
                set.Validate();

                if (set.Ruleset == "Infiltration")
                    Assert.IsTrue(set.BlockStart == 10000 && set.BlockEnd == 19999, set.Name);
                else
                    Assert.IsTrue(set.BlockStart == 20000 && set.BlockEnd == 29999, set.Name);

                foreach (var release in set.Releases)
                    Assert.IsTrue(release.Number >= set.BlockStart && release.Number <= set.BlockEnd, $"{set.Name} {release.Number}");
            }

            Assert.AreEqual(6, DatReleases.All.Select(s => s.Name).Distinct().Count());
        }

        [TestMethod]
        public void Recipes_HaveDatesInOrder_AndTheirRulesetsReleaseTable()
        {
            foreach (var recipe in DatBuildRecipes.All)
            {
                recipe.ValidateDates();
                Assert.AreEqual(recipe.Ruleset, recipe.Releases.Ruleset, recipe.Name);
                Assert.IsNotNull(DatReleases.Find(recipe.Ruleset, recipe.Name.StartsWith("Portal") ? DatReleases.Portal : DatReleases.Language), recipe.Name);
            }

            foreach (var recipe in DatBuildRecipes.AllCells)
            {
                Assert.AreEqual(recipe.Ruleset, recipe.Releases.Ruleset, recipe.Name);
                Assert.AreEqual(DatReleases.Cell, recipe.Releases.Dat, recipe.Name);
                Assert.IsTrue(DatStepDate.Parse(recipe.ConversionDate) >= recipe.Releases.StartTime, recipe.Name);
            }
        }

        [TestMethod]
        public void Recipe_StepDatedBeforeAnEarlierStep_IsRejected()
        {
            var recipe = new DatBuildRecipe("Test", "CustomDM", DatBuildRecipes.LanguageBaseDat, DatOverlay.IsLanguageId, DatReleases.CustomDMLanguage,
                new DatBuildStep("Language/One", "2026-11-17"),
                new DatBuildStep("Language/Two", "2026-11-16"));

            Assert.ThrowsExactly<InvalidDataException>(() => recipe.ValidateDates());

            var early = new DatBuildRecipe("Test", "CustomDM", DatBuildRecipes.LanguageBaseDat, DatOverlay.IsLanguageId, DatReleases.CustomDMLanguage,
                new DatBuildStep("Language/One", "2026-11-15"));

            Assert.ThrowsExactly<InvalidDataException>(() => early.ValidateDates());
        }

        [TestMethod]
        public void MapEditor_RequiresAStepBeforeAnEdit_AndStepsRunInDateOrder()
        {
            var store = new DatCellStore(new System.Collections.Generic.SortedDictionary<uint, byte[]>());
            var editor = new DatMapEditor(store);

            store.RequireDate = true;
            Assert.ThrowsExactly<InvalidOperationException>(() => store.Set(0x12340100, new byte[] { 1 }));

            editor.Step("2026-11-16");
            store.Set(0x12340100, new byte[] { 1 });
            Assert.AreEqual(DatStepDate.Parse("2026-11-16"), store.DateOf(0x12340100));

            editor.Step("2026-12-01");
            store.Set(0x12340100, new byte[] { 1 });
            Assert.AreEqual(DatStepDate.Parse("2026-11-16"), store.DateOf(0x12340100), "an edit that changes nothing keeps the old date");

            store.Set(0x12340100, new byte[] { 2 });
            Assert.AreEqual(DatStepDate.Parse("2026-12-01"), store.DateOf(0x12340100));

            Assert.ThrowsExactly<ArgumentException>(() => editor.Step("2026-11-20"));
        }

        /// <summary>
        /// The release guard: a locked latest release must still build to its hash. While it is unlocked the test only reports the hash to put in the table.
        /// A full build of every dat takes about 6 minutes, so it runs only when <c>ACE_TEST_RELEASE_GUARD</c> is set.
        /// </summary>
        [TestMethod]
        public void ReleaseGuard_BuildsMatchTheLockedHashes()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(GuardVariable)))
                Assert.Inconclusive($"Set {GuardVariable}=1 to build every dat and compare the hashes (about 6 minutes).");

            if (!Directory.Exists(Path.Combine(SourceDats, "Custom Files")))
                Assert.Inconclusive($"Custom files not found: {Path.Combine(SourceDats, "Custom Files")}");

            var outRoot = TestSourceDats.TempPath("DatReleaseGuard");
            var custom = Path.Combine(SourceDats, "Custom Files");
            var mismatches = new System.Text.StringBuilder();

            try
            {
                foreach (var recipe in DatBuildRecipes.All)
                    Check(recipe.Releases, DatBuildRunner.Build(recipe, SourceDats, custom, outRoot).ReleaseHash, mismatches);

                foreach (var recipe in DatBuildRecipes.AllCells)
                    Check(recipe.Releases, DatCellBuildRunner.Build(recipe, SourceDats, outRoot).ReleaseHash, mismatches);
            }
            finally
            {
                if (Directory.Exists(outRoot))
                    Directory.Delete(outRoot, true);
            }

            Assert.AreEqual("", mismatches.ToString());
        }

        private static void Check(DatReleaseSet set, string hash, System.Text.StringBuilder mismatches)
        {
            Console.WriteLine($"{set.Name} release {set.CurrentNumber}: {(set.Current.Locked ? "locked" : "unlocked")}, built hash {hash}");

            if (set.Current.Locked && set.Current.Hash != hash)
                mismatches.AppendLine($"{set.Name} release {set.CurrentNumber} is locked with hash {set.Current.Hash} but the build gives {hash}: the released content changed (add a new dated step and release instead of editing the old one).");
        }
    }
}
