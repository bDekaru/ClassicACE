using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using log4net;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// Builds navmeshes with Recast and Detour: one single-tile mesh per interior cell group, one tile per landblock
    /// outdoors, each with its ledge drops (see LedgeDropFinder).
    /// </summary>
    public static class NavMeshBuilder
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The coarsest detail height error a tile retries with (see BuildTile): all six tiles that hit the limit build at 4 m.
        /// </summary>
        private const float MaxDetailSampleMaxError = 4f;

        /// <summary>
        /// How many cells a single-tile build's grid reaches past the geometry on every side (see Build).
        /// </summary>
        private const int BoundsPaddingCells = 2;

        /// <summary>
        /// What Recast has written to the console during this thread's build, null outside one (see RunBuild).
        /// </summary>
        [ThreadStatic]
        private static List<string> recastMessages;

        // DotRecast writes to the console itself, with no setting to change that
        static NavMeshBuilder()
        {
            Console.SetOut(new RecastConsoleWriter(Console.Out));
            Console.SetError(new RecastConsoleWriter(Console.Error));
        }

        /// <summary>
        /// Passes text on to a console writer, except the lines written during a Recast build, which are collected for
        /// the log, leaving out the harmless detail mesh warnings.
        /// </summary>
        private sealed class RecastConsoleWriter(TextWriter inner) : TextWriter
        {
            public override Encoding Encoding => inner.Encoding;

            public override void Write(char value) => inner.Write(value);

            public override void Write(string value) => inner.Write(value);

            public override void Write(char[] buffer, int index, int count) => inner.Write(buffer, index, count);

            public override void WriteLine(string value)
            {
                if (recastMessages == null)
                    inner.WriteLine(value);
                else if (value != null && !value.StartsWith("delaunayHull: Removing dangling face", StringComparison.Ordinal)
                    && !value.StartsWith("Walk towards polygon center failed to reach center", StringComparison.Ordinal))
                    recastMessages.Add(value);
            }

            public override void Flush() => inner.Flush();
        }

        /// <summary>
        /// Builds a single-tile navmesh (an interior cell group) over all of the geometry.
        /// </summary>
        /// <param name="name">What is being built, for the log</param>
        public static DtMeshData Build(NavMeshGeometry geom, MeshBuildSettings settings, string name)
        {
            // Recast rounds the grid to whole cells and can cut off the geometry's last half cell (an outer wall), and
            // the erosion needs the columns past the outermost geometry to be empty, not off the grid
            var padding = new RcVec3f(BoundsPaddingCells * settings.CellSize, 0f, BoundsPaddingCells * settings.CellSize);
            var bcfg = new RcBuilderConfig(CreateConfig(settings), geom.GetMeshBoundsMin() - padding, geom.GetMeshBoundsMax() + padding);
            var rcResult = RunBuild(geom, settings, bcfg, name);
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
        public static DtMeshData BuildTile(NavMeshGeometry geom, MeshBuildSettings settings, int tileX, int tileZ, float tileSize, string name)
        {
            var tileCells = (int)MathF.Round(tileSize / settings.CellSize);
            var borderSize = RcConfig.CalcBorder(settings.AgentRadius, settings.CellSize);

            // the tile's xz bounds come from the tile coordinates (plus the border), only the height range from the geometry
            var bmin = new RcVec3f(0f, geom.GetMeshBoundsMin().Y, 0f);
            var bmax = new RcVec3f(0f, geom.GetMeshBoundsMax().Y, 0f);

            // DotRecast throws where Recast trims a poly's detail mesh to 255 triangles (one huge poly over rolling
            // ground): a coarser height fit keeps the tile, and its polys are the same
            RcBuilderResult rcResult;
            for (var detailError = settings.DetailSampleMaxError; ; detailError = MathF.Min(MathF.Max(detailError * 2, 1f), MaxDetailSampleMaxError))
            {
                var bcfg = new RcBuilderConfig(CreateConfig(settings, true, tileCells, borderSize, detailError), bmin, bmax, tileX, tileZ);
                try
                {
                    rcResult = RunBuild(geom, settings, bcfg, name);
                    break;
                }
                catch (Exception e) when (e.Message.StartsWith("rcBuildPolyMeshDetail") && detailError < MaxDetailSampleMaxError)
                {
                }
            }

            if (rcResult?.Mesh == null || rcResult.Mesh.npolys == 0)
                return null;

            var option = GetCreateParams(rcResult, settings);
            option.tileX = tileX;
            option.tileZ = tileZ;

            return CreateMeshData(option, geom, settings);
        }

        /// <summary>
        /// Recast's build with its filters and erosion replaced by ObstacleErosion's.
        /// </summary>
        private static RcBuilderResult RunBuild(NavMeshGeometry geom, MeshBuildSettings settings, RcBuilderConfig bcfg, string name)
        {
            recastMessages = new List<string>();
            try
            {
                var context = new RcContext();
                var solid = RcVoxelizations.BuildSolidHeightfield(context, geom, bcfg);
                ObstacleErosion.FilterAndErode(context, solid, bcfg.cfg, settings);

                return new RcBuilder().Build(context, bcfg.tileX, bcfg.tileZ, geom, bcfg.cfg, solid, false);
            }
            finally
            {
                var messages = recastMessages;
                recastMessages = null;

                // logged only now: logging from inside the writer, under the console's lock, could deadlock with the
                // console appender
                foreach (var message in messages)
                    log.Info($"[RECAST] {name}: {message}");
            }
        }

        /// <summary>
        /// The Recast config for a build: a single mesh over all of the geometry, or one tile of a tiled mesh. It has no
        /// agent radius or filters, as RunBuild applies them itself.
        /// </summary>
        private static RcConfig CreateConfig(MeshBuildSettings settings, bool useTiles = false, int tileCells = 0, int borderSize = 0, float? detailSampleMaxError = null)
        {
            return new RcConfig(
                useTiles, tileCells, tileCells, borderSize,
                settings.Partitioning,
                settings.CellSize, settings.CellHeight,
                settings.AgentMaxSlope, settings.AgentHeight, 0f, settings.AgentMaxClimb,
                settings.MinRegionArea, settings.MergedRegionArea,
                settings.EdgeMaxLen, settings.EdgeMaxError,
                settings.VertsPerPoly,
                settings.DetailSampleDist, detailSampleMaxError ?? settings.DetailSampleMaxError,
                false, false, false,
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
        /// floor is flagged Walk (narrow floor Narrow too) and opening aprons Opening (see NavArea, NavPolyFlags).
        /// </summary>
        private static void SetAreasAndFlags(RcPolyMesh polyMesh)
        {
            for (var i = 0; i < polyMesh.npolys; i++)
            {
                if (polyMesh.areas[i] == NavArea.Walkable)
                    polyMesh.areas[i] = NavArea.Ground;

                polyMesh.flags[i] = polyMesh.areas[i] switch
                {
                    NavArea.Opening => NavPolyFlags.Opening,
                    NavArea.Narrow => NavPolyFlags.Walk | NavPolyFlags.Narrow,
                    _ => NavPolyFlags.Walk
                };
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

            var ledgeDrops = LedgeDropFinder.Find(meshData, geom, settings.AgentRadius, settings.AgentHeight, settings.AgentMaxClimb, settings.AgentMaxStepDown);
            if (ledgeDrops.Count > 0)
            {
                SetOffMeshConnections(option, ledgeDrops);
                meshData = DtNavMeshBuilder.CreateNavMeshData(option) ?? meshData;
            }

            LedgeDropFinder.MarkDropEdges(meshData, geom, settings.AgentRadius, settings.AgentHeight, settings.AgentMaxClimb);
            return meshData;
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
