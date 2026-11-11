using ACE.Entity;
using ACE.Server.Pathfinding.Geometry;
using DotRecast.Detour;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// Debug dumps of routes as JSON, for looking at them outside the game (e.g. plotting them). See @dumproute.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// Where route dumps are written.
        /// </summary>
        public static string DebugDumpDirectory => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Pathfinding", "Debug");

        /// <summary>
        /// How far around a leg's waypoints (or a failed route's start and end) the mesh is included in a dump.
        /// </summary>
        private const float DUMP_MESH_MARGIN = 8.0f;

        private const int DUMP_FORMAT_VERSION = 1;

        /// <summary>
        /// Finds a route like FindRouteLegs and writes it to a JSON file in DebugDumpDirectory, with the navmesh polys
        /// and drops around each leg. When there is no route, the mesh around the start and the end is written
        /// instead. Everything is in world coordinates, Z up (a landblock's local origin is at
        /// GeometryHelpers.GetLandblockOrigin). Returns the file's path, or null if it could not be written.
        /// </summary>
        /// <param name="label">A note stored in the dump, e.g. who was routing</param>
        public static string DumpRoute(Position start, Position end, AgentWidth agentWidth, string label, out List<List<Position>> legs, out bool reachesEnd)
        {
            legs = FindRouteLegs(start, end, agentWidth, out reachesEnd);

            try
            {
                Directory.CreateDirectory(DebugDumpDirectory);
                var fileName = string.Format(CultureInfo.InvariantCulture, "route_{0:yyyyMMdd_HHmmss_fff}_{1:X8}.json", DateTime.Now, start.Cell);
                var path = Path.Combine(DebugDumpDirectory, fileName);

                using (var stream = File.Create(path))
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("version", DUMP_FORMAT_VERSION);
                    writer.WriteString("created", DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
                    writer.WriteString("label", label ?? "");
                    writer.WriteString("agentWidth", agentWidth.ToString());
                    writer.WriteNumber("agentRadius", GetAgentRadius(agentWidth));
                    writer.WriteNumber("agentHeight", GetAgentHeight(agentWidth));
                    writer.WriteNumber("agentMaxClimb", GetAgentMaxClimb(agentWidth));

                    writer.WritePropertyName("start");
                    WriteDumpPosition(writer, start);
                    writer.WriteString("startGroup", GetGroupKey(start).ToString());

                    writer.WritePropertyName("end");
                    WriteDumpPosition(writer, end);
                    writer.WriteString("endGroup", GetGroupKey(end).ToString());

                    writer.WriteBoolean("found", legs != null);
                    writer.WriteBoolean("reachesEnd", reachesEnd);

                    writer.WriteStartArray("legs");
                    if (legs != null)
                    {
                        var legStart = start;
                        foreach (var leg in legs)
                        {
                            // the leg's first waypoint is on the leg's mesh (legStart may be on the other side of a doorway)
                            var group = GetGroupKey(leg[0]);
                            var points = new List<Vector3> { ToGlobal(legStart) };
                            points.AddRange(leg.Select(ToGlobal));

                            writer.WriteStartObject();
                            writer.WriteString("group", group.ToString());
                            writer.WritePropertyName("start");
                            WriteDumpPosition(writer, legStart);

                            writer.WriteStartArray("waypoints");
                            foreach (var waypoint in leg)
                                WriteDumpPosition(writer, waypoint, RoutePosition.IsDrop(waypoint));
                            writer.WriteEndArray();

                            writer.WritePropertyName("mesh");
                            WriteDumpMesh(writer, leg[0], group, agentWidth, points);

                            writer.WriteEndObject();

                            legStart = leg[leg.Count - 1];
                        }
                    }
                    writer.WriteEndArray();

                    // without a route, the meshes at either end show whether the start or end is on the mesh at all
                    if (legs == null)
                    {
                        writer.WritePropertyName("startMesh");
                        WriteDumpMesh(writer, start, GetGroupKey(start), agentWidth, new List<Vector3> { ToGlobal(start) });

                        writer.WritePropertyName("endMesh");
                        WriteDumpMesh(writer, end, GetGroupKey(end), agentWidth, new List<Vector3> { ToGlobal(end) });
                    }

                    writer.WriteEndObject();
                }

                return path;
            }
            catch (Exception e)
            {
                log.Warn($"Pathfinder: failed to write route dump from {start.ToLOCString()} to {end.ToLOCString()}: {e.Message}");
                return null;
            }
        }

        private static void WriteDumpPosition(Utf8JsonWriter writer, Position pos, bool isDropStart = false)
        {
            var at = ToGlobal(pos);
            writer.WriteStartObject();
            writer.WriteString("cell", $"0x{pos.Cell:X8}");
            writer.WriteNumber("x", at.X);
            writer.WriteNumber("y", at.Y);
            writer.WriteNumber("z", at.Z);
            if (isDropStart)
                writer.WriteBoolean("drop", true);
            writer.WriteEndObject();
        }

        /// <summary>
        /// Writes the polys and drops of a group's mesh within DUMP_MESH_MARGIN of the given world points, or null if
        /// the mesh is not loaded.
        /// </summary>
        private static void WriteDumpMesh(Utf8JsonWriter writer, Position reference, MeshGroupKey group, AgentWidth agentWidth, List<Vector3> points)
        {
            using var lease = AcquireMesh(reference, group, agentWidth);
            if (lease == null)
            {
                writer.WriteNullValue();
                return;
            }

            var min = new Vector3(points.Min(p => p.X), points.Min(p => p.Y), 0f) - new Vector3(DUMP_MESH_MARGIN, DUMP_MESH_MARGIN, 0f);
            var max = new Vector3(points.Max(p => p.X), points.Max(p => p.Y), 0f) + new Vector3(DUMP_MESH_MARGIN, DUMP_MESH_MARGIN, 0f);

            // interior meshes are landblock-local, the outdoor mesh is in world coordinates
            var offset = group.IsOutdoor ? Vector3.Zero : GeometryHelpers.GetLandblockOrigin(group.LandblockId);

            var mesh = lease.Query.GetAttachedNavMesh();
            var tiles = new List<DtMeshTile>();
            if (group.IsOutdoor)
            {
                var tileBuffer = new DtMeshTile[4];
                for (var x = (int)MathF.Floor(min.X / BlockLength); x <= (int)MathF.Floor(max.X / BlockLength); x++)
                {
                    for (var y = (int)MathF.Floor(min.Y / BlockLength); y <= (int)MathF.Floor(max.Y / BlockLength); y++)
                    {
                        var count = mesh.GetTilesAt(x, y, tileBuffer, tileBuffer.Length);
                        for (var t = 0; t < count; t++)
                        {
                            if (tileBuffer[t]?.data?.header != null)
                                tiles.Add(tileBuffer[t]);
                        }
                    }
                }
            }
            else
            {
                var tile = mesh.GetMaxTiles() > 0 ? mesh.GetTile(0) : null;
                if (tile?.data?.header != null)
                    tiles.Add(tile);
            }

            writer.WriteStartObject();
            writer.WriteString("space", group.IsOutdoor ? "world" : "landblock-local, converted to world");

            var drops = new List<(Vector3 from, Vector3 to)>();

            writer.WriteStartArray("polys");
            foreach (var tile in tiles)
            {
                var data = tile.data;
                var baseRef = mesh.GetPolyRefBase(tile);

                for (var i = 0; i < data.header.polyCount; i++)
                {
                    var poly = data.polys[i];
                    var verts = new Vector3[poly.vertCount];
                    for (var v = 0; v < poly.vertCount; v++)
                    {
                        var index = poly.verts[v] * 3;
                        verts[v] = new Vector3(data.verts[index], data.verts[index + 2], data.verts[index + 1]) + offset;
                    }

                    if (!verts.Any(p => p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y))
                        continue;

                    // a drop is a two-vertex poly from its start to its landing
                    if (poly.GetPolyType() == DtPolyTypes.DT_POLYTYPE_OFFMESH_CONNECTION)
                    {
                        if (verts.Length >= 2)
                            drops.Add((verts[0], verts[1]));
                        continue;
                    }

                    writer.WriteStartObject();
                    writer.WriteString("ref", $"0x{baseRef | (long)i:X}");
                    writer.WriteNumber("area", poly.GetArea());
                    writer.WriteNumber("flags", poly.flags);
                    if ((poly.flags & NavPolyFlags.Island) != 0)
                        writer.WriteBoolean("island", true);
                    if (poly.GetArea() == NavArea.Door)
                        writer.WriteBoolean("door", true);

                    writer.WriteStartArray("verts");
                    foreach (var p in verts)
                        WriteDumpPoint(writer, p);
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }
            }
            writer.WriteEndArray();

            writer.WriteStartArray("drops");
            foreach (var (from, to) in drops)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("from");
                WriteDumpPoint(writer, from);
                writer.WritePropertyName("to");
                WriteDumpPoint(writer, to);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        private static void WriteDumpPoint(Utf8JsonWriter writer, Vector3 p)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(MathF.Round(p.X, 3));
            writer.WriteNumberValue(MathF.Round(p.Y, 3));
            writer.WriteNumberValue(MathF.Round(p.Z, 3));
            writer.WriteEndArray();
        }
    }
}
