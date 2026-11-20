using System.IO;

namespace ACE.DatLoader.Entity
{
    /// <summary>
    /// I'm not quite sure what a "Stab" is, but this is what the client calls these.
    /// It is an object and a corresponding position. 
    /// Note that since these are referenced by either a landblock or a cellblock, the corresponding Landblock and Cell should come from the parent.
    /// </summary>
    public class Stab : IUnpackable, IPackable
    {
        public uint Id { get; set; }
        public Frame Frame { get; } = new Frame();

        public void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            Frame.Unpack(reader);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            Frame.Pack(writer);
        }
    }
}
