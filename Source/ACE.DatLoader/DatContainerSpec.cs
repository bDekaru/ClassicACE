namespace ACE.DatLoader
{
    /// <summary>
    /// The values a new post-ToD dat carries in its header (DiskFileInfo_t, 0x50 bytes at 0x140), everything the file's contents do not determine.
    /// The client creates a dat the same way (DiskController::InitFile: DiskFileInfo_t::ClearData, then block size, data set, subset and pack versions, then the free list).
    /// The presets are the values of the retail EoR dats.
    /// </summary>
    public class DatContainerSpec
    {
        /// <summary>Block size in bytes: 0x400 for portal and language dats, 0x100 for cell dats.</summary>
        public uint BlockSize { get; set; }

        public DatDatabaseType DataSet { get; set; }

        public uint DataSubset { get; set; }

        /// <summary>The DID of the master map (a DidMapper, 0x25000000 in the portal dat) the client reads every enum to DID mapping from; 0 when the dat has none.
        /// A portal dat without it makes the client unable to name its language dat ("Can't find the local data file ''").</summary>
        public uint MasterMapId { get; set; }

        /// <summary>DiskFileInfo_t::eng_pack_vnum: the client refuses a dat whose value differs from the one it expects (InitFile returns -6).</summary>
        public uint EnginePackVersion { get; set; }

        /// <summary>DiskFileInfo_t::game_pack_vnum: checked like <see cref="EnginePackVersion"/> unless the caller asks for the engine version only.</summary>
        public uint GamePackVersion { get; set; }

        /// <summary>DatIDStamp, 16 bytes: the same value in every retail dat. The client only reports it to the network layer.</summary>
        public byte[] VersionMajor { get; set; } = new byte[] { 0xD2, 0xD7, 0xA7, 0x34, 0x2F, 0x72, 0x46, 0x4C, 0x8A, 0xB4, 0xEF, 0x51, 0x4F, 0x85, 0x6F, 0xFD };

        /// <summary>DatIDStamp, last dword: 0x1A01 in every retail EoR dat (portal, cell, language, highres); the 2005 Throne of Destiny cell dat has 0xDE.</summary>
        public uint VersionMinor { get; set; }

        public static DatContainerSpec Portal => new DatContainerSpec
        {
            BlockSize = 0x400,
            DataSet = DatDatabaseType.Portal,
            DataSubset = 0,
            MasterMapId = 0x25000000,
            EnginePackVersion = 0x6E,
            GamePackVersion = 0,
            VersionMinor = 0x1A01,
        };

        public static DatContainerSpec Cell => new DatContainerSpec
        {
            BlockSize = 0x100,
            DataSet = DatDatabaseType.Cell,
            DataSubset = 1,
            MasterMapId = 0,
            EnginePackVersion = 0x16,
            GamePackVersion = 0,
            VersionMinor = 0x1A01,
        };

        public static DatContainerSpec Language => new DatContainerSpec
        {
            BlockSize = 0x400,
            DataSet = DatDatabaseType.Language,
            DataSubset = 1,
            MasterMapId = 0,
            EnginePackVersion = 0x6E,
            GamePackVersion = 0,
            VersionMinor = 0x1A01,
        };
    }
}
