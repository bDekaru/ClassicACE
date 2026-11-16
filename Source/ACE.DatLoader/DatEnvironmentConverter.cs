using System;
using System.IO;

namespace ACE.DatLoader
{
    /// <summary>
    /// Converts an Environment record (portal dat, 0x0D......) from the pre-ToD layout to the ToD/EoR layout.
    /// Both layouts have the same fields; the pre-ToD client also aligns to 4 bytes after every polygon, after the
    /// polygon list of every physics leaf and drawing node, and after every drawing portal node. The ToD layout only
    /// aligns after a cell structure's portal list and at the end of each cell structure, at its own offsets.
    /// </summary>
    public static class DatEnvironmentConverter
    {
        private const uint TagPortal = 0x504F5254; // PORT
        private const uint TagLeaf = 0x4C454146;   // LEAF
        private const int ClockwiseSides = 2;

        private enum TreeType
        {
            Drawing,
            Physics,
            Cell
        }

        public static byte[] Convert(byte[] retail)
        {
            var conv = new Transcoder(retail);
            conv.Environment();
            return conv.Result();
        }

        /// <summary>
        /// Converts a GfxObj record (portal dat, 0x01......) from the pre-ToD layout to the ToD/EoR layout:
        /// counts become compressed integers, the alignment pads go, and the legacy triangle strips (flag 4) are not supported.
        /// </summary>
        public static byte[] ConvertGfxObj(byte[] retail, Func<uint, uint> surfaceId = null)
        {
            var conv = new Transcoder(retail);
            conv.SurfaceId = surfaceId;
            conv.GfxObj();
            return conv.Result();
        }

        private sealed class Transcoder
        {
            private readonly byte[] _in;
            private int _p;
            private readonly MemoryStream _out = new MemoryStream();

            public Func<uint, uint> SurfaceId { get; set; }

            public Transcoder(byte[] input)
            {
                _in = input;
            }

            public byte[] Result()
            {
                if (_p != _in.Length)
                    throw new InvalidDataException($"Environment: {_in.Length - _p} unread bytes");

                return _out.ToArray();
            }

            private uint U32()
            {
                Need(4);
                return BitConverter.ToUInt32(_in, _p);
            }

            private void Need(int n)
            {
                if (_p + n > _in.Length)
                    throw new InvalidDataException("Environment: truncated record");
            }

            private void Copy(int n)
            {
                Need(n);
                _out.Write(_in, _p, n);
                _p += n;
            }

            private uint CopyU32()
            {
                var v = U32();
                Copy(4);
                return v;
            }

            private void SkipInPad()
            {
                while (_p % 4 != 0)
                {
                    Need(1);
                    if (_in[_p] != 0)
                        throw new InvalidDataException("Environment: nonzero alignment byte");

                    _p++;
                }
            }

            private void WriteOutPad()
            {
                while (_out.Position % 4 != 0)
                    _out.WriteByte(0);
            }

            private uint CountToCompressed()
            {
                var v = U32();
                _p += 4;

                if (v < 0x80)
                    _out.WriteByte((byte)v);
                else if (v < 0x4000)
                {
                    _out.WriteByte((byte)(0x80 | (v >> 8)));
                    _out.WriteByte((byte)v);
                }
                else
                {
                    _out.WriteByte((byte)(0xC0 | (v >> 24)));
                    _out.WriteByte((byte)(v >> 16));
                    _out.WriteByte((byte)(v >> 8));
                    _out.WriteByte((byte)v);
                }

                return v;
            }

            public void GfxObj()
            {
                Copy(4); // id

                var flags = U32();
                _p += 4;

                if ((flags & 4) != 0)
                    throw new NotSupportedException("GfxObj: legacy triangle strips (flag 4)");

                _out.Write(BitConverter.GetBytes(flags), 0, 4);

                var numSurfaces = CountToCompressed();
                for (uint i = 0; i < numSurfaces; i++)
                {
                    var surface = U32();
                    _p += 4;
                    _out.Write(BitConverter.GetBytes(SurfaceId == null ? surface : SurfaceId(surface)), 0, 4);
                }

                VertexArray();

                if ((flags & 1) != 0)
                {
                    Polygons(CountToCompressed());
                    Tree(TreeType.Physics);
                }

                Copy(12); // sort center

                if ((flags & 2) != 0)
                {
                    Polygons(CountToCompressed());
                    Tree(TreeType.Drawing);
                }

                SkipInPad();
            }

            public void Environment()
            {
                Copy(4); // id

                var cells = CopyU32();
                for (uint i = 0; i < cells; i++)
                {
                    Copy(4); // cell id
                    CellStruct();
                }
            }

            private void CellStruct()
            {
                var numPolygons = CopyU32();
                var numPhysicsPolygons = CopyU32();
                var numPortals = CopyU32();

                VertexArray();
                Polygons(numPolygons);

                Copy(2 * (int)numPortals);
                SkipInPad();
                WriteOutPad();

                Tree(TreeType.Cell);

                Polygons(numPhysicsPolygons);

                Tree(TreeType.Physics);

                if (CopyU32() != 0)
                    Tree(TreeType.Drawing);

                SkipInPad();
                WriteOutPad();
            }

            private void VertexArray()
            {
                var vertexType = CopyU32();
                var numVertices = CopyU32();

                if (vertexType != 1)
                    throw new InvalidDataException($"Environment: vertex type {vertexType}");

                for (uint i = 0; i < numVertices; i++)
                {
                    Copy(2);                     // vertex id
                    Need(2);
                    var numUVs = BitConverter.ToUInt16(_in, _p);
                    Copy(2);
                    Copy(24 + 8 * numUVs);       // origin, normal, uvs
                }
            }

            private void Polygons(uint count)
            {
                for (uint i = 0; i < count; i++)
                {
                    Copy(2); // polygon id

                    Need(8);
                    int numPts = _in[_p];
                    var stippling = _in[_p + 1];
                    var sidesType = BitConverter.ToInt32(_in, _p + 2);
                    Copy(2 + 4 + 4);             // numPts, stippling, sides type, pos surface, neg surface
                    Copy(2 * numPts);

                    if ((stippling & 4) == 0)
                        Copy(numPts);

                    if (sidesType == ClockwiseSides && (stippling & 8) == 0)
                        Copy(numPts);

                    SkipInPad();
                }
            }

            private void Tree(TreeType type)
            {
                Node(type);
            }

            private void Node(TreeType type)
            {
                var tag = U32();

                if (tag == TagPortal)
                    Portal(type);
                else if (tag == TagLeaf)
                    Leaf(type);
                else
                    Interior(type);
            }

            private void Interior(TreeType type)
            {
                var tag = CopyU32();
                Copy(16); // splitting plane

                switch (tag)
                {
                    case 0x42506E6E: // BPnn
                    case 0x4250496E: // BPIn
                        Node(type);
                        break;
                    case 0x4270494E: // BpIN
                    case 0x42706E4E: // BpnN
                        Node(type);
                        break;
                    case 0x4250494E: // BPIN
                    case 0x42506E4E: // BPnN
                        Node(type);
                        Node(type);
                        break;
                }

                if (type == TreeType.Cell)
                    return;

                Copy(16); // sphere

                if (type == TreeType.Physics)
                    return;

                var numPolys = CopyU32();
                Copy(2 * (int)numPolys);
                SkipInPad();
            }

            private void Portal(TreeType type)
            {
                Copy(4);
                Copy(16);
                Node(type);
                Node(type);

                if (type != TreeType.Drawing)
                    return;

                Copy(16);
                var numPolys = CopyU32();
                var numPortals = CopyU32();
                Copy(2 * (int)numPolys);
                Copy(4 * (int)numPortals);
                SkipInPad();
            }

            private void Leaf(TreeType type)
            {
                Copy(4); // tag
                Copy(4); // leaf index

                if (type != TreeType.Physics)
                    return;

                Copy(4);  // solid
                Copy(16); // sphere
                var numPolys = CopyU32();
                Copy(2 * (int)numPolys);
                SkipInPad();
            }
        }
    }
}
