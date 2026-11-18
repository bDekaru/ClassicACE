using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE.DatLoader.Entity
{
    public class MediaDesc : IUnpackable
    {
        public MediaType Type { get; set; }

        // The second UInt32 of every media item; it is the media type again in the records seen.
        public uint TypeAgain;

        // We could break all these properties into MediaDesc inherted types...but as this is just for reference, this is easier.
        public string FileName;
        public bool StretchToFullScreen;

        public uint File;

        public float Duration;
        public uint DrawMode;
        public List<uint> Frames;
        public uint JumpItemIndex;

        public uint XHotspot;
        public uint YHotspot;
        public uint MessageId;
        public float Probability;

        public float MinDuration;
        public float MaxDuration;
        public uint SType;
        public uint StateId;
        public float StartAlpha;
        public float EndAlpha;


        public virtual void Unpack(BinaryReader reader)
        {
            Type = (MediaType)reader.ReadUInt32();

            TypeAgain = reader.ReadUInt32();
            switch (Type)
            {
                case MediaType.Movie:
                    FileName = reader.ReadPString(1);
                    StretchToFullScreen = reader.ReadBoolean();
                    break;
                case MediaType.Alpha:
                    File = reader.ReadUInt32();
                    break;
                case MediaType.Animation:
                    Duration = reader.ReadSingle();
                    DrawMode = reader.ReadUInt32();
                    var numFrames = reader.ReadUInt32();
                    Frames = new List<uint>();
                    for(var i =0; i < numFrames; i++)
                    {
                        Frames.Add(reader.ReadUInt32());
                    }
                    break;
                case MediaType.Cursor:
                    File = reader.ReadUInt32();
                    XHotspot = reader.ReadUInt32();
                    YHotspot = reader.ReadUInt32();
                    break;
                case MediaType.Image:
                    File = reader.ReadUInt32();
                    DrawMode = reader.ReadUInt32();
                    break;
                case MediaType.Jump:
                    JumpItemIndex = reader.ReadUInt32();
                    Probability = reader.ReadSingle();
                    break;
                case MediaType.Message:
                    MessageId = reader.ReadUInt32();
                    Probability = reader.ReadSingle();
                    break;
                case MediaType.Pause:
                    MinDuration = reader.ReadSingle();
                    MaxDuration = reader.ReadSingle();
                    break;
                case MediaType.Sound:
                    File = reader.ReadUInt32();
                    SType = reader.ReadUInt32();
                    break;
                case MediaType.State:
                    StateId = reader.ReadUInt32();
                    Probability = reader.ReadSingle();
                    break;
                case MediaType.Fade:
                    StartAlpha = reader.ReadSingle();
                    EndAlpha = reader.ReadSingle();
                    Duration = reader.ReadSingle();
                    break;
            }
        }

        public virtual void Pack(BinaryWriter writer)
        {
            writer.Write((uint)Type);
            writer.Write(TypeAgain);

            switch (Type)
            {
                case MediaType.Movie:
                    writer.WritePString1(FileName);
                    writer.Write(StretchToFullScreen);
                    break;
                case MediaType.Alpha:
                    writer.Write(File);
                    break;
                case MediaType.Animation:
                    writer.Write(Duration);
                    writer.Write(DrawMode);
                    writer.Write((uint)Frames.Count);
                    foreach (var frame in Frames)
                        writer.Write(frame);
                    break;
                case MediaType.Cursor:
                    writer.Write(File);
                    writer.Write(XHotspot);
                    writer.Write(YHotspot);
                    break;
                case MediaType.Image:
                    writer.Write(File);
                    writer.Write(DrawMode);
                    break;
                case MediaType.Jump:
                    writer.Write(JumpItemIndex);
                    writer.Write(Probability);
                    break;
                case MediaType.Message:
                    writer.Write(MessageId);
                    writer.Write(Probability);
                    break;
                case MediaType.Pause:
                    writer.Write(MinDuration);
                    writer.Write(MaxDuration);
                    break;
                case MediaType.Sound:
                    writer.Write(File);
                    writer.Write(SType);
                    break;
                case MediaType.State:
                    writer.Write(StateId);
                    writer.Write(Probability);
                    break;
                case MediaType.Fade:
                    writer.Write(StartAlpha);
                    writer.Write(EndAlpha);
                    writer.Write(Duration);
                    break;
            }
        }
    }
}
