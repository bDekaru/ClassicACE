using System.IO;

using Newtonsoft.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using ACE.Server.Entity;

namespace ACE.Server.Tests
{
    [TestClass]
    public class StarterGearTests
    {
        [TestMethod]
        public void CanParseStarterGearJson()
        {
            string contents = File.ReadAllText("../../../../../ACE.Server/starterGear.json");
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                contents = File.ReadAllText("../../../../../ACE.Server/starterGear.infiltration.json");
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                contents = File.ReadAllText("../../../../../ACE.Server/starterGear.customDM.json");

            StarterGearConfiguration config = JsonConvert.DeserializeObject<StarterGearConfiguration>(contents);
        }
    }
}
