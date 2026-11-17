using ACE.DatLoader.FileTypes;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ACE.DatLoader.Entity
{

    public class SpellSet : IUnpackable
    {
        // uint key is the total combined item level of all the equipped pieces in the set
        // client calls this m_PieceCount
        public SortedDictionary<uint, SpellSetTiers> SpellSetTiers = new SortedDictionary<uint, SpellSetTiers>();

        public uint HighestTier = 0;

        public SortedDictionary<uint, SpellSetTiers> SpellSetTiersNoGaps = new SortedDictionary<uint, SpellSetTiers>();

        /// <summary>The tier hash table's bucket count as stored in the record; the client rebuilds the table from it.</summary>
        public ushort BucketSize { get; set; } = 8;

        public void Unpack(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;
            reader.ReadUInt16();
            BucketSize = reader.ReadUInt16();
            reader.BaseStream.Position = start;

            SpellSetTiers.UnpackPackedHashTable(reader);

            HighestTier = SpellSetTiers.Keys.LastOrDefault();

            SpellSetTiers lastSpellSetTier = null;

            for (uint i = 0; i <= HighestTier; i++)
            {
                if (SpellSetTiers.TryGetValue(i, out var spellSetTiers))
                    lastSpellSetTier = spellSetTiers;

                if (lastSpellSetTier != null)
                    SpellSetTiersNoGaps.Add(i, lastSpellSetTier);
            }
        }

        /// <summary>Writes the tiers in key order; <see cref="SpellSetTiersNoGaps"/> and <see cref="HighestTier"/> are derived on reading and not stored.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write((ushort)SpellSetTiers.Count);
            writer.Write(BucketSize);

            foreach (var tier in SpellSetTiers)
            {
                writer.Write(tier.Key);
                tier.Value.Pack(writer);
            }
        }
    }
}
