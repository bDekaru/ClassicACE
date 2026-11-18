using ACE.Entity.Enum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ACE.DatLoader.Entity
{
    public class ElementDesc : StateDesc
    {
        public uint UiReadOrder;

        // Enum names for these are in EnumMapper 0x2200001B (UIElementID)
        public uint ElementId; 
        public uint Type;
        public uint BaseElement;
        public uint BaseLayout;
        public uint DefaultState;

        public uint X;
        public uint Y;
        public uint Width;
        public uint Height;
        public uint ZLevel;
        public uint LeftEdge;
        public uint TopEdge;
        public uint RightEdge;
        public uint BottomEdge;

        public byte StatesBucketSize; // first byte of each packed table, kept so that a record is written back as it was read
        public byte ChildrenBucketSize;

        public Dictionary<uint, StateDesc> States = new Dictionary<uint, StateDesc>();
        public Dictionary<uint, ElementDesc> Children = new Dictionary<uint, ElementDesc>();

        public override void Unpack(BinaryReader reader, Func<uint, BasePropertyType> typeLookup)
        {
            base.Unpack(reader, typeLookup);
            UnpackElementDesc(reader, typeLookup);
        }

        public void UnpackElementDesc(BinaryReader reader)
        {
            UnpackElementDesc(reader, BaseProperty.DefaultTypeLookup);
        }

        public void UnpackElementDesc(BinaryReader reader, Func<uint, BasePropertyType> typeLookup)
        {
            UiReadOrder = reader.ReadUInt32();
            ElementId = reader.ReadUInt32();
            Type = reader.ReadUInt32();
            BaseElement = reader.ReadUInt32();
            BaseLayout = reader.ReadUInt32();
            DefaultState = reader.ReadUInt32();

            if ((UiIncorporationFlags & IncorporationFlags.X) != 0)
                X = reader.ReadUInt32();
            if ((UiIncorporationFlags & IncorporationFlags.Y) != 0)
                Y = reader.ReadUInt32();
            if ((UiIncorporationFlags & IncorporationFlags.Width) != 0)
                Width = reader.ReadUInt32();
            if ((UiIncorporationFlags & IncorporationFlags.Height) != 0)
                Height = reader.ReadUInt32();
            if ((UiIncorporationFlags & IncorporationFlags.ZLevel) != 0)
                ZLevel = reader.ReadUInt32();

            LeftEdge = reader.ReadUInt32();
            TopEdge = reader.ReadUInt32();
            RightEdge = reader.ReadUInt32();
            BottomEdge = reader.ReadUInt32();

            StatesBucketSize = reader.ReadByte();
            var totalObjects = reader.ReadByte();
            for (int i = 0; i < totalObjects; i++)
            {
                var key = reader.ReadUInt32();

                var item = new StateDesc();
                item.Unpack(reader, typeLookup);
                States.Add(key, item);
            }

            ChildrenBucketSize = reader.ReadByte();
            var totalChildren = reader.ReadByte();
            for (int i = 0; i < totalChildren; i++)
            {
                var key = reader.ReadUInt32();

                var item = new ElementDesc();
                item.Unpack(reader, typeLookup);
                Children.Add(key, item);
            }

        }

        public override void Pack(BinaryWriter writer)
        {
            base.Pack(writer);
            PackElementDesc(writer);
        }

        public void PackElementDesc(BinaryWriter writer)
        {
            writer.Write(UiReadOrder);
            writer.Write(ElementId);
            writer.Write(Type);
            writer.Write(BaseElement);
            writer.Write(BaseLayout);
            writer.Write(DefaultState);

            if ((UiIncorporationFlags & IncorporationFlags.X) != 0)
                writer.Write(X);
            if ((UiIncorporationFlags & IncorporationFlags.Y) != 0)
                writer.Write(Y);
            if ((UiIncorporationFlags & IncorporationFlags.Width) != 0)
                writer.Write(Width);
            if ((UiIncorporationFlags & IncorporationFlags.Height) != 0)
                writer.Write(Height);
            if ((UiIncorporationFlags & IncorporationFlags.ZLevel) != 0)
                writer.Write(ZLevel);

            writer.Write(LeftEdge);
            writer.Write(TopEdge);
            writer.Write(RightEdge);
            writer.Write(BottomEdge);

            writer.Write(StatesBucketSize);
            writer.Write((byte)States.Count);
            foreach (var state in States)
            {
                writer.Write(state.Key);
                state.Value.Pack(writer);
            }

            writer.Write(ChildrenBucketSize);
            writer.Write((byte)Children.Count);
            foreach (var child in Children)
            {
                writer.Write(child.Key);
                child.Value.Pack(writer);
            }
        }
    }
}
