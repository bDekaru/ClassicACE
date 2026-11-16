using System.IO;

namespace ACE.DatLoader
{
    public class DatDirectoryHeader : IUnpackable
    {
        internal const uint ObjectSize = ((sizeof(uint) * 0x3E) + sizeof(uint) + (DatFile.ObjectSize * 0x3D));

        internal const uint RetailObjectSize = ((sizeof(uint) * 0x3E) + sizeof(uint) + (DatFile.RetailObjectSize * 0x3D));

        private readonly bool retail;

        public uint[] Branches { get; } = new uint[0x3E];
        public uint EntryCount { get; private set; }
        public DatFile[] Entries { get; private set; }

        public DatDirectoryHeader(bool retail = false)
        {
            this.retail = retail;
        }

        public void Unpack(BinaryReader reader)
        {
            for (int i = 0; i < Branches.Length; i++)
                Branches[i] = reader.ReadUInt32();

            EntryCount = reader.ReadUInt32();

            Entries = new DatFile[EntryCount];

            for (int i = 0; i < EntryCount; i++)
            {
                Entries[i] = new DatFile();

                if (retail)
                    Entries[i].UnpackRetail(reader);
                else
                    Entries[i].Unpack(reader);
            }
        }
    }
}
