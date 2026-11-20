using System.IO;

namespace ACE.DatLoader.Entity
{
    public class ChatEmoteData : IUnpackable, IPackable
    {
        public string MyEmote; // What the emote string is to the character doing the emote
        public string OtherEmote; // What the emote string is to other characters

        public void Unpack(BinaryReader reader)
        {
            MyEmote = reader.ReadPString(); reader.AlignBoundary();
            OtherEmote = reader.ReadPString(); reader.AlignBoundary();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WritePString(MyEmote); writer.AlignBoundary();
            writer.WritePString(OtherEmote); writer.AlignBoundary();
        }
    }
}
