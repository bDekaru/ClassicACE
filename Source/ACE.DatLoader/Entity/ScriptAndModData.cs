using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ScriptAndModData : IUnpackable
    {
        public float Mod { get; private set; }
        public uint ScriptId { get; private set; }

        public static ScriptAndModData Create(float mod, uint scriptId)
        {
            return new ScriptAndModData { Mod = mod, ScriptId = scriptId };
        }

        public void Unpack(BinaryReader reader)
        {
            Mod         = reader.ReadSingle();
            ScriptId    = reader.ReadUInt32();
        }

        public void Remap(System.Func<uint, uint> map)
        {
            ScriptId = map(ScriptId);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Mod);
            writer.Write(ScriptId);
        }
    }
}
