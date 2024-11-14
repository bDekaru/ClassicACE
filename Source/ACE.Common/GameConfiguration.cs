using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ACE.Common
{
    public enum Ruleset
    {
        Invalid,
        //Release,                // November 1999
        //DarkMajesty,            // October 2001
        CustomDM,
        Infiltration,           // February 2005
        //ThroneOfDestiny,        // July 2005
        //FromDarknessLight,      // November 2010 - Void Magic
        //MasterOfDesign,         // November 2011 - Innate Augmentations
        //MasterOfArms,           // February 2012 - New weapon skills
        //BalanceOfPower,         // February 2013 - Summoning
        EoR                     // May 2015
    }

    public class GameConfiguration
    {
        public string WorldName { get; set; } = "ACEmulator";

        public NetworkSettings Network { get; set; } = new NetworkSettings();

        public AccountDefaults Accounts { get; set; } = new AccountDefaults();

        public string DatFilesDirectory { get; set; } = "c:\\ACE\\Dats\\";

        public string ModsDirectory { get; set; }

        /// <summary>
        /// The amount of seconds to wait before turning off the server. Default value is 60 (for 1 minute).
        /// </summary>
        public uint ShutdownInterval { get; set; } = 60;

        public bool ServerPerformanceMonitorAutoStart { get; set; } = false;

        public ThreadConfiguration Threading { get; set; } = new ThreadConfiguration();

        /// <summary>
        /// The amount of minutes to keep a player object from shard database in memory. Default value is 31 minutes.
        /// </summary>
        public uint ShardPlayerBiotaCacheTime { get; set; } = 31;

        /// <summary>
        /// The amount of minutes to keep a non player object from shard database in memory. Default value is 11 minutes.
        /// </summary>
        public uint ShardNonPlayerBiotaCacheTime { get; set; } = 11;

        public bool WorldDatabasePrecaching { get; set; } = false;

        public bool LandblockPreloading { get; set; } = true;

        public List<PreloadedLandblocks> PreloadedLandblocks { get; set; } = new List<PreloadedLandblocks>()
        {
            new PreloadedLandblocks()
            {
                Id                  = "E74EFFFF",
                Description         = "Hebian-To (Global Events)",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = true
            },
            new PreloadedLandblocks()
            {
                Id                  = "A9B4FFFF",
                Description         = "Holtburg",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "DA55FFFF",
                Description         = "Shoushi",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "7D64FFFF",
                Description         = "Yaraq",
                Permaload           = true,
                IncludeAdjacents    = true,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "0007FFFF",
                Description         = "Town Network",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = false
            },
            new PreloadedLandblocks()
            {
                Id                  = "00000000",
                Description         = "Apartment Landblocks",
                Permaload           = true,
                IncludeAdjacents    = false,
                Enabled             = false
            }
        };

        public bool IsTestWorld { get; set; } = false;

        /// <summary>
        /// Determines which era's ruleset to use.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Ruleset WorldRuleset { get; set; } = Ruleset.Infiltration;
    }
}
