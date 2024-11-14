using System;
using ACE.Server.Network.Structure;
using ACE.Server.Entity;
using ACE.Server.WorldObjects;
using ACE.Entity.Enum;
using ACE.Entity;
using ACE.Server.Network.Enum;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventAllegianceUpdate : GameEventMessage
    {
        /// <summary>
        /// Returns info related to a player's monarch, patron, and vassals.
        /// </summary>
        public GameEventAllegianceUpdate(Session session, Allegiance allegiance, AllegianceNode node)
            : base(GameEventType.AllegianceUpdate, GameMessageGroup.UIQueue, session, 512) // 398 is the average seen in retail pcaps, 1,040 is the max seen in retail pcaps
        {
            var startPos = Writer.BaseStream.Position;

            var player = session != null ? session.Player : null;
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM || player == null)
            {
                // uint - rank - this player's rank within their allegiance
                // AllegianceProfile - prof
                uint rank = (node == null) ? 0 : node.Rank;
                //Console.WriteLine("Rank: " + rank);
                Writer.Write(rank);

                var prof = new AllegianceProfile(allegiance, node);
                Writer.Write(prof);

                var endPos = Writer.BaseStream.Position;

                var totalBytes = endPos - startPos;
                //Console.WriteLine("Allegiance bytes written: " + totalBytes);
            }
            else
            {
                // We need to inject the correct allegiance rank into the packet.
                if (node == null)
                {
                    // We do not have an allegiance, fake entire packet.
                    Writer.Write((uint)player.AllegianceRank); //rank

                    Writer.Write((uint)0); //totalMembers
                    Writer.Write((uint)0); //totalVassals

                    Writer.Write((ushort)1); //recordCount
                    Writer.Write((ushort)0x000B); //oldVersion
                    PackableHashTable.WriteHeader(Writer, 0, 256); //officers
                    Writer.Write(0); //officerTitles
                    Writer.Write((uint)0); //monarchBroadcastTime
                    Writer.Write((uint)0); //monarchBroadcastsToday
                    Writer.Write((uint)0); //spokesBroadcastTime
                    Writer.Write((uint)0); //spokesBroadcastsToday
                    Writer.WriteString16L(""); //motd
                    Writer.WriteString16L(""); //motdSetBy
                    Writer.Write((uint)0); //chatRoomID
                    Writer.Write(new Position()); //bindPoint
                    Writer.WriteString16L(""); //allegianceName
                    Writer.Write((uint)0); //nameLastSetTime
                    Writer.Write((uint)0); //isLocked
                    Writer.Write(0); //approvedVassal

                    Writer.Write(player.Guid.Full); //characterID
                    Writer.Write((uint)0); //cpCached
                    Writer.Write((uint)0); //cpTithed
                    Writer.Write((uint)(AllegianceIndex.HasAllegianceAge | AllegianceIndex.HasPackedLevel | AllegianceIndex.LoggedIn)); //bitfield
                    Writer.Write((byte)(Gender)player.Gender); //gender
                    Writer.Write((byte)(HeritageGroup)player.Heritage); //hg
                    Writer.Write((ushort)player.AllegianceRank); //rank
                    Writer.Write((ushort)player.Level); //level
                    Writer.Write((ushort)player.GetCurrentLoyalty()); //loyalty
                    Writer.Write((ushort)player.GetCurrentLeadership()); //leadership
                    Writer.Write((uint)0); //timeOnline
                    Writer.Write((uint)0); //allegianceAge
                    Writer.WriteString16L(player.Name); //name
                }
                else
                {
                    // We do have an allegiance, override the allegiance rank.
                    node.Rank = (uint)player.AllegianceRank;
                    Writer.Write(node.Rank);

                    var prof = new AllegianceProfile(allegiance, node);
                    Writer.Write(prof);
                }

                var endPos = Writer.BaseStream.Position;

                var totalBytes = endPos - startPos;
                //Console.WriteLine("Allegiance bytes written: " + totalBytes);
            }
        }
    }
}
