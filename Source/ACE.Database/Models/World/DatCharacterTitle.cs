using System;
using System.Collections.Generic;

namespace ACE.Database.Models.World;

/// <summary>
/// Character titles of the portal and language dats
/// </summary>
public partial class DatCharacterTitle
{
    /// <summary>
    /// Title id as sent to the client (the CharacterTitle enum value)
    /// </summary>
    public uint Id { get; set; }

    /// <summary>
    /// Name in the title enum mapper, e.g. ID_CharacterTitle_Adventurer
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Display text
    /// </summary>
    public string Text { get; set; }

    public DateTime LastModified { get; set; }
}
