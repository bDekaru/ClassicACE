using System;
using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>The cell dats that map recipes take landblocks from.</summary>
    public enum DatMapSource
    {
        Release,
        Arwic,
        Tufa,
        DarkMajesty,
        ObsidianSpan,
        ToD,
        EoR,
        Admin2005,
        DarkMajestyDisc,
    }

    /// <summary>
    /// The donor cell dats of a map recipe as stores, each loaded when first used. Retail-layout dats are converted to ToD layout like the destination dat;
    /// the ToD and EoR cell dats are read as they are.
    /// </summary>
    public class DatMapSources
    {
        // MD5 of each file as checked 2026-11-17 (the 2001-09-12 one 2026-11-27).
        public static readonly IReadOnlyDictionary<DatMapSource, (string File, bool RetailLayout)> Files = new Dictionary<DatMapSource, (string, bool)>
        {
            // MD5 8e61b2b6dfae5bef16f3ef670a6869df
            [DatMapSource.Release] = ("cell - 1999-10-09 - Iteration 67 - 100% - Release.dat", true),
            // MD5 b6ef1f5b543c3e36854f3a06c38d4eb3
            [DatMapSource.Arwic] = ("cell - 2000-08-22 - Iteration 108 - 100% - Ice Island, Shadow Spires.dat", true),
            // MD5 09a0fa002834ec38ea4259e76efe4b6d
            [DatMapSource.Tufa] = ("cell - 2000-07-26 - Iteration 91 - 98.57% - To Raise a Banner of Flame.dat", true),
            // MD5 d562b297fe7c19dab545c3d57a5b1e16
            [DatMapSource.DarkMajesty] = ("cell - 2002-03-08 - Iteration 1303 - 100% - Fever Dreams.dat", true),
            // MD5 33dc182ab077525a3c1b1ca544dbb9b5
            [DatMapSource.ObsidianSpan] = ("cell - 2000-12-31 - Iteration 279 - 100% - Obsidian Span.dat", true),
            // MD5 c1521ecd4dab4123df5467090742a45b
            [DatMapSource.ToD] = ("cell - 2005-06-02 - Iteration 4 - 100% - Throne of Destiny.dat", false),
            // MD5 6401b73fd3842ffdb953339522a7331a
            [DatMapSource.EoR] = ("cell - 2016-12-23 - Iteration 982 - 100% - EoR.dat", false),
            // The cell dat the destination is converted from (MD5 0f32588ffbf648cd0695a4dbd6488205), for edits that put a 2005 building back.
            [DatMapSource.Admin2005] = (DatBuildRecipes.PreToDCellDat, true),
            // The 2001-09-12 snapshot (Dark Majesty CD), MD5 8b1210713dd0381be164fe9edb563c6e
            [DatMapSource.DarkMajestyDisc] = ("cell - 2001-09-12 - Iteration 375 - 100% - Dark Majesty.dat", true),
        };

        private readonly string _sourceDats;
        private readonly IReadOnlyDictionary<ushort, ushort> _surfaceMap;
        private readonly Dictionary<DatMapSource, DatCellStore> _stores = new Dictionary<DatMapSource, DatCellStore>();

        public Dictionary<DatMapSource, DatCellConversionReport> Reports { get; } = new Dictionary<DatMapSource, DatCellConversionReport>();

        public DatMapSources(string sourceDats, IReadOnlyDictionary<ushort, ushort> surfaceMap)
        {
            _sourceDats = sourceDats;
            _surfaceMap = surfaceMap;
        }

        public DatCellStore Get(DatMapSource source)
        {
            if (_stores.TryGetValue(source, out var store))
                return store;

            var (file, retailLayout) = Files[source];
            var path = System.IO.Path.Combine(_sourceDats, file);

            if (!System.IO.File.Exists(path))
                throw new ArgumentException($"Source dat not found: {path}");

            if (retailLayout)
            {
                var report = new DatCellConversionReport();
                store = new DatCellStore(DatCellConverter.ConvertToStore(path, _surfaceMap, report));
                Reports[source] = report;
            }
            else
            {
                var records = new SortedDictionary<uint, byte[]>();
                var db = new DatDatabase(path, true);

                foreach (var id in db.AllFiles.Keys.Where(k => (k >> 16) != 0xFFFF))
                    records[id] = db.GetReaderForFile(id).Buffer;

                store = new DatCellStore(records);
            }

            _stores[source] = store;
            return store;
        }
    }

    /// <summary>Map edits per ruleset, applied to the converted 2005 cell dat before it is written. Each follows the order of the edits it replaces, because a later edit overrides an earlier one.</summary>
    public static class DatMapRecipes
    {
        public const string SettlementListFile = "Cell/settlement_landblocks.tsv";

        public static DatMapEditor.SettlementList LoadSettlementList()
        {
            using (var reader = new System.IO.StreamReader(DatRecipeData.Open(SettlementListFile)))
                return DatMapEditor.SettlementList.Parse(reader);
        }

        private static (ushort TopLeft, ushort BottomRight, ushort NewTopLeft) Rect(ushort topLeft, ushort bottomRight, ushort newTopLeft = 0) => (topLeft, bottomRight, newTopLeft);

        /// <param name="portalIs2005">The portal dat is the 2005 one, so the 856E tent and the Olthoi dungeon (66790000) come from the 2005 cell dat instead of the ToD and EoR ones, whose surface ids the portal lacks.</param>
        public static void CustomDM(DatMapEditor map, DatMapSources sources, bool portalIs2005 = false)
        {
            var release = sources.Get(DatMapSource.Release);
            var arwic = sources.Get(DatMapSource.Arwic);
            var tufa = sources.Get(DatMapSource.Tufa);
            var darkMajesty = sources.Get(DatMapSource.DarkMajesty);
            var obsidianSpan = sources.Get(DatMapSource.ObsidianSpan);
            var tod = sources.Get(DatMapSource.ToD);
            var eor = sources.Get(DatMapSource.EoR);

            // Every edit below is part of the initial version. Edits added later start their own step with a new date.
            map.Step(DatBuildRecipes.InitialVersionDate);

            // Arwic
            map.ReplaceLandblocks(new uint[] { 0xC6A90013, 0xC6A90008, 0xC6A80038, 0xC6AA0011, 0xC5AA0039, 0xC5A80028 }, release);
            map.ReplaceLandblock(0xC5A90037, arwic);
            map.ReplaceLandblock(0xC6A90013, arwic, false, false, true, false);
            map.ReplaceLandblock(0xC5A6003A, release);
            map.ReplaceLandblock(0xC5AB0036, release);

            // Yanshi, then without New Yanshi
            map.ReplaceLandblocks(new uint[] { 0xB86E0038, 0xB86F0032, 0xB8700029, 0xB9700024, 0xB96F0040, 0xB96E0018, 0xBA6F0004, 0xBA700001 }, arwic);
            map.ReplaceLandblocks(new uint[] { 0xB9720022, 0xBA720005, 0xB9710030 }, arwic);

            // Tufa
            map.ReplaceLandblocks(new uint[] { 0x866D000B, 0x866E0011, 0x856D003C, 0x856E0031, 0x856C001C, 0x876E0032, 0x856F0015 }, tufa);
            map.ReplaceLandblocks(new uint[] { 0x846E0012, 0x846D0018, 0x836D0040, 0x836E0039 }, release);
            map.AddBuilding(0x856E010E, portalIs2005 ? sources.Get(DatMapSource.Admin2005) : tod);

            // Eastham shadow spire crater
            map.ReplaceLandblock(0xCD95002F, release);
            map.ReplaceLandblock(0xCE950006, release, true, true, false, false);
            map.ReplaceLandblock(0xCE960001, release);
            map.ReplaceLandblock(0xCD960039, release);
            map.RemoveBuilding(0xCE95016C);

            map.RemoveHouseSettlements(obsidianSpan, LoadSettlementList());
            map.RemoveNoobFence();
            map.ReplaceLandblock(0x7D8F0013, obsidianSpan);

            map.ReplaceStarterOutposts(new uint[]
            {
                0x905C003B, 0x915C0003, 0x8C58002B, 0xC380000C, 0xBB80002E, 0xEA3D000E, 0xE542002B,
                0xCC8C0014, 0xC8880016, 0x9B7B0015, 0x937F002D, 0xB9730032, 0xB96B001E,
            }, obsidianSpan);

            var portalBunkersAndStatues = new uint[]
            {
                0xA9B30114, 0xA9B40183, 0xA9B40188, 0xA9B4017E, 0xAAB40108, 0xAAB40103,
                0x7D64018B, 0x7D64017C, 0x7D640181, 0x7E640122, 0x7E640127, 0x7D640177, 0x7D640186,
                0xDA560116, 0xDA5501F9, 0xDA56011B, 0xDA5501F4, 0xDA540103, 0xDA5501EF, 0xD955010D,
                0x90580141, 0xA9B40177, 0xBF80017D, 0xE63D0154, 0xC88C0171, 0x977B015F, 0xDA5501E8, 0x7D640151,
                0xBC9F0166, 0x85880129, 0xCE95016A, 0xA1A40162, 0xE74E01A7, 0x9E430132, 0xE9220141, 0xDA3B013C,
                0xA2600122, 0x80900139, 0xF75C0155, 0xCD41018B, 0xF2220122, 0xE5320136, 0x49B6012D, 0x9722015F,
                0xC95B0169, 0x64D50131, 0x866C010B, 0x1134014A, 0xBA170143, 0x3F310101, 0x2581016B,
            };

            foreach (var building in portalBunkersAndStatues)
                map.RemoveBuilding(building);

            foreach (var landblock in new uint[] { 0xA9B40024, 0xAAB4000A, 0xA8B4003B })
                map.ReplaceTerrain(landblock, obsidianSpan, true, true);

            var meetingHalls = new uint[]
            {
                0x7D640173, 0x8090015D, 0x846D0101, 0x85890101, 0x90580152, 0x97220162, 0x987B0101, 0x9D430101, 0xA1A40167,
                0xA25F0166, 0xA9B4017A, 0xB76F0101, 0xBC9F0169, 0xBF800191, 0xC5A90101, 0xC88D0101, 0xC95B0175, 0xCD41018E,
                0xCE950172, 0xDA3A010B, 0xDA5501EB, 0xE5320139, 0xE63D0166, 0xE74E01C9, 0xF65C0118,
            };
            var meetingHallObjects = new uint[] { 0x020003C7, 0x0200040B, 0x020005D8, 0x02000398, 0x02000719, 0x0200071A };

            foreach (var building in meetingHalls)
                map.RemoveBuilding(building, true, meetingHallObjects);

            var miscBuildings = new uint[]
            {
                0x90570102, 0xA9B40171, 0xBE800109, 0xE63D014E, 0xC88B0102, 0x977B0159, 0xDA56010E, 0x7D64014B, 0xBA9E0105,
                0x85880123, 0xCE950163, 0xA2A40100, 0xE64E0104, 0x9E43012D, 0xE8210104, 0xDB3B011D, 0xA260011C, 0x7F8F011C,
                0xF75C014F, 0xCD410183, 0xF2230104, 0xE532012E, 0x49B60126, 0x97220159, 0xC95B0161, 0x64D50129, 0x11340144,
                0xBA17013B,
                0xDB550103, 0x7D640163, 0x97220173, 0xE63D0159, 0xDA75016D, 0xCD410199, 0xA25F015F, 0x90580143, 0x3F31010A,
            };
            var miscObjects = new uint[]
            {
                0x020002F8, 0x0200036F, 0x02000370, 0x02000371, 0x0200036E, 0x0200035B, 0x0200035E, 0x02000183, 0x02000719,
                0x020000F8, 0x02000337, 0x02000332, 0x02000120, 0x02000123, 0x02000322, 0x01000BEB, 0x010009D1, 0x010009D2,
                0x02000338, 0x0200071A,
            };

            foreach (var building in miscBuildings)
                map.RemoveBuilding(building, true, miscObjects);

            map.ReplaceLandblocks(new uint[] { 0xAAB30004, 0xA9B3003C, 0xF75D0022 }, darkMajesty);

            foreach (var cell in new uint[] { 0x90580021, 0xCD41002F, 0xCD410030, 0xCD410037 })
                map.RemoveObjects(cell, miscObjects);

            map.ReplaceLandblock(0xE63D002D, release, false, true, false, false);

            var portalPedestals = new uint[] { 0x02000B61, 0x020007C5, 0x02000B6F };

            foreach (var cell in new uint[] { 0x7D640033, 0x7D64000B, 0xA9B4000E, 0xD955003C })
                map.RemoveObjects(cell, portalPedestals);

            map.ReplaceLandblocks(new uint[] { 0xA6B40015, 0x81640017, 0xDE510003 }, darkMajesty, false, false, true, false);

            map.ReplaceLandblock(0xE04E0025, release);

            // Islands from the Arwic dat. Old Caul, White Island, ! Island, Ice Island and Eastern Island first.
            map.ReplaceRects(arwic, new[]
            {
                Rect(0x0011, 0x1100, 0x0423),
                Rect(0x653E, 0x7B28),
                Rect(0x5C80, 0x6F57),
                Rect(0x54FB, 0x61EB),
                Rect(0x61FB, 0x6CEE),
                Rect(0x6CFB, 0x72F2),
                Rect(0xD1ED, 0xDAE2, 0xD1F9), // The part of this area whose destination the Eastern Island rectangles overwrite is left out (x DB-E1 below y E7, all of x E2).
                Rect(0xDBED, 0xE1E7, 0xDBF9),
                Rect(0xDBBB, 0xE29E, 0xDBF2),
                Rect(0xD6E8, 0xDADF, 0xD6DF),
            });

            // Eastern Island, from a later dat than the other islands: the same terrain, plus a huge hut, an object and two Shadow Spires pieces (removed below).
            map.ReplaceRects(sources.Get(DatMapSource.DarkMajestyDisc), new[] { Rect(0xE2C6, 0xFD6D, 0xE2FD), Rect(0xE96D, 0xF061, 0xE9A4) });

            // Old Marae Lassel: the dat's old terrain words are swapped for the ones the current client draws before the landblocks are taken.
            var marae = new[]
            {
                Rect(0x0EB3, 0x259F, 0x05E5),
                Rect(0x119F, 0x229F, 0x08D1),
                Rect(0x119E, 0x219E, 0x08D0),
                Rect(0x119D, 0x1F9B, 0x08CF),
                Rect(0x139A, 0x1E99, 0x0ACC),
                Rect(0x1798, 0x1C97, 0x0ECA),
                Rect(0x1896, 0x1A96, 0x0FC8),
            };

            // The swaps change the Arwic donor itself, so every later edit that takes landblocks from it gets the swapped words.
            var arwicEditor = new DatMapEditor(arwic);
            var oldMarae = marae.SelectMany(r => arwicEditor.LandblocksInRect(r.TopLeft, r.BottomRight)).ToList();

            foreach (var swap in new (ushort Old, ushort New)[] { (0x0000, 0x5000), (0x0024, 0x5024), (0x000C, 0x100C), (0x0034, 0x5034), (0x003C, 0x603C), (0x0038, 0x5038), (0x0040, 0x4840), (0x0054, 0x6054) })
                foreach (var landblock in oldMarae)
                    arwicEditor.ReplaceTexture(landblock, swap.Old, swap.New);

            map.ReplaceRects(arwic, marae);

            // Islands from the EoR dat. Olthoi Island overlaps the Dark Isle area and has to go after it; the Halaetan Isles and Freebooter Isle after that.
            map.ReplaceRects(eor, new[]
            {
                Rect(0x3B0F, 0x400D),
                Rect(0x370D, 0x4409),
                Rect(0xC0F9, 0xD9E0),
                Rect(0xCBE0, 0xD9DD),
                Rect(0xD2DD, 0xD9D9),
            });
            map.ReplaceRects(eor, new[] { Rect(0xE2D8, 0xEBCC, 0xCEF3) });
            map.ReplaceRects(eor, new[]
            {
                Rect(0xF43F, 0xFD2C),
                Rect(0x1EF9, 0x3DD5),
                Rect(0x3DFA, 0x45E6),
                Rect(0x45FA, 0x51EC),
            });

            map.ReplaceLandblocks(new uint[]
            {
                0x1A130000, 0x1B120000, 0x1B130000, 0x1B140000, 0x1C120000, 0x1C130000, 0x1C140000, 0x1C150000,
                0x1D120000, 0x1D130000, 0x1D140000, 0x1D150000, 0x1E130000, 0x1E140000, 0x1A140000, 0x1A120040,
            }, obsidianSpan);

            // The Olthoi dungeon: the Arwic island rectangle 5C80-6F57 above removes its landblock info, so it is put back. The EoR record differs from the 2005 one only in surface ids.
            map.ReplaceLandblock(0x66790000, portalIs2005 ? sources.Get(DatMapSource.Admin2005) : eor);

            // The Olthoi Warrior Nest and Mutilator Tunnels, lost to the same Arwic rectangle. Their portals are in the world DB. Cells only: neither has outside objects, and the Arwic terrain stays.
            foreach (var landblock in new uint[] { 0x66780000, 0x667A0000 })
                map.ReplaceLandblock(landblock, portalIs2005 ? sources.Get(DatMapSource.Admin2005) : eor, false, false, false, true);

            // Buildings retail never loaded (has-objects flag off from 1999-2000 until ToD emptied them). Four copies of a huge hut are switched on.
            foreach (var landblock in new uint[] { 0x24590000, 0x25770000, 0x26770000, 0x287F0000 })
                map.SetHasObjects(landblock);

            // The rest go (an empty record, three landblocks of Shadow Spires event pieces and a Sho house). No world spawn is in their cells.
            foreach (var landblock in new uint[] { 0x24580000, 0xC0810000, 0xDB470000, 0xF4D80000, 0xF95D0000 })
                map.RemoveLandblockInfo(landblock);
        }
    }
}
