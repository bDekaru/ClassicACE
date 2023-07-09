using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using ACE.Server.Managers;
using log4net;

using Discord;
using Discord.WebSocket;
using ACE.Server.Entity;
using ACE.Entity.Enum;
using ACE.Server.Network.GameMessages.Messages;
using System.Text.RegularExpressions;
using System.Linq;
using System.Globalization;

namespace ACE.Server.Network
{    public static class DiscordChatBridge
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static DiscordSocketClient DiscordClient = null;
        public static bool IsRunning { get; private set; }

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
            if (!IsRunning)
                return;

            await DiscordClient.LogoutAsync();
            await DiscordClient.StopAsync();

            IsRunning = false;
        }

        private static Task DiscordMessageReceived(SocketMessage messageParam)
        {
            try
            {
                // Don't process the command if it was a system message
                var message = messageParam as SocketUserMessage;
                if (message == null)
                    return Task.CompletedTask;

                if (message.Author.IsBot || message.Channel.Id != (ulong)PropertyManager.GetLong("discord_channel_id").Item)
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

                    var messageText = message.CleanContent;

                    if (messageText.Length > 256)
                        messageText = messageText.Substring(0, 250) +"[...]";

                    if (!string.IsNullOrWhiteSpace(authorName) && !string.IsNullOrWhiteSpace(messageText))
                    {
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
