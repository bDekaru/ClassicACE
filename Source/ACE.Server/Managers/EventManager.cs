using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

using log4net;

namespace ACE.Server.Managers
{
    public static class EventManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static Dictionary<string, Event> Events;

        public static bool Debug = false;

        static EventManager()
        {
            Events = new Dictionary<string, Event>(StringComparer.OrdinalIgnoreCase);

            NextHotDungeonSwitch = Time.GetFutureUnixTime(HotDungeonRollDelay);
        }

        public static void Initialize()
        {
            var events = Database.DatabaseManager.World.GetAllEvents();

            foreach (var evnt in events)
            {
                Events.Add(evnt.Name, evnt);

                if (evnt.State == (int)GameEventState.On)
                    StartEvent(evnt.Name, null, null);
            }

            log.DebugFormat("EventManager Initalized.");
        }

        public static bool StartEvent(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
                return false;

            if (!Events.TryGetValue(eventName, out Event evnt))
                return false;

            var state = (GameEventState)evnt.State;

            if (state == GameEventState.Disabled)
                return false;

            if (state == GameEventState.Enabled || state == GameEventState.Off)
            {
                evnt.State = (int)GameEventState.On;

                if (Debug)
                    Console.WriteLine($"Starting event {evnt.Name}");
            }

            log.Debug($"[EVENT] {(source == null ? "SYSTEM" : $"{source.Name} (0x{source.Guid}|{source.WeenieClassId})")}{(target == null ? "" : $", triggered by {target.Name} (0x{target.Guid}|{target.WeenieClassId}),")} started an event: {evnt.Name}{((int)state == evnt.State ? (source == null ? ", which is the default state for this event." : ", which had already been started.") : "")}");

            return true;
        }

        public static bool StopEvent(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
                return false;

            if (!Events.TryGetValue(eventName, out Event evnt))
                return false;

            var state = (GameEventState)evnt.State;

            if (state == GameEventState.Disabled)
                return false;

            if (state == GameEventState.Enabled || state == GameEventState.On)
            {
                evnt.State = (int)GameEventState.Off;

                if (Debug)
                    Console.WriteLine($"Stopping event {evnt.Name}");
            }

            log.Debug($"[EVENT] {(source == null ? "SYSTEM" : $"{source.Name} (0x{source.Guid}|{source.WeenieClassId})")}{(target == null ? "" : $", triggered by {target.Name} (0x{target.Guid}|{target.WeenieClassId}),")} stopped an event: {evnt.Name}{((int)state == evnt.State ? (source == null ? ", which is the default state for this event." : ", which had already been stopped.") : "")}");

            return true;
        }

        public static bool IsEventStarted(string e, WorldObject source, WorldObject target)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
            {
                var serverPkState = PropertyManager.GetBool("pk_server").Item;

                return serverPkState;
            }

            if (!Events.TryGetValue(eventName, out Event evnt))
                return false;

            if (evnt.State != (int)GameEventState.Disabled && (evnt.StartTime != -1 || evnt.EndTime != -1))
            {
                var prevState = (GameEventState)evnt.State;

                var now = (int)Time.GetUnixTime();

                var start = (now > evnt.StartTime) && (evnt.StartTime > -1);
                var end = (now > evnt.EndTime) && (evnt.EndTime > -1);

                if (prevState == GameEventState.On && end)
                    return !StopEvent(evnt.Name, source, target);
                else if ((prevState == GameEventState.Off || prevState == GameEventState.Enabled) && start && !end)
                    return StartEvent(evnt.Name, source, target);
            }

            return evnt.State == (int)GameEventState.On;
        }

        public static bool IsEventEnabled(string e)
        {
            var eventName = GetEventName(e);

            if (!Events.TryGetValue(eventName, out Event evnt))
                return false;

            return evnt.State != (int)GameEventState.Disabled;
        }

        public static bool IsEventAvailable(string e)
        {
            var eventName = GetEventName(e);

            return Events.ContainsKey(eventName);
        }

        public static GameEventState GetEventStatus(string e)
        {
            var eventName = GetEventName(e);

            if (eventName.Equals("EventIsPKWorld", StringComparison.OrdinalIgnoreCase)) // special event
            {
                if (PropertyManager.GetBool("pk_server").Item)
                    return GameEventState.On;
                else
                    return GameEventState.Off;
            }

            if (!Events.TryGetValue(eventName, out Event evnt))
                return GameEventState.Undef;

            return (GameEventState)evnt.State;
        }

        /// <summary>
        /// Returns the event name without the @ comment
        /// </summary>
        /// <param name="eventFormat">A event name with an optional @comment on the end</param>
        public static string GetEventName(string eventFormat)
        {
            var idx = eventFormat.IndexOf('@');     // strip comment
            if (idx == -1)
                return eventFormat;

            var eventName = eventFormat.Substring(0, idx);
            return eventName;
        }

        private static double NextEventManagerShortHeartbeat = 0;
        private static double NextEventManagerLongHeartbeat = 0;
        private static double EventManagerHeartbeatShortInterval = 10;
        private static double EventManagerHeartbeatLongInterval = 300;
        public static void Tick()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            double currentUnixTime = Time.GetUnixTime();
            if (NextEventManagerShortHeartbeat > currentUnixTime)
                return;
            NextEventManagerShortHeartbeat = Time.GetFutureUnixTime(EventManagerHeartbeatShortInterval);

            HotDungeonTick(currentUnixTime);

            if (NextEventManagerLongHeartbeat > currentUnixTime)
                return;
            NextEventManagerLongHeartbeat = Time.GetFutureUnixTime(EventManagerHeartbeatLongInterval);

            var smugglersDen = GetEventStatus("smugglersden");
            if (smugglersDen == GameEventState.Off && PlayerManager.GetOnlinePKCount() >= 5)
                StartEvent("smugglersden", null, null);
            else if (smugglersDen == GameEventState.On && PlayerManager.GetOnlinePKCount() < 5)
                StopEvent("smugglersden", null, null);
        }

        public static int HotDungeonLandblock = 0;
        public static string HotDungeonName = "";
        public static string HotDungeonDescription = "";
        public static double NextHotDungeonSwitch = 0;

        private static double HotDungeonInterval = 7201;
        private static double HotDungeonRollDelay = 1200;
        private static double HotDungeonChance = 0.33;
        public static void HotDungeonTick(double currentUnixTime)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (NextHotDungeonSwitch > currentUnixTime)
                return;

            if (HotDungeonLandblock != 0)
            {
                var msg = $"{HotDungeonName} is no longer giving extra experience rewards.";
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
            }

            HotDungeonLandblock = 0;
            HotDungeonName = "";
            HotDungeonDescription = "";

            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (roll > HotDungeonChance)
            {
                // No hot dungeons for now!
                NextHotDungeonSwitch = Time.GetFutureUnixTime(HotDungeonRollDelay);
                return;
            }

            RollHotDungeon();
        }

        public static void ProlongHotDungeon()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextHotDungeonSwitch = Time.GetFutureUnixTime(HotDungeonInterval);

            var msg = $"The current extra experience dungeon duration has been prolonged!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
        }

        public static void RollHotDungeon(ushort forceLandblock = 0)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextHotDungeonSwitch = Time.GetFutureUnixTime(HotDungeonInterval);

            var onlinePlayers = PlayerManager.GetAllOnline();

            if (onlinePlayers.Count > 0 || forceLandblock != 0)
            {
                var averageLevel = 0;
                var godCharactersCount = 0;
                foreach (var player in onlinePlayers)
                {
                    if (player.GodState == null)
                        averageLevel += player.Level ?? 1;
                    else
                        godCharactersCount++;
                }
                var onlineMinusGods = onlinePlayers.Count - godCharactersCount;

                if (onlineMinusGods > 0 || forceLandblock != 0)
                {
                    List<ExplorationSite> possibleDungeonList;

                    if (forceLandblock == 0)
                    {
                        averageLevel /= onlineMinusGods;

                        var minLevel = Math.Max(averageLevel - (int)(averageLevel * 0.1f), 1);
                        var maxLevel = averageLevel + (int)(averageLevel * 0.2f);
                        if (averageLevel > 100)
                            maxLevel = int.MaxValue;
                        possibleDungeonList = DatabaseManager.World.GetExplorationSitesByLevelRange(minLevel, maxLevel, averageLevel);
                    }
                    else
                        possibleDungeonList = DatabaseManager.World.GetExplorationSitesByLandblock(forceLandblock);

                    if (possibleDungeonList.Count != 0)
                    {
                        var dungeon = possibleDungeonList[ThreadSafeRandom.Next(0, possibleDungeonList.Count - 1)];

                        string dungeonName;
                        string dungeonDirections;
                        var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)dungeon.Landblock).FirstOrDefault();
                        if (entryLandblock != null)
                        {
                            dungeonName = entryLandblock.Name;
                            dungeonDirections = entryLandblock.Directions;
                        }
                        else
                        {
                            dungeonName = $"unknown location({dungeon.Landblock})";
                            dungeonDirections = "at an unknown location";
                        }

                        HotDungeonLandblock = dungeon.Landblock;
                        HotDungeonName = dungeonName;

                        var dungeonLevel = Math.Clamp(dungeon.Level, dungeon.MinLevel, dungeon.MaxLevel != 0 ? dungeon.MaxLevel : int.MaxValue);
                        HotDungeonDescription = $"Extra experience rewards dungeon: {dungeonName} located {dungeonDirections}. Dungeon level: {dungeonLevel:N0}.";

                        var timeRemaining = TimeSpan.FromSeconds(NextHotDungeonSwitch - Time.GetUnixTime()).GetFriendlyString();

                        var msg = $"{dungeonName} will be giving extra experience rewards for the next {timeRemaining}! The dungeon level is {dungeonLevel:N0}. The entrance is located {dungeonDirections}!";
                        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

                        return;
                    }
                }
            }

            NextHotDungeonSwitch = Time.GetFutureUnixTime(HotDungeonRollDelay); // We failed to select a new hot dungeon, reschedule it.
        }
    }
}
