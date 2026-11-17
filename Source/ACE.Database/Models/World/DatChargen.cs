using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat character generation credits
/// </summary>
public partial class DatChargen
{
    /// <summary>
    /// Always 1: one row for the whole record
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Attribute credits of every heritage group
    /// </summary>
    public uint AttributeCredits { get; set; }

    /// <summary>
    /// Skill credits of every heritage group
    /// </summary>
    public uint SkillCredits { get; set; }

    public DateTime LastModified { get; set; }
}
