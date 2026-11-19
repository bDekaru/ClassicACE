using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>The language records that are made from the EoR record with the edits of a data file rather than kept as files in the Language folder of Custom Files.</summary>
    public static class LanguageRecords
    {
        public const uint ChargenHeritageUiId = 0x21000046;
        public const uint ChargenTextId = 0x23000002;
        public const uint TextStringsId = 0x23000003;

        /// <summary>The heritage layout edits are the same in every ruleset.</summary>
        public const string ChargenHeritageUiFile = "chargen-heritage-ui.json";
        public const string InfiltrationChargenTextFile = "infiltration-chargen-text.json";
        public const string CustomDMChargenTextFile = "customdm-chargen-text.json";

        // Source dats of the steps below (MD5 of the files):
        //   EoR language dat (heritage layout, character generation text): language - 2015-06-18 - Iteration 994 - EoR.dat, MD5 b55f7465dbf4fa94fc919d51afcb70da
        //   EoR portal dat (property table the layout is read with): portal - 2017-01-29 - Iteration 2072 - EoR.dat, MD5 2c89662a44fcdc2a3c31fe8f6677d265
        public static DatRecordStep[] Infiltration(string date)
        {
            return new[] { ChargenHeritageUi(date), ChargenText(date, InfiltrationChargenTextFile), TextureDetailLabels(date) };
        }

        public static DatRecordStep[] CustomDM(string date)
        {
            return new[] { ChargenHeritageUi(date), ChargenText(date, CustomDMChargenTextFile), TextureDetailLabels(date) };
        }

        /// <summary>The labels of the landscape and environment detail options, as the client's texture detail patch reads them by the hash of the name.</summary>
        public static readonly (string Name, string Label)[] TextureDetailLabelEntries =
        {
            ("ID_Graphics_TexFull", "Full"),
            ("ID_Graphics_TexHalf", "1/2"),
            ("ID_Graphics_TexQuarter", "1/4"),
            ("ID_Graphics_TexEighth", "1/8"),
        };

        public static IList<StringTableData> MakeTextureDetailLabels()
        {
            var entries = new List<StringTableData>();

            foreach (var (name, label) in TextureDetailLabelEntries)
            {
                var entry = new StringTableData { Id = SpellTable.ComputeHash(name) };
                entry.Strings.Add(label);
                entries.Add(entry);
            }

            return entries;
        }

        private static DatRecordStep TextureDetailLabels(string date)
        {
            return new DatRecordStep("Texture detail option labels", date, c =>
            {
                var table = c.ReadTarget<StringTable>(TextStringsId);

                foreach (var entry in MakeTextureDetailLabels())
                    InsertInBucketOrder(table, entry);

                c.Write(TextStringsId, table.Pack(), "texture detail option labels");
            });
        }

        /// <summary>The bucket counts the client's string table hash tables are sized from; the size byte of a record is an index into this list.</summary>
        private static readonly uint[] StringTableBucketCounts =
        {
            11, 23, 47, 89, 191, 383, 761, 1531, 3067, 6143, 12287, 24571, 49139, 98299, 196597, 393209, 786431, 1572859, 3145721, 6291449, 12582893, 25165811, 50331599
        };

        /// <summary>The client adds the entries of a record to its hash table in file order and rejects the whole record when an entry's bucket is lower than the previous one's, so new entries go in at their bucket position, not at the end.</summary>
        public static void InsertInBucketOrder(StringTable table, StringTableData entry)
        {
            if (table.Unknown >= StringTableBucketCounts.Length)
                throw new InvalidDataException($"String table size byte {table.Unknown} is not a known bucket count index.");

            var buckets = StringTableBucketCounts[table.Unknown];
            var entries = table.StringTableData;

            for (var i = 1; i < entries.Count; i++)
                if (entries[i].Id % buckets < entries[i - 1].Id % buckets)
                    throw new InvalidDataException($"String table {table.Id:X8} is not in bucket order at entry {i}.");

            if (entries.Any(e => e.Id == entry.Id))
                throw new InvalidDataException($"String {entry.Id:X8} already exists in {table.Id:X8}.");

            var bucket = entry.Id % buckets;
            var position = entries.Count;

            while (position > 0 && entries[position - 1].Id % buckets > bucket)
                position--;

            entries.Insert(position, entry);
        }

        private static DatRecordStep ChargenHeritageUi(string date)
        {
            return new DatRecordStep("Character generation heritage layout from the EoR record", date, c =>
            {
                var master = c.ReadSource<MasterProperty>(DatBuildRecipes.PortalBaseDat, DatLayoutEdits.MasterPropertyId);
                var layout = DatLayoutEdits.Unpack(c.ReadTarget(ChargenHeritageUiId), master);
                DatLayoutEdits.Apply(layout, DatRecipeData.Load<List<DatLayoutMediaEdit>>(ChargenHeritageUiFile));
                c.Write(ChargenHeritageUiId, layout.Pack(), "character generation heritage layout");
            });
        }

        private static DatRecordStep ChargenText(string date, string editsFile)
        {
            return new DatRecordStep("Character generation text from the EoR record", date, c =>
            {
                var table = c.ReadTarget<StringTable>(ChargenTextId);
                DatStringEdits.Apply(table, DatRecipeData.Load<List<DatStringEdit>>(editsFile));
                c.Write(ChargenTextId, table.Pack(), "character generation text");
            });
        }
    }
}
