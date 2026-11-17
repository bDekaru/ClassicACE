using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat vital table
/// </summary>
public partial class DatVital
{
    /// <summary>
    /// PropertyAttribute2nd: MaxHealth, MaxStamina or MaxMana
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Vital formula: (attr1 * x + attr2 * y) / z + w
    /// </summary>
    public uint FormulaW { get; set; }

    public uint FormulaX { get; set; }

    public uint FormulaY { get; set; }

    public uint FormulaZ { get; set; }

    /// <summary>
    /// PropertyAttribute
    /// </summary>
    public uint FormulaAttr1 { get; set; }

    /// <summary>
    /// PropertyAttribute
    /// </summary>
    public uint FormulaAttr2 { get; set; }

    public DateTime LastModified { get; set; }
}
