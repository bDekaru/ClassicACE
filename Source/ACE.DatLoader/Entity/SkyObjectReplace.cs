using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyObjectReplace : IUnpackable, IPackable
    {
        public uint ObjectIndex { get; private set; }
        public uint GFXObjId { get; private set; }
        public float Rotate { get; private set; }
        public float Transparent { get; private set; }
        public float Luminosity { get; private set; }
        public float MaxBright { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            ObjectIndex = reader.ReadUInt32();
            GFXObjId    = reader.ReadUInt32();
            Rotate      = reader.ReadSingle();
            Transparent = reader.ReadSingle();
            Luminosity  = reader.ReadSingle();
            MaxBright   = reader.ReadSingle();

            reader.AlignBoundary();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(ObjectIndex);
            writer.Write(GFXObjId);
            writer.Write(Rotate);
            writer.Write(Transparent);
            writer.Write(Luminosity);
            writer.Write(MaxBright);
            writer.AlignBoundary();
        }

        /// <summary>The object list got a new object at <paramref name="index"/>: the objects from there on move up by one.</summary>
        public void Inserted(uint index)
        {
            if (ObjectIndex >= index)
                ObjectIndex++;
        }

        public void Remap(System.Func<uint, uint> map)
        {
            GFXObjId = map.RemapIfSet(GFXObjId);
        }
    }
}
