using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using ACE.DatLoader.Entity;
using ACE.Entity.Enum;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x02. 
    /// They are basically 3D model descriptions.
    /// </summary>
    /// <remarks>
    /// A big huge thank you to "Pea" for his trailblazing work on decoding this structure. Without his work on this, we might still be decoding models on cave walls.
    /// </remarks>
    [DatFileType(DatFileType.Setup)]
    public class SetupModel : FileType
    {
        public SetupFlags Flags { get; private set; }
        public bool AllowFreeHeading { get; private set; }
        public bool HasPhysicsBSP { get; private set; }
        public List<uint> Parts { get; } = new List<uint>();
        public List<uint> ParentIndex { get; } = new List<uint>();
        public List<Vector3> DefaultScale { get; } = new List<Vector3>();
        public Dictionary<int, LocationType> HoldingLocations { get; } = new Dictionary<int, LocationType>();
        public Dictionary<int, LocationType> ConnectionPoints { get; } = new Dictionary<int, LocationType>();
        public Dictionary<int, PlacementType> PlacementFrames { get; } = new Dictionary<int, PlacementType>();
        public List<CylSphere> CylSpheres { get; } = new List<CylSphere>();
        public List<Sphere> Spheres { get; } = new List<Sphere>();
        public float Height { get; private set; }
        public float Radius { get; private set; }
        public float StepUpHeight { get; private set; }
        public float StepDownHeight { get; private set; }
        public Sphere SortingSphere { get; private set; } = new Sphere();
        public Sphere SelectionSphere { get; private set; } = new Sphere();
        public Dictionary<int, LightInfo> Lights { get; } = new Dictionary<int, LightInfo>();
        public uint DefaultAnimation { get; private set; }
        public uint DefaultScript { get; private set; }
        public uint DefaultMotionTable { get; private set; }
        public uint DefaultSoundTable { get; private set; }
        public uint DefaultScriptTable { get; private set; }

        public bool HasMissileFlightPlacement { get; private set; } = false;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            Flags = (SetupFlags)reader.ReadUInt32();

            AllowFreeHeading    = (Flags & SetupFlags.AllowFreeHeading) != 0;
            HasPhysicsBSP       = (Flags & SetupFlags.HasPhysicsBSP) != 0;

            // Get all the GraphicsObjects in this SetupModel. These are all the 01-types.
            uint numParts = reader.ReadUInt32();
            for (int i = 0; i < numParts; i++)
                Parts.Add(reader.ReadUInt32());

            if ((Flags & SetupFlags.HasParent) != 0)
            {
                for (int i = 0; i < numParts; i++)
                    ParentIndex.Add(reader.ReadUInt32());
            }

            if ((Flags & SetupFlags.HasDefaultScale) != 0)
            {
                for (int i = 0; i < numParts; i++)
                    DefaultScale.Add(reader.ReadVector3());
            }

            HoldingLocations.Unpack(reader);
            ConnectionPoints.Unpack(reader);

            int placementsCount = reader.ReadInt32();
            for (int i = 0; i < placementsCount; i++)
            {
                int key = reader.ReadInt32();
                // there is a frame for each Part
                var placementType = new PlacementType();
                placementType.Unpack(reader, (uint)Parts.Count);
                PlacementFrames.Add(key, placementType);
                if (key == (int)Placement.MissileFlight)
                    HasMissileFlightPlacement = true;
            }

            CylSpheres.Unpack(reader);

            Spheres.Unpack(reader);

            Height          = reader.ReadSingle();
            Radius          = reader.ReadSingle();
            StepUpHeight    = reader.ReadSingle();
            StepDownHeight  = reader.ReadSingle();

            SortingSphere.Unpack(reader);
            SelectionSphere.Unpack(reader);

            Lights.Unpack(reader);

            DefaultAnimation    = reader.ReadUInt32();
            DefaultScript       = reader.ReadUInt32();
            DefaultMotionTable  = reader.ReadUInt32();
            DefaultSoundTable   = reader.ReadUInt32();
            DefaultScriptTable  = reader.ReadUInt32();
        }

        /// <summary>Writes the record in the layout <see cref="Unpack"/> reads. Placement frames with animation hooks are not supported.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Id);
                writer.Write((uint)Flags);

                writer.Write((uint)Parts.Count);
                foreach (var part in Parts)
                    writer.Write(part);

                if ((Flags & SetupFlags.HasParent) != 0)
                    foreach (var index in ParentIndex)
                        writer.Write(index);

                if ((Flags & SetupFlags.HasDefaultScale) != 0)
                    foreach (var scale in DefaultScale)
                    {
                        writer.Write(scale.X);
                        writer.Write(scale.Y);
                        writer.Write(scale.Z);
                    }

                PackLocations(writer, HoldingLocations);
                PackLocations(writer, ConnectionPoints);

                writer.Write(PlacementFrames.Count);
                foreach (var placement in PlacementFrames)
                {
                    if (placement.Value.AnimFrame.Hooks.Count > 0)
                        throw new NotSupportedException("Setup placement frames with animation hooks cannot be written.");

                    writer.Write(placement.Key);
                    foreach (var frame in placement.Value.AnimFrame.Frames)
                        frame.Pack(writer);
                    writer.Write(0u);
                }

                writer.Write((uint)CylSpheres.Count);
                foreach (var sphere in CylSpheres)
                    sphere.Pack(writer);

                writer.Write((uint)Spheres.Count);
                foreach (var sphere in Spheres)
                    sphere.Pack(writer);

                writer.Write(Height);
                writer.Write(Radius);
                writer.Write(StepUpHeight);
                writer.Write(StepDownHeight);

                SortingSphere.Pack(writer);
                SelectionSphere.Pack(writer);

                writer.Write(Lights.Count);
                foreach (var light in Lights)
                {
                    writer.Write(light.Key);
                    light.Value.Pack(writer);
                }

                writer.Write(DefaultAnimation);
                writer.Write(DefaultScript);
                writer.Write(DefaultMotionTable);
                writer.Write(DefaultSoundTable);
                writer.Write(DefaultScriptTable);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void PackLocations(BinaryWriter writer, Dictionary<int, LocationType> locations)
        {
            writer.Write(locations.Count);
            foreach (var location in locations)
            {
                writer.Write(location.Key);
                location.Value.Pack(writer);
            }
        }

        /// <summary>Replaces every referenced part (GfxObj), animation, script, motion table, sound table and script table id (when set) by map(id).</summary>
        public void Remap(Func<uint, uint> map)
        {
            for (var i = 0; i < Parts.Count; i++)
                Parts[i] = map(Parts[i]);

            DefaultAnimation = DefaultAnimation != 0 ? map(DefaultAnimation) : 0;
            DefaultScript = DefaultScript != 0 ? map(DefaultScript) : 0;
            DefaultMotionTable = DefaultMotionTable != 0 ? map(DefaultMotionTable) : 0;
            DefaultSoundTable = DefaultSoundTable != 0 ? map(DefaultSoundTable) : 0;
            DefaultScriptTable = DefaultScriptTable != 0 ? map(DefaultScriptTable) : 0;
        }

        public static SetupModel CreateSimpleSetup()
        {
            var setup = new SetupModel();

            setup.SortingSphere = Sphere.CreateDummySphere();
            setup.SelectionSphere = Sphere.CreateDummySphere();
            setup.AllowFreeHeading = true;

            return setup;
        }
    }
}
