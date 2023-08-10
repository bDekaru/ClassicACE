using System;
using System.Collections.Generic;
using ACE.Common;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class PKModifier : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public PKModifier(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public PKModifier(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        public bool IsPKSwitch  => PkLevelModifier ==  1;
        public bool IsNPKSwitch => PkLevelModifier == -1;

        private void SetEphemeralValues()
        {
            CurrentMotionState = new Motion(MotionStance.NonCombat);

            if (IsNPKSwitch)
                ObjectDescriptionFlags |= ObjectDescriptionFlag.NpkSwitch;

            if (IsPKSwitch)
                ObjectDescriptionFlags |= ObjectDescriptionFlag.PkSwitch;
        }

        public override ActivationResult CheckUseRequirements(WorldObject activator)
        {
            if (!(activator is Player player))
                return new ActivationResult(false);

            if (player.IsOlthoiPlayer)
            {
                player.SendWeenieError(WeenieError.OlthoiCannotInteractWithThat);
                return new ActivationResult(false);
            }

            if (PkLevelModifier >= 10)
                return new ActivationResult(true);
            else if(player.IsHardcore)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Hardcore characters may not interact with that.", ChatMessageType.Broadcast));
                return new ActivationResult(false);
            }

            if (player.PkLevel > PKLevel.PK || PropertyManager.GetBool("pk_server").Item || PropertyManager.GetBool("pkl_server").Item)
            {
                if (!string.IsNullOrWhiteSpace(GetProperty(PropertyString.UsePkServerError)))
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(GetProperty(PropertyString.UsePkServerError), ChatMessageType.Broadcast));

                return new ActivationResult(false);
            }

            if (player.PlayerKillerStatus == PlayerKillerStatus.PKLite)
            {
                if (!string.IsNullOrWhiteSpace(GetProperty(PropertyString.UsePkServerError)))
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(GetProperty(PropertyString.UsePkServerError), ChatMessageType.Broadcast));

                player.Session.Network.EnqueueSend(new GameMessageSystemChat("Player Killer Lites may not change their PK status.", ChatMessageType.Broadcast)); // not sure how retail handled this case

                return new ActivationResult(false);
            }

            if (player.PkLevel == PKLevel.PK && !PropertyManager.GetBool("allow_PKs_to_go_NPK").Item)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat("This server does not allow Player Killers to go back to being Non-Player Killers.", ChatMessageType.Broadcast));

                return new ActivationResult(false);
            }

            if (player.Teleporting)
                return new ActivationResult(false);

            if (player.IsBusy)
                return new ActivationResult(false);

            if (player.IsAdvocate || player.AdvocateQuest || player.AdvocateState)
            {
                return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.AdvocatesCannotChangePKStatus));
            }

            if (player.MinimumTimeSincePk != null)
            {
                return new ActivationResult(new GameEventWeenieError(player.Session, WeenieError.CannotChangePKStatusWhileRecovering));
            }

            if (IsBusy)
            {
                return new ActivationResult(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.The_IsCurrentlyInUse, Name));
            }

            return new ActivationResult(true);
        }

        public void ConvertToGameplayMode(Player player, bool setStarterLocation)
        {
            switch (PkLevelModifier)
            {
                case 10: // Hardcore NPK
                    player.RevertToBrandNewCharacterEquipment(true, true);
                    player.RemoveAllTitles();
                    player.AddTitle((uint)CharacterTitle.GimpyMageofMight, true, true, true); // This title was replaced with the "Hardcore" title.
                    player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                    player.PkLevel = PKLevel.NPK;
                    player.GameplayMode = GameplayModes.HardcoreNPK;

                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PkLevelModifier, player.PkLevelModifier));
                    break;
                case 11: // Hardcore PK
                    player.RevertToBrandNewCharacterEquipment(true, true);
                    player.RemoveAllTitles();
                    player.AddTitle((uint)CharacterTitle.GimpyMageofMight, true, true, true); // This title was replaced with the "Hardcore" title.
                    player.PlayerKillerStatus = PlayerKillerStatus.PKLite;
                    player.PkLevel = PKLevel.NPK;
                    player.GameplayMode = GameplayModes.HardcorePK;

                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PkLevelModifier, player.PkLevelModifier));

                    player.GiveFromEmote(this, (int)Factories.Enum.WeenieClassName.ringHardcore);
                    break;
                case 12: // Solo Self Found
                    player.RevertToBrandNewCharacterEquipment(true, true);
                    player.RemoveAllTitles();
                    player.AddTitle((uint)CharacterTitle.GimpGoddess, true, true, true); // This title was replaced with the "Solo Self-Found" title.
                    player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                    player.PkLevel = PKLevel.NPK;
                    player.GameplayMode = GameplayModes.SoloSelfFound;
                    player.GameplayModeExtraIdentifier = player.Guid.Full;
                    player.GameplayModeIdentifierString = player.Name;

                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PkLevelModifier, player.PkLevelModifier));
                    break;
                case 13: // Regular
                    player.RevertToBrandNewCharacterEquipment(true, true);
                    player.RemoveAllTitles();
                    if (player.ChargenTitleId > 0)
                        player.AddTitle((uint)player.ChargenTitleId, true, true, true);
                    else
                        player.AddTitle((uint)CharacterTitle.Adventurer, true, true, true);
                    player.PlayerKillerStatus = PlayerKillerStatus.NPK;
                    player.PkLevel = PKLevel.NPK;
                    player.GameplayMode = GameplayModes.Regular;
                    player.GameplayModeExtraIdentifier = 0;
                    player.GameplayModeIdentifierString = null;

                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PkLevelModifier, player.PkLevelModifier));
                    break;
                case 100: // Reset to brand new character but keep xp.
                    player.RevertToBrandNewCharacter(true, true, true, true, true, true, player.TotalExperience ?? 0);
                    setStarterLocation = false;
                    break;
                case 101: // Update starting template skills
                    var heritageGroup = DatManager.PortalDat.CharGen.HeritageGroups[(uint)(Heritage ?? 1)];
                    var availableSkillCredits = (int)heritageGroup.SkillCredits;

                    var trainedSkills = new List<int>();
                    var specializedSkills = new List<int>();
                    var secondarySkills = new List<string>();

                    foreach (var skillEntry in player.Skills)
                    {
                        var skillId = (int)skillEntry.Key;
                        var skill = skillEntry.Value;
                        var sac = skill.AdvancementClass;

                        if (sac < SkillAdvancementClass.Trained)
                            continue;

                        var skillBase = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)skillId];

                        var trainedCost = skillBase.TrainedCost;
                        var specializedCost = skillBase.UpgradeCostFromTrainedToSpecialized;

                        foreach (var skillGroup in heritageGroup.Skills)
                        {
                            if (skillGroup.SkillNum == skillId)
                            {
                                trainedCost = skillGroup.NormalCost;
                                specializedCost = skillGroup.PrimaryCost;
                                break;
                            }
                        }

                        if (sac == SkillAdvancementClass.Specialized)
                        {
                            specializedSkills.Add(skillId);
                            availableSkillCredits -= trainedCost;
                            availableSkillCredits -= specializedCost;
                        }
                        else if (sac == SkillAdvancementClass.Trained)
                        {
                            trainedSkills.Add(skillId);
                            availableSkillCredits -= trainedCost;
                        }

                        if(skill.IsSecondary && sac > SkillAdvancementClass.Untrained)
                        {
                            secondarySkills.Add($"{skillId}:{(int)skill.SecondaryTo}");
                        }
                    }

                    if (availableSkillCredits >= 0)
                    {
                        player.ChargenSkillsTrained = string.Join("|", trainedSkills);
                        player.ChargenSkillsSpecialized = string.Join("|", specializedSkills);
                        player.ChargenSkillsSecondary = string.Join("|", secondarySkills);
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your starting template has been updated!", ChatMessageType.Broadcast));
                        return;
                    }
                    else
                    {
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your starting template must be {(int)heritageGroup.SkillCredits} skill credits or less! You are currently {-availableSkillCredits} skil credits above that!", ChatMessageType.Broadcast));
                        return;
                    }
                default:
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat("Invalid gameplay mode!", ChatMessageType.Broadcast));
                    return;
            }

            player.SetMaxVitals();

            var inventory = player.GetAllPossessions();
            if (player.GameplayMode != GameplayModes.Limbo)
            {
                foreach (var item in inventory)
                {
                    if (item.GameplayMode >= player.GameplayMode || item.GameplayMode == GameplayModes.Limbo)
                    {
                        item.GameplayMode = player.GameplayMode;
                        item.GameplayModeExtraIdentifier = player.GameplayModeExtraIdentifier;
                        item.GameplayModeIdentifierString = player.GameplayModeIdentifierString;
                    }
                }

                player.UpdateCoinValue();
            }

            if (setStarterLocation)
            {
                player.SetProperty(PropertyBool.RecallsDisabled, false);

                var starterLocation = ThreadSafeRandom.Next(1, 3);
                switch (starterLocation)
                {
                    case 1:
                        if (ThreadSafeRandom.Next(0, 1) == 1)
                            player.Location = new Position(0xD6550023, 108.765625f, 62.215103f, 52.005001f, 0.000000f, 0.000000f, -0.300088f, 0.953912f); // Shoushi West
                        else
                            player.Location = new Position(0xDE51001D, 85.017159f, 107.291908f, 15.861228f, 0.000000f, 0.000000f, 0.323746f, 0.946144f); // Shoushi Southeast
                        break;
                    case 2:
                        if (ThreadSafeRandom.Next(0, 1) == 1)
                            player.Location = new Position(0x7D680012, 65.508179f, 37.516647f, 16.257774f, 0.000000f, 0.000000f, -0.950714f, 0.310069f); // Yaraq North
                        else
                            player.Location = new Position(0x8164000D, 40.296101f, 107.638382f, 31.363008f, 0.000000f, 0.000000f, -0.699884f, -0.714257f); //Yaraq East
                        break;
                    case 3:
                    default:
                        if (ThreadSafeRandom.Next(0, 1) == 1)
                            player.Location = new Position(0xA5B4002A, 131.134338f, 33.602352f, 53.077141f, 0.000000f, 0.000000f, -0.263666f, 0.964614f); // Holtburg West
                        else
                            player.Location = new Position(0xA9B00015, 60.108139f, 103.333549f, 64.402885f, 0.000000f, 0.000000f, -0.381155f, -0.924511f); // Holtburg South
                        break;
                }

                player.Instantiation = new Position(player.Location);
                player.Sanctuary = new Position(player.Location);

                WorldManager.ThreadSafeTeleport(player, player.Instantiation);

                player.CheckMultipleAccounts();
            }
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            if (IsBusy)
            {
                player.Session.Network.EnqueueSend(new GameEventWeenieErrorWithString(player.Session, WeenieErrorWithString.The_IsCurrentlyInUse, Name));
                return;
            }

            if(PkLevelModifier >= 10)
            {
                IsBusy = true;
                player.IsBusy = true;

                var useMotion = UseTargetSuccessAnimation != MotionCommand.Invalid ? UseTargetSuccessAnimation : MotionCommand.Twitch1;
                EnqueueBroadcastMotion(new Motion(this, useMotion));

                var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
                var useTime = motionTable.GetAnimationLength(useMotion);

                player.LastUseTime += useTime;

                var actionChain = new ActionChain();

                actionChain.AddDelaySeconds(useTime);

                actionChain.AddAction(player, () =>
                {
                    ConvertToGameplayMode(player, true);

                    player.IsBusy = false;
                    Reset();
                });

                actionChain.EnqueueChain();

                return;
            }

            if (player.PkLevel == PKLevel.PK && IsNPKSwitch && (Time.GetUnixTime() - player.PkTimestamp) < MinimumTimeSincePk)
            {
                IsBusy = true;
                player.IsBusy = true;

                var actionChain = new ActionChain();

                if (UseTargetFailureAnimation != MotionCommand.Invalid)
                {
                    var useMotion = UseTargetFailureAnimation;
                    EnqueueBroadcastMotion(new Motion(this, useMotion));

                    var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
                    var useTime = motionTable.GetAnimationLength(useMotion);

                    player.LastUseTime += useTime;

                    actionChain.AddDelaySeconds(useTime);
                }

                actionChain.AddAction(player, () =>
                {
                    player.Session.Network.EnqueueSend(new GameEventWeenieError(player.Session, WeenieError.YouFeelAHarshDissonance));
                    player.IsBusy = false;
                    Reset();
                });

                actionChain.EnqueueChain();

                return;
            }

            if ((player.PkLevel == PKLevel.NPK && IsPKSwitch) || (player.PkLevel == PKLevel.PK && IsNPKSwitch))
            {
                IsBusy = true;
                player.IsBusy = true;

                var useMotion = UseTargetSuccessAnimation != MotionCommand.Invalid ? UseTargetSuccessAnimation : MotionCommand.Twitch1;
                EnqueueBroadcastMotion(new Motion(this, useMotion));

                var motionTable = DatManager.PortalDat.ReadFromDat<MotionTable>(MotionTableId);
                var useTime = motionTable.GetAnimationLength(useMotion);

                player.LastUseTime += useTime;

                var actionChain = new ActionChain();

                actionChain.AddDelaySeconds(useTime);

                actionChain.AddAction(player, () =>
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat(GetProperty(PropertyString.UseMessage), ChatMessageType.Broadcast));
                    player.PkLevelModifier += PkLevelModifier;

                    if (player.PkLevel == PKLevel.PK)
                        player.PlayerKillerStatus = PlayerKillerStatus.PK;
                    else
                        player.PlayerKillerStatus = PlayerKillerStatus.NPK;

                    player.EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(player, PropertyInt.PlayerKillerStatus, (int)player.PlayerKillerStatus));
                    //player.ApplySoundEffects(Sound.Open); // in pcaps, but makes no sound/has no effect. ?
                    player.IsBusy = false;
                    Reset();
                });

                actionChain.EnqueueChain();
            }
            else
                player.Session.Network.EnqueueSend(new GameMessageSystemChat(GetProperty(PropertyString.ActivationFailure), ChatMessageType.Broadcast));
        }

        public void Reset()
        {
            IsBusy = false;
        }

        public double? MinimumTimeSincePk
        {
            get => GetProperty(PropertyFloat.MinimumTimeSincePk);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.MinimumTimeSincePk); else SetProperty(PropertyFloat.MinimumTimeSincePk, value.Value); }
        }
    }
}
