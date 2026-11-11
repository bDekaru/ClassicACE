using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// The geometry a navmesh is built from (see NavMeshBuilder), as Recast takes it: one triangle mesh in Recast space
    /// (Y up), and the convex volumes that mark the door aprons.
    /// </summary>
    public class CellGeometryProvider : IRcInputGeomProvider
    {
        public readonly float[] vertices;
        public readonly int[] faces;
        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        private readonly List<RcConvexVolume> volumes = new List<RcConvexVolume>();
        private readonly RcTriMesh _mesh;
        private readonly RcTriMesh[] _meshes;

        /// <summary>
        /// Geometry for a set of interior (dungeon) cells, for the mesh of one agent width: with a door apron (see
        /// DoorApron) sized for that agent outside each of the given doorways, marked as a door area.
        /// </summary>
        public static CellGeometryProvider LoadGeometry(LandblockGeometry geometry, List<CellGeometry> neighbors, IEnumerable<CellGroupDoor> doors, AgentWidth agentWidth)
        {
            var providers = LoadGeometry(geometry, neighbors, doors, new[] { agentWidth });
            return providers?[agentWidth];
        }

        /// <summary>
        /// Like the other overload, for several agent widths at once: the cells and their static objects are loaded
        /// once, and only the door aprons differ per width. Null if the cells have no geometry.
        /// </summary>
        public static Dictionary<AgentWidth, CellGeometryProvider> LoadGeometry(LandblockGeometry geometry, List<CellGeometry> neighbors, IEnumerable<CellGroupDoor> doors, IEnumerable<AgentWidth> agentWidths)
        {
            var exporter = new LandblockGeometryExporter(geometry, neighbors);
            exporter.LoadLandblockInfo();

            // only cells with geometry of their own get a mesh; the aprons alone are not walkable space
            if (exporter.Vertices.Count == 0)
                return null;

            var wallDoors = doors?.Where(d => d.IsInWall).ToList() ?? new List<CellGroupDoor>();
            var cells = exporter.GetMark();
            var providers = new Dictionary<AgentWidth, CellGeometryProvider>();

            foreach (var agentWidth in agentWidths)
            {
                exporter.AddDoorAprons(wallDoors, agentWidth);

                var provider = FromExporter(exporter);
                foreach (var door in wallDoors)
                    provider.AddConvexVolume(DoorApron.GetDoorVolume(door, agentWidth));
                providers[agentWidth] = provider;

                exporter.Truncate(cells);
            }

            return providers;
        }

        /// <summary>
        /// Geometry for a landblock's outdoor tile, in world coordinates: terrain, building shells, static objects and
        /// scenery of the landblock and of the border strip of its neighbors (see LandblockGeometryExporter.LoadSurface).
        /// Interior cells are only used to cut the terrain out of building interiors; their geometry goes into their own
        /// group meshes.
        /// </summary>
        public static CellGeometryProvider LoadSurfaceGeometry(uint landblockId, IEnumerable<SurfaceSource> sources)
        {
            var exporter = new LandblockGeometryExporter(new LandblockGeometry(landblockId), new List<CellGeometry>());
            exporter.LoadSurface(landblockId, sources);

            return FromExporter(exporter);
        }

        private static CellGeometryProvider FromExporter(LandblockGeometryExporter exporter)
        {
            if (exporter.Vertices.Count == 0) return null;

            exporter.GetMeshArrays(out var vertices, out var faces);
            return new CellGeometryProvider(vertices, faces);
        }

        public CellGeometryProvider(float[] vertices, int[] faces)
        {
            this.vertices = vertices;
            this.faces = faces;

            bmin = vertices.ToVec3();
            bmax = vertices.ToVec3();
            for (int i = 1; i < vertices.Length / 3; i++)
            {
                bmin = RcVec3f.Min(bmin, vertices.ToVec3(i * 3));
                bmax = RcVec3f.Max(bmax, vertices.ToVec3(i * 3));
            }

            _mesh = new RcTriMesh(vertices, faces);
            _meshes = new[] { _mesh };
        }

        public RcTriMesh GetMesh()
        {
            return _mesh;
        }

        public IEnumerable<RcTriMesh> Meshes()
        {
            return _meshes;
        }

        public RcVec3f GetMeshBoundsMin()
        {
            return bmin;
        }

        public RcVec3f GetMeshBoundsMax()
        {
            return bmax;
        }

        /// <summary>
        /// The volumes that mark areas of the mesh: the door aprons (see DoorApron).
        /// </summary>
        public IList<RcConvexVolume> ConvexVolumes()
        {
            return volumes;
        }

        public void AddConvexVolume(RcConvexVolume convexVolume)
        {
            volumes.Add(convexVolume);
        }

        // Off-mesh connections are part of IRcInputGeomProvider, but none come from the geometry: the drops are found
        // on the built mesh and handed to Detour directly (see NavMeshBuilder).

        public List<RcOffMeshConnection> GetOffMeshConnections()
        {
            return new List<RcOffMeshConnection>();
        }

        public void AddOffMeshConnection(RcVec3f start, RcVec3f end, float radius, bool bidir, int area, int flags)
        {
            throw new NotSupportedException("off-mesh connections are added when the mesh is built, see NavMeshBuilder");
        }

        public void RemoveOffMeshConnections(Predicate<RcOffMeshConnection> filter)
        {
        }
    }
}
