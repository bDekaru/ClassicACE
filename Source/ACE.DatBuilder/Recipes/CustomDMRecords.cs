namespace ACE.DatBuilder
{
    /// <summary>The CustomDM portal records that are made from other records rather than kept as files in Custom Files/Portal/CustomDM.</summary>
    public static class CustomDMRecords
    {
        public const string ChargenEditsFile = "customdm-chargen.json";

        /// <summary>The EoR-only records the CustomDM world's DID properties reach that the Infiltration list lacks (56 setups, 48 icons, 10 clothing tables, 5 palettes and everything those name).</summary>
        public const string EoRRecordsFile = "customdm-eor-records.json";

        /// <summary>The EoR-only records the landblocks that <see cref="DatMapRecipes.CustomDM"/> takes from the EoR and ToD cell dats reference (56 environments, 183 surfaces, 238 objects and everything those name).</summary>
        public const string MapEoRRecordsFile = "customdm-map-eor-records.json";

        public const string LifestonesFile = "lifestones.json";
        public const string MiscModelsFile = "misc-models.json";
        public const string BanderlingFile = "old-models-banderling.json";
        public const string CarenziFile = "old-models-carenzi.json";
        public const string DrudgeFile = "old-models-drudge.json";
        public const string LugianLeatherFile = "old-models-lugian-leather.json";
        public const string LugianMetalFile = "old-models-lugian-metal.json";
        public const string MattekarFile = "old-models-mattekar.json";
        public const string MiteFile = "old-models-mite.json";
        public const string MiteAlternatePalettesFile = "old-models-mite-alternate-palettes.json";
        public const string MosswartFile = "old-models-mosswart.json";
        public const string OlthoiFile = "old-models-olthoi.json";
        public const string ReedsharkFile = "old-models-reedshark.json";
        public const string SiraluunFile = "old-models-siraluun.json";
        public const string TuskerFile = "old-models-tusker.json";
        public const string UrsuinFile = "old-models-ursuin.json";
        public const string MissingModelsFile = "missing-models.json";
        public const string MonougaFile = "old-models-monouga.json";
        public const string OldDrudgeFile = "old-models-old-drudge.json";
        public const string SclavusFile = "old-models-sclavus.json";
        public const string TumerokFile = "old-models-tumerok.json";

        /// <summary>The CustomDM steps a 2005-based portal needs: the Police Box, the sneaking scripts and the brown underwear. The rest of <see cref="All"/> is 2005 data the base already holds.</summary>
        public static DatRecordStep[] AdditionsTo2005(string date)
        {
            return new[]
            {
                ModelRecords.Step("Miscellaneous models from the 2001 dat", date, MiscModelsFile, DatBuildRecipes.Portal2001Dat),
                SneakScriptRecords.Step(date),
                UnderwearRecords.Step(date)
            };
        }

        public static DatRecordStep[] All(string date)
        {
            return new[]
            {
                InfiltrationRecords.MapFromPreToD(date),
                InfiltrationRecords.ChargenFromEoR(date, ChargenEditsFile),
                ModelRecords.Step("Original lifestone models from the 2005 dat", date, LifestonesFile),
                ModelRecords.Step("Miscellaneous models from the 2001 dat", date, MiscModelsFile, DatBuildRecipes.Portal2001Dat),
                ModelRecords.Step("Banderling models from the 2005 dat", date, BanderlingFile),
                ModelRecords.Step("Carenzi models from the 2005 dat", date, CarenziFile),
                ModelRecords.Step("Drudge models from the 2005 dat", date, DrudgeFile),
                ModelRecords.Step("Lugian (leather) models from the 2005 dat", date, LugianLeatherFile),
                ModelRecords.Step("Lugian (metal) models from the 2005 dat", date, LugianMetalFile),
                ModelRecords.Step("Mattekar models from the 2005 dat", date, MattekarFile),
                ModelRecords.Step("Mite models from the 2005 dat", date, MiteFile),
                ModelRecords.Step("Mite (alternate palettes) models from the 2005 dat", date, MiteAlternatePalettesFile),
                ModelRecords.Step("Mosswart models from the 2005 dat", date, MosswartFile),
                ModelRecords.Step("Olthoi models from the 2005 dat", date, OlthoiFile),
                ModelRecords.Step("Reedshark models from the 2005 dat", date, ReedsharkFile),
                ModelRecords.Step("Siraluun models from the 2005 dat", date, SiraluunFile),
                ModelRecords.Step("Tusker models from the 2005 dat", date, TuskerFile),
                ModelRecords.Step("Ursuin models from the 2005 dat", date, UrsuinFile),
                ModelRecords.Step("Models missing from the ToD dat, from the 2005 dat", date, MissingModelsFile),
                // Old creature models that are not part of the built dats yet; each is verified byte for byte against its former files. Uncomment one to add it.
                // ModelRecords.Step("Monouga models from the 2005 dat", date, MonougaFile),
                // ModelRecords.Step("Old Drudge models from the 2005 dat", date, OldDrudgeFile),
                // ModelRecords.Step("Sclavus models from the 2005 dat", date, SclavusFile),
                // ModelRecords.Step("Tumerok models from the 2005 dat", date, TumerokFile),
                SneakScriptRecords.Step(date)
            };
        }
    }
}
