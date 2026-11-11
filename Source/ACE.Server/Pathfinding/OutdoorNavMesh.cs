using DotRecast.Core.Numerics;
using DotRecast.Detour;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// The outdoors of the whole world as one tiled navmesh, with one tile per landblock at its landblock coordinates.
    /// Detour links the edge polys of neighboring tiles, so routes run across landblock edges. Tiles are added as
    /// landblocks need them and removed when they unload.
    ///
    /// Detour does not allow changing a mesh while it is queried: queries (and any use of poly refs) run under the read
    /// lock, tile changes under the write lock. Never take the write lock while holding the read lock.
    /// </summary>
    internal sealed class OutdoorNavMesh
    {
        /// <summary>
        /// Enough for every landblock of the world (256 x 256).
        /// </summary>
        private const int MaxTiles = 1 << 16;

        /// <summary>
        /// The most polys a single tile may hold.
        /// </summary>
        private const int MaxPolysPerTile = 1 << 20;

        public AgentSize AgentSize { get; }

        public DtNavMesh Mesh { get; }

        private readonly ReaderWriterLockSlim meshLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Per landblock (0xAAAA0000), the ref of its tile. Guarded by meshLock.
        /// </summary>
        private readonly Dictionary<uint, long> tileRefs = new Dictionary<uint, long>();

        public OutdoorNavMesh(AgentSize agentSize, float tileSize, int maxVertsPerPoly)
        {
            AgentSize = agentSize;

            var navMeshParams = new DtNavMeshParams
            {
                orig = RcVec3f.Zero,
                tileWidth = tileSize,
                tileHeight = tileSize,
                maxTiles = MaxTiles,
                maxPolys = MaxPolysPerTile
            };

            Mesh = new DtNavMesh();
            var status = Mesh.Init(in navMeshParams, maxVertsPerPoly);
            if (status.Failed())
                throw new InvalidOperationException($"could not initialize the {agentSize} outdoor navmesh ({status})");
        }

        public void EnterRead()
        {
            meshLock.EnterReadLock();
        }

        public void ExitRead()
        {
            meshLock.ExitReadLock();
        }

        /// <summary>
        /// True if the landblock's tile is in the mesh. Call under the read lock if the answer is acted on while
        /// querying.
        /// </summary>
        public bool HasTile(uint landblockId)
        {
            meshLock.EnterReadLock();
            try
            {
                return tileRefs.ContainsKey(landblockId & 0xFFFF0000);
            }
            finally
            {
                meshLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Adds a landblock's tile, replacing its previous tile if there is one. The tile data must not be changed
        /// afterwards (e.g. its poly flags), as queries read it.
        /// </summary>
        public bool SetTile(uint landblockId, DtMeshData data)
        {
            landblockId &= 0xFFFF0000;

            meshLock.EnterWriteLock();
            try
            {
                if (tileRefs.Remove(landblockId, out var oldRef))
                    Mesh.RemoveTile(oldRef);

                var status = Mesh.AddTile(data, 0, 0, out var tileRef);
                if (status.Failed() || tileRef == 0)
                    return false;

                tileRefs[landblockId] = tileRef;
                return true;
            }
            finally
            {
                meshLock.ExitWriteLock();
            }
        }

        public void RemoveTile(uint landblockId)
        {
            landblockId &= 0xFFFF0000;

            meshLock.EnterWriteLock();
            try
            {
                if (tileRefs.Remove(landblockId, out var tileRef))
                    Mesh.RemoveTile(tileRef);
            }
            finally
            {
                meshLock.ExitWriteLock();
            }
        }
    }
}
