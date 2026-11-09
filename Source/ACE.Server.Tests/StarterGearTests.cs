using System;
using System.IO;

using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Common;
using ACE.Server.Entity;

namespace ACE.Server.Tests
{
    [TestClass]
    public class StarterGearTests
    {
        [TestMethod]
        public void CanParseStarterGearJson()
        {
            var testDir = AppContext.BaseDirectory;
            var starterGearPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "ACE.Server", "starterGear.json"));
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                starterGearPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "ACE.Server", "starterGear.infiltration.json"));
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                starterGearPath = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "ACE.Server", "starterGear.customDM.json"));
            string contents = File.ReadAllText(starterGearPath);

            StarterGearConfiguration config = JsonSerializer.Deserialize<StarterGearConfiguration>(contents, ConfigManager.SerializerOptions);
        }
    }
}
