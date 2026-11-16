using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    [TestClass]
    public class DatBuildRunnerTests
    {
        private static readonly string SourceDats = Environment.GetEnvironmentVariable("ACE_TEST_SOURCE_DATS") ?? @"D:\Programming\Asheron's Call\ACEmulator\Dats\Source";

        private static string TempFolder()
        {
            var folder = Path.Combine(Path.GetTempPath(), $"DatBuildRunnerTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void RequireInputs(DatBuildRecipe recipe)
        {
            if (!File.Exists(Path.Combine(SourceDats, recipe.BaseDat)))
                Assert.Inconclusive($"Test dat not found: {Path.Combine(SourceDats, recipe.BaseDat)}");

            foreach (var folder in recipe.ResolveFolders(Path.Combine(SourceDats, "Custom Files")))
                if (!Directory.Exists(folder))
                    Assert.Inconclusive($"Custom files not found: {folder}");
        }

        [TestMethod]
        public void Build_LanguageCustomDM_WritesAuditedDatAndKeepsSource()
        {
            var recipe = DatBuildRecipes.LanguageCustomDM;
            RequireInputs(recipe);

            var sourceSize = new FileInfo(Path.Combine(SourceDats, recipe.BaseDat)).Length;
            var sourceTime = File.GetLastWriteTimeUtc(Path.Combine(SourceDats, recipe.BaseDat));
            var outRoot = TempFolder();

            try
            {
                var result = DatBuildRunner.Build(recipe, SourceDats, Path.Combine(SourceDats, "Custom Files"), outRoot);

                Assert.AreEqual(Path.Combine(outRoot, "CustomDM", "client_local_English.dat"), result.OutputPath);
                Assert.IsTrue(File.Exists(result.OutputPath));
                Assert.IsFalse(File.Exists(result.OutputPath + ".building"));
                Assert.AreEqual(result.AuditBefore.Records + result.Overlay.Added, result.AuditAfter.Records);
                Assert.IsTrue(result.Overlay.Written > 0);

                using (var writer = new DatWriter(result.OutputPath))
                    writer.Audit();

                Assert.AreEqual(sourceSize, new FileInfo(Path.Combine(SourceDats, recipe.BaseDat)).Length);
                Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(Path.Combine(SourceDats, recipe.BaseDat)));
            }
            finally
            {
                Directory.Delete(outRoot, true);
            }
        }

        [TestMethod]
        public void Build_Language_WritesTheReleaseIterationRecordAndReportsTheHash()
        {
            foreach (var recipe in new[] { DatBuildRecipes.LanguageCustomDM, DatBuildRecipes.LanguageInfiltration })
            {
                RequireInputs(recipe);

                var outRoot = TempFolder();

                try
                {
                    var result = DatBuildRunner.Build(recipe, SourceDats, Path.Combine(SourceDats, "Custom Files"), outRoot);

                    Assert.AreEqual(recipe.Releases.CurrentNumber, result.ReleaseNumber);
                    Assert.AreEqual((int)recipe.Releases.CurrentNumber, new DatDatabase(result.OutputPath).Iteration);
                    Assert.AreEqual(DatReleaseHash.Compute(result.OutputPath), result.ReleaseHash);
                }
                finally
                {
                    Directory.Delete(outRoot, true);
                }
            }
        }

        [TestMethod]
        public void Build_Language_ReplacedRecordsKeepTheVersionAndGetTheStepDateAndRelease()
        {
            foreach (var recipe in new[] { DatBuildRecipes.LanguageCustomDM, DatBuildRecipes.LanguageInfiltration })
            {
                RequireInputs(recipe);

                var outRoot = TempFolder();

                try
                {
                    var result = DatBuildRunner.Build(recipe, SourceDats, Path.Combine(SourceDats, "Custom Files"), outRoot);
                    var copy = Path.Combine(outRoot, "source.dat");
                    File.Copy(Path.Combine(SourceDats, recipe.BaseDat), copy);

                    using (var original = new DatWriter(copy))
                    using (var built = new DatWriter(result.OutputPath))
                    {
                        var compared = 0;

                        foreach (var id in new uint[] { 0x21000046, 0x23000002 })
                        {
                            Assert.IsTrue(original.TryGetEntryInfo(id, out var v0, out _, out _));
                            Assert.IsTrue(built.TryGetEntryInfo(id, out var v1, out var i1, out var d1));
                            Assert.AreEqual(v0, v1, $"{recipe.Name} {id:X8} version");
                            Assert.AreEqual(recipe.Releases.CurrentNumber, i1, $"{recipe.Name} {id:X8} iteration");
                            Assert.AreEqual(DatStepDate.Parse(DatBuildRecipes.InitialVersionDate), d1, $"{recipe.Name} {id:X8} date");
                            Assert.AreNotEqual((ushort)0, v1);
                            compared++;
                        }

                        Assert.AreEqual(2, compared);
                    }
                }
                finally
                {
                    Directory.Delete(outRoot, true);
                }
            }
        }

        [TestMethod]
        public void Build_RefusesExistingOutputWithoutForce_AndOutputInsideSource()
        {
            var recipe = DatBuildRecipes.LanguageInfiltration;
            RequireInputs(recipe);

            var outRoot = TempFolder();

            try
            {
                var custom = Path.Combine(SourceDats, "Custom Files");
                DatBuildRunner.Build(recipe, SourceDats, custom, outRoot);

                Assert.ThrowsExactly<ArgumentException>(() => DatBuildRunner.Build(recipe, SourceDats, custom, outRoot));

                var again = DatBuildRunner.Build(recipe, SourceDats, custom, outRoot, true);
                Assert.IsTrue(File.Exists(again.OutputPath));

                Assert.ThrowsExactly<ArgumentException>(() => DatBuildRunner.Build(recipe, SourceDats, custom, Path.Combine(SourceDats, "Build")));
                Assert.IsFalse(Directory.Exists(Path.Combine(SourceDats, "Build")));
            }
            finally
            {
                Directory.Delete(outRoot, true);
            }
        }

        [TestMethod]
        public void Build_MissingBaseDat_Throws()
        {
            var outRoot = TempFolder();

            try
            {
                Assert.ThrowsExactly<ArgumentException>(() => DatBuildRunner.Build(DatBuildRecipes.LanguageCustomDM, outRoot, outRoot, Path.Combine(outRoot, "out")));
            }
            finally
            {
                Directory.Delete(outRoot, true);
            }
        }
    }
}
