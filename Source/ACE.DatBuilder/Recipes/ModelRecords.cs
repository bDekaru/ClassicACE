using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

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

            /// <summary>Surface texture entries only: the texture (0x06) it refers to.</summary>
            public string Texture { get; set; }
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

            /// <summary>Ids the listed records refer to that are not made by this file (records of the end of retail dat, other files), as pre-ToD id to the id they have in the dat being built.</summary>
            public List<Map> Maps { get; set; } = new List<Map>();
        }

        private static uint Hex(string value)
        {
            return Convert.ToUInt32(value, 16);
        }

        public const string TreesFile = "trees.json";

        public static DatRecordStep Trees(string date)
        {
            return Step("Trees from the 2005 dat", date, TreesFile);
        }

        public static DatRecordStep Step(string name, string date, string dataFile)
        {
            return new DatRecordStep(name, date, c =>
            {
                foreach (var record in Build(DatRecipeData.Load<Data>(dataFile), id => c.ReadSource(DatBuildRecipes.PreToDPortalDat, id)))
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

            foreach (var entry in data.Palettes)
            {
                var palette = ContextUnpack<Palette>(readSource(Hex(entry.Source)));
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
                var surface = ContextUnpack<Surface>(record.Skip(4).ToArray());
                surface.Remap(id => Lookup(surfaceTextures, id), id => Lookup(palettes, id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), surface.Pack()));
            }

            foreach (var entry in data.GfxObjs)
            {
                var record = DatEnvironmentConverter.ConvertGfxObj(readSource(Hex(entry.Source)), id => Lookup(surfaces, id));
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.Setups)
            {
                var setup = ContextUnpack<SetupModel>(readSource(Hex(entry.Source)));
                setup.Remap(id => Lookup(objects, id));
                var record = setup.Pack();
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            foreach (var entry in data.Scenes)
            {
                var scene = ContextUnpack<Scene>(readSource(Hex(entry.Source)));
                scene.Remap(id => Lookup(objects, id));
                var record = scene.Pack();
                WriteId(record, Hex(entry.Id));
                result.Add(new KeyValuePair<uint, byte[]>(Hex(entry.Id), record));
            }

            return result;
        }

        private static T ContextUnpack<T>(byte[] record) where T : FileType, new()
        {
            return DatRecordContext.Unpack<T>(record);
        }

        private static Dictionary<uint, uint> ToMap(List<Entry> entries, Dictionary<uint, uint> explicitMap)
        {
            var map = new Dictionary<uint, uint>(explicitMap);

            foreach (var entry in entries)
                map[Hex(entry.Source)] = Hex(entry.Id);

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
