using System;
using System.Collections.Generic;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;

namespace ACE.Server.Pathfinding.Geometry
{

    /// <summary>
    /// Builds navmeshes with Recast and Detour: one single-tile mesh per interior cell group, one tile per landblock
    /// outdoors, each with its ledge drops (see LedgeDropFinder).
    /// </summary>
    public static class NavMeshBuilder
    {
        /// <summary>
        /// Builds a single-tile navmesh (an interior cell group) over all of the geometry.
        /// </summary>
        public static DtMeshData Build(NavMeshGeometry geom, MeshBuildSettings settings)
        {
            var bcfg = new RcBuilderConfig(CreateConfig(settings), geom.GetMeshBoundsMin(), geom.GetMeshBoundsMax());
            var rcResult = new RcBuilder().Build(geom, bcfg, false);
            if (rcResult?.Mesh == null || rcResult.Mesh.npolys == 0)
                return null;

            return CreateMeshData(GetCreateParams(rcResult, settings), geom, settings);
        }

        /// <summary>
        /// Builds one tile of a tiled navmesh: the square of the given size at (tileX, tileZ) on the tile grid, which
        /// starts at the world origin. The geometry should reach past the tile by at least the Recast border (the agent
        /// radius plus 3 cells); the border is rasterized and then trimmed, so the walkable area at the tile edge is
        /// correct and its edge polys line up with the neighboring tiles', which lets Detour link them.
        /// </summary>
        public static DtMeshData BuildTile(NavMeshGeometry geom, MeshBuildSettings settings, int tileX, int tileZ, float tileSize)
        {
            var tileCells = (int)MathF.Round(tileSize / settings.CellSize);
            var borderSize = RcConfig.CalcBorder(settings.AgentRadius, settings.CellSize);
            var cfg = CreateConfig(settings, true, tileCells, borderSize);

            // the tile's xz bounds come from the tile coordinates (plus the border), only the height range from the geometry
            var bmin = new RcVec3f(0f, geom.GetMeshBoundsMin().Y, 0f);
            var bmax = new RcVec3f(0f, geom.GetMeshBoundsMax().Y, 0f);
            var bcfg = new RcBuilderConfig(cfg, bmin, bmax, tileX, tileZ);

            var rcResult = new RcBuilder().Build(geom, bcfg, false);
            if (rcResult?.Mesh == null || rcResult.Mesh.npolys == 0)
                return null;

            var option = GetCreateParams(rcResult, settings);
            option.tileX = tileX;
            option.tileZ = tileZ;

            return CreateMeshData(option, geom, settings);
        }

        /// <summary>
        /// The Recast config for a build: a single mesh over all of the geometry, or one tile of a tiled mesh.
        /// </summary>
        private static RcConfig CreateConfig(MeshBuildSettings settings, bool useTiles = false, int tileCells = 0, int borderSize = 0)
        {
            return new RcConfig(
                useTiles, tileCells, tileCells, borderSize,
                settings.Partitioning,
                settings.CellSize, settings.CellHeight,
                settings.AgentMaxSlope, settings.AgentHeight, settings.AgentRadius, settings.AgentMaxClimb,
                settings.MinRegionArea, settings.MergedRegionArea,
                settings.EdgeMaxLen, settings.EdgeMaxError,
                settings.VertsPerPoly,
                settings.DetailSampleDist, settings.DetailSampleMaxError,
                settings.FilterLowHangingObstacles, settings.FilterLedgeSpans, settings.FilterWalkableLowHeightSpans,
                NavArea.WalkableMod, true);
        }

        /// <summary>
        /// The parameters Detour creates the mesh data from: the polygon and detail meshes of the Recast build, with
        /// each poly's area and flags set (see SetAreasAndFlags), and no off-mesh connections yet.
        /// </summary>
        private static DtNavMeshCreateParams GetCreateParams(RcBuilderResult result, MeshBuildSettings settings)
        {
            var polyMesh = result.Mesh;
            var detailMesh = result.MeshDetail;

            SetAreasAndFlags(polyMesh);

            var option = new DtNavMeshCreateParams
            {
                verts = polyMesh.verts,
                vertCount = polyMesh.nverts,
                polys = polyMesh.polys,
                polyAreas = polyMesh.areas,
                polyFlags = polyMesh.flags,
                polyCount = polyMesh.npolys,
                nvp = polyMesh.nvp,

                walkableHeight = settings.AgentHeight,
                walkableRadius = settings.AgentRadius,
                walkableClimb = settings.AgentMaxClimb,
                bmin = polyMesh.bmin,
                bmax = polyMesh.bmax,
                cs = settings.CellSize,
                ch = settings.CellHeight,
                buildBvTree = true,

                offMeshConCount = 0,
                offMeshConVerts = Array.Empty<float>(),
                offMeshConRad = Array.Empty<float>(),
                offMeshConDir = Array.Empty<int>(),
                offMeshConAreas = Array.Empty<int>(),
                offMeshConFlags = Array.Empty<int>(),
                offMeshConUserID = Array.Empty<int>()
            };

            if (detailMesh != null)
            {
                option.detailMeshes = detailMesh.meshes;
                option.detailVerts = detailMesh.verts;
                option.detailVertsCount = detailMesh.nverts;
                option.detailTris = detailMesh.tris;
                option.detailTriCount = detailMesh.ntris;
            }

            return option;
        }

        /// <summary>
        /// Gives each poly of the Recast build its area and flags: the walkable area Recast marks becomes Ground, and
        /// floor is flagged Walk and door aprons Door (see NavArea, NavPolyFlags).
        /// </summary>
        private static void SetAreasAndFlags(RcPolyMesh polyMesh)
        {
            for (var i = 0; i < polyMesh.npolys; i++)
            {
                if (polyMesh.areas[i] == NavArea.Walkable)
                    polyMesh.areas[i] = NavArea.Ground;

                polyMesh.flags[i] = polyMesh.areas[i] == NavArea.Door ? NavPolyFlags.Door : NavPolyFlags.Walk;
            }
        }

        /// <summary>
        /// Creates the mesh data, with its ledge drops (see LedgeDropFinder) as one-way off-mesh connections:
        /// the ledges are found on the mesh without them, then the mesh data is created again with them. The ledge drops
        /// get their area and flags from their connections.
        /// </summary>
        private static DtMeshData CreateMeshData(DtNavMeshCreateParams option, IRcInputGeomProvider geom, MeshBuildSettings settings)
        {
            var meshData = DtNavMeshBuilder.CreateNavMeshData(option);
            if (meshData == null)
                return null;

            var ledgeDrops = LedgeDropFinder.Find(meshData, geom, settings.VertsPerPoly, settings.AgentRadius, settings.AgentHeight, settings.AgentMaxClimb);
            if (ledgeDrops.Count == 0)
                return meshData;

            SetOffMeshConnections(option, ledgeDrops);

            return DtNavMeshBuilder.CreateNavMeshData(option) ?? meshData;
        }

        private static void SetOffMeshConnections(DtNavMeshCreateParams option, List<RcOffMeshConnection> connections)
        {
            var count = connections.Count;
            option.offMeshConCount = count;
            option.offMeshConVerts = new float[count * 6];
            option.offMeshConRad = new float[count];
            option.offMeshConDir = new int[count];
            option.offMeshConAreas = new int[count];
            option.offMeshConFlags = new int[count];
            option.offMeshConUserID = new int[count];

            for (var i = 0; i < count; i++)
            {
                var connection = connections[i];
                Array.Copy(connection.verts, 0, option.offMeshConVerts, i * 6, 6);
                option.offMeshConRad[i] = connection.radius;
                option.offMeshConDir[i] = connection.bidir ? DtDetour.DT_OFFMESH_CON_BIDIR : 0;
                option.offMeshConAreas[i] = connection.area;
                option.offMeshConFlags[i] = connection.flags;
                option.offMeshConUserID[i] = i;
            }
        }
    }
}
