using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat XP table: character levels
/// </summary>
public partial class DatXpLevel
{
    /// <summary>
    /// Character level
    /// </summary>
    public uint Level { get; set; }

    /// <summary>
    /// Total XP needed to reach this level
    /// </summary>
    public ulong TotalXp { get; set; }

    /// <summary>
    /// Skill credits granted at this level
    /// </summary>
    public uint SkillCredits { get; set; }

    public DateTime LastModified { get; set; }
}
