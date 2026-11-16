using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader
{
    /// <summary>An object placed in a landblock or cell: a model id and a 28-byte frame (origin and rotation), kept as raw bytes.</summary>
    public class DatStab
    {
        public const int FrameBytes = 28;

        public uint Id;
        public byte[] Frame = new byte[FrameBytes];
    }

    public class DatBuildingPortal
    {
        public ushort Flags;
        public ushort OtherCellId;
        public ushort OtherPortalId;
        public List<ushort> VisibleCells = new List<ushort>();
    }

    public class DatBuilding
    {
        public uint ModelId;
        public byte[] Frame = new byte[DatStab.FrameBytes];
        public uint NumLeaves;
        public List<DatBuildingPortal> Portals = new List<DatBuildingPortal>();
    }

    /// <summary>
    /// A landblock info record (xxyyFFFE) as a model that can be changed and written back, for map edits.
    /// The layout is the same in the retail and ToD builds; every section already ends on a 4-byte boundary.
    /// </summary>
    public class DatLandblockInfo
    {
        public uint Id;
        public uint NumCells;
        public List<DatStab> Objects = new List<DatStab>();
        public ushort BuildingFlags;
        public List<DatBuilding> Buildings = new List<DatBuilding>();
        public ushort RestrictionTableCount;
        public ushort RestrictionTableBucketSize;
        public List<KeyValuePair<uint, uint>> RestrictionTables = new List<KeyValuePair<uint, uint>>();

        public static DatLandblockInfo Parse(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                var info = new DatLandblockInfo { Id = reader.ReadUInt32(), NumCells = reader.ReadUInt32() };

                var objectCount = reader.ReadInt32();
                for (var i = 0; i < objectCount; i++)
                    info.Objects.Add(new DatStab { Id = reader.ReadUInt32(), Frame = reader.ReadBytes(DatStab.FrameBytes) });

                var buildingCount = reader.ReadUInt16();
                info.BuildingFlags = reader.ReadUInt16();

                for (var i = 0; i < buildingCount; i++)
                {
                    var building = new DatBuilding { ModelId = reader.ReadUInt32(), Frame = reader.ReadBytes(DatStab.FrameBytes), NumLeaves = reader.ReadUInt32() };

                    var portalCount = reader.ReadInt32();
                    for (var p = 0; p < portalCount; p++)
                    {
                        var portal = new DatBuildingPortal { Flags = reader.ReadUInt16(), OtherCellId = reader.ReadUInt16(), OtherPortalId = reader.ReadUInt16() };

                        var visible = reader.ReadUInt16();
                        for (var v = 0; v < visible; v++)
                            portal.VisibleCells.Add(reader.ReadUInt16());

                        reader.BaseStream.Position = (reader.BaseStream.Position + 3) & ~3L;
                        building.Portals.Add(portal);
                    }

                    info.Buildings.Add(building);
                }

                if ((info.BuildingFlags & 1) == 1)
                {
                    info.RestrictionTableCount = reader.ReadUInt16();
                    info.RestrictionTableBucketSize = reader.ReadUInt16();

                    for (var i = 0; i < info.RestrictionTableCount; i++)
                        info.RestrictionTables.Add(new KeyValuePair<uint, uint>(reader.ReadUInt32(), reader.ReadUInt32()));
                }

                return info;
            }
        }

        public byte[] Pack()
        {
            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write(NumCells);

                writer.Write(Objects.Count);
                foreach (var stab in Objects)
                {
                    writer.Write(stab.Id);
                    writer.Write(stab.Frame);
                }

                writer.Write((ushort)Buildings.Count);
                writer.Write(BuildingFlags);

                foreach (var building in Buildings)
                {
                    writer.Write(building.ModelId);
                    writer.Write(building.Frame);
                    writer.Write(building.NumLeaves);
                    writer.Write(building.Portals.Count);

                    foreach (var portal in building.Portals)
                    {
                        writer.Write(portal.Flags);
                        writer.Write(portal.OtherCellId);
                        writer.Write(portal.OtherPortalId);
                        writer.Write((ushort)portal.VisibleCells.Count);

                        foreach (var cell in portal.VisibleCells)
                            writer.Write(cell);

                        Align(stream);
                    }
                }

                if ((BuildingFlags & 1) == 1)
                {
                    writer.Write(RestrictionTableCount);
                    writer.Write(RestrictionTableBucketSize);

                    foreach (var entry in RestrictionTables)
                    {
                        writer.Write(entry.Key);
                        writer.Write(entry.Value);
                    }
                }
            }

            return stream.ToArray();
        }

        private static void Align(Stream stream)
        {
            while (stream.Position % 4 != 0)
                stream.WriteByte(0);
        }
    }
}
