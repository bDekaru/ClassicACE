using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// Surface (0x08) numbers did not survive ToD, so the EoR models and cells of a dat built on the pre-ToD records name unrelated pre-ToD surfaces. They are pointed at the older surface
    /// of the same material, or at the EoR surface imported under a new id. Records that did not come from EoR keep their surfaces.
    /// </summary>
    public static class EoRSurfaceRecords
    {
        public const string DataFile = "customdm-eor-surfaces.json";

        public class SurfaceEdits
        {
            /// <summary>EoR surface to the older surface that stands for it.</summary>
            public Dictionary<string, string> Repoint { get; set; }

            /// <summary>EoR surface to the new id it is imported under.</summary>
            public Dictionary<string, string> Import { get; set; }

            /// <summary>EoR palettes whose id holds another palette here, to the new id they are imported under.</summary>
            public Dictionary<string, string> Palettes { get; set; }

            /// <summary>Surface textures that differ from the EoR record but hold the same picture.</summary>
            public List<string> SamePicture { get; set; }

            /// <summary>The surface that takes the place of each EoR surface.</summary>
            public Dictionary<uint, uint> SurfaceMap()
            {
                return Repoint.Concat(Import).ToDictionary(p => DatRecipeData.Hex(p.Key), p => DatRecipeData.Hex(p.Value));
            }
        }

        public static DatRecordStep Step(string date)
        {
            return new DatRecordStep("EoR surfaces of EoR models", date, c =>
            {
                var edits = DatRecipeData.Load<SurfaceEdits>(DataFile);
                var map = edits.SurfaceMap();
                var palettes = edits.Palettes.ToDictionary(p => DatRecipeData.Hex(p.Key), p => DatRecipeData.Hex(p.Value));
                var samePicture = edits.SamePicture.Select(DatRecipeData.Hex).ToHashSet();
                var eor = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PortalBaseDat), true);
                var preToD = new DatDatabase(Path.Combine(c.SourceDats, DatBuildRecipes.PreToDPortalDat), true);

                byte[] EoR(uint id) => c.ReadSource(DatBuildRecipes.PortalBaseDat, id);
                uint Palette(uint id) => palettes.TryGetValue(id, out var renamed) ? renamed : id;

                // A record an imported surface needs, at its own id, and what that record needs. Images only the EoR high-res dat has are left to that dat.
                void Bring(uint id)
                {
                    if (palettes.ContainsKey(id) || !eor.AllFiles.ContainsKey(id))
                        return;

                    var record = EoR(id);

                    var held = c.Writer.Read(id);

                    if (held != null && !held.SequenceEqual(record))
                    {
                        if (!samePicture.Contains(id))
                            throw new InvalidDataException($"An imported surface needs {id:X8}, whose id holds another record.");

                        return;
                    }

                    if (id >> 24 == 0x05)
                        foreach (var image in FileType.Unpack<SurfaceTexture>(record).Textures)
                            Bring(image);
                    else if (id >> 24 == 0x06 && FileType.Unpack<Texture>(record).DefaultPaletteId is { } palette)
                        Bring(palette);

                    if (held == null)
                        c.Write(id, record, "EoR record of an imported surface", true);
                }

                foreach (var palette in palettes)
                {
                    var record = FileType.Unpack<Palette>(EoR(palette.Key));
                    record.Id = palette.Value;
                    c.Write(palette.Value, record.Pack(), $"EoR palette {palette.Key:X8} under a new id");
                }

                foreach (var import in edits.Import)
                {
                    var eorId = DatRecipeData.Hex(import.Key);
                    var surface = FileType.Unpack<Surface>(EoR(eorId));

                    if (surface.OrigTextureId != 0)
                        Bring(surface.OrigTextureId);
                    if (surface.OrigPaletteId != 0)
                        Bring(surface.OrigPaletteId);

                    surface.Remap(texture => texture, Palette);
                    c.Write(DatRecipeData.Hex(import.Value), surface.Pack(), $"EoR surface {eorId:X8} under a new id");
                }

                // The records still as EoR has them: their images point at the renamed palettes, their models at the mapped surfaces.
                var changed = new List<KeyValuePair<uint, byte[]>>();

                foreach (var id in eor.AllFiles.Keys.Where(id => id >> 24 == 0x01 || id >> 24 == 0x06).OrderBy(id => id))
                {
                    var record = c.Writer.Read(id);

                    // A pre-ToD model means the pre-ToD surfaces even if it converts to the EoR bytes. The pre-ToD image ids hold EoR images here.
                    if (record == null || id >> 24 == 0x01 && preToD.AllFiles.ContainsKey(id) || !record.SequenceEqual(EoR(id)))
                        continue;

                    if (id >> 24 == 0x06)
                    {
                        var image = FileType.Unpack<Texture>(record);

                        if (image.DefaultPaletteId is { } palette && palettes.ContainsKey(palette))
                        {
                            image.DefaultPaletteId = Palette(palette);
                            changed.Add(new KeyValuePair<uint, byte[]>(id, image.Pack()));
                        }
                    }
                    else
                    {
                        var model = FileType.Unpack<GfxObj>(record);

                        if (!model.Surfaces.Any(map.ContainsKey))
                            continue;

                        for (var i = 0; i < model.Surfaces.Count; i++)
                            if (map.TryGetValue(model.Surfaces[i], out var surface))
                                model.Surfaces[i] = surface;

                        changed.Add(new KeyValuePair<uint, byte[]>(id, model.Pack()));
                    }
                }

                c.Write(changed, "EoR models and images pointed at the mapped surfaces and palettes", true);
            });
        }
    }
}
