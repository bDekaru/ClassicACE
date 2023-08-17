using System;
using System.Collections.Generic;

using log4net;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;
using System.Linq;
using System.Text;
using ACE.Entity.Enum.Properties;
using ACE.Database.Models.Shard;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Models;

namespace ACE.Server.Command.Handlers
{
    public static class PlayerCommands
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // pop
        [CommandHandler("pop", AccessLevel.Player, CommandHandlerFlag.None, 0,
            "Show current world population",
            "")]
        public static void HandlePop(Session session, params string[] parameters)
        {
            CommandHandlerHelper.WriteOutputInfo(session, $"Current world population: {PlayerManager.GetOnlineCount():N0}", ChatMessageType.Broadcast);
        }

        // quest info (uses GDLe formatting to match plugin expectations)
        [CommandHandler("myquests", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows your quest log")]
        public static void HandleQuests(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("quest_info_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"myquests\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var quests = session.Player.QuestManager.GetQuests();

            if (quests.Count == 0)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Quest list is empty.", ChatMessageType.Broadcast));
                return;
            }

            foreach (var playerQuest in quests)
            {
                var text = "";
                var questName = QuestManager.GetQuestName(playerQuest.QuestName);
                var quest = DatabaseManager.World.GetCachedQuest(questName);
                if (quest == null)
                {
                    Console.WriteLine($"Couldn't find quest {playerQuest.QuestName}");
                    continue;
                }

                var minDelta = quest.MinDelta;
                if (QuestManager.CanScaleQuestMinDelta(quest))
                {
                    minDelta = (uint)(quest.MinDelta * PropertyManager.GetDouble("quest_mindelta_rate").Item);

                    if (minDelta != quest.MinDelta)
                        minDelta = Math.Max(minDelta, (uint)PropertyManager.GetLong("quest_mindelta_rate_shortest", 0).Item);
                }

                text += $"{playerQuest.QuestName.ToLower()} - {playerQuest.NumTimesCompleted} solves ({playerQuest.LastTimeCompleted})";
                text += $"\"{quest.Message}\" {quest.MaxSolves} {minDelta}";

                session.Network.EnqueueSend(new GameMessageSystemChat(text, ChatMessageType.Broadcast));
            }
        }

        /// <summary>
        /// For characters/accounts who currently own multiple houses, used to select which house they want to keep
        /// </summary>
        [CommandHandler("house-select", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "For characters/accounts who currently own multiple houses, used to select which house they want to keep")]
        public static void HandleHouseSelect(Session session, params string[] parameters)
        {
            HandleHouseSelect(session, false, parameters);
        }

        public static void HandleHouseSelect(Session session, bool confirmed, params string[] parameters)
        {
            if (!int.TryParse(parameters[0], out var houseIdx))
                return;

            // ensure current multihouse owner
            if (!session.Player.IsMultiHouseOwner(false))
            {
                log.Warn($"{session.Player.Name} tried to /house-select {houseIdx}, but they are not currently a multi-house owner!");
                return;
            }

            // get house info for this index
            var multihouses = session.Player.GetMultiHouses();

            if (houseIdx < 1 || houseIdx > multihouses.Count)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Please enter a number between 1 and {multihouses.Count}.", ChatMessageType.Broadcast));
                return;
            }

            var keepHouse = multihouses[houseIdx - 1];

            // show confirmation popup
            if (!confirmed)
            {
                var houseType = $"{keepHouse.HouseType}".ToLower();
                var loc = HouseManager.GetCoords(keepHouse.SlumLord.Location);

                var msg = $"Are you sure you want to keep the {houseType} at\n{loc}?";
                if (!session.Player.ConfirmationManager.EnqueueSend(new Confirmation_Custom(session.Player.Guid, () => HandleHouseSelect(session, true, parameters)), msg))
                    session.Player.SendWeenieError(WeenieError.ConfirmationInProgress);
                return;
            }

            // house to keep confirmed, abandon the other houses
            var abandonHouses = new List<House>(multihouses);
            abandonHouses.RemoveAt(houseIdx - 1);

            foreach (var abandonHouse in abandonHouses)
            {
                var house = session.Player.GetHouse(abandonHouse.Guid.Full);

                HouseManager.HandleEviction(house, house.HouseOwner ?? 0, true);
            }

            // set player properties for house to keep
            var player = PlayerManager.FindByGuid(keepHouse.HouseOwner ?? 0, out bool isOnline);
            if (player == null)
            {
                log.Error($"{session.Player.Name}.HandleHouseSelect({houseIdx}) - couldn't find HouseOwner {keepHouse.HouseOwner} for {keepHouse.Name} ({keepHouse.Guid})");
                return;
            }

            player.HouseId = keepHouse.HouseId;
            player.HouseInstance = keepHouse.Guid.Full;

            player.SaveBiotaToDatabase();

            // update house panel for current player
            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(3.0f);  // wait for slumlord inventory biotas above to save
            actionChain.AddAction(session.Player, session.Player.HandleActionQueryHouse);
            actionChain.EnqueueChain();

            Console.WriteLine("OK");
        }

        [CommandHandler("debugcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows debug information about the current magic casting state")]
        public static void HandleDebugCast(Session session, params string[] parameters)
        {
            var physicsObj = session.Player.PhysicsObj;

            var pendingActions = physicsObj.MovementManager.MoveToManager.PendingActions;
            var currAnim = physicsObj.PartArray.Sequence.CurrAnim;

            session.Network.EnqueueSend(new GameMessageSystemChat(session.Player.MagicState.ToString(), ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"IsMovingOrAnimating: {physicsObj.IsMovingOrAnimating}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"PendingActions: {pendingActions.Count}", ChatMessageType.Broadcast));
            session.Network.EnqueueSend(new GameMessageSystemChat($"CurrAnim: {currAnim?.Value.Anim.ID:X8}", ChatMessageType.Broadcast));
        }

        [CommandHandler("fixcast", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Fixes magic casting if locked up for an extended time")]
        public static void HandleFixCast(Session session, params string[] parameters)
        {
            var magicState = session.Player.MagicState;

            if (magicState.IsCasting && DateTime.UtcNow - magicState.StartTime > TimeSpan.FromSeconds(5))
            {
                session.Network.EnqueueSend(new GameEventCommunicationTransientString(session, "Fixed casting state"));
                session.Player.SendUseDoneEvent();
                magicState.OnCastDone();
            }
        }

        [CommandHandler("castmeter", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows the fast casting efficiency meter")]
        public static void HandleCastMeter(Session session, params string[] parameters)
        {
            if (parameters.Length == 0)
            {
                session.Player.MagicState.CastMeter = !session.Player.MagicState.CastMeter;
            }
            else
            {
                if (parameters[0].Equals("on", StringComparison.OrdinalIgnoreCase))
                    session.Player.MagicState.CastMeter = true;
                else
                    session.Player.MagicState.CastMeter = false;
            }
            session.Network.EnqueueSend(new GameMessageSystemChat($"Cast efficiency meter {(session.Player.MagicState.CastMeter ? "enabled" : "disabled")}", ChatMessageType.Broadcast));
        }

        private static List<string> configList = new List<string>()
        {
            "Common settings:\nConfirmVolatileRareUse, MainPackPreferred, SalvageMultiple, SideBySideVitals, UseCraftSuccessDialog",
            "Interaction settings:\nAcceptLootPermits, AllowGive, AppearOffline, AutoAcceptFellowRequest, DragItemOnPlayerOpensSecureTrade, FellowshipShareLoot, FellowshipShareXP, IgnoreAllegianceRequests, IgnoreFellowshipRequests, IgnoreTradeRequests, UseDeception",
            "UI settings:\nCoordinatesOnRadar, DisableDistanceFog, DisableHouseRestrictionEffects, DisableMostWeatherEffects, FilterLanguage, LockUI, PersistentAtDay, ShowCloak, ShowHelm, ShowTooltips, SpellDuration, TimeStamp, ToggleRun, UseMouseTurning",
            "Chat settings:\nHearAllegianceChat, HearGeneralChat, HearLFGChat, HearRoleplayChat, HearSocietyChat, HearTradeChat, HearPKDeaths, StayInChatMode",
            "Combat settings:\nAdvancedCombatUI, AutoRepeatAttack, AutoTarget, LeadMissileTargets, UseChargeAttack, UseFastMissiles, ViewCombatTarget, VividTargetingIndicator",
            "Character display settings:\nDisplayAge, DisplayAllegianceLogonNotifications, DisplayChessRank, DisplayDateOfBirth, DisplayFishingSkill, DisplayNumberCharacterTitles, DisplayNumberDeaths"
        };

        /// <summary>
        /// Mapping of GDLE -> ACE CharacterOptions
        /// </summary>
        private static Dictionary<string, string> translateOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Common
            { "ConfirmVolatileRareUse", "ConfirmUseOfRareGems" },
            { "MainPackPreferred", "UseMainPackAsDefaultForPickingUpItems" },
            { "SalvageMultiple", "SalvageMultipleMaterialsAtOnce" },
            { "SideBySideVitals", "SideBySideVitals" },
            { "UseCraftSuccessDialog", "UseCraftingChanceOfSuccessDialog" },

            // Interaction
            { "AcceptLootPermits", "AcceptCorpseLootingPermissions" },
            { "AllowGive", "LetOtherPlayersGiveYouItems" },
            { "AppearOffline", "AppearOffline" },
            { "AutoAcceptFellowRequest", "AutomaticallyAcceptFellowshipRequests" },
            { "DragItemOnPlayerOpensSecureTrade", "DragItemToPlayerOpensTrade" },
            { "FellowshipShareLoot", "ShareFellowshipLoot" },
            { "FellowshipShareXP", "ShareFellowshipExpAndLuminance" },
            { "IgnoreAllegianceRequests", "IgnoreAllegianceRequests" },
            { "IgnoreFellowshipRequests", "IgnoreFellowshipRequests" },
            { "IgnoreTradeRequests", "IgnoreAllTradeRequests" },
            { "UseDeception", "AttemptToDeceiveOtherPlayers" },

            // UI
            { "CoordinatesOnRadar", "ShowCoordinatesByTheRadar" },
            { "DisableDistanceFog", "DisableDistanceFog" },
            { "DisableHouseRestrictionEffects", "DisableHouseRestrictionEffects" },
            { "DisableMostWeatherEffects", "DisableMostWeatherEffects" },
            { "FilterLanguage", "FilterLanguage" },
            { "LockUI", "LockUI" },
            { "PersistentAtDay", "AlwaysDaylightOutdoors" },
            { "ShowCloak", "ShowYourCloak" },
            { "ShowHelm", "ShowYourHelmOrHeadGear" },
            { "ShowTooltips", "Display3dTooltips" },
            { "SpellDuration", "DisplaySpellDurations" },
            { "TimeStamp", "DisplayTimestamps" },
            { "ToggleRun", "RunAsDefaultMovement" },
            { "UseMouseTurning", "UseMouseTurning" },

            // Chat
            { "HearAllegianceChat", "ListenToAllegianceChat" },
            { "HearGeneralChat", "ListenToGeneralChat" },
            { "HearLFGChat", "ListenToLFGChat" },
            { "HearRoleplayChat", "ListentoRoleplayChat" },
            { "HearSocietyChat", "ListenToSocietyChat" },
            { "HearTradeChat", "ListenToTradeChat" },
            { "HearPKDeaths", "ListenToPKDeathMessages" },
            { "StayInChatMode", "StayInChatModeAfterSendingMessage" },

            // Combat
            { "AdvancedCombatUI", "AdvancedCombatInterface" },
            { "AutoRepeatAttack", "AutoRepeatAttacks" },
            { "AutoTarget", "AutoTarget" },
            { "LeadMissileTargets", "LeadMissileTargets" },
            { "UseChargeAttack", "UseChargeAttack" },
            { "UseFastMissiles", "UseFastMissiles" },
            { "ViewCombatTarget", "KeepCombatTargetsInView" },
            { "VividTargetingIndicator", "VividTargetingIndicator" },

            // Character Display
            { "DisplayAge", "AllowOthersToSeeYourAge" },
            { "DisplayAllegianceLogonNotifications", "ShowAllegianceLogons" },
            { "DisplayChessRank", "AllowOthersToSeeYourChessRank" },
            { "DisplayDateOfBirth", "AllowOthersToSeeYourDateOfBirth" },
            { "DisplayFishingSkill", "AllowOthersToSeeYourFishingSkill" },
            { "DisplayNumberCharacterTitles", "AllowOthersToSeeYourNumberOfTitles" },
            { "DisplayNumberDeaths", "AllowOthersToSeeYourNumberOfDeaths" },
        };

        /// <summary>
        /// Manually sets a character option on the server. Use /config list to see a list of settings.
        /// </summary>
        [CommandHandler("config", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Manually sets a character option on the server.\nUse /config list to see a list of settings.", "<setting> <on/off>")]
        public static void HandleConfig(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("player_config_command").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"config\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            // /config list - show character options
            if (parameters[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in configList)
                    session.Network.EnqueueSend(new GameMessageSystemChat(line, ChatMessageType.Broadcast));

                return;
            }

            // translate GDLE CharacterOptions for existing plugins
            if (!translateOptions.TryGetValue(parameters[0], out var param) || !Enum.TryParse(param, out CharacterOption characterOption))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown character option: {parameters[0]}", ChatMessageType.Broadcast));
                return;
            }

            var option = session.Player.GetCharacterOption(characterOption);

            // modes of operation:
            // on / off / toggle

            // - if none specified, default to toggle
            var mode = "toggle";

            if (parameters.Length > 1)
            {
                if (parameters[1].Equals("on", StringComparison.OrdinalIgnoreCase))
                    mode = "on";
                else if (parameters[1].Equals("off", StringComparison.OrdinalIgnoreCase))
                    mode = "off";
            }

            // set character option
            if (mode.Equals("on"))
                option = true;
            else if (mode.Equals("off"))
                option = false;
            else
                option = !option;

            session.Player.SetCharacterOption(characterOption, option);

            session.Network.EnqueueSend(new GameMessageSystemChat($"Character option {parameters[0]} is now {(option ? "on" : "off")}.", ChatMessageType.Broadcast));

            // update client
            session.Network.EnqueueSend(new GameEventPlayerDescription(session));
        }

        /// <summary>
        /// Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs.
        /// Can only be used once every 5 mins max.
        /// </summary>
        [CommandHandler("objsend", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Force resend of all visible objects known to this player. Can fix rare cases of invisible object bugs. Can only be used once every 5 mins max.")]
        public static void HandleObjSend(Session session, params string[] parameters)
        {
            // a good repro spot for this is the first room after the door in facility hub
            // in the portal drop / staircase room, the VisibleCells do not have the room after the door
            // however, the room after the door *does* have the portal drop / staircase room in its VisibleCells (the inverse relationship is imbalanced)
            // not sure how to fix this atm, seems like it triggers a client bug..

            if (DateTime.UtcNow - session.Player.PrevObjSend < TimeSpan.FromMinutes(5))
            {
                session.Player.SendTransientError("You have used this command too recently!");
                return;
            }

            var creaturesOnly = parameters.Length > 0 && parameters[0].Contains("creature", StringComparison.OrdinalIgnoreCase);

            var knownObjs = session.Player.GetKnownObjects();

            foreach (var knownObj in knownObjs)
            {
                if (creaturesOnly && !(knownObj is Creature))
                    continue;

                session.Player.RemoveTrackedObject(knownObj, false);
                session.Player.TrackObject(knownObj);
            }
            session.Player.PrevObjSend = DateTime.UtcNow;
        }

        // show player ace server versions
        [CommandHandler("aceversion", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Shows this server's version data")]
        public static void HandleACEversion(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("version_info_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"aceversion\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var msg = ServerBuildInfo.GetVersionInfo();

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
        }

        // reportbug < code | content > < description >
        [CommandHandler("reportbug", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2,
            "Generate a Bug Report",
            "<category> <description>\n" +
            "This command generates a URL for you to copy and paste into your web browser to submit for review by server operators and developers.\n" +
            "Category can be the following:\n" +
            "Creature\n" +
            "NPC\n" +
            "Item\n" +
            "Quest\n" +
            "Recipe\n" +
            "Landblock\n" +
            "Mechanic\n" +
            "Code\n" +
            "Other\n" +
            "For the first three options, the bug report will include identifiers for what you currently have selected/targeted.\n" +
            "After category, please include a brief description of the issue, which you can further detail in the report on the website.\n" +
            "Examples:\n" +
            "/reportbug creature Drudge Prowler is over powered\n" +
            "/reportbug npc Ulgrim doesn't know what to do with Sake\n" +
            "/reportbug quest I can't enter the portal to the Lost City of Frore\n" +
            "/reportbug recipe I cannot combine Bundle of Arrowheads with Bundle of Arrowshafts\n" +
            "/reportbug code I was killed by a Non-Player Killer\n"
            )]
        public static void HandleReportbug(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("reportbug_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"reportbug\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            var category = parameters[0];
            var description = "";

            for (var i = 1; i < parameters.Length; i++)
                description += parameters[i] + " ";

            description.Trim();

            switch (category.ToLower())
            {
                case "creature":
                case "npc":
                case "quest":
                case "item":
                case "recipe":
                case "landblock":
                case "mechanic":
                case "code":
                case "other":
                    break;
                default:
                    category = "Other";
                    break;
            }

            var sn = ConfigManager.Config.Server.WorldName;
            var c = session.Player.Name;

            var st = "ACE";

            //var versions = ServerBuildInfo.GetVersionInfo();
            var databaseVersion = DatabaseManager.World.GetVersion();
            var sv = ServerBuildInfo.FullVersion;
            var pv = databaseVersion.PatchVersion;

            //var ct = PropertyManager.GetString("reportbug_content_type").Item;
            var cg = category.ToLower();

            var w = "";
            var g = "";

            if (cg == "creature" || cg == "npc" || cg == "item" || cg == "item")
            {
                var objectId = new ObjectGuid();
                if (session.Player.HealthQueryTarget.HasValue || session.Player.ManaQueryTarget.HasValue || session.Player.CurrentAppraisalTarget.HasValue)
                {
                    if (session.Player.HealthQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.HealthQueryTarget);
                    else if (session.Player.ManaQueryTarget.HasValue)
                        objectId = new ObjectGuid((uint)session.Player.ManaQueryTarget);
                    else
                        objectId = new ObjectGuid((uint)session.Player.CurrentAppraisalTarget);

                    //var wo = session.Player.CurrentLandblock?.GetObject(objectId);

                    var wo = session.Player.FindObject(objectId.Full, Player.SearchLocations.Everywhere);

                    if (wo != null)
                    {
                        w = $"{wo.WeenieClassId}";
                        g = $"0x{wo.Guid:X8}";
                    }
                }
            }

            var l = session.Player.Location.ToLOCString();

            var issue = description;

            var urlbase = $"https://www.accpp.net/bug?";

            var url = urlbase;
            if (sn.Length > 0)
                url += $"sn={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sn))}";
            if (c.Length > 0)
                url += $"&c={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(c))}";
            if (st.Length > 0)
                url += $"&st={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(st))}";
            if (sv.Length > 0)
                url += $"&sv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sv))}";
            if (pv.Length > 0)
                url += $"&pv={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pv))}";
            //if (ct.Length > 0)
            //    url += $"&ct={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ct))}";
            if (cg.Length > 0)
            {
                if (cg == "npc")
                    cg = cg.ToUpper();
                else
                    cg = char.ToUpper(cg[0]) + cg.Substring(1);
                url += $"&cg={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cg))}";
            }
            if (w.Length > 0)
                url += $"&w={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(w))}";
            if (g.Length > 0)
                url += $"&g={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(g))}";
            if (l.Length > 0)
                url += $"&l={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(l))}";
            if (issue.Length > 0)
                url += $"&i={Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(issue))}";

            var msg = "\n\n\n\n";
            msg += "Bug Report - Copy and Paste the following URL into your browser to submit a bug report\n";
            msg += "-=-\n";
            msg += $"{url}\n";
            msg += "-=-\n";
            msg += "\n\n\n\n";

            session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.AdminTell));
        }

        static List<ActivityRecommendation> Recommendations = new List<ActivityRecommendation>()
        {
            // General
            new ActivityRecommendation(10, 80, new HashSet<Skill>{Skill.Axe, Skill.Dagger, Skill.Mace, Skill.Spear, Skill.Staff, Skill.Sword, Skill.UnarmedCombat}, "Equipment: Hunt Golems for Motes to craft Atlan and Isparian Weapons at the Crater Lake Village."),
            new ActivityRecommendation(10, 80, new HashSet<Skill>{Skill.Bow, Skill.Crossbow, Skill.ThrownWeapon, Skill.WarMagic, Skill.LifeMagic}, "Equipment: Hunt Golems for Motes to craft Isparian Weapons at the Crater Lake Village."),
            new ActivityRecommendation(15, 126, Skill.Lockpick, "XP: Hunt Undead for Mnemosynes. Unlock them with keys made using Lockpicking from Golem Hearts, and turn them in at the Mnemosyne Collection Site near Samsur at 2.5S, 16.4E."),
            new ActivityRecommendation(15, 126, Skill.Lockpick, "Equipment: Hunt Undead for Mnemosynes. Unlock them with keys made using Lockpicking from Golem Hearts, and turn them in at the Undead Hunter's tent near Tufa at 13.3S, 5.1E."),
            new ActivityRecommendation(15, 126, Skill.Armor, "Equipment: Hunt Shadows and Crystals for shards to craft Shadow Armor near Eastham at 18.5N, 62.8E, near Al-Jalima at 7.1N, 3.0E or near Kara at 82.9S, 46.0E."),
            new ActivityRecommendation(10, 126, "Hunting Grounds: Hunt Olthois in the Olthoi Arcade near Redspire at 39.1N 81.2W."),
            new ActivityRecommendation(20, 126, "GaerlanDefeated" , "Fellowship - Equipment: Explore Gaerlan's Citadel and recover Olthoi Slayer equipment."),

            // T1
            new ActivityRecommendation(1, 1, "RecruitSent", "XP/Equipment: Go through the Training Academy Portal and complete the tutorial for equipment and experience."),
            new ActivityRecommendation(2, 15, "Equipment: Collect Red and Gold Letters, gather stamps and trade them in for Exploration Society Equipment. For more information talk to Exploration Society Agents, usually located in taverns wearing green clothes."),

            //new ActivityRecommendation(2, 5, new HashSet<string>{ "RITHWICMINDORLALETTER"}, "XP: Visit Rithwic East and help Mindorla with an errand."),
            //new ActivityRecommendation(2, 5, new HashSet<string>{ "RITHWICCELCYNDRING"}, "XP: Recover a letter from the Old Warehouse near Rithwic at 7.6N, 58.4E and deliver it to Celcynd the Dour in Rithwic."),

            new ActivityRecommendation(2, 8, new HashSet<string>{ "HoltburgAfrinCorn1204", "HoltburgAfrinRye1204", "HoltburgAfrinWheat1204","HoltburgAfrinDrudge1204"}, "XP: Recover Stolen Supplies from the Drudge Hideout near Holtburg at 41.3N, 33.3E and deliver them to Alfrin in Holtburg."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "AxeBrogordQuest", "HoltburgNoteBrogord1204"}, "XP: Recover Brogord's Axe and a Letter to Ryndya from the Cave of Alabree near Holtburg at 41.8N, 32.1E and deliver them to Flinrala Ryndmad in Holtburg."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "HoltburgRedoubtCandlestick1204", "HoltburgRedoubtBowl1204", "AntiquePlatterQuest", "HoltburgRedoubtLamp1204","HoltburgRedoubtHandbell1204","HardunnaBandQuest","HoltburgRedoubtMug1204","HoltburgRedoubtGoblet1204"}, "XP: Recover Heirlooms from the Holtburg Redoubt near Holtburg at 40.4N, 34.4E and return them to Worcer in Holtburg."),

            new ActivityRecommendation(2, 8, new HashSet<string>{ "YaojiLouKaQuest", "ShoushiBraidBracelet1204", "ShoushiBraidKatar1204", "ShoushiBraidNecklace1204", "ShoushiBraidRing1204", "ShoushiBraidShuriken1204", "ShoushiBraidTrident1204"}, "XP: Recover Lou Ka's Stolen Items from Braid Mansion Ruin just outside of Shoushi, at 34.2S 72.0E and return them to Lou Ka in the bar in Shoushi."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "ShoushiNenAiCheese1204", "ShoushiNenAiCider1204"}, "XP: Help Nen Ai feed her pet drudge at 34.8S, 71.2E near Shoushi."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "ShoushiStoneCompassion", "ShoushiStoneDetachment", "ShoushiStoneDiscipline1204", "ShoushiStoneHumility1204"}, "XP: Recover the four Stones of Jojii from the Shreth Hive at 32.4S, 71.0E near Shoushi and bring them to Oi-Tong Ye in Shoushi."),

            new ActivityRecommendation(2, 8, new HashSet<string>{ "PerfectlyAgedCoveCiderQuest", "YaraqAppleCovePerfect1204", "YaraqApplePieHot1204", "YaraqBakingPanCoveApple1204", "YaraqCiderCoveAppleAged1204", "YaraqCiderHardCoveApple1204", "YaraqKnifeCoveApple1204", "YaraqWineCoveApple1204"}, "XP: Retrieve Lubziklan al-Luq stolen items from the Sea Temple Catacombs at 20.2S, 4.4W near Yaraq and return them to Lubziklan al-Luq at 22.4S, 1.9W near Yaraq."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "YaraqNasunLetter", "YaraqAhyaraLetter" }, "XP: Help Nasun ibn Tifar and Ahyara by delivering some letters, they can be found in the North Yaraq Outpost and the East Yaraq Outpost respectively."),
            new ActivityRecommendation(2, 8, new HashSet<string>{ "NoteDrudgeScrawledPickup", "YaraqHeadMarionetteMadStar1204" }, "XP: Help Ma'yad ibn Ibsar locate her missing brother and investigate the mystery of the Mad Star. Meet her at the Cerulean Cove Pub in Yaraq."),

            new ActivityRecommendation(2, 8, "HeaToneawaCompleted", "XP: Deliver a love letter for Hea Toneawa at 43.7N 66.9W near Greenspire."),

            new ActivityRecommendation(8, 15, "OlthoiHunting1", "XP: Kill Olthois in the Abandoned Tumerok Site near Redspire at 42.0N, 82.2W and bring a Harvester Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(12, 20, "OlthoiHunting2", "XP: Kill Olthois in the Dark Lair near Greenspire at 43.8N, 68.4W and bring a Gardener Pincer to Behdo Yii in Redspire."),

            new ActivityRecommendation(5, 10, Skill.Shield, "Equipment: Explore Eastham Sewer near Eastham at 18.7N, 63.4E for the Metal Round Shield."),

            new ActivityRecommendation(10, 20, Skill.Axe, new HashSet<string>{"BanderlingMaceShaft", "BanderlingMaceHead"}, "Equipment: Explore Banderling Conquest near Sawato at 29.0S, 50.5E for the Banderling Mace Shaft and Mosswart Maze near Al-Arqas at 25.2S, 19.4E for Banderling Mace Head and bring them to Olivier Rognath in Eastham for the Mace of the Explorer."),
            new ActivityRecommendation(10, 20, Skill.Axe, "CrimsonBrokenHaft", "Equipment: Reclaim the Silifi of Crimson Stars, start by exploring Leikotha's Crypt at 10.1S 31.3E and then visit Kayna bint Iswas at 1.7S, 36.6E."),
            new ActivityRecommendation(12, 20, Skill.Axe, "TumerokVanguardMorningstar", "Equipment: Explore the North Tumerok Vanguard Outpost near Tufa at 7.5S, 0.0W for the Vanguard Leader's Morningstar and Vanguard Leader's Amulet."),
            new ActivityRecommendation(12, 20, Skill.Bow, "TumerokVanguardCrossbow", "Equipment: Explore the South Tumerok Vanguard Outpost near Khayyaban at 45.9S, 14.2E for the Vanguard Leader's Crossbow and Vanguard Leader's Amulet."),
            new ActivityRecommendation(10, 20, Skill.Dagger, "Equipment: Explore the Folthid Estate near Yanshi at 8.6S, 52.9EE for the Dagger of Tikola and Dull Dagger. Speak with Raxanza Folthid at 8.8S, 53.7E for more information."),
            new ActivityRecommendation(10, 20, Skill.Spear, "MosswartExodusSpear", "Equipment: Help Bleeargh retrieve the Spear of Kreerg, Bleeargh can be found in Yanshi at 12.8S, 46.2E."),
            new ActivityRecommendation(10, 20, Skill.Spear, "Equipment: Recover the Quarter Staff of Fire from Banderlings camped near Edelbar at 43.9N, 25.1E."),
            new ActivityRecommendation(10, 20, "RegicideComplete", "Equipment: Speak with Sir Rylanan in Holtburg, Sir Tenshin in Shoushi or Dame Tsaya in Yaraq to help with an investigation and be rewarded with Elysa's Favor."),

            new ActivityRecommendation(5, 20, "IceTachiTurnedIn", "XP/Equipment: Retrieve the fabled Ice Tachi from the mosswart camp at 27.5S, 71.0E near Shoushi, keep it or deliver it to an Ivory Crafter for an experience reward."),
            new ActivityRecommendation(10, 20, "AcidAxeTurnedIn", "XP/Equipment: Explore Suntik Village near Zaikhal at 16.2N 4.3E for the Acid Axe, keep it or deliver it to an Ivory Crafter for an experience reward."),
            new ActivityRecommendation(10, 20, "GivenTibriSpear", "XP/Equipment: Explore a Cave near Cragstone at 24.2N, 43.2E for Tibri's Fire Spear, keep it or deliver it to Tibri also in the cave for an experience reward."),
            new ActivityRecommendation(10, 20, "LilithasBowGiven", "XP/Equipment: Explore Hunter's Leap near Holtburg at 35.7N, 32.6E for Lilitha's Bow, keep it or deliver it to Eldrista at 35.7N, 33.4E for an experience reward."),

            new ActivityRecommendation(10, 20, Skill.Armor, "Equipment: Explore the Glenden Wood Dungeon near Glenden Wood at 29.9N, 26.4E for the Platemail Hauberk of the Ogre."),
            new ActivityRecommendation(10, 20, new HashSet<Skill>{Skill.Armor, Skill.Shield}, "Equipment: Explore the Halls of the Helm near Zaikhal at 15.8N, 2.1E for the Fiery Shield and Superior Helmet."),
            new ActivityRecommendation(10, 20, new HashSet<Skill>{Skill.Axe, Skill.Shield}, "Equipment: Explore Trothyr's Rest near Rithwic at 10.3N, 54.9E for Trothyr's War Hammer and Trothyr's Shield. Talk to Ringoshu the Apple Seller at 13.6N, 50.7E for more information."),
            new ActivityRecommendation(5, 20, new HashSet<Skill>{Skill.Armor, Skill.Spear}, "Equipment: Explore the Green Mire Grave near Shoushi at 27.8S, 71.6E for the Green Mire Warrior's Yoroi Cuirass and the Green Mire Yari."),

            new ActivityRecommendation(20, 30, "Hunting Grounds: Hunt Lugians in the Hills Citadel near Lin at 56.6S, 66.9E."),

            // T2
            new ActivityRecommendation(15, 30, "PalenqualCompleted", "Equipment: Hunt Hea Warriors north of Greenspire to get Totems, once you have a totem take it to Aun Shimauri at 46.7N, 70.6W for more information on how to acquire your Palenqual weapon."),
            new ActivityRecommendation(20, 30, "OlthoiHunting3", "XP: Kill Olthois in the Crumbling Empyrean Mansion near Greenspire at 46.8N, 67.8W and bring a Worker Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(14, 25, "TuskFemalePickup", "XP: Kill Tuskers in the Tusker Burrow in Alphus Lassel at 2.0N, 97.9E and bring a Female Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(18, 30, "TuskMalePickup", "XP: Kill Tuskers in the Tusker Lodge in Alphus Lassel at 0.1N, 98.1E and bring a Male Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(25, 35, "TuskCrimsonbackPickup", "XP: Kill Tuskers in the Tusker Cave in Alphus Lassel at 0.4N, 97.4E and bring a Tusker Crimsonback Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),

            new ActivityRecommendation(15, 30, Skill.Lockpick, "Hunting Grounds: Halls of Metos - North of Tufa at 4.4S, 0.6W - Hunt Undeads for Mnemosynes, and golems for Motes and Hearts. Use a Intricate Carving Tool and the Lockpick skill to turn the Hearts into keys for the Mnemosynes."),
            new ActivityRecommendation(15, 30, "Hunting Grounds: Halls of Metos - North of Tufa at 4.4S, 0.6W - Hunt Undeads for Mnemosynes, and golems for Motes."),
            new ActivityRecommendation(20, 30, "Hunting Grounds: Northern Tiofor Woods - Hunt Shadows for Dark Slivers in the region north of Glenden Wood and Holtburg."),
            new ActivityRecommendation(20, 35, "Hunting Grounds: The very bottom of the Fenmalain Chamber is a great place to hunt Fragments for Tiny Shards. To get there you need to use Fenmalain Keys at the bottom of the Fenmalain Vestibule near Baishi at 46.9S, 55.2E."),
            new ActivityRecommendation(20, 30, Skill.Axe, "Equipment: Explore the Bellig Tower near Zaikhal at 17.8N, 16.0E for the Hammer of Lightning."),
            new ActivityRecommendation(20, 35, Skill.Mace, "JitteKrauLiLesser", "Equipment: Explore the Catacombs of the Forgotten in the Plains of Gaerwel at 17.3N, 32.8E for Mi Krau-Li's Jitte."),
            new ActivityRecommendation(20, 50, Skill.Axe, "Equipment: Kill the lugians in the Gotrok Raider Camp at 80.8S, 37.6E for the Lugian Scepter and Cloth of the Arm and take them to Master Ulkas in Livak Tukal for the Scepter of Might and Sleeves of Inexhaustibility."),
            new ActivityRecommendation(20, 50, new HashSet<Skill>{Skill.Shield, Skill.WarMagic}, "Equipment: Kill the lugians in the Gotrok Raider Camp at 87.2S, 27.3E for the Lugian Crest and Sceptre of the Mind and take them to Master Ulkas in Livak Tukal for the Crest of Kings and Staff of Clarity."),
            new ActivityRecommendation(20, 50, new HashSet<Skill>{Skill.Spear, Skill.Armor}, "Equipment: Kill the lugians in the Gotrok Raider Camp at 67.9S, 32.8E for the Lugian Pauldron and Blade of the Heart and take them to Master Ulkas in Livak Tukal for the Helm of the Crag and Spear of Purity."),
            new ActivityRecommendation(20, 50, new HashSet<Skill>{Skill.Axe, Skill.Dagger, Skill.Mace, Skill.Spear, Skill.Staff, Skill.Sword, Skill.UnarmedCombat, Skill.Bow, Skill.Crossbow, Skill.ThrownWeapon, Skill.WarMagic, Skill.LifeMagic}, "Equipment: Venture into the Tumerok Training Camps near Dryreach, acquire Tumerok Banners and trade them in at the Army Recruiter located in capital cities for an Assault Weapon."),

            new ActivityRecommendation(20, 35, Skill.Armor, new HashSet<string>{"PickedUpBroodMatronTail", "PickedUpBroodMatronTarsus", "PickedUpBroodMatronTibia", "PickedUpBroodQueenCarapace", "PickedUpBroodQueenClaw", "PickedUpBroodQueenCrest", "PickedUpBroodQueenFemur", "PickedUpBroodQueenHead", "PickedUpBroodQueenMetathorax"}, "Fellowship - Equipment: Explore the Olthoi Brood Hives at 51.2N 48.1E or 44.2N, 66.2E for the Lesser Olthoi Armor."),

            // Higher
            new ActivityRecommendation(35, 999, "OlthoiHunting4", "XP: Kill Olthois in An Olthoi Soldier Nest in the Marescent Plateau at 45.2N, 76.3W and bring a Soldier Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(40, 999, "OlthoiHunting5", "XP: Kill Olthois in the Ancient Empyrean Grotto in the Marescent Plateau at 52.6N, 73.1W and bring a Legionary Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(50, 999, "OlthoiHunting6", "XP: Kill Olthois in the Lair of the Eviscerators in the Marescent Plateau at 53.7N, 76.6W and bring an Eviscerator Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(70, 999, "OlthoiHunting7", "XP: Kill Olthois in the Olthoi Warrior Nest in the Marescent Plateau at 46.9N, 81.2W and bring a Warrior Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(80, 999, "OlthoiHunting8", "XP: Kill Olthois in the Mutilator Tunnels in the Marescent Plateau at 52.8N, 78.1W and bring a Mutilator Pincer to Behdo Yii in Redspire."),
            new ActivityRecommendation(30, 999, "TuskGoldenbackPickup", "XP: Kill Tuskers in the Tusker Cavern in Alphus Lassel at 1.0N, 96.9E and bring a Goldenback Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(35, 999, "TuskRedeemerPickup", "XP: Kill Tuskers in the Tusker Abode in Alphus Lassel at 3.2S, 94.9E and bring a Tusker Redeemer Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(40, 999, "TuskLiberatorPickup", "XP: Kill Tuskers in the Tusker Habitat in Alphus Lassel at 0.5S, 95.9E and bring a Tusker Liberator Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(45, 999, "TuskSlavePickup", "XP: Kill Tuskers in the Tusker Quarters in Alphus Lassel at 2.3S, 95.6E and bring a Tusker Slave Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(50, 999, "TuskGuardPickup", "XP: Kill Tuskers in the Tusker Barracks in Alphus Lassel at 0.3S, 90.8E and bring a Tusker Guard Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(55, 999, "TuskSilverPickup", "XP: Kill Tuskers in the Tusker Pits in Alphus Lassel at 1.3N, 91.8E and bring a Silver Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(60, 999, "TuskArmoredPickup", "XP: Kill Tuskers in the Tusker Armory in Alphus Lassel at 0.0N, 89.4E and bring a Armored Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(65, 999, "TuskRampagerPickup", "XP: Kill Tuskers in the Tusker Holding in Alphus Lassel at 3.5S, 85.3E and bring a Rampager Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(70, 999, "TuskPlatedPickup", "XP: Kill Tuskers in the Tusker Tunnels in Alphus Lassel at 0.4N, 86.4E and bring a Plated Tusker Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(80, 999, "TuskAssailerPickup", "XP: Kill Tuskers in the Tusker Honeycombs in Alphus Lassel at 1.3S, 86.9E and bring a Assailer Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
            new ActivityRecommendation(100, 999, "TuskDevastatorPickup", "XP: Kill Tuskers in the Tusker Lacuna in Alphus Lassel at 9.9S, 90.7E and bring a Devastator Tusk to Brighteyes in Oolutanga's Refuge. You can get to Alphus lassel via the Tusker Temples: 10.5S 65.6E for levels 1-30, 59.8N 28.4E for levels 20-50 and 0.7N 68.1W for levels 40+."),
        };

        [CommandHandler("recs", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Recommend activities appropriate to the character.")]
        public static void HandleRecommend(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: recs", ChatMessageType.Help));
                return;
            }

            var level = session.Player?.Level ?? 1;
            var minLevel = Math.Max(level - (int)(level * 0.1f), 1);
            var maxLevel = level + (int)(level * 0.2f);
            if (level > 100)
                maxLevel = int.MaxValue;
            var explorationList = DatabaseManager.World.GetExplorationSitesByLevelRange(minLevel, maxLevel, level);

            var validRecommendations = BuildRecommendationList(session.Player);

            if (validRecommendations.Count == 0 && explorationList.Count == 0)
                session.Network.EnqueueSend(new GameMessageSystemChat("No recommendations at the moment.", ChatMessageType.WorldBroadcast));
            else
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Activity Recommendations:", ChatMessageType.WorldBroadcast));
                foreach (var recommendation in validRecommendations)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(recommendation.RecommendationText, ChatMessageType.WorldBroadcast));
                }

                foreach (var entry in explorationList)
                {
                    string amountAdjetive;
                    if (entry.CreatureCount < 10)
                        amountAdjetive = "a few ";
                    else if (entry.CreatureCount < 25)
                        amountAdjetive = "";
                    else if (entry.CreatureCount < 50)
                        amountAdjetive = "some ";
                    else if (entry.CreatureCount < 75)
                        amountAdjetive = "quite a few ";
                    else
                        amountAdjetive = "a lot of ";

                    string entryName;
                    string entryDirections;
                    var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)entry.Landblock).FirstOrDefault();
                    if (entryLandblock != null)
                    {
                        entryName = entryLandblock.Name;
                        entryDirections = entryLandblock.Directions;
                    }
                    else
                    {
                        entryName = $"unknown location({entry.Landblock})";
                        entryDirections = "at an unknown location";
                    }

                    var msg = $"Hunting Grounds: {entryName} {entryDirections}. Expect to find {amountAdjetive}{entry.ContentDescription}.";
                    session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
                }
            }
        }

        [CommandHandler("rec", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Recommend an activity appropriate to the character.")]
        public static void HandleSingleRecommendation(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: rec", ChatMessageType.Help));
                return;
            }

            SingleRecommendation(session, false);
        }

        public static void SingleRecommendation(Session session, bool failSilently)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            var validRecommendations = BuildRecommendationList(session.Player);

            var level = session.Player?.Level ?? 1;
            var minLevel = Math.Max(level - (int)(level * 0.1f), 1);
            var maxLevel = level + (int)(level * 0.2f);
            if (level > 100)
                maxLevel = int.MaxValue;
            var explorationList = DatabaseManager.World.GetExplorationSitesByLevelRange(minLevel, maxLevel, level);

            if (validRecommendations.Count != 0)
            {
                if (ThreadSafeRandom.Next(1, 4) != 4 || explorationList.Count == 0)
                {
                    var recommendation = validRecommendations[ThreadSafeRandom.Next(0, validRecommendations.Count - 1)];
                    session.Network.EnqueueSend(new GameMessageSystemChat($"Activity Recommendation:\n{recommendation.RecommendationText}", ChatMessageType.WorldBroadcast));
                    return;
                }
            }

            if (explorationList.Count == 0)
            {
                if (!failSilently)
                    session.Network.EnqueueSend(new GameMessageSystemChat("No recommendations at the moment.", ChatMessageType.WorldBroadcast));
            }
            else
            {
                var entry = explorationList[ThreadSafeRandom.Next(0, explorationList.Count - 1)];

                string amountAdjetive;
                if (entry.CreatureCount < 10)
                    amountAdjetive = "a few ";
                else if (entry.CreatureCount < 25)
                    amountAdjetive = "";
                else if (entry.CreatureCount < 50)
                    amountAdjetive = "some ";
                else if (entry.CreatureCount < 75)
                    amountAdjetive = "quite a few ";
                else
                    amountAdjetive = "a lot of ";

                string entryName;
                string entryDirections;
                var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)entry.Landblock).FirstOrDefault();
                if (entryLandblock != null)
                {
                    entryName = entryLandblock.Name;
                    entryDirections = entryLandblock.Directions;
                }
                else
                {
                    entryName = $"unknown location({entry.Landblock})";
                    entryDirections = "at an unknown location";
                }

                var msg = $"Activity Recommendation:\nHunting Grounds: {entryName} {entryDirections}. Expect to find {amountAdjetive}{entry.ContentDescription}.";
                session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
            }
        }

        public static List<ActivityRecommendation> BuildRecommendationList(Player player)
        {
            if (player == null)
                return new List<ActivityRecommendation>();

            var validRecommendations = new List<ActivityRecommendation>();
            foreach (var recommendation in Recommendations)
            {
                if (recommendation.IsApplicable(player))
                {
                    validRecommendations.Add(recommendation);
                }
            }

            return validRecommendations;
        }

        [CommandHandler("xptracker", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Return XP tracking information.", "<reset>")]
        public static void HandleXpTracker(Session session, params string[] parameters)
        {
            bool reset = false;
            if (parameters.Length > 0)
                reset = parameters[0].ToLower() == "reset";

            if (!reset)
            {
                if (!session.Player.XpTrackerStartTimestamp.HasValue || !session.Player.XpTrackerTotalXp.HasValue)
                {
                    session.Player.XpTrackerStartTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                    session.Player.XpTrackerTotalXp = 0;
                    session.Network.EnqueueSend(new GameMessageSystemChat($"XP tracking has been enabled for your character.\n", ChatMessageType.Broadcast));
                    return;
                }

                var currUnixTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                var durationSeconds = currUnixTimestamp - session.Player.XpTrackerStartTimestamp.Value;

                if (session.Player.XpTrackerTotalXp.Value > 0 && durationSeconds > 0)
                {
                    var durationTimespan = TimeSpan.FromSeconds(durationSeconds);
                    var xpPerSecond = session.Player.XpTrackerTotalXp.Value / (double)(durationSeconds);
                    var xpPerHour = xpPerSecond * 60 * 60;
                    var msg = $"You've earned {session.Player.XpTrackerTotalXp.Value:N0} experience in {FormatTimespan(durationTimespan)} at a rate of {xpPerHour:N0} experience per hour.";
                    session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                }
                else
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("No XP has been tracked for your character yet.", ChatMessageType.Broadcast));
                }
            }
            else
            {
                session.Player.XpTrackerStartTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                session.Player.XpTrackerTotalXp = 0;
                session.Network.EnqueueSend(new GameMessageSystemChat($"Your character's xp tracking data has been reset.\n", ChatMessageType.Broadcast));
            }
        }

        [CommandHandler("tar", AccessLevel.Player, CommandHandlerFlag.None, 0, "Show current T.A.R. experience multipliers", "<creatureType>")]
        public static void HandleRest(Session session, params string[] parameters)
        {
            if (parameters.Length > 0 && Enum.TryParse(parameters[0], true, out CreatureType creatureType))
            {
                session.Player.CampManager.GetCurrentCampBonus(creatureType, out var typeCampBonus, out var areaCampBonus, out var restCampBonus, out var typeRecovery, out var areaRecovery, out var restRecovery);
                CommandHandlerHelper.WriteOutputInfo(session, $"Current T.A.R. experience multipliers:\n   Type({creatureType}): {(typeCampBonus * 100).ToString("0")}%{(typeCampBonus != 1 ? $" - Estimated recovery time: {FormatTimespan(typeRecovery)}" : "")}\n   Area: {(areaCampBonus * 100).ToString("0")}%{(areaCampBonus != 1 ? $" - Estimated recovery time: {FormatTimespan(areaRecovery)}" : "")}\n   Rest: {(restCampBonus * 100).ToString("0")}%{(restCampBonus != 1 ? $" - Estimated recovery time: {FormatTimespan(restRecovery)}" : "")}");
            }
            else
            {
                session.Player.CampManager.GetCurrentCampBonus(CreatureType.Invalid, out _, out var areaCampBonus, out var restCampBonus, out var typeRecovery, out var areaRecovery, out var restRecovery);
                CommandHandlerHelper.WriteOutputInfo(session, $"Current T.A.R. experience multipliers:\n   Area: {(areaCampBonus * 100).ToString("0")}%{(areaCampBonus != 1 ? $" - Estimated recovery time: {FormatTimespan(areaRecovery)}" : "")}\n   Rest: {(restCampBonus * 100).ToString("0")}%{(restCampBonus != 1 ? $" - Estimated recovery time: {FormatTimespan(restRecovery)}" : "")}");
            }
        }

        /// <summary>
        /// List online players within the character's allegiance.
        /// </summary>
        [CommandHandler("who", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "List online players within the character's allegiance.")]
        public static void HandleWho(Session session, params string[] parameters)
        {
            if (!PropertyManager.GetBool("command_who_enabled").Item)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("The command \"who\" is not currently enabled on this server.", ChatMessageType.Broadcast));
                return;
            }

            if (session.Player.MonarchId == null)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You must be in an allegiance to use this command.", ChatMessageType.Broadcast));
                return;
            }

            if (DateTime.UtcNow - session.Player.PrevWho < TimeSpan.FromMinutes(1))
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                return;
            }

            session.Player.PrevWho = DateTime.UtcNow;

            StringBuilder message = new StringBuilder();
            message.Append("Allegiance Members: \n");


            uint playerCounter = 0;
            foreach (var player in PlayerManager.GetAllOnline().OrderBy(p => p.Name))
            {
                if (player.MonarchId == session.Player.MonarchId)
                {
                    message.Append($"{player.Name} - Level {player.Level}\n");
                    playerCounter++;
                }
            }

            message.Append("Total: " + playerCounter + "\n");

            CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
        }

        public static string FormatTimespan(TimeSpan timespan)
        {
            string returnText = "";
            if (timespan.TotalMinutes < 2)
            {
                if (timespan.Seconds > 1)
                    returnText = $"{timespan.Seconds} seconds";
                else if (timespan.Seconds == 1)
                    returnText = $"{timespan.Seconds} second";

                if (timespan.Minutes > 0)
                    returnText = $"{timespan.Minutes} minute" + (returnText.Length > 0 ? $" {returnText}" : "");
            }
            else
            {
                if (timespan.Minutes > 1)
                    returnText = $"{timespan.Minutes} minutes";
                else if (timespan.Minutes == 1)
                    returnText = $"{timespan.Minutes} minute";

                int totalHours = (int)Math.Floor(timespan.TotalHours);
                if (totalHours > 0)
                {
                    if (timespan.Hours > 1)
                        returnText = $"{timespan.Hours} hours" + (returnText.Length > 0 ? $" {returnText}" : "");
                    else if (timespan.Hours == 1)
                        returnText = $"{timespan.Hours} hour" + (returnText.Length > 0 ? $" {returnText}" : "");
                }

                int totalDays = (int)Math.Floor(timespan.TotalDays);
                if (totalDays > 1)
                    returnText = $"{totalDays} days" + (returnText.Length > 0 ? $" {returnText}" : "");
                else if (totalDays > 0)
                    returnText = $"{totalDays} day" + (returnText.Length > 0 ? $" {returnText}" : "");
            }

            return returnText;
        }

        public class ActivityRecommendation
        {
            public HashSet<Skill> Skills = new HashSet<Skill>();
            public int MinLevel = 0;
            public int MaxLevel = int.MaxValue;
            public int MinSkill = 0;
            public int MaxSkill = int.MaxValue;
            public HashSet<string> QuestFlags = new HashSet<string>();
            public string RecommendationText;

            public ActivityRecommendation(int minLevel, int maxLevel, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill>(), 0, int.MaxValue, new HashSet<string>(), recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, Skill skill, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill> { skill }, 0, int.MaxValue, new HashSet<string>(), recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, HashSet<Skill> skills, string recommendation)
                : this(minLevel, maxLevel, skills, 0, int.MaxValue, new HashSet<string>(), recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, string questFlag, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill>(), 0, int.MaxValue, new HashSet<string> { questFlag }, recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, HashSet<string> questFlags, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill>(), 0, int.MaxValue, questFlags, recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, Skill skill, int minSkill, int maxSkill, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill> { skill }, minSkill, maxSkill, new HashSet<string>(), recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, Skill skill, string questFlag, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill> { skill }, 0, int.MaxValue, new HashSet<string> { questFlag }, recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, Skill skill, HashSet<string> questFlags, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill> { skill }, 0, int.MaxValue, questFlags, recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, Skill skill, int minSkill, int maxSkill, string questFlag, string recommendation)
                : this(minLevel, maxLevel, new HashSet<Skill> { skill }, minSkill, maxSkill, new HashSet<string> { questFlag }, recommendation) { }

            public ActivityRecommendation(int minLevel, int maxLevel, HashSet<Skill> skills, int minSkill, int maxSkill, HashSet<string> questFlags, string recommendation)
            {
                MinLevel = minLevel;
                MaxLevel = maxLevel;
                Skills = skills;
                MinSkill = minSkill;
                MaxSkill = maxSkill;
                QuestFlags = questFlags;
                RecommendationText = recommendation;
            }

            public bool IsApplicable(Player player)
            {
                if (player.Level < MinLevel || player.Level > MaxLevel)
                    return false;

                foreach (var questFlag in QuestFlags)
                {
                    if (!player.QuestManager.CanSolve(questFlag))
                        return false;
                }

                if (Skills.Count == 0)
                    return true;

                foreach (var skill in Skills)
                {
                    var playerSkill = player.GetCreatureSkill(player.ConvertToMoASkill(skill));
                    if (playerSkill.AdvancementClass == SkillAdvancementClass.Trained || playerSkill.AdvancementClass == SkillAdvancementClass.Specialized)
                    {
                        if (playerSkill.Current >= MinSkill && playerSkill.Current <= MaxSkill)
                            return true;
                    }
                }

                return false;
            }
        }

        [CommandHandler("fi", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Resends all visible items and creatures to the client")]
        [CommandHandler("fixinvisible", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Resends all visible items and creatures to the client")]
        public static void HandleFixInvisible(Session session, params string[] parameters)
        {
            var knownObjects = session.Player.GetKnownObjects();
            foreach (var entry in knownObjects)
            {
                session.Player.TrackObject(entry, true);
            }
        }

        [CommandHandler("allowres", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Toggles allowing resurrection attempts.")]
        [CommandHandler("allowress", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Toggles allowing resurrection attempts.")]
        public static void HandleAllowRess(Session session, params string[] parameters)
        {
            var newSetting = !session.Player.GetCharacterOption(CharacterOption.AllowRessAttempts);
            session.Player.SetCharacterOption(CharacterOption.AllowRessAttempts, newSetting);

            if (newSetting)
                CommandHandlerHelper.WriteOutputInfo(session, $"You are now accepting resurrection attempts.", ChatMessageType.Broadcast);
            else
                CommandHandlerHelper.WriteOutputInfo(session, $"You are no longer accepting resurrection attempts.", ChatMessageType.Broadcast);
        }

        [CommandHandler("smartsalvage", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Configures the smart salvage system.")]
        public static void HandleSmartSalvage(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: smartsalvage", ChatMessageType.Help));
                return;
            }

            var param0 = "help";
            if (parameters.Length > 0)
                param0 = parameters[0].ToLower();

            if (param0 == "help")
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage system usage:", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage status to show current settings.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage <on/off> to enable/disable the smart salvage system.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage avoidInscripted <on/off> to enable/disable avoiding salvaging inscripted items.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage mode <whitelist/blacklist> to switch between filter modes: in blacklist mode materials in the filter won't be salvaged, in whitelist mode only materials in the filter will be salvaged.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage add <material> to add that material to the filter.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage remove <material> to remove that material from the filter.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"   /SmartSalvage clear to remove all materials from the filter.", ChatMessageType.Broadcast);
            }
            else if (param0 == "status")
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage system status: {(session.Player.UseSmartSalvageSystem ? "enabled" : "disabled")}.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage avoid inscripted items: {(session.Player.SmartSalvageAvoidInscripted ? "enabled" : "disabled")}.", ChatMessageType.Broadcast);
                CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage filter mode: {(session.Player.SmartSalvageIsWhitelist ? "whitelist" : "blacklist")}.", ChatMessageType.Broadcast);
                if (session.Player.SmartSalvageFilter == null)
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage filter: empty.", ChatMessageType.Broadcast);
                else
                {
                    var filters = (session.Player.SmartSalvageFilter ?? "").Split(",").ToList();
                    string filtersText = "";
                    bool first = true;
                    foreach (var filter in filters)
                    {
                        if (!first)
                            filtersText += ", ";
                        if (int.TryParse(filter, out var materialId))
                        {
                            filtersText += RecipeManager.GetMaterialName((MaterialType)materialId);
                            first = false;
                        }
                    }
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage filter: {filtersText}", ChatMessageType.Broadcast);
                }
            }
            else if (param0 == "on")
            {
                if (!session.Player.UseSmartSalvageSystem)
                {
                    session.Player.UseSmartSalvageSystem = true;
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is now enabled.", ChatMessageType.Broadcast);
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already enabled.", ChatMessageType.Broadcast);
            }
            else if (param0 == "off")
            {
                if (!session.Player.UseSmartSalvageSystem)
                {
                    session.Player.UseSmartSalvageSystem = true;
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is now disabled.", ChatMessageType.Broadcast);
                }
                else
                    CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already disabled.", ChatMessageType.Broadcast);
            }
            else if (param0 == "avoidinscripted")
            {
                if (parameters.Length < 2)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Could not parse command, check \"/SmartSalvage help\" for usage instructions.", ChatMessageType.Broadcast);
                    return;
                }

                var param1 = parameters[1].ToLower();
                if (param1 == "on")
                {
                    if (!session.Player.SmartSalvageAvoidInscripted)
                    {
                        session.Player.SmartSalvageAvoidInscripted = true;
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage will now avoid salvaging inscripted items.", ChatMessageType.Broadcast);
                    }
                    else
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already avoiding salvaging inscripted items.", ChatMessageType.Broadcast);
                }
                else if (param1 == "off")
                {
                    if (session.Player.UseSmartSalvageSystem)
                    {
                        session.Player.UseSmartSalvageSystem = false;
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage will no longer avoid salvaging inscripted items.", ChatMessageType.Broadcast);
                    }
                    else
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already not avoiding salvaging inscripted items.", ChatMessageType.Broadcast);
                }
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Could not parse command, check \"/SmartSalvage help\" for usage instructions.", ChatMessageType.Broadcast);
                    return;
                }
            }
            else if (param0 == "mode")
            {
                if (parameters.Length < 2)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Could not parse command, check \"/SmartSalvage help\" for usage instructions.", ChatMessageType.Broadcast);
                    return;
                }

                var param1 = parameters[1].ToLower();
                if (param1 == "blacklist")
                {
                    if (session.Player.SmartSalvageIsWhitelist)
                    {
                        session.Player.SmartSalvageIsWhitelist = false;
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage will now avoid salvaging materials in the filter.", ChatMessageType.Broadcast);
                    }
                    else
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already avoiding salvaging materials in the filter.", ChatMessageType.Broadcast);
                }
                else if (param1 == "whitelist")
                {
                    if (!session.Player.SmartSalvageIsWhitelist)
                    {
                        session.Player.SmartSalvageIsWhitelist = true;
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage will now only salvage materials in the filter.", ChatMessageType.Broadcast);
                    }
                    else
                        CommandHandlerHelper.WriteOutputInfo(session, $"Smart salvage is already salvaging only materials in the filter.", ChatMessageType.Broadcast);
                }
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Could not parse command, check \"/SmartSalvage help\" for usage instructions.", ChatMessageType.Broadcast);
                    return;
                }
            }
            else if (param0 == "add")
            {
                if (parameters.Length < 2)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Missing material type to add to the salvage filter", ChatMessageType.Broadcast);
                    return;
                }

                if (!Enum.TryParse(parameters[1], true, out MaterialType material))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unable to add {parameters[1]} to the salvage filter.", ChatMessageType.Broadcast);
                    return;
                }

                var filters = (session.Player.SmartSalvageFilter ?? "").Split(",").ToList();
                var searchString = ((int)material).ToString();
                var friendlyName = RecipeManager.GetMaterialName(material);
                if (filters.Contains(searchString))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"{friendlyName} is already included in the salvage filter.", ChatMessageType.Broadcast);
                    return;
                }

                filters.Add(searchString);
                session.Player.SmartSalvageFilter = string.Join(",", filters);
                CommandHandlerHelper.WriteOutputInfo(session, $"Added {friendlyName} to the salvage filter.", ChatMessageType.Broadcast);
            }
            else if (param0 == "remove")
            {
                if (parameters.Length < 2)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Missing material type to remove from the salvage filter", ChatMessageType.Broadcast);
                    return;
                }

                if (!Enum.TryParse(parameters[1], true, out MaterialType material))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"Unable to add {parameters[1]} to the salvage filter.", ChatMessageType.Broadcast);
                    return;
                }

                var filters = (session.Player.SmartSalvageFilter ?? "").Split(",").ToList();
                var searchString = ((int)material).ToString();
                var friendlyName = RecipeManager.GetMaterialName(material);
                if (filters.Contains(searchString))
                {
                    filters.Remove(searchString);
                    session.Player.SmartSalvageFilter = string.Join(",", filters);

                    CommandHandlerHelper.WriteOutputInfo(session, $"Removed {friendlyName} from the salvage filter.", ChatMessageType.Broadcast);
                    return;
                }
                CommandHandlerHelper.WriteOutputInfo(session, $"{friendlyName} is not included in the salvage filter.", ChatMessageType.Broadcast);
            }
            else if (param0 == "clear")
            {
                session.Player.SmartSalvageFilter = null;
                CommandHandlerHelper.WriteOutputInfo(session, $"All materials removed from the salvage filter.", ChatMessageType.Broadcast);
            }
            else
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Could not parse command, check \"/SmartSalvage help\" for usage instructions.", ChatMessageType.Broadcast);
                return;
            }
        }

        [CommandHandler("food", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Show the character's current hunger status.")]
        [CommandHandler("hunger", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "Show the character's current hunger status.")]
        public static void HandleHunger(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: food", ChatMessageType.Help));
                return;
            }

            var player = session.Player;

            var health = player.ExtraHealthRegenPool ?? 0;
            var stamina = player.ExtraStaminaRegenPool ?? 0;
            var mana = player.ExtraManaRegenPool ?? 0;
            var max = Creature.MaxRegenPoolValue;

            var healthFullness = "";
            if (health >= max)
                healthFullness = $"Completely Full";
            else if (health >= max * 0.75f)
                healthFullness = $"Pretty Full";
            else if (health >= max * 0.5f)
                healthFullness = $"Satiated";
            else if (health >= max * 0.25f)
                healthFullness = $"Hungry";
            else
                healthFullness = $"Very hungry";

            var staminaFullness = "";
            if (stamina >= max)
                staminaFullness = $"Completely Full";
            else if (stamina >= max * 0.75f)
                staminaFullness = $"Pretty Full";
            else if (stamina >= max * 0.5f)
                staminaFullness = $"Satiated";
            else if (stamina >= max * 0.25f)
                staminaFullness = $"Hungry";
            else
                staminaFullness = $"Very hungry";

            var manaFullness = "";
            if (mana >= max)
                manaFullness = $"Completely Full";
            else if (mana >= max * 0.75f)
                manaFullness = $"Pretty Full";
            else if (mana >= max * 0.5f)
                manaFullness = $"Satiated";
            else if (mana >= max * 0.25f)
                manaFullness = $"Hungry";
            else
                manaFullness = $"Very hungry";

            CommandHandlerHelper.WriteOutputInfo(session, $"Health Food: {healthFullness} ({health}/{max})", ChatMessageType.Broadcast);
            CommandHandlerHelper.WriteOutputInfo(session, $"Stamina Food: {staminaFullness} ({stamina}/{max})", ChatMessageType.Broadcast);
            CommandHandlerHelper.WriteOutputInfo(session, $"Mana Food: {manaFullness} ({mana}/{max})", ChatMessageType.Broadcast);
        }

        [CommandHandler("Exploration", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        [CommandHandler("Exp", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        public static void HandleExploration(Session session, params string[] parameters)
        {
            var player = session?.Player;

            if (player == null)
                return;

            var hasAssignments = false;
            var assignment1Complete = false;
            var assignment2Complete = false;
            var assignment3Complete = false;
            if (player.Exploration1LandblockId != 0 && player.Exploration1Description.Length > 0)
            {
                hasAssignments = true;
                assignment1Complete = player.Exploration1KillProgressTracker <= 0 && player.Exploration1MarkerProgressTracker <= 0;
            }
            if (player.Exploration2LandblockId != 0 && player.Exploration2Description.Length > 0)
            {
                hasAssignments = true;
                assignment2Complete = player.Exploration2KillProgressTracker <= 0 && player.Exploration2MarkerProgressTracker <= 0;
            }
            if (player.Exploration3LandblockId != 0 && player.Exploration3Description.Length > 0)
            {
                hasAssignments = true;
                assignment3Complete = player.Exploration3KillProgressTracker <= 0 && player.Exploration3MarkerProgressTracker <= 0;
            }

            if (!hasAssignments)
                CommandHandlerHelper.WriteOutputInfo(session, "No ongoing exploration assignments at the moment.");
            else
            {
                var msg1 = "";
                var msg2 = "";
                var msg3 = "";
                if (player.Exploration1LandblockId != 0 && player.Exploration1Description.Length > 0)
                    msg1 = $"{player.Exploration1Description} {(assignment1Complete ? "Complete!" : $"Kills remaining: {player.Exploration1KillProgressTracker} Markers remaining: {player.Exploration1MarkerProgressTracker}")}";
                if (player.Exploration2LandblockId != 0 && player.Exploration2Description.Length > 0)
                    msg2 = $"{player.Exploration2Description} {(assignment2Complete ? "Complete!" : $"Kills remaining: {player.Exploration2KillProgressTracker} Markers remaining: {player.Exploration2MarkerProgressTracker}")}";
                if (player.Exploration3LandblockId != 0 && player.Exploration3Description.Length > 0)
                    msg3 = $"{player.Exploration3Description} {(assignment3Complete ? "Complete!" : $"Kills remaining: {player.Exploration3KillProgressTracker} Markers remaining: {player.Exploration3MarkerProgressTracker}")}";

                var count = 0;
                CommandHandlerHelper.WriteOutputInfo(session, "Exploration Assignments:");
                if (msg1.Length > 0)
                {
                    count++;
                    CommandHandlerHelper.WriteOutputInfo(session, $"\n\n{count:N0}. {msg1}");
                }
                if (msg2.Length > 0)
                {
                    count++;
                    CommandHandlerHelper.WriteOutputInfo(session, $"\n\n{count:N0}. {msg2}");
                }
                if (msg3.Length > 0)
                {
                    count++;
                    CommandHandlerHelper.WriteOutputInfo(session, $"\n\n{count:N0}. {msg3}");
                }
            }
        }

        [CommandHandler("HotDungeon", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        [CommandHandler("Hot", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        public static void HandleHotDungeon(Session session, params string[] parameters)
        {
            ShowHotDungeon(session, false);
        }

        public static void ShowHotDungeon(Session session, bool failSilently, ulong discordChannel = 0)
        {
            if (EventManager.HotDungeonLandblock == 0)
            {
                if (!failSilently)
                {
                    var msg = "There's no dungeons providing extra experience rewards at the moment.";
                    if (discordChannel == 0)
                        CommandHandlerHelper.WriteOutputInfo(session, msg);
                    else
                        DiscordChatBridge.SendMessage(discordChannel, msg);
                }
            }
            else
            {
                var timeRemaining = TimeSpan.FromSeconds(EventManager.NextHotDungeonEnd - Time.GetUnixTime()).GetFriendlyString();
                var msg = $"{EventManager.HotDungeonDescription} Time Remaining: {timeRemaining}.";
                if (discordChannel == 0)
                    CommandHandlerHelper.WriteOutputInfo(session, msg);
                else
                    DiscordChatBridge.SendMessage(discordChannel, msg);
            }
        }


        [CommandHandler("FireSaleTown", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        [CommandHandler("FireSale", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, "")]
        public static void HandleFireSaleTown(Session session, params string[] parameters)
        {
            ShowFireSaleTown(session, false);
        }

        public static void ShowFireSaleTown(Session session, bool failSilently, ulong discordChannel = 0)
        {
            if (EventManager.FireSaleTownName == "")
            {
                if (!failSilently)
                {
                    var msg = "There's no towns with an ongoing fire sale at the moment.";
                    if (discordChannel == 0)
                        CommandHandlerHelper.WriteOutputInfo(session, msg);
                    else
                        DiscordChatBridge.SendMessage(discordChannel, msg);
                }
            }
            else
            {
                var timeRemaining = TimeSpan.FromSeconds(EventManager.NextFireSaleTownEnd - Time.GetUnixTime()).GetFriendlyString();
                var msg = $"{EventManager.FireSaleTownDescription} Time Remaining: {timeRemaining}.";
                if (discordChannel == 0)
                    CommandHandlerHelper.WriteOutputInfo(session, msg);
                else
                    DiscordChatBridge.SendMessage(discordChannel, msg);
            }
        }

        public struct LeaderboardEntry
        {
            public string Name;
            public int Level;
            public string KillerName;
            public int KillerLevel;
            public bool WasPvP;
            public int HardcoreKills;
            public int PKKills;
            public long XP;
            public bool Living;
            public bool isPK;
        }

        public static List<LeaderboardEntry> PrepareLeaderboard(GameplayModes gameplayMode, bool onlyLiving)
        {
            var living = PlayerManager.FindAllByGameplayMode(gameplayMode);

            var leaderboard = new List<LeaderboardEntry>();
            foreach (var entry in living)
            {
                if (entry.Account.AccessLevel > 0)
                    continue;

                var level = entry.GetProperty(PropertyInt.Level) ?? 1;

                var leaderboardEntry = new LeaderboardEntry();
                leaderboardEntry.Living = true;
                leaderboardEntry.Name = entry.GetProperty(PropertyString.Name);
                leaderboardEntry.Level = level;
                leaderboardEntry.XP = entry.GetProperty(PropertyInt64.TotalExperience) ?? 0;
                leaderboardEntry.HardcoreKills = entry.GetProperty(PropertyInt.PlayerKillsPkl) ?? 0;
                leaderboardEntry.PKKills = entry.GetProperty(PropertyInt.PlayerKillsPk) ?? 0;
                leaderboardEntry.isPK = entry.GetProperty(PropertyInt.PlayerKillerStatus) == (int)PlayerKillerStatus.PKLite || entry.GetProperty(PropertyInt.PlayerKillerStatus) == (int)PlayerKillerStatus.PK;

                leaderboard.Add(leaderboardEntry);
            }

            if (!onlyLiving)
            {
                var obituaryEntries = DatabaseManager.Shard.BaseDatabase.GetHardcoreDeathsByGameplayMode(gameplayMode);
                foreach (var entry in obituaryEntries)
                {
                    if (entry.GameplayMode == (int)gameplayMode)
                    {
                        var leaderboardEntry = new LeaderboardEntry();
                        leaderboardEntry.Living = false;
                        leaderboardEntry.Name = entry.CharacterName;
                        leaderboardEntry.Level = entry.CharacterLevel;
                        leaderboardEntry.KillerName = entry.KillerName;
                        leaderboardEntry.KillerLevel = entry.KillerLevel;
                        leaderboardEntry.WasPvP = entry.WasPvP;
                        leaderboardEntry.XP = entry.XP;
                        leaderboardEntry.HardcoreKills = entry.Kills;
                        leaderboardEntry.isPK = gameplayMode == GameplayModes.HardcorePK;

                        leaderboard.Add(leaderboardEntry);
                    }
                }
            }

            return leaderboard;
        }

        /// <summary>
        /// List top 10 Hardcore characters by total XP
        /// </summary>
        [CommandHandler("HCXP", AccessLevel.Player, CommandHandlerFlag.None, "List top 10 Hardcore characters by total XP.", "HCXP [pk|npk] [alltime|living]")]
        public static void HandleLeaderboardHCXP(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardHCXPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardHCXPCommandRequestTimestamp = DateTime.UtcNow;
            }

            bool IsPkLeaderboard = true;
            bool onlyLiving = false;
            foreach(var par in parameters)
            {
                if (par == "npk")
                    IsPkLeaderboard = false;
                else if(par == "living")
                    onlyLiving = true;
            }

            ulong discordChannel = 0;
            if (parameters.Length > 3 && parameters[2] == "discord")
                ulong.TryParse(parameters[3], out discordChannel);

            var leaderboard = PrepareLeaderboard(IsPkLeaderboard ? GameplayModes.HardcorePK : GameplayModes.HardcoreNPK, onlyLiving).OrderByDescending(b => b.XP).ToList();

            StringBuilder message = new StringBuilder();
            message.Append($"Hardcore {(IsPkLeaderboard ? "PK" : "NPK")} {(onlyLiving ? "Living" : "All-Time")} Characters by XP: \n");
            message.Append("-----------------------\n");
            uint playerCounter = 1;
            foreach(var entry in leaderboard)
            {
                var label = playerCounter < 10 ? $" {playerCounter}." : $"{playerCounter}.";
                var deathStatus = onlyLiving ? "" : $"{(entry.Living ? " - Living" : $" - Killed by {entry.KillerName}{(entry.KillerLevel > 0 && entry.WasPvP ? $"(Level {entry.KillerLevel})" : "")}")}";
                message.Append($"{label} {entry.Name} - Level {entry.Level}{deathStatus}\n");
                playerCounter++;

                if (playerCounter > 10)
                    break;
            }
            message.Append("-----------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        [CommandHandler("HCTopNPC", AccessLevel.Player, CommandHandlerFlag.None, "List top 10 NPCs by total kills.", "HCTopNPC [minLevel] [maxLevel]")]
        public static void HandleLeaderboardHCTopNPC(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardHCXPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardHCXPCommandRequestTimestamp = DateTime.UtcNow;
            }

            int minLevel = 1;
            if (parameters.Length > 0)
                int.TryParse(parameters[0], out minLevel);

            int maxLevel = 275;
            if (parameters.Length > 1)
                int.TryParse(parameters[1], out maxLevel);

            ulong discordChannel = 0;
            if (parameters.Length > 3 && parameters[2] == "discord")
                ulong.TryParse(parameters[3], out discordChannel);

            var leaderboard = new Dictionary<string, int>();
            var obituaryEntries = DatabaseManager.Shard.BaseDatabase.GetHardcoreDeaths();
            foreach (var entry in obituaryEntries)
            {
                if (entry.CharacterLevel < minLevel || entry.CharacterLevel > maxLevel)
                    continue;

                if (!entry.WasPvP)
                {
                    if (!leaderboard.TryGetValue(entry.KillerName, out var kills))
                        leaderboard.Add(entry.KillerName, 1);
                    else
                        leaderboard[entry.KillerName]++;
                }
            }

            var sorted = from entry in leaderboard orderby entry.Value descending select entry;

            StringBuilder message = new StringBuilder();
            message.Append($"Top Hardcore Character Killers - Levels {minLevel} to {maxLevel}:\n");
            message.Append("-----------------------\n");
            uint counter = 1;
            foreach (var entry in sorted)
            {
                var label = counter < 10 ? $" {counter}." : $"{counter}.";
                message.Append($"{label} {entry.Key} - {entry.Value} kill{(entry.Value != 1 ? "s" : "")}\n");
                counter++;

                if (counter > 10)
                    break;
            }
            message.Append("-----------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        /// <summary>
        /// List top 10 Solo Self-Found characters by total XP
        /// </summary>
        [CommandHandler("TopSSF", AccessLevel.Player, CommandHandlerFlag.None, "List top 10 Solo Self-Found characters by total XP.", "TopSSF")]
        public static void HandleLeaderboardSSF(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardSSFCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardSSFCommandRequestTimestamp = DateTime.UtcNow;
            }

            ulong discordChannel = 0;
            if (parameters.Length > 1 && parameters[0] == "discord")
                ulong.TryParse(parameters[1], out discordChannel);

            var leaderboard = PrepareLeaderboard(GameplayModes.SoloSelfFound, true).OrderByDescending(b => b.XP).ToList();

            StringBuilder message = new StringBuilder();
            message.Append($"Solo Self-Found Characters by XP: \n");
            message.Append("-----------------------\n");
            uint playerCounter = 1;
            foreach (var entry in leaderboard)
            {
                if (entry.XP > 0)
                {
                    var label = playerCounter < 10 ? $" {playerCounter}." : $"{playerCounter}.";
                    message.Append($"{label} {entry.Name} - Level {entry.Level}\n");
                    playerCounter++;

                    if (playerCounter > 10)
                        break;
                }
            }
            message.Append("-----------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        /// <summary>
        /// List top 10 characters by total XP
        /// </summary>
        [CommandHandler("TopXP", AccessLevel.Player, CommandHandlerFlag.None, "List top 10 characters by total XP.", "TopXP")]
        public static void HandleLeaderboardLevel(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardXPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardXPCommandRequestTimestamp = DateTime.UtcNow;
            }

            ulong discordChannel = 0;
            if (parameters.Length > 1 && parameters[0] == "discord")
                ulong.TryParse(parameters[1], out discordChannel);

            var leaderboard = PrepareLeaderboard(GameplayModes.Regular, true).OrderByDescending(b => b.XP).ToList();

            StringBuilder message = new StringBuilder();
            message.Append($"Top Characters by XP: \n");
            message.Append("-----------------------\n");
            uint playerCounter = 1;
            foreach (var entry in leaderboard)
            {
                if (entry.XP > 0)
                {
                    var label = playerCounter < 10 ? $" {playerCounter}." : $"{playerCounter}.";
                    var pkStatus = entry.isPK ? "(PK)" : "";
                    message.Append($"{label} {entry.Name} - Level {entry.Level}{pkStatus}\n");
                    playerCounter++;

                    if (playerCounter > 10)
                        break;
                }
            }
            message.Append("-----------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        /// <summary>
        /// List top 10 characters by total kills
        /// </summary>
        [CommandHandler("TopPvP", AccessLevel.Player, CommandHandlerFlag.None, "List top 10 characters by total kills.", "TopPvP")]
        public static void HandleLeaderboardPvP(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardPvPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardPvPCommandRequestTimestamp = DateTime.UtcNow;
            }

            ulong discordChannel = 0;
            if (parameters.Length > 1 && parameters[0] == "discord")
                ulong.TryParse(parameters[1], out discordChannel);

            var leaderboard = PrepareLeaderboard(GameplayModes.Regular, true).OrderByDescending(b => b.XP).ToList();

            StringBuilder message = new StringBuilder();
            message.Append($"Top Characters by Kills: \n");
            message.Append("-----------------------\n");
            uint playerCounter = 1;
            foreach (var entry in leaderboard)
            {
                if (entry.PKKills > 0)
                {
                    var label = playerCounter < 10 ? $" {playerCounter}." : $"{playerCounter}.";
                    var pkStatus = entry.isPK ? "(PK)" : "";
                    message.Append($"{label} {entry.Name} - Level {entry.Level}{pkStatus} - {entry.PKKills} kill{(entry.PKKills != 1 ? "s" : "")}\n");
                    playerCounter++;

                    if (playerCounter > 10)
                        break;
                }
            }
            message.Append("-----------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        /// <summary>
        /// Reports on the top 10 Hardcore characters by PvP kills
        /// </summary>
        [CommandHandler("HCPvP", AccessLevel.Player, CommandHandlerFlag.None, "Reports on the top 10 Hardcore characters by PvP kills.", "HCPvP [alltime|living]")]
        public static void HandleLeaderboardHCPvP(Session session, params string[] parameters)
        {
            if (session != null)
            {
                if (session.AccessLevel == AccessLevel.Player && DateTime.UtcNow - session.Player.PrevLeaderboardHCPvPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat("You have used this command too recently!", ChatMessageType.Broadcast));
                    return;
                }
                session.Player.PrevLeaderboardHCPvPCommandRequestTimestamp = DateTime.UtcNow;
            }

            bool onlyLiving = false;
            if (parameters.Length > 0 && parameters[0] == "living")
                onlyLiving = true;

            ulong discordChannel = 0;
            if (parameters.Length > 2 && parameters[1] == "discord")
                ulong.TryParse(parameters[2], out discordChannel);

            var living = PlayerManager.FindAllByGameplayMode(GameplayModes.HardcorePK);

            var leaderboard = PrepareLeaderboard(GameplayModes.HardcorePK, onlyLiving).OrderByDescending(b => b.HardcoreKills).ToList();

            leaderboard = leaderboard.OrderByDescending(b => b.HardcoreKills).ToList();

            StringBuilder message = new StringBuilder();
            message.Append($"Hardcore {(onlyLiving ? "Living" : "All-Time")} PvP Leaderboard:\n");
            message.Append("-------------------------\n");
            uint playerCounter = 1;
            foreach (var entry in leaderboard)
            {
                if (entry.HardcoreKills > 0)
                {
                    var label = playerCounter < 10 ? $" {playerCounter}." : $"{playerCounter}.";
                    var deathStatus = onlyLiving ? "" : $"{(entry.Living ? " - Living" : $" - Killed by {entry.KillerName}{(entry.KillerLevel > 0 && entry.WasPvP ? $"(Level {entry.KillerLevel})" : "")}")}";
                    message.Append($"{label} {entry.Name} - Level {entry.Level} - {entry.HardcoreKills} kill{(entry.HardcoreKills != 1 ? "s" : "")}{deathStatus}\n");
                    playerCounter++;

                    if (playerCounter > 10)
                        break;
                }
            }
            message.Append("-------------------------\n");

            if (discordChannel == 0)
                CommandHandlerHelper.WriteOutputInfo(session, message.ToString(), ChatMessageType.Broadcast);
            else
                DiscordChatBridge.SendMessage(discordChannel, $"`{message.ToString()}`");
        }

        [CommandHandler("OfflineSwear", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Swear allegiance to an offline character on the same account.", "OfflineSwear <PatronName>")]
        public static void HandleOfflineSwear(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: OfflineSwear", ChatMessageType.Help));
                return;
            }

            var patronName = string.Join(" ", parameters);

            var onlinePlayer = PlayerManager.GetOnlinePlayer(patronName);
            var offlinePlayer = PlayerManager.GetOfflinePlayer(patronName, false);
            if (onlinePlayer != null)
            {
                if (onlinePlayer.Account.AccountId != session.AccountId)
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"The target character must be on the same account as this character!");
                    return;
                }
                else
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"That character is not offline!");
                    return;
                }
            }
            else if (offlinePlayer == null)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"Could not find a character with that name!");
                return;
            }
            else if (offlinePlayer.Account.AccountId != session.AccountId)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"The target character must be on the same account as this character!");
                return;
            }
            else if (offlinePlayer.IsPendingDeletion || offlinePlayer.IsDeleted)
            {
                CommandHandlerHelper.WriteOutputInfo(session, $"The target must not be a deleted character!");
                return;
            }
            else
                session.Player.OfflineSwearAllegiance(offlinePlayer.Guid.Full);
        }

        [CommandHandler("AutoFillComps", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Automatically adds components to fillcomps according to the current spellbook.", "AutoFillComps <Amount Multiplier|Clear>")]
        public static void HandleAutoFillComps(Session session, params string[] parameters)
        {
            var player = session.Player;
            if (player == null)
                return;

            if (parameters[0].ToLower() == "clear")
            {
                player.Character.ClearFillComponents(player.CharacterDatabaseLock);
            }
            else
            {
                if (!int.TryParse(parameters[0], out var multiplier))
                {
                    CommandHandlerHelper.WriteOutputInfo(session, $"The syntax of the command is incorrect.\nUsage: @AutoFillComps <Amount Multiplier|Clear>");
                    return;
                }

                multiplier = Math.Clamp(multiplier, 0, 5000);

                foreach (var spell in player.Biota.GetKnownSpellsIds(player.BiotaDatabaseLock))
                {
                    Spell spellEntity = new Spell(spell);
                    foreach (var componentId in spellEntity.Formula.Components)
                    {
                        int amount = Math.Min(10 * multiplier, 5000);
                        if (SpellFormula.SpellComponentsTable.SpellComponents.TryGetValue(componentId, out var spellComponent))
                        {
                            if (spellComponent.Type == (int)SpellComponentsTable.Type.Scarab || spellComponent.Type == (int)SpellComponentsTable.Type.Talisman)
                                amount = Math.Min(3 * multiplier, 5000);

                            var compWcid = Spell.GetComponentWCID(componentId);
                            if (SpellComponent.IsValid(compWcid))
                            {
                                player.Character.TryRemoveFillComponent(compWcid, out _, player.CharacterDatabaseLock);
                                player.Character.AddFillComponent(compWcid, (uint)amount, player.CharacterDatabaseLock, out _);
                            }
                        }
                    }
                }
            }

            CommandHandlerHelper.WriteOutputInfo(session, $"Fillcomps values updated. Please relog to see the updated values.");
        }

        [CommandHandler("Where", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Shows information about your current location")]
        public static void HandleWhere(Session session, params string[] parameters)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat($"Unknown command: Where", ChatMessageType.Help));
                return;
            }

            var player = session.Player;
            if (player == null)
                return;

            var landblockDescription = DatabaseManager.World.GetLandblockDescriptionsByLandblock(player.CurrentLandblock.Id.Landblock).FirstOrDefault();

            if(landblockDescription != null)
                CommandHandlerHelper.WriteOutputInfo(session, $"Current Location: {landblockDescription.Name}\nDirections: {landblockDescription.Directions}\nReference: {landblockDescription.Reference}\nMacro Region: {landblockDescription.MacroRegion}\nMicro Region: {landblockDescription.MicroRegion}");
            else
                CommandHandlerHelper.WriteOutputInfo(session, $"You are at an unknown location.");
        }
    }
}
