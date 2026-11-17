using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat spell table
/// </summary>
public partial class DatSpell
{
    /// <summary>
    /// Unique Id of this Spell
    /// </summary>
    public uint Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// MagicSchool
    /// </summary>
    public uint School { get; set; }

    public uint IconId { get; set; }

    /// <summary>
    /// SpellCategory: spells of one category do not stack
    /// </summary>
    public uint Category { get; set; }

    /// <summary>
    /// SpellFlags
    /// </summary>
    public uint Bitfield { get; set; }

    public uint BaseMana { get; set; }

    public float BaseRangeConstant { get; set; }

    public float BaseRangeMod { get; set; }

    /// <summary>
    /// Tells the strongest spell of a category
    /// </summary>
    public uint Power { get; set; }

    public float SpellEconomyMod { get; set; }

    /// <summary>
    /// How the taper slots of the formula are randomized per account
    /// </summary>
    public uint FormulaVersion { get; set; }

    /// <summary>
    /// Burn rate of the components
    /// </summary>
    public float ComponentLoss { get; set; }

    /// <summary>
    /// SpellType: which group of the spell table columns the server reads
    /// </summary>
    public uint MetaSpellType { get; set; }

    public uint MetaSpellId { get; set; }

    /// <summary>
    /// Enchantment and FellowEnchantment only
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// Enchantment and FellowEnchantment only
    /// </summary>
    public float? DegradeModifier { get; set; }

    /// <summary>
    /// Enchantment and FellowEnchantment only
    /// </summary>
    public float? DegradeLimit { get; set; }

    /// <summary>
    /// PortalSummon only
    /// </summary>
    public double? PortalLifetime { get; set; }

    /// <summary>
    /// Formula: id of a component of the dat spell component table, empty slots are NULL and only the last slots may be empty
    /// </summary>
    public uint? Component1 { get; set; }

    public uint? Component2 { get; set; }

    public uint? Component3 { get; set; }

    public uint? Component4 { get; set; }

    public uint? Component5 { get; set; }

    public uint? Component6 { get; set; }

    public uint? Component7 { get; set; }

    public uint? Component8 { get; set; }

    /// <summary>
    /// PlayScript
    /// </summary>
    public uint CasterEffect { get; set; }

    /// <summary>
    /// PlayScript
    /// </summary>
    public uint TargetEffect { get; set; }

    /// <summary>
    /// PlayScript
    /// </summary>
    public uint FizzleEffect { get; set; }

    public double RecoveryInterval { get; set; }

    public float RecoveryAmount { get; set; }

    /// <summary>
    /// Sort order in the client spell list
    /// </summary>
    public uint DisplayOrder { get; set; }

    /// <summary>
    /// ItemType
    /// </summary>
    public uint NonComponentTargetType { get; set; }

    /// <summary>
    /// Additional mana cost per target
    /// </summary>
    public uint ManaMod { get; set; }

    public DateTime LastModified { get; set; }

    public virtual Spell Spell { get; set; }
}
