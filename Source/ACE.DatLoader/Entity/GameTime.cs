using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class GameTime : IUnpackable
    {
        public double ZeroTimeOfYear { get; private set; }
        public uint ZeroYear { get; private set; } // Year "0" is really "P.Y. 10" in the calendar.
        public float DayLength { get; private set; }
        public uint DaysPerYear { get; private set; } // 360. Likely for easier math so each month is same length
        public string YearSpec { get; private set; } // "P.Y."
        public List<TimeOfDay> TimesOfDay { get; } = new List<TimeOfDay>();
        public List<string> DaysOfTheWeek { get; } = new List<string>();
        public List<Season> Seasons { get; } = new List<Season>();

        public void Unpack(BinaryReader reader)
        {
            ZeroTimeOfYear  = reader.ReadDouble();
            ZeroYear        = reader.ReadUInt32();
            DayLength       = reader.ReadSingle();
            DaysPerYear     = reader.ReadUInt32();
            YearSpec        = reader.ReadPString();
            reader.AlignBoundary();

            TimesOfDay.Unpack(reader);

            uint numDaysOfTheWeek = reader.ReadUInt32();
            for (uint i = 0; i < numDaysOfTheWeek; i++)
            {
                var weekDay = reader.ReadPString();
                reader.AlignBoundary();
                DaysOfTheWeek.Add(weekDay);
            }

            Seasons.Unpack(reader);
        }

        /// <summary>Writes the entity in the layout <see cref="Unpack"/> reads.</summary>
        public void Pack(BinaryWriter writer)
        {
            writer.Write(ZeroTimeOfYear);
            writer.Write(ZeroYear);
            writer.Write(DayLength);
            writer.Write(DaysPerYear);
            writer.WritePString(YearSpec);
            writer.AlignBoundary();

            writer.Write((uint)TimesOfDay.Count);

            foreach (var item in TimesOfDay)
                item.Pack(writer);

            writer.Write((uint)DaysOfTheWeek.Count);

            foreach (var day in DaysOfTheWeek)
            {
                writer.WritePString(day);
                writer.AlignBoundary();
            }

            writer.Write((uint)Seasons.Count);

            foreach (var item in Seasons)
                item.Pack(writer);
        }
    }
}
