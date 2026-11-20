using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x34.
    /// </summary>
    [DatFileType(DatFileType.PhysicsScriptTable)]
    public class PhysicsScriptTable : FileType
    {
        public Dictionary<uint, PhysicsScriptTableData> ScriptTable { get; set; } = new Dictionary<uint, PhysicsScriptTableData>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            ScriptTable.Unpack(reader);
        }

        /// <summary>Replaces every script id by map(id).</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var entry in ScriptTable.Values)
                foreach (var script in entry.Scripts)
                    script.Remap(map);
        }

        /// <summary>Entries are written in dictionary order, which is the file's order for a table read from a record and then added to.</summary>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            ScriptTable.Pack(writer);
        }
    }
}
