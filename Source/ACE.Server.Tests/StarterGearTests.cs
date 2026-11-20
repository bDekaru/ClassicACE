using System;
using System.IO;

using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Common;
using ACE.Server.Entity;
using ACE.TestSupport;

namespace ACE.Server.Tests
{
    [TestClass]
    public class StarterGearTests
    {
        [TestMethod]
        public void CanParseStarterGearJson()
        {
            TestServerSetup.InitializeConfig(Path.Combine(AppContext.BaseDirectory, "Config.js"), false);

            var starterGearPath = Path.Combine(TestServerSetup.Folder, "starterGear.json");
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                starterGearPath = Path.Combine(TestServerSetup.Folder, "starterGear.infiltration.json");
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                starterGearPath = Path.Combine(TestServerSetup.Folder, "starterGear.customDM.json");
            string contents = File.ReadAllText(starterGearPath);

            StarterGearConfiguration config = JsonSerializer.Deserialize<StarterGearConfiguration>(contents, ConfigManager.SerializerOptions);
        }
    }
}
