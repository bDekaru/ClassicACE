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

            NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonRollDelay);
            NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownRollDelay);
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
            FireSaleTick(currentUnixTime);

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
        public static double NextHotDungeonRoll = 0;
        public static double NextHotDungeonEnd = 0;

        private static double HotDungeonInterval = 7800;
        private static double HotDungeonDuration = 7200;
        private static double HotDungeonRollDelay = 1200;
        private static double HotDungeonChance = 0.33;
        public static void HotDungeonTick(double currentUnixTime)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (NextHotDungeonEnd != 0 && NextHotDungeonEnd > currentUnixTime)
                return;

            NextHotDungeonEnd = 0;
            if (HotDungeonLandblock != 0)
            {
                var msg = $"{HotDungeonName} is no longer giving extra experience rewards.";
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
            }

            HotDungeonLandblock = 0;
            HotDungeonName = "";
            HotDungeonDescription = "";

            if (NextHotDungeonRoll > currentUnixTime)
                return;

            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (roll > HotDungeonChance)
            {
                // No hot dungeons for now!
                NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonRollDelay);
                return;
            }

            RollHotDungeon();
        }

        public static void ProlongHotDungeon()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextHotDungeonEnd = Time.GetFutureUnixTime(HotDungeonDuration);
            NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonInterval);

            var msg = $"The current extra experience dungeon duration has been prolonged!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
        }

        public static void RollHotDungeon(ushort forceLandblock = 0)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonInterval);

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

                        NextHotDungeonEnd = Time.GetFutureUnixTime(HotDungeonDuration);
                        NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonInterval);

                        var timeRemaining = TimeSpan.FromSeconds(HotDungeonDuration).GetFriendlyString();

                        var msg = $"{dungeonName} will be giving extra experience rewards for the next {timeRemaining}! The dungeon level is {dungeonLevel:N0}. The entrance is located {dungeonDirections}!";
                        PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                        PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);

                        return;
                    }
                }
            }

            NextHotDungeonRoll = Time.GetFutureUnixTime(HotDungeonRollDelay); // We failed to select a new hot dungeon, reschedule it.
        }

        public static List<string> PossibleFireSaleTowns = new List<string>()
        {
            "Arwic",
            "Eastham",
            "Rithwic",
            "Cragstone",
            "Holtburg",
            "Glenden Wood",
            "Mayoi",
            "Yanshi",
            "Shoushi",
            "Hebian-to",
            "Underground City",
            "Samsur",
            "Zaikhal",
            "Yaraq",
            "Qalaba'r",
            "Tufa",
            "Xarabydun",
            "Uziz",
            "Dryreach",
            "Baishi",
            "Sawato",
            "Fort Tethana",
            "Crater Lake",
            "Plateau",
            "Stonehold",
            "Kara",
            "Wai Jhou",
            "Lytelthorpe",
            "Lin",
            "Nanto",
            "Tou-Tou",
            "Al-Arqas",
            "Al-Jalima",
            "Khayyaban",
            "Neydisa Castle",
            "Ayan Baqur",
            "Kryst",
            "MacNiall's Freehold",
            "Linvak Tukal",
            "Danby's Outpost",
            "Ahurenga",
            "Bluespire",
            "Greenspire",
            "Redspire",
            "Timaru",
            "Qalabar",
            "Candeth Keep",
            "Bandit Castle"
        };

        public static string FireSaleTownName = "";
        public static string FireSaleTownDescription = "";
        public static double NextFireSaleTownRoll = 0;
        public static double NextFireSaleTownEnd = 0;

        private static double FireSaleTownInterval = 14400;
        private static double FireSaleTownRollDelay = 1800;
        private static double FireSaleTownDuration = 1200;
        private static double FireSaleTownChance = 0.20;
        public static double FireSaleSellPrice = 1.25;
        public static int FireSaleItemStockAmountMultiplier = 3;

        public static void FireSaleTick(double currentUnixTime)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (NextFireSaleTownEnd != 0 && NextFireSaleTownEnd > currentUnixTime)
                return;

            NextFireSaleTownEnd = 0;
            if (FireSaleTownName != "")
            {
                var msg = $"{FireSaleTownName} is no longer having a fire sale.";
                PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
            }

            FireSaleTownName = "";
            FireSaleTownDescription = "";

            if (NextFireSaleTownRoll > currentUnixTime)
                return;

            var roll = ThreadSafeRandom.Next(0.0f, 1.0f);
            if (roll > FireSaleTownChance)
            {
                // No fire sales for now!
                NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownRollDelay);
                return;
            }

            RollFireSaleTown();
        }

        public static void RollFireSaleTown(string forceTown = "")
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownInterval);

            var onlinePlayers = PlayerManager.GetAllOnline();

            if (onlinePlayers.Count > 0 || forceTown != "")
            {
                if (forceTown == "")
                    FireSaleTownName = PossibleFireSaleTowns[ThreadSafeRandom.Next(0, PossibleFireSaleTowns.Count - 1)];
                else if (PossibleFireSaleTowns.Contains(forceTown))
                    FireSaleTownName = forceTown;
                else
                    return;

                if (FireSaleTownName != "")
                {
                    FireSaleTownDescription = $"Current Fire Sale Town: {FireSaleTownName}.";

                    NextFireSaleTownEnd = Time.GetFutureUnixTime(FireSaleTownDuration);
                    NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownInterval);

                    var timeRemaining = TimeSpan.FromSeconds(FireSaleTownDuration).GetFriendlyString();

                    var msg = $"{FireSaleTownName} will be holding a fire sale for the next {timeRemaining}!";
                    PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                    PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
                    return;
                }
            }

            NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownRollDelay); // We failed to select a new fire sale town, reschedule it.
        }

        public static void ProlongFireSaleTown()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            NextFireSaleTownEnd = Time.GetFutureUnixTime(FireSaleTownDuration);
            NextFireSaleTownRoll = Time.GetFutureUnixTime(FireSaleTownInterval);

            var msg = $"The current fire sale duration has been prolonged!";
            PlayerManager.BroadcastToAll(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            PlayerManager.LogBroadcastChat(Channel.AllBroadcast, null, msg);
        }
    }
}
