using ACE.DatLoader.Entity;
using ACE.DatLoader.Entity.AnimationHooks;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>The two physics scripts that fade a character to half transparency and back when sneaking starts and ends, and their entries in the physics script table.</summary>
    public static class SneakScriptRecords
    {
        public const uint BeginScriptId = 0x330013A0;
        public const uint EndScriptId = 0x330013A1;
        public const uint ScriptTableId = 0x34000004;
        public const uint BeginTableKey = 0xAE;
        public const uint EndTableKey = 0xAF;

        // The script table is the EoR dat's own record: portal - 2017-01-29 - Iteration 2072 - EoR.dat, MD5 2c89662a44fcdc2a3c31fe8f6677d265
        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("Sneaking scripts", date, c =>
            {
                c.Write(BeginScriptId, FadeScript(BeginScriptId, 0f, 0.5f), "sneaking begin script");
                c.Write(EndScriptId, FadeScript(EndScriptId, 0.5f, 0f), "sneaking end script");

                var table = c.ReadTarget<PhysicsScriptTable>(ScriptTableId);
                AddTableEntry(table, BeginTableKey, BeginScriptId);
                AddTableEntry(table, EndTableKey, EndScriptId);
                c.Write(ScriptTableId, table.Pack(), "physics script table with the sneaking scripts", true);
            });
        }

        public static byte[] FadeScript(uint id, float start, float end)
        {
            return PhysicsScript.Create(id, PhysicsScriptData.Create(0, TransparentHook.Create(AnimationHookDir.Both, start, end, 1f))).Pack();
        }

        private static void AddTableEntry(PhysicsScriptTable table, uint key, uint scriptId)
        {
            var entry = new PhysicsScriptTableData();
            entry.Scripts.Add(ScriptAndModData.Create(1f, scriptId));
            table.ScriptTable[key] = entry;
        }
    }
}
