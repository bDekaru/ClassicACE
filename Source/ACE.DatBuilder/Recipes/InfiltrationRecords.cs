using System.Collections.Generic;
using System.Text.Json;

using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The Infiltration portal records that are made from other records rather than kept as files in Custom Files/Portal/Infiltration: three come from the 2005 dat, two from the EoR record with the Infiltration edits of the data files.
    /// </summary>
    public static class InfiltrationRecords
    {
        public const uint MapId = 0x0600127D;
        public const uint ChargenId = 0x0E000002;
        public const uint SkillTableId = 0x0E000004;
        public const uint SpellTableId = 0x0E00000E;
        public const uint XpTableId = 0x0E000018;

        public const string ChargenEditsFile = "infiltration-chargen.json";
        public const string SpellEditsFile = "infiltration-spells.json";

        // The two skills whose formula differs from the 2005 record (X, Z and Attr1 are set in SkillTableFromPreToD).
        private const uint LeadershipSkillId = 35;
        private const uint LoyaltySkillId = 36;

        public static DatRecordStep[] All(string date)
        {
            return new[]
            {
                MapFromPreToD(date),
                new DatRecordStep("XP table from the 2005 dat", date, c => c.Write(XpTableId, XpTable.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, XpTableId)).Pack(), "XP table")),
                new DatRecordStep("Skill table from the 2005 dat", date, SkillTableFromPreToD),
                ChargenFromEoR(date, ChargenEditsFile),
                new DatRecordStep("Spell table from the EoR record", date, c =>
                {
                    var spells = c.ReadTarget<SpellTable>(SpellTableId);
                    DatSpellEdits.Apply(spells, DatRecipeData.Load<List<Dictionary<string, JsonElement>>>(SpellEditsFile));
                    c.Write(SpellTableId, spells.Pack(), "spell table");
                })
            };
        }

        /// <summary>The in-game map of the pre-ToD look; the same record in the CustomDM and Infiltration rulesets.</summary>
        public static DatRecordStep MapFromPreToD(string date)
        {
            return new DatRecordStep("In-game map from the 2005 dat", date, c => c.Write(MapId, Texture.UnpackPreToD(c.ReadSource(DatBuildRecipes.PreToDPortalDat, MapId)).Pack(), "in-game map"));
        }

        public static DatRecordStep ChargenFromEoR(string date, string editsFile)
        {
            return new DatRecordStep("Character generation from the EoR record", date, c =>
            {
                var chargen = c.ReadTarget<CharGen>(ChargenId);
                DatRecipeData.Load<DatChargenEdits>(editsFile).Apply(chargen);
                c.Write(ChargenId, chargen.Pack(), "character generation");
            });
        }

        private static void SkillTableFromPreToD(DatRecordContext context)
        {
            var skills = context.ReadSource<SkillTable>(DatBuildRecipes.PreToDPortalDat, SkillTableId);

            foreach (var id in new[] { LeadershipSkillId, LoyaltySkillId })
            {
                var formula = skills.SkillBaseHash[id].Formula;
                formula.X = 1;
                formula.Z = 4;
                formula.Attr1 = 6;
            }

            context.Write(SkillTableId, skills.Pack(), "skill table");
        }
    }
}
