using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatLoader
{
    /// <summary>
    /// The ids a record refers to, read through the file types' own <c>Remap</c> codecs with a collecting function, so the references are exact and typed.
    /// A record of a type without a walker yields none.
    /// </summary>
    public static class DatReferences
    {
        /// <summary>The record types <see cref="OfPortalRecord"/> can read; the others of a portal dat are tables and layouts, or leaves (palettes, waves, environments).</summary>
        public static bool HasWalker(uint id)
        {
            switch (id >> 24)
            {
                case 0x01: case 0x02: case 0x03: case 0x05: case 0x06: case 0x08: case 0x09: case 0x0F: case 0x10: case 0x11:
                case 0x12: case 0x13: case 0x20: case 0x32: case 0x33: case 0x34:
                    return true;

                default:
                    return false;
            }
        }

        /// <exception cref="Exception">The record cannot be read as its type.</exception>
        public static List<uint> OfPortalRecord(DatDatabase portal, uint id)
        {
            var refs = new List<uint>();

            uint Collect(uint value)
            {
                if (value != 0)
                    refs.Add(value);

                return value;
            }

            switch (id >> 24)
            {
                case 0x01:
                    var gfxObj = portal.ReadFromDat<GfxObj>(id);
                    refs.AddRange(gfxObj.Surfaces);
                    Collect(gfxObj.DIDDegrade);
                    break;

                case 0x02: portal.ReadFromDat<SetupModel>(id).Remap(Collect); break;
                case 0x03: portal.ReadFromDat<Animation>(id).Remap(Collect); break;
                case 0x05: portal.ReadFromDat<SurfaceTexture>(id).Remap(Collect); break;
                case 0x06: Collect(portal.ReadFromDat<Texture>(id).DefaultPaletteId ?? 0); break;
                case 0x08: portal.ReadFromDat<Surface>(id).Remap(Collect, Collect); break;
                case 0x09: portal.ReadFromDat<MotionTable>(id).Remap(Collect); break;
                case 0x0F: portal.ReadFromDat<PaletteSet>(id).Remap(Collect); break;
                case 0x10: portal.ReadFromDat<ClothingTable>(id).Remap(Collect); break;
                case 0x11: portal.ReadFromDat<GfxObjDegradeInfo>(id).Remap(Collect); break;
                case 0x12: portal.ReadFromDat<Scene>(id).Remap(Collect); break;
                case 0x20: portal.ReadFromDat<SoundTable>(id).Remap(Collect); break;
                case 0x32: portal.ReadFromDat<ParticleEmitterInfo>(id).Remap(Collect); break;
                case 0x33: portal.ReadFromDat<PhysicsScript>(id).Remap(Collect); break;
                case 0x34: portal.ReadFromDat<PhysicsScriptTable>(id).Remap(Collect); break;

                case 0x13:
                    var region = portal.ReadFromDat<RegionDesc>(id);
                    region.Remap(Collect);

                    var surfaces = region.TerrainInfo.LandSurfaces.TexMerge;

                    foreach (var map in surfaces.CornerTerrainMaps)
                        Collect(map.TexGID);

                    foreach (var map in surfaces.SideTerrainMaps)
                        Collect(map.TexGID);

                    foreach (var map in surfaces.RoadMaps)
                        Collect(map.RoadTexGID);

                    foreach (var terrain in surfaces.TerrainDesc)
                    {
                        Collect(terrain.TerrainTex.TexGID);
                        Collect(terrain.TerrainTex.DetailTexGID);
                    }

                    Collect(region.RegionMisc.GameMapID);
                    Collect(region.RegionMisc.ClearCellId);
                    // ClearMonsterId (01001612) is in no dat, retail included, and the client only stores it without ever loading it.
                    // Left out so it does not show up as dangling.
                    break;
            }

            return refs;
        }

        /// <summary>The ids a cell dat record refers to in the portal dat: a landblock info's objects and buildings, an environment cell's environment, surfaces and objects.</summary>
        /// <exception cref="Exception">The record cannot be read as its type.</exception>
        public static List<uint> OfCellRecord(DatDatabase cell, uint id)
        {
            var refs = new List<uint>();
            var low = id & 0xFFFF;

            if (low == 0xFFFE)
            {
                var info = cell.ReadFromDat<LandblockInfo>(id);

                foreach (var obj in info.Objects)
                    refs.Add(obj.Id);

                foreach (var building in info.Buildings)
                    refs.Add(building.ModelId);
            }
            else if (low < 0xFFFE)
            {
                var envCell = cell.ReadFromDat<EnvCell>(id);

                refs.Add(Normalize(envCell.EnvironmentId, 0x0D000000));

                foreach (var surface in envCell.Surfaces)
                    refs.Add(Normalize(surface, 0x08000000));

                foreach (var obj in envCell.StaticObjects)
                    refs.Add(obj.Id);
            }

            return refs;
        }

        /// <summary>The ids a UI layout refers to: the images, cursors, sounds and animation frames of its states, every data file and string table in the properties of its elements and states, and the layouts its elements are based on.</summary>
        public static List<uint> OfLayout(LayoutDesc layout)
        {
            var refs = new List<uint>();

            void Add(uint id)
            {
                if (id != 0)
                    refs.Add(id);
            }

            void Property(BaseProperty property)
            {
                switch (property.PropertyType)
                {
                    case BasePropertyType.DataFile: Add(property.ValueDataFile); break;
                    case BasePropertyType.StringInfo: Add(property.ValueString.TableId); break;
                    case BasePropertyType.Array: property.ValueArray.ForEach(Property); break;
                    case BasePropertyType.Struct: property.ValueStruct.Values.ToList().ForEach(Property); break;
                }
            }

            void State(StateDesc state)
            {
                foreach (var property in state.Properties.Values)
                    Property(property);

                foreach (var media in state.Media)
                {
                    switch (media.Type)
                    {
                        case MediaType.Alpha:
                        case MediaType.Cursor:
                        case MediaType.Image:
                        case MediaType.Sound:
                            Add(media.File);
                            break;

                        case MediaType.Animation:
                            media.Frames.ForEach(Add);
                            break;
                    }
                }
            }

            void Element(ElementDesc element)
            {
                State(element);
                Add(element.BaseLayout);

                foreach (var state in element.States.Values)
                    State(state);

                foreach (var child in element.Children.Values)
                    Element(child);
            }

            foreach (var element in layout.Elements.Values)
                Element(element);

            return refs;
        }

        // Cells store the environment and the surfaces without their type byte.
        private static uint Normalize(uint value, uint typeBits) => value >> 24 == 0 ? value | typeBits : value;
    }
}
