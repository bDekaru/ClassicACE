using System;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SkillBase : IUnpackable, IPackable
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public uint IconId { get; set; }
        public int TrainedCost { get; set; }
        /// <summary>
        /// This is the total cost to specialize a skill, which INCLUDES the trained cost.
        /// </summary>
        public int SpecializedCost { get; set; }
        public uint Category { get; set; }      // 1 = combat, 2 = other, 3 = magic
        public uint ChargenUse { get; set; }    // always 1?
        /// <summary>
        /// This is the minimum SAC required for usability.
        /// 1 = Usable when untrained
        /// 2 = Trained or greater required for usability
        /// </summary>
        public uint MinLevel { get; set; }      // 1-2?

        public SkillFormula Formula { get; set; } = new SkillFormula();

        public double UpperBound { get; set; }
        public double LowerBound { get; set; }
        public double LearnMod { get; set; }

        public int UpgradeCostFromTrainedToSpecialized => SpecializedCost - TrainedCost;

        public void Unpack(BinaryReader reader)
        {
            Description = reader.ReadPString(); reader.AlignBoundary();
            Name = reader.ReadPString(); reader.AlignBoundary();
            IconId = reader.ReadUInt32();
            TrainedCost = reader.ReadInt32();
            SpecializedCost = reader.ReadInt32();
            Category = reader.ReadUInt32();
            ChargenUse = reader.ReadUInt32();
            MinLevel = reader.ReadUInt32();
            Formula.Unpack(reader);
            UpperBound = reader.ReadDouble();
            LowerBound = reader.ReadDouble();
            LearnMod = reader.ReadDouble();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WritePString(Description); writer.AlignBoundary();
            writer.WritePString(Name); writer.AlignBoundary();
            writer.Write(IconId);
            writer.Write(TrainedCost);
            writer.Write(SpecializedCost);
            writer.Write(Category);
            writer.Write(ChargenUse);
            writer.Write(MinLevel);
            Formula.Pack(writer);
            writer.Write(UpperBound);
            writer.Write(LowerBound);
            writer.Write(LearnMod);
        }

        public SkillBase() { }

        public SkillBase(SkillFormula formula)
        {
            Formula = formula;
        }
    }
}
