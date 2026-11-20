using System.IO;

namespace ACE.DatLoader.Entity
{
    public class RegionMisc : IUnpackable, IPackable
    {
        public uint Version { get; private set; }
        public uint GameMapID { get; private set; }
        public uint AutotestMapId { get; private set; }
        public uint AutotestMapSize { get; private set; }
        public uint ClearCellId { get; private set; }
        public uint ClearMonsterId { get; private set; }

        public void Unpack(BinaryReader reader)
        {
            Version         = reader.ReadUInt32();
            GameMapID       = reader.ReadUInt32();
            AutotestMapId   = reader.ReadUInt32();
            AutotestMapSize = reader.ReadUInt32();
            ClearCellId     = reader.ReadUInt32();
            ClearMonsterId  = reader.ReadUInt32();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(GameMapID);
            writer.Write(AutotestMapId);
            writer.Write(AutotestMapSize);
            writer.Write(ClearCellId);
            writer.Write(ClearMonsterId);
        }
    }
}
