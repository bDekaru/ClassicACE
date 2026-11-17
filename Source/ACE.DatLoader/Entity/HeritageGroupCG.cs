using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class HeritageGroupCG : IUnpackable
    {
        public string Name { get; private set; }
        public uint IconImage { get; private set; }
        public uint SetupID { get; private set; } // Basic character model
        public uint EnvironmentSetupID { get; private set; } // This is the background environment during Character Creation
        public uint AttributeCredits { get; set; }
        public uint SkillCredits { get; set; }
        public List<int> PrimaryStartAreas { get; } = new List<int>();
        public List<int> SecondaryStartAreas { get; } = new List<int>();
        public List<SkillCG> Skills { get; } = new List<SkillCG>();
        public List<TemplateCG> Templates { get; } = new List<TemplateCG>();
        public Dictionary<int, SexCG> Genders { get; } = new Dictionary<int, SexCG>();

        /// <summary>The byte in front of the genders, kept so a rewritten group equals the original.</summary>
        public byte GendersMarker { get; private set; } = 1;

        public void Unpack(BinaryReader reader)
        {
            Name                = reader.ReadString();
            IconImage           = reader.ReadUInt32();
            SetupID             = reader.ReadUInt32();
            EnvironmentSetupID  = reader.ReadUInt32();
            AttributeCredits    = reader.ReadUInt32();
            SkillCredits        = reader.ReadUInt32();

            PrimaryStartAreas.UnpackSmartArray(reader);
            SecondaryStartAreas.UnpackSmartArray(reader);
            Skills.UnpackSmartArray(reader);
            Templates.UnpackSmartArray(reader);
            GendersMarker = reader.ReadByte();
            Genders.UnpackSmartArray(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(IconImage);
            writer.Write(SetupID);
            writer.Write(EnvironmentSetupID);
            writer.Write(AttributeCredits);
            writer.Write(SkillCredits);

            writer.WriteSmartArray(PrimaryStartAreas);
            writer.WriteSmartArray(SecondaryStartAreas);
            writer.WriteSmartArray(Skills, (item, w) => item.Pack(w));
            writer.WriteSmartArray(Templates, (item, w) => item.Pack(w));

            writer.Write(GendersMarker);

            writer.WriteCompressedUInt32((uint)Genders.Count);

            foreach (var gender in Genders)
            {
                writer.Write(gender.Key);
                gender.Value.Pack(writer);
            }
        }
    }
}
