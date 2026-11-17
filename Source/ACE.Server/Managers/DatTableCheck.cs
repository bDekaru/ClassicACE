using System;
using System.IO;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;

namespace ACE.Server.Managers
{
    /// <summary>
    /// Compares the dat_* tables of the world database (the authoring source of the XP and skill tables) with the records loaded from the portal dat,
    /// and warns when the dat was built from other values.
    /// </summary>
    public static class DatTableCheck
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void Run()
        {
            try
            {
                DatTableRows rows;

                using (var context = new WorldDbContext())
                    rows = DatTableReader.Read(context, Common.ConfigManager.Config.MySql.World.Database);

                if (rows == null)
                {
                    log.Info("[DATTABLES] The world database has no dat_* tables (or they are empty); the XP and skill tables come from the dat as it is.");
                    return;
                }

                var portal = DatLoader.DatManager.PortalDat;

                var xpMatches = DatTables.Hash(rows.Xp) == DatTables.Hash(DatTables.ToRows(portal.XpTable));
                var skillsMatch = DatTables.Hash(rows.Skills) == DatTables.Hash(DatTables.ToRows(portal.SkillTable));

                if (xpMatches && skillsMatch)
                    log.Info("[DATTABLES] The portal dat's XP and skill tables match the world database.");
                else
                    log.Warn($"[DATTABLES] The portal dat is out of date with the world database dat_* tables (different: {(xpMatches ? "" : "XP table")}{(!xpMatches && !skillsMatch ? ", " : "")}{(skillsMatch ? "" : "skill table")}). The server uses the dat's values; rebuild the dat (ACE.DatBuilder) and restart to apply the database values.");
            }
            catch (InvalidDataException ex)
            {
                log.Warn($"[DATTABLES] {ex.Message}");
            }
            catch (Exception ex)
            {
                log.Warn($"[DATTABLES] The dat tables could not be compared with the dat: {ex.Message}");
            }
        }
    }
}
