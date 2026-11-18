namespace ACE.DatBuilder
{
    /// <summary>The CustomDM portal records that are made from other records rather than kept as files in Custom Files/Portal/CustomDM.</summary>
    public static class CustomDMRecords
    {
        public const string ChargenEditsFile = "customdm-chargen.json";

        public static DatRecordStep[] All(string date)
        {
            return new[]
            {
                InfiltrationRecords.MapFromPreToD(date),
                InfiltrationRecords.ChargenFromEoR(date, ChargenEditsFile)
            };
        }
    }
}
