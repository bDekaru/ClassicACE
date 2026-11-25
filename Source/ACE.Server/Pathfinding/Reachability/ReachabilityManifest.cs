using System;
using System.IO;

namespace ACE.Server.Pathfinding.Reachability
{
    /// <summary>
    /// How a landblock's outdoor connectivity is found.
    /// </summary>
    public enum ReachabilityMethod : byte
    {
        /// <summary>
        /// Not looked at yet.
        /// </summary>
        Unchecked,

        /// <summary>
        /// Nothing to walk on: past the edge of the world, or entirely water.
        /// </summary>
        None,

        /// <summary>
        /// The terrain alone: neither the landblock nor a neighbor has a building or static object that reaches into
        /// it. Scenery is left out.
        /// </summary>
        Terrain,

        /// <summary>
        /// Navmeshes: the landblock's outdoor tile, or its interior cell groups' meshes.
        /// </summary>
        Recast
    }

    public enum ReachabilityStatus : byte
    {
        Pending,
        Done,
        Failed
    }

    public struct ReachabilityEntry
    {
        public ReachabilityMethod Method;
        public ReachabilityStatus Status;

        /// <summary>
        /// The fingerprint of what the method reads (see Pathfinder.GetReachabilityInput): the entry is pending again
        /// when it changes.
        /// </summary>
        public ulong Fingerprint;

        /// <summary>
        /// The landblock's interior cell groups, apart from its outdoors: Recast if it has any, None if not.
        /// </summary>
        public ReachabilityMethod InteriorMethod;
        public ReachabilityStatus InteriorStatus;

        /// <summary>
        /// The fingerprint of the groups' Small mesh geometry (see Pathfinder.GetInteriorInput).
        /// </summary>
        public ulong InteriorFingerprint;
    }

    /// <summary>
    /// The state of the world reachability bake, one entry per landblock, saved in one file. The world key covers
    /// what every entry depends on (versions, the walkable slope, the mesh settings): when it changes, every entry is
    /// pending again. The dat identity (the dat files and the server build) lets a start skip the fingerprint check
    /// while neither changed.
    /// </summary>
    public class ReachabilityManifest
    {
        private const uint Magic = 0x4D524341; // "ACRM"

        private const int FormatVersion = 2;

        public const int LandblockCount = 0x10000;

        public string WorldKey { get; set; } = "";

        public string DatIdentity { get; set; } = "";

        /// <summary>
        /// By landblock index (landblock id >> 16).
        /// </summary>
        public ReachabilityEntry[] Entries { get; } = new ReachabilityEntry[LandblockCount];

        /// <summary>
        /// Null if the file is missing, unreadable or of another format version.
        /// </summary>
        public static ReachabilityManifest Load(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != Magic || reader.ReadInt32() != FormatVersion)
                        return null;

                    var manifest = new ReachabilityManifest
                    {
                        WorldKey = reader.ReadString(),
                        DatIdentity = reader.ReadString()
                    };

                    if (reader.ReadInt32() != LandblockCount)
                        return null;

                    for (var i = 0; i < LandblockCount; i++)
                    {
                        manifest.Entries[i] = new ReachabilityEntry
                        {
                            Method = (ReachabilityMethod)reader.ReadByte(),
                            Status = (ReachabilityStatus)reader.ReadByte(),
                            Fingerprint = reader.ReadUInt64(),
                            InteriorMethod = (ReachabilityMethod)reader.ReadByte(),
                            InteriorStatus = (ReachabilityStatus)reader.ReadByte(),
                            InteriorFingerprint = reader.ReadUInt64()
                        };
                    }

                    return manifest;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// False (logged) if it could not be saved; an interrupted save leaves the previous manifest (see
        /// Pathfinder.TryWriteFile).
        /// </summary>
        public bool Save(string path)
        {
            return Pathfinder.TryWriteFile(path, writer =>
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(WorldKey);
                writer.Write(DatIdentity);
                writer.Write(LandblockCount);

                foreach (var entry in Entries)
                {
                    writer.Write((byte)entry.Method);
                    writer.Write((byte)entry.Status);
                    writer.Write(entry.Fingerprint);
                    writer.Write((byte)entry.InteriorMethod);
                    writer.Write((byte)entry.InteriorStatus);
                    writer.Write(entry.InteriorFingerprint);
                }
            });
        }
    }
}
