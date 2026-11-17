using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat spell table: the spells of each equipment set tier
/// </summary>
public partial class DatSpellSetSpell
{
    /// <summary>
    /// EquipmentSet
    /// </summary>
    public uint SetId { get; set; }

    /// <summary>
    /// Piece count (or sum of item levels) at which the spells start to be active
    /// </summary>
    public uint Tier { get; set; }

    /// <summary>
    /// Order of the spell within the tier, from 0
    /// </summary>
    public uint Position { get; set; }

    /// <summary>
    /// Id of a dat_spell row
    /// </summary>
    public uint SpellId { get; set; }

    public DateTime LastModified { get; set; }
}
