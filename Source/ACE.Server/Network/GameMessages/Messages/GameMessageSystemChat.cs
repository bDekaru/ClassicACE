using ACE.Entity.Enum;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageSystemChat : GameMessage
    {
        public string Message;
        public ChatMessageType ChatMessageType;
        public GameMessageSystemChat(string message, ChatMessageType chatMessageType)
            : base(GameMessageOpcode.ServerMessage, GameMessageGroup.UIQueue)
        {
            Message = message;
            ChatMessageType = chatMessageType;

            Writer.WriteString16L(message);
            Writer.Write((int)chatMessageType);
        }
    }
}
