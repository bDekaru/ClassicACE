using System;
using System.Collections.Generic;
using System.Numerics;

using log4net;

using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Sequence;
using ACE.Server.Network.Structure;
using ACE.Server.Physics;
using ACE.Server.Physics.Animation;
using ACE.Server.Physics.Common;
using ACE.Server.WorldObjects.Managers;

using Character = ACE.Database.Models.Shard.Character;
using MotionTable = ACE.DatLoader.FileTypes.MotionTable;
using System.Linq;
using ACE.Server.Command.Handlers;

namespace ACE.Server.WorldObjects
{
    public partial class Player : Creature, IPlayer
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Account Account { get; }

        public Character Character { get; }

        public Session Session { get; }

        public ContractManager ContractManager;

        public bool LastContact = true;

        protected CampManager _campManager;

        public CampManager CampManager
        {
            get
            {
                if (_campManager == null)
                    _campManager = new CampManager(this);
                return _campManager;
            }
        }

        public bool IsJumping
        {
            get
            {
                if (FastTick)
                    return !PhysicsObj.TransientState.HasFlag(TransientStateFlags.OnWalkable);
                else
                {
                    // for npks only, fixes a bug where OnWalkable can briefly lose state for 1 AutoPos frame
                    // a good repro for this is collision w/ monsters near the top of ramps
                    return !PhysicsObj.TransientState.HasFlag(TransientStateFlags.OnWalkable) && Velocity != Vector3.Zero;
                }
            }
        }

        public DateTime LastJumpTime;

        public ACE.Entity.Position LastGroundPos;
        public ACE.Entity.Position SnapPos;
        public float PrevMovementUpdateMaxSpeed;
        public bool HasPerformedActionsSinceLastMovementUpdate; // Ideally we wouldn't need this, but necessary until we figure out how to detect certain actions while a player isn't running with FastTicks enabled.
        public double LastPlayerAutoposTime;
        public double LastPlayerMovementCheckTime;
        public int MovementEnforcementCounter;

        public float LatestMovementHeading = 0;

        public double NextTechniqueActivationTime = 0;
        public double NextTechniqueNegativeActivationTime = 0;

        public static double TechniqueActivationInterval = 2.5;
        public static double TechniqueNegativeActivationInterval = 10;

        public double NextDualWieldRiposteActivationTime = 0;
        public static double DualWieldRiposteActivationInterval = 2.5;

        public double NextRessOfferTime = 0;
        public static double RessOfferInterval = 60;

        private bool? CachedAttemptToTaunt = null;
        public bool IsAttemptingToTaunt
        {
            get
            {
                if (!CachedAttemptToTaunt.HasValue)
                    CachedAttemptToTaunt = GetCharacterOption(CharacterOption.AttemptToTaunt);

                return CachedAttemptToTaunt ?? false;
            }
        }

        public ConfirmationManager ConfirmationManager;

        public SquelchManager SquelchManager;

        public const float MaxRadarRange_Indoors = 25.0f;
        public const float MaxRadarRange_Outdoors = 75.0f;

        public DateTime PrevObjSend;
        public DateTime PrevWho;
        public DateTime PrevLeaderboardHCXPCommandRequestTimestamp;
        public DateTime PrevLeaderboardHCPvPCommandRequestTimestamp;
        public DateTime PrevLeaderboardSSFCommandRequestTimestamp;
        public DateTime PrevLeaderboardXPCommandRequestTimestamp;
        public DateTime PrevLeaderboardPvPCommandRequestTimestamp;

        public float CurrentRadarRange => Location.Indoors ? MaxRadarRange_Indoors : MaxRadarRange_Outdoors;

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public Player(Weenie weenie, ObjectGuid guid, uint accountId) : base(weenie, guid)
        {
            Character = new Character();
            Character.Id = guid.Full;
            Character.AccountId = accountId;
            Character.Name = GetProperty(PropertyString.Name);
            CharacterChangesDetected = true;

            Account = DatabaseManager.Authentication.GetAccountById(Character.AccountId);

            SetEphemeralValues();

            // Make sure properties this WorldObject requires are not null.
            AvailableExperience = AvailableExperience ?? 0;
            TotalExperience = TotalExperience ?? 0;

            Attackable = true;

            SetProperty(PropertyString.DateOfBirth, $"{DateTime.UtcNow:dd MMMM yyyy}");

            if (IsOlthoiPlayer)
            {
                GenerateContainList();
            }
            else
                Biota.PropertiesCreateList?.Clear();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public Player(Biota biota, IEnumerable<ACE.Database.Models.Shard.Biota> inventory, IEnumerable<ACE.Database.Models.Shard.Biota> wieldedItems, Character character, Session session) : base(biota)
        {
            Character = character;
            Session = session;

            Account = DatabaseManager.Authentication.GetAccountById(Character.AccountId);

            SetEphemeralValues();

            SortBiotasIntoInventory(inventory);
            AddBiotasToEquippedObjects(wieldedItems);

            UpdateCoinValue(false);
            UpdateTradeNoteValue(false);
        }

        public override void InitPhysicsObj()
        {
            base.InitPhysicsObj();

            // set pink bubble state
            IgnoreCollisions = true; ReportCollisions = false; Hidden = true;
        }

        private void SetEphemeralValues()
        {
            ObjectDescriptionFlags |= ObjectDescriptionFlag.Player;

            // This is the default send upon log in and the most common. Anything with a velocity will need to add that flag.
            // This should be handled automatically...
            //PositionFlags |= PositionFlags.OrientationHasNoX | PositionFlags.OrientationHasNoY | PositionFlags.IsGrounded | PositionFlags.HasPlacementID;

            FirstEnterWorldDone = false;

            SetStance(MotionStance.NonCombat, false);

            // radius for object updates
            ListeningRadius = 5f;

            if (Session != null && ConfigManager.Config.Server.Accounts.OverrideCharacterPermissions)
            {
                if (Session.AccessLevel == AccessLevel.Admin)
                    IsAdmin = true;
                if (Session.AccessLevel == AccessLevel.Developer)
                    IsArch = true;
                if (Session.AccessLevel == AccessLevel.Sentinel)
                    IsSentinel = true;
                if (Session.AccessLevel == AccessLevel.Envoy)
                {
                    IsEnvoy = true;
                    IsSentinel = true; //IsEnvoy is not recognized by the client and therefore the client should treat the user as a Sentinel.
                }
                if (Session.AccessLevel == AccessLevel.Advocate)
                    IsAdvocate = true;
            }

            IsOlthoiPlayer = HeritageGroup == HeritageGroup.Olthoi || HeritageGroup == HeritageGroup.OlthoiAcid;

            IsGearKnightPlayer = PropertyManager.GetBool("gearknight_core_plating").Item && HeritageGroup == HeritageGroup.Gearknight;

            ContainerCapacity = (byte)(7 + AugmentationExtraPackSlot);

            if (Session != null && AdvocateQuest && IsAdvocate) // Advocate permissions are per character regardless of override
            {
                if (Session.AccessLevel == AccessLevel.Player)
                    Session.SetAccessLevel(AccessLevel.Advocate); // Elevate to Advocate permissions
                if (AdvocateLevel > 4)
                    IsPsr = true; // Enable AdvocateTeleport via MapClick
            }

            CombatTable = DatManager.PortalDat.ReadFromDat<CombatManeuverTable>(CombatTableDID.Value);

            _questManager = new QuestManager(this);

            ContractManager = new ContractManager(this);

            ConfirmationManager = new ConfirmationManager(this);

            LootPermission = new Dictionary<ObjectGuid, DateTime>();

            SquelchManager = new SquelchManager(this);

            MagicState = new MagicState(this);

            FoodState = new FoodState(this);

            RecordCast = new RecordCast(this);

            AttackQueue = new AttackQueue(this);

            if (!PlayerKillsPk.HasValue)
                PlayerKillsPk = 0;
            if (!PlayerKillsPkl.HasValue)
                PlayerKillsPkl = 0;

            AttackType = AttackType.Punch;

            return; // todo

            // =======================================
            // This code was taken from the old Load()
            // =======================================
            /*AceCharacter character;

            if (Common.ConfigManager.Config.Server.Accounts.OverrideCharacterPermissions)
            {
                if (Session.AccessLevel == AccessLevel.Admin)
                    character.IsAdmin = true;
                if (Session.AccessLevel == AccessLevel.Developer)
                    character.IsArch = true;
                if (Session.AccessLevel == AccessLevel.Envoy)
                    character.IsEnvoy = true;
                // TODO: Need to setup and account properly for IsSentinel and IsAdvocate.
                // if (Session.AccessLevel == AccessLevel.Sentinel)
                //    character.IsSentinel = true;
                // if (Session.AccessLevel == AccessLevel.Advocate)
                //    character.IsAdvocate= true;
            }*/

            // FirstEnterWorldDone = false;

            // IsAlive = true;
        }

        public bool IsDeleted => Character.IsDeleted;
        public bool IsPendingDeletion => Character.DeleteTime > 0 && !IsDeleted;


        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************
        // ******************************************************************* OLD CODE BELOW ********************************

        public MotionStance stance = MotionStance.NonCombat;

        /// <summary>
        /// Called when player presses the 'e' key to appraise an object
        /// </summary>
        public void HandleActionIdentifyObject(uint objectGuid)
        {
            //Console.WriteLine($"{Name}.HandleActionIdentifyObject({objectGuid:X8})");

            if (objectGuid == 0)
            {
                // Deselect the formerly selected Target
                //selectedTarget = ObjectGuid.Invalid;
                RequestedAppraisalTarget = null;
                CurrentAppraisalTarget = null;
                return;
            }

            var wo = FindObject(objectGuid, SearchLocations.Everywhere, out _, out _, out _);

            if (wo == null)
            {
                //log.DebugFormat("{0}.HandleActionIdentifyObject({1:X8}): couldn't find object", Name, objectGuid);
                Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(Session, objectGuid));
                return;
            }

            var currentTime = Time.GetUnixTime();

            // compare with previously requested appraisal target
            if (objectGuid == RequestedAppraisalTarget)
            {
                if (objectGuid == CurrentAppraisalTarget)
                {
                    // continued success, rng roll no longer needed
                    Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(Session, wo, true));
                    OnAppraisal(wo, true);
                    return;
                }

                if (currentTime < AppraisalRequestedTimestamp + 5.0f)
                {
                    // rate limit for unsuccessful appraisal spam
                    Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(Session, wo, false));
                    OnAppraisal(wo, false);
                    return;
                }
            }

            RequestedAppraisalTarget = objectGuid;
            AppraisalRequestedTimestamp = currentTime;

            Examine(wo);
        }

        public void Examine(WorldObject obj)
        {
            //Console.WriteLine($"{Name}.Examine({obj.Name})");

            var success = true;
            var creature = obj as Creature;
            Player player = null;

            if (creature != null)
            {
                player = obj as Player;

                Skill skill;
                if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    skill = Skill.AssessCreature;
                else
                    skill = player != null ? Skill.AssessPerson : Skill.AssessCreature;

                var currentSkill = (int)GetCreatureSkill(skill).Current;
                int difficulty = (int)creature.GetCreatureSkill(Skill.Deception).Current;

                if (PropertyManager.GetBool("assess_creature_mod").Item && skill == Skill.AssessCreature
                        && Skills[Skill.AssessCreature].AdvancementClass < SkillAdvancementClass.Trained)
                    currentSkill = (int)((Focus.Current + Self.Current) / 2);

                var chance = SkillCheck.GetSkillChance(currentSkill, difficulty);

                if (difficulty == 0 || player == this || player != null && !player.GetCharacterOption(CharacterOption.AttemptToDeceiveOtherPlayers))
                    chance = 1.0f;

                if ((this is Admin || this is Sentinel) && CloakStatus == CloakStatus.On)
                    chance = 1.0f;

                success = chance > ThreadSafeRandom.Next(0.0f, 1.0f);
            }

            if (obj.ResistItemAppraisal >= 999)
                success = false;

            if (creature is Pet || creature is CombatPet)
                success = true;

            if (success)
                CurrentAppraisalTarget = obj.Guid.Full;

            Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(Session, obj, success));

            OnAppraisal(obj, success);
        }

        public void OnAppraisal(WorldObject obj, bool success)
        {
            if (!success && obj is Player player && !player.SquelchManager.Squelches.Contains(this, ChatMessageType.Appraisal))
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name} tried and failed to assess you!", ChatMessageType.Appraisal));

            // pooky logic - handle monsters attacking on appraisal
            if (obj is Creature creature && creature.MonsterState == State.Idle)
            {
                if (creature.Tolerance.HasFlag(Tolerance.Appraise))
                {
                    creature.AttackTarget = this;
                    creature.WakeUp();
                }
            }
        }

        public override void OnCollideEnvironment()
        {
            //HandleFallingDamage();
        }

        public override void OnCollideObject(WorldObject target)
        {
            //Console.WriteLine($"{Name}.OnCollideObject({target.Name})");

            if (target.ReportCollisions == false)
                return;

            if (target is Portal portal)
                portal.OnCollideObject(this);
            else if (target is PressurePlate pressurePlate)
                pressurePlate.OnCollideObject(this);
            else if (target is Hotspot hotspot)
                hotspot.OnCollideObject(this);
            else if (target is SpellProjectile spellProjectile)
                spellProjectile.OnCollideObject(this);
            else if (target.ProjectileTarget != null)
                ProjectileCollisionHelper.OnCollideObject(target, this);
        }

        public override void OnCollideObjectEnd(WorldObject target)
        {
            if (target is Hotspot hotspot)
                hotspot.OnCollideObjectEnd(this);
        }

        public void HandleActionQueryHealth(uint objectGuid)
        {
            if (objectGuid == 0)
            {
                // Deselect the formerly selected Target
                UpdateSelectedTarget(null);
                return;
            }

            var obj = CurrentLandblock?.GetObject(objectGuid) as Creature;

            if (obj == null)
            {
                // Deselect the formerly selected Target
                UpdateSelectedTarget(null);
                return;
            }

            UpdateSelectedTarget(obj);

            obj.QueryHealth(Session);
        }

        private void UpdateSelectedTarget(Creature target)
        {
            if (selectedTarget != null)
            {
                var prevSelected = selectedTarget.TryGetWorldObject() as Creature;

                if (prevSelected != null)
                    prevSelected.OnTargetDeselected(this);
            }

            if (target != null)
            {
                selectedTarget = new WorldObjectInfo(target);
                HealthQueryTarget = target.Guid.Full;

                target.OnTargetSelected(this);
            }
            else
            {
                selectedTarget = null;
                HealthQueryTarget = null;
            }
        }

        public void HandleActionQueryItemMana(uint itemGuid)
        {
            if (itemGuid == 0)
            {
                ManaQueryTarget = null;
                return;
            }

            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                var item = FindObject(itemGuid, SearchLocations.MyInventory | SearchLocations.MyEquippedItems | SearchLocations.Landblock, out _, out _, out _);

                if (item != null)
                    item.QueryItemMana(Session);
            }
            else
            {
                // the object could be in the world or on the player, first check player
                var item = GetInventoryItem(itemGuid) ?? GetEquippedItem(itemGuid);

                if (item != null)
                    item.QueryItemMana(Session);
            }

            ManaQueryTarget = itemGuid;
        }


        /// <summary>
        /// Sends a death message broadcast all players on the landblock? that a killer has a victim
        /// </summary>
        /// <remarks>
        /// TODO:
        ///     1. Figure out who all receieves death messages, on what landblock and at what order -
        ///         Example: Does the players around the vicitm receive the message or do the players at the lifestone receieve the message, or both?
        /// </remarks>
        /// <param name="deathMessage"></param>
        /// <param name="victimId"></param>
        public void ActionBroadcastKill(string deathMessage, ObjectGuid victimId, ObjectGuid killerId)
        {
            var deathBroadcast = new GameMessagePlayerKilled(deathMessage, victimId, killerId);

            // OutdoorChatRange?
            EnqueueBroadcast(deathBroadcast);
        }

        /// <summary>
        /// Emits a sound at location sourceId and volume
        /// The client will perform sound attenuation / volume adjustment
        /// based on the listener distance from the origin of sourceId
        /// </summary>
        public void PlaySound(Sound sound, ObjectGuid sourceId, float volume = 1.0f)
        {
            Session.Network.EnqueueSend(new GameMessageSound(sourceId, sound, volume));
        }

        /// <summary>
        /// Returns TRUE if a Player Killer has clicked logout after being involved in a PK battle
        /// within the past 2 mins.
        /// The server delays the logout for 20s, and the client remains in frozen state during this delay
        /// </summary>
        public bool PKLogout;

        public bool IsLoggingOut;

        /// <summary>
        /// Do the player log out work.<para />
        /// If you want to force a player to logout, use Session.LogOffPlayer().
        /// </summary>
        public bool LogOut(bool clientSessionTerminatedAbruptly = false, bool forceImmediate = false)
        {
            if(OfflineInstances != null)
            {
                // Discard and rollback any unsaved changes in this player's offline instances.
                var landblockId = new LandblockId((uint)(OfflineInstancesLandblockId << 16 | 0xFFFF));
                if (LandblockManager.IsLoaded(landblockId))
                    DeveloperCommands.ReloadLandblock(LandblockManager.GetLandblock(landblockId, false));

                OfflineInstancesLandblockId = 0;
                OfflineInstances = null;
            }

            var isHardcoreLogout = false;
            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM && (IsPK || IsPKL))
            {
                if (!LandblockManager.apartmentLandblocks.Contains((uint)Location.LandblockId.Landblock << 16 ^ 0x0000FFFF) && !NoDamage_Landblocks.Contains(Location.LandblockId.Landblock))
                {
                    var currentTime = Time.GetUnixTime();
                    var timeSinceLastPortal = currentTime - (LastPortalTeleportTimestamp ?? 0);
                    if (Teleporting || timeSinceLastPortal < 10)
                        isHardcoreLogout = true;
                    else
                    {
                        List<Player> visiblePlayers;
                        if (GameplayMode == GameplayModes.HardcorePK)
                            visiblePlayers = PhysicsObj.ObjMaint.GetVisiblePlayersValuesAsPlayer().Where(e => e.Guid != Guid && e.GameplayMode == GameplayModes.HardcorePK && Math.Abs((Level ?? 1) - (e.Level ?? 1)) <= 10).ToList();
                        else
                            visiblePlayers = PhysicsObj.ObjMaint.GetVisiblePlayersValuesAsPlayer().Where(e => e.Guid != Guid && e.GameplayMode == GameplayModes.Regular && e.IsPK && Math.Abs((Level ?? 1) - (e.Level ?? 1)) <= 10).ToList();

                        if (visiblePlayers.Count() > 0)
                        {
                            foreach (var entry in visiblePlayers)
                            {
                                if (Fellowship == null || entry.Fellowship != Fellowship)
                                {
                                    if (Allegiance == null || entry.Allegiance != Allegiance)
                                    {
                                        isHardcoreLogout = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if ((PKLogoutActive || isHardcoreLogout) && !forceImmediate)
            {
                var timer = PropertyManager.GetLong("pk_timer").Item;
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You will logout in {timer} seconds...", ChatMessageType.Broadcast));

                if (!PKLogout)
                {
                    PKLogout = true;

                    if (Teleporting && ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    {
                        OnTeleportComplete();

                        var actionChain = new ActionChain();
                        actionChain.AddDelaySeconds(2);
                        actionChain.AddAction(this, () =>
                        {
                            IsFrozen = true;
                            EnqueueBroadcastPhysicsState();
                        });
                        actionChain.EnqueueChain();
                    }
                    else
                    {
                        IsFrozen = true;
                        EnqueueBroadcastPhysicsState();
                    }

                    LogoffTimestamp = Time.GetFutureUnixTime(timer);
                    PlayerManager.AddPlayerToLogoffQueue(this);
                }
                return false;
            }

            LogOut_Inner(clientSessionTerminatedAbruptly);

            return true;
        }

        public void LogOut_Inner(bool clientSessionTerminatedAbruptly = false)
        {
            IsBusy = true;
            IsLoggingOut = true;

            EndSneaking();

            PlayerManager.AddPlayerToFinalLogoffQueue(this);

            if (Fellowship != null)
                FellowshipQuit(false);

            if (IsTrading && TradePartner != ObjectGuid.Invalid)
            {
                var tradePartner = PlayerManager.GetOnlinePlayer(TradePartner);

                if (tradePartner != null)
                    tradePartner.HandleActionCloseTradeNegotiations();
            }

            if (!clientSessionTerminatedAbruptly)
            {
                if (PropertyManager.GetBool("use_turbine_chat").Item)
                {
                    if (IsOlthoiPlayer)
                    {
                        LeaveTurbineChatChannel("Olthoi");
                    }
                    else
                    {
                        if (GetCharacterOption(CharacterOption.ListenToGeneralChat))
                            LeaveTurbineChatChannel("General");
                        if (GetCharacterOption(CharacterOption.ListenToTradeChat))
                            LeaveTurbineChatChannel("Trade");
                        if (GetCharacterOption(CharacterOption.ListenToLFGChat))
                            LeaveTurbineChatChannel("LFG");
                        if (GetCharacterOption(CharacterOption.ListenToRoleplayChat))
                            LeaveTurbineChatChannel("Roleplay");
                        if (GetCharacterOption(CharacterOption.ListenToAllegianceChat) && Allegiance != null)
                            LeaveTurbineChatChannel("Allegiance");
                        if (GetCharacterOption(CharacterOption.ListenToSocietyChat) && Society != FactionBits.None)
                            LeaveTurbineChatChannel("Society");
                    }
                }
            }

            if (CurrentActivePet != null)
                CurrentActivePet.Destroy();

            // If we're in the dying animation process, we cannot logout until that animation completes..
            if (IsInDeathProcess)
                return;

            LogOut_Final();
        }

        private void LogOut_Final(bool skipAnimations = false)
        {
            if (CurrentLandblock != null)
            {
                if (skipAnimations)
                {
                    FinalizeLogout();
                }
                else
                {
                    if (IsFrozen ?? false)
                        IsFrozen = false;

                    EnqueueBroadcastPhysicsState();

                    var motionCommand = MotionCommand.LogOut;
                    var motion = new Motion(this, motionCommand);
                    var stanceNonCombat = MotionStance.NonCombat;
                    var animLength = Physics.Animation.MotionTable.GetAnimationLength(MotionTableId, stanceNonCombat, motionCommand);

                    var logoutChain = new ActionChain();

                    logoutChain.AddAction(this, () => SendMotionAsCommands(motionCommand, stanceNonCombat));
                    logoutChain.AddDelaySeconds(animLength);

                    // remove the player from landblock management -- after the animation has run
                    logoutChain.AddAction(WorldManager.ActionQueue, () =>
                    {
                        // If we're in the dying animation process, we cannot RemoveWorldObject and logout until that animation completes..
                        if (IsInDeathProcess)
                            return;

                        FinalizeLogout();
                    });

                    // close any open landblock containers (chests / corpses)
                    if (LastOpenedContainerId != ObjectGuid.Invalid)
                    {
                        var container = CurrentLandblock.GetObject(LastOpenedContainerId) as Container;

                        if (container != null)
                            container.Close(this);
                    }

                    logoutChain.EnqueueChain();
                }
            }
            else
            {
                FinalizeLogout();
            }
        }

        public double LogOffFinalizedTime;

        public bool ForcedLogOffRequested;

        /// <summary>
        /// Force Log off a player requested to log out by an admin command forcelogoff/forcelogout or the ServerManager.<para />
        /// THIS FUNCTION FOR SYSTEM USE ONLY; If you want to force a player to logout, use Session.LogOffPlayer().
        /// </summary>
        public void ForceLogoff()
        {
            if (!ForcedLogOffRequested) return;

            log.WarnFormat("[LOGOUT] Executing ForcedLogoff for Account {0} with character {1} (0x{2}) at {3}.", Account.AccountName, Name, Guid, DateTime.Now.ToCommonString());

            FinalizeLogout();

            ForcedLogOffRequested = false;
        }

        private void FinalizeLogout()
        {
            PlayerManager.RemovePlayerFromFinalLogoffQueue(this);
            CurrentLandblock?.RemoveWorldObject(Guid, false);
            SetPropertiesAtLogOut();
            SavePlayerToDatabase();
            PlayerManager.SwitchPlayerFromOnlineToOffline(this);

            log.DebugFormat("[LOGOUT] Account {0} exited the world with character {1} (0x{2}) at {3}.", Account.AccountName, Name, Guid, DateTime.Now.ToCommonString());
        }

        public void SaveDotsAndHots()
        {
            lock (DoTHoTListLock)
            {
                if (ActiveDamageOverTimeList != null && ActiveDamageOverTimeList.Count > 0)
                {
                    var dotStrings = new List<string>();
                    foreach (var entry in ActiveDamageOverTimeList)
                    {
                        dotStrings.Add($"{entry.TickAmount}:{entry.TotalAmount}:{(int)entry.CombatType}:{(int)entry.DamageType}");
                    }

                    DamageOverTimeLog = string.Join(",", dotStrings);
                }
                else
                    DamageOverTimeLog = null;

                if (ActiveHealOverTimeList != null && ActiveHealOverTimeList.Count > 0)
                {
                    var hotStrings = new List<string>();
                    foreach (var entry in ActiveHealOverTimeList)
                    {
                        hotStrings.Add($"{entry.TickAmount}:{entry.TotalAmount}:{(int)entry.VitalType}");
                    }
                    HealOverTimeLog = string.Join(",", hotStrings);
                }
                else
                    HealOverTimeLog = null;
            }
        }

        public void RestoreDotsAndHots()
        {
            lock (DoTHoTListLock)
            {
                if (DamageOverTimeLog != null)
                {
                    try
                    {
                        var dotStrings = DamageOverTimeLog.Split(",");
                        foreach (var entry in dotStrings)
                        {
                            var entryStrings = entry.Split(":");
                            if (!int.TryParse(entryStrings[0], out var tickAmount))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore DoT TickAmount from string: {entryStrings}");
                                continue;
                            }
                            if (!int.TryParse(entryStrings[1], out var totalAmount))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore DoT TotalAmount from string: {entryStrings}");
                                continue;
                            }
                            if (!Enum.TryParse(entryStrings[2], out CombatType combatType))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore DoT CombatType from string: {entryStrings}");
                                continue;
                            }
                            if (!Enum.TryParse(entryStrings[3], out DamageType damageType))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore DoT DamageType from string: {entryStrings}");
                                continue;
                            }

                            ActiveDamageOverTimeList.Add(new DoTInfo(tickAmount, totalAmount, combatType, damageType, null));
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"RestoreDotsAndHots() Failed to restore DoTs from string: {DamageOverTimeLog} - {ex}");
                    }

                    DamageOverTimeLog = null;
                }

                if (HealOverTimeLog != null)
                {
                    try
                    {
                        var hotStrings = HealOverTimeLog.Split(",");
                        foreach (var entry in hotStrings)
                        {
                            var entryStrings = entry.Split(":");
                            if (!int.TryParse(entryStrings[0], out var tickAmount))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore HoT TickAmount from string: {entryStrings}");
                                continue;
                            }
                            if (!int.TryParse(entryStrings[1], out var totalAmount))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore HoT TotalAmount from string: {entryStrings}");
                                continue;
                            }
                            if (!Enum.TryParse(entryStrings[2], out DamageType damageType))
                            {
                                log.Warn($"RestoreDotsAndHots() failed to restore DoT DamageType from string: {entryStrings}");
                                continue;
                            }

                            ActiveHealOverTimeList.Add(new HoTInfo(tickAmount, totalAmount, damageType, null));
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"RestoreDotsAndHots() Failed to restore HoTs from string: {HealOverTimeLog} - {ex}");
                    }

                    HealOverTimeLog = null;
                }
            }
        }

        public void HandleMRT()
        {
            // This requires the Admin flag set on ObjectDescriptionFlags
            // I would expect this flag to be set in Admin.cs which would be a subclass of Player
            // FIXME: maybe move to Admin class?
            // TODO: reevaluate class location

            // The EnqueueBroadcastUpdateObject below sends the player back into teleport. I assume at this point, this was never done to players
            // EnqueueBroadcastUpdateObject();

            // The private message below worked as expected, but it only broadcast to the player. This would be a problem with for others in range seeing something try to
            // pass through a barrier but not being allowed.
            // var updateBool = new GameMessagePrivateUpdatePropertyBool(Session, PropertyBool.IgnoreHouseBarriers, ImmuneCellRestrictions);
            // Session.Network.EnqueueSend(updateBool);

            UpdateProperty(this, PropertyBool.IgnoreHouseBarriers, !IgnoreHouseBarriers, true);

            Session.Network.EnqueueSend(new GameMessageSystemChat($"Bypass Housing Barriers now set to: {IgnoreHouseBarriers}", ChatMessageType.Broadcast));
        }

        public void SendAutonomousPosition()
        {
            // Session.Network.EnqueueSend(new GameMessageAutonomousPosition(this));
        }



        

        /// <summary>
        ///  Sends object description if the client requests it
        /// </summary>
        public void HandleActionForceObjDescSend(uint itemGuid)
        {
            var wo = FindObject(itemGuid, SearchLocations.Everywhere);
            if (wo == null)
            {
                //log.DebugFormat("HandleActionForceObjDescSend() - couldn't find object {0:X8}", itemGuid);
                return;
            }
            Session.Network.EnqueueSend(new GameMessageObjDescEvent(wo));
        }

        public void HandleActionApplySoundEffect(Sound sound)
        {
            PlaySound(sound, Guid);
        }

        //public void TestWieldItem(Session session, uint modelId, int palOption, float shade = 0)
        //{
        //    // ClothingTable item = ClothingTable.ReadFromDat(0x1000002C); // Olthoi Helm
        //    // ClothingTable item = ClothingTable.ReadFromDat(0x10000867); // Cloak
        //    // ClothingTable item = ClothingTable.ReadFromDat(0x10000008); // Gloves
        //    // ClothingTable item = ClothingTable.ReadFromDat(0x100000AD); // Heaume
        //    var item = DatManager.PortalDat.ReadFromDat<ClothingTable>(modelId);

        //    int palCount = 0;

        //    List<uint> coverage = new List<uint>(); // we'll store our fake coverage items here
        //    ClearObjDesc();
        //    AddCharacterBaseModelData(); // Add back in the facial features, hair and skin palette

        //    if (item.ClothingBaseEffects.ContainsKey((uint)SetupTableId))
        //    {
        //        // Add the model and texture(s)
        //        ClothingBaseEffect clothingBaseEffec = item.ClothingBaseEffects[(uint)SetupTableId];
        //        for (int i = 0; i < clothingBaseEffec.CloObjectEffects.Count; i++)
        //        {
        //            byte partNum = (byte)clothingBaseEffec.CloObjectEffects[i].Index;
        //            AddModel((byte)clothingBaseEffec.CloObjectEffects[i].Index, (ushort)clothingBaseEffec.CloObjectEffects[i].ModelId);
        //            coverage.Add(partNum);
        //            for (int j = 0; j < clothingBaseEffec.CloObjectEffects[i].CloTextureEffects.Count; j++)
        //                AddTexture((byte)clothingBaseEffec.CloObjectEffects[i].Index, (ushort)clothingBaseEffec.CloObjectEffects[i].CloTextureEffects[j].OldTexture, (ushort)clothingBaseEffec.CloObjectEffects[i].CloTextureEffects[j].NewTexture);
        //        }

        //        // Apply an appropriate palette. We'll just pick a random one if not specificed--it's a surprise every time!
        //        // For actual equipment, these should just be stored in the ace_object palette_change table and loaded from there
        //        if (item.ClothingSubPalEffects.Count > 0)
        //        {
        //            int size = item.ClothingSubPalEffects.Count;
        //            palCount = size;

        //            CloSubPalEffect itemSubPal;
        //            // Generate a random index if one isn't provided
        //            if (item.ClothingSubPalEffects.ContainsKey((uint)palOption))
        //            {
        //                itemSubPal = item.ClothingSubPalEffects[(uint)palOption];
        //            }
        //            else
        //            {
        //                List<CloSubPalEffect> values = item.ClothingSubPalEffects.Values.ToList();
        //                Random rand = new Random();
        //                palOption = rand.Next(size);
        //                itemSubPal = values[palOption];
        //            }

        //            for (int i = 0; i < itemSubPal.CloSubPalettes.Count; i++)
        //            {
        //                var itemPalSet = DatManager.PortalDat.ReadFromDat<PaletteSet>(itemSubPal.CloSubPalettes[i].PaletteSet);
        //                ushort itemPal = (ushort)itemPalSet.GetPaletteID(shade);

        //                for (int j = 0; j < itemSubPal.CloSubPalettes[i].Ranges.Count; j++)
        //                {
        //                    uint palOffset = itemSubPal.CloSubPalettes[i].Ranges[j].Offset / 8;
        //                    uint numColors = itemSubPal.CloSubPalettes[i].Ranges[j].NumColors / 8;
        //                    AddPalette(itemPal, (ushort)palOffset, (ushort)numColors);
        //                }
        //            }
        //        }

        //        // Add the "naked" body parts. These are the ones not already covered.
        //        var baseSetup = DatManager.PortalDat.ReadFromDat<SetupModel>((uint)SetupTableId);
        //        for (byte i = 0; i < baseSetup.Parts.Count; i++)
        //        {
        //            if (!coverage.Contains(i) && i != 0x10) // Don't add body parts for those that are already covered. Also don't add the head.
        //                AddModel(i, baseSetup.Parts[i]);
        //        }

        //        var objDescEvent = new GameMessageObjDescEvent(this);
        //        session.Network.EnqueueSend(objDescEvent);
        //        ChatPacket.SendServerMessage(session, "Equipping model " + modelId.ToString("X") +
        //                                              ", Applying palette index " + palOption + " of " + palCount +
        //                                              " with a shade value of " + shade + ".", ChatMessageType.Broadcast);
        //    }
        //    else
        //    {
        //        // Alert about the failure
        //        ChatPacket.SendServerMessage(session, "Could not match that item to your character model.", ChatMessageType.Broadcast);
        //    }
        //}

        public void HandleActionTalk(string message)
        {
            if (!IsGagged)
            {
                EnqueueBroadcast(new GameMessageHearSpeech(message, GetNameWithSuffix(), Guid.Full, ChatMessageType.Speech), LocalBroadcastRange, ChatMessageType.Speech);

                OnTalk(message);
            }
            else
                SendGagError();
        }

        public void SendGagError()
        {
            var msg = "You are unable to talk locally, globally, or send tells because you have been gagged.";
            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, msg), new GameMessageSystemChat(msg,ChatMessageType.WorldBroadcast));
        }

        public void SendGagNotice()
        {
            var msg = "Your chat privileges have been suspended.";
            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, msg), new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
        }

        public void SendUngagNotice()
        {
            var msg = "Your chat privileges have been restored.";
            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, msg), new GameMessageSystemChat(msg, ChatMessageType.WorldBroadcast));
        }

        public void HandleActionEmote(string message)
        {
            if (!IsGagged)
            {
                EnqueueBroadcast(new GameMessageEmoteText(Guid.Full, GetNameWithSuffix(), message), LocalBroadcastRange);

                OnTalk(message);
            }
            else
                SendGagError();
        }

        public void HandleActionSoulEmote(string message)
        {
            if (!IsGagged)
            {
                if (!IsOlthoiPlayer || (IsOlthoiPlayer && NoOlthoiTalk))
                    EnqueueBroadcast(new GameMessageSoulEmote(Guid.Full, Name, message), LocalBroadcastRange);

                OnTalk(message);
            }
            else
                SendGagError();
        }

        public void OnTalk(string message)
        {
            if (PhysicsObj == null || CurrentLandblock == null) return;

            var isDungeon = CurrentLandblock.PhysicsLandblock != null && CurrentLandblock.PhysicsLandblock.IsDungeon;

            var rangeSquared = LocalBroadcastRangeSq;

            foreach (var creature in PhysicsObj.ObjMaint.GetKnownObjectsValuesAsCreature())
            {
                if (isDungeon && Location.Landblock != creature.Location.Landblock)
                    continue;

                var distSquared = Location.SquaredDistanceTo(creature.Location);
                if (distSquared <= rangeSquared)
                    creature.EmoteManager.OnHearChat(this, message);
            }
        }

        public void HandleActionJump(JumpPack jump)
        {
            StartJump = new ACE.Entity.Position(Location);
            //Console.WriteLine($"JumpPack: Velocity: {jump.Velocity}, Extent: {jump.Extent}");

            var strength = Strength.Current;
            var capacity = EncumbranceSystem.EncumbranceCapacity((int)strength, AugmentationIncreasedCarryingCapacity);
            var burden = EncumbranceSystem.GetBurden(capacity, EncumbranceVal ?? 0);

            // calculate stamina cost for this jump
            var extent = Math.Clamp(jump.Extent, 0.0f, 1.0f);
            var staminaCost = MovementSystem.JumpStaminaCost(extent, burden, PKTimerActive);

            //Console.WriteLine($"Strength: {strength}, Capacity: {capacity}, Encumbrance: {EncumbranceVal ?? 0}, Burden: {burden}, StaminaCost: {staminaCost}");

            // ensure player has enough stamina to jump

            /*if (staminaCost > Stamina.Current)
            {
                // get adjusted power
                extent = MovementSystem.GetJumpPower(Stamina.Current, burden, false);

                staminaCost = (int)Stamina.Current;

                // adjust jump velocity
                var velocityZ = MovementSystem.GetJumpHeight(burden, GetCreatureSkill(Skill.Jump).Current, extent, 1.0f);

                jump.Velocity.Z = velocityZ;
            }*/

            LastJumpTime = DateTime.UtcNow;
            HasPerformedActionsSinceLastMovementUpdate = true;

            UpdateVitalDelta(Stamina, -staminaCost);

            //Console.WriteLine($"Jump velocity: {jump.Velocity}");

            // TODO: have server verify / scale magnitude
            if (FastTick)
            {
                if (!PhysicsObj.IsMovingOrAnimating)
                    //PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime - Physics.PhysicsGlobals.MinQuantum;
                    PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

                // perform jump in physics engine
                PhysicsObj.TransientState &= ~(TransientStateFlags.Contact | TransientStateFlags.WaterContact);
                PhysicsObj.calc_acceleration();
                PhysicsObj.set_on_walkable(false);
                PhysicsObj.set_local_velocity(jump.Velocity, false);
                PhysicsObj.RemoveLinkAnimations();      // matches MotionInterp.LeaveGround more closely
                PhysicsObj.MovementManager.MotionInterpreter.PendingMotions.Clear();        //hack
                PhysicsObj.IsAnimating = false;

                if (CombatMode == CombatMode.Magic && MagicState.IsCasting)
                {
                    // clear possible CastMotion out of InterpretedMotionState.ForwardCommand
                    PhysicsObj.MovementManager.MotionInterpreter.StopCompletely();

                    FailCast();
                }
            }
            else
            {
                PhysicsObj.UpdateTime = PhysicsTimer.CurrentTime;

                // set jump velocity
                //var glob_velocity = Vector3.Transform(jump.Velocity, Location.Rotation);
                //PhysicsObj.set_velocity(glob_velocity, true);

                // perform jump in physics engine
                PhysicsObj.TransientState &= ~(TransientStateFlags.Contact | TransientStateFlags.WaterContact);
                PhysicsObj.calc_acceleration();
                PhysicsObj.set_on_walkable(false);
                PhysicsObj.set_local_velocity(jump.Velocity, false);
                PhysicsObj.RemoveLinkAnimations();      // matches MotionInterp.LeaveGround more closely
                PhysicsObj.MovementManager.MotionInterpreter.PendingMotions.Clear();        //hack
                PhysicsObj.IsAnimating = false;
            }

            // this shouldn't be needed, but without sending this update motion / simulated movement event beforehand,
            // running forward and then performing a charged jump does an uncharged shallow arc jump instead
            // this hack fixes that...
            var movementData = new MovementData(this);
            movementData.IsAutonomous = true;
            movementData.MovementType = MovementType.Invalid;
            movementData.Invalid = new MovementInvalid(movementData);
            EnqueueBroadcast(new GameMessageUpdateMotion(this, movementData));

            // broadcast jump
            EnqueueBroadcast(new GameMessageVectorUpdate(this));

            if (MagicState.IsCasting && RecordCast.Enabled)
                RecordCast.OnJump(jump);
        }

        /// <summary>
        /// Called when the Player's stamina has recently changed to 0
        /// </summary>
        public void OnExhausted()
        {
            // adjust player speed if they are currently pressing movement keys
            HandleRunRateUpdate();

            Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, "You're Exhausted!"));
        }

        /// <summary>
        /// Detects changes in the player's RunRate --
        /// If there are changes, re-broadcasts player movement packet
        /// </summary>
        public bool HandleRunRateUpdate()
        {
            //Console.WriteLine($"{Name}.HandleRunRateUpdates()");

            if (CurrentMovementData.MovementType != MovementType.Invalid || CurrentMovementData.Invalid == null)
                return false;

            var prevState = CurrentMovementData.Invalid.State;

            var movementData = new MovementData(this, CurrentMoveToState);
            var currentState = movementData.Invalid.State;

            var changed = currentState.ForwardSpeed  != prevState.ForwardSpeed ||
                          currentState.TurnSpeed     != prevState.TurnSpeed ||
                          currentState.SidestepSpeed != prevState.SidestepSpeed;

            if (!changed)
                return false;

            //Console.WriteLine($"Old: {prevState.ForwardSpeed}, New: {currentState.ForwardSpeed}");

            if (!CurrentMovementData.Invalid.State.HasMovement() || IsJumping)
                return false;

            //Console.WriteLine($"{Name}.OnRunRateChanged()");

            CurrentMovementData = new MovementData(this, CurrentMoveToState);

            // verify - forced commands from server should be non-autonomous, but could have been sent as autonomous in retail?
            // if set to autonomous here, the desired effect doesn't happen
            CurrentMovementData.IsAutonomous = false;

            var movementEvent = new GameMessageUpdateMotion(this, CurrentMovementData);
            EnqueueBroadcast(movementEvent);    // broadcast to all players, including self

            return true;
        }

        /// <summary>
        /// Returns a modifier for a player's Run, Jump, Melee Defense, and Missile Defense skills if they are overburdened
        /// </summary>
        public override float GetBurdenMod()
        {
            var strength = Strength.Current;

            var capacity = EncumbranceSystem.EncumbranceCapacity((int)strength, AugmentationIncreasedCarryingCapacity);

            var burden = EncumbranceSystem.GetBurden(capacity, EncumbranceVal ?? 0);

            var burdenMod = EncumbranceSystem.GetBurdenMod(burden);

            //Console.WriteLine($"Burden mod: {burdenMod}");

            return burdenMod;
        }

        public bool Adminvision;

        public void HandleAdminvisionToggle(int choice)
        {
            bool oldState = Adminvision;

            switch (choice)
            {
                case -1:
                    // Do nothing
                    break;
                case 0:
                    Adminvision = false;
                    break;
                case 1:
                    Adminvision = true;
                    break;
                case 2:
                    if (Adminvision)
                        Adminvision = false;
                    else
                        Adminvision = true;
                    break;
            }

            // send CO network messages for admin objects
            if (oldState != Adminvision)
            {
                var adminObjs = PhysicsObj.ObjMaint.GetKnownObjectsValuesWhere(o => o.WeenieObj.WorldObject != null && o.WeenieObj.WorldObject.Visibility);
                var nodrawObjs = PhysicsObj.ObjMaint.GetKnownObjectsValuesWhere(o => o.WeenieObj.WorldObject != null && ((o.WeenieObj.WorldObject.NoDraw ?? false) || o.WeenieObj.WorldObject.UiHidden));

                if (Adminvision)
                {
                    PhysicsObj.enqueue_objs(adminObjs);
                    foreach (var wo in nodrawObjs)
                        Session.Network.EnqueueSend(new GameMessageUpdateObject(wo.WeenieObj.WorldObject, Adminvision, Adminvision ? true : false));
                }
                else
                {
                    foreach (var wo in adminObjs)
                        RemoveTrackedObject(wo.WeenieObj.WorldObject, false);

                    foreach (var wo in nodrawObjs)
                        RemoveTrackedObject(wo.WeenieObj.WorldObject, false);
                }
            }

            string state = Adminvision ? "enabled" : "disabled";
            Session.Network.EnqueueSend(new GameMessageSystemChat($"Admin Vision is {state}.", ChatMessageType.Broadcast));
        }

        public void SendMessage(string msg, ChatMessageType type = ChatMessageType.Broadcast, WorldObject source = null)
        {
            if (SquelchManager.IsLegalChannel(type) && SquelchManager.Squelches.Contains(source, type))
                return;

            Session.Network.EnqueueSend(new GameMessageSystemChat(msg, type));
        }

        public void HandleActionEnterPkLite()
        {
            if (ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"The PK Lite system is not available on this server.", ChatMessageType.Broadcast));
                return;
            }

            // ensure permanent npk
            if (PlayerKillerStatus != PlayerKillerStatus.NPK || MinimumTimeSincePk != null)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.OnlyNonPKsMayEnterPKLite));
                return;
            }

            if (IsBusy || Teleporting || suicideInProgress)
            {
                Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YoureTooBusy));
                return;
            }

            var animTime = 0.0f;

            if (CombatMode != CombatMode.NonCombat)
            {
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.CombatMode, (int)CombatMode.NonCombat));
                animTime += SetCombatMode(CombatMode.NonCombat);
            }

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(animTime);
            actionChain.AddAction(this, () =>
            {
                IsBusy = true;

                EnqueueBroadcast(new GameMessageSystemChat($"{Name} is looking for a fight!", ChatMessageType.Broadcast), LocalBroadcastRange);

                // perform pk lite entry motion / effect
                SendMotionAsCommands(MotionCommand.EnterPKLite, MotionStance.NonCombat);

                var innerChain = new ActionChain();

                // wait for animation to complete
                animTime = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId).GetAnimationLength(MotionCommand.EnterPKLite);
                innerChain.AddDelaySeconds(animTime);
                innerChain.AddAction(this, () =>
                {
                    IsBusy = false;

                    if (PropertyManager.GetBool("allow_pkl_bump").Item)
                    {
                        // check for collisions
                        PlayerKillerStatus = PlayerKillerStatus.PKLite;

                        var colliding = PhysicsObj.ethereal_check_for_collisions();

                        if (colliding)
                        {
                            // try initial placement
                            var result = PhysicsObj.SetPositionSimple(PhysicsObj.Position, true);

                            if (result == SetPositionError.OK)
                            {
                                // handle landblock update?
                                SyncLocation();

                                // force broadcast
                                Sequences.GetNextSequence(SequenceType.ObjectForcePosition);
                                SendUpdatePosition();
                            }
                        }
                    }
                    UpdateProperty(this, PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus.PKLite, true);

                    Session.Network.EnqueueSend(new GameEventWeenieError(Session, WeenieError.YouAreNowPKLite));
                });

                innerChain.EnqueueChain();
            });
            actionChain.EnqueueChain();
        }

        // Content developer helper variables
        public ACE.Entity.Position CopiedPos = null;
        public List<ACE.Database.Models.World.LandblockInstance> OfflineInstances = null;
        public ushort OfflineInstancesLandblockId = 0;
    }
}
