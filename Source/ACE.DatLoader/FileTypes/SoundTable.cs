using ACE.DatLoader.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ACE.DatLoader.FileTypes
{
    /// <summary>
    /// SoundTable files contain a listing of which Wav types to play in response to certain events.
    /// They are located in the client_portal.dat and are files starting with 0x20
    /// </summary>
    [DatFileType(DatFileType.SoundTable)]
    public class SoundTable:FileType
    {
        public uint Unknown; // As the name implies, not sure what this is

        // Not quite sure what this is for, but it's the same in every file.
        public List<SoundTableData> SoundHash { get; } = new List<SoundTableData>();

        // The uint key corresponds to an Enum.Sound
        public Dictionary<uint, SoundData> Data { get; } = new Dictionary<uint, SoundData>();

        /// <summary>The bucket size the packed hash table was stored with, kept so that a record is written back as it was read.</summary>
        public ushort DataBucketSize { get; set; }

        public override void Unpack(BinaryReader reader)
        {
            Id = reader.ReadUInt32();
            Unknown = reader.ReadUInt32();
            SoundHash.Unpack(reader);
            DataBucketSize = Data.UnpackPackedHashTable(reader);
        }

        public override void Pack(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Unknown);

            SoundHash.Pack(writer);

            Data.PackPackedHashTable(writer, DataBucketSize);
        }

        /// <summary>Renumbers the sounds the table is keyed by (the Sound enum): every key becomes map(key), in the same order.</summary>
        public void RemapSounds(Func<uint, uint> map)
        {
            var entries = Data.ToList();

            Data.Clear();
            foreach (var entry in entries)
                Data.Add(map(entry.Key), entry.Value);
        }

        /// <summary>Replaces every wave id by map(id).</summary>
        public void Remap(Func<uint, uint> map)
        {
            foreach (var item in SoundHash)
                item.SoundId = map(item.SoundId);

            foreach (var sound in Data.Values)
                foreach (var item in sound.Data)
                    item.SoundId = map(item.SoundId);
        }
    }
}
