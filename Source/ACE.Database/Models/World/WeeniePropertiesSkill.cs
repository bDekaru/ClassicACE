using System;
using System.Collections.Generic;

#nullable disable

namespace ACE.Database.Models.World
{
    public partial class WeeniePropertiesSkill
    {
        public uint Id { get; set; }
        public uint ObjectId { get; set; }
        public ushort Type { get; set; }
        public ushort LevelFromPP { get; set; }
        public uint SAC { get; set; }
        public uint PP { get; set; }
        public uint InitLevel { get; set; }
        public uint ResistanceAtLastCheck { get; set; }
        public double LastUsedTime { get; set; }
        public ushort SecondaryTo { get; set; }

        public virtual Weenie Object { get; set; }

        public WeeniePropertiesSkill() { }

        /// <summary>
        /// Copy constructor
        /// </summary>
        public WeeniePropertiesSkill(WeeniePropertiesSkill other)
        {
            Id = other.Id;
            ObjectId = other.ObjectId;
            Type = other.Type;
            LevelFromPP = other.LevelFromPP;
            SAC = other.SAC;
            PP = other.PP;
            InitLevel = other.InitLevel;
            ResistanceAtLastCheck = other.ResistanceAtLastCheck;
            LastUsedTime = other.LastUsedTime;
            SecondaryTo = other.SecondaryTo;
            Object = other.Object;
        }
    }
}
