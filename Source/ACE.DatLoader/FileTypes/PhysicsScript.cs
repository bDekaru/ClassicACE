using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x33.
    /// </summary>
    [DatFileType(DatFileType.PhysicsScript)]
    public class PhysicsScript : FileType
    {
        public List<PhysicsScriptData> ScriptData { get; } = new List<PhysicsScriptData>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            ScriptData.Unpack(reader);
        }

        public static PhysicsScript Create(uint id, params PhysicsScriptData[] data)
        {
            var result = new PhysicsScript { Id = id };
            result.ScriptData.AddRange(data);
            return result;
        }

        /// <summary>Puts the entries in a new order: position i of the result is the entry that was at order[i].</summary>
        public void Reorder(System.Collections.Generic.IReadOnlyList<int> order)
        {
            if (order.Count != ScriptData.Count)
                throw new System.ArgumentException("The order must name every entry once.", nameof(order));

            var reordered = new System.Collections.Generic.List<PhysicsScriptData>();
            foreach (var index in order)
                reordered.Add(ScriptData[index]);

            ScriptData.Clear();
            ScriptData.AddRange(reordered);
        }

        /// <summary>Replaces the dat ids the hooks refer to by map(id).</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var data in ScriptData)
                data.Remap(map);
        }

        /// <exception cref="System.NotSupportedException">A hook of a type that has no writer yet.</exception>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            ScriptData.Pack(writer);
        }
    }
}
