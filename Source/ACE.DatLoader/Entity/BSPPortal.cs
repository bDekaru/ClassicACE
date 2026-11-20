using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ACE.Entity.Enum;

namespace ACE.DatLoader.Entity
{
    public class BSPPortal : BSPNode
    {
        public List<PortalPoly> InPortals { get; } = new List<PortalPoly>();

        /// <summary>
        /// You must use the Unpack(BinaryReader reader, BSPType treeType) method.
        /// </summary>
        /// <exception cref="NotSupportedException">You must use the Unpack(BinaryReader reader, BSPType treeType) method.</exception>
        public override void Unpack(BinaryReader reader)
        {
            throw new NotSupportedException();
        }

        public override void Unpack(BinaryReader reader, BSPType treeType)
        {
            Type = Encoding.ASCII.GetString(reader.ReadBytes(4)).Reverse();

            SplittingPlane = new Plane();
            SplittingPlane.Unpack(reader);

            PosNode = BSPNode.ReadNode(reader, treeType);
            NegNode = BSPNode.ReadNode(reader, treeType);

            if (treeType == BSPType.Drawing)
            {
                Sphere = new Sphere();
                Sphere.Unpack(reader);

                var numPolys = reader.ReadUInt32();
                var numPortals = reader.ReadUInt32();

                InPolys = new List<ushort>();
                for (uint i = 0; i < numPolys; i++)
                    InPolys.Add(reader.ReadUInt16());

                InPortals.Unpack(reader, numPortals);
            }
        }

        /// <summary>
        /// You must use the Pack(BinaryWriter writer, BSPType treeType) method.
        /// </summary>
        /// <exception cref="NotSupportedException">You must use the Pack(BinaryWriter writer, BSPType treeType) method.</exception>
        public override void Pack(BinaryWriter writer)
        {
            throw new NotSupportedException();
        }

        public override void Pack(BinaryWriter writer, BSPType treeType)
        {
            writer.Write(Encoding.ASCII.GetBytes(Type.Reverse()));

            SplittingPlane.Pack(writer);

            PosNode.Pack(writer, treeType);
            NegNode.Pack(writer, treeType);

            if (treeType == BSPType.Drawing)
            {
                Sphere.Pack(writer);

                writer.Write((uint)InPolys.Count);
                writer.Write((uint)InPortals.Count);

                foreach (var id in InPolys)
                    writer.Write(id);

                foreach (var portal in InPortals)
                    portal.Pack(writer);
            }
        }
    }
}
