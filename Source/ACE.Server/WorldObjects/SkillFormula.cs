using System;
using System.Collections.Generic;
using System.Text;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    public class SkillFormula
    {
        // everything else: melee weapons (including finesse), thrown weapons, atlatls
        public static readonly float DefaultMod = 0.011f;

        // bows and crossbows
        public static readonly float BowMod = 0.008f;

        public static readonly float UnarmedMod = 0.004f; // Infiltration Ruleset: Unarmed combat gets str scaling but with the lower factor as a test to see if this is closer to retail values.

        public static readonly float ArmorMod = 200.0f / 3.0f;

        public static float GetAttributeMod(int currentAttribute, ACE.Entity.Enum.Skill skill = ACE.Entity.Enum.Skill.None)
        {
            float factor = 0.0f;

            switch(skill)
            {
                case ACE.Entity.Enum.Skill.Bow:
                    factor = BowMod;
                    break;
                case ACE.Entity.Enum.Skill.UnarmedCombat:
                    if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
                        factor = UnarmedMod;
                    break;
                default:
                    factor = DefaultMod;
                    break;
            }

            return Math.Max(1.0f + (currentAttribute - 55) * factor, 1.0f);
        }

        /// <summary>
        /// Converts AL from an additive linear value
        /// to a scaled damage multiplier
        /// </summary>
        public static float CalcArmorMod(float armorLevel)
        {
            if (armorLevel > 0)
                return ArmorMod / (armorLevel + ArmorMod);
            else if (armorLevel < 0)
                return 1.0f - armorLevel / ArmorMod;
            else
                return 1.0f;
        }
    }
}
