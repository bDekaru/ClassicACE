using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class TemplateCG : IUnpackable
    {
        public string Name { get; set; }
        public uint IconImage { get; set; }
        public uint Title { get; set; }
        public uint Strength { get; set; }
        public uint Endurance { get; set; }
        public uint Coordination { get; set; }
        public uint Quickness { get; set; }
        public uint Focus { get; set; }
        public uint Self { get; set; }
        public List<uint> NormalSkillsList { get; } = new List<uint>();
        public List<uint> PrimarySkillsList { get; } = new List<uint>();

        public void Unpack(BinaryReader reader)
        {
            Name            = reader.ReadString();
            IconImage       = reader.ReadUInt32();
            Title           = reader.ReadUInt32();
            // Attributes
            Strength        = reader.ReadUInt32();
            Endurance       = reader.ReadUInt32();
            Coordination    = reader.ReadUInt32();
            Quickness       = reader.ReadUInt32();
            Focus           = reader.ReadUInt32();
            Self            = reader.ReadUInt32();

            NormalSkillsList.UnpackSmartArray(reader);
            PrimarySkillsList.UnpackSmartArray(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(IconImage);
            writer.Write(Title);
            writer.Write(Strength);
            writer.Write(Endurance);
            writer.Write(Coordination);
            writer.Write(Quickness);
            writer.Write(Focus);
            writer.Write(Self);

            writer.WriteSmartArray(NormalSkillsList);
            writer.WriteSmartArray(PrimarySkillsList);
        }
    }
}
