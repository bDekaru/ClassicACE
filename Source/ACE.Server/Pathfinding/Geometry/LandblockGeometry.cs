using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Contains all geometry data for a landblock. Use different methods to generate different data types,
    /// like for mapping, nav, etc.
    /// </summary>
    public class LandblockGeometry {
        private bool _didLoadTerrain = false;
        private bool _didLoadIndoors = false;
        private bool _didLoadDungeons = false;
        private Dictionary<uint, bool> _checkedCells = new Dictionary<uint, bool>();
        private LandblockInfo _landblockInfo;
        private CellLandblock _cellLandblock;
        private ConcurrentDictionary<uint, CellGeometry> _terrainCells = new ConcurrentDictionary<uint, CellGeometry>();
        private ConcurrentDictionary<uint, CellGeometry> _indoorCells = new ConcurrentDictionary<uint, CellGeometry>();
        private ConcurrentDictionary<uint, CellGeometry> _dungeonCells = new ConcurrentDictionary<uint, CellGeometry>();

        /// <summary>
        /// The id of this landblock, in format 0xFFFF0000
        /// </summary> 
        public uint Id { get; }

        /// <summary>
        /// LandblockInfo from the dat files
        /// </summary>
        public LandblockInfo LandblockInfo {
            get {
                if (_landblockInfo is null) {
                    _landblockInfo = DatManager.CellDat.ReadFromDat<LandblockInfo>(Id + 0xFFFE);
                }

                return _landblockInfo;
            }
        }

        /// <summary>
        /// CellLandblock from the dat files
        /// </summary>
        public CellLandblock CellLandblock {
            get {
                if (_cellLandblock is null) {
                    _cellLandblock = DatManager.CellDat.ReadFromDat<CellLandblock>(Id + 0xFFFF);
                }

                return _cellLandblock;
            }
        }

        /// <summary>
        /// Terrain cells in this landblock, if any. The dictionary key is the landblock + landcell id in the form of 0xAAAABBBB
        /// where AAAA is the landblock id, and BBBB is the landcell id.
        /// </summary>
        public ConcurrentDictionary<uint, CellGeometry> Terrain {
            get {
                if (!_didLoadTerrain) {
                    LoadTerrain();
                }
                return _terrainCells;
            }
        }

        /// <summary>
        /// All indoor cells in this landblock, if any. The dictionary key is the landblock + landcell id in the form of 0xAAAABBBB
        /// where AAAA is the landblock id, and BBBB is the landcell id.
        /// </summary>
        public ConcurrentDictionary<uint, CellGeometry> IndoorCells {
            get {
                if (!_didLoadIndoors) {
                    LoadIndoors();
                }
                return _indoorCells;
            }
        }

        /// <summary>
        /// All dungeon cells in this landblock, if any. The dictionary key is the landblock + landcell id in the form of 0xAAAABBBB
        /// where AAAA is the landblock id, and BBBB is the landcell id.
        /// </summary>
        public ConcurrentDictionary<uint, CellGeometry> DungeonCells {
            get {
                if (!_didLoadDungeons) {
                    LoadDungeons();
                }
                return _dungeonCells;
            }
        }

        /// <summary>
        /// Create a new landblock geometry object
        /// </summary>
        /// <param name="id">The id of the landblock. Should be in format 0xFFFF0000. If less than 0xFFFF, it will be shifted 16bits to the left</param>
        public LandblockGeometry(uint id) {
            if (id <= 0xFFFF) {
                Id = id << 16;
            }
            else {
                Id = id & 0xFFFF0000;
            }
        }

        #region public api
        /// <summary>
        /// Check if this landblock contains any dungeons. Dungeons are defined as a group of connected cells
        /// that are not connected to a building that leads to the terrain. Landblocks that contain dungeons
        /// may also contain terrain, they are not exclusive.
        /// </summary>
        /// <returns>True if this landblock contains dungeons</returns>
        public bool HasDungeons() {
            // todo: i dont think we can properly check this without loading indoor cells first, and checking for
            // leftover cells on this landblock
            return LandblockInfo?.NumCells > 0;
        }

        /// <summary>
        /// Check if this landblock contains walkable terrain data. Landblocks containing terrain may
        /// also contain dungeons / indoor cells.
        /// </summary>
        /// <returns>True if this landblock has walkable terrain</returns>
        public bool HasTerrain() {
            return CellLandblock?.Height?.Where(h => h != 0).Any() == true;
        }

        /// <summary>
        /// Check if this landblock has any buildings with portals to indoor cells. Caves that are entered
        /// directly from a hole in the landscape are considered indoors, the same as buildings.
        /// </summary>
        /// <returns>True if this landblock contains indoor cells</returns>
        public bool HasIndoors() {
            // todo: i'm assuming all buildings have indoor cells..
            return LandblockInfo?.Buildings?.Count() > 0;
        }
        #endregion // public api

        private void LoadTerrain() {
            return;
        }

        private void LoadIndoors() {
            if (_didLoadIndoors)
                return;

            _didLoadIndoors = true;

            if (!HasIndoors())
                return;

            // todo: store buildings, and the cells/portals connecting them to outside
            foreach (var building in LandblockInfo.Buildings) {
                foreach (var portal in building.Portals) {
                    // only interested in portals that lead indoors, and ones we haven't already checked
                    if (portal.OtherCellId < 100) {
                        continue;
                    }
                    var otherCellId = Id + portal.OtherCellId;
                    var indoorStartingCell = CellGeometry.FromCache(otherCellId, CellType.Indoors);

                    var connectedCells = indoorStartingCell.GetConnectedCells(ConnectionStrategy.Visible, _checkedCells, out var neighbors);

                    foreach (var cell in connectedCells) {
                        _indoorCells.TryAdd(cell.CellId, cell);
                    }
                }
            }
        }

        private void LoadDungeons() {
            if (_didLoadDungeons)
                return;

            _didLoadDungeons = true;

            if (!HasDungeons())
                return;

            // the dungeon chunking relies on indoor cells being checked first, to exclude them from dungeon cells.
            // todo: maybe check for portals to outside? or reverse check buildings that portal to any of these cells?
            if (!_didLoadIndoors) {
                LoadIndoors();
            }

            for (var i = 0; i < LandblockInfo.NumCells; i++) {
                var cell = CellGeometry.FromCache(Id + 0x0100 + (uint)i, CellType.Dungeon);
                _dungeonCells.TryAdd(cell.CellId, cell);
                /*
                if (_checkedCells.ContainsKey(dungeonStartingCell.CellId))
                    continue;

                var connectedCells = dungeonStartingCell.GetConnectedCells(ConnectionStrategy.Visible, _checkedCells, out var neighbors);

                foreach (var cell in connectedCells) {
                    var x = _dungeonCells.TryAdd(cell.CellId, cell);
                }
                */
            }
        }
    }
}
