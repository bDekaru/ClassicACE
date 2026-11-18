using System.Collections.Generic;

using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>The Infiltration language records that are made from the EoR record with the edits of a data file rather than kept as files in Custom Files/Language/Infiltration.</summary>
    public static class LanguageRecords
    {
        public const uint ChargenHeritageUiId = 0x21000046;
        public const uint ChargenTextId = 0x23000002;

        public const string InfiltrationChargenHeritageUiFile = "infiltration-chargen-heritage-ui.json";
        public const string InfiltrationChargenTextFile = "infiltration-chargen-text.json";

        public static DatRecordStep[] Infiltration(string date)
        {
            return new[]
            {
                new DatRecordStep("Character generation heritage layout from the EoR record", date, c =>
                {
                    var master = c.ReadSource<MasterProperty>(DatBuildRecipes.PortalBaseDat, DatLayoutEdits.MasterPropertyId);
                    var layout = DatLayoutEdits.Unpack(c.ReadTarget(ChargenHeritageUiId), master);
                    DatLayoutEdits.Apply(layout, DatRecipeData.Load<List<DatLayoutMediaEdit>>(InfiltrationChargenHeritageUiFile));
                    c.Write(ChargenHeritageUiId, layout.Pack(), "character generation heritage layout");
                }),
                new DatRecordStep("Character generation text from the EoR record", date, c =>
                {
                    var table = c.ReadTarget<StringTable>(ChargenTextId);
                    DatStringEdits.Apply(table, DatRecipeData.Load<List<DatStringEdit>>(InfiltrationChargenTextFile));
                    c.Write(ChargenTextId, table.Pack(), "character generation text");
                })
            };
        }
    }
}
