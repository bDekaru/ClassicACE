using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat character generation template skills
/// </summary>
public partial class DatChargenTemplateSkill
{
    /// <summary>
    /// HeritageGroup
    /// </summary>
    public uint HeritageGroup { get; set; }

    /// <summary>
    /// dat_chargen_template.id
    /// </summary>
    public uint TemplateId { get; set; }

    /// <summary>
    /// Skill
    /// </summary>
    public uint SkillId { get; set; }

    /// <summary>
    /// SkillAdvancementClass: 2 Trained or 3 Specialized
    /// </summary>
    public uint AdvancementClass { get; set; }

    public DateTime LastModified { get; set; }
}
