using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Monster-specific properties
    /// </summary>
    partial class Creature
    {
        public int AiOptions
        {
            get => GetProperty(PropertyInt.AiOptions) ?? 0;
            set { if (value == 0) RemoveProperty(PropertyInt.AiOptions); else SetProperty(PropertyInt.AiOptions, value); }
        }

        public bool CanOpenDoors => (AiOptions & (int)AiOption.CanOpenDoors) != 0;

        public bool UsedRangedAttacks = false;
        public bool UsedSpells = false;
    }
}
