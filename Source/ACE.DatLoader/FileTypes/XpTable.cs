using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// Reads and stores the XP Tables from the client_portal.dat (file 0x0E000018).
    /// </summary>
    [DatFileType(DatFileType.XpTable)]
    public class XpTable : FileType
    {
        internal const uint FILE_ID = 0x0E000018;

        public List<uint> AttributeXpList { get; } = new List<uint>();
        public List<uint> VitalXpList { get; } = new List<uint>();
        public List<uint> TrainedSkillXpList { get; } = new List<uint>();
        public List<uint> SpecializedSkillXpList { get; } = new List<uint>();

        /// <summary>
        /// The XP needed to reach each level
        /// </summary>
        public List<ulong> CharacterLevelXPList { get; } = new List<ulong>();

        /// <summary>
        /// Number of credits gifted at each level
        /// </summary>
        public List<uint> CharacterLevelSkillCreditList { get; } = new List<uint>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            // The counts for each "Table" are at the top of the file.
            int attributeCount          = reader.ReadInt32();
            int vitalCount              = reader.ReadInt32();
            int trainedSkillCount       = reader.ReadInt32();
            int specializedSkillCount   = reader.ReadInt32();

            uint levelCount             = reader.ReadUInt32();

            for (int i = 0; i <= attributeCount; i++)
                AttributeXpList.Add(reader.ReadUInt32());

            for (int i = 0; i <= vitalCount; i++)
                VitalXpList.Add(reader.ReadUInt32());

            for (int i = 0; i <= trainedSkillCount; i++)
                TrainedSkillXpList.Add(reader.ReadUInt32());

            for (int i = 0; i <= specializedSkillCount; i++)
                SpecializedSkillXpList.Add(reader.ReadUInt32());

            for (int i = 0; i <= levelCount; i++)
                CharacterLevelXPList.Add(reader.ReadUInt64());

            for (int i = 0; i <= levelCount; i++)
                CharacterLevelSkillCreditList.Add(reader.ReadUInt32());
        }

        /// <summary>
        /// Reads the record as the pre-ToD (2005) client stored it: the same layout as ToD, except that the XP needed for each level is 32 bits wide.
        /// </summary>
        /// <exception cref="InvalidDataException">The record does not end where its counts say.</exception>
        public static XpTable UnpackPreToD(byte[] record)
        {
            var table = new XpTable();

            using (var reader = new BinaryReader(new MemoryStream(record)))
            {
                table.Id = reader.ReadUInt32();

                int attributeCount = reader.ReadInt32();
                int vitalCount = reader.ReadInt32();
                int trainedSkillCount = reader.ReadInt32();
                int specializedSkillCount = reader.ReadInt32();
                uint levelCount = reader.ReadUInt32();

                for (int i = 0; i <= attributeCount; i++)
                    table.AttributeXpList.Add(reader.ReadUInt32());

                for (int i = 0; i <= vitalCount; i++)
                    table.VitalXpList.Add(reader.ReadUInt32());

                for (int i = 0; i <= trainedSkillCount; i++)
                    table.TrainedSkillXpList.Add(reader.ReadUInt32());

                for (int i = 0; i <= specializedSkillCount; i++)
                    table.SpecializedSkillXpList.Add(reader.ReadUInt32());

                for (int i = 0; i <= levelCount; i++)
                    table.CharacterLevelXPList.Add(reader.ReadUInt32());

                for (int i = 0; i <= levelCount; i++)
                    table.CharacterLevelSkillCreditList.Add(reader.ReadUInt32());

                if (reader.BaseStream.Position != record.Length)
                    throw new InvalidDataException($"Pre-ToD XP table: {record.Length - reader.BaseStream.Position} bytes follow the credit list.");
            }

            return table;
        }

        /// <exception cref="InvalidDataException">The level and credit lists differ in length.</exception>
        public override void Pack(BinaryWriter writer)
        {
            if (CharacterLevelXPList.Count != CharacterLevelSkillCreditList.Count)
                throw new InvalidDataException($"XpTable: {CharacterLevelXPList.Count} level XP entries but {CharacterLevelSkillCreditList.Count} skill credit entries.");

            writer.Write(FILE_ID);

            writer.Write(AttributeXpList.Count - 1);
            writer.Write(VitalXpList.Count - 1);
            writer.Write(TrainedSkillXpList.Count - 1);
            writer.Write(SpecializedSkillXpList.Count - 1);
            writer.Write((uint)(CharacterLevelXPList.Count - 1));

            foreach (var xp in AttributeXpList)
                writer.Write(xp);

            foreach (var xp in VitalXpList)
                writer.Write(xp);

            foreach (var xp in TrainedSkillXpList)
                writer.Write(xp);

            foreach (var xp in SpecializedSkillXpList)
                writer.Write(xp);

            foreach (var xp in CharacterLevelXPList)
                writer.Write(xp);

            foreach (var credits in CharacterLevelSkillCreditList)
                writer.Write(credits);
        }
    }
}
