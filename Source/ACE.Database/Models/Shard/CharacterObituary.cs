using System;

namespace ACE.Database.Models.Shard
{
    public partial class CharacterObituary
    {
        public uint Id { get; set; }
        public uint AccountId { get; set; }
        public uint CharacterId { get; set; }
        public string CharacterName{ get; set; }
        public int CharacterLevel { get; set; }
        public string KillerName { get; set; }
        public int KillerLevel { get; set; }
        public uint LandblockId { get; set; }
        public int GameplayMode { get; set; }
        public bool WasPvP { get; set; }
        public int Kills { get; set; }
        public long XP { get; set; }
        public int Age { get; set; }
        public DateTime TimeOfDeath { get; set; }
        public uint? MonarchId { get; set; }
    }
}
