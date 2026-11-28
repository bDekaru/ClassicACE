using ACE.Entity;
using ACE.Server.Pathfinding.Geometry;
using ACE.Server.WorldObjects;
using DotRecast.Core.Numerics;
using DotRecast.Detour;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ACE.Server.Pathfinding
{
    /// <summary>
    /// How a route search treats closed doors, for the creature it is planned for. Without BlockImpassable every door
    /// can be taken (a closed one the creature can open at a small cost), to tell which doors the shortest way
    /// crosses; with it, closed doors the creature can't pass are walls.
    /// </summary>
    public readonly record struct DoorRules(bool CanOpenDoors, bool BlockImpassable);

    /// <summary>
    /// Doors in routes. The meshes mark the floor under each placed door (see DoorArea), and each such poly is matched
    /// to its doors when the mesh loads (neighboring marks can share a poly), so a search can judge it by their state at
    /// that moment.
    /// </summary>
    public static partial class Pathfinder
    {
        /// <summary>
        /// What opening a closed door adds to a route's cost, in metres: an open way not much longer is preferred.
        /// </summary>
        private const float DoorOpenCost = 5.0f;

        /// <summary>
        /// How far from an opening's position a door may stand and still be the opening's door.
        /// </summary>
        private const float OpeningDoorReach = 0.5f;

        /// <summary>
        /// How far above or below a door's origin floor or an opening still counts as the door's level.
        /// </summary>
        private const float DoorLevelTolerance = 1.5f;

        private static readonly ConcurrentDictionary<uint, List<DoorPlacement>> PlacedDoors = new ConcurrentDictionary<uint, List<DoorPlacement>>();

        /// <summary>
        /// Route searches run on other threads than the door's landblock, and read its state (whose properties are
        /// locked) from here.
        /// </summary>
        private static readonly ConcurrentDictionary<uint, WeakReference<Door>> LiveDoors = new ConcurrentDictionary<uint, WeakReference<Door>>();

        private static readonly ConditionalWeakTable<DtPoly, DoorPlacement[]> DoorPolys = new ConditionalWeakTable<DtPoly, DoorPlacement[]>();

        /// <summary>
        /// Called when a landblock takes a door in; temporary copies of a door never get here.
        /// </summary>
        public static void RegisterDoor(Door door) => LiveDoors[door.Guid.Full] = new WeakReference<Door>(door);

        /// <summary>
        /// The door object with this guid, or null if it isn't loaded.
        /// </summary>
        public static Door GetDoor(uint guid) => LiveDoors.TryGetValue(guid, out var reference) && reference.TryGetTarget(out var door) && door.CurrentLandblock != null ? door : null;

        /// <summary>
        /// True if a creature gets through the door as it is now: open, or one it can open. A door that isn't loaded
        /// counts as open.
        /// </summary>
        public static bool CanPassDoor(uint guid, bool canOpenDoors)
        {
            var door = GetDoor(guid);
            return door == null || door.IsOpen || canOpenDoors && door.CanBeOpenedByCreatures;
        }

        private static DoorPlacement[] GetPolyDoors(DtPoly poly) => poly != null && poly.GetArea() == NavArea.Door && DoorPolys.TryGetValue(poly, out var doors) ? doors : Array.Empty<DoorPlacement>();

        /// <summary>
        /// Matches each door poly of a mesh's tile to the doors on its level that it covers, or else the nearest one.
        /// </summary>
        /// <param name="doors">In the mesh's coordinates</param>
        private static void MapDoorPolys(DtNavMesh mesh, List<DoorPlacement> doors)
        {
            var data = doors.Count > 0 && mesh.GetMaxTiles() > 0 ? mesh.GetTile(0)?.data : null;
            if (data == null)
                return;

            for (var i = 0; i < data.header.polyCount; i++)
            {
                var poly = data.polys[i];
                if (poly.GetArea() != NavArea.Door)
                    continue;

                var outline = new Vector2[poly.vertCount];
                var center = Vector2.Zero;
                var height = 0f;
                for (var j = 0; j < poly.vertCount; j++)
                {
                    var v = poly.verts[j] * 3;
                    outline[j] = new Vector2(data.verts[v], data.verts[v + 2]); // Recast space: y up
                    center += outline[j] / poly.vertCount;
                    height += data.verts[v + 1] / poly.vertCount;
                }

                var onLevel = doors.Where(door => MathF.Abs(door.Position.Z - height) <= DoorLevelTolerance).ToList();
                var under = onLevel.Where(door => DoorArea.Covers(door, outline)).ToArray();

                if (under.Length == 0)
                    under = onLevel.OrderBy(door => Vector2.DistanceSquared(new Vector2(door.Position.X, door.Position.Y), center)).Take(1).ToArray();

                if (under.Length > 0)
                    DoorPolys.AddOrUpdate(poly, under);
            }
        }

        /// <summary>
        /// The doors in the opening a crossing goes through. Between a building and the outdoors no leg need walk over
        /// their floor: the outdoor leg ends in front of the opening and the building's leg starts in it.
        /// </summary>
        private static uint[] GetCrossingDoors(Crossing crossing)
        {
            // the position on the building's side is the opening's own
            var opening = crossing.From.IsOutdoor ? crossing.Far : crossing.Near;
            var at = new Vector2(opening.PositionX, opening.PositionY);
            var around = new[]
            {
                at + new Vector2(-OpeningDoorReach, -OpeningDoorReach), at + new Vector2(OpeningDoorReach, -OpeningDoorReach),
                at + new Vector2(OpeningDoorReach, OpeningDoorReach), at + new Vector2(-OpeningDoorReach, OpeningDoorReach)
            };

            return PlacedDoors.GetOrAdd(opening.Cell & 0xFFFF0000, id => DoorArea.GetDoors(id))
                .Where(door => MathF.Abs(door.Position.Z - opening.PositionZ) <= DoorLevelTolerance && DoorArea.Covers(door, around))
                .Select(door => door.Guid)
                .ToArray();
        }

        /// <summary>
        /// Puts doors a leg doesn't carry yet on its last waypoint.
        /// </summary>
        private static void AddDoors(List<Position> leg, uint[] doors)
        {
            var missing = doors.Except(leg.SelectMany(RoutePosition.GetDoors)).ToArray();
            if (missing.Length > 0)
                leg[^1] = new RoutePosition(leg[^1], RoutePosition.IsLedgeDrop(leg[^1]), RoutePosition.GetDoors(leg[^1]).Concat(missing).ToArray());
        }

        private sealed class DoorFilter : IDtQueryFilter
        {
            private readonly IDtQueryFilter inner;
            private readonly DoorRules rules;

            public DoorFilter(IDtQueryFilter inner, DoorRules rules)
            {
                this.inner = inner;
                this.rules = rules;
            }

            public bool PassFilter(long refs, DtMeshTile tile, DtPoly poly)
            {
                if (!inner.PassFilter(refs, tile, poly))
                    return false;

                if (!rules.BlockImpassable)
                    return true;

                foreach (var door in GetPolyDoors(poly))
                {
                    if (!CanPassDoor(door.Guid, rules.CanOpenDoors))
                        return false;
                }

                return true;
            }

            public float GetCost(RcVec3f pa, RcVec3f pb, long prevRef, DtMeshTile prevTile, DtPoly prevPoly, long curRef, DtMeshTile curTile, DtPoly curPoly, long nextRef, DtMeshTile nextTile, DtPoly nextPoly)
            {
                var cost = inner.GetCost(pa, pb, prevRef, prevTile, prevPoly, curRef, curTile, curPoly, nextRef, nextTile, nextPoly);

                // once per door, on the way into it
                if (rules.CanOpenDoors)
                {
                    var current = GetPolyDoors(curPoly);
                    foreach (var door in GetPolyDoors(nextPoly))
                    {
                        if (Array.IndexOf(current, door) < 0 && GetDoor(door.Guid) is { IsOpen: false })
                            cost += DoorOpenCost;
                    }
                }

                return cost;
            }
        }
    }
}
