namespace ACE.DatBuilder
{
    /// <summary>Records of the EoR dat that nothing refers to and that are removed from the built dat.</summary>
    public static class RemovedRecords
    {
        /// <summary>Two staircase setups and their two main GfxObjs. Setups 02001C4F and 02001C50 are referenced by no dat, database or code, and they are the only users of the staircase plank GfxObj 01004E29, which exists in no dat.</summary>
        public static readonly uint[] UnusedStaircases = { 0x02001C4F, 0x02001C50, 0x01004E28, 0x01004E2A };

        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Unused staircase models removed", date, c =>
            {
                foreach (var id in UnusedStaircases)
                    c.Delete(id, "unused staircase model");
            });
        }
    }
}
