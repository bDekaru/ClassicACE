using System.Collections.Generic;
using System.IO;
using System.Linq;

using ACE.DatLoader;
using ACE.DatLoader.FileTypes;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The Fire in the Sky event (October 22 to 24, 1999) on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>): the comet, the smoky orange cloud layer, the red dome, the rumbling and the short, red-brown fog.
    /// The event dat (<see cref="SourceDat"/>) differs from the retail CD dat three weeks before only in its Region's sky (one day group, chance 100, so the weather never changes) and in records the sky and the event's creatures use.
    /// Every record the sky needs but the comet's model is already in the 2005 dat, so the steps only add that model and replace the Region's day groups with the event's.
    /// The steps are an event kit: a recipe lists them only for the time the event runs.
    /// </summary>
    /// <remarks>
    /// Left out on purpose: the event's landscape (craters, volcano, wrecked towns), its creatures and the event dat's own lighting. The lighting (colors and brightness of the sun and the ambient light) stays that of the 2005 sky; the fog is the event's.
    /// The event's moons (01001327, 01001620) are the 2005 moons, with their glare cover (<see cref="MoonRecords.AddCover"/>). The two sound Setups keep the script word 1 of the old Region, as the rainy skies' flashes of the 2005 base do.
    /// </remarks>
    public static class FireInTheSkyRecords
    {
        /// <summary>portal.dat 1999-10-31, Iteration 105 (End of Beta), MD5 b3e99d5eb5cd852854fe83f58e1bfcad.</summary>
        public const string SourceDat = "portal - 1999-10-31 - Iteration 105 - Fire in the Sky.dat";

        /// <summary>The comet: a quad high in the sky with an additive surface (080014B6 in the 2005 dat) of a glowing head and a long tail.</summary>
        private const uint Comet = 0x010019D1;

        private const uint Dome = 0x010015F0;
        private const uint OldDome = 0x010015EE;
        private const uint Clouds = 0x010016E1;
        private const uint OldClouds = 0x010015B4;

        /// <summary>The moons of the retail CD and event dats to the 2005 moons that stand in their place.</summary>
        private static readonly Dictionary<uint, uint> Moons = new Dictionary<uint, uint> { [0x01001327] = 0x01001F67, [0x01001620] = 0x01001F6A };

        /// <summary>The models that take the place of a 2005 sky model, and so the properties (EoR sets them by role) of the model they replace.</summary>
        private static readonly Dictionary<uint, uint> Roles = new Dictionary<uint, uint> { [Dome] = OldDome, [Clouds] = OldClouds };

        public static DatRecordStep[] All(string date)
        {
            return new[] { CometModel(date), Sky(date) };
        }

        public static DatRecordStep CometModel(string date)
        {
            return new DatRecordStep("Fire in the Sky comet", date, c =>
            {
                c.Write(Comet, DatEnvironmentConverter.ConvertGfxObj(c.ReadSource(SourceDat, Comet)), "comet of the event");
            });
        }

        /// <summary>
        /// The event's sky in the Region: its one day group (chance 100) replaces all the day groups, as in the event dat. The group keeps the dat's objects, the dome and cloud layers, the comet and the two sound Setups, and its fog and its
        /// replacements; the moons are the 2005 moons, and the sun and ambient light of each time of day are those of a sunny day of the 2005 sky.
        /// </summary>
        /// <exception cref="InvalidDataException">The event's times of day are not those of the sunny day.</exception>
        public static DatRecordStep Sky(string date)
        {
            return new DatRecordStep("Fire in the Sky sky", date, c =>
            {
                var region = c.ReadTarget<RegionDesc>(RegionRecords.RegionId);
                var sunny = PreToDBaseRecords.DayGroupsNamed(region, "Sunny").FirstOrDefault() ?? throw new InvalidDataException("The base Region has no Sunny day group.");
                var group = RegionDesc.UnpackPreToD(c.ReadSource(SourceDat, PreToDBaseRecords.HardwareRegionId)).SkyInfo.DayGroups.Single();

                if (!group.SkyTime.Select(t => t.Begin).SequenceEqual(sunny.SkyTime.Select(t => t.Begin)))
                    throw new InvalidDataException("The times of day of the event's sky are not those of the sunny day of the base Region.");

                group.Remap(id => Moons.TryGetValue(id, out var moon) ? moon : id);

                var properties = sunny.SkyObjects.GroupBy(s => s.DefaultGFXObjectId).ToDictionary(g => g.Key, g => g.First().Properties);

                foreach (var skyObject in group.SkyObjects)
                    skyObject.Properties = properties.TryGetValue(Roles.TryGetValue(skyObject.DefaultGFXObjectId, out var role) ? role : skyObject.DefaultGFXObjectId, out var value) ? value : 0;

                for (var i = 0; i < group.SkyTime.Count; i++)
                    group.SkyTime[i].CopyLight(sunny.SkyTime[i]);

                MoonRecords.AddCover(group);

                region.SkyInfo.DayGroups.Clear();
                region.SkyInfo.DayGroups.Add(group);

                c.Write(RegionRecords.RegionId, region.Pack(), "region with the sky of the event", true);
            });
        }
    }
}
