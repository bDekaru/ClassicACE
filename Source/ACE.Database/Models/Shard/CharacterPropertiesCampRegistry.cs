using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.Shard
{
    public partial class CharacterPropertiesCampRegistry
    {
        public uint CharacterId { get; set; }
        public uint CampId { get; set; }
        public uint NumInteractions { get; set; }
        public uint LastDecayTime { get; set; }

        public virtual Character Character { get; set; }
    }
}
