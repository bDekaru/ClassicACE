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

        /// <summary>
        /// Reads the record as the pre-ToD (2005) client stored it, into the later shape: the heritages are numbered 1, 2, 3 in list order and the starter areas are passed over.
        /// </summary>
        /// <exception cref="InvalidDataException">The record does not end where its parts say.</exception>
        public static CharGen UnpackPreToD(byte[] record)
        {
            var chargen = new CharGen();

            using (var reader = new BinaryReader(new MemoryStream(record)))
            {
                chargen.Id = reader.ReadUInt32();
                reader.BaseStream.Position += 8 * 4; // help strings

                reader.SkipPreToDList(r =>
                {
                    r.ReadPreToDString();
                    r.SkipPreToDList(p => p.BaseStream.Position += 32);
                });

                var heritages = reader.ReadPreToDList(r => { var heritage = new HeritageGroupCG(); heritage.UnpackPreToD(r); return heritage; });

                for (var i = 0; i < heritages.Count; i++)
                    chargen.HeritageGroups.Add((uint)(i + 1), heritages[i]);

                if (reader.BaseStream.Position != record.Length)
                    throw new InvalidDataException($"Pre-ToD chargen: {record.Length - reader.BaseStream.Position} bytes follow the last part.");
            }

            return chargen;
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
