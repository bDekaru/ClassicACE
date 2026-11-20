using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    /// <summary>
    /// A list of indexed vertices, and their associated type
    /// </summary>
    public class CVertexArray : IUnpackable, IPackable
    {
        public int VertexType { get; private set; }
        public Dictionary<ushort, SWVertex> Vertices { get; } = new Dictionary<ushort, SWVertex>();

        public void Unpack(BinaryReader reader)
        {
            VertexType = reader.ReadInt32();

            var numVertices = reader.ReadUInt32();

            if (VertexType == 1)
                Vertices.Unpack(reader, numVertices);
            else
                throw new NotImplementedException();
        }

        public void Pack(BinaryWriter writer)
        {
            if (VertexType != 1)
                throw new NotImplementedException();

            writer.Write(VertexType);
            writer.Write((uint)Vertices.Count);

            Vertices.PackFixedQuantity(writer);
        }
    }
}
