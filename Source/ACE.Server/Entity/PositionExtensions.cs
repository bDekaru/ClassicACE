using System;
using System.Numerics;

using log4net;

using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Physics.Common;
using ACE.Server.Physics.Extensions;
using ACE.Server.Physics.Util;
using ACE.Server.WorldObjects;

using Position = ACE.Entity.Position;

namespace ACE.Server.Entity
{
    public static class PositionExtensions
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static Vector3 ToGlobal(this Position p, bool skipIndoors = false)
        {
            // TODO: Is this necessary? It seemed to be loading rogue physics landblocks. Commented out 2019-04 Mag-nus
            //var landblock = LScape.get_landblock(p.LandblockId.Raw);

            // TODO: investigate dungeons that are below actual traversable overworld terrain
            // ex., 010AFFFF
            //if (landblock.IsDungeon)
            if (p.Indoors && skipIndoors)
                return p.Pos;

            var x = p.LandblockId.LandblockX * Position.BlockLength + p.PositionX;
            var y = p.LandblockId.LandblockY * Position.BlockLength + p.PositionY;
            var z = p.PositionZ;

            return new Vector3(x, y, z);
        }

        public static Position FromGlobal(this Position p, Vector3 pos)
        {
            // TODO: Is this necessary? It seemed to be loading rogue physics landblocks. Commented out 2019-04 Mag-nus
            //var landblock = LScape.get_landblock(p.LandblockId.Raw);

            // TODO: investigate dungeons that are below actual traversable overworld terrain
            // ex., 010AFFFF
            //if (landblock.IsDungeon)
            if (p.Indoors)
            {
                var iPos = new Position();
                iPos.LandblockId = p.LandblockId;
                iPos.Pos = new Vector3(pos.X, pos.Y, pos.Z);
                iPos.Rotation = p.Rotation;
                iPos.LandblockId = new LandblockId(GetCell(iPos));
                return iPos;
            }

            var blockX = (uint)pos.X / Position.BlockLength;
            var blockY = (uint)pos.Y / Position.BlockLength;

            var localX = pos.X % Position.BlockLength;
            var localY = pos.Y % Position.BlockLength;

            var landblockID = blockX << 24 | blockY << 16 | 0xFFFF;

            var position = new Position();
            position.LandblockId = new LandblockId((byte)blockX, (byte)blockY);
            position.PositionX = localX;
            position.PositionY = localY;
            position.PositionZ = pos.Z;
            position.Rotation = p.Rotation;
            position.LandblockId = new LandblockId(GetCell(position));
            return position;
        }

        /// <summary>
        /// Gets the cell ID for a position within a landblock
        /// </summary>
        public static uint GetCell(this Position p)
        {
            try
            {
                //var landblock = LScape.get_landblock(p.LandblockId.Raw);

                // dungeons
                // TODO: investigate dungeons that are below actual traversable overworld terrain
                // ex., 010AFFFF
                //if (landblock.IsDungeon)
                if (p.Indoors)
                    return GetIndoorCell(p);

                // outside - could be on landscape, in building, or underground cave
                var cellID = GetOutdoorCell(p);
                var landcell = LScape.get_landcell(cellID) as LandCell;

                if (landcell == null)
                    return cellID;

                if (landcell.has_building())
                {
                    var envCells = landcell.Building.get_building_cells();
                    foreach (var envCell in envCells)
                        if (envCell.point_in_cell(p.Pos))
                            return envCell.ID;
                }

                // handle underground areas ie. caves
                // get the terrain Z-height for this X/Y
                Physics.Polygon walkable = null;
                var terrainPoly = landcell.find_terrain_poly(p.Pos, ref walkable);
                if (walkable != null)
                {
                    Vector3 terrainPos = p.Pos;
                    walkable.Plane.set_height(ref terrainPos);

                    // are we below ground? if so, search all of the indoor cells for this landblock
                    if (terrainPos.Z > p.Pos.Z)
                    {
                        var landblock = LScape.get_landblock(p.LandblockId.Raw);
                        var envCells = landblock.get_envcells();
                        foreach (var envCell in envCells)
                            if (envCell.point_in_cell(p.Pos))
                                return envCell.ID;
                    }
                }

                return cellID;
            }
            catch (Exception e)
            {
                log.ErrorFormat("GetCell() threw an exception: {0}\nposition as LOC => {1}", e.ToString(), p.ToLOCString());
                log.Error(e);

                return 0;
            }
        }

        /// <summary>
        /// Gets an outdoor cell ID for a position within a landblock
        /// </summary>
        public static uint GetOutdoorCell(this Position p)
        {
            var cellX = (uint)p.PositionX / Position.CellLength;
            var cellY = (uint)p.PositionY / Position.CellLength;

            var cellID = cellX * Position.CellSide + cellY + 1;

            var blockCellID = (uint)((p.LandblockId.Raw & 0xFFFF0000) | cellID);
            return blockCellID;
        }

        /// <summary>
        /// Gets an indoor cell ID for a position within a dungeon
        /// </summary>
        private static uint GetIndoorCell(this Position p)
        {
            var adjustCell = AdjustCell.Get(p.Landblock);
            var envCell = adjustCell.GetCell(p.Pos);
            if (envCell != null)
                return envCell.Value;
            else
                return p.Cell;
        }

        public static bool IsValidIndoorCell(this Position p)
        {
            var adjustCell = AdjustCell.Get(p.Landblock);
            var envCell = adjustCell.GetCell(p.Pos);
            return envCell != null;
        }

        public static bool IsValidIndoorCell(this Position p, float objHeight)
        {
            var adjustCell = AdjustCell.Get(p.Landblock);
            var envCell = adjustCell.GetCell(p.Pos);

            var heightPos = new Position(p);
            heightPos.PositionZ += objHeight;

            var adjustCell2 = AdjustCell.Get(heightPos.Landblock);
            var envCell2 = adjustCell.GetCell(heightPos.Pos);

            return envCell != null && envCell2 != null;
        }

        /// <summary>
        /// Returns the greatest single-dimension square distance between 2 positions
        /// </summary>
        public static uint CellDist(this Position p1, Position p2)
        {
            if (!p1.Indoors && !p2.Indoors)
            {
                return Math.Max(p1.GlobalCellX, p1.GlobalCellY);
            }

            // handle dungeons
            /*var block1 = LScape.get_landblock(p1.LandblockId.Raw);
            var block2 = LScape.get_landblock(p2.LandblockId.Raw);
            if (block1.IsDungeon || block2.IsDungeon)
            {
                // 2 separate dungeons = infinite distance
                if (block1.ID != block2.ID)
                    return uint.MaxValue;

                return GetDungeonCellDist(p1, p2);
            }*/

            var _p1 = new Position(p1);
            var _p2 = new Position(p2);

            if (_p1.Indoors)
                _p1.LandblockId = new LandblockId(_p1.GetOutdoorCell());
            if (_p2.Indoors)
                _p2.LandblockId = new LandblockId(_p2.GetIndoorCell());

            return Math.Max(_p1.GlobalCellX, _p2.GlobalCellY);
        }

        public static uint GetDungeonCellDist(Position p1, Position p2)
        {
            // not implemented yet
            return uint.MaxValue;
        }

        public static Vector2? GetMapCoords(this Position pos, bool evenIfIndoors = false)
        {
            // no map coords available for dungeons / indoors?
            if ((pos.Cell & 0xFFFF) >= 0x100 && !evenIfIndoors)
                return null;

            var globalPos = pos.ToGlobal();

            // 1 landblock = 192 meters
            // 1 landblock = 0.8 map units

            // 1 map unit = 1.25 landblocks
            // 1 map unit = 240 meters

            var mapCoords = new Vector2(globalPos.X / 240, globalPos.Y / 240);

            // dereth is 204 map units across, -102 to +102
            mapCoords -= Vector2.One * 102;

            return mapCoords;
        }

        public static string GetMapCoordStr(this Position pos, bool evenIfIndoors = false)
        {
            var mapCoords = pos.GetMapCoords(evenIfIndoors);

            if (mapCoords == null)
                return null;

            var northSouth = mapCoords.Value.Y >= 0 ? "N" : "S";
            var eastWest = mapCoords.Value.X >= 0 ? "E" : "W";

            return string.Format("{0:0.0}", Math.Abs(mapCoords.Value.Y) - 0.05f) + northSouth + ", "
                 + string.Format("{0:0.0}", Math.Abs(mapCoords.Value.X) - 0.05f) + eastWest;
        }

        public static void AdjustMapCoords(this Position pos)
        {
            // adjust Z to terrain height
            pos.PositionZ = pos.GetTerrainZ();

            // adjust to building height, if applicable
            var sortCell = LScape.get_landcell(pos.Cell) as SortCell;
            if (sortCell != null && sortCell.has_building())
            {
                var building = sortCell.Building;

                var minZ = building.GetMinZ();

                if (minZ > 0 && minZ < float.MaxValue)
                    pos.PositionZ += minZ;

                pos.LandblockId = new LandblockId(pos.GetCell());
            }
        }

        public static void TranslateLandblockId(this Position pos, uint blockCell)
        {
            var newBlockX = blockCell >> 24;
            var newBlockY = (blockCell >> 16) & 0xFF;

            var xDiff = (int)newBlockX - pos.LandblockX;
            var yDiff = (int)newBlockY - pos.LandblockY;

            //pos.Origin.X -= xDiff * 192;
            pos.PositionX -= xDiff * 192;
            //pos.Origin.Y -= yDiff * 192;
            pos.PositionY -= yDiff * 192;

            //pos.ObjCellID = blockCell;
            pos.LandblockId = new LandblockId(blockCell);
        }

        public static void FindZ(this Position pos)
        {
            var envCell = DatManager.CellDat.ReadFromDat<DatLoader.FileTypes.EnvCell>(pos.Cell);
            pos.PositionZ = envCell.Position.Origin.Z;
        }

        public static float GetTerrainZ(this Position p)
        {
            var landblock = LScape.get_landblock(p.LandblockId.Raw);

            var cellID = GetOutdoorCell(p);
            var landcell = (LandCell)LScape.get_landcell(cellID);

            if (landcell == null)
                return p.Pos.Z;

            Physics.Polygon walkable = null;
            if (!landcell.find_terrain_poly(p.Pos, ref walkable))
                return p.Pos.Z;

            Vector3 terrainPos = p.Pos;
            walkable.Plane.set_height(ref terrainPos);

            return terrainPos.Z;
        }

        /// <summary>
        /// Returns TRUE if outdoor position is located on walkable slope
        /// </summary>
        public static bool IsWalkable(this Position p)
        {
            if (p.Indoors) return true;

            var landcell = (LandCell)LScape.get_landcell(p.Cell);

            Physics.Polygon walkable = null;
            var terrainPoly = landcell.find_terrain_poly(p.Pos, ref walkable);
            if (walkable == null) return false;

            return Physics.PhysicsObj.is_valid_walkable(walkable.Plane.Normal);
        }

        /// <summary>
        /// Returns TRUE if current cell is a House cell
        /// </summary>
        public static bool IsRestrictable(this Position p, Landblock landblock)
        {
            var cell = landblock.IsDungeon ? p.Cell : p.GetOutdoorCell();

            return HouseCell.HouseCells.ContainsKey(cell);
        }

        public static Position ACEPosition(this Physics.Common.Position pos)
        {
            return new Position(pos.ObjCellID, pos.Frame.Origin, pos.Frame.Orientation);
        }

        public static Physics.Common.Position PhysPosition(this Position pos)
        {
            return new Physics.Common.Position(pos.Cell, new Physics.Animation.AFrame(pos.Pos, pos.Rotation));
        }


        // differs from ac physics engine
        public static readonly float RotationEpsilon = 0.0001f;

        public static bool IsRotationValid(this Quaternion q)
        {
            if (q == Quaternion.Identity)
                return true;

            if (float.IsNaN(q.X) || float.IsNaN(q.Y) || float.IsNaN(q.Z) || float.IsNaN(q.W))
                return false;

            var length = q.Length();
            if (float.IsNaN(length))
                return false;

            if (Math.Abs(1.0f - length) > RotationEpsilon)
                return false;

            return true;
        }

        public static bool AttemptToFixRotation(this Position pos, WorldObject wo, PositionType positionType)
        {
            log.Warn($"detected bad quaternion x y z w for {wo.Name} (0x{wo.Guid}) | WCID: {wo.WeenieClassId} | WeenieType: {wo.WeenieType} | PositionType: {positionType}");
            log.Warn($"before fix: {pos.ToLOCString()}");

            var normalized = Quaternion.Normalize(pos.Rotation);

            var success = IsRotationValid(normalized);

            if (success)
                pos.Rotation = normalized;

            log.Warn($" after fix: {pos.ToLOCString()}");

            return success;
        }

        public static float GetLargestOffset(this Position pos, Position p)
        {
            var offset = pos.GetOffset(p);
            var absOffset = new Vector3(Math.Abs(offset.X), Math.Abs(offset.Y), Math.Abs(offset.Z));

            if (absOffset.X > absOffset.Y)
            {
                if (absOffset.X > absOffset.Z)
                    return offset.X;
            }
            else
            {
                if (absOffset.Y > absOffset.Z)
                    return offset.Y;
            }
            return offset.Z;
        }

        public static string GetCardinalDirectionsTo(this Position pos, Position p)
        {
            var offset = pos.GetOffset(p);

            var minDist = 2;
            var aCoupleStepsDistance = 5;
            var aCoupleStepsThresholdDistance = 10;
            var aFewStepsDistance = 20;
            var aFewStepsThresholdDistance = 40;
            var aBitDistance = 200;
            var aBitThresholdDistance = 400;
            var farDistance = 900;
            var veryFarDistance = 1800;

            var isNorthSouth = false;
            var isEastWest = false;

            var directionEastWestString = "";
            if (offset.X > minDist)
            {
                isEastWest = true;
                directionEastWestString += "east";
            }
            else if (offset.X < -minDist)
            {
                isEastWest = true;
                directionEastWestString += "west";
            }

            var directionNorthSouthString = "";
            if (offset.Y > minDist)
            {
                isNorthSouth = true;
                directionNorthSouthString += "north";
            }
            else if (offset.Y < -minDist)
            {
                isNorthSouth = true;
                directionNorthSouthString += "south";
            }

            if (directionEastWestString.Length == 0 && directionNorthSouthString.Length == 0)
                return "";
            else
            {
                var eastWestDist = Math.Abs(offset.X);
                var northSouthDist = Math.Abs(offset.Y);

                if (northSouthDist > aCoupleStepsThresholdDistance && eastWestDist < aCoupleStepsDistance)
                    isEastWest = false;
                else if (eastWestDist > aCoupleStepsThresholdDistance && northSouthDist < aCoupleStepsDistance)
                    isNorthSouth = false;
                else if (northSouthDist > aFewStepsThresholdDistance && eastWestDist < aFewStepsDistance)
                    isEastWest = false;
                else if (eastWestDist > aFewStepsThresholdDistance && northSouthDist < aFewStepsDistance)
                    isNorthSouth = false;
                else if (northSouthDist > aBitThresholdDistance && eastWestDist < aBitDistance)
                    isEastWest = false;
                else if (eastWestDist > aBitThresholdDistance && northSouthDist < aBitDistance)
                    isNorthSouth = false;

                string eastWestDistanceString = "";
                if (isEastWest)
                {
                    if (eastWestDist < aCoupleStepsDistance)
                        eastWestDistanceString = "a couple steps to the ";
                    else if (eastWestDist < aFewStepsDistance)
                        eastWestDistanceString = "a few steps to the ";
                    else if (eastWestDist < aBitDistance)
                        eastWestDistanceString = "a bit to the ";
                    else if (eastWestDist < farDistance)
                        eastWestDistanceString = "";
                    else if (eastWestDist < veryFarDistance)
                        eastWestDistanceString = "far to the ";
                    else
                        eastWestDistanceString = "very far to the ";
                }

                string northSouthDistanceString = "";
                if (isNorthSouth)
                {
                    if (northSouthDist < aCoupleStepsDistance)
                        northSouthDistanceString = "a couple steps to the ";
                    else if (northSouthDist < aFewStepsDistance)
                        northSouthDistanceString = "a few steps to the ";
                    else if (northSouthDist < aBitDistance)
                        northSouthDistanceString = "a bit to the ";
                    else if (northSouthDist < farDistance)
                        northSouthDistanceString = "";
                    else if (northSouthDist < veryFarDistance)
                        northSouthDistanceString = "far to the ";
                    else
                        northSouthDistanceString = "very far to the ";
                }

                string direction;
                if (isEastWest && isNorthSouth)
                {
                    if (eastWestDistanceString == northSouthDistanceString)
                        direction = $"{northSouthDistanceString}{directionNorthSouthString}{directionEastWestString}";
                    else if (northSouthDist > eastWestDist)
                        direction = $"{northSouthDistanceString}{directionNorthSouthString} and {eastWestDistanceString}{directionEastWestString}";
                    else
                        direction = $"{eastWestDistanceString}{directionEastWestString} and {northSouthDistanceString}{directionNorthSouthString}";
                }
                else if (isEastWest && !isNorthSouth)
                    direction = $"{eastWestDistanceString}{directionEastWestString}";
                else if (isNorthSouth && !isEastWest)
                    direction = $"{northSouthDistanceString}{directionNorthSouthString}";
                else
                    direction = "";

                return direction;
            }
        }

        public static void RotateAroundPivot(this Position pos, Position pivot, float degrees)
        {
            var radians = degrees.ToRadians();

            var cosTheta = Math.Cos(radians);
            var sinTheta = Math.Sin(radians);

            var x = cosTheta * (pos.PositionX - pivot.PositionX) - sinTheta * (pos.PositionY - pivot.PositionY) + pivot.PositionX;
            var y = sinTheta * (pos.PositionX - pivot.PositionX) + cosTheta * (pos.PositionY - pivot.PositionY) + pivot.PositionY;

            pos.PositionX = (float)x;
            pos.PositionY = (float)y;

            pos.Rotate(0, 0, degrees);
        }

        public static Vector3 GetYawPitchRoll(this Position pos)
        {
            var q = pos.Rotation;

            double Ysqr = q.Y * q.Y;
            double t0 = -2.0 * (Ysqr + q.Z * q.Z) + 1.0;
            double t1 = +2.0 * (q.X * q.Y + q.W * q.Z);
            double t2 = -2.0 * (q.X * q.Z - q.W * q.Y);
            double t3 = +2.0 * (q.Y * q.Z + q.W * q.X);
            double t4 = -2.0 * (q.X * q.X + Ysqr) + 1.0;

            t2 = t2 > 1.0 ? 1.0 : t2;
            t2 = t2 < -1.0 ? -1.0 : t2;

            var yaw = (float)Math.Round(Math.Atan2(t1, t0), 4) % 360f;
            var pitch = (float)Math.Round(Math.Asin(t2), 4) % 360f;
            var roll = (float)Math.Round(Math.Atan2(t3, t4), 4) % 360f;

            if (yaw < 0)
                yaw += 360f;
            if (pitch < 0)
                pitch += 360f;
            if (roll < 0)
                roll += 360f;

            return new Vector3(yaw, pitch, roll);
        }

        public static float GetYaw(this Position pos)
        {
            var q = pos.Rotation;

            var yaw = (float)Math.Round(((float)Math.Atan2(2.0 * (q.Z * q.W + q.X * q.Y), -1.0 + 2.0 * (q.W * q.W + q.X * q.X))).ToDegrees(), 4) % 360.0f;

            if (yaw < 0)
                yaw += 360f;

            return yaw;
        }

        public static float GetPitch(this Position pos)
        {
            var q = pos.Rotation;

            var pitch = (float)Math.Round(((float)Math.Asin(2.0 * (q.Y * q.W - q.Z * q.X))).ToDegrees(), 4) % 360.0f;

            if (pitch < 0)
                pitch += 360f;

            return pitch;
        }

        public static float GetRoll(this Position pos)
        {
            var q = pos.Rotation;

            var roll = (float)Math.Round(((float)Math.Atan2(2.0 * (q.Z * q.Y + q.W * q.X), 1.0 - 2.0 * (q.X * q.X + q.Y * q.Y))).ToDegrees(), 4) % 360.0f;

            if (roll < 0)
                roll += 360f;

            return roll;
        }
    }
}
