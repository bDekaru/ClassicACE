using DotRecast.Core.Numerics;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using System;
using System.Collections.Generic;
using System.Linq;
using EnvCell = ACE.DatLoader.FileTypes.EnvCell;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// The geometry a navmesh is built from (see NavMeshBuilder), as Recast takes it: one triangle mesh in Recast space
    /// (Y up), and the convex volumes that mark the door aprons.
    /// </summary>
    public class NavMeshGeometry : IRcInputGeomProvider
    {
        /// <summary>
        /// The vertices, x, y, z in turn, in Recast space.
        /// </summary>
        public float[] Vertices { get; }

        /// <summary>
        /// The triangles, three vertex indices each.
        /// </summary>
        public int[] Faces { get; }

        private readonly RcVec3f bmin;
        private readonly RcVec3f bmax;
        private readonly List<RcConvexVolume> volumes = new List<RcConvexVolume>();
        private readonly RcTriMesh mesh;
        private readonly RcTriMesh[] meshes;

        /// <summary>
        /// Geometry for a set of interior (dungeon) cells, per agent size: with a door apron (see DoorApron) sized for
        /// that agent outside each of the given doorways, marked as a door area. The cells and their static objects are
        /// loaded once, and only the door aprons differ per size. Null if the cells have no geometry.
        /// </summary>
        public static Dictionary<AgentSize, NavMeshGeometry> LoadGeometry(List<EnvCell> cells, IEnumerable<CellGroupDoor> doors, IEnumerable<AgentSize> agentSizes)
        {
            var collector = new MeshGeometryCollector(cells);
            collector.LoadCells();

            // only cells with geometry of their own get a mesh; the aprons alone are not walkable space
            if (collector.Vertices.Count == 0)
                return null;

            var doorList = doors?.ToList() ?? new List<CellGroupDoor>();
            var cellsEnd = collector.GetMark();
            var geometries = new Dictionary<AgentSize, NavMeshGeometry>();

            foreach (var agentSize in agentSizes)
            {
                collector.AddDoorAprons(doorList, agentSize);

                var geometry = FromCollector(collector);
                foreach (var door in doorList)
                    geometry.AddConvexVolume(DoorApron.GetDoorVolume(door, agentSize));
                geometries[agentSize] = geometry;

                collector.Truncate(cellsEnd);
            }

            return geometries;
        }

        /// <summary>
        /// Geometry for a landblock's outdoor tile, in world coordinates: terrain, building shells, static objects and
        /// scenery of the landblock and of the border strip of its neighbors (see MeshGeometryCollector.LoadSurface).
        /// Interior cells are only used to cut the terrain out of building interiors; their geometry goes into their own
        /// group meshes.
        /// </summary>
        public static NavMeshGeometry LoadSurfaceGeometry(uint landblockId, IEnumerable<SurfaceSource> sources)
        {
            var collector = new MeshGeometryCollector(new List<EnvCell>());
            collector.LoadSurface(landblockId, sources);

            return FromCollector(collector);
        }

        private static NavMeshGeometry FromCollector(MeshGeometryCollector collector)
        {
            if (collector.Vertices.Count == 0)
                return null;

            collector.GetMeshArrays(out var vertices, out var faces);
            return new NavMeshGeometry(vertices, faces);
        }

        public NavMeshGeometry(float[] vertices, int[] faces)
        {
            Vertices = vertices;
            Faces = faces;

            bmin = vertices.ToVec3();
            bmax = vertices.ToVec3();
            for (int i = 1; i < vertices.Length / 3; i++)
            {
                bmin = RcVec3f.Min(bmin, vertices.ToVec3(i * 3));
                bmax = RcVec3f.Max(bmax, vertices.ToVec3(i * 3));
            }

            mesh = new RcTriMesh(vertices, faces);
            meshes = new[] { mesh };
        }

        public RcTriMesh GetMesh()
        {
            return mesh;
        }

        public IEnumerable<RcTriMesh> Meshes()
        {
            return meshes;
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

        // Off-mesh connections are part of IRcInputGeomProvider, but none come from the geometry: the ledge drops are found
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
