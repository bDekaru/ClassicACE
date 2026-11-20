using System.Collections.Generic;
using System.IO;

using ACE.Entity.Enum;

namespace ACE.DatLoader.Entity
{
    public class CellStruct : IUnpackable, IPackable
    {
        public CVertexArray VertexArray { get; } = new CVertexArray();
        public Dictionary<ushort, Polygon> Polygons { get; } = new Dictionary<ushort, Polygon>();
        public List<ushort> Portals { get; } = new List<ushort>();
        public BSPTree CellBSP { get; } = new BSPTree();
        public Dictionary<ushort, Polygon> PhysicsPolygons { get; } = new Dictionary<ushort, Polygon>();
        public BSPTree PhysicsBSP { get; } = new BSPTree();
        public BSPTree DrawingBSP { get; set; }

        public void Unpack(BinaryReader reader)
        {
            var numPolygons        = reader.ReadUInt32();
            var numPhysicsPolygons = reader.ReadUInt32();
            var numPortals         = reader.ReadUInt32();

            VertexArray.Unpack(reader);

            Polygons.Unpack(reader, numPolygons);

            for (uint i = 0; i < numPortals; i++)
                Portals.Add(reader.ReadUInt16());

            reader.AlignBoundary();

            CellBSP.Unpack(reader, BSPType.Cell);

            PhysicsPolygons.Unpack(reader, numPhysicsPolygons);

            PhysicsBSP.Unpack(reader, BSPType.Physics);

            uint hasDrawingBSP = reader.ReadUInt32();
            if (hasDrawingBSP != 0)
            {
                DrawingBSP = new BSPTree();
                DrawingBSP.Unpack(reader, BSPType.Drawing);
            }

            reader.AlignBoundary();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write((uint)Polygons.Count);
            writer.Write((uint)PhysicsPolygons.Count);
            writer.Write((uint)Portals.Count);

            VertexArray.Pack(writer);

            Polygons.PackFixedQuantity(writer);

            foreach (var portal in Portals)
                writer.Write(portal);

            writer.AlignBoundary();

            CellBSP.Pack(writer, BSPType.Cell);

            PhysicsPolygons.PackFixedQuantity(writer);

            PhysicsBSP.Pack(writer, BSPType.Physics);

            writer.Write(DrawingBSP != null ? 1u : 0u);
            DrawingBSP?.Pack(writer, BSPType.Drawing);

            writer.AlignBoundary();
        }
    }
}
