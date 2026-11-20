using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class PhysicsScriptTableData : IUnpackable, IPackable
    {
        public List<ScriptAndModData> Scripts { get; } = new List<ScriptAndModData>();

        public void Unpack(BinaryReader reader)
        {
            Scripts.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            Scripts.Pack(writer);
        }
    }
}
