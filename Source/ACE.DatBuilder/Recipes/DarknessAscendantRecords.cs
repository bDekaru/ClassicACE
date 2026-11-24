using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader.Entity.AnimationHooks;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The Darkness Ascendant event (March 2000) on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>): the winged figure that flickers in the lightning of the rainy skies, the dim red sun, the sulfur-tinted clouds and the swollen moons.
    /// Every record the event needs is already in the 2005 dat (the same bytes as in the portal dat of 2000-07-26, Iteration 102, the one that has the event's sky), so the steps only change which records the Region uses and the pauses of two scripts.
    /// The steps are an event kit: a recipe lists them only for the time the event runs.
    /// </summary>
    /// <remarks>
    /// Left out on purpose: the event dat's colored lighting of the rainy skies. The 2005 cloud layers already drift at the event's speed.
    /// The moons are the 2005 moons (130 and 259 units of half-width) enlarged to the on-screen size of the event's moons (414 and 641), so the swollen moons look like the moons the players know; the old pink and cream moons
    /// of the event dat (01001BC0 and 01001BC1, 414 and 641 units) are in no later dat; <see cref="Region"/> keeps the lines that would use them, commented out, for the history.
    /// </remarks>
    public static class DarknessAscendantRecords
    {
        /// <summary>The winged figure's Setup (its script 3300085E makes the figure for 0.01 s, then calls itself again after a random time of up to its pause) and the Setup of the thunder (script 3300085F).</summary>
        private const uint Figure = 0x020007BA;
        private const uint Thunder = 0x020007C2;

        /// <summary>The lightning flash of the 2005 sky that stands where the event had the figure.</summary>
        private const uint Flash = 0x02000BA6;

        private const uint FigureScript = 0x3300085E;
        private const uint ThunderScript = 0x3300085F;

        /// <summary>The pauses of the event dat (the 2005 dat has 300 for both): the figure shows up about every 30 seconds on average, the thunder about every 50.</summary>
        private const float FigurePause = 60;
        private const float ThunderPause = 100;

        private const uint Sun = 0x01001348;
        private const uint RedSun = 0x01001CEA;

        private const uint Clouds = 0x02000714;
        private const uint SulfurClouds = 0x020008C3;

        /// <summary>The 2005 moons, the cover that is drawn over the larger one (<see cref="MoonRecords"/>) and the enlarged copies. 7091 to 7093 are free ids (the dat has no model after 01006xxx apart from the kits' 7080 to 7087).</summary>
        private const uint SmallMoon = 0x01001F67;
        private const uint BigMoon = MoonRecords.Moon;
        private const uint BigMoonCover = 0x01007087;
        private const uint SwollenSmallMoon = 0x01007091;
        private const uint SwollenBigMoon = 0x01007092;
        private const uint SwollenBigMoonCover = 0x01007093;

        // Half-widths of the event's moons (01001BC0, 01001BC1) over those of the 2005 moons (130 and 259): the swollen moons get the size the event's had on the screen.
        private const float SmallMoonFactor = 414f / 130f;
        private const float BigMoonFactor = 641f / 259f;

        public static DatRecordStep[] All(string date)
        {
            return new[] { Scripts(date), Moons(date), Region(date) };
        }

        public static DatRecordStep Scripts(string date)
        {
            return new DatRecordStep("Darkness Ascendant figure and thunder pauses", date, c =>
            {
                foreach (var (id, pause) in new[] { (FigureScript, FigurePause), (ThunderScript, ThunderPause) })
                {
                    var script = c.ReadTarget<PhysicsScript>(id);

                    foreach (var hook in script.ScriptData.Select(d => d.Hook).OfType<CallPESHook>())
                        hook.Pause = pause;

                    c.Write(id, script.Pack(), "script pause of the event", true);
                }
            });
        }

        /// <summary>The moons enlarged about their centers, and the cover the larger one has (the Region step puts them in).</summary>
        public static DatRecordStep Moons(string date)
        {
            return new DatRecordStep("Darkness Ascendant swollen moons", date, c =>
            {
                foreach (var (source, id, factor) in new[] { (SmallMoon, SwollenSmallMoon, SmallMoonFactor), (BigMoon, SwollenBigMoon, BigMoonFactor), (BigMoonCover, SwollenBigMoonCover, BigMoonFactor) })
                {
                    var model = c.ReadTarget<GfxObj>(source);

                    model.Id = id;
                    model.Scale(factor);
                    c.Write(id, model.Pack(), $"moon {source:X8} enlarged {factor:0.###} times");
                }
            });
        }

        /// <summary>
        /// The event's sky in the Region: the red sun and the sulfur clouds in every day group, the swollen moons, and in the rainy groups the figure where the 2005 sky has the flash at the same times of day (the morning and evening storms),
        /// with the thunder at the same times.
        /// </summary>
        public static DatRecordStep Region(string date)
        {
            return new DatRecordStep("Darkness Ascendant sky", date, c =>
            {
                var region = c.ReadTarget<RegionDesc>(RegionDesc.FILE_ID);
                var replaced = new Dictionary<uint, uint>
                {
                    [Sun] = RedSun,
                    [Clouds] = SulfurClouds,
                    [SmallMoon] = SwollenSmallMoon,
                    [BigMoon] = SwollenBigMoon,
                    [BigMoonCover] = SwollenBigMoonCover,
                };

                region.Remap(id => replaced.TryGetValue(id, out var target) ? target : id);

                var figures = 0;

                foreach (var group in PreToDBaseRecords.DayGroupsNamed(region, "Rainy"))
                {
                    var flashes = group.SkyObjects.Where(s => s.DefaultGFXObjectId == Flash && (Math.Abs(s.BeginTime - 0.03f) < 0.001f || Math.Abs(s.BeginTime - 0.91f) < 0.001f)).ToList();

                    foreach (var flash in flashes)
                    {
                        group.SkyObjects[group.SkyObjects.IndexOf(flash)] = flash.CloneWith(Figure);
                        group.SkyObjects.Add(flash.CloneWith(Thunder));
                        figures++;
                    }
                }

                if (figures == 0)
                    throw new InvalidDataException("The base Region has no lightning flash at the event's times in a Rainy day group, so the figure and thunder would be missing.");

                c.Write(RegionDesc.FILE_ID, region.Pack(), "region with the sky of the event", true);
            });
        }
    }
}
