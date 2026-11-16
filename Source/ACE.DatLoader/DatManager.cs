using System.IO;

using log4net;

namespace ACE.DatLoader
{
    public static class DatManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static string datFile;

        private static int count;

        // End of retail Iteration versions.
        public static string CLIENT_VERSION_STRING = "1802";
        private static int ITERATION_CELL = 982;
        private static int ITERATION_PORTAL = 2072;
        private static int ITERATION_HIRES = 497;
        private static int ITERATION_LANGUAGE = 994;

        // The iteration numbers of the Infiltration and CustomDM dats are the latest releases in DatReleases.
        public static string INFILTRATION_CLIENT_VERSION_STRING = "i104";
        private static int INFILTRATION_ITERATION_HIRES = 497;

        public static string CUSTOMDM_CLIENT_VERSION_STRING = "c119";
        private static int CUSTOMDM_ITERATION_HIRES = 497;
        public static CellDatDatabase CellDat { get; private set; }

        public static PortalDatDatabase PortalDat { get; private set; }
        public static DatDatabase HighResDat { get; private set; }
        public static LanguageDatDatabase LanguageDat { get; private set; }

        public static void Initialize(string datFileDirectory, bool keepOpen = false, bool loadCell = true)
        {
            int cellExpectedIteration;
            int portalExpectedIteration;
            int highResExpectedIteration;
            int languageExpectedIteration;

            switch (Common.ConfigManager.Config.Server.WorldRuleset)
            {
                default:
                case Common.Ruleset.EoR:
                    cellExpectedIteration = ITERATION_CELL;
                    portalExpectedIteration = ITERATION_PORTAL;
                    highResExpectedIteration = ITERATION_HIRES;
                    languageExpectedIteration = ITERATION_LANGUAGE;
                    break;
                case Common.Ruleset.Infiltration:
                    cellExpectedIteration = (int)DatReleases.InfiltrationCell.CurrentNumber;
                    portalExpectedIteration = (int)DatReleases.InfiltrationPortal.CurrentNumber;
                    highResExpectedIteration = INFILTRATION_ITERATION_HIRES;
                    languageExpectedIteration = (int)DatReleases.InfiltrationLanguage.CurrentNumber;
                    break;
                case Common.Ruleset.CustomDM:
                    cellExpectedIteration = (int)DatReleases.CustomDMCell.CurrentNumber;
                    portalExpectedIteration = (int)DatReleases.CustomDMPortal.CurrentNumber;
                    highResExpectedIteration = CUSTOMDM_ITERATION_HIRES;
                    languageExpectedIteration = (int)DatReleases.CustomDMLanguage.CurrentNumber;
                    break;
            }

            var datDir = Path.GetFullPath(Path.Combine(datFileDirectory));

            if (loadCell)
            {
                try
                {
                    datFile = Path.Combine(datDir, "client_cell_1.dat");
                    CellDat = new CellDatDatabase(datFile, keepOpen);
                    count = CellDat.AllFiles.Count;
                    log.Info($"Successfully opened {datFile} file, containing {count} records, iteration {CellDat.Iteration}");
                    if (CellDat.Iteration != cellExpectedIteration)
                        log.Warn($"{datFile} iteration {CellDat.Iteration} does not match expected version of {cellExpectedIteration}.");
                }
                catch (FileNotFoundException ex)
                {
                    log.Error($"An exception occured while attempting to open {datFile} file!  This needs to be corrected in order for Landblocks to load!");
                    log.Error($"Exception: {ex.Message}");
                }
            }

            try
            {
                datFile = Path.Combine(datDir, "client_portal.dat");
                PortalDat = new PortalDatDatabase(datFile, keepOpen);
                count = PortalDat.AllFiles.Count;
                log.Info($"Successfully opened {datFile} file, containing {count} records, iteration {PortalDat.Iteration}");
                if (PortalDat.Iteration != portalExpectedIteration)
                    log.Warn($"{datFile} iteration {PortalDat.Iteration} does not match expected version of {portalExpectedIteration}.");
            }
            catch (FileNotFoundException ex)
            {
                log.Error($"An exception occured while attempting to open {datFile} file!\n\n *** Please check your 'DatFilesDirectory' setting in the config.js file. ***\n *** ACE will not run properly without this properly configured! ***\n");
                log.Error($"Exception: {ex.Message}");
            }

            // Load the client_highres.dat file. This is not required for ACE operation, so no exception needs to be generated.
            datFile = Path.Combine(datDir, "client_highres.dat");
            if (File.Exists(datFile))
            {
                HighResDat = new DatDatabase(datFile, keepOpen);
                count = HighResDat.AllFiles.Count;
                log.Info($"Successfully opened {datFile} file, containing {count} records, iteration {HighResDat.Iteration}");
                if (HighResDat.Iteration != highResExpectedIteration)
                    log.Warn($"{datFile} iteration {HighResDat.Iteration} does not match expected iteration version of {highResExpectedIteration}.");
            }

            try
            {
                datFile = Path.Combine(datDir, "client_local_English.dat");
                LanguageDat = new LanguageDatDatabase(datFile, keepOpen);
                count = LanguageDat.AllFiles.Count;
                log.Info($"Successfully opened {datFile} file, containing {count} records, iteration {LanguageDat.Iteration}");
                if (LanguageDat.Iteration != languageExpectedIteration)
                    log.Warn($"{datFile} iteration {LanguageDat.Iteration} does not match expected version of {languageExpectedIteration}.");
            }
            catch (FileNotFoundException ex)
            {
                log.Error($"An exception occured while attempting to open {datFile} file!\n\n *** Please check your 'DatFilesDirectory' setting in the config.json file. ***\n *** ACE will not run properly without this properly configured! ***\n");
                log.Error($"Exception: {ex.Message}");
            }
        }
    }
}
