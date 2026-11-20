using System.IO;

namespace ACE.DatLoader.Entity
{
    public class GfxObjInfo : IUnpackable, IPackable
    {
        public uint Id { get; private set; }
        public uint DegradeMode { get; private set; }
        public float MinDist { get; private set; }
        public float IdealDist { get; private set; }
        public float MaxDist { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            Id          = reader.ReadUInt32();
            DegradeMode = reader.ReadUInt32();
            MinDist     = reader.ReadSingle();
            IdealDist   = reader.ReadSingle();
            MaxDist     = reader.ReadSingle();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(DegradeMode);
            writer.Write(MinDist);
            writer.Write(IdealDist);
            writer.Write(MaxDist);
        }

        /// <summary>Replaces the model id by map(id); 0 (none) stays 0.</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            Id = map.RemapIfSet(Id);
        }
    }
}
