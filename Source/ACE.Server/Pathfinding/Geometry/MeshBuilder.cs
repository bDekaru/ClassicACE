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
