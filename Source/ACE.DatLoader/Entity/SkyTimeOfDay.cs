using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkyTimeOfDay : IUnpackable, IPackable
    {
        public float Begin { get; private set; }

        public float DirBright { get; private set; }
        public float DirHeading { get; private set; }
        public float DirPitch { get; private set; }
        public uint DirColor { get; private set; }

        public float AmbBright { get; private set; }
        public uint AmbColor { get; private set; }

        public float MinWorldFog { get; private set; }
        public float MaxWorldFog { get; private set; }
        public uint WorldFogColor { get; private set; }
        public uint WorldFog { get; private set; }

        public List<SkyObjectReplace> SkyObjReplace { get; } = new List<SkyObjectReplace>();

        public void Unpack(BinaryReader reader)
        {
            Begin       = reader.ReadSingle();

            DirBright   = reader.ReadSingle();
            DirHeading  = reader.ReadSingle();
            DirPitch    = reader.ReadSingle();
            DirColor    = reader.ReadUInt32();

            AmbBright   = reader.ReadSingle();
            AmbColor    = reader.ReadUInt32();

            MinWorldFog     = reader.ReadSingle();
            MaxWorldFog     = reader.ReadSingle();
            WorldFogColor   = reader.ReadUInt32();
            WorldFog        = reader.ReadUInt32();

            reader.AlignBoundary();

            SkyObjReplace.Unpack(reader);
        }

        /// <summary>Takes the directional and ambient light of another time of day; the fog and the replacements stay.</summary>
        public void CopyLight(SkyTimeOfDay other)
        {
            DirBright   = other.DirBright;
            DirHeading  = other.DirHeading;
            DirPitch    = other.DirPitch;
            DirColor    = other.DirColor;
            AmbBright   = other.AmbBright;
            AmbColor    = other.AmbColor;
        }

        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var replace in SkyObjReplace)
                replace.Remap(map);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Begin);
            writer.Write(DirBright);
            writer.Write(DirHeading);
            writer.Write(DirPitch);
            writer.Write(DirColor);
            writer.Write(AmbBright);
            writer.Write(AmbColor);
            writer.Write(MinWorldFog);
            writer.Write(MaxWorldFog);
            writer.Write(WorldFogColor);
            writer.Write(WorldFog);
            writer.AlignBoundary();

            SkyObjReplace.Pack(writer);
        }
    }
}
