using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Factories.Tables;
using ACE.Server.Managers;
using ACE.Server.Pathfinding;
using ACE.Server.WorldObjects.Entity;
using log4net;
using System;
using System.Collections.Generic;
using System.Numerics;
using Position = ACE.Entity.Position;

namespace ACE.Server.WorldObjects
{
    public partial class Creature : Container
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool IsHumanoid { get => (this is Player || AiAllowedCombatStyle != CombatStyle.Undef); } // Our definition of humanoid in this case is a creature that can wield weapons.
        public bool IsExhausted { get => Stamina.Current == 0; }

        protected QuestManager _questManager;

        public QuestManager QuestManager
        {
            get
            {
                if (_questManager == null)
                {
                    /*if (!(this is Player))
                        log.DebugFormat("Initializing non-player QuestManager for {0} (0x{1})", Name, Guid);*/

                    _questManager = new QuestManager(this);
                }

                return _questManager;
            }
        }

        /// <summary>
        /// A table of players who currently have their targeting reticule on this creature
        /// </summary>
        private Dictionary<uint, WorldObjectInfo> selectedTargets;

        /// <summary>
        /// A list of ammo types and amount that we've been hit with. Used so we can drop some of that on our corpse.
        /// </summary>
        public Dictionary<uint, int> ammoHitWith;

        /// <summary>
        /// A decaying count of attacks this creature has received recently.
        /// </summary>
        public int numRecentAttacksReceived;
        public float attacksReceivedPerSecond;

        /// <summary>
        /// Currently used to handle some edge cases for faction mobs
        /// DamageHistory.HasDamager() has the following issues:
        /// - if a player attacks a same-factioned mob but is evaded, the mob would quickly de-aggro
        /// - if a player attacks a same-factioned mob in a group of same-factioned mobs, the other nearby faction mobs should be alerted, and should maintain aggro, even without a DamageHistory entry
        /// - if a summoner attacks a same-factioned mob, should the summoned CombatPet possibly defend the player in that situation?
        /// </summary>
        //public HashSet<uint> RetaliateTargets { get; set; }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Creature(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            InitializePropertyDictionaries();
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Creature(Biota biota) : base(biota)
        {
            InitializePropertyDictionaries();
            SetEphemeralValues();
        }

        private void InitializePropertyDictionaries()
        {
            if (Biota.PropertiesAttribute == null)
                Biota.PropertiesAttribute = new Dictionary<PropertyAttribute, PropertiesAttribute>();
            if (Biota.PropertiesAttribute2nd == null)
                Biota.PropertiesAttribute2nd = new Dictionary<PropertyAttribute2nd, PropertiesAttribute2nd>();
            if (Biota.PropertiesBodyPart == null)
                Biota.PropertiesBodyPart = new Dictionary<CombatBodyPart, PropertiesBodyPart>();
            if (Biota.PropertiesSkill == null)
                Biota.PropertiesSkill = new Dictionary<Skill, PropertiesSkill>();
        }

        private void SetEphemeralValues()
        {
            CombatMode = CombatMode.NonCombat;
            DamageHistory = new DamageHistory(this);

            if (!(this is Player))
                GenerateNewFace();

            // If any of the vitals don't exist for this biota, one will be created automatically in the CreatureVital ctor
            Vitals[PropertyAttribute2nd.MaxHealth] = new CreatureVital(this, PropertyAttribute2nd.MaxHealth);
            Vitals[PropertyAttribute2nd.MaxStamina] = new CreatureVital(this, PropertyAttribute2nd.MaxStamina);
            Vitals[PropertyAttribute2nd.MaxMana] = new CreatureVital(this, PropertyAttribute2nd.MaxMana);

            // If any of the attributes don't exist for this biota, one will be created automatically in the CreatureAttribute ctor
            Attributes[PropertyAttribute.Strength] = new CreatureAttribute(this, PropertyAttribute.Strength);
            Attributes[PropertyAttribute.Endurance] = new CreatureAttribute(this, PropertyAttribute.Endurance);
            Attributes[PropertyAttribute.Coordination] = new CreatureAttribute(this, PropertyAttribute.Coordination);
            Attributes[PropertyAttribute.Quickness] = new CreatureAttribute(this, PropertyAttribute.Quickness);
            Attributes[PropertyAttribute.Focus] = new CreatureAttribute(this, PropertyAttribute.Focus);
            Attributes[PropertyAttribute.Self] = new CreatureAttribute(this, PropertyAttribute.Self);

            foreach (var kvp in Biota.PropertiesSkill)
                Skills[kvp.Key] = new CreatureSkill(this, kvp.Key, kvp.Value);

            if (Health.Current <= 0)
                Health.Current = Health.MaxValue;
            if (Stamina.Current <= 0)
                Stamina.Current = Stamina.MaxValue;
            if (Mana.Current <= 0)
                Mana.Current = Mana.MaxValue;

            if (!(this is Player))
            {
                GenerateWieldList();

                EquipInventoryItems();

                GenerateWieldedTreasure();

                EquipInventoryItems();

                GenerateInventoryTreasure();

                // TODO: fix tod data
                Health.Current = Health.MaxValue;
                Stamina.Current = Stamina.MaxValue;
                Mana.Current = Mana.MaxValue;
            }

            SetMonsterState();

            CurrentMotionState = new Motion(MotionStance.NonCombat, MotionCommand.Ready);

            selectedTargets = new Dictionary<uint, WorldObjectInfo>();

            ammoHitWith = new Dictionary<uint, int>();

            numRecentAttacksReceived = 0;
            attacksReceivedPerSecond = 0.0f;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                PathfindingEnabled = Pathfinder.PathfindingEnabled;

                if (!Tier.HasValue && WeenieType != WeenieType.Vendor)
                    Tier = CalculateExtendedTier();
            }
        }

        public override void BeforeEnterWorld()
        {
            if (IsNPC)
                GenerateNewFace(); // Now that we have our location we can generate our pseudo-random appearance.

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                UpdateDefenseCapBonus();

                if (PathfindingEnabled && Location != null && Location.Indoors)
                    Pathfinder.TryLoadMesh(Location);
            }

            if (AiIncapableOfAnyMotion && Location != null && !Location.Indoors)
                Location.AdjustMapCoords(); // These won't fall to the ground after spawn so we have to adjust their position here.
        }

        public override void OnGeneration(WorldObject generator)
        {
            base.OnGeneration(generator);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && IsMonster && Tolerance == Tolerance.None)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(5);
                actionChain.AddAction(this, () =>
                {
                    if (Location == null || CurrentLandblock == null)
                        return;

                    int seed = Time.GetDateTimeFromTimestamp(Time.GetUnixTime()).DayOfYear + (CurrentLandblock.Id.LandblockX << 8 | CurrentLandblock.Id.LandblockY);

                    Random pseudoRandom = new Random(seed);
                    var trapExtraChance = pseudoRandom.NextSingle();
                    var chestExtraChance = pseudoRandom.NextSingle();

                    var baseChestChance = 0.0075;
                    var baseTrapChance = 0.05;

                    if (InDungeon)
                    {
                        var trapRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
                        if (trapRoll < baseTrapChance + (baseTrapChance * trapExtraChance))
                            DeployRandomTrap();
                    }

                    bool hasChest = false;
                    if (!InDungeon && !AiIncapableOfAnyMotion)
                    {
                        var chestRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
                        if (chestRoll < baseChestChance + (baseChestChance * chestExtraChance))
                            hasChest = DeploySpecialChest();
                    }

                    if (!hasChest && Indoors)
                    {
                        var chestRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
                        if (chestRoll < baseChestChance + (baseChestChance * chestExtraChance))
                            hasChest = DeployChest();
                    }

                    if (!hasChest && (Indoors || AiIncapableOfAnyMotion))
                    {
                        var chestRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
                        if (chestRoll < baseChestChance + (baseChestChance * chestExtraChance))
                            hasChest = DeployHiddenChest();
                    }

                    if (!hasChest && !AiIncapableOfAnyMotion)
                    {
                        var corpseRoll = ThreadSafeRandom.Next(0.0f, 1.0f);
                        if (corpseRoll < baseChestChance + (baseChestChance * chestExtraChance))
                            DeployHiddenCorpse();
                    }
                });
                actionChain.EnqueueChain();
            }
        }

        public List<WorldObjectInfo> DeployedObjects = new List<WorldObjectInfo>();

        private List<List<uint>> Chests = new List<List<uint>>
        {
            new List<uint>{ 27243, 27245, 50067, 27244, 50075, 27242, 50069, 1932, 3978 },
            new List<uint>{ 1915, 3961, 1924, 3970, 1918, 3964, 7493, 1930, 3976, 1921, 3967, 1927, 3973, 1934, 3980, 1937, 3983, 1940, 3986, 1943, 3989, 1949, 3995, 1913, 3959 },
            new List<uint>{ 1916, 3962, 1925, 3971, 1919, 3965, 7500, 1931, 3977, 1922, 3968, 1928, 3974, 1935, 3981, 1938, 3984, 1941, 3987, 1944, 3990, 1950, 3996 },
            new List<uint>{ 3960, 2544, 1914, 1923, 3969, 1917, 3963, 7494, 7495, 1929, 3975, 1920, 3966, 1926, 3972, 1933, 3979, 1936, 3982, 1939, 3985, 1942, 3988, 1948, 3994, 7297, 1912, 3958  },
            new List<uint>{ 50252, 50253 },
            new List<uint>{ 24476, 50254, 50255 }
        };

        private List<List<uint>> Corpses = new List<List<uint>>
        {
            new List<uint>{ 50074 },
            new List<uint>{ 4180 },
            new List<uint>{ 4381 },
            new List<uint>{ 4382 },
            new List<uint>{ 50241 },
            new List<uint>{ 50242 }
        };

        public bool DeployChest()
        {
            DeployedObjects.RemoveAll(x => x.TryGetWorldObject() == null);

            var chestList = Chests;
            var isCorpse = false;
            if (!IsHumanoid || ThreadSafeRandom.Next(0.0f, 1.0f) < 0.20f)
            {
                isCorpse = true;
                chestList = Corpses;
            }

            var tier = Math.Clamp(RollTier(), 1, chestList.Count);

            var chestWcidsList = chestList[tier - 1];
            if (chestWcidsList == null || chestWcidsList.Count == 0)
                return false;

            var chestWcid = chestWcidsList[ThreadSafeRandom.Next(0, chestWcidsList.Count - 1)];

            if (chestWcid == 0)
                return false;

            var chest = WorldObjectFactory.CreateNewWorldObject(chestWcid);
            if (chest == null)
                return false;

            var radius = PhysicsObj.GetRadius() + 0.5f;
            var chestLocation = new Position(Location);
            if (PathfindingEnabled && Location.Indoors && (!isCorpse || ThreadSafeRandom.Next(0.0f, 1.0f) < 0.5f))
            {
                var randomPos = Pathfinder.GetRandomPointWithinCircle(chestLocation, 40, AgentWidth.Wide);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

                randomPos = Pathfinder.GetNearestWallPosition(chestLocation, 20, AgentWidth.Narrow, out _, true);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));
            }
            else
                chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

            if(isCorpse)
                chestLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));

            chest.Location = chestLocation;
            chest.Location.LandblockId = new LandblockId(chest.Location.GetCell());

            chest.Generator = this;
            chest.Tier = tier;

            if (chest.EnterWorld())
            {
                DeployedObjects.Add(new WorldObjectInfo(chest));
                return true;
            }

            if (chest != null)
                chest.Destroy();

            return false;
        }

        private List<List<uint>> RunedChests = new List<List<uint>>
        {
            new List<uint>{ 50203, 50202 },
            new List<uint>{ 22572, 22568 },
            new List<uint>{ 22576, 22570 },
            new List<uint>{ 22571, 22567 },
            new List<uint>{ 50244, 22566 },
            new List<uint>{ 50245, 50243 }
        };

        private List<List<uint>> RunedCorpses = new List<List<uint>>
        {
            new List<uint>{ 50256 },
            new List<uint>{ 50257 },
            new List<uint>{ 50258 },
            new List<uint>{ 50259 },
            new List<uint>{ 50260 },
            new List<uint>{ 50308 }
        };

        private List<List<uint>> VirindiChests = new List<List<uint>>
        {
            null,
            new List<uint>{ 9287 },
            new List<uint>{ 9286 },
            new List<uint>{ 9288 },
            new List<uint>{ 8999, 9001 },
            new List<uint>{ 8999, 9001 }
        };

        private List<Tuple<List<uint>, List<uint>>> SpecialChestsByKeyDrop = new List<Tuple<List<uint>, List<uint>>>
        {
            new Tuple<List<uint>, List<uint>>(new List<uint>{ 23107, 23108 }, new List<uint> { 23085, 23086 }), // Mangled Dark Key and Twisted Dark Key
            new Tuple<List<uint>, List<uint>>(new List<uint>{ 30823 }, new List<uint> { 30796 }), // Broken Black Marrow Key
        };

        public bool DeploySpecialChest()
        {
            DeployedObjects.RemoveAll(x => x.TryGetWorldObject() == null);

            List<uint> chestWcidsList = null;
            var chestList = RunedChests;
            var isCorpse = false;

            var tier = RollTier();

            if (Biota.PropertiesCreateList != null)
            {
                foreach (var createListEntry in Biota.PropertiesCreateList)
                {
                    foreach (var entry in SpecialChestsByKeyDrop)
                    {
                        if (entry.Item1.Contains(createListEntry.WeenieClassId))
                        {
                            chestWcidsList = entry.Item2;
                            break;
                        }
                    }
                }
            }

            if (chestWcidsList == null)
            {
                if (CreatureType == ACE.Entity.Enum.CreatureType.Virindi || FriendType == ACE.Entity.Enum.CreatureType.Virindi)
                    chestList = VirindiChests;
                else if (!IsHumanoid)
                {
                    isCorpse = true;
                    chestList = RunedCorpses;
                }

                tier = Math.Clamp(tier, 1, chestList.Count);                

                chestWcidsList = chestList[tier - 1];
                if (chestWcidsList == null || chestWcidsList.Count == 0)
                    return false;
            }

            var chestWcid = chestWcidsList[ThreadSafeRandom.Next(0, chestWcidsList.Count - 1)];

            if (chestWcid == 0)
                return false;

            var specialChest = WorldObjectFactory.CreateNewWorldObject(chestWcid);
            if (specialChest == null)
                return false;

            var radius = PhysicsObj.GetRadius() + 0.5f;
            var chestLocation = new Position(Location);
            if (PathfindingEnabled && Location.Indoors && (!isCorpse || ThreadSafeRandom.Next(0.0f, 1.0f) < 0.5f))
            {
                var randomPos = Pathfinder.GetRandomPointWithinCircle(chestLocation, 40, AgentWidth.Wide);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

                randomPos = Pathfinder.GetNearestWallPosition(chestLocation, 20, AgentWidth.Narrow, out _, true);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));
            }
            else
                chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

            if (isCorpse)
                chestLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));

            specialChest.Location = chestLocation;
            specialChest.Location.LandblockId = new LandblockId(specialChest.Location.GetCell());

            specialChest.Generator = this;
            specialChest.Tier = tier;

            if (specialChest.EnterWorld())
            {
                DeployedObjects.Add(new WorldObjectInfo(specialChest));
                return true;
            }

            if (specialChest != null)
                specialChest.Destroy();

            return false;
        }

        private List<uint> HiddenChests = new List<uint>()
        {
            50144,
            50145,
            50146,
            50147,
            50148,
            50149,
        };

        public bool DeployHiddenChest()
        {
            DeployedObjects.RemoveAll(x => x.TryGetWorldObject() == null);

            var tier = Math.Clamp(RollTier(), 1, HiddenChests.Count);

            var chestWcid = HiddenChests[tier - 1];
            if (chestWcid == 0)
                return false;

            var hiddenChest = WorldObjectFactory.CreateNewWorldObject(chestWcid);
            if (hiddenChest == null)
                return false;

            var radius = PhysicsObj.GetRadius() + 0.5f;
            var chestLocation = new Position(Location);
            if (PathfindingEnabled && Location.Indoors)
            {
                var randomPos = Pathfinder.GetRandomPointWithinCircle(chestLocation, 40, AgentWidth.Wide);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

                randomPos = Pathfinder.GetNearestWallPosition(chestLocation, 20, AgentWidth.Narrow, out _, true);
                if (randomPos != null)
                    chestLocation = randomPos;
                else
                    chestLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));
            }
            else
                chestLocation = chestLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

            hiddenChest.Location = chestLocation;
            hiddenChest.Location.LandblockId = new LandblockId(hiddenChest.Location.GetCell());

            hiddenChest.Generator = this;
            hiddenChest.Tier = tier;
            hiddenChest.ResistAwareness = (int)(Tier * 65);

            if (ThreadSafeRandom.Next(0.0f, 1.0f) < 0.5f)
                hiddenChest.IsLocked = true;

            if (hiddenChest.EnterWorld())
            {
                DeployedObjects.Add(new WorldObjectInfo(hiddenChest));
                return true;
            }

            if (hiddenChest != null)
                hiddenChest.Destroy();

            return false;
        }

        private List<uint> HiddenCorpses = new List<uint>()
        {
            50246,
            50247,
            50248,
            50249,
            50250,
            50251,
        };

        public bool DeployHiddenCorpse()
        {
            DeployedObjects.RemoveAll(x => x.TryGetWorldObject() == null);

            var tier = Math.Clamp(RollTier(), 1, HiddenCorpses.Count);

            var chestWcid = HiddenCorpses[tier - 1];
            if (chestWcid == 0)
                return false;

            var hiddenCorpse = WorldObjectFactory.CreateNewWorldObject(chestWcid);
            if (hiddenCorpse == null)
                return false;

            var radius = PhysicsObj.GetRadius() + 0.5f;
            var corpseLocation = new Position(Location);
            if (PathfindingEnabled && Location.Indoors && ThreadSafeRandom.Next(0.0f, 1.0f) < 0.5f)
            {
                var randomPos = Pathfinder.GetRandomPointWithinCircle(corpseLocation, 40, AgentWidth.Wide);
                if (randomPos != null)
                    corpseLocation = randomPos;
                else
                    corpseLocation = corpseLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

                randomPos = Pathfinder.GetNearestWallPosition(corpseLocation, 20, AgentWidth.Narrow, out _, true);
                if (randomPos != null)
                    corpseLocation = randomPos;
            }
            else
                corpseLocation = corpseLocation.InFrontOf(AiIncapableOfAnyMotion ? radius : -radius, true);

            corpseLocation.SetRotation((float)ThreadSafeRandom.Next(0f, 360f));

            hiddenCorpse.Location = corpseLocation;
            hiddenCorpse.Location.LandblockId = new LandblockId(hiddenCorpse.Location.GetCell());

            hiddenCorpse.Generator = this;
            hiddenCorpse.Tier = tier;
            hiddenCorpse.ResistAwareness = (int)(Tier * 65);

            if (hiddenCorpse.EnterWorld())
            {
                DeployedObjects.Add(new WorldObjectInfo(hiddenCorpse));
                return true;
            }

            if (hiddenCorpse != null)
                hiddenCorpse.Destroy();

            return false;
        }

        private List<SpellId> TrapSpells = new List<SpellId>()
        {
            SpellId.ForceBolt1,
            SpellId.WhirlingBlade1,
            SpellId.ShockWave1,
            SpellId.FlameBolt1,
            SpellId.FrostBolt1,
            SpellId.LightningBolt1,
            SpellId.AcidStream1,
            SpellId.HarmOther1,
        };

        public bool DeployRandomTrap()
        {
            DeployedObjects.RemoveAll(x => x.TryGetWorldObject() == null);

            var trapObject = WorldObjectFactory.CreateNewWorldObject(50143);
            var trapTrigger = WorldObjectFactory.CreateNewWorldObject(2131);

            var triggerLocation = new Position(Location);
            if (PathfindingEnabled && Location.Indoors)
            {
                var randomPos = Pathfinder.GetRandomPointWithinCircle(triggerLocation, 40, AgentWidth.Narrow);
                if (randomPos != null)
                    triggerLocation = randomPos;
                else
                    triggerLocation = triggerLocation.InFrontOf(2);
            }
            else
                triggerLocation = triggerLocation.InFrontOf(2);

            var trapLocation = new Position(triggerLocation);
            if (PathfindingEnabled && Location.Indoors)
            {
                var randomPos = Pathfinder.GetNearestWallPosition(trapLocation, 20, AgentWidth.Narrow, out _);
                if (randomPos != null)
                    trapLocation = randomPos;
                else
                    trapLocation = trapLocation.InFrontOf(8);
            }
            else
                trapLocation = trapLocation.InFrontOf(8);
            trapLocation.Rotate(GetDirection(trapLocation.ToGlobal(), triggerLocation.ToGlobal()));

            trapObject.Location = trapLocation;
            trapObject.Location.PositionZ += 2.5f;
            trapObject.Location.LandblockId = new LandblockId(trapObject.Location.GetCell());
            trapObject.Generator = this;
            var tier = RollTier();
            trapObject.Tier = tier;
            trapObject.SpellDID = (uint)SpellLevelProgression.GetSpellAtLevel((SpellId)TrapSpells[ThreadSafeRandom.Next(0, TrapSpells.Count - 1)], tier + 1);
            trapObject.ItemSpellcraft = (tier + 1) * 50;

            if (trapObject.EnterWorld())
            {
                trapTrigger.Location = triggerLocation;
                trapTrigger.Location.LandblockId = new LandblockId(trapTrigger.Location.GetCell());
                trapTrigger.ActivationTarget = trapObject.Guid.Full;
                trapTrigger.Generator = this;
                trapTrigger.ResistAwareness = (int)(Tier * 65);

                if (trapTrigger.EnterWorld())
                {
                    DeployedObjects.Add(new WorldObjectInfo(trapObject));
                    DeployedObjects.Add(new WorldObjectInfo(trapTrigger));
                    return true;
                }
            }

            if (trapObject != null)
                trapObject.Destroy();
            if (trapTrigger != null)
                trapTrigger.Destroy();

            return false;
        }

        // verify logic
        public bool IsNPC => !(this is Player) && !Attackable && TargetingTactic == TargetingTactic.None;

        public void GenerateNewFace()
        {
            if (IsNPC && Location == null)
                return; // We shall finish this later when we have our location.

            var cg = DatManager.PortalDat.CharGen;

            if (!Heritage.HasValue)
            {
                if (!string.IsNullOrEmpty(HeritageGroupName) && Enum.TryParse(HeritageGroupName.Replace("'", ""), true, out HeritageGroup heritage))
                    Heritage = (int)heritage;
            }

            if (!Gender.HasValue)
            {
                if (!string.IsNullOrEmpty(Sex) && Enum.TryParse(Sex, true, out Gender gender))
                    Gender = (int)gender;
            }

            if (!Heritage.HasValue || !Gender.HasValue)
            {
#if DEBUG
                //if (!(NpcLooksLikeObject ?? false))
                    //log.DebugFormat("Creature.GenerateNewFace: {0} (0x{1}) - wcid {2} - Heritage: {3} | HeritageGroupName: {4} | Gender: {5} | Sex: {6} - Data missing or unparsable, Cannot randomize face.", Name, Guid, WeenieClassId, Heritage, HeritageGroupName, Gender, Sex);
#endif
                return;
            }

            if (!cg.HeritageGroups.TryGetValue((uint)Heritage, out var heritageGroup) || !heritageGroup.Genders.TryGetValue((int)Gender, out var sex))
            {
#if DEBUG
                log.DebugFormat("Creature.GenerateNewFace: {0} (0x{1}) - wcid {2} - Heritage: {3} | HeritageGroupName: {4} | Gender: {5} | Sex: {6} - Data invalid, Cannot randomize face.", Name, Guid, WeenieClassId, Heritage, HeritageGroupName, Gender, Sex);
#endif
                return;
            }

            PaletteBaseId = sex.BasePalette;

            var appearance = new Appearance
            {
                HairStyle = 1,
                HairColor = 1,
                HairHue = 1,

                EyeColor = 1,
                Eyes = 1,

                Mouth = 1,
                Nose = 1,

                SkinHue = 1
            };

            DatLoader.Entity.HairStyleCG hairstyle;
            if (!IsNPC)
            {
                // Get the hair first, because we need to know if you're bald, and that's the name of that tune!
                if (sex.HairStyleList.Count > 1)
                {
                    if (PropertyManager.GetBool("npc_hairstyle_fullrange").Item)
                        appearance.HairStyle = (uint)ThreadSafeRandom.Next(0, sex.HairStyleList.Count - 1);
                    else
                        appearance.HairStyle = (uint)ThreadSafeRandom.Next(0, Math.Min(sex.HairStyleList.Count - 1, 8)); // retail range data compiled by OptimShi
                }
                else
                    appearance.HairStyle = 0;

                if (sex.HairStyleList.Count < appearance.HairStyle)
                {
                    log.Warn($"Creature.GenerateNewFace: {Name} (0x{Guid}) - wcid {WeenieClassId} - HairStyle = {appearance.HairStyle} | HairStyleList.Count = {sex.HairStyleList.Count} - Data invalid, Cannot randomize face.");
                    return;
                }

                hairstyle = sex.HairStyleList[Convert.ToInt32(appearance.HairStyle)];

                appearance.HairColor = (uint)ThreadSafeRandom.Next(0, sex.HairColorList.Count - 1);
                appearance.HairHue = ThreadSafeRandom.Next(0.0f, 0.7f); // Leave the overly bright hair colors to players.

                appearance.EyeColor = (uint)ThreadSafeRandom.Next(0, sex.EyeColorList.Count - 1);
                appearance.Eyes = (uint)ThreadSafeRandom.Next(0, sex.EyeStripList.Count - 1);

                appearance.Mouth = (uint)ThreadSafeRandom.Next(0, sex.MouthStripList.Count - 1);

                appearance.Nose = (uint)ThreadSafeRandom.Next(0, sex.NoseStripList.Count - 1);

                appearance.SkinHue = ThreadSafeRandom.Next(0.0f, 1.0f);
            }
            else
            {
                // Let's use a pseudo random seed based on our WCID so we mantain the same appearance between sessions. Retail did not persist NPC appearances at all but I feel this is one detail we can diverge on.

                // By adding the location to our seed we avoid having all instances of the same WCID having the same appearance, this would affect NPCs such as town criers and collectors.
                int seed = (int)WeenieClassId + (int)(1000.0f * (Location.PositionX + Location.PositionY + Location.PositionZ));

                Random pseudoRandom = new Random(seed); // Note that this class uses EXCLUSIVE max values instead of inclusive for our regular ThreadSafeRandom.

                // Get the hair first, because we need to know if you're bald, and that's the name of that tune!
                if (sex.HairStyleList.Count > 1)
                {
                    if (PropertyManager.GetBool("npc_hairstyle_fullrange").Item)
                        appearance.HairStyle = (uint)pseudoRandom.Next(0, sex.HairStyleList.Count);
                    else
                        appearance.HairStyle = (uint)pseudoRandom.Next(0, Math.Min(sex.HairStyleList.Count, 9)); // retail range data compiled by OptimShi
                }
                else
                    appearance.HairStyle = 0;

                if (sex.HairStyleList.Count < appearance.HairStyle)
                {
                    log.Warn($"Creature.GenerateNewFace: {Name} (0x{Guid}) - wcid {WeenieClassId} - HairStyle = {appearance.HairStyle} | HairStyleList.Count = {sex.HairStyleList.Count} - Data invalid, Cannot randomize face.");
                    return;
                }

                hairstyle = sex.HairStyleList[Convert.ToInt32(appearance.HairStyle)];

                appearance.HairColor = (uint)pseudoRandom.Next(0, sex.HairColorList.Count);
                appearance.HairHue = pseudoRandom.Next(0, 61) / 100.0; // Leave the overly bright hair colors to players.

                appearance.EyeColor = (uint)pseudoRandom.Next(0, sex.EyeColorList.Count);
                appearance.Eyes = (uint)pseudoRandom.Next(0, sex.EyeStripList.Count);

                appearance.Mouth = (uint)pseudoRandom.Next(0, sex.MouthStripList.Count);

                appearance.Nose = (uint)pseudoRandom.Next(0, sex.NoseStripList.Count);

                appearance.SkinHue = pseudoRandom.NextDouble();
            }

            //// Certain races (Undead, Tumeroks, Others?) have multiple body styles available. This is controlled via the "hair style".
            ////if (hairstyle.AlternateSetup > 0)
            ////    character.SetupTableId = hairstyle.AlternateSetup;

            if (!EyesTextureDID.HasValue)
                EyesTextureDID = sex.GetEyeTexture(appearance.Eyes, hairstyle.Bald);
            if (!DefaultEyesTextureDID.HasValue)
                DefaultEyesTextureDID = sex.GetDefaultEyeTexture(appearance.Eyes, hairstyle.Bald);
            if (!NoseTextureDID.HasValue)
                NoseTextureDID = sex.GetNoseTexture(appearance.Nose);
            if (!DefaultNoseTextureDID.HasValue)
                DefaultNoseTextureDID = sex.GetDefaultNoseTexture(appearance.Nose);
            if (!MouthTextureDID.HasValue)
                MouthTextureDID = sex.GetMouthTexture(appearance.Mouth);
            if (!DefaultMouthTextureDID.HasValue)
                DefaultMouthTextureDID = sex.GetDefaultMouthTexture(appearance.Mouth);
            if (!HeadObjectDID.HasValue)
                HeadObjectDID = sex.GetHeadObject(appearance.HairStyle);

            // Skin is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var skinPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.SkinPalSet);
            if (!SkinPaletteDID.HasValue)
                SkinPaletteDID = skinPalSet.GetPaletteID(appearance.SkinHue);

            // Hair is stored as PaletteSet (list of Palettes), so we need to read in the set to get the specific palette
            var hairPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(sex.HairColorList[Convert.ToInt32(appearance.HairColor)]);
            if (!HairPaletteDID.HasValue)
                HairPaletteDID = hairPalSet.GetPaletteID(appearance.HairHue);

            // Eye Color
            if (!EyesPaletteDID.HasValue)
                EyesPaletteDID = sex.EyeColorList[Convert.ToInt32(appearance.EyeColor)];
        }

        public virtual float GetBurdenMod()
        {
            return 1.0f;    // override for players
        }

        /// <summary>
        /// This will be false when creature is dead and waits for respawn
        /// </summary>
        public bool IsAlive { get => Health.Current > 0; }

        /// <summary>
        /// Sends the network commands to move a player towards an object
        /// </summary>
        public void MoveToObject(WorldObject target, float? useRadius = null)
        {
            var distanceToObject = useRadius ?? target.UseRadius ?? 0.6f;

            var moveToObject = new Motion(this, target, MovementType.MoveToObject);
            moveToObject.MoveToParameters.DistanceToObject = distanceToObject;

            // move directly to portal origin
            //if (target is Portal)
                //moveToObject.MoveToParameters.MovementParameters &= ~MovementParams.UseSpheres;

            SetWalkRunThreshold(moveToObject, target.Location);

            EnqueueBroadcastMotion(moveToObject);
        }

        /// <summary>
        /// Sends the network commands to move a player towards a position
        /// </summary>
        public void MoveToPosition(Position position, float? radius = null)
        {
            var distanceToObject = radius ?? 0.6f;

            var moveToPosition = new Motion(this, position);
            moveToPosition.MoveToParameters.DistanceToObject = distanceToObject;

            SetWalkRunThreshold(moveToPosition, position);

            EnqueueBroadcastMotion(moveToPosition);
        }

        public void SetWalkRunThreshold(Motion motion, Position targetLocation)
        {
            // FIXME: WalkRunThreshold (default 15 distance) seems to not be used automatically by client
            // player will always walk instead of run, and if MovementParams.CanCharge is sent, they will always charge
            // to remedy this, we manually calculate a threshold based on WalkRunThreshold

            var dist = Location.DistanceTo(targetLocation);
            if (dist >= motion.MoveToParameters.WalkRunThreshold / 2.0f)     // default 15 distance seems too far, especially with weird in-combat walking animation?
            {
                motion.MoveToParameters.MovementParameters |= MovementParams.CanCharge;

                // TODO: find the correct runrate here
                // the default runrate / charge seems much too fast...
                //motion.RunRate = GetRunRate() / 4.0f;
                motion.RunRate = GetRunRate();
            }
        }

        /// <summary>
        /// This is raised by Player.HandleActionUseItem.<para />
        /// The item does not exist in the players possession.<para />
        /// If the item was outside of range, the player will have been commanded to move using DoMoveTo before ActOnUse is called.<para />
        /// When this is called, it should be assumed that the player is within range.
        /// 
        /// This is the OnUse method.   This is just an initial implemention.   I have put in the turn to action at this point.
        /// If we are out of use radius, move to the object.   Once in range, let's turn the creature toward us and get started.
        /// Note - we may need to make an NPC class vs monster as using a monster does not make them turn towrad you as I recall. Og II
        ///  Also, once we are reading in the emotes table by weenie - this will automatically customize the behavior for creatures.
        /// </summary>
        public override void ActOnUse(WorldObject worldObject)
        {
            // handled in base.OnActivate -> EmoteManager.OnUse()
        }

        public override void OnCollideObject(WorldObject target)
        {
            if (target.ReportCollisions == false)
                return;

            if (target is Door door)
                door.OnCollideObject(this);
            else if (target is Hotspot hotspot)
                hotspot.OnCollideObject(this);
            else if (target is Creature creature && creature.IsMonster)
                TryRequestPassage(creature);
        }

        /// <summary>
        /// Called when a player selects a target
        /// </summary>
        public bool OnTargetSelected(Player player)
        {
            return selectedTargets.TryAdd(player.Guid.Full, new WorldObjectInfo(player));
        }

        /// <summary>
        /// Called when a player deselects a target
        /// </summary>
        public bool OnTargetDeselected(Player player)
        {
            return selectedTargets.Remove(player.Guid.Full);
        }

        /// <summary>
        /// Called when a creature's health changes
        /// </summary>
        public void OnHealthUpdate()
        {
            foreach (var kvp in selectedTargets)
            {
                var player = kvp.Value.TryGetWorldObject() as Player;

                if (player?.Session != null)
                    QueryHealth(player.Session);
                else
                    selectedTargets.Remove(kvp.Key);
            }
        }

        public double CalculateExtendedTier()
        {
            return CalculateExtendedTier(Level ?? 1);
        }

        public static double CalculateExtendedTier(int level)
        {
            if (level < 10) // Tier 1.0
                return 1.0f;
            else if (level < 30) // Tier 1.0 to 2.0
                return 1f + (float)Math.Pow((level - 10f) / 20f, 2);
            else if (level < 60) // Tier 2.0 to 3.0
                return 2f + (float)Math.Pow((level - 30f) / 30f, 2);
            else if (level < 100) // Tier 3.0 to 4.0
                return 3f + (float)Math.Pow((level - 60) / 40f, 2);
            else if (level < 150) // Tier 4.0 to 5.0
                return 4f + (float)Math.Pow((level - 100f) / 50f, 2);
            else if (level < 200) // Tier 5.0 to 6.0
                return 5f + (float)Math.Pow((level - 150) / 50f, 2);
            else // Tier 6.0
                return 6f;
        }
    }
}
