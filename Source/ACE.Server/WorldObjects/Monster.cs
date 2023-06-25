using ACE.DatLoader;
using ACE.Entity.Enum;
using System.Collections.Generic;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    /// <summary>
    /// Monster AI functions
    /// </summary>
    partial class Creature
    {
        public bool IsMonster { get; set; }

        public bool IsChessPiece { get; set; }

        public bool IsPassivePet { get; set; }

        public bool IsFactionMob { get; set; }

        public bool HasFoeType { get; set; }

        /// <summary>
        /// The exclusive state of the monster
        /// </summary>
        public State MonsterState = State.Idle;

        /// <summary>
        /// The exclusive states the monster can be in
        /// </summary>
        public enum State
        {
            Idle,
            Awake,
            Return
        };

        /// <summary>
        /// Determines if this creature runs combat ai,
        /// and caches into IsMonster
        /// </summary>
        public void SetMonsterState()
        {
            if (this is Player) return;

            IsPassivePet = WeenieType == WeenieType.Pet;
            IsChessPiece = WeenieType == WeenieType.GamePiece;

            // includes CombatPets
            IsMonster = Attackable || TargetingTactic != TargetingTactic.None;

            IsFactionMob = IsMonster && WeenieType != WeenieType.CombatPet && Faction1Bits != null;

            HasFoeType = IsMonster && FoeType != null;
        }

        List<MotionCommand> IdleMotionsList = null;
        public void BuildIdleMotionsList()
        {
            IdleMotionsList = new List<MotionCommand>();

            if (MotionTableId != 0)
            {
                var motionTable = DatManager.PortalDat.ReadFromDat<DatLoader.FileTypes.MotionTable>(MotionTableId);
                if (motionTable != null && Biota != null && Biota.PropertiesEmote != null)
                {
                    var heartbeatEmotes = Biota.PropertiesEmote.Where(e => e.Category == EmoteCategory.HeartBeat);
                    foreach (var emote in heartbeatEmotes)
                    {
                        foreach (var emoteAction in emote.PropertiesEmoteAction)
                        {
                            if (emoteAction.Type == 5 && emoteAction.Motion != null)
                            {
                                MotionCommand motion = (MotionCommand)emoteAction.Motion;
                                if (emoteAction.Type == (int)EmoteType.Motion && !IdleMotionsList.Contains(motion))
                                    IdleMotionsList.Add(motion);
                            }
                        }
                    }
                }
            }
        }
    }
}
