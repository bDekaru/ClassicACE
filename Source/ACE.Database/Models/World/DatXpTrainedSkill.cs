using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat XP table: trained skill ranks
/// </summary>
public partial class DatXpTrainedSkill
{
    /// <summary>
    /// Rank: the number of times the trained skill has been raised; rank 0 is not raised
    /// </summary>
    public uint Rank { get; set; }

    /// <summary>
    /// Total XP spent on a trained skill to reach this rank
    /// </summary>
    public uint TotalXp { get; set; }

    public DateTime LastModified { get; set; }
}
