using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatLoader
{
    /// <summary>
    /// Converts the records of a pre-ToD portal dat to the ToD/EoR layout, keeping every id: models, environments, surfaces and images go through the
    /// <see cref="DatPortalImporter"/> converters (a pre-ToD image 0x05 becomes the SurfaceTexture of the same id plus a new image 0x06), a model gets
    /// the degrade record the pre-ToD client derives from its id, clothing palette ranges are multiplied (see <see cref="DatPortalImporter.PaletteRepeat"/>),
    /// and the types whose layout is the same in both formats are copied. The tables (0x0E), the Region (0x13) and the UI strings (0x31) are not converted here.
    /// </summary>
    public sealed class DatPreToDPortalConverter
    {
        private const uint DegradePrefix = 0x11000000;

        private static readonly HashSet<uint> CopiedTypes = new HashSet<uint> { 0x02, 0x03, 0x0A, 0x0F, 0x11, 0x12, 0x32, 0x33 };

        private const uint ClipMapSurface = 0x4;

        /// <summary>The UI strings are not converted: the EoR layouts name EoR's strings, so the recipe takes them from the EoR dat.</summary>
        private const uint StringType = 0x31;

        /// <summary>
        /// The effect (PlayScript) numbers of the pre-ToD dat from this one up are one lower than the numbers the server and the EoR dat use (EoR added a value here): in the 127 effect tables both dats have, every entry of the
        /// 2005 table from 0x1E up (to its last, 0x74) holds the scripts that the entry one above holds in the EoR table. Without the shift the server's Hide plays the unhide script and the player stays in the hidden state.
        /// </summary>
        private const uint FirstShiftedPlayScript = 0x1E;

        /// <summary>
        /// The sound numbers (the Sound enum, the keys of the sound tables) of the pre-ToD dat from this one up are 0x15 lower than the EoR and server numbers: in the 159 sound tables both dats have, the 2005 entries from 0x76 up
        /// (checked to 0x85) hold the waves that the EoR table holds 0x15 higher. The animation hooks name no sound that high (the highest is 0x60).
        /// </summary>
        private const uint FirstShiftedSound = 0x76;
        private const uint SoundShift = 0x15;

        /// <summary>
        /// The motion command numbers (the low 16 bits of a command or stance) of the pre-ToD dat from this one up are 3 lower than the EoR numbers (EoR added three values here): with the shift, 263 of the 278 motion tables
        /// both dats have equal EoR's key for key (239 before); the differing ones hold EoR's retouched animations and added commands. The highest number the 2005 tables use is 0x16E.
        /// </summary>
        private const uint FirstShiftedMotion = 0x115;
        private const uint MotionShift = 3;

        private static uint ShiftedMotion(uint number) => number >= FirstShiftedMotion ? number + MotionShift : number;

        private readonly DatDatabase _pre;
        private readonly ISet<uint> _basePalettes;
        private readonly Lazy<HashSet<uint>> _clipMapTextures;
        private uint _nextImage;

        /// <summary>Pre-ToD image id (0x05) to the id of the image (0x06) written for it.</summary>
        public SortedDictionary<uint, uint> ImageOfTexture { get; } = new SortedDictionary<uint, uint>();

        /// <summary>The ids of the models that got a degrade record, and the records that could not be converted, with the reason.</summary>
        public List<uint> DegradedModels { get; } = new List<uint>();
        public List<string> Failures { get; } = new List<string>();

        /// <param name="firstImageId">The id of the first new image (0x06); the ids go up from there in the order of the pre-ToD image ids.</param>
        /// <param name="basePalettes">The palettes the world gives objects as their base palette: the client recolors an object's indexed images (skin, hair, clothing) by laying the object's sub-palettes over the base palette,
        /// so an image on one of these palettes stays indexed (<see cref="DatPortalImporter.ConvertImageKeepingFormat"/>) instead of being baked to its colors.</param>
        public DatPreToDPortalConverter(DatDatabase pre, uint firstImageId, ISet<uint> basePalettes = null)
        {
            _pre = pre;
            _basePalettes = basePalettes ?? new HashSet<uint>();
            _nextImage = firstImageId;

            _clipMapTextures = new Lazy<HashSet<uint>>(() => ClipMapTextures(_pre));
        }

        /// <summary>The pre-ToD textures (0x05) that a clipmap surface uses: the client clips them at palette index 0.</summary>
        public static HashSet<uint> ClipMapTextures(DatDatabase pre)
        {
            return new HashSet<uint>(pre.AllFiles.Keys
                .Where(id => id >> 24 == 0x08 && (BitConverter.ToUInt32(pre.GetReaderForFile(id).Buffer, 4) & ClipMapSurface) != 0)
                .Select(id => BitConverter.ToUInt32(pre.GetReaderForFile(id).Buffer, 8)));
        }

        private bool IsIndexedOnBasePalette(byte[] texture)
        {
            if (BitConverter.ToUInt32(texture, 4) != DatPortalImporter.PreIndexedFormat)
                return false;

            var pixels = BitConverter.ToInt32(texture, 8) * BitConverter.ToInt32(texture, 12);

            return _basePalettes.Contains(BitConverter.ToUInt32(texture, 16 + pixels));
        }

        private byte[] Record(uint id)
        {
            return _pre.GetReaderForFile(id).Buffer;
        }

        /// <summary>Every record of the dat that has a rule, converted, in the order of the pre-ToD ids. A record that fails is listed in <see cref="Failures"/> and left out.</summary>
        public IEnumerable<KeyValuePair<uint, byte[]>> Convert()
        {
            foreach (var id in _pre.AllFiles.Keys.OrderBy(k => k))
            {
                var type = id >> 24;

                if (type == 0x0E || type == 0x13 || type == StringType)
                    continue;

                List<KeyValuePair<uint, byte[]>> records;

                try
                {
                    records = Convert(id, type);
                }
                catch (Exception e) when (e is InvalidDataException || e is NotSupportedException || e is EndOfStreamException || e is ArgumentException || e is KeyNotFoundException)
                {
                    Failures.Add($"{id:X8}: {e.GetType().Name} {e.Message}");
                    continue;
                }

                foreach (var record in records)
                    yield return record;
            }
        }

        private List<KeyValuePair<uint, byte[]>> Convert(uint id, uint type)
        {
            var record = Record(id);
            var result = new List<KeyValuePair<uint, byte[]>>();

            void Add(uint recordId, byte[] bytes) => result.Add(new KeyValuePair<uint, byte[]>(recordId, bytes));

            if (CopiedTypes.Contains(type))
                Add(id, record);
            else
                switch (type)
                {
                    case 0x01:
                        var degrade = DegradePrefix | (id & 0xFFFFFF);
                        var hasDegrade = _pre.AllFiles.ContainsKey(degrade);

                        Add(id, DatEnvironmentConverter.ConvertGfxObj(record, null, hasDegrade ? degrade : 0));

                        if (hasDegrade)
                            DegradedModels.Add(id);

                        break;
                    case 0x04:
                        var palette = new Palette();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            palette.Unpack(reader);

                        Add(id, palette.Repeated(id, DatPortalImporter.PaletteRepeat).Pack());
                        break;
                    case 0x05:
                        var imageId = _nextImage++;

                        if (IsIndexedOnBasePalette(record))
                            Add(imageId, DatPortalImporter.ConvertImageKeepingFormat(record, imageId));
                        else
                            Add(imageId, DatPortalImporter.ConvertImage(record, imageId, Record, out _, out _, out _, _clipMapTextures.Value.Contains(id)));

                        Add(id, SurfaceTexture.Create(id, imageId).Pack());
                        ImageOfTexture[id] = imageId;
                        break;
                    case 0x06:
                        var plain = Texture.UnpackPreToD(record, SurfacePixelFormat.PFID_A8R8G8B8);

                        plain.KeyOutBlack();
                        Add(id, plain.Pack());
                        break;
                    case 0x08:
                        Add(id, DatPortalImporter.ConvertSurface(record, texture => texture));
                        break;
                    case 0x0D:
                        Add(id, DatEnvironmentConverter.Convert(record));
                        break;
                    case 0x34:
                        var scripts = new PhysicsScriptTable();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            scripts.Unpack(reader);

                        scripts.ScriptTable = scripts.ScriptTable.OrderBy(e => e.Key).ToDictionary(e => e.Key >= FirstShiftedPlayScript ? e.Key + 1 : e.Key, e => e.Value);
                        Add(id, scripts.Pack());
                        break;
                    case 0x09:
                        var motions = new MotionTable();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            motions.Unpack(reader);

                        motions.RemapCommands(ShiftedMotion);
                        Add(id, motions.Pack());
                        break;
                    case 0x20:
                        var sounds = new SoundTable();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            sounds.Unpack(reader);

                        sounds.RemapSounds(sound => sound >= FirstShiftedSound ? sound + SoundShift : sound);
                        Add(id, sounds.Pack());
                        break;
                    case 0x30:
                        var maneuvers = new CombatManeuverTable();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            maneuvers.Unpack(reader);

                        maneuvers.RemapCommands(ShiftedMotion);
                        Add(id, maneuvers.Pack());
                        break;
                    case 0x10:
                        var clothing = new ClothingTable();

                        using (var reader = new BinaryReader(new MemoryStream(record)))
                            clothing.Unpack(reader);

                        clothing.Remap(x => x, DatPortalImporter.PaletteRepeat);
                        Add(id, clothing.Pack());
                        break;
                    default:
                        throw new NotSupportedException($"no conversion rule for type {type:X2}");
                }

            return result;
        }
    }
}
