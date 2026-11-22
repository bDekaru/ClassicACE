using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using ACE.DatLoader.Entity;
using ACE.Entity.Enum;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x01. 
    /// These are used both on their own for some pre-populated structures in the world (trees, buildings, etc) or make up SetupModel (0x02) objects.
    /// </summary>
    [DatFileType(DatFileType.GraphicsObject)]
    public class GfxObj : FileType
    {
        public GfxObjFlags Flags { get; private set; }
        public List<uint> Surfaces { get; } = new List<uint>(); // also referred to as m_rgSurfaces in the client
        public CVertexArray VertexArray { get; } = new CVertexArray();

        public Dictionary<ushort, Polygon> PhysicsPolygons { get; } = new Dictionary<ushort, Polygon>();
        public BSPTree PhysicsBSP { get; } = new BSPTree();

        public Vector3 SortCenter { get; private set; }

        public Dictionary<ushort, Polygon> Polygons { get; } = new Dictionary<ushort, Polygon>();
        public BSPTree DrawingBSP { get; } = new BSPTree();

        public uint DIDDegrade { get; private set; }

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            Flags = (GfxObjFlags)reader.ReadUInt32();

            Surfaces.UnpackSmartArray(reader);

            VertexArray.Unpack(reader);

            // Has Physics 
            if ((Flags & GfxObjFlags.HasPhysics) != 0)
            {
                PhysicsPolygons.UnpackSmartArray(reader);

                PhysicsBSP.Unpack(reader, BSPType.Physics);
            }

            SortCenter = reader.ReadVector3();

            // Has Drawing 
            if ((Flags & GfxObjFlags.HasDrawing) != 0)
            {
                Polygons.UnpackSmartArray(reader);

                DrawingBSP.Unpack(reader, BSPType.Drawing);
            }

            if ((Flags & GfxObjFlags.HasDIDDegrade) != 0)
                DIDDegrade = reader.ReadUInt32();
        }

        /// <summary>
        /// Reads the record as the pre-ToD client stored it (32-bit counts, alignment pads, no degrade field), by way of <see cref="DatEnvironmentConverter.ConvertGfxObj"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">The model uses legacy triangle strips.</exception>
        public static GfxObj UnpackPreToD(byte[] record)
        {
            using (var reader = new BinaryReader(new MemoryStream(DatEnvironmentConverter.ConvertGfxObj(record))))
            {
                var model = new GfxObj();
                model.Unpack(reader);
                return model;
            }
        }

        /// <summary>Scales the model about its sort center: the vertices and the spheres of its trees. For a flat model facing the viewer, as the sky's moons are, it is the model drawn larger at the same place.</summary>
        public void Scale(float factor)
        {
            foreach (var vertex in VertexArray.Vertices.Values)
                vertex.Scale(SortCenter, factor);

            DrawingBSP.RootNode.Scale(SortCenter, factor);
            PhysicsBSP.RootNode.Scale(SortCenter, factor);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);

            writer.Write((uint)Flags);

            Surfaces.PackSmartArray(writer);

            VertexArray.Pack(writer);

            if ((Flags & GfxObjFlags.HasPhysics) != 0)
            {
                PhysicsPolygons.PackSmartArray(writer);

                PhysicsBSP.Pack(writer, BSPType.Physics);
            }

            writer.WriteVector3(SortCenter);

            if ((Flags & GfxObjFlags.HasDrawing) != 0)
            {
                Polygons.PackSmartArray(writer);

                DrawingBSP.Pack(writer, BSPType.Drawing);
            }

            if ((Flags & GfxObjFlags.HasDIDDegrade) != 0)
                writer.Write(DIDDegrade);
        }
    }
}
