using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatLoader
{
    /// <summary>
    /// Converts selected records of a pre-ToD portal dat so they can be laid over a ToD/EoR portal dat.
    /// Environments (0x0D) and models (0x01) are converted in place; the surfaces of a model go through the
    /// pre-ToD to ToD surface map, and a surface the map does not know is imported with its texture chain under new ids:
    /// pre-ToD textured surface + indexed image + palette become a ToD surface, SurfaceTexture and A8R8G8B8 image.
    /// </summary>
    public sealed class DatPortalImporter
    {
        private const uint SurfacePrefix = 0x08000000;
        private const uint PixelFormatA8R8G8B8 = 21;
        private const uint ImageHeaderValue = 6;
        private const uint PreIndexedFormat = 2;
        private const byte TexturedSurfaceMask = 6;

        // Pre-ToD image formats stored as plain pixels (ImgTex::UnPack): 3 A1R5G5B5, 4 A4R4G4B4, 7 R5G6B5, 10 R8G8B8 (3 bytes), 11 A8.
        // They keep their pixel layout in ToD (D3D formats 25, 26, 23, 20, 28); the header value is the one that format carries in the EoR dat.
        private static readonly Dictionary<uint, (uint ToDFormat, int BytesPerPixel, uint HeaderValue)> DirectFormats = new Dictionary<uint, (uint, int, uint)>
        {
            { 3, (25, 2, 7) },
            { 4, (26, 2, 8) },
            { 7, (23, 2, 7) },
            { 10, (20, 3, 9) },
            { 11, (28, 1, 10) },
        };

        private const uint PreR8G8B8Format = 10;

        /// <summary>The pixels of a pre-ToD plain-pixel image in the ToD layout. The 24-bit image (format 10) is stored as three separate planes (red, green, blue: the client's "color separated image"), ToD stores B, G, R per pixel.</summary>
        private static byte[] DirectPixels(byte[] texture, uint format, int pixels, int length)
        {
            var result = new byte[length];

            if (format == PreR8G8B8Format)
            {
                for (var i = 0; i < pixels; i++)
                {
                    result[i * 3] = texture[16 + 2 * pixels + i];
                    result[i * 3 + 1] = texture[16 + pixels + i];
                    result[i * 3 + 2] = texture[16 + i];
                }
            }
            else
                Array.Copy(texture, 16, result, 0, length);

            return result;
        }

        private readonly DatDatabase _pre;
        private readonly DatDatabase _target;
        private readonly IReadOnlyDictionary<ushort, ushort> _surfaceMap;

        private uint _nextSurface;
        private uint _nextImage;
        private uint _nextSurfaceTexture;

        private readonly Dictionary<uint, uint> _importedSurfaces = new Dictionary<uint, uint>();
        private readonly Dictionary<uint, uint> _importedTextures = new Dictionary<uint, uint>();

        /// <summary>The records to add to the target dat, by id.</summary>
        public SortedDictionary<uint, byte[]> Records { get; } = new SortedDictionary<uint, byte[]>();

        /// <summary>Pre-ToD surface id (low 16 bits) to the id imported for it, in the form of the surface map the cell converter reads.</summary>
        public IEnumerable<KeyValuePair<ushort, ushort>> ImportedSurfaces => _importedSurfaces.Select(s => new KeyValuePair<ushort, ushort>((ushort)s.Key, (ushort)s.Value)).OrderBy(s => s.Key);

        /// <summary>One line per decision, for the report.</summary>
        public List<string> Log { get; } = new List<string>();

        public DatPortalImporter(DatDatabase pre, DatDatabase target, IReadOnlyDictionary<ushort, ushort> surfaceMap)
        {
            _pre = pre;
            _target = target;
            _surfaceMap = surfaceMap;

            _nextSurface = MaxId(0x08) + 1;
            _nextImage = MaxId(0x06) + 1;
            _nextSurfaceTexture = MaxId(0x05) + 1;
        }

        private uint MaxId(uint type)
        {
            return _target.AllFiles.Keys.Where(k => k >> 24 == type).DefaultIfEmpty(type << 24).Max();
        }

        public void ImportEnvironment(uint id)
        {
            Records[id] = DatEnvironmentConverter.Convert(PreRecord(id));
            Log.Add($"{id:X8} environment converted");
        }

        public void ImportModel(uint id)
        {
            Records[id] = DatEnvironmentConverter.ConvertGfxObj(PreRecord(id), ResolveSurface);
            Log.Add($"{id:X8} model converted");
        }

        /// <summary>A Setup (0x02) has the same layout in both formats; the models it uses that the target lacks are converted too.</summary>
        public void ImportSetup(uint id)
        {
            var record = PreRecord(id);
            var setup = new FileTypes.SetupModel();

            using (var stream = new MemoryStream(record))
            using (var reader = new BinaryReader(stream))
                setup.Unpack(reader);

            Records[id] = record;
            Log.Add($"{id:X8} setup copied");

            foreach (var part in setup.Parts)
                if (!_target.AllFiles.ContainsKey(part) && !Records.ContainsKey(part))
                    ImportModel(part);
        }

        /// <summary>
        /// Imports the records named in a list such as "0D000418-0D00041B,01001753" (hex ids, ranges with '-'): environments (0D), models (01) and setups (02).
        /// </summary>
        public void ImportIds(string list)
        {
            foreach (var id in ParseIds(list))
            {
                switch (id >> 24)
                {
                    case 0x0D: ImportEnvironment(id); break;
                    case 0x01: ImportModel(id); break;
                    case 0x02: ImportSetup(id); break;
                    default: throw new ArgumentException($"Only environments (0D), models (01) and setups (02) can be imported by id, not {id:X8}.");
                }
            }
        }

        /// <summary>
        /// Replaces a building as a whole: the exterior models and environments of its group (see <see cref="DatBuildingGroups"/>), so that the cells of the pre-ToD map
        /// keep matching the records they were made for. Records already imported are not converted again.
        /// </summary>
        public DatBuildingGroup ImportBuilding(uint modelId, DatBuildingGroups groups)
        {
            var group = groups.Resolve(modelId);

            Log.Add($"{modelId:X8} building: {group.Buildings} buildings, models {string.Join(" ", group.Models.Select(m => m.ToString("X8")))}, environments {string.Join(" ", group.Environments.Select(e => e.ToString("X8")))}");

            foreach (var environment in group.Environments)
                if (!Records.ContainsKey(environment))
                    ImportEnvironment(environment);

            foreach (var model in group.Models)
                if (!Records.ContainsKey(model))
                    ImportModel(model);

            return group;
        }

        /// <summary>Resolves the listed pre-ToD surfaces (importing those the map does not know), in list order.</summary>
        public void ImportSurfaces(string list)
        {
            foreach (var id in ParseIds(list))
                ResolveSurface(id);
        }

        public static IEnumerable<uint> ParseIds(string list)
        {
            if (string.IsNullOrEmpty(list))
                yield break;

            foreach (var part in list.Split(','))
            {
                var bounds = part.Split('-');
                var first = Convert.ToUInt32(bounds[0], 16);
                var last = bounds.Length > 1 ? Convert.ToUInt32(bounds[1], 16) : first;

                for (var id = first; id <= last; id++)
                    yield return id;
            }
        }

        private byte[] PreRecord(uint id)
        {
            if (!_pre.AllFiles.ContainsKey(id))
                throw new InvalidDataException($"Record {id:X8} is not in the pre-ToD dat.");

            return _pre.GetReaderForFile(id).Buffer;
        }

        /// <summary>The surface id to use in the target dat for a pre-ToD surface id.</summary>
        public uint ResolveSurface(uint preSurfaceId)
        {
            if (_surfaceMap.TryGetValue((ushort)preSurfaceId, out var mapped))
                return SurfacePrefix | mapped;

            if (_importedSurfaces.TryGetValue(preSurfaceId, out var imported))
                return imported;

            var record = PreRecord(preSurfaceId);
            var type = BitConverter.ToUInt32(record, 4);

            var toD = new MemoryStream();
            toD.Write(record, 4, 4); // type

            if ((type & TexturedSurfaceMask) != 0)
            {
                var textureId = BitConverter.ToUInt32(record, 8);
                var surfaceTexture = ImportTexture(textureId);

                toD.Write(BitConverter.GetBytes(surfaceTexture), 0, 4);
                toD.Write(BitConverter.GetBytes(0u), 0, 4); // the palette lives in the image now
                toD.Write(record, 16, 12);
            }
            else
                toD.Write(record, 8, 16); // colour and the three floats

            var id = _nextSurface++;
            Records[id] = toD.ToArray();
            _importedSurfaces[preSurfaceId] = id;
            Log.Add($"{preSurfaceId:X8} surface imported as {id:X8}");

            return id;
        }

        /// <summary>Imports a pre-ToD image (0x05) as a ToD image and SurfaceTexture and returns the SurfaceTexture id.</summary>
        public uint ImportTexture(uint preTextureId)
        {
            if (_importedTextures.TryGetValue(preTextureId, out var known))
                return known;

            var texture = PreRecord(preTextureId);
            var imageId = _nextImage++;
            var imageRecord = ConvertImage(texture, imageId, PreRecord, out var width, out var height, out var detail);

            Records[imageId] = imageRecord;

            var surfaceTextureId = _target.AllFiles.ContainsKey(preTextureId) ? _nextSurfaceTexture++ : preTextureId;

            var surfaceTexture = new MemoryStream();
            surfaceTexture.Write(BitConverter.GetBytes(surfaceTextureId), 0, 4);
            surfaceTexture.Write(BitConverter.GetBytes(0u), 0, 4);
            surfaceTexture.WriteByte(2);
            surfaceTexture.Write(BitConverter.GetBytes(1u), 0, 4);
            surfaceTexture.Write(BitConverter.GetBytes(imageId), 0, 4);

            Records[surfaceTextureId] = surfaceTexture.ToArray();
            _importedTextures[preTextureId] = surfaceTextureId;
            Log.Add($"{preTextureId:X8} texture ({width}x{height}, {detail}) imported as image {imageId:X8} and surface texture {surfaceTextureId:X8}");

            return surfaceTextureId;
        }

        /// <summary>Converts a pre-ToD image record (0x05) into the ToD image record (0x06) with the given id; an indexed image reads its palette through <paramref name="readRecord"/>.</summary>
        /// <exception cref="InvalidDataException">The record is shorter than its header says or a palette index is outside its palette.</exception>
        /// <exception cref="NotSupportedException">The pre-ToD image format has no conversion.</exception>
        public static byte[] ConvertImage(byte[] texture, uint imageId, Func<uint, byte[]> readRecord, out int width, out int height, out string detail)
        {
            var preId = BitConverter.ToUInt32(texture, 0);
            var format = BitConverter.ToUInt32(texture, 4);

            width = BitConverter.ToInt32(texture, 8);
            height = BitConverter.ToInt32(texture, 12);
            var pixels = width * height;

            var image = new MemoryStream();

            if (format == PreIndexedFormat)
            {
                var paletteId = BitConverter.ToUInt32(texture, 16 + pixels);
                var palette = readRecord(paletteId);
                var colors = BitConverter.ToInt32(palette, 4);

                WriteImageHeader(image, imageId, ImageHeaderValue, width, height, PixelFormatA8R8G8B8, pixels * 4);

                for (var i = 0; i < pixels; i++)
                {
                    var index = texture[16 + i];

                    if (index >= colors)
                        throw new InvalidDataException($"Texture {preId:X8}: palette index {index} outside palette {paletteId:X8}");

                    image.Write(palette, 8 + 4 * index, 4);
                }

                detail = $"palette {paletteId:X8}";
            }
            else if (DirectFormats.TryGetValue(format, out var direct))
            {
                var length = pixels * direct.BytesPerPixel;

                if (16 + length > texture.Length)
                    throw new InvalidDataException($"Texture {preId:X8}: format {format} image of {width}x{height} is longer than its record");

                WriteImageHeader(image, imageId, direct.HeaderValue, width, height, direct.ToDFormat, length);
                image.Write(DirectPixels(texture, format, pixels, length), 0, length);

                detail = $"format {format}";
            }
            else
                throw new NotSupportedException($"Texture {preId:X8}: pre-ToD image format {format} is not converted");

            return image.ToArray();
        }

        /// <summary>
        /// The number of palette entries one pre-ToD palette entry becomes in <see cref="ConvertImageKeepingFormat"/> and <see cref="Palette.Repeated"/>:
        /// the image keeps its palette and its colors, as a 16-bit index into a palette in which every color is repeated this many times.
        /// </summary>
        public const int PaletteRepeat = 8;

        /// <summary>
        /// Converts a pre-ToD image record (0x05) into a ToD image record (0x06) that keeps the pixel format of the pre-ToD image: an indexed image becomes a PFID_INDEX16 image of the same palette
        /// (every index multiplied by <see cref="PaletteRepeat"/>, to be read against <see cref="Palette.Repeated"/> of that palette, whose id <paramref name="paletteId"/> gives for the pre-ToD palette id; the same id when null), the plain pixel formats keep their pixels. The header value is 0.
        /// </summary>
        /// <exception cref="InvalidDataException">The record is shorter than its header says.</exception>
        /// <exception cref="NotSupportedException">The pre-ToD image format has no conversion.</exception>
        public static byte[] ConvertImageKeepingFormat(byte[] texture, uint imageId, Func<uint, uint> paletteId = null)
        {
            var preId = BitConverter.ToUInt32(texture, 0);
            var format = BitConverter.ToUInt32(texture, 4);
            var width = BitConverter.ToInt32(texture, 8);
            var height = BitConverter.ToInt32(texture, 12);
            var pixels = width * height;

            Texture image;

            if (format == PreIndexedFormat)
            {
                if (20 + pixels > texture.Length)
                    throw new InvalidDataException($"Texture {preId:X8}: indexed image of {width}x{height} is longer than its record");

                var indices = new byte[pixels * 2];

                for (var i = 0; i < pixels; i++)
                {
                    var index = texture[16 + i] * PaletteRepeat;
                    indices[2 * i] = (byte)index;
                    indices[2 * i + 1] = (byte)(index >> 8);
                }

                image = Texture.Create(imageId, width, height, SurfacePixelFormat.PFID_INDEX16, indices, paletteId != null ? paletteId(BitConverter.ToUInt32(texture, 16 + pixels)) : BitConverter.ToUInt32(texture, 16 + pixels));
            }
            else if (DirectFormats.TryGetValue(format, out var direct))
            {
                var length = pixels * direct.BytesPerPixel;

                if (16 + length > texture.Length)
                    throw new InvalidDataException($"Texture {preId:X8}: format {format} image of {width}x{height} is longer than its record");

                image = Texture.Create(imageId, width, height, (SurfacePixelFormat)direct.ToDFormat, DirectPixels(texture, format, pixels, length));
            }
            else
                throw new NotSupportedException($"Texture {preId:X8}: pre-ToD image format {format} is not converted");

            return image.Pack();
        }

        private static void WriteImageHeader(Stream image, uint id, uint headerValue, int width, int height, uint pixelFormat, int length)
        {
            image.Write(BitConverter.GetBytes(id), 0, 4);
            image.Write(BitConverter.GetBytes(headerValue), 0, 4);
            image.Write(BitConverter.GetBytes(width), 0, 4);
            image.Write(BitConverter.GetBytes(height), 0, 4);
            image.Write(BitConverter.GetBytes(pixelFormat), 0, 4);
            image.Write(BitConverter.GetBytes(length), 0, 4);
        }

        public string ToText()
        {
            var text = new StringBuilder();

            foreach (var line in Log)
                text.AppendLine(line);

            text.AppendLine($"{Records.Count} records");
            return text.ToString();
        }
    }
}
