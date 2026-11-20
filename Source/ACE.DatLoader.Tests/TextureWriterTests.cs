using System.IO;
using ACE.DatBuilder;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.DatLoader.Tests
{
    /// <summary>Byte layout of the SurfaceTexture, RenderTexture and Texture writers, without installed dats.</summary>
    [TestClass]
    public class TextureWriterTests
    {
        [TestMethod]
        public void SurfaceTextureCreate_PacksCategoryZeroAndTexture2D()
        {
            var packed = SurfaceTexture.Create(0x0500000C, 0x06003B9D).Pack();

            CollectionAssert.AreEqual(SurfaceTextureRecord(0x0500000C, 0, TextureType.Texture2D, 0x06003B9D), packed);
        }

        [TestMethod]
        public void SurfaceTexture_UnpackThenPack_KeepsCategoryAndTextureType()
        {
            var record = SurfaceTextureRecord(0x05000001, 0xF0000001u, TextureType.Cube, 0x06000001, 0x06000002);

            CollectionAssert.AreEqual(record, DatRecordContext.Unpack<SurfaceTexture>(record).Pack());
        }

        [TestMethod]
        public void RenderTexture_UnpackThenPack_WritesTheSurfaceTextureLayout()
        {
            var record = SurfaceTextureRecord(0x15000000, 0x80000000u, TextureType.Movie2D, 0x06000010);

            CollectionAssert.AreEqual(record, DatRecordContext.Unpack<RenderTexture>(record).Pack());
        }

        [TestMethod]
        public void TextureCreate_PacksCategoryZero()
        {
            var pixels = new byte[] { 1, 2, 3, 4 };
            var packed = Texture.Create(0x06000001, 1, 1, SurfacePixelFormat.PFID_A8R8G8B8, pixels).Pack();

            CollectionAssert.AreEqual(TextureRecord(0x06000001, 0, 1, 1, SurfacePixelFormat.PFID_A8R8G8B8, pixels), packed);
        }

        [TestMethod]
        public void TextureUnpackPreToD_PacksTheToDHeaderValue()
        {
            // Pre-ToD record: id, width, height, then red, green, blue.
            var source = Bytes(w =>
            {
                w.Write(0x06000002u);
                w.Write(1);
                w.Write(1);
                w.Write(new byte[] { 10, 20, 30 });
            });

            var packed = Texture.UnpackPreToD(source, SurfacePixelFormat.PFID_A8R8G8B8).Pack();

            CollectionAssert.AreEqual(TextureRecord(0x06000002, Texture.ToDHeaderValue, 1, 1, SurfacePixelFormat.PFID_A8R8G8B8, new byte[] { 30, 20, 10, 0xFF }), packed);
        }

        [TestMethod]
        [DataRow(0u)]
        [DataRow(6u)]
        [DataRow(0x80000001u)]
        public void Texture_UnpackThenPack_KeepsTheCategory(uint category)
        {
            var record = TextureRecord(0x06000003, category, 1, 1, SurfacePixelFormat.PFID_A8R8G8B8, new byte[] { 5, 6, 7, 8 });

            CollectionAssert.AreEqual(record, DatRecordContext.Unpack<Texture>(record).Pack());
        }

        private static byte[] SurfaceTextureRecord(uint id, uint category, TextureType textureType, params uint[] textures)
        {
            return Bytes(w =>
            {
                w.Write(id);
                w.Write(category);
                w.Write((byte)textureType);
                w.Write(textures.Length);

                foreach (var texture in textures)
                    w.Write(texture);
            });
        }

        private static byte[] TextureRecord(uint id, uint category, int width, int height, SurfacePixelFormat format, byte[] pixels)
        {
            return Bytes(w =>
            {
                w.Write(id);
                w.Write(category);
                w.Write(width);
                w.Write(height);
                w.Write((uint)format);
                w.Write(pixels.Length);
                w.Write(pixels);
            });
        }

        private static byte[] Bytes(System.Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    write(writer);

                return stream.ToArray();
            }
        }
    }
}
