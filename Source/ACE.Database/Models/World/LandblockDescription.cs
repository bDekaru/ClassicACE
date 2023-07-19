using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.World
{
    public partial class LandblockDescription
    {
        public uint Id { get; set; }
        public int Landblock { get; set; }
        public string Name { get; set; }
        public bool IsDungeon { get; set; }
        public bool HasDungeon { get; set; }
        public string Directions { get; set; }
        public string Reference { get; set; }
        public string MacroRegion{ get; set; }
        public string MicroRegion { get; set; }
        public DateTime LastModified { get; set; }
    }
}
