using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ACE.DatLoader
{
    /// <summary>
    /// The dates that recipe steps carry: "yyyy-MM-dd" or "yyyy-MM-dd HH:mm", UTC, stored in the dat entries as Unix time.
    /// </summary>
    public static class DatStepDate
    {
        private static readonly string[] Formats = { "yyyy-MM-dd", "yyyy-MM-dd HH:mm" };

        public static uint Parse(string text)
        {
            if (!DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                throw new ArgumentException($"'{text}' is not a step date (yyyy-MM-dd or yyyy-MM-dd HH:mm, UTC).");

            return (uint)new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        /// <summary>The last second a release takes in: the end of the day for a date alone, the end of the minute for a date with a time.</summary>
        public static uint ParseCutoff(string text)
        {
            return Parse(text) + (text.Length == 10 ? 86399u : 59u);
        }
    }

    /// <summary>One numbered release of a dat. Steps dated after the previous release's cutoff and up to this one's belong to it.</summary>
    public class DatRelease
    {
        public uint Number { get; }

        /// <summary>Last date and time (inclusive) of the steps that belong to this release.</summary>
        public string Cutoff { get; }

        public uint CutoffTime { get; }

        /// <summary>The <see cref="DatReleaseHash"/> of the dat as released, set when the release is distributed; null while it is still being developed.</summary>
        public string Hash { get; }

        public bool Locked => Hash != null;

        public DatRelease(uint number, string cutoff, string hash = null)
        {
            Number = number;
            Cutoff = cutoff;
            CutoffTime = DatStepDate.ParseCutoff(cutoff);
            Hash = hash;
        }
    }

    public enum DatClientStatus
    {
        /// <summary>The client holds the server's iteration count.</summary>
        UpToDate,

        /// <summary>The client is on this ruleset's lineage and behind the server.</summary>
        Patchable,

        /// <summary>The client's count is below the first release of this ruleset: retail, an older distribution or another ruleset's lower block. Its dats must not be patched.</summary>
        TooOld,

        /// <summary>The client's count is above the server's within this ruleset's block.</summary>
        NewerThanServer,

        /// <summary>The client's count is above this ruleset's block.</summary>
        OtherRuleset,
    }

    /// <summary>
    /// The releases of one dat of one ruleset. A ruleset owns a block of iteration numbers, so a client's count says which ruleset its dats come from.
    /// An entry's iteration is the number of the release its date falls in; a client receives a record only when it lacks that number.
    /// </summary>
    public class DatReleaseSet
    {
        public string Ruleset { get; }
        public string Dat { get; }
        public uint BlockStart { get; }
        public uint BlockEnd { get; }

        /// <summary>Records dated before this belong to the base dats and keep the iteration they have.</summary>
        public string StartDate { get; }

        public uint StartTime { get; }
        public IReadOnlyList<DatRelease> Releases { get; }

        public uint FirstNumber => Releases[0].Number;
        public DatRelease Current => Releases[Releases.Count - 1];
        public uint CurrentNumber => Current.Number;

        public DatReleaseSet(string ruleset, string dat, uint blockStart, uint blockEnd, string startDate, params DatRelease[] releases)
        {
            Ruleset = ruleset;
            Dat = dat;
            BlockStart = blockStart;
            BlockEnd = blockEnd;
            StartDate = startDate;
            StartTime = DatStepDate.Parse(startDate);
            Releases = releases;

            Validate();
        }

        public string Name => $"{Ruleset} {Dat}";

        /// <exception cref="InvalidDataException">The table breaks one of its rules.</exception>
        public void Validate()
        {
            if (Releases.Count == 0)
                throw new InvalidDataException($"{Name}: no release.");

            for (var i = 0; i < Releases.Count; i++)
            {
                var release = Releases[i];

                if (release.Number < BlockStart || release.Number > BlockEnd)
                    throw new InvalidDataException($"{Name}: release {release.Number} is outside the ruleset's block {BlockStart}-{BlockEnd}.");

                if (release.CutoffTime < StartTime)
                    throw new InvalidDataException($"{Name}: release {release.Number} ends before the start date {StartDate}.");

                if (i > 0 && (release.Number <= Releases[i - 1].Number || release.CutoffTime <= Releases[i - 1].CutoffTime))
                    throw new InvalidDataException($"{Name}: release {release.Number} must come after release {Releases[i - 1].Number} in number and in cutoff.");

                if (i < Releases.Count - 1 && !release.Locked)
                    throw new InvalidDataException($"{Name}: release {release.Number} is not the latest but has no hash (lock it before adding the next release).");
            }
        }

        /// <summary>The entry iteration for a record written on <paramref name="date"/>: the release it falls in, or 0 for a date before the start (the record keeps the iteration of the base dat).</summary>
        /// <exception cref="InvalidOperationException">The date is after the latest release's cutoff: a new release has to be added first.</exception>
        public uint IterationForDate(uint date)
        {
            if (date < StartTime)
                return 0;

            foreach (var release in Releases)
                if (date <= release.CutoffTime)
                    return release.Number;

            throw new InvalidOperationException($"{Name}: a record is dated after the latest release ({CurrentNumber}, cutoff {Current.Cutoff}); add the next release to the table before using that date.");
        }

        /// <summary>The Unix time of the latest second the latest release takes in.</summary>
        public uint CurrentCutoffTime => Current.CutoffTime;

        /// <summary>
        /// Says what the server may do with a client that reports <paramref name="clientCount"/> iterations for this dat while the server's dat holds <paramref name="serverTotal"/>.
        /// </summary>
        public DatClientStatus Classify(uint clientCount, uint serverTotal)
        {
            if (clientCount == serverTotal)
                return DatClientStatus.UpToDate;

            if (clientCount > serverTotal)
                return clientCount > BlockEnd ? DatClientStatus.OtherRuleset : DatClientStatus.NewerThanServer;

            return clientCount < FirstNumber ? DatClientStatus.TooOld : DatClientStatus.Patchable;
        }
    }

    /// <summary>The release tables. Each ruleset owns a block of 10000 iteration numbers: Infiltration 10000-19999, CustomDM 20000-29999.</summary>
    public static class DatReleases
    {
        public const string Portal = "Portal";
        public const string Cell = "Cell";
        public const string Language = "Language";

        private const uint InfiltrationBlockStart = 10000;
        private const uint InfiltrationBlockEnd = 19999;
        private const uint CustomDMBlockStart = 20000;
        private const uint CustomDMBlockEnd = 29999;

        /// <summary>The date of the first step of the first release: all records the steps write are dated on or after it.</summary>
        public const string StartDate = "2026-11-16";

        public static readonly DatReleaseSet InfiltrationPortal = new DatReleaseSet("Infiltration", Portal, InfiltrationBlockStart, InfiltrationBlockEnd, StartDate,
            new DatRelease(10007, "2026-11-16"));

        public static readonly DatReleaseSet InfiltrationCell = new DatReleaseSet("Infiltration", Cell, InfiltrationBlockStart, InfiltrationBlockEnd, StartDate,
            new DatRelease(10002, "2026-11-16"));

        public static readonly DatReleaseSet InfiltrationLanguage = new DatReleaseSet("Infiltration", Language, InfiltrationBlockStart, InfiltrationBlockEnd, StartDate,
            new DatRelease(10003, "2026-11-16"));

        public static readonly DatReleaseSet CustomDMPortal = new DatReleaseSet("CustomDM", Portal, CustomDMBlockStart, CustomDMBlockEnd, StartDate,
            new DatRelease(20045, "2026-11-16"));

        public static readonly DatReleaseSet CustomDMCell = new DatReleaseSet("CustomDM", Cell, CustomDMBlockStart, CustomDMBlockEnd, StartDate,
            new DatRelease(20012, "2026-11-16"));

        public static readonly DatReleaseSet CustomDMLanguage = new DatReleaseSet("CustomDM", Language, CustomDMBlockStart, CustomDMBlockEnd, StartDate,
            new DatRelease(20012, "2026-11-16"));

        public static readonly DatReleaseSet[] All = { InfiltrationPortal, InfiltrationCell, InfiltrationLanguage, CustomDMPortal, CustomDMCell, CustomDMLanguage };

        /// <summary>The table of a ruleset's dat, or null for a ruleset without one (the retail ruleset).</summary>
        public static DatReleaseSet Find(string ruleset, string dat)
        {
            return All.FirstOrDefault(s => s.Ruleset.Equals(ruleset, StringComparison.OrdinalIgnoreCase) && s.Dat.Equals(dat, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A hash of what a client would receive from a dat: every record's id, entry iteration and bytes, in id order. Dates and versions are not part of it.
    /// </summary>
    public static class DatReleaseHash
    {
        public static string Compute(string datPath)
        {
            var db = new DatDatabase(datPath);

            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                foreach (var id in db.AllFiles.Keys.OrderBy(k => k))
                {
                    var data = db.GetReaderForFile(id).Buffer;

                    sha.AppendData(BitConverter.GetBytes(id));
                    sha.AppendData(BitConverter.GetBytes(db.AllFiles[id].Iteration));
                    sha.AppendData(BitConverter.GetBytes(data.Length));
                    sha.AppendData(data);
                }

                return BitConverter.ToString(sha.GetHashAndReset()).Replace("-", "");
            }
        }
    }
}
