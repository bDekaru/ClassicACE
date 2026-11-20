using System.Collections.Generic;
using System.IO;

using ACE.DatLoader.Entity;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// These are client_portal.dat files starting with 0x11. 
    /// Contains info on what objects to display at what distance to help with render performance (e.g. low-poly very far away, but high-poly when close)
    /// </summary>
    [DatFileType(DatFileType.DegradeInfo)]
    public class GfxObjDegradeInfo : FileType
    {
        public List<GfxObjInfo> Degrades { get; } = new List<GfxObjInfo>();

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();

            Degrades.Unpack(reader);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            Degrades.Pack(writer);
        }

        /// <summary>Replaces every model id of the levels by map(id).</summary>
        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var degrade in Degrades)
                degrade.Remap(map);
        }
    }
}
