
using ACE.Common;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Factories;
using ACE.Server.Factories.Enum;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Linq;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        public void HandleActionAbandonContract(uint contractId)
        {
            ContractManager.Abandon(contractId);
        }

        public void RefreshExplorationAssignments(WorldObject sourceObject = null, bool confirmed = false)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            if (!confirmed && Exploration1KillProgressTracker != 0)
            {
                if (sourceObject != null)
                {
                    if (!ConfirmationManager.EnqueueSend(new Confirmation_Custom(Guid, () => RefreshExplorationAssignments(sourceObject, true), () => GiveFromEmote(sourceObject, (uint)Factories.Enum.WeenieClassName.blankExplorationContract)), "This will overwrite all current exploration assignments.\nProceed?"))
                        SendWeenieError(WeenieError.ConfirmationInProgress);
                }
                else
                {
                    if (!ConfirmationManager.EnqueueSend(new Confirmation_Custom(Guid, () => RefreshExplorationAssignments(sourceObject, true)), "This will overwrite all current exploration assignments.\nProceed?"))
                        SendWeenieError(WeenieError.ConfirmationInProgress);
                }
                return;
            }

            bool useName = sourceObject?.Name.Length > 0;
            var msg = $"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}Unfortunately there's no available exploration assignments suited for you at the moment.{(useName ? $"\"" : "")}";

            if (useName)
            {
                // Cleanup extra contracts that might have cropped up.
                TryConsumeFromEquippedObjectsWithNetworking((uint)Factories.Enum.WeenieClassName.explorationContract);
                TryConsumeFromInventoryWithNetworking((uint)Factories.Enum.WeenieClassName.explorationContract);
            }

            var level = Level ?? 1;
            var minLevel = Math.Max(level - (int)(level * 0.1f), 1);
            var maxLevel = level + (int)(level * 0.2f);
            if (level > 100)
                maxLevel = int.MaxValue;
            var explorationList = DatabaseManager.World.GetExplorationSitesByLevelRange(minLevel, maxLevel, level);

            if (explorationList.Count == 0)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));
                if (sourceObject != null)
                    GiveFromEmote(sourceObject, (uint)Factories.Enum.WeenieClassName.blankExplorationContract); // Return Blank Contract.

                Exploration1LandblockId = 0;
                Exploration1KillProgressTracker = 0;
                Exploration1MarkerProgressTracker = 0;
                Exploration1Description = "";

                Exploration2LandblockId = 0;
                Exploration2KillProgressTracker = 0;
                Exploration2MarkerProgressTracker = 0;
                Exploration2Description = "";

                Exploration3LandblockId = 0;
                Exploration3KillProgressTracker = 0;
                Exploration3MarkerProgressTracker = 0;
                Exploration3Description = "";
                return;
            }

            var roll = ThreadSafeRandom.Next(0, explorationList.Count - 1);
            var entry = explorationList[roll];
            explorationList.RemoveAt(roll);

            var explorationKillAmount = Math.Clamp((int)ThreadSafeRandom.Next(entry.CreatureCount, entry.CreatureCount * 2.0f), 10, 200);
            var explorationMarkerAmount = Math.Clamp(explorationKillAmount / 30, 1, 10) + ThreadSafeRandom.Next(0, 4);

            Exploration1LandblockId = entry.Landblock;
            Exploration1KillProgressTracker = explorationKillAmount;
            Exploration1MarkerProgressTracker = explorationMarkerAmount;

            string entryName;
            string entryDirections;
            var entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)entry.Landblock).FirstOrDefault();
            if (entryLandblock != null)
            {
                entryName = entryLandblock.Name;
                entryDirections = entryLandblock.Directions;
            }
            else
            {
                entryName = $"unknown location({entry.Landblock})";
                entryDirections = "at an unknown location";
            }

            Exploration1Description = $"Explore {entryName} by killing {explorationKillAmount:N0} {entry.ContentDescription} and finding {explorationMarkerAmount:N0} exploration marker{(explorationMarkerAmount > 1 ? "s" : "")}. It is located {entryDirections}.";
            msg = $"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}{Exploration1Description}{(useName ? $"\"" : "")}";

            if (useName)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourceObject.Name} tells you, \"Here's your assignments:\"", ChatMessageType.Tell));
            Session.Network.EnqueueSend(new GameMessageSystemChat(msg, useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));

            if (explorationList.Count != 0)
            {
                roll = ThreadSafeRandom.Next(0, explorationList.Count - 1);
                entry = explorationList[roll];
                explorationList.RemoveAt(roll);
                explorationKillAmount = Math.Clamp((int)ThreadSafeRandom.Next(entry.CreatureCount, entry.CreatureCount * 2.0f), 10, 200);
                explorationMarkerAmount = Math.Clamp(explorationKillAmount / 30, 1, 10) + ThreadSafeRandom.Next(0, 4);

                Exploration2LandblockId = entry.Landblock;
                Exploration2KillProgressTracker = explorationKillAmount;
                Exploration2MarkerProgressTracker = explorationMarkerAmount;

                entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)entry.Landblock).FirstOrDefault();
                if (entryLandblock != null)
                {
                    entryName = entryLandblock.Name;
                    entryDirections = entryLandblock.Directions;
                }
                else
                {
                    entryName = $"unknown location({entry.Landblock})";
                    entryDirections = "at an unknown location";
                }

                Exploration2Description = $"Explore {entryName} by killing {explorationKillAmount:N0} {entry.ContentDescription} and finding {explorationMarkerAmount:N0} exploration marker{(explorationMarkerAmount > 1 ? "s" : "")}. It is located {entryDirections}.";
                msg = $"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}{Exploration2Description}{(useName ? $"\"" : "")}";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));
            }
            else
            {
                Exploration2LandblockId = 0;
                Exploration2KillProgressTracker = 0;
                Exploration2MarkerProgressTracker = 0;
                Exploration2Description = "";
            }

            if (explorationList.Count != 0)
            {
                roll = ThreadSafeRandom.Next(0, explorationList.Count - 1);
                entry = explorationList[roll];
                explorationList.RemoveAt(roll);
                explorationKillAmount = Math.Clamp((int)ThreadSafeRandom.Next(entry.CreatureCount, entry.CreatureCount * 2.0f), 10, 200);
                explorationMarkerAmount = Math.Clamp(explorationKillAmount / 30, 1, 10) + ThreadSafeRandom.Next(0, 4);

                Exploration3LandblockId = entry.Landblock;
                Exploration3KillProgressTracker = explorationKillAmount;
                Exploration3MarkerProgressTracker = explorationMarkerAmount;

                entryLandblock = DatabaseManager.World.GetLandblockDescriptionsByLandblock((ushort)entry.Landblock).FirstOrDefault();
                if (entryLandblock != null)
                {
                    entryName = entryLandblock.Name;
                    entryDirections = entryLandblock.Directions;
                }
                else
                {
                    entryName = $"unknown location({entry.Landblock})";
                    entryDirections = "at an unknown location";
                }

                Exploration3Description = $"Explore {entryName} by killing {explorationKillAmount:N0} {entry.ContentDescription} and finding {explorationMarkerAmount:N0} exploration marker{(explorationMarkerAmount > 1 ? "s" : "")}. It is located {entryDirections}.";
                msg = $"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}{Exploration3Description}{(useName ? $"\"" : "")}";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));
            }
            else
            {
                Exploration3LandblockId = 0;
                Exploration3KillProgressTracker = 0;
                Exploration3MarkerProgressTracker = 0;
                Exploration3Description = "";
            }

            if (sourceObject != null)
            {
                QuestManager.Stamp("ExplorationAssignmentsGiven");
                GiveFromEmote(sourceObject, (uint)Factories.Enum.WeenieClassName.explorationContract); // Give new contract.
            }
        }

        public void RewardExplorationAssignments(WorldObject sourceObject = null, bool confirmed = false)
        {
            bool useName = sourceObject?.Name.Length > 0;

            var hasAssignments = false;
            var assignment1Complete = false;
            var assignment2Complete = false;
            var assignment3Complete = false;
            if (Exploration1LandblockId != 0 && Exploration1Description.Length > 0)
            {
                hasAssignments = true;
                assignment1Complete = Exploration1KillProgressTracker <= 0 && Exploration1MarkerProgressTracker <= 0;
            }
            if (Exploration2LandblockId != 0 && Exploration2Description.Length > 0)
            {
                hasAssignments = true;
                assignment2Complete = Exploration2KillProgressTracker <= 0 && Exploration2MarkerProgressTracker <= 0;
            }
            if (Exploration3LandblockId != 0 && Exploration3Description.Length > 0)
            {
                hasAssignments = true;
                assignment3Complete = Exploration3KillProgressTracker <= 0 && Exploration3MarkerProgressTracker <= 0;
            }

            if (useName)
            {
                // Cleanup extra contracts that might have cropped up.
                TryConsumeFromEquippedObjectsWithNetworking((uint)Factories.Enum.WeenieClassName.explorationContract);
                TryConsumeFromInventoryWithNetworking((uint)Factories.Enum.WeenieClassName.explorationContract);
            }

            if (!hasAssignments)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}{"You have no assignments!"}{(useName ? $"\"" : "")}", useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));
            else if (!assignment1Complete && !assignment2Complete && !assignment3Complete)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{(useName ? $"{sourceObject.Name} tells you, \"" : "")}{"None of your assignments are complete!"}{(useName ? $"\"" : "")}", useName ? ChatMessageType.Tell : ChatMessageType.Broadcast));
            else
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"{sourceObject.Name} tells you, \"Here's your reward for the completed assignments:\"", ChatMessageType.Tell));

                var rewardTier = Math.Clamp(RollTier(CalculateExtendedTier(Level ?? 1)) + 1, 1, 7);
                int rewardAmount;

                if (assignment1Complete)
                {
                    rewardAmount = ThreadSafeRandom.Next(3, 5);
                    for (int i = 0; i < rewardAmount; i++)
                        TryGiveRandomSalvage(sourceObject, rewardTier);

                    Exploration1LandblockId = 0;
                    Exploration1KillProgressTracker = 0;
                    Exploration1MarkerProgressTracker = 0;
                    Exploration1Description = "";
                }
                if (assignment2Complete)
                {
                    rewardAmount = ThreadSafeRandom.Next(3, 5);
                    for (int i = 0; i < rewardAmount; i++)
                        TryGiveRandomSalvage(sourceObject, rewardTier);

                    Exploration2LandblockId = 0;
                    Exploration2KillProgressTracker = 0;
                    Exploration2MarkerProgressTracker = 0;
                    Exploration2Description = "";
                }
                if (assignment3Complete)
                {
                    rewardAmount = ThreadSafeRandom.Next(3, 5);
                    for (int i = 0; i < rewardAmount; i++)
                        TryGiveRandomSalvage(sourceObject, rewardTier);

                    Exploration3LandblockId = 0;
                    Exploration3KillProgressTracker = 0;
                    Exploration3MarkerProgressTracker = 0;
                    Exploration3Description = "";
                }
            }

            if (sourceObject != null && hasAssignments && (!assignment1Complete || !assignment1Complete || !assignment1Complete))
            {
                GiveFromEmote(sourceObject, (uint)Factories.Enum.WeenieClassName.explorationContract); // Return contract if there's still unfinished contracts.
            }
        }

        public bool TryGiveRandomSalvage(WorldObject giver = null, int tier = 1, float qualityMod = 0.0f)
        {
            var salvage = LootGenerationFactory.CreateRandomLootObjects_New(tier, qualityMod, TreasureItemCategory.MundaneItem, TreasureItemType_Orig.Salvage);
            var success = false;
            if (salvage != null)
            {
                if (giver != null)
                    success = TryCreateForGive(giver, salvage);
                else
                    success = TryCreateInInventoryWithNetworking(salvage, out _, true);
            }

            if (!success)
                salvage.Destroy();

            return success;
        }
    }
}
