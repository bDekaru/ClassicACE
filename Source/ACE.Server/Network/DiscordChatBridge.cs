using System;
using System.Text;
using System.Threading.Tasks;
using ACE.Server.Managers;
using log4net;

using Discord;
using Discord.WebSocket;
using ACE.Server.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using System.Linq;
using ACE.Server.Command.Handlers;
using HarmonyLib;

namespace ACE.Server.Network
{    public static class DiscordChatBridge
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static DiscordSocketClient DiscordClient = null;
        public static bool IsRunning { get; private set; }

        public static DateTime PrevLeaderboardHCXPCommandRequestTimestamp;
        public static DateTime PrevLeaderboardHCPvPCommandRequestTimestamp;
        public static DateTime PrevLeaderboardSSFCommandRequestTimestamp;
        public static DateTime PrevLeaderboardXPCommandRequestTimestamp;
        public static DateTime PrevLeaderboardPvPCommandRequestTimestamp;
        public static DateTime PrevLeaderboardHCTopNPCCommandRequestTimestamp;

        public static async void Start()
        {
            if (IsRunning)
                return;

            if (string.IsNullOrWhiteSpace(PropertyManager.GetString("discord_login_token").Item) || PropertyManager.GetLong("discord_channel_id").Item == 0)
                return;

            var config = new DiscordSocketConfig();
            config.GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent;
            config.GatewayIntents ^= GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites;

            DiscordClient = new DiscordSocketClient(config);

            DiscordClient.Log += DiscordLogMessageReceived;
            DiscordClient.MessageReceived += DiscordMessageReceived;

            await DiscordClient.LoginAsync(TokenType.Bot, PropertyManager.GetString("discord_login_token").Item);
            await DiscordClient.StartAsync();

            IsRunning = true;
        }

        public static async void Stop()
        {
            if (!IsRunning || DiscordClient == null)
                return;

            await DiscordClient.LogoutAsync();
            await DiscordClient.StopAsync();

            IsRunning = false;
        }

        public static Task SendMessage(ulong channelId, string message)
        {
            if (!IsRunning || DiscordClient == null)
                return Task.CompletedTask;

            var channel = DiscordClient.GetChannel(channelId) as IMessageChannel;
            if(channel != null)
                channel.SendMessageAsync(message);
            return Task.CompletedTask;
        }

        private static Task DiscordMessageReceived(SocketMessage messageParam)
        {
            try
            {
                // Don't process the command if it was a system message
                var message = messageParam as SocketUserMessage;
                if (message == null)
                    return Task.CompletedTask;

                var messageText = message.CleanContent;
                if (messageText.StartsWith("/") || messageText.StartsWith("!"))
                {
                    var splitString = messageText.Split(" ");
                    string[] parameters;
                    if (splitString.Length > 0)
                    {
                        var command = splitString[0].Substring(1).ToLower();
                        switch (command)
                        {
                            case "hcxp":
                                if (DateTime.UtcNow - PrevLeaderboardHCXPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardHCXPCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).Take(2).ToArray();
                                if (parameters.Length == 0)
                                    parameters = parameters.AddToArray("pk");
                                if (parameters.Length == 1)
                                    parameters = parameters.AddToArray("alltime");
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardHCXP(null, parameters);
                                return Task.CompletedTask;

                            case "hcpvp":
                                if (DateTime.UtcNow - PrevLeaderboardHCPvPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardHCPvPCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).Take(1).ToArray();
                                if (parameters.Length == 0)
                                    parameters = parameters.AddToArray("alltime");
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardHCPvP(null, parameters);
                                return Task.CompletedTask;

                            case "topssf":
                                if (DateTime.UtcNow - PrevLeaderboardSSFCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardSSFCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).ToArray();
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardSSF(null, parameters);
                                return Task.CompletedTask;

                            case "topxp":
                                if (DateTime.UtcNow - PrevLeaderboardXPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardXPCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).ToArray();
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardLevel(null, parameters);
                                return Task.CompletedTask;

                            case "toppvp":
                                if (DateTime.UtcNow - PrevLeaderboardPvPCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardPvPCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).ToArray();
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardPvP(null, parameters);
                                return Task.CompletedTask;

                            case "hctopnpc":
                                if (DateTime.UtcNow - PrevLeaderboardHCTopNPCCommandRequestTimestamp < TimeSpan.FromMinutes(1))
                                {
                                    SendMessage(message.Channel.Id, $"This command was used too recently. Please try again later.");
                                    return Task.CompletedTask;
                                }
                                PrevLeaderboardHCTopNPCCommandRequestTimestamp = DateTime.UtcNow;

                                parameters = splitString.Skip(1).Take(2).ToArray();
                                if (parameters.Length == 0)
                                    parameters = parameters.AddToArray("1");
                                if (parameters.Length == 1)
                                    parameters = parameters.AddToArray("275");
                                parameters = parameters.AddToArray("discord");
                                parameters = parameters.AddToArray(message.Channel.Id.ToString());

                                PlayerCommands.HandleLeaderboardHCTopNPC(null, parameters);
                                return Task.CompletedTask;

                            case "hot":
                                PlayerCommands.ShowHotDungeon(null, false, message.Channel.Id);
                                return Task.CompletedTask;

                            case "pop":
                                SendMessage(message.Channel.Id, $"Current world population: {PlayerManager.GetOnlineCount():N0}");
                                return Task.CompletedTask;
                        }
                    }
                    return Task.CompletedTask;
                }

                if (!PropertyManager.GetBool("show_discord_chat_ingame").Item || message.Author.IsBot || message.Channel.Id != (ulong)PropertyManager.GetLong("discord_channel_id").Item)
                    return Task.CompletedTask;

                if (message.Author is SocketGuildUser author)
                {
                    var authorName = author.DisplayName;
                    authorName = authorName.Normalize(NormalizationForm.FormKC);

                    var validLetters = "";
                    foreach(char letter in authorName)
                    {
                        if ((letter >= 32 && letter <= 126) || (letter >= 160 && letter <= 383)) //Basic Latin + Latin-1 Supplement + Latin Extended-A
                            validLetters += letter;
                    }
                    authorName = validLetters;

                    authorName = authorName.Trim();
                    authorName = authorName.TrimStart('+');
                    authorName = authorName.Trim();
                    authorName = authorName.TrimStart('+');

                    if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(messageText))
                    {
                        messageText = messageText.Replace("\n", " ");
                        messageText.Trim();
                        if (!string.IsNullOrWhiteSpace(messageText))
                        {
                            if (messageText.Length > 256)
                                messageText = messageText.Substring(0, 250) + "[...]";

                            authorName = $"[Discord] {authorName}";
                            foreach (var recipient in PlayerManager.GetAllOnline())
                            {
                                if (!recipient.GetCharacterOption(CharacterOption.ListenToGeneralChat))
                                    continue;

                                if (recipient.IsOlthoiPlayer)
                                    continue;

                                var gameMessageTurbineChat = new GameMessageTurbineChat(ChatNetworkBlobType.NETBLOB_EVENT_BINARY, ChatNetworkBlobDispatchType.ASYNCMETHOD_SENDTOROOMBYNAME, TurbineChatChannel.General, authorName, messageText, 0, ChatType.General);
                                recipient.Session.Network.EnqueueSend(gameMessageTurbineChat);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"[DISCORD] Error handling Discord message. Ex: {ex}");
            }

            return Task.CompletedTask;
        }
        private static Task DiscordLogMessageReceived(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    log.Error($"[DISCORD] ({msg.Severity}) {msg.Exception} {msg.Message}");
                    break;
                case LogSeverity.Warning:
                    log.Warn($"[DISCORD] ({msg.Severity}) {msg.Exception} {msg.Message}");
                    break;
                case LogSeverity.Info:
                    log.Info($"[DISCORD] {msg.Message}");
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
