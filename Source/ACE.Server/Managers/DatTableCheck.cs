using System;
using System.Collections.Generic;
using System.IO;

using log4net;

using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

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

                var spellsMatch = rows.Spells == null || DatTables.Hash(rows.Spells, rows.SetSpells) == DatTables.Hash(DatTables.ToRows(portal.SpellTable), DatTables.ToSetRows(portal.SpellTable));

                var vitalsMatch = rows.Vitals == null || DatTables.Hash(rows.Vitals) == DatTables.Hash(DatTables.ToRows(portal.SecondaryAttributeTable));

                var titlesMatch = rows.Titles == null || DatTables.Hash(rows.Titles) == DatTables.Hash(DatTables.ToRows(portal.ReadFromDat<EnumMapper>(DatTables.TitleMapperId), DatLoader.DatManager.LanguageDat.CharacterTitles));

                var chargenMatch = rows.Chargen == null || (DatTables.Hash(rows.Chargen) == DatTables.Hash(DatTables.ToRows(portal.CharGen)) && DatTables.CreditsApplied(portal.CharGen, rows.Chargen.Chargen));

                var different = new List<string>();

                if (!xpMatches)
                    different.Add("XP table");

                if (!skillsMatch)
                    different.Add("skill table");

                if (!spellsMatch)
                    different.Add("spell table");

                if (!vitalsMatch)
                    different.Add("vital table");

                if (!titlesMatch)
                    different.Add("title tables");

                if (!chargenMatch)
                    different.Add("character generation tables");

                if (different.Count == 0)
                {
                    var matched = new List<string> { "XP", "skill" };

                    if (rows.Vitals != null)
                        matched.Add("vital");

                    if (rows.Spells != null)
                        matched.Add("spell");

                    if (rows.Titles != null)
                        matched.Add("title");

                    if (rows.Chargen != null)
                        matched.Add("character generation");

                    log.Info($"[DATTABLES] The portal dat's {string.Join(", ", matched)} tables match the world database.");
                }
                else
                    log.Warn($"[DATTABLES] The portal dat is out of date with the world database dat_* tables (different: {string.Join(", ", different)}). The server uses the dat's values; rebuild the dat (ACE.DatBuilder) and restart to apply the database values.");
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
