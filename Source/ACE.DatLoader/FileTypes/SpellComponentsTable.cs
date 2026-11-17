using System.Collections.Generic;
using System.IO;
using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    [DatFileType(DatFileType.SpellComponentTable)]
    public class SpellComponentsTable : FileType
    {
        public enum Type
        {
            Scarab      = 1,
            Herb        = 2,
            Powder      = 3,
            Potion      = 4,
            Talisman    = 5,
            Taper       = 6,
            PotionPea   = 7,
            TalismanPea = 5,
            TaperPea    = 7
        }

        internal const uint FILE_ID = 0x0E00000F;

        public Dictionary<uint, SpellComponentBase> SpellComponents { get; } = new Dictionary<uint, SpellComponentBase>();

        /// <summary>The hash table's bucket count as stored in the record; the client rebuilds the table from it.</summary>
        public ushort BucketSize { get; set; } = 32;

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            uint numComps = reader.ReadUInt16(); // Should be 163 or 0xA3
            BucketSize = reader.ReadUInt16();
            reader.AlignBoundary();

            SpellComponents.Unpack(reader, numComps);
        }

        /// <summary>Writes the record in the order of <see cref="SpellComponents"/>.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(FILE_ID);
                writer.Write((ushort)SpellComponents.Count);
                writer.Write(BucketSize);

                foreach (var component in SpellComponents)
                {
                    writer.Write(component.Key);
                    component.Value.Pack(writer);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static string GetSpellWords(SpellComponentsTable comps, List<uint> formula)
        {
            string firstSpellWord = "";
            string secondSpellWord = "";
            string thirdSpellWord = "";

            if (formula == null) return "";

            // Locate the herb component in the Spell formula
            for (int i = 0; i < formula.Count; i++)
            {
                if (comps.SpellComponents[formula[i]].Type == (uint)Type.Herb)
                    firstSpellWord = comps.SpellComponents[formula[i]].Text;
            }

            // Locate the powder component in the Spell formula
            for (int i = 0; i < formula.Count; i++)
            {
                if (comps.SpellComponents[formula[i]].Type == (uint)Type.Powder)
                    secondSpellWord = comps.SpellComponents[formula[i]].Text;
            }

            // Locate the potion component in the Spell formula
            for (int i = 0; i < formula.Count; i++)
            {
                if (comps.SpellComponents[formula[i]].Type == (uint)Type.Potion)
                    thirdSpellWord = comps.SpellComponents[formula[i]].Text;
            }

            // We need to make sure our second spell word, if any, is capitalized
            // Some spell words have no "secondSpellWord", so we're basically making sure the third word is capitalized.
            string secondSpellWordSet = (secondSpellWord + thirdSpellWord.ToLower());
            if(secondSpellWordSet != "")
            {
                string firstLetter = secondSpellWordSet.Substring(0, 1).ToUpper();
                secondSpellWordSet = firstLetter + secondSpellWordSet.Substring(1);

            }

            string result = $"{firstSpellWord} {secondSpellWordSet}";
            return result.Trim();
        }
    }
}
