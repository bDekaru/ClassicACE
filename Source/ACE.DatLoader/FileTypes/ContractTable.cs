using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// This is the client_portal.dat file 0x0E00001D
    /// </summary>
    [DatFileType(DatFileType.ContractTable)]
    public class ContractTable : FileType
    {
        internal const uint FILE_ID = 0x0E00001D;

        public Dictionary<uint, Contract> Contracts { get; } = new Dictionary<uint, Contract>();

        private ushort bucketSize;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            ushort num_contracts = reader.ReadUInt16();
            bucketSize = reader.ReadUInt16();

            for (ushort i = 0; i < num_contracts; i++)
            {
                uint key = reader.ReadUInt32();

                Contract value = new Contract();
                value.Unpack(reader);

                Contracts.Add(key, value);
            }
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            Contracts.PackPackedHashTable(writer, bucketSize);
        }
    }
}
