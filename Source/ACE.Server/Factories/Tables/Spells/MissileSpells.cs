using System.Collections.Generic;

using log4net;

using ACE.Common;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Factories.Entity;

namespace ACE.Server.Factories.Tables
{
    public static class MissileSpells
    {
        private static readonly List<(SpellId spellId, float chance)> weaponMissileSpells = new List<(SpellId, float)>()
        {
            ( SpellId.SwiftKillerSelf1,  0.30f ),
            ( SpellId.DefenderSelf1,     0.25f ),
            ( SpellId.BloodDrinkerSelf1, 1.00f ),
        };

        public static ChanceTable<SpellId> missileProcs = new ChanceTable<SpellId>(ChanceTableType.Weight)
        {
            ( SpellId.Undef,              150.0f ),

            ( SpellId.StaminaToHealthSelf1, 1.0f ),
            ( SpellId.StaminaToManaSelf1,   1.0f ),
            ( SpellId.ManaToStaminaSelf1,   1.0f ),
            ( SpellId.ManaToHealthSelf1,    1.0f ),
            ( SpellId.HealthToStaminaSelf1, 1.0f ),
            ( SpellId.HealthToManaSelf1,    1.0f ),

            ( SpellId.DrainStamina1,        1.0f ),
            ( SpellId.DrainMana1,           1.0f ),
            ( SpellId.DrainHealth1,         1.0f ),

            ( SpellId.BloodLoather,         1.0f ),
            ( SpellId.LeadenWeapon1,        1.0f ),
            ( SpellId.TurnBlade1,           1.0f ),
            ( SpellId.Brittlemail1,         1.0f ),
            ( SpellId.TurnShield1,          1.0f ),
        };

        private static ChanceTable<SpellId> missileProcsCertain = new ChanceTable<SpellId>(ChanceTableType.Weight)
        {
            ( SpellId.StaminaToHealthSelf1, 1.0f ),
            ( SpellId.StaminaToManaSelf1,   1.0f ),
            ( SpellId.ManaToStaminaSelf1,   1.0f ),
            ( SpellId.ManaToHealthSelf1,    1.0f ),
            ( SpellId.HealthToStaminaSelf1, 1.0f ),
            ( SpellId.HealthToManaSelf1,    1.0f ),

            ( SpellId.DrainStamina1,        1.0f ),
            ( SpellId.DrainMana1,           1.0f ),
            ( SpellId.DrainHealth1,         1.0f ),

            ( SpellId.BloodLoather,         1.0f ),
            ( SpellId.LeadenWeapon1,        1.0f ),
            ( SpellId.TurnBlade1,           1.0f ),
            ( SpellId.Brittlemail1,         1.0f ),
            ( SpellId.TurnShield1,          1.0f ),
        };

        static MissileSpells()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Ruleset.CustomDM)
            {
                weaponMissileSpells = new List<(SpellId, float)>()
                {
                    //( SpellId.SwiftKillerSelf1,  0.30f ),
                    ( SpellId.DefenderSelf1,     0.25f ),
                    //( SpellId.BloodDrinkerSelf1, 1.00f ),
                };
            }
        }

        public static List<SpellId> Roll(TreasureDeath treasureDeath)
        {
            var spells = new List<SpellId>();

            foreach (var spell in weaponMissileSpells)
            {
                var rng = ThreadSafeRandom.NextInterval(treasureDeath.LootQualityMod);

                if (rng < spell.chance)
                    spells.Add(spell.spellId);
            }
            return spells;
        }

        public static SpellId RollProc(TreasureDeath treasureDeath)
        {
            float lootQualityMod = 0.0f;
            if (treasureDeath != null)
                lootQualityMod = treasureDeath.LootQualityMod;
            return missileProcs.Roll(lootQualityMod);
        }

        public static SpellId PseudoRandomRollProc(int seed)
        {
            return missileProcsCertain.PseudoRandomRoll(seed);
        }
    }
}
