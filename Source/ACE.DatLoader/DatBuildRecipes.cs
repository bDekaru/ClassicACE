using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ACE.DatLoader
{
    /// <summary>
    /// Records taken from a pre-ToD portal dat during a portal build: converted by <see cref="DatPortalImporter"/> and written before the overlay folders.
    /// </summary>
    public class DatPreToDImport
    {
        /// <summary>File name of the pre-ToD portal dat in the source dats folder.</summary>
        public string PreToDDat { get; }

        /// <summary>File name of the embedded pre-ToD to ToD surface map (Data folder).</summary>
        public string SurfaceMap { get; }

        /// <summary>Environments, models and setups to import, e.g. "0D000418-0D00041B,01001753". Imported in this order; surface ids handed out for new surfaces depend on it.</summary>
        public string Ids { get; }

        /// <summary>Surfaces to import although the map does not know them.</summary>
        public string Surfaces { get; }

        /// <summary>File name of the pre-ToD cell dat in the source dats folder, which says which environments each building uses.</summary>
        public string PreToDCellDat { get; }

        /// <summary>Building models to replace as a whole with the pre-ToD versions, e.g. "01002EF1,01002EF2": each brings its group (see <see cref="DatBuildingGroups"/>). Imported after the surfaces.</summary>
        public string Buildings { get; }

        public DatPreToDImport(string preToDDat, string surfaceMap, string ids, string surfaces, string preToDCellDat = null, string buildings = null)
        {
            PreToDDat = preToDDat;
            SurfaceMap = surfaceMap;
            Ids = ids;
            Surfaces = surfaces;
            PreToDCellDat = preToDCellDat;
            Buildings = buildings;
        }

        public DatPortalImporter Run(string sourceDats, string targetDat)
        {
            var importer = new DatPortalImporter(new DatDatabase(Path.Combine(sourceDats, PreToDDat), true), new DatDatabase(targetDat, true), DatCellConverter.LoadEmbeddedSurfaceMap(SurfaceMap));

            importer.ImportIds(Ids);
            importer.ImportSurfaces(Surfaces);

            if (!string.IsNullOrEmpty(Buildings))
            {
                var groups = new DatBuildingGroups(new DatDatabase(Path.Combine(sourceDats, PreToDCellDat), true));

                foreach (var model in DatPortalImporter.ParseIds(Buildings))
                    importer.ImportBuilding(model, groups);
            }

            return importer;
        }
    }

    /// <summary>
    /// One dat build: a base dat, optionally records imported from a pre-ToD dat, and the ordered list of overlay folders (relative to the Custom Files root) laid over it.
    /// </summary>
    public class DatBuildRecipe
    {
        /// <summary>Set for portal recipes that need old content the EoR portal dat no longer has.</summary>
        public DatPreToDImport PreToDImport { get; private set; }

        public DatBuildRecipe WithPreToDImport(DatPreToDImport import)
        {
            PreToDImport = import;
            return this;
        }

        public string Name { get; }

        /// <summary>File name of the base dat in the source dats folder.</summary>
        public string BaseDat { get; }

        /// <summary>Folders in application order, relative to the Custom Files root, written with '/'.</summary>
        public string[] Folders { get; }

        public Func<uint, bool> AcceptId { get; }

        /// <summary>Output subfolder name, e.g. CustomDM or Infiltration.</summary>
        public string Ruleset { get; }

        /// <summary>The built dat's file name: the base dat's name without the " - EoR" suffix.</summary>
        public string OutputFileName => BaseDat.Replace(" - EoR", "");

        /// <summary>Recipe name as typed on the command line, e.g. portal-customdm.</summary>
        public string Key => Name.Replace(' ', '-').ToLowerInvariant();

        public DatBuildRecipe(string name, string ruleset, string baseDat, Func<uint, bool> acceptId, params string[] folders)
        {
            Name = name;
            Ruleset = ruleset;
            BaseDat = baseDat;
            AcceptId = acceptId;
            Folders = folders;
        }

        public string[] ResolveFolders(string customFilesRoot)
        {
            return Folders.Select(f => Path.Combine(customFilesRoot, f.Replace('/', Path.DirectorySeparatorChar))).ToArray();
        }

        public DatOverlayReport Apply(DatWriter writer, string customFilesRoot, IEnumerable<KeyValuePair<uint, byte[]>> generated = null)
        {
            return DatOverlay.Apply(writer, ResolveFolders(customFilesRoot), AcceptId, generated);
        }
    }

    /// <summary>
    /// The overlay steps of Melt's Language and Portal builders (Program.cs: LanguageManipulationForCustomDM, LanguageManipulationForInfiltration,
    /// PortalManipulationForCustomDM, PortalManipulationForInfiltration), with the same folders in the same order.
    /// Folders Melt has commented out are deliberately absent.
    /// </summary>
    public static class DatBuildRecipes
    {
        public const string LanguageBaseDat = "client_local_English - EoR.dat";
        public const string PortalBaseDat = "client_portal - EoR.dat";

        public const string PreToDPortalDat = "portal - 2005-02 (Iteration 2112).dat";
        public const string PreToDSurfaceMap = "surface_map_preToD_2005-02_to_ToD.tsv";

        // Environments and models of the landblocks whose content was removed in ToD (2459 2577 2677 287F), the EC0E objects, and the old models
        // CustomDM also gets from Melt's Missing Files; surfaces that the cell conversion reports as unmapped.
        // 0D0003DA is the environment of two cells of landblock EBF7 (CustomDM, from the older-era dats), which ToD removed.
        private const string RemovedContentIds = "0D000418-0D00041B,01001753-01001756,0100353E-0100353F,010015F3-010015F4,0100281E,020001DC,0D0003DA";
        private const string UnmappedSurfaces = "080015B0,080015B1,080015E2,08001E7A";

        // Buildings whose exterior model or environments were reshaped between 2005 and EoR, so the 2005 cells no longer fit the EoR records. Each is replaced with its whole group (models and environments).
        // 01002EF1/2: Candeth Keep and Renegade Fortress (0574, 0575), 010034FA: the EC0E chapel (05BF), 01000830: the 02FB buildings (the 2005 look), 01002EF6: exterior only.
        private const string ReplacedBuildings = "01000830,01002EF1,01002EF2,01002EF6,010034FA";

        // Every portal recipe imports the same set, so the rulesets get identical converted records and the cell conversion can use either portal.
        private static DatPreToDImport RemovedContent()
        {
            return new DatPreToDImport(PreToDPortalDat, PreToDSurfaceMap, RemovedContentIds, UnmappedSurfaces, PreToDCellDat, ReplacedBuildings);
        }

        public static readonly DatBuildRecipe LanguageCustomDM = new DatBuildRecipe("Language CustomDM", "CustomDM", LanguageBaseDat, DatOverlay.IsLanguageId,
            "Language/CustomDM");

        public static readonly DatBuildRecipe LanguageInfiltration = new DatBuildRecipe("Language Infiltration", "Infiltration", LanguageBaseDat, DatOverlay.IsLanguageId,
            "Language/Infiltration");

        public static readonly DatBuildRecipe PortalCustomDM = new DatBuildRecipe("Portal CustomDM", "CustomDM", PortalBaseDat, DatOverlay.IsPortalId,
            "Portal/CustomDM",
            "Portal/Shared/Ground Textures",
            "Portal/Shared/Miscellaneous Textures",
            "Portal/Shared/Trees",
            "Portal/Shared/Frozen Fields",
            "Portal/Shared/Police Box",
            "Portal/Shared/Old Models/Drudge",
            "Portal/Shared/Old Models/Mattekar",
            "Portal/Shared/Old Models/Ursuin",
            "Portal/Shared/Old Models/Carenzi",
            "Portal/Shared/Old Models/Siraluun",
            "Portal/Shared/Old Models/Reedshark",
            "Portal/Shared/Old Models/Lugian - Leather",
            "Portal/Shared/Old Models/Mite",
            "Portal/Shared/Old Models/Mite - Alternate Palettes",
            "Portal/Shared/Old Models/Tusker",
            "Portal/Shared/Old Models/Banderling",
            "Portal/Shared/Old Models/Lugian - Metal",
            "Portal/Shared/Old Models/Mosswart",
            "Portal/Shared/Old Models/Olthoi")
            .WithPreToDImport(RemovedContent());

        public static readonly DatBuildRecipe PortalInfiltration = new DatBuildRecipe("Portal Infiltration", "Infiltration", PortalBaseDat, DatOverlay.IsPortalId,
            "Portal/Infiltration",
            "Portal/Shared/Ground Textures",
            "Portal/Shared/Trees")
            .WithPreToDImport(RemovedContent());

        public static readonly DatBuildRecipe[] All = { LanguageCustomDM, LanguageInfiltration, PortalCustomDM, PortalInfiltration };

        public const string PreToDCellDat = "cell - 2005-02-XX (202752kb) (Admin) (Iteration 1593 - Complete).dat";
        public const string ToDCellDat = "client_cell_1 - ToD.dat";

        public static readonly DatCellBuildRecipe CellCustomDM = new DatCellBuildRecipe("Cell CustomDM", "CustomDM", PreToDCellDat, ToDCellDat, PortalCustomDM, PreToDSurfaceMap, DatMapRecipes.CustomDM);

        public static readonly DatCellBuildRecipe CellInfiltration = new DatCellBuildRecipe("Cell Infiltration", "Infiltration", PreToDCellDat, ToDCellDat, PortalInfiltration, PreToDSurfaceMap);

        /// <summary>Cell recipes need their ruleset's portal dat built first.</summary>
        public static readonly DatCellBuildRecipe[] AllCells = { CellCustomDM, CellInfiltration };
    }
}
