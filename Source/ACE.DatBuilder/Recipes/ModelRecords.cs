using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using static ACE.DatBuilder.DatRecipeData;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Models of the older dats written as ToD records: every record of a data file is made from a record of the 2005 dat, and the ids it refers to are replaced by the ids the data file gives (the records the file lists, and the explicit maps).
    /// </summary>
    public static class ModelRecords
    {
        public class Entry
        {
            /// <summary>The record that is written.</summary>
            public string Id { get; set; }

            /// <summary>The 2005 record it is made from.</summary>
            public string Source { get; set; }

            /// <summary>Palette records only used by textures: the palette the texture reads.</summary>
            public string Palette { get; set; }

            /// <summary>Physics script entries only: the order of the script's entries, as positions in the source record (the written script is not in the source's order).</summary>
            public List<int> Order { get; set; }

            /// <summary>Surface texture entries only: the texture (0x06) it refers to.</summary>
            public string Texture { get; set; }

            /// <summary>GfxObj entries only: surfaces (by their id in the source) that take the given id instead of the id the file maps them to.</summary>
            public Dictionary<string, string> SurfaceOverrides { get; set; }

            /// <summary>Scene entries only: replaces the density (Freq) of every object of the scene.</summary>
            public float? Freq { get; set; }
        }

        public class Map
        {
            public string Source { get; set; }
            public string Target { get; set; }
        }

        public class Data
        {
            public List<Entry> Palettes { get; set; } = new List<Entry>();
            public List<Entry> Textures { get; set; } = new List<Entry>();
            public List<Entry> SurfaceTextures { get; set; } = new List<Entry>();
            public List<Entry> Surfaces { get; set; } = new List<Entry>();
            public List<Entry> GfxObjs { get; set; } = new List<Entry>();
            public List<Entry> Setups { get; set; } = new List<Entry>();
            public List<Entry> Scenes { get; set; } = new List<Entry>();
            public List<Entry> Waves { get; set; } = new List<Entry>();
            public List<Entry> Animations { get; set; } = new List<Entry>();
            public List<Entry> ParticleEmitters { get; set; } = new List<Entry>();
            public List<Entry> PhysicsScripts { get; set; } = new List<Entry>();
            public List<Entry> ScriptTables { get; set; } = new List<Entry>();
            public List<Entry> MotionTables { get; set; } = new List<Entry>();
            public List<Entry> PaletteSets { get; set; } = new List<Entry>();
            public List<Entry> SoundTables { get; set; } = new List<Entry>();
            public List<Entry> Clothing { get; set; } = new List<Entry>();

            /// <summary>The scenes keep the object ids of their source: the scenery objects are the ones the dat being built already has, not the copies this file makes.</summary>
            public bool KeepSceneObjects { get; set; }

            /// <summary>Ids the listed records refer to that are not made by this file (records of the end of retail dat, other files), as pre-ToD id to the id they have in the dat being built.</summary>
            public List<Map> Maps { get; set; } = new List<Map>();
        }

        // Source dat of a model step unless it names another: portal - 2005-02-XX - Iteration 2112 - Infiltration.dat, MD5 304275cb743dc28f37410e06e91cb17c
        public static DatRecordStep Step(string name, string date, string dataFile, string sourceDat = DatBuildRecipes.PreToDPortalDat)
        {
            return new DatRecordStep(name, date, c =>
            {
                foreach (var record in Build(DatRecipeData.Load<Data>(dataFile), id => c.ReadSource(sourceDat, id)))
                    c.Write(record.Key, record.Value, "model record");
            });
        }

        /// <summary>Makes every record of the data file, in the order palettes, textures, surface textures, surfaces, GfxObjs, setups, scenes.</summary>
        /// <exception cref="InvalidDataException">A record refers to an id the file neither lists nor maps.</exception>
        public static List<KeyValuePair<uint, byte[]>> Build(Data data, Func<uint, byte[]> readSource)
        {
            var result = new List<KeyValuePair<uint, byte[]>>();

            var explicitMap = data.Maps.ToDictionary(m => Hex(m.Source), m => Hex(m.Target));
            var palettes = ToMap(data.Palettes, explicitMap);
            var surfaceTextures = ToMap(data.SurfaceTextures, explicitMap);
            var surfaces = ToMap(data.Surfaces, explicitMap);
            var gfxObjs = ToMap(data.GfxObjs, explicitMap);
            var objects = ToMap(data.Setups, explicitMap);

            foreach (var gfx in gfxObjs)
                if (!objects.ContainsKey(gfx.Key))
                    objects[gfx.Key] = gfx.Value;

            var everything = IdMap(data);

            Func<uint, uint> all = id => id == 0 ? 0 : Lookup(everything, id);

            foreach (var entry in data.Palettes)
            {
                var palette = FileType.Unpack<Palette>(readSource(Hex(entry.Source)));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), palette.Repeated(Hex(entry.Id), DatPortalImporter.PaletteRepeat).Pack()));
            }

            foreach (var entry in data.Textures)
            {
                var paletteId = entry.Palette != null ? Hex(entry.Palette) : (uint?)null;
                var record = DatPortalImporter.ConvertImageKeepingFormat(readSource(Hex(entry.Source)), Hex(entry.Id), id => paletteId ?? Lookup(palettes, id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.SurfaceTextures)
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), SurfaceTexture.Create(Hex(entry.Id), Hex(entry.Texture)).Pack()));

            foreach (var entry in data.Surfaces)
            {
                var record = readSource(Hex(entry.Source));
                var surface = FileType.Unpack<Surface>(record.Skip(4).ToArray());
                surface.Remap(id => Lookup(surfaceTextures, id), id => Lookup(palettes, id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), surface.Pack()));
            }

            foreach (var entry in data.GfxObjs)
            {
                var record = DatEnvironmentConverter.ConvertGfxObj(readSource(Hex(entry.Source)), id => entry.SurfaceOverrides != null && entry.SurfaceOverrides.TryGetValue(id.ToString("X8"), out var over) ? Hex(over) : Lookup(surfaces, id));
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.Setups)
            {
                var setup = FileType.Unpack<SetupModel>(readSource(Hex(entry.Source)));
                setup.Remap(all);

                var record = setup.Pack();
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.Scenes)
            {
                var scene = FileType.Unpack<Scene>(readSource(Hex(entry.Source)));

                if (!data.KeepSceneObjects)
                    scene.Remap(all);

                if (entry.Freq.HasValue)
                    foreach (var obj in scene.Objects)
                        obj.Freq = entry.Freq.Value;

                var record = scene.Pack();
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.Waves)
            {
                var wave = FileType.Unpack<Wave>(readSource(Hex(entry.Source)));
                Add(result, entry, wave.Pack());
            }

            foreach (var entry in data.Animations)
            {
                var animation = FileType.Unpack<Animation>(readSource(Hex(entry.Source)));
                animation.Remap(all);
                Add(result, entry, animation.Pack());
            }

            foreach (var entry in data.ParticleEmitters)
            {
                var emitter = FileType.Unpack<ParticleEmitterInfo>(readSource(Hex(entry.Source)));
                emitter.Remap(all);
                Add(result, entry, emitter.Pack());
            }

            foreach (var entry in data.PhysicsScripts)
            {
                var script = FileType.Unpack<PhysicsScript>(readSource(Hex(entry.Source)));
                script.Remap(all);
                if (entry.Order != null)
                    script.Reorder(entry.Order);
                Add(result, entry, script.Pack());
            }

            foreach (var entry in data.ScriptTables)
            {
                var table = FileType.Unpack<PhysicsScriptTable>(readSource(Hex(entry.Source)));
                table.Remap(all);
                Add(result, entry, table.Pack());
            }

            foreach (var entry in data.MotionTables)
            {
                var table = FileType.Unpack<MotionTable>(readSource(Hex(entry.Source)));
                table.Remap(all);
                Add(result, entry, table.Pack());
            }

            foreach (var entry in data.PaletteSets)
            {
                var set = FileType.Unpack<PaletteSet>(readSource(Hex(entry.Source)));
                set.Remap(all);
                Add(result, entry, set.Pack());
            }

            foreach (var entry in data.SoundTables)
            {
                var table = FileType.Unpack<SoundTable>(readSource(Hex(entry.Source)));
                table.Remap(all);
                Add(result, entry, table.Pack());
            }

            foreach (var entry in data.Clothing)
            {
                var clothing = FileType.Unpack<ClothingTable>(readSource(Hex(entry.Source)));
                clothing.Remap(all, DatPortalImporter.PaletteRepeat);
                Add(result, entry, clothing.Pack());
            }

            return result;
        }

        /// <summary>Every source id of the data file with the id it is written as, plus the explicit maps.</summary>
        public static Dictionary<uint, uint> IdMap(Data data)
        {
            var map = data.Maps.ToDictionary(m => Hex(m.Source), m => Hex(m.Target));

            foreach (var list in new[] { data.Palettes, data.Textures, data.SurfaceTextures, data.Surfaces, data.GfxObjs, data.Setups, data.Scenes, data.Waves, data.Animations, data.ParticleEmitters, data.PhysicsScripts, data.ScriptTables, data.MotionTables, data.PaletteSets, data.SoundTables, data.Clothing })
                AddEntries(map, list);

            return map;
        }

        /// <summary>The first entry of a source is the record other records refer to; later entries of the same source are variants (copies with other surfaces) that nothing refers to.</summary>
        private static void AddEntries(Dictionary<uint, uint> map, List<Entry> entries)
        {
            foreach (var entry in entries.GroupBy(e => Hex(e.Source)).Select(g => g.First()))
                map[Hex(entry.Source)] = Hex(entry.Id);
        }

        private static void Add(List<KeyValuePair<uint, byte[]>> result, Entry entry, byte[] record)
        {
            WriteId(record, Hex(entry.Id));
            result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
        }

        private static Dictionary<uint, uint> ToMap(List<Entry> entries, Dictionary<uint, uint> explicitMap)
        {
            var map = new Dictionary<uint, uint>(explicitMap);

            AddEntries(map, entries);

            return map;
        }

        private static uint Lookup(Dictionary<uint, uint> map, uint id)
        {
            return map.TryGetValue(id, out var target) ? target : throw new InvalidDataException($"The model data has no target for the id {id:X8}.");
        }

        private static void WriteId(byte[] record, uint id)
        {
            Array.Copy(BitConverter.GetBytes(id), record, 4);
        }
    }
}
