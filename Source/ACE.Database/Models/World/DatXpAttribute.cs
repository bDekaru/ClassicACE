using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat XP table: attribute ranks
/// </summary>
public partial class DatXpAttribute
{
    /// <summary>
    /// Rank: the number of times the attribute has been raised; rank 0 is not raised
    /// </summary>
    public uint Rank { get; set; }

    /// <summary>
    /// Total XP spent on an attribute to reach this rank (the cost of the next point is the difference to the previous rank)
    /// </summary>
    public uint TotalXp { get; set; }

    public DateTime LastModified { get; set; }
}
