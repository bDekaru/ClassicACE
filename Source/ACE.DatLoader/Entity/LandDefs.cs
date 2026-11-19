using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class LandDefs : IUnpackable
    {
        public int NumBlockLength { get; private set; }
        public int NumBlockWidth { get; private set; }
        public float SquareLength { get; private set; }
        public int LBlockLength { get; private set; }
        public int VertexPerCell { get; private set; }
        public float MaxObjHeight { get; private set; }
        public float SkyHeight { get; private set; }
        public float RoadWidth { get; private set; }

        public List<float> LandHeightTable { get; } = new List<float>();

        public void Unpack(BinaryReader reader)
        {
            NumBlockLength  = reader.ReadInt32();
            NumBlockWidth   = reader.ReadInt32();
            SquareLength    = reader.ReadSingle();
            LBlockLength    = reader.ReadInt32();
            VertexPerCell   = reader.ReadInt32();
            MaxObjHeight    = reader.ReadSingle();
            SkyHeight       = reader.ReadSingle();
            RoadWidth       = reader.ReadSingle();

            for (int i = 0; i < 256; i++)
                LandHeightTable.Add(reader.ReadSingle());
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(NumBlockLength);
            writer.Write(NumBlockWidth);
            writer.Write(SquareLength);
            writer.Write(LBlockLength);
            writer.Write(VertexPerCell);
            writer.Write(MaxObjHeight);
            writer.Write(SkyHeight);
            writer.Write(RoadWidth);

            foreach (var height in LandHeightTable)
                writer.Write(height);
        }
    }
}
