using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.World
{
    public partial class ExplorationSite
    {
        public uint Id { get; set; }
        public int Landblock { get; set; }
        public string Name { get; set; }
        public string Directions { get; set; }
        public int Level { get; set; }
        public string ContentDescription { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public int CreatureCount { get; set; }
        public DateTime LastModified { get; set; }
    }
}
