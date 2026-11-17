using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Portal dat character generation templates
/// </summary>
public partial class DatChargenTemplate
{
    /// <summary>
    /// HeritageGroup
    /// </summary>
    public uint HeritageGroup { get; set; }

    /// <summary>
    /// Template index of the heritage group: 0, 1, 2, ... without gaps
    /// </summary>
    public uint Id { get; set; }

    public string Name { get; set; }

    public uint IconId { get; set; }

    /// <summary>
    /// CharacterTitle
    /// </summary>
    public uint Title { get; set; }

    public uint Strength { get; set; }

    public uint Endurance { get; set; }

    public uint Coordination { get; set; }

    public uint Quickness { get; set; }

    public uint Focus { get; set; }

    public uint Self { get; set; }

    public DateTime LastModified { get; set; }
}
