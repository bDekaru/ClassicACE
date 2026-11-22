using System;
using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader.Entity
{
    public class SexCG : IUnpackable, IPackable
    {
        public string Name { get; private set; }
        public uint Scale { get; private set; }
        public uint SetupID { get; private set; }
        public uint SoundTable { get; private set; }
        public uint IconImage { get; private set; }
        public uint BasePalette { get; private set; }
        public uint SkinPalSet { get; private set; }
        public uint PhysicsTable { get; private set; }
        public uint MotionTable { get; private set; }
        public uint CombatTable { get; private set; }
        public ObjDesc BaseObjDesc { get; private set; } = new ObjDesc();
        public List<uint> HairColorList { get; } = new List<uint>();
        public List<HairStyleCG> HairStyleList { get; } = new List<HairStyleCG>();
        public List<uint> EyeColorList { get; } = new List<uint>();
        public List<EyeStripCG> EyeStripList { get; } = new List<EyeStripCG>();
        public List<FaceStripCG> NoseStripList { get; } = new List<FaceStripCG>();
        public List<FaceStripCG> MouthStripList { get; } = new List<FaceStripCG>();
        public List<GearCG> HeadgearList { get; } = new List<GearCG>();
        public List<GearCG> ShirtList { get; } = new List<GearCG>();
        public List<GearCG> PantsList { get; } = new List<GearCG>();
        public List<GearCG> FootwearList { get; } = new List<GearCG>();
        public List<uint> ClothingColorsList { get; } = new List<uint>();

        // Eyes
        public uint GetEyeTexture(uint eyesStrip, bool isBald)
        {
            ObjDesc eyes;
            if (isBald)
                eyes = EyeStripList[Convert.ToInt32(eyesStrip)].ObjDescBald;
            else
                eyes = EyeStripList[Convert.ToInt32(eyesStrip)].ObjDesc;
            return eyes.TextureChanges[0].NewTexture;
        }
        public uint GetDefaultEyeTexture(uint eyesStrip, bool isBald)
        {
            ObjDesc eyes;
            if (isBald)
                eyes = EyeStripList[Convert.ToInt32(eyesStrip)].ObjDescBald;
            else
                eyes = EyeStripList[Convert.ToInt32(eyesStrip)].ObjDesc;
            return eyes.TextureChanges[0].OldTexture;
        }

        // Nose
        public uint GetNoseTexture(uint noseStrip)
        {
            ObjDesc nose = NoseStripList[Convert.ToInt32(noseStrip)].ObjDesc;
            return nose.TextureChanges[0].NewTexture;
        }
        public uint GetDefaultNoseTexture(uint noseStrip)
        {
            ObjDesc nose = NoseStripList[Convert.ToInt32(noseStrip)].ObjDesc;
            return nose.TextureChanges[0].OldTexture;
        }

        // Mouth
        public uint GetMouthTexture(uint mouthStrip)
        {
            ObjDesc mouth = MouthStripList[Convert.ToInt32(mouthStrip)].ObjDesc;
            return mouth.TextureChanges[0].NewTexture;
        }
        public uint GetDefaultMouthTexture(uint mouthStrip)
        {
            ObjDesc mouth = MouthStripList[Convert.ToInt32(mouthStrip)].ObjDesc;
            return mouth.TextureChanges[0].OldTexture;
        }

        // Hair (Head)
        public uint? GetHeadObject(uint hairStyle)
        {
            HairStyleCG hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];

            // Gear Knights, both Olthoi types have multiple anim part changes.
            if (hairstyle.ObjDesc.AnimPartChanges.Count == 1)
                return hairstyle.ObjDesc.AnimPartChanges[0].PartID;
            else
                return null;
        }
        public uint? GetHairTexture(uint hairStyle)
        {
            HairStyleCG hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];

            // OlthoiAcid has no TextureChanges
            if (hairstyle.ObjDesc.TextureChanges.Count > 0)
                return hairstyle.ObjDesc.TextureChanges[0].NewTexture;
            else
                return null;

        }
        public uint? GetDefaultHairTexture(uint hairStyle)
        {
            HairStyleCG hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];

            // OlthoiAcid has no TextureChanges
            if (hairstyle.ObjDesc.TextureChanges.Count > 0)
                return hairstyle.ObjDesc.TextureChanges[0].OldTexture;
            else
                return null;
        }

        // Headgear
        public uint GetHeadgearWeenie(uint headgearStyle)
        {
            return HeadgearList[Convert.ToInt32(headgearStyle)].WeenieDefault;
        }
        public uint GetHeadgearClothingTable(uint headgearStyle)
        {
            return HeadgearList[Convert.ToInt32(headgearStyle)].ClothingTable;
        }

        // Shirt
        public uint GetShirtWeenie(uint shirtStyle)
        {
            return ShirtList[Convert.ToInt32(shirtStyle)].WeenieDefault;
        }
        public uint GetShirtClothingTable(uint shirtStyle)
        {
            return ShirtList[Convert.ToInt32(shirtStyle)].ClothingTable;
        }

        // Pants
        public uint GetPantsWeenie(uint pantsStyle)
        {
            return PantsList[Convert.ToInt32(pantsStyle)].WeenieDefault;
        }
        public uint GetPantsClothingTable(uint pantsStyle)
        {
            return PantsList[Convert.ToInt32(pantsStyle)].ClothingTable;
        }

        // Footwear
        public uint GetFootwearWeenie(uint footwearStyle)
        {
            return FootwearList[Convert.ToInt32(footwearStyle)].WeenieDefault;
        }
        public uint GetFootwearClothingTable(uint footwearStyle)
        {
            return FootwearList[Convert.ToInt32(footwearStyle)].ClothingTable;
        }

        public void Unpack(BinaryReader reader)
        {
            Name            = reader.ReadString();
            Scale           = reader.ReadUInt32();
            SetupID         = reader.ReadUInt32();
            SoundTable      = reader.ReadUInt32();
            IconImage       = reader.ReadUInt32();
            BasePalette     = reader.ReadUInt32();
            SkinPalSet      = reader.ReadUInt32();
            PhysicsTable    = reader.ReadUInt32();
            MotionTable     = reader.ReadUInt32();
            CombatTable     = reader.ReadUInt32();

            BaseObjDesc.Unpack(reader);

            HairColorList.UnpackSmartArray(reader);
            HairStyleList.UnpackSmartArray(reader);
            EyeColorList.UnpackSmartArray(reader);
            EyeStripList.UnpackSmartArray(reader);
            NoseStripList.UnpackSmartArray(reader);
            MouthStripList.UnpackSmartArray(reader);

            HeadgearList.UnpackSmartArray(reader);
            ShirtList.UnpackSmartArray(reader);
            PantsList.UnpackSmartArray(reader);
            FootwearList.UnpackSmartArray(reader);
            ClothingColorsList.UnpackSmartArray(reader);
        }

        /// <summary>
        /// Reads a sex as the pre-ToD (2005) client stored it: full 32-bit counts and strings, the credits, skills and templates (which belong to the sex there, and are skipped here),
        /// and no scale, physics, motion or combat table and no alternate hair setup (they read as 0).
        /// </summary>
        public void UnpackPreToD(BinaryReader reader)
        {
            Name        = reader.ReadPreToDString();
            SetupID     = reader.ReadUInt32();
            SoundTable  = reader.ReadUInt32();
            IconImage   = reader.ReadUInt32();
            reader.ReadUInt32(); // naming help string

            BaseObjDesc.Unpack(reader);
            reader.BaseStream.Position += 8;

            reader.SkipPreToDList(r => r.ReadPreToDString());
            reader.SkipPreToDList(r => { r.ReadPreToDString(); r.ReadPreToDString(); r.ReadUInt32(); });
            reader.BaseStream.Position += 12; // attribute credits, a word, skill credits
            reader.SkipPreToDList(r => { r.ReadPreToDString(); r.BaseStream.Position += 8; });
            reader.SkipPreToDList(r => r.BaseStream.Position += 12);
            reader.SkipPreToDList(r =>
            {
                r.ReadPreToDString();
                r.BaseStream.Position += 8;
                r.SkipPreToDList(p =>
                {
                    p.BaseStream.Position += 24;
                    for (var i = 0; i < 3; i++)
                        p.SkipPreToDList(s => s.BaseStream.Position += 4);
                });
            });
            reader.SkipPreToDList(r => r.BaseStream.Position += 17 * 4); // heraldry

            BasePalette = reader.ReadUInt32();
            SkinPalSet  = reader.ReadUInt32();

            HairColorList.AddRange(reader.ReadPreToDList(r => r.ReadUInt32()));
            HairStyleList.AddRange(reader.ReadPreToDList(r => { var hairStyle = new HairStyleCG(); hairStyle.UnpackPreToD(r); return hairStyle; }));
            EyeColorList.AddRange(reader.ReadPreToDList(r => r.ReadUInt32()));
            EyeStripList.AddRange(reader.ReadPreToDList(r => { var eyeStrip = new EyeStripCG(); eyeStrip.Unpack(r); return eyeStrip; }));
            NoseStripList.AddRange(reader.ReadPreToDList(r => { var strip = new FaceStripCG(); strip.Unpack(r); return strip; }));
            MouthStripList.AddRange(reader.ReadPreToDList(r => { var strip = new FaceStripCG(); strip.Unpack(r); return strip; }));

            foreach (var gear in new[] { HeadgearList, ShirtList, PantsList, FootwearList })
                gear.AddRange(reader.ReadPreToDList(r => { var item = new GearCG(); item.UnpackPreToD(r); return item; }));

            var colorCount = reader.ReadUInt32();
            reader.ReadUInt32(); // the largest index
            for (var i = 0; i < colorCount; i++)
                ClothingColorsList.Add(reader.ReadUInt32());
        }

        /// <summary>Takes the look of the head from another sex: the body's object description and the hair, eye, nose and mouth lists.</summary>
        public void TakeFaceFrom(SexCG other)
        {
            BaseObjDesc = other.BaseObjDesc;

            HairStyleList.Clear();
            HairStyleList.AddRange(other.HairStyleList);
            EyeStripList.Clear();
            EyeStripList.AddRange(other.EyeStripList);
            NoseStripList.Clear();
            NoseStripList.AddRange(other.NoseStripList);
            MouthStripList.Clear();
            MouthStripList.AddRange(other.MouthStripList);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Scale);
            writer.Write(SetupID);
            writer.Write(SoundTable);
            writer.Write(IconImage);
            writer.Write(BasePalette);
            writer.Write(SkinPalSet);
            writer.Write(PhysicsTable);
            writer.Write(MotionTable);
            writer.Write(CombatTable);

            BaseObjDesc.Pack(writer);

            HairColorList.PackSmartArray(writer);
            HairStyleList.PackSmartArray(writer);
            EyeColorList.PackSmartArray(writer);
            EyeStripList.PackSmartArray(writer);
            NoseStripList.PackSmartArray(writer);
            MouthStripList.PackSmartArray(writer);

            HeadgearList.PackSmartArray(writer);
            ShirtList.PackSmartArray(writer);
            PantsList.PackSmartArray(writer);
            FootwearList.PackSmartArray(writer);
            ClothingColorsList.PackSmartArray(writer);
        }
    }
}
