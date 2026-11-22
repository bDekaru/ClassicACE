using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class DayGroup : IUnpackable, IPackable
    {
        public float ChanceOfOccur { get; private set; }
        public string DayName { get; private set; }
        public List<SkyObject> SkyObjects { get; } = new List<SkyObject>();
        public List<SkyTimeOfDay> SkyTime { get; } = new List<SkyTimeOfDay>();

        public void Unpack(BinaryReader reader) => Unpack(reader, false);

        /// <summary>Reads the pre-ToD (2005) layout, whose sky objects have no properties field.</summary>
        public void UnpackPreToD(BinaryReader reader) => Unpack(reader, true);

        private void Unpack(BinaryReader reader, bool preToD)
        {
            ChanceOfOccur   = reader.ReadSingle();
            DayName         = reader.ReadPString();
            reader.AlignBoundary();

            if (preToD)
            {
                var count = reader.ReadUInt32();

                for (int i = 0; i < count; i++)
                {
                    var skyObject = new SkyObject();
                    skyObject.UnpackPreToD(reader);
                    SkyObjects.Add(skyObject);
                }
            }
            else
                SkyObjects.Unpack(reader);

            SkyTime.Unpack(reader);
        }

        /// <summary>Puts a sky object into the list; the replacements of the times keep pointing at the objects they pointed at.</summary>
        public void InsertSkyObject(int index, SkyObject skyObject)
        {
            SkyObjects.Insert(index, skyObject);

            foreach (var time in SkyTime)
                foreach (var replace in time.SkyObjReplace)
                    replace.Inserted((uint)index);
        }

        public void Remap(System.Func<uint, uint> map)
        {
            foreach (var skyObject in SkyObjects)
                skyObject.Remap(map);

            foreach (var time in SkyTime)
                time.Remap(map);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(ChanceOfOccur);
            writer.WritePString(DayName);
            writer.AlignBoundary();

            SkyObjects.Pack(writer);

            SkyTime.Pack(writer);
        }
    }
}
