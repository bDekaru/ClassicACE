using System.Collections.Generic;
using System.Linq;

using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The Child of Daralet event (December 2000) on a portal dat made from the 2005 dat (<see cref="PreToDBaseRecords"/>): the records the event's dat added or changed, the red water golem and the event's Region (its sky, sounds and scenery).
    /// The records are made from <see cref="DatBuildRecipes.Portal2000Dat"/> by the id shifts of the data file (the shifts of the EoR weather set of the event), with our own conversion; the weather set's records are not copied, because they carry
    /// conversion mistakes (24-bit images left planar, sound hashes of 0, surface palette ids unshifted). The steps are an event kit: a recipe lists them only for the time the event runs.
    /// </summary>
    /// <remarks>
    /// Left out on purpose: the weather set's changes to the 2005 water textures (the 2005 dat already has the red water). The set's two custom moons (01007085, 01007086) are made from the old moons and put into every day group, as the set has them.
    /// The weather set's red golem (emitter 32000830, model 01003BA3, Setup 020007E6 script word) is the author's rework; the event dat only swaps the model of the golem's bubbles, which <see cref="RedWaterGolem"/> does.
    /// </remarks>
    public static class DaraletRecords
    {
        public const string KitFile = "daralet-kit.json";

        /// <summary>The swarm emitters the water golem's script (3300088E) creates: 3200048E on its limbs, 3200048F (12 particles) on the chest. The event dat has both red.</summary>
        private static readonly uint[] GolemEmitters = { 0x3200048E, 0x3200048F };

        private const uint CoverSurface = 0x08007085;
        private const uint CoverSurfaceTexture = 0x05007085;
        private const uint CoverTexture = 0x06009F01;
        private const uint CoverPalette = 0x04007085;

        /// <summary>The red bubbles the event dat gives the golem emitter (010016C0 of that dat), as the kit writes them.</summary>
        private const uint RedBubbles = 0x010066C0;

        /// <summary>Where the weather set puts its custom moons (01007085, 01007086, the moons without the glare layer) in every day group: right after the object at the index before, which they copy.</summary>
        private const int MoonIndex = 4;

        /// <summary>The moon model of the object before the new one (the shifted 2080 and 207B) to the custom moon that follows it.</summary>
        private static readonly Dictionary<uint, uint> Moons = new Dictionary<uint, uint> { [0x01007080] = 0x01007085, [0x0100707B] = 0x01007086 };

        public static DatRecordStep[] All(string date)
        {
            return new[] { Records(date), MoonCover(date), RedWaterGolem(date), WaterTextures(date), Region(date) };
        }

        /// <summary>
        /// The surface the custom moons (01007085, 01007086) draw instead of the old moons' glare layer: a clip-map surface that is fully translucent, over an 8x8 image of palette index 0 and a black palette. It is the same
        /// surface the weather set uses (EoR's 08000015), made from nothing; the ids are free ones that the kit's SurfaceOverrides name.
        /// </summary>
        public static DatRecordStep MoonCover(string date)
        {
            return new DatRecordStep("Moon glare cover surface", date, c =>
            {
                c.Write(MoonRecords.CoverRecords(CoverSurface, CoverSurfaceTexture, CoverTexture, CoverPalette), "moon glare cover");
            });
        }

        public static DatRecordStep Records(string date)
        {
            return ModelRecords.Step("Child of Daralet records", date, KitFile, DatBuildRecipes.Portal2000Dat);
        }

        /// <summary>The terrain textures of the red water (05001AF3-05001AF7, the same records in the 2000 and 2005 dats) as terrain images: the event's Region points its water terrain at them.</summary>
        public static DatRecordStep WaterTextures(string date)
        {
            return new DatRecordStep("Red water terrain textures", date, c =>
            {
                for (uint id = 0x05001AF3; id <= 0x05001AF7; id++)
                    GroundTextureRecords.WriteTerrainImage(c, c.ReadTarget<SurfaceTexture>(id).Textures[0], id);
            });
        }

        /// <summary>The event dat edits the golem's emitters in place (their bubbles are blue in every other dat); the Setup and its script stay as they are.</summary>
        public static DatRecordStep RedWaterGolem(string date)
        {
            return new DatRecordStep("Red water golem", date, c =>
            {
                foreach (var id in GolemEmitters)
                {
                    var emitter = c.ReadTarget<ParticleEmitterInfo>(id);
                    emitter.HwGfxObjId = RedBubbles;
                    c.Write(id, emitter.Pack(), "water golem bubbles made red", true);
                }
            });
        }

        /// <summary>
        /// The event dat's Region: its sky (day groups, colors, thunder), sounds and scenery with the ids of the kit. Only what the old record cannot hold comes from the EoR record, as for the 2005 Region: the sky objects' properties
        /// (the value EoR gives the object of the same role: that of the 2005 object of the same model, see <see cref="PreToDBaseRecords.SkyPropertiesOf2005"/>) and the region number. A Setup sky object that named script 1 gets the default script of its Setup, as the weather set has it. The terrain is the old record's, adapted like the base Region (<see cref="GroundTextureRecords.RegionStep"/>).
        /// </summary>
        public static DatRecordStep Region(string date)
        {
            var rebuild = PreToDBaseRecords.Region("Region of the Child of Daralet", date, DatBuildRecipes.Portal2000Dat, (c, region, eor) =>
            {
                var properties = PreToDBaseRecords.SkyPropertiesOf2005(c, eor);
                var skyObjects = region.SkyInfo.DayGroups.SelectMany(g => g.SkyObjects).ToList();

                foreach (var skyObject in skyObjects)
                    skyObject.Properties = properties.TryGetValue(skyObject.DefaultGFXObjectId, out var value) ? value : 0;

                var ids = ModelRecords.IdMap(DatRecipeData.Load<ModelRecords.Data>(KitFile));
                region.Remap(id => ids.TryGetValue(id, out var target) ? target : id);

                foreach (var group in region.SkyInfo.DayGroups)
                    group.InsertSkyObject(MoonIndex, group.SkyObjects[MoonIndex - 1].CloneWith(Moons[group.SkyObjects[MoonIndex - 1].DefaultGFXObjectId]));

                foreach (var skyObject in skyObjects.Where(s => s.DefaultGFXObjectId >> 24 == 0x02 && s.DefaultPESObjectId == 1))
                    skyObject.DefaultPESObjectId = c.ReadTarget<SetupModel>(skyObject.DefaultGFXObjectId).DefaultScript;
            });

            var adapt = GroundTextureRecords.RegionStep(date);

            // Steps run in date order, so this one also repeats the ground texture adaptation of the Region it replaces.
            return new DatRecordStep(rebuild.Name, date, c =>
            {
                rebuild.Run(c);
                adapt.Run(c);
            });
        }
    }
}
