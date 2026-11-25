using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Reachability;
using ACE.Server.Physics;
using ACE.Server.Physics.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The world reachability bake: which landblocks need their connectivity found again, from fingerprints of what
    /// each one is read from.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Part of the reachability world key: bump it when the terrain pass changes what it finds.
        /// </summary>
        private const int TerrainPassVersion = 1;

        public static string ReachabilityDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Reachability");

        private static string ReachabilityManifestPath => Path.Combine(ReachabilityDirectory, "manifest.bin");

        /// <summary>
        /// Brings the reachability manifest up to date with the dats and saves it: an entry whose method or fingerprint
        /// changed, or every entry if the world key changed, is pending again. While the dats are unchanged nothing is
        /// read. Reads the whole world (about a minute), so it runs off the world thread.
        /// </summary>
        public static ReachabilityManifest UpdateReachabilityManifest()
        {
            var manifest = ReachabilityManifest.Load(ReachabilityManifestPath) ?? new ReachabilityManifest();

            var worldKey = FormattableString.Invariant($"{TerrainPassVersion}|{PhysicsGlobals.FloorZ}|{GetMeshCacheKey(true, AgentSize.Small)}");

            // the server build too: the fingerprints change with the code that collects the geometry
            var datIdentity = string.Join("|", new[] { DatManager.CellDat.FilePath, DatManager.PortalDat.FilePath }.Select(path =>
            {
                var file = new FileInfo(path);
                return $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";
            })) + $"|{typeof(Pathfinder).Assembly.ManifestModule.ModuleVersionId}";

            if (manifest.WorldKey == worldKey && manifest.DatIdentity == datIdentity && !manifest.Entries.Any(e => e.Method == ReachabilityMethod.Unchecked))
                return manifest;

            var worldChanged = manifest.WorldKey != worldKey;
            var changed = 0;

            // the scan reads the whole world, so what it adds to the cell dat cache is dropped afterwards, except for
            // landblocks in use
            var cachedBefore = new HashSet<uint>(DatManager.CellDat.FileCache.Keys);

            for (var i = 0; i < ReachabilityManifest.LandblockCount; i++)
            {
                var landblockId = (uint)i << 16;
                ReachabilityMethod method;
                ulong fingerprint;
                try
                {
                    (method, fingerprint) = GetReachabilityInput(landblockId);
                }
                catch (Exception e)
                {
                    log.Warn($"Pathfinder: could not fingerprint 0x{landblockId:X8} for reachability: {e}");
                    manifest.Entries[i] = new ReachabilityEntry { Method = ReachabilityMethod.Unchecked, Status = ReachabilityStatus.Failed };
                    continue;
                }

                var entry = manifest.Entries[i];
                if (!worldChanged && entry.Method == method && entry.Fingerprint == fingerprint && entry.Status != ReachabilityStatus.Failed)
                    continue;

                manifest.Entries[i] = new ReachabilityEntry
                {
                    Method = method,
                    Status = method == ReachabilityMethod.None ? ReachabilityStatus.Done : ReachabilityStatus.Pending,
                    Fingerprint = fingerprint
                };
                changed++;
            }

            foreach (var fileId in DatManager.CellDat.FileCache.Keys)
            {
                // x or y 0xFF is past the world (neighbors of the last row), never loaded
                var outsideWorld = fileId >> 24 == 0xFF || (fileId >> 16 & 0xFF) == 0xFF;
                if (!cachedBefore.Contains(fileId) && (outsideWorld || !LandblockManager.IsLoaded(new LandblockId(fileId | 0xFFFF))))
                    DatManager.CellDat.FileCache.TryRemove(fileId, out _);
            }

            manifest.WorldKey = worldKey;
            manifest.DatIdentity = datIdentity;
            if (!manifest.Save(ReachabilityManifestPath))
                log.Warn($"Pathfinder: could not save the reachability manifest {ReachabilityManifestPath}");

            log.Info($"Pathfinder: reachability manifest updated, {changed} landblocks changed{(worldChanged ? " (new world key)" : "")}");
            return manifest;
        }

        /// <summary>
        /// How a landblock's connectivity is found, and the fingerprint of what that reads: the Recast tile's geometry
        /// (the same fingerprint as its cache file, see GetMeshPath), or the terrain's heights and types.
        /// </summary>
        private static (ReachabilityMethod method, ulong fingerprint) GetReachabilityInput(uint landblockId)
        {
            // a scan of the whole world, so the landblocks are left out of the dat cache
            var cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(landblockId | 0xFFFF, false);
            if (cellLandblock.Height.Count != LandDefs.VertexDim * LandDefs.VertexDim || cellLandblock.Terrain.Count != LandDefs.VertexDim * LandDefs.VertexDim)
                return (ReachabilityMethod.None, 0);

            // physics blocks entirely water landblocks (see LandCell)
            var terrain = new LandblockStruct(cellLandblock);
            if (terrain.WaterType == LandDefs.WaterType.EntirelyWater)
                return (ReachabilityMethod.None, 0);

            if (cellLandblock.HasObjects)
            {
                var geom = LoadOutdoorGeometry(landblockId);
                return geom == null ? (ReachabilityMethod.None, 0) : (ReachabilityMethod.Recast, geom.GetFingerprint());
            }

            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                hash.AppendData(MemoryMarshal.AsBytes(terrain.VertexArray.Vertices.Select(v => v.Origin.Z).ToArray().AsSpan()));
                hash.AppendData(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(cellLandblock.Terrain)));
                return (ReachabilityMethod.Terrain, BitConverter.ToUInt64(hash.GetHashAndReset(), 0));
            }
        }
    }
}
