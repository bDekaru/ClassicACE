namespace ACE.DatBuilder
{
    /// <summary>The CustomDM portal records that are made from other records.</summary>
    public static class CustomDMRecords
    {
        public const string ChargenEditsFile = "customdm-chargen.json";

        /// <summary>The EoR-only records the CustomDM world's DID properties reach that the Infiltration list lacks (56 setups, 48 icons, 10 clothing tables, 5 palettes and everything those name).</summary>
        public const string EoRRecordsFile = "customdm-eor-records.json";

        /// <summary>The EoR-only records the landblocks that <see cref="DatMapRecipes.CustomDM"/> takes from the EoR and ToD cell dats reference (56 environments, 183 surfaces, 238 objects and everything those name).</summary>
        public const string MapEoRRecordsFile = "customdm-map-eor-records.json";

        public const string MiscModelsFile = "misc-models.json";

        /// <summary>The CustomDM steps a 2005-based portal needs: the Police Box, the sneaking scripts and the brown underwear.</summary>
        public static DatRecordStep[] AdditionsTo2005(string date)
        {
            return new[]
            {
                ModelRecords.Step("Miscellaneous models from the 2001 dat", date, MiscModelsFile, DatBuildRecipes.Portal2001Dat),
                SneakScriptRecords.Step(date),
                UnderwearRecords.Step(date)
            };
        }
    }
}
