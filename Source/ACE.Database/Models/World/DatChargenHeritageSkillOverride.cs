using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat character generation skill costs of a heritage group
/// </summary>
public partial class DatChargenHeritageSkillOverride
{
    /// <summary>
    /// HeritageGroup
    /// </summary>
    public uint HeritageGroup { get; set; }

    /// <summary>
    /// Skill
    /// </summary>
    public uint SkillId { get; set; }

    /// <summary>
    /// Credits to train the skill, instead of dat_skill.trained_Cost
    /// </summary>
    public int TrainedCost { get; set; }

    /// <summary>
    /// Credits to specialize the skill, trained cost included (as in dat_skill)
    /// </summary>
    public int SpecializedCost { get; set; }

    public DateTime LastModified { get; set; }
}
