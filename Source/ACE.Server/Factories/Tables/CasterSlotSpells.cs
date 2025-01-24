using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;
using ACE.Server.WorldObjects;

namespace ACE.Server.Factories.Tables
{
    public static class CasterSlotSpells
    {
        private static ChanceTable<SpellId> orbSpells = new ChanceTable<SpellId>()
        {
            ( SpellId.StrengthOther1,     0.05f ),
            ( SpellId.EnduranceOther1,    0.05f ),
            ( SpellId.CoordinationOther1, 0.05f ),
            ( SpellId.QuicknessOther1,    0.05f ),
            ( SpellId.FocusOther1,        0.05f ),
            ( SpellId.WillpowerOther1,    0.05f ),
            ( SpellId.FealtyOther1,       0.10f ),
            ( SpellId.HealOther1,         0.10f ),
            ( SpellId.RevitalizeOther1,   0.10f ),
            ( SpellId.ManaBoostOther1,    0.10f ),
            ( SpellId.RegenerationOther1, 0.10f ),
            ( SpellId.RejuvenationOther1, 0.10f ),
            ( SpellId.ManaRenewalOther1,  0.10f ),
        };

        private static ChanceTable<SpellId> wandStaffSpells = new ChanceTable<SpellId>()
        {
            ( SpellId.WhirlingBlade1,     0.14f ),
            ( SpellId.ForceBolt1,         0.14f ),
            ( SpellId.ShockWave1,         0.14f ),
            ( SpellId.AcidStream1,        0.14f ),
            ( SpellId.FlameBolt1,         0.15f ),
            ( SpellId.FrostBolt1,         0.14f ),
            ( SpellId.LightningBolt1,     0.15f ),
        };

        private static ChanceTable<SpellId> netherSpells = new ChanceTable<SpellId>()
        {
            ( SpellId.Corruption1,            0.20f ),
            ( SpellId.NetherArc1,             0.20f ),
            ( SpellId.NetherBolt1,            0.20f ),
            ( SpellId.Corrosion1,             0.15f ),
            ( SpellId.CurseWeakness1,         0.10f ),
            ( SpellId.CurseFestering1,        0.10f ),
            ( SpellId.CurseDestructionOther1, 0.05f ),
        };

        public static SpellId Roll(WorldObject wo)
        {
            var table = IsOrb(wo) ? orbSpells :
                wo.W_DamageType == DamageType.Nether ? netherSpells : wandStaffSpells;

            return table.Roll();
        }

        public static bool IsOrb(WorldObject wo)
        {
            // todo: any other wcids for obs?
            return wo.WeenieClassId == (int)Enum.WeenieClassName.orb;
        }
        public static SpellId PseudoRandomRoll(WorldObject wo, int seed)
        {
            var table = IsOrb(wo) ? orbSpells :
                wo.W_DamageType == DamageType.Nether ? netherSpells : wandStaffSpells;

            return table.PseudoRandomRoll(seed);
        }
    }
}
