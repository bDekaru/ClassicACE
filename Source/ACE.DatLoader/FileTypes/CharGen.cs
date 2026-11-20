using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.CharacterGenerator)]
    public class CharGen : FileType
    {
        internal const uint FILE_ID = 0x0E000002;

        public List<StarterArea> StarterAreas { get; } = new List<StarterArea>();
        public Dictionary<uint, HeritageGroupCG> HeritageGroups { get; } = new Dictionary<uint, HeritageGroupCG>();

        /// <summary>The byte in front of the heritage groups, kept so a rewritten record equals the original.</summary>
        public byte HeritageGroupsMarker { get; private set; } = 1;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            reader.BaseStream.Position += 4; // second copy of the file id

            StarterAreas.UnpackSmartArray(reader);

            // HERITAGE GROUPS -- 11 standard player races and 2 Olthoi.
            HeritageGroupsMarker = reader.ReadByte();

            HeritageGroups.UnpackSmartArray(reader);
        }

        /// <summary>Writes the record in the order of <see cref="StarterAreas"/> and <see cref="HeritageGroups"/>.</summary>
        public override void Pack(BinaryWriter writer)
        {
            writer.Write(FILE_ID);
            writer.Write(FILE_ID);

            StarterAreas.PackSmartArray(writer);

            writer.Write(HeritageGroupsMarker);

            writer.WriteCompressedUInt32((uint)HeritageGroups.Count);

            foreach (var heritageGroup in HeritageGroups)
            {
                writer.Write(heritageGroup.Key);
                heritageGroup.Value.Pack(writer);
            }
        }
    }
}
