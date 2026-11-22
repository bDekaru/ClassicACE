using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class HeritageGroupCG : IUnpackable, IPackable
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

        /// <summary>
        /// Reads a heritage as the pre-ToD client stored it. Its sexes have no keys there: they are numbered by name (Male 1, Female 2), and the credits, skills and templates
        /// stay with the sexes (they are not read into the group).
        /// </summary>
        public void UnpackPreToD(BinaryReader reader)
        {
            Name                = reader.ReadPreToDString();
            IconImage           = reader.ReadUInt32();
            SetupID             = reader.ReadUInt32();
            reader.ReadUInt32(); // description string
            EnvironmentSetupID  = reader.ReadUInt32();

            PrimaryStartAreas.AddRange(reader.ReadPreToDList(r => r.ReadInt32()));
            SecondaryStartAreas.AddRange(reader.ReadPreToDList(r => r.ReadInt32()));

            var sexes = reader.ReadPreToDList(r => { var sex = new SexCG(); sex.UnpackPreToD(r); return sex; });

            for (var i = 0; i < sexes.Count; i++)
                Genders.Add(sexes[i].Name == "Male" ? 1 : sexes[i].Name == "Female" ? 2 : i + 1, sexes[i]);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(IconImage);
            writer.Write(SetupID);
            writer.Write(EnvironmentSetupID);
            writer.Write(AttributeCredits);
            writer.Write(SkillCredits);

            PrimaryStartAreas.PackSmartArray(writer);
            SecondaryStartAreas.PackSmartArray(writer);
            Skills.PackSmartArray(writer);
            Templates.PackSmartArray(writer);

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
