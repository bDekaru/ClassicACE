using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.DatLoader
{
    /// <summary>The records that make up one building in a cell dat: its exterior models and the environments of the cells inside them.</summary>
    public sealed class DatBuildingGroup
    {
        /// <summary>Exterior models (0x01) of every building in the group, ascending.</summary>
        public IReadOnlyList<uint> Models { get; }

        /// <summary>Environments (0x0D) used by the cells of those buildings, ascending.</summary>
        public IReadOnlyList<uint> Environments { get; }

        /// <summary>Buildings in the cell dat that use the group's models.</summary>
        public int Buildings { get; }

        public DatBuildingGroup(IEnumerable<uint> models, IEnumerable<uint> environments, int buildings)
        {
            Models = models.OrderBy(m => m).ToList();
            Environments = environments.OrderBy(e => e).ToList();
            Buildings = buildings;
        }
    }

    /// <summary>
    /// Which environments each building model uses in a retail-layout cell dat, so a building can be replaced as one unit.
    /// A model and its environments belong together; when an environment is also used by another model, that model joins the group,
    /// and so on, because replacing the environment changes every cell that uses it.
    /// </summary>
    public sealed class DatBuildingGroups
    {
        private readonly Dictionary<uint, HashSet<uint>> _environmentsOfModel = new Dictionary<uint, HashSet<uint>>();
        private readonly Dictionary<uint, HashSet<uint>> _modelsOfEnvironment = new Dictionary<uint, HashSet<uint>>();
        private readonly Dictionary<uint, int> _buildingsOfModel = new Dictionary<uint, int>();

        public DatBuildingGroups(DatDatabase retailCellDat)
        {
            foreach (var infoId in retailCellDat.AllFiles.Keys.Where(k => (k & 0xFFFF) == 0xFFFE && (k >> 16) != 0xFFFF).OrderBy(k => k))
            {
                var info = DatLandblockInfo.Parse(retailCellDat.GetReaderForFile(infoId).Buffer);
                var prefix = infoId & 0xFFFF0000;

                foreach (var building in info.Buildings)
                {
                    _buildingsOfModel[building.ModelId] = _buildingsOfModel.TryGetValue(building.ModelId, out var count) ? count + 1 : 1;

                    if (!_environmentsOfModel.TryGetValue(building.ModelId, out var used))
                        _environmentsOfModel[building.ModelId] = used = new HashSet<uint>();

                    foreach (var environment in EnvironmentsOf(retailCellDat, prefix, building))
                    {
                        used.Add(environment);

                        if (!_modelsOfEnvironment.TryGetValue(environment, out var models))
                            _modelsOfEnvironment[environment] = models = new HashSet<uint>();

                        models.Add(building.ModelId);
                    }
                }
            }
        }

        /// <summary>The group a model belongs to. A model that no building uses forms a group of its own.</summary>
        public DatBuildingGroup Resolve(uint modelId)
        {
            var models = new HashSet<uint> { modelId };
            var environments = new HashSet<uint>();
            var pending = new Queue<uint>();
            pending.Enqueue(modelId);

            while (pending.Count > 0)
            {
                var model = pending.Dequeue();

                if (!_environmentsOfModel.TryGetValue(model, out var used))
                    continue;

                foreach (var environment in used)
                {
                    if (!environments.Add(environment))
                        continue;

                    foreach (var other in _modelsOfEnvironment[environment])
                        if (models.Add(other))
                            pending.Enqueue(other);
                }
            }

            return new DatBuildingGroup(models, environments, models.Sum(m => _buildingsOfModel.TryGetValue(m, out var n) ? n : 0));
        }

        // The cells of a building: those its portals open into and the visible cells of its portals, then every cell reachable through visible-cell lists (Melt's removeCell with recursion).
        private static IEnumerable<uint> EnvironmentsOf(DatDatabase cellDat, uint prefix, DatBuilding building)
        {
            var seen = new HashSet<ushort>();
            var pending = new Stack<ushort>();

            foreach (var portal in building.Portals)
            {
                if (portal.OtherCellId != 0xFFFF)
                    pending.Push(portal.OtherCellId);

                foreach (var visible in portal.VisibleCells)
                    pending.Push(visible);
            }

            var environments = new HashSet<uint>();

            while (pending.Count > 0)
            {
                var low = pending.Pop();

                if (!seen.Add(low) || !cellDat.AllFiles.ContainsKey(prefix | low))
                    continue;

                var record = cellDat.GetReaderForFile(prefix | low).Buffer;
                ReadRetailCell(record, out var environment, out var visibleCells);
                environments.Add(0x0D000000u | environment);

                foreach (var visible in visibleCells)
                    pending.Push(visible);
            }

            return environments;
        }

        // Retail layout: flags, id, surface count, portal count, visible cell count, surfaces (u16), align 4, environment (u16), structure (u16), frame, portals, align 4, visible cells (u16).
        private static void ReadRetailCell(byte[] cell, out ushort environment, out List<ushort> visibleCells)
        {
            var surfaces = cell[8];
            var portals = cell[9];
            var visibleCount = BitConverter.ToUInt16(cell, 10);
            var at = (12 + 2 * surfaces + 3) & ~3;

            environment = BitConverter.ToUInt16(cell, at);

            var visibleAt = (at + 4 + 28 + portals * 8 + 3) & ~3;
            visibleCells = new List<ushort>(visibleCount);

            for (var i = 0; i < visibleCount; i++)
                visibleCells.Add(BitConverter.ToUInt16(cell, visibleAt + i * 2));
        }
    }
}
