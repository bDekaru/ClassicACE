using ACE.Common;
using ACE.DatLoader.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.SurfaceTexture)]
    public class SurfaceTexture : FileType
    {
        // public int Id { get; private set; }
        public int Unknown { get; private set; }
        public byte UnknownByte { get; private set; }
        // public int TextureCount { get; private set; }
        public List<uint> Textures { get; private set; } = new List<uint>(); // These values correspond to a Surface (0x06) entry

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            Unknown = reader.ReadInt32();
            UnknownByte = reader.ReadByte();
            Textures.Unpack(reader);
        }

        /// <summary>A surface texture with one texture (0x06) in the form the pre-ToD conversion writes it.</summary>
        public static SurfaceTexture Create(uint id, uint texture)
        {
            var result = new SurfaceTexture { Id = id, UnknownByte = 2 };
            result.Textures.Add(texture);
            return result;
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Unknown);
            writer.Write(UnknownByte);
            Textures.Pack(writer);
        }

        /// <summary>Replaces every referenced texture id by map(id).</summary>
        public void Remap(Func<uint, uint> map)
        {
            for (var i = 0; i < Textures.Count; i++)
                Textures[i] = map(Textures[i]);
        }
    }
}
