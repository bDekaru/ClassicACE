using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat skill table
/// </summary>
public partial class DatSkill
{
    /// <summary>
    /// Unique Id of this Skill
    /// </summary>
    public uint Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public uint IconId { get; set; }

    /// <summary>
    /// Skill credits to train
    /// </summary>
    public int TrainedCost { get; set; }

    /// <summary>
    /// Total skill credits to specialize, including the trained cost
    /// </summary>
    public int SpecializedCost { get; set; }

    /// <summary>
    /// 1 = combat, 2 = other, 3 = magic
    /// </summary>
    public uint Category { get; set; }

    public uint ChargenUse { get; set; }

    /// <summary>
    /// 1 = usable untrained, 2 = trained or specialized needed
    /// </summary>
    public uint MinLevel { get; set; }

    /// <summary>
    /// Skill formula: (attr1 * x + attr2 * y) / z + w
    /// </summary>
    public uint FormulaW { get; set; }

    public uint FormulaX { get; set; }

    public uint FormulaY { get; set; }

    public uint FormulaZ { get; set; }

    public uint FormulaAttr1 { get; set; }

    public uint FormulaAttr2 { get; set; }

    public double UpperBound { get; set; }

    public double LowerBound { get; set; }

    public double LearnMod { get; set; }

    public DateTime LastModified { get; set; }
}
