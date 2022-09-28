using ACE.DatLoader;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.GameMessages.Messages;
using log4net;
using System;
using System.Collections.Generic;

namespace ACE.Server.Network.Handlers
{
    public static class DDDHandler
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static IEnumerable<object> ItersWIthKeys { get; private set; }

        [GameMessage(GameMessageOpcode.DDD_InterrogationResponse, SessionState.AuthConnected)]
        public static void DDD_InterrogationResponse(ClientMessage message, Session session)
        {
            if (PropertyManager.GetBool("show_dat_warning").Item)
            {
                message.Payload.ReadUInt32(); // m_ClientLanguage

                var ItersWithKeys = CAllIterationList.Read(message.Payload);
                // var ItersWithoutKeys = CAllIterationList.Read(message.Payload); // Not seen this populated in any pcap.
                // message.Payload.ReadUInt32(); // m_dwFlags - We don't need this

                foreach (var entry in ItersWithKeys.Lists)
                {
                    switch (entry.DatFileId)
                    {
                        case 1: // PORTAL
                            if (entry.Ints[0] != DatManager.PortalDat.Iteration)
                                session.DatWarnPortal = true;
                            break;
                        case 2: // CELL
                            if (entry.Ints[0] != DatManager.CellDat.Iteration)
                                session.DatWarnCell = true;
                            break;
                        case 3: // LANGUAGE
                            if (entry.Ints[0] != DatManager.LanguageDat.Iteration)
                                session.DatWarnLanguage = true;
                            break;
                    }
                }

                if (session.DatWarnPortal || session.DatWarnCell || session.DatWarnLanguage)
                {
                    session.Terminate(SessionTerminationReason.ClientOutOfDate, new GameMessageBootAccount(" because you do not have the correct data files for this server"));
                    return;
                }
            }

            GameMessageDDDEndDDD patchStatusMessage = new GameMessageDDDEndDDD();
            session.Network.EnqueueSend(patchStatusMessage);
        }

        [GameMessage(GameMessageOpcode.DDD_EndDDD, SessionState.AuthConnected)]
        public static void DDD_EndDDD(ClientMessage message, Session session)
        {
            // We don't need to reply to this message.
        }

        [GameMessage(GameMessageOpcode.DDD_RequestDataMessage, SessionState.WorldConnected)]
        public static void DDD_RequestDataMessage(ClientMessage message, Session session)
        {
            if (!PropertyManager.GetBool("show_dat_warning").Item) return;

            // True DAT patching would be triggered by this msg, but as we're not supporting that, respond instead with warning and push to external download

            var resourceType = message.Payload.ReadUInt32();
            var dataId = message.Payload.ReadUInt32();
            var errorType = 1u; // unknown enum... this seems to trigger reattempt request by client.
            var dddErrorMsg = new GameMessageDDDErrorMessage(resourceType, dataId, errorType);

            var currentTime = DateTime.UtcNow;
            if (currentTime - session.LastDDDTime < Session.DDDInterval) // Let's keep things sane otherwise the server will spam the client with warning messages.
            {
                session.Network.EnqueueSend(dddErrorMsg);
                return;
            }
            session.LastDDDTime = currentTime;

            var msg = PropertyManager.GetString("dat_warning_msg").Item;
            var popupMsg = new GameEventPopupString(session, msg);
            var chatMsg = new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast);
            var transientMsg = new GameEventCommunicationTransientString(session, msg);

            session.Network.EnqueueSend(popupMsg, chatMsg, transientMsg, dddErrorMsg);
            if (PropertyManager.GetBool("enforce_player_movement").Item && session.Player.FirstEnterWorldDone)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(5.0f);
                actionChain.AddAction(session.Player, () =>
                {
                    if (session != null && session.Player != null)
                        session.Player.Teleport(session.Player.SnapPos);
                });
                actionChain.EnqueueChain();
            }
            else
            {
                // send to lifestone, or fallback location
                var fixLoc = session.Player.Sanctuary ?? new Position(0xA9B40019, 84, 7.1f, 94, 0, 0, -0.0784591f, 0.996917f);

                log.Error($"DDD_RequestDataMessage received for {session.Player.Name}, relocating to {fixLoc.ToLOCString()}");

                session.Player.Location = new Position(fixLoc);
                LandblockManager.AddObject(session.Player, true);

                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(5.0f);
                actionChain.AddAction(session.Player, () =>
                {
                    if (session != null && session.Player != null)
                        session.Player.Teleport(fixLoc);
                });
                actionChain.EnqueueChain();
            }
        }
    }
}
