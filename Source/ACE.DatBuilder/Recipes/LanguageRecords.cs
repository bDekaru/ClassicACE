using System.Collections.Generic;

using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>The language records that are made from the EoR record with the edits of a data file rather than kept as files in the Language folder of Custom Files.</summary>
    public static class LanguageRecords
    {
        public const uint ChargenHeritageUiId = 0x21000046;
        public const uint ChargenTextId = 0x23000002;

        /// <summary>The heritage layout edits are the same in every ruleset.</summary>
        public const string ChargenHeritageUiFile = "chargen-heritage-ui.json";
        public const string InfiltrationChargenTextFile = "infiltration-chargen-text.json";
        public const string CustomDMChargenTextFile = "customdm-chargen-text.json";

        public static DatRecordStep[] Infiltration(string date)
        {
            return new[] { ChargenHeritageUi(date), ChargenText(date, InfiltrationChargenTextFile) };
        }

        public static DatRecordStep[] CustomDM(string date)
        {
            return new[] { ChargenHeritageUi(date), ChargenText(date, CustomDMChargenTextFile) };
        }

        private static DatRecordStep ChargenHeritageUi(string date)
        {
            return new DatRecordStep("Character generation heritage layout from the EoR record", date, c =>
            {
                var master = c.ReadSource<MasterProperty>(DatBuildRecipes.PortalBaseDat, DatLayoutEdits.MasterPropertyId);
                var layout = DatLayoutEdits.Unpack(c.ReadTarget(ChargenHeritageUiId), master);
                DatLayoutEdits.Apply(layout, DatRecipeData.Load<List<DatLayoutMediaEdit>>(ChargenHeritageUiFile));
                c.Write(ChargenHeritageUiId, layout.Pack(), "character generation heritage layout");
            });
        }

        private static DatRecordStep ChargenText(string date, string editsFile)
        {
            return new DatRecordStep("Character generation text from the EoR record", date, c =>
            {
                var table = c.ReadTarget<StringTable>(ChargenTextId);
                DatStringEdits.Apply(table, DatRecipeData.Load<List<DatStringEdit>>(editsFile));
                c.Write(ChargenTextId, table.Pack(), "character generation text");
            });
        }
    }
}
