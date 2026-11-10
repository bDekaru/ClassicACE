using System;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Toolset;
using DotRecast.Recast.Toolset.Builder;

namespace ACE.Server.Pathfinding.Geometry
{

    public class NavMeshBuilder
    {
        public DtMeshData Build(CellGeometryProvider geom, RcNavMeshBuildSettings settings)
        {
            return Build(geom,
                RcPartitionType.OfValue(settings.partitioning),
                settings.cellSize, settings.cellHeight,
                settings.agentMaxSlope, settings.agentHeight, settings.agentRadius, settings.agentMaxClimb,
                settings.minRegionSize, settings.mergedRegionSize,
                settings.edgeMaxLen, settings.edgeMaxError,
                settings.vertsPerPoly,
                settings.detailSampleDist, settings.detailSampleMaxError,
                settings.filterLowHangingObstacles, settings.filterLedgeSpans, settings.filterWalkableLowHeightSpans,
                settings.keepInterResults);
        }

        public DtMeshData Build(CellGeometryProvider geom,
            RcPartition partitionType,
            float cellSize, float cellHeight,
            float agentMaxSlope, float agentHeight, float agentRadius, float agentMaxClimb,
            int regionMinSize, int regionMergeSize,
            float edgeMaxLen, float edgeMaxError,
            int vertsPerPoly,
            float detailSampleDist, float detailSampleMaxError,
            bool filterLowHangingObstacles, bool filterLedgeSpans, bool filterWalkableLowHeightSpans,
            bool keepInterResults)
        {
            RcConfig cfg = new RcConfig(
                partitionType,
                cellSize, cellHeight,
                agentMaxSlope, agentHeight, agentRadius, agentMaxClimb,
                regionMinSize, regionMergeSize,
                edgeMaxLen, edgeMaxError,
                vertsPerPoly,
                detailSampleDist, detailSampleMaxError,
                filterLowHangingObstacles, filterLedgeSpans, filterWalkableLowHeightSpans,
                SampleAreaModifications.SAMPLE_AREAMOD_WALKABLE, true);

            RcBuilderResult rcResult = BuildRecastResult(geom, cfg, keepInterResults);
            var meshData = BuildMeshData(geom, cellSize, cellHeight, agentHeight, agentRadius, agentMaxClimb, rcResult);

            return meshData;
        }

        /// <summary>
        /// Builds one tile of a tiled navmesh: the square of the given size at (tileX, tileZ) on the tile grid, which
        /// starts at the world origin. The geometry should reach past the tile by at least the Recast border (the agent
        /// radius plus 3 cells); the border is rasterized and then trimmed, so the walkable area at the tile edge is
        /// correct and its edge polys line up with the neighboring tiles', which lets Detour link them.
        /// </summary>
        public DtMeshData BuildTile(CellGeometryProvider geom, RcNavMeshBuildSettings settings, int tileX, int tileZ, float tileSize)
        {
            var tileCells = (int)MathF.Round(tileSize / settings.cellSize);
            var borderSize = RcConfig.CalcBorder(settings.agentRadius, settings.cellSize);

            // the tile constructor takes the region sizes as areas in world units
            var cellArea = settings.cellSize * settings.cellSize;

            var cfg = new RcConfig(
                true, tileCells, tileCells, borderSize,
                RcPartitionType.OfValue(settings.partitioning),
                settings.cellSize, settings.cellHeight,
                settings.agentMaxSlope, settings.agentHeight, settings.agentRadius, settings.agentMaxClimb,
                settings.minRegionSize * settings.minRegionSize * cellArea, settings.mergedRegionSize * settings.mergedRegionSize * cellArea,
                settings.edgeMaxLen, settings.edgeMaxError,
                settings.vertsPerPoly,
                settings.detailSampleDist, settings.detailSampleMaxError,
                settings.filterLowHangingObstacles, settings.filterLedgeSpans, settings.filterWalkableLowHeightSpans,
                SampleAreaModifications.SAMPLE_AREAMOD_WALKABLE, true);

            // the tile's xz bounds come from the tile coordinates (plus the border), only the height range from the geometry
            var bmin = new RcVec3f(0f, geom.GetMeshBoundsMin().Y, 0f);
            var bmax = new RcVec3f(0f, geom.GetMeshBoundsMax().Y, 0f);
            var bcfg = new RcBuilderConfig(cfg, bmin, bmax, tileX, tileZ);

            var rcResult = new RcBuilder().Build(geom, bcfg, false);
            if (rcResult?.Mesh == null || rcResult.Mesh.npolys == 0)
                return null;

            var option = DemoNavMeshBuilder.GetNavMeshCreateParams(geom, settings.cellSize, settings.cellHeight, settings.agentHeight, settings.agentRadius, settings.agentMaxClimb, rcResult);
            option.tileX = tileX;
            option.tileZ = tileZ;
            option.bmin = rcResult.Mesh.bmin;
            option.bmax = rcResult.Mesh.bmax;

            var meshData = DtNavMeshBuilder.CreateNavMeshData(option);
            if (meshData == null)
                return null;

            return DemoNavMeshBuilder.UpdateAreaAndFlags(meshData);
        }

        private DtNavMesh BuildNavMesh(DtMeshData meshData, int vertsPerPoly)
        {
            var mesh = new DtNavMesh();
            var status = mesh.Init(meshData, vertsPerPoly, 0);
            if (status.Failed())
            {
                return null;
            }

            return mesh;
        }

        private RcBuilderResult BuildRecastResult(CellGeometryProvider geom, RcConfig cfg, bool keepInterResults)
        {
            RcBuilderConfig bcfg = new RcBuilderConfig(cfg, geom.GetMeshBoundsMin(), geom.GetMeshBoundsMax());
            RcBuilder rcBuilder = new RcBuilder();
            return rcBuilder.Build(geom, bcfg, keepInterResults);
        }

        public DtMeshData BuildMeshData(CellGeometryProvider geom,
            float cellSize, float cellHeight,
            float agentHeight, float agentRadius, float agentMaxClimb,
            RcBuilderResult result)
        {
            DtNavMeshCreateParams option = DemoNavMeshBuilder
                .GetNavMeshCreateParams(geom, cellSize, cellHeight, agentHeight, agentRadius, agentMaxClimb, result);

            var meshData = DtNavMeshBuilder.CreateNavMeshData(option);
            if (null == meshData)
            {
                return null;
            }

            return DemoNavMeshBuilder.UpdateAreaAndFlags(meshData);
        }
    }
}
