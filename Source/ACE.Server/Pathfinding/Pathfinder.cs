//
// This code is based on Trevis' pathfinding proof of concept at https://gitlab.com/trevis/ace.mods.pathfinding/-/tree/master
//

using ACE.Entity;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Pathfinding.Geometry;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Toolset;
using DotRecast.Recast.Toolset.Tools;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace ACE.Server.Pathfinding
{
    public static class Pathfinder
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int VERTS_PER_POLY = 6;
        private const int MAX_POLYS = 256;

        public static string InsideMeshDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Meshes", "Indoors");
        public static readonly ConcurrentDictionary<uint, DtNavMesh?> Meshes = new ConcurrentDictionary<uint, DtNavMesh?>();

        static Pathfinder() {
            if (!Directory.Exists(InsideMeshDirectory)) {
                Directory.CreateDirectory(InsideMeshDirectory);
            }
        }

        private static bool CreateMarker(Position position)
        {
            var marker = WorldObjectFactory.CreateNewWorldObject((uint)Factories.Enum.WeenieClassName.pathfinderHelper);

            if (marker == null)
                return false;

            marker.Location = position;
            marker.Location.LandblockId = new LandblockId(marker.Location.GetCell());
            var landblock = LandblockManager.GetLandblock(marker.Location.LandblockId, false);

            if (marker.EnterWorld())
            {
                if (landblock.AddWorldObject(marker))
                    return true;
            }

            marker.Destroy();
            return false;
        }

        public static void DrawRoute(List<Position> route)
        {
            if (route == null || route.Count == 0)
                return;

            foreach (var entry in route)
            {
                CreateMarker(entry);
            }
        }

        /// <summary>
        /// Find a route to the end position.
        /// </summary>
        /// <param name="end">The ending position</param>
        /// <returns>A list of positions</returns>
        public static List<Position>? FindRoute(Position start, Position end, bool drawRoute = false)
        {
            if (!TryGetMesh(start, out var mesh) || mesh is null)
            {
                return null;
            }

            if ((start.Cell & 0xFFFF0000) != (end.Cell & 0xFFFF0000))
            {
                log.Warn($"FindRoute only works inside a single landblock.");
                return null;
            }

            var rc = new RcTestNavMeshTool();

            var halfExtents = new RcVec3f(1.25f, 1.25f, 1.25f);

            var query = new DtNavMeshQuery(mesh);
            var m_filter = new DtQueryDefaultFilter();

            var startStatus = query.FindNearestPoly(new RcVec3f(start.PositionX, start.PositionZ, start.PositionY), halfExtents, m_filter, out long startRef, out var startPt, out bool isStartOverPoly);
            var endStatus = query.FindNearestPoly(new RcVec3f(end.PositionX, end.PositionZ, end.PositionY), halfExtents, m_filter, out long endRef, out var endPt, out bool isEndOverPoly);

            var polys = new List<long>();
            DtStraightPath[] path = new DtStraightPath[MAX_POLYS];
            var straightPathCount = 0;

            var res = rc.FindStraightPath(query, startRef, endRef, startPt, endPt, m_filter, true, ref polys, path, out straightPathCount, MAX_POLYS, 0);

            //var res = rc.FindFollowPath(PluginCore.Instance.Nav?.Mesh, query, startRef, endRef, startPt, endPt, m_filter, false, ref polys, ref pts);

            var positionList = new List<Position>();
            for(int i = 0; i < straightPathCount; i++)
            {
                // TODO: proper cell ids..
                var entry = path[i];
                positionList.Add(new Position(start.Cell, new Vector3(entry.pos.X, entry.pos.Z, entry.pos.Y), Quaternion.Identity));
            }

            if(drawRoute)
                DrawRoute(positionList);

            return positionList;
        }

        /// <summary>
        /// Get a random point on the navmesh
        /// </summary>
        /// <param name="start"></param>
        /// <returns></returns>
        public static Position? GetRandomPointOnMesh(Position start)
        {
            if (!TryGetMesh(start, out var mesh) || mesh is null) {
                return null;
            }

            var query = new DtNavMeshQuery(mesh);
            var m_filter = new DtQueryDefaultFilter();
            var frand = new RcRand(DateTime.Now.Ticks);

            query.FindRandomPoint(m_filter, frand, out long randomRef, out var randomPt);

            return new Position(start.Landblock, new Vector3(randomPt.X, randomPt.Z, randomPt.Y), Quaternion.Identity);
        }

        public static bool TryGetMesh(Position pos, out DtNavMesh? mesh)
        {
            if (Meshes.TryGetValue(pos.Cell & 0xFFFF0000, out mesh))
            {
                return mesh is not null;
            }

            Meshes.TryAdd(pos.Cell & 0xFFFF0000, null);

            TryLoadMesh(pos);
            return false;
        }

        public static void TryUnloadMesh(Position pos)
        {
            uint landblockId = pos.Cell & 0xFFFF0000;
            Meshes.TryRemove(landblockId, out _);
        }

        public static void TryUnloadMesh(Landblock landblock)
        {
            uint landblockId = landblock.Id.Raw & 0xFFFF0000;
            Meshes.TryRemove(landblockId, out _);
        }

        private static void TryLoadMesh(Position pos)
        {
            _ = Task.Run(() =>
            {
                if (!pos.Indoors)
                {
                    log.Warn($"PathFinder only works inside dungeons: {pos}");
                    return;
                }

                var geometry = new LandblockGeometry(pos.Cell & 0xFFFF0000);
                if (!geometry.DungeonCells.TryGetValue(pos.Cell, out var cellGeometry))
                {
                    log.Warn($"Could not load cell geometry! {pos} cellGeometry:{cellGeometry}");
                    return;
                }

                Dictionary<uint, bool> checkedCells = new();
                var cells = geometry.DungeonCells.Values.ToList();

                var meshPath = Path.Combine(InsideMeshDirectory, $"{pos.Cell & 0xFFFF0000:X8}.mesh");
                if (File.Exists(meshPath))
                {
                    var meshReader = new DtMeshDataReader();

                    using (var stream = File.OpenRead(meshPath))
                    using (var reader = new BinaryReader(stream))
                    {
                        var rcBytes = new RcByteBuffer(reader.ReadBytes((int)stream.Length));
                        var meshData = meshReader.Read(rcBytes, VERTS_PER_POLY, true);

                        var mesh = new DtNavMesh();
                        mesh.Init(meshData, VERTS_PER_POLY, 0);
                        Meshes.TryUpdate(pos.Cell & 0xFFFF0000, mesh, null);
                        return;
                    }
                }

                var geom = CellGeometryProvider.LoadGeometry(geometry, cells);
                if (geom is null)
                {
                    log.Warn($"Could not load cell geometry provider! {pos} cellGeometry:{geom} neighbors:{string.Join(",", cells.Select(n => $"{n.CellId:X8}"))}");
                    return;

                }

                var builder = new NavMeshBuilder();
                var settings = GetMeshSettings();
                var res = builder.Build(geom, settings);

                var meshWriter = new DtMeshDataWriter();
                using (var stream = File.OpenWrite(meshPath))
                using (var writer = new BinaryWriter(stream))
                {
                    meshWriter.Write(writer, res, RcByteOrder.LITTLE_ENDIAN, false);
                }

                var meshNew = new DtNavMesh();
                meshNew.Init(res, VERTS_PER_POLY, 0);
                Meshes.TryUpdate(pos.Cell & 0xFFFF0000, meshNew, null);
            });
        }

        private static RcNavMeshBuildSettings GetMeshSettings()
        {
            return new RcNavMeshBuildSettings()
            {
                agentHeight = 2f, // made this a little extra, just to try and account for bigger mobs...
                agentMaxClimb = 0.95f,
                agentMaxSlope = 50f,
                cellHeight = 0.1f,
                cellSize = 0.1f,
                agentRadius = 0.7f,//0.45f,
                detailSampleDist = 6.0f,
                detailSampleMaxError = 1.0f,
                edgeMaxError = 1f,
                edgeMaxLen = 12.0f,
                mergedRegionSize = 20,
                minRegionSize = 8,
                vertsPerPoly = VERTS_PER_POLY,
                partitioning = (int)RcPartition.WATERSHED
            };
        }
    }
}
