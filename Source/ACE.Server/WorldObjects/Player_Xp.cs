using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Common.Extensions;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Server.Command.Handlers;
using ACE.Server.Entity;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// A player earns XP through natural progression, ie. kills and quests completed
        /// </summary>
        /// <param name="amount">The amount of XP being added</param>
        /// <param name="xpType">The source of XP being added</param>
        /// <param name="shareable">True if this XP can be shared with Fellowship</param>
        public void EarnXP(long amount, XpType xpType, int? xpSourceLevel, uint? xpSourceId, uint xpSourceCampValue, double? xpSourceTier, ShareType shareType = ShareType.All, string xpMessage = "")
        {
            //Console.WriteLine($"{Name}.EarnXP({amount}, {sharable}, {fixedAmount})");

            bool usesRewardByLevelSystem = false;
            if ((xpType == XpType.Quest || xpType == XpType.Exploration) && amount < 0 && amount > -6000) // this range is used to specify the reward by level system.
            {
                usesRewardByLevelSystem = true;
                int formulaVersion;
                // The following comments are just recommendations and vary from quest to quest, but the larger the value the higher the xp sum awarded.
                if (amount <= -5000) // once per character
                {
                    xpSourceLevel = -((int)amount + 5000);
                    formulaVersion = 5;
                }
                else if (amount <= -4000) // once every 3 weeks or more
                {
                    xpSourceLevel = -((int)amount + 4000);
                    formulaVersion = 4;
                }
                else if (amount <= -3000) // once a week or more
                {
                    xpSourceLevel = -((int)amount + 3000);
                    formulaVersion = 3;
                }
                else if (amount <= -2000) // once a day or more
                {
                    xpSourceLevel = -((int)amount + 2000);
                    formulaVersion = 2;
                }
                else if (amount <= -1000) // more than once per day
                {
                    xpSourceLevel = -((int)amount + 1000);
                    formulaVersion = 1;
                }
                else
                {
                    xpSourceLevel = -(int)amount;
                    formulaVersion = 0;
                }

                int modifiedLevel = Math.Max((int)Level, 5);
                if (Level < 100 && modifiedLevel <= xpSourceLevel / 3)
                {
                    xpSourceLevel = modifiedLevel * 3;
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Your experience reward has been reduced because your level is not high enough!", ChatMessageType.System));
                }

                float totalXP = GetCreatureDeathXP(xpSourceLevel.Value, 0, false, false, formulaVersion);

                if (xpSourceId != null && xpSourceId != 0)
                {
                    float typeCampBonus;
                    CampManager.HandleCampInteraction(xpSourceId.Value ^ 0xFFFF0000, null, xpSourceCampValue, out typeCampBonus, out _, out _);

                    totalXP = totalXP * typeCampBonus;

                    xpMessage = $"T: {(typeCampBonus * 100).ToString("0")}%";
                }

                amount = (long)Math.Round(totalXP);
            }
            else if (amount < 0)
            {
                SpendXP(-amount);
                return;
            }
            else if (xpType == XpType.Kill && Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                float typeCampBonus;
                float areaCampBonus;
                float restCampBonus;

                float totalXP = amount;

                if (xpSourceId != null && xpSourceId != 0)
                {
                    CampManager.HandleCampInteraction(xpSourceId.Value, CurrentLandblock, xpSourceCampValue, out typeCampBonus, out areaCampBonus, out restCampBonus);

                    float thirdXP = totalXP / 3.0f;
                    totalXP = (thirdXP * typeCampBonus) + (thirdXP * areaCampBonus) + (thirdXP * restCampBonus);

                    xpMessage = $"{xpMessage}{(xpMessage.Length > 0 ? " " : "")}T: {(typeCampBonus * 100).ToString("0")}% A: {(areaCampBonus * 100).ToString("0")}% R: {(restCampBonus * 100).ToString("0")}%";

                    if (CurrentLandblock != null && EventManager.HotDungeonLandblock == CurrentLandblock.Id.Raw >> 16)
                    {
                        var extraXP = totalXP * (float)PropertyManager.GetDouble("hot_dungeon_bonus_xp").Item;
                        totalXP += extraXP;

                        xpMessage = $"Hot Dungeon Bonus: +{extraXP:N0}xp {xpMessage}";
                    }
                }

                if (CurrentLandblock != null && !(CurrentLandblock.IsDungeon || (CurrentLandblock.HasDungeon && Location.Indoors)))
                {
                    var extraXP = totalXP * (float)PropertyManager.GetDouble("surface_bonus_xp").Item; // Surface provides extra xp to account for lower creature density.
                    totalXP += extraXP;

                    xpMessage = $"Surface Bonus: +{extraXP:N0}xp {xpMessage}";
                }

                amount = (long)Math.Round(totalXP);
            }

            // apply xp modifiers.  Quest XP is multiplicative with general XP modification
            var questModifier = PropertyManager.GetDouble("quest_xp_modifier").Item;
            var modifier = PropertyManager.GetDouble("xp_modifier").Item;
            if (xpType == XpType.Quest)
                modifier *= questModifier;

            if (GameplayMode == GameplayModes.HardcorePK)
                modifier *= PropertyManager.GetDouble("hardcore_pk_xp_modifier").Item;
            else if (GameplayMode == GameplayModes.HardcoreNPK)
                modifier *= PropertyManager.GetDouble("hardcore_npk_xp_modifier").Item;

            if (xpType == XpType.Kill)
            {
                if (xpSourceTier != null)
                {
                    var highTier = Math.Ceiling(xpSourceTier ?? 1);
                    var lowTier = Math.Floor(xpSourceTier ?? 1);

                    double highTierMod;
                    switch (highTier)
                    {
                        case 1: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier1").Item; break;
                        case 2: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier2").Item; break;
                        case 3: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier3").Item; break;
                        case 4: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier4").Item; break;
                        case 5: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier5").Item; break;
                        case 6: highTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier6").Item; break;
                        default: highTierMod = 1; break;
                    }

                    double lowTierMod;
                    switch (lowTier)
                    {
                        case 1: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier1").Item; break;
                        case 2: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier2").Item; break;
                        case 3: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier3").Item; break;
                        case 4: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier4").Item; break;
                        case 5: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier5").Item; break;
                        case 6: lowTierMod = PropertyManager.GetDouble("xp_modifier_kill_tier6").Item; break;
                        default: lowTierMod = 1; break;
                    }

                    if (highTierMod != 1 || lowTierMod != 1)
                    {
                        var highTierWeight = (xpSourceTier ?? 1) % 1;
                        var lowTierWeight = 1 - highTierWeight;

                        var highTierAmount = (long)(amount * highTierWeight * highTierMod);
                        var lowTierAmount = (long)(amount * lowTierWeight * lowTierMod);

                        amount = lowTierAmount + highTierAmount;
                    }
                }
                else if (xpSourceLevel != null)
                {
                    if (xpSourceLevel < 28)
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier1").Item;
                    else if (xpSourceLevel < 65)
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier2").Item;
                    else if (xpSourceLevel < 95)
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier3").Item;
                    else if (xpSourceLevel < 110)
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier4").Item;
                    else if (xpSourceLevel < 135)
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier5").Item;
                    else
                        modifier *= PropertyManager.GetDouble("xp_modifier_kill_tier6").Item;
                }
            }
            else if (xpType == XpType.Quest && xpSourceLevel != null)
            {
                if (usesRewardByLevelSystem)
                {
                    var tier = CalculateExtendedTier(xpSourceLevel ?? 1);
                    var highTier = Math.Ceiling(tier);
                    var lowTier = Math.Floor(tier);

                    double highTierMod;
                    switch (highTier)
                    {
                        case 1: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier1").Item; break;
                        case 2: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier2").Item; break;
                        case 3: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier3").Item; break;
                        case 4: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier4").Item; break;
                        case 5: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier5").Item; break;
                        case 6: highTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier6").Item; break;
                        default: highTierMod = 1; break;
                    }

                    double lowTierMod;
                    switch (lowTier)
                    {
                        case 1: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier1").Item; break;
                        case 2: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier2").Item; break;
                        case 3: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier3").Item; break;
                        case 4: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier4").Item; break;
                        case 5: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier5").Item; break;
                        case 6: lowTierMod = PropertyManager.GetDouble("xp_modifier_reward_tier6").Item; break;
                        default: lowTierMod = 1; break;
                    }

                    if (highTierMod != 1 || lowTierMod != 1)
                    {
                        var highTierWeight = (xpSourceTier ?? 1) % 1;
                        var lowTierWeight = 1 - highTierWeight;

                        var highTierAmount = (long)(amount * highTierWeight * highTierMod);
                        var lowTierAmount = (long)(amount * lowTierWeight * lowTierMod);

                        amount = lowTierAmount + highTierAmount;
                    }
                }
                else
                {
                    if (xpSourceLevel < 16)
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier1").Item;
                    else if (xpSourceLevel < 36)
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier2").Item;
                    else if (xpSourceLevel < 56)
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier3").Item;
                    else if (xpSourceLevel < 76)
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier4").Item;
                    else if (xpSourceLevel < 96)
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier5").Item;
                    else
                        modifier *= PropertyManager.GetDouble("xp_modifier_reward_tier6").Item;
                }
            }

            // should this be passed upstream to fellowship / allegiance?
            var enchantment = GetXPAndLuminanceModifier(xpType);

            var m_amount = (long)Math.Round(amount * enchantment * modifier);

            if (m_amount < 0)
            {
                log.Warn($"{Name}.EarnXP({amount}, {shareType})");
                log.Warn($"modifier: {modifier}, enchantment: {enchantment}, m_amount: {m_amount}");
                return;
            }

            GrantXP(m_amount, xpType, shareType, xpMessage);

            if (xpType == XpType.Kill && CurrentLandblock != null)
            {
                if (Exploration1LandblockId == CurrentLandblock.Id.Raw >> 16 && Exploration1KillProgressTracker > 0)
                {
                    Exploration1KillProgressTracker--;
                    long explorationXP = (long)(m_amount * (float)PropertyManager.GetDouble("exploration_bonus_xp").Item);
                    xpMessage = $"{Exploration1KillProgressTracker:N0} kill{(Exploration1KillProgressTracker != 1 ? "s" : "")} remaining.";
                    GrantXP(explorationXP, XpType.Exploration, ShareType.None, xpMessage);
                }
                else if (Exploration2LandblockId == CurrentLandblock.Id.Raw >> 16 && Exploration2KillProgressTracker > 0)
                {
                    Exploration2KillProgressTracker--;
                    long explorationXP = (long)(m_amount * (float)PropertyManager.GetDouble("exploration_bonus_xp").Item);
                    xpMessage = $"{Exploration2KillProgressTracker:N0} kill{(Exploration2KillProgressTracker != 1 ? "s" : "")} remaining.";
                    GrantXP(explorationXP, XpType.Exploration, ShareType.None, xpMessage);
                }
                else if (Exploration3LandblockId == CurrentLandblock.Id.Raw >> 16 && Exploration3KillProgressTracker > 0)
                {
                    Exploration3KillProgressTracker--;
                    long explorationXP = (long)(m_amount * (float)PropertyManager.GetDouble("exploration_bonus_xp").Item);
                    xpMessage = $"{Exploration3KillProgressTracker:N0} kill{(Exploration3KillProgressTracker != 1 ? "s" : "")} remaining.";
                    GrantXP(explorationXP, XpType.Exploration, ShareType.None, xpMessage);
                }
            }
        }

        /// <summary>
        /// Directly grants XP to the player, without the XP modifier
        /// </summary>
        /// <param name="amount">The amount of XP to grant to the player</param>
        /// <param name="xpType">The source of the XP being granted</param>
        /// <param name="shareable">If TRUE, this XP can be shared with fellowship members</param>
        public void GrantXP(long amount, XpType xpType, ShareType shareType = ShareType.All, string xpMessage = "")
        {
            if (GameplayMode == GameplayModes.Limbo)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You are in limbo mode and cannot earn any experience, please select a gameplay mode.", ChatMessageType.Broadcast));
                return;
            }

            if (IsOlthoiPlayer)
            {
                if (HasVitae)
                    UpdateXpVitae(amount);

                return;
            }

            if (Fellowship != null && Fellowship.ShareXP && shareType.HasFlag(ShareType.Fellowship))
            {
                // this will divy up the XP, and re-call this function
                // with ShareType.Fellowship removed
                Fellowship.SplitXp((ulong)amount, xpType, shareType, this, xpMessage);
                return;
            }

            // Make sure UpdateXpAndLevel is done on this players thread
            EnqueueAction(new ActionEventDelegate(() => UpdateXpAndLevel(amount, xpType, xpMessage)));

            //Update XP tracking info
            try
            {
                if (!XpTrackerStartTimestamp.HasValue)
                {
                    XpTrackerStartTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
                    XpTrackerTotalXp = 0;
                }

                XpTrackerTotalXp += amount;
            }
            catch (Exception ex)
            {
                log.Error($"Exception in Player.GrantXP while updating XP tracking info. Ex: {ex}");
            }

            // for passing XP up the allegiance chain,
            // this function is only called at the very beginning, to start the process.
            if (shareType.HasFlag(ShareType.Allegiance))
                UpdateXpAllegiance(amount);

            // only certain types of XP are granted to items
            if (xpType == XpType.Kill || xpType == XpType.Quest)
                GrantItemXP(amount);
        }

        /// <summary>
        /// Adds XP to a player's total XP, handles triggers (vitae, level up)
        /// </summary>
        private void UpdateXpAndLevel(long amount, XpType xpType, string xpMessage = "")
        {
            // until we are max level we must make sure that we send
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();
            var maxLevelXp = xpTable.CharacterLevelXPList[(int)maxLevel];

            bool allowXpAtMaxLevel = PropertyManager.GetBool("allow_xp_at_max_level").Item;
            var totalXpCap = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? maxLevelXp : long.MaxValue; // At what value the total xp counter will stop counting.
            var availableXpCap = Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration ? uint.MaxValue : long.MaxValue; // Max unassigned xp amount.

            if (Level != maxLevel || allowXpAtMaxLevel)
            {
                var addAmount = amount;

                var amountLeftToEnd = (long)maxLevelXp - TotalExperience ?? 0;
                if (!allowXpAtMaxLevel && amount > amountLeftToEnd)
                    addAmount = amountLeftToEnd;

                TotalExperience += addAmount;
                if (TotalExperience > (long)totalXpCap)
                    TotalExperience = (long)totalXpCap;

                AvailableExperience += addAmount;
                if (AvailableExperience > availableXpCap)
                    AvailableExperience = availableXpCap;

                var xpTotalUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.TotalExperience, TotalExperience ?? 0);
                var xpAvailUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
                Session.Network.EnqueueSend(xpTotalUpdate, xpAvailUpdate);

                CheckForLevelup();
            }

            if (xpType == XpType.Quest)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience. {xpMessage}", ChatMessageType.Broadcast));
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (xpType == XpType.Fellowship)
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your fellowship shared {amount:N0} experience with you!", ChatMessageType.Broadcast));
                else if (xpType == XpType.Kill && xpMessage != "")
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience! {xpMessage}", ChatMessageType.Broadcast));
                else if (amount > 0 && xpType == XpType.Proficiency && xpMessage != "")
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} {xpMessage} experience!", ChatMessageType.Broadcast));
                else if (amount > 0 && xpType == XpType.Exploration)
                {
                    if (xpMessage != "")
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} exploration experience! {xpMessage}", ChatMessageType.Broadcast));
                    else
                        Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} exploration experience!", ChatMessageType.Broadcast));
                }
            }

            if (HasVitae && xpType != XpType.Allegiance)
                UpdateXpVitae(amount);
        }

        /// <summary>
        /// Optionally passes XP up the Allegiance tree
        /// </summary>
        private void UpdateXpAllegiance(long amount)
        {
            if (!HasAllegiance) return;

            AllegianceManager.PassXP(AllegianceNode, (ulong)amount, true);
        }

        /// <summary>
        /// Handles updating the vitae penalty through earned XP
        /// </summary>
        /// <param name="amount">The amount of XP to apply to the vitae penalty</param>
        private void UpdateXpVitae(long amount)
        {
            var vitae = EnchantmentManager.GetVitae();

            if (vitae == null)
            {
                log.Error($"{Name}.UpdateXpVitae({amount}) vitae null, likely due to cross-thread operation or corrupt EnchantmentManager cache. Please report this.");
                log.Error(Environment.StackTrace);
                return;
            }

            var vitaePenalty = vitae.StatModValue;
            var startPenalty = vitaePenalty;

            var maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            var curPool = VitaeCpPool + amount;
            while (curPool >= maxPool)
            {
                curPool -= maxPool;
                vitaePenalty = EnchantmentManager.ReduceVitae();
                if (vitaePenalty == 1.0f)
                    break;
                maxPool = (int)VitaeCPPoolThreshold(vitaePenalty, DeathLevel.Value);
            }
            VitaeCpPool = (int)curPool;

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.VitaeCpPool, VitaeCpPool.Value));

            if (vitaePenalty != startPenalty)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your experience has reduced your Vitae penalty!", ChatMessageType.Magic));
                EnchantmentManager.SendUpdateVitae();
            }

            if (vitaePenalty.EpsilonEquals(1.0f) || vitaePenalty > 1.0f)
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(2.0f);
                actionChain.AddAction(this, () =>
                {
                    var vitae = EnchantmentManager.GetVitae();
                    if (vitae != null)
                    {
                        var curPenalty = vitae.StatModValue;
                        if (curPenalty.EpsilonEquals(1.0f) || curPenalty > 1.0f)
                            EnchantmentManager.RemoveVitae();
                    }
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the maximum possible character level
        /// </summary>
        public static uint GetMaxLevel()
        {
            uint maxPossibleLevel = (uint)DatManager.PortalDat.XpTable.CharacterLevelXPList.Count - 1;
            uint maxSettingLevel = (uint)PropertyManager.GetLong("max_level").Item;
            return (Math.Min(maxPossibleLevel, maxSettingLevel));
        }

        /// <summary>
        /// Returns TRUE if player >= MaxLevel
        /// </summary>
        public bool IsMaxLevel => Level >= GetMaxLevel();

        /// <summary>
        /// Returns the remaining XP required to reach a level
        /// </summary>
        public long? GetRemainingXP(uint level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 1 || level > maxLevel)
                return null;

            var levelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[(int)level];

            return (long)levelTotalXP - TotalExperience.Value;
        }

        /// <summary>
        /// Returns the remaining XP required to the next level
        /// </summary>
        public ulong GetRemainingXP()
        {
            var maxLevel = GetMaxLevel();
            if (Level >= maxLevel)
                return 0;

            var nextLevelTotalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[Level.Value + 1];
            return nextLevelTotalXP - (ulong)TotalExperience.Value;
        }

        /// <summary>
        /// Returns the total XP required to reach a level
        /// </summary>
        public static ulong GetTotalXP(int level)
        {
            var maxLevel = GetMaxLevel();
            if (level < 0 || level > maxLevel)
                return 0;

            return DatManager.PortalDat.XpTable.CharacterLevelXPList[level];
        }

        /// <summary>
        /// Returns the total amount of XP required for a player reach max level
        /// </summary>
        public static long MaxLevelXP
        {
            get
            {
                var xpTable = DatManager.PortalDat.XpTable.CharacterLevelXPList;

                return (long)xpTable[xpTable.Count - 1];
            }
        }

        /// <summary>
        /// Returns the XP required to go from level A to level B
        /// </summary>
        public ulong GetXPBetweenLevels(int levelA, int levelB)
        {
            // special case for max level
            var maxLevel = (int)GetMaxLevel();

            levelA = Math.Clamp(levelA, 1, maxLevel - 1);
            levelB = Math.Clamp(levelB, 1, maxLevel);

            var levelA_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelA];
            var levelB_totalXP = DatManager.PortalDat.XpTable.CharacterLevelXPList[levelB];

            return levelB_totalXP - levelA_totalXP;
        }

        public ulong GetXPToNextLevel(int level)
        {
            return GetXPBetweenLevels(level, level + 1);
        }

        /// <summary>
        /// Determines if the player has advanced a level
        /// </summary>
        private void CheckForLevelup()
        {
            var xpTable = DatManager.PortalDat.XpTable;

            var maxLevel = GetMaxLevel();

            if (Level >= maxLevel) return;

            var startingLevel = Level;
            bool creditEarned = false;

            // increases until the correct level is found
            while ((ulong)(TotalExperience ?? 0) >= xpTable.CharacterLevelXPList[(Level ?? 0) + 1])
            {
                Level++;

                // increase the skill credits if the chart allows this level to grant a credit
                if (xpTable.CharacterLevelSkillCreditList[Level ?? 0] > 0)
                {
                    AvailableSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                    TotalSkillCredits += (int)xpTable.CharacterLevelSkillCreditList[Level ?? 0];
                    creditEarned = true;
                }

                // break if we reach max
                if (Level == maxLevel)
                {
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    break;
                }
            }

            if (Level > startingLevel)
            {
                var message = (Level == maxLevel) ? $"You have reached the maximum level of {Level}!" : $"You are now level {Level}!";

                message += (AvailableSkillCredits > 0) ? $"\nYou have {AvailableExperience:#,###0} experience points and {AvailableSkillCredits} skill credits available to raise skills and attributes." : $"\nYou have {AvailableExperience:#,###0} experience points available to raise skills and attributes.";

                var levelUp = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.Level, Level ?? 1);
                var currentCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                if (Level != maxLevel && !creditEarned)
                {
                    var nextLevelWithCredits = 0;

                    for (int i = (Level ?? 0) + 1; i <= maxLevel; i++)
                    {
                        if (xpTable.CharacterLevelSkillCreditList[i] > 0)
                        {
                            nextLevelWithCredits = i;
                            break;
                        }
                    }
                    message += $"\nYou will earn another skill credit at level {nextLevelWithCredits}.";
                }

                if (Fellowship != null)
                    Fellowship.OnFellowLevelUp(this);

                if (AllegianceNode != null)
                    AllegianceNode.OnLevelUp();

                Session.Network.EnqueueSend(levelUp);

                SetMaxVitals();

                // play level up effect
                PlayParticleEffect(PlayScript.LevelUp, Guid);

                VerifyWieldedLevelRequirements();

                Session.Network.EnqueueSend(new GameMessageSystemChat(message, ChatMessageType.Advancement), currentCredits);

                // Let's take the opportinity to send an activity recommendation to the player.
                var recommendationChain = new ActionChain();
                recommendationChain.AddDelaySeconds(5.0f);
                recommendationChain.AddAction(this, () =>
                {
                    PlayerCommands.SingleRecommendation(Session, true);
                });
                recommendationChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Spends the amount of XP specified, deducting it from available experience
        /// </summary>
        public bool SpendXP(long amount, bool sendNetworkUpdate = true)
        {
            if (amount > AvailableExperience)
                return false;

            AvailableExperience -= amount;

            if (sendNetworkUpdate)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0));

            return true;
        }

        /// <summary>
        /// Tries to spend all of the players Xp into Attributes, Vitals and Skills
        /// </summary>
        public void SpendAllXp(bool sendNetworkUpdate = true)
        {
            SpendAllAvailableAttributeXp(Strength, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Endurance, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Coordination, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Quickness, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Focus, sendNetworkUpdate);
            SpendAllAvailableAttributeXp(Self, sendNetworkUpdate);

            SpendAllAvailableVitalXp(Health, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Stamina, sendNetworkUpdate);
            SpendAllAvailableVitalXp(Mana, sendNetworkUpdate);

            foreach (var skill in Skills)
            {
                if (skill.Value.AdvancementClass >= SkillAdvancementClass.Trained)
                    SpendAllAvailableSkillXp(skill.Value, sendNetworkUpdate);
            }
        }

        /// <summary>
        /// Gives available XP of the amount specified, without increasing total XP
        /// </summary>
        public void RefundXP(long amount)
        {
            AvailableExperience += amount;

            var xpUpdate = new GameMessagePrivateUpdatePropertyInt64(this, PropertyInt64.AvailableExperience, AvailableExperience ?? 0);
            Session.Network.EnqueueSend(xpUpdate);
        }

        public void HandleMissingXp()
        {
            var verifyXp = GetProperty(PropertyInt64.VerifyXp) ?? 0;
            if (verifyXp == 0) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                var xpType = verifyXp > 0 ? "unassigned experience" : "experience points";

                var msg = $"This character was missing some {xpType} --\nYou have gained an additional {Math.Abs(verifyXp).ToString("N0")} {xpType}!";

                Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                if (verifyXp < 0)
                {
                    // add to character's total XP
                    TotalExperience -= verifyXp;

                    CheckForLevelup();
                }

                RemoveProperty(PropertyInt64.VerifyXp);
            });

            actionChain.EnqueueChain();
        }

        /// <summary>
        /// Returns the total amount of XP required to go from vitae to vitae + 0.01
        /// </summary>
        /// <param name="vitae">The current player life force, ie. 0.95f vitae = 5% penalty</param>
        /// <param name="level">The player DeathLevel, their level on last death</param>
        private double VitaeCPPoolThreshold(float vitae, int level)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                return (Math.Pow(level, 2.5) * 2.5 + 20.0) * Math.Pow(vitae, 5.0) + 0.5;
            else
            {
                // http://acpedia.org/wiki/Announcements_-_2005/07_-_Throne_of_Destiny_(expansion)#FAQ_-_AC:TD_Level_Cap_Update
                // "The vitae system has not changed substantially since Asheron's Call launched in 1999.
                // Since that time, the experience awarded by killing creatures has increased considerably.
                // This means that a 5% vitae loss currently is much easier to work off now than it was in the past.
                // In addition, the maximum cost to work off a point of vitae was capped at 12,500 experience points."
                return Math.Min((Math.Pow(level, 2) * 5 + 20) * Math.Pow(vitae, 5.0) + 0.5, 12500);
            }
        }

        /// <summary>
        /// Raise the available XP by a percentage of the current level XP or a maximum
        /// </summary>
        public void GrantLevelProportionalXp(double percent, long min, long max)
        {
            var nextLevelXP = GetXPBetweenLevels(Level.Value, Level.Value + 1);

            var scaledXP = (long)Math.Round(nextLevelXP * percent);

            if (max > 0)
                scaledXP = Math.Min(scaledXP, max);

            if (min > 0)
                scaledXP = Math.Max(scaledXP, min);

            // apply xp modifiers?
            EarnXP(scaledXP, XpType.Quest, Level, null, 1, null, ShareType.Allegiance);
        }

        /// <summary>
        /// The player earns XP for items that can be leveled up
        /// by killing creatures and completing quests,
        /// while those items are equipped.
        /// </summary>
        public void GrantItemXP(long amount)
        {
            foreach (var item in EquippedObjects.Values.Where(i => i.HasItemLevel))
                GrantItemXP(item, amount);
        }

        public void GrantItemXP(WorldObject item, long amount)
        {
            var prevItemLevel = item.ItemLevel.Value;
            var addItemXP = item.AddItemXP(amount);

            if (addItemXP > 0)
                Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt64(item, PropertyInt64.ItemTotalXp, item.ItemTotalXp.Value));

            // handle item leveling up
            var newItemLevel = item.ItemLevel.Value;
            if (newItemLevel > prevItemLevel)
            {
                OnItemLevelUp(item, prevItemLevel);

                var actionChain = new ActionChain();
                actionChain.AddAction(this, () =>
                {
                    var msg = $"Your {item.Name} has increased in power to level {newItemLevel}!";
                    Session.Network.EnqueueSend(new GameMessageSystemChat(msg, ChatMessageType.Broadcast));

                    EnqueueBroadcast(new GameMessageScript(Guid, PlayScript.AetheriaLevelUp));
                });
                actionChain.EnqueueChain();
            }
        }

        /// <summary>
        /// Returns the multiplier to XP and Luminance from Trinkets and Augmentations
        /// </summary>
        public float GetXPAndLuminanceModifier(XpType xpType)
        {
            var enchantmentBonus = EnchantmentManager.GetXPBonus();

            var augBonus = 0.0f;
            if (xpType == XpType.Kill && AugmentationBonusXp > 0)
                augBonus = AugmentationBonusXp * 0.05f;

            var modifier = 1.0f + enchantmentBonus + augBonus;
            //Console.WriteLine($"XPAndLuminanceModifier: {modifier}");

            return modifier;
        }

        public void RevertToBrandNewCharacter(bool keepFellowship, bool keepAllegiance, bool keepHousing, bool setToLimboGameplayMode = false, long startingXP = 0)
        {
            if(!keepFellowship)
                FellowshipQuit(false);

            if (!keepAllegiance)
                AllegianceManager.HandlePlayerDelete(Guid.Full);

            // Reset Gameplay Mode
            PlayerKillerStatus = PlayerKillerStatus.NPK;
            PkLevel = PKLevel.NPK;
            GameplayMode = GameplayModes.Regular;
            GameplayModeExtraIdentifier = 0;
            GameplayModeIdentifierString = null;

            // Reset Titles
            RemoveAllTitles();
            if(setToLimboGameplayMode)
                AddTitle((uint)CharacterTitle.DeadMeat, true, true, true);
            else if (ChargenTitleId > 0)
                AddTitle((uint)ChargenTitleId, true, true, true);

            // Reset buffs and vitae
            EnchantmentManager.RemoveVitae();
            EnchantmentManager.RemoveAllEnchantments();

            // Reset trackers
            XpTrackerStartTimestamp = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();
            XpTrackerTotalXp = 0;
            NumDeaths = 0;
            PlayerKillsPkl = 0;
            PlayerKillsPk = 0;
            DeathLevel = 0;
            Age = 0;
            initialAgeTime = DateTime.MinValue;
            ImbueAttempts = 0;
            ImbueSuccesses = 0;
            LeyLineSeed = null;
            SetProperty(PropertyString.DateOfBirth, $"{DateTime.UtcNow:dd MMMM yyyy}");

            // Reset positions
            RemovePosition(PositionType.LastOutsideDeath);
            RemovePosition(PositionType.LastPortal);
            RemovePosition(PositionType.LinkedLifestone);
            RemovePosition(PositionType.LinkedPortalOne);
            RemovePosition(PositionType.LinkedPortalTwo);

            RemoveAllSpells();

            // Reset Quest Timers
            QuestManager.EraseAll();

            // Reset Contracts
            ContractManager.EraseAll();

            // Reset Exploration Contracts
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

            // Reset Attributes
            var propertyCount = Enum.GetNames(typeof(PropertyAttribute)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var attribute = (PropertyAttribute)i;

                Attributes[attribute].Ranks = 0;
                Attributes[attribute].ExperienceSpent = 0;
            }

            propertyCount = Enum.GetNames(typeof(PropertyAttribute2nd)).Length;
            for (var i = 1; i < propertyCount; i += 2)
            {
                var attribute = (PropertyAttribute2nd)i;

                Vitals[attribute].Ranks = 0;
                Vitals[attribute].ExperienceSpent = 0;
            }

            // Reset Skills
            propertyCount = Enum.GetNames(typeof(Skill)).Length;
            for (var i = 1; i < propertyCount; i++)
            {
                var skill = (Skill)i;

                ResetSkill(skill, false, true);
            }

            AvailableExperience = 0;

            var heritageGroup = DatManager.PortalDat.CharGen.HeritageGroups[(uint)(Heritage ?? 1)];
            AvailableSkillCredits = (int)heritageGroup.SkillCredits;

            // Reset Luminance
            LumAugDamageRating = 0;
            LumAugDamageReductionRating = 0;
            LumAugCritDamageRating = 0;
            LumAugCritReductionRating = 0;
            //LumAugSurgeEffectRating = 0;
            LumAugSurgeChanceRating = 0;
            LumAugItemManaUsage = 0;
            LumAugItemManaGain = 0;
            LumAugVitality = 0;
            LumAugHealingRating = 0;
            LumAugSkilledCraft = 0;
            LumAugSkilledSpec = 0;
            LumAugAllSkills = 0;

            AvailableLuminance = null;
            MaximumLuminance = null;

            // Reset Society
            Faction1Bits = null;
            SocietyRankCelhan = null;
            SocietyRankEldweb = null;
            SocietyRankRadblo = null;

            // Reset Aetheria
            AetheriaFlags = AetheriaBitfield.None;

            // Remove Level
            TotalExperience = 0;
            Level = 1;

            // Add starter skills
            if (ChargenSkillsTrained != null)
            {
                var skillsToTrain = ChargenSkillsTrained.Split("|");
                foreach (var skillString in skillsToTrain)
                {
                    if (int.TryParse(skillString, out var skillId))
                    {
                        var skill = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)skillId];
                        var trainedCost = skill.TrainedCost;

                        foreach (var skillGroup in heritageGroup.Skills)
                        {
                            if (skillGroup.SkillNum == skillId)
                            {
                                trainedCost = skillGroup.NormalCost;
                                break;
                            }
                        }

                        TrainSkill((Skill)skillId, trainedCost, true);
                    }
                }
            }

            if (ChargenSkillsSpecialized != null)
            {
                var skillsToSpecialize = ChargenSkillsSpecialized.Split("|");
                foreach (var skillString in skillsToSpecialize)
                {
                    if (int.TryParse(skillString, out var skillId))
                    {
                        var skill = DatManager.PortalDat.SkillTable.SkillBaseHash[(uint)skillId];
                        var trainedCost = skill.TrainedCost;
                        var specializedCost = skill.UpgradeCostFromTrainedToSpecialized;

                        foreach (var skillGroup in heritageGroup.Skills)
                        {
                            if (skillGroup.SkillNum == skillId)
                            {
                                trainedCost = skillGroup.NormalCost;
                                specializedCost = skillGroup.PrimaryCost;
                                break;
                            }
                        }

                        TrainSkill((Skill)skillId, trainedCost);
                        SpecializeSkill((Skill)skillId, specializedCost);
                    }
                }
            }

            if (ChargenSkillsSecondary != null)
            {
                var skillsToSetAsSecondary = ChargenSkillsSecondary.Split("|");
                foreach (var skillString in skillsToSetAsSecondary)
                {
                    var skillAndPrimarySkill = skillString.Split(":");
                    if(skillAndPrimarySkill.Length == 2)
                    {
                        if (int.TryParse(skillAndPrimarySkill[0], out var secondarySkillId) && int.TryParse(skillAndPrimarySkill[1], out var primarySkillId))
                        {
                            var primarySkill = GetCreatureSkill((Skill)primarySkillId);
                            var secondarySkill = GetCreatureSkill((Skill)secondarySkillId);

                            if(primarySkill.AdvancementClass > SkillAdvancementClass.Untrained && secondarySkill.AdvancementClass > SkillAdvancementClass.Untrained)
                            {
                                secondarySkill.SecondaryTo = (Skill)primarySkillId;
                            }
                        }
                    }
                }
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
            {
                // Set Heritage based Melee and Ranged Masteries
                PlayerFactory.GetMasteries(HeritageGroup, out WeaponType meleeMastery, out WeaponType rangedMastery);

                SetProperty(PropertyInt.MeleeMastery, (int)meleeMastery);
                SetProperty(PropertyInt.RangedMastery, (int)rangedMastery);

                // Set innate augs
                PlayerFactory.SetInnateAugmentations(this);
            }

            RevertToBrandNewCharacterEquipment(keepHousing);

            if (startingXP > 0)
                UpdateXpAndLevel(startingXP, XpType.Admin);

            // Leave this for last as it could potentially block some actions during the reset process.
            if (setToLimboGameplayMode)
            {
                GameplayMode = GameplayModes.Limbo;
                SetProperty(PropertyBool.RecallsDisabled, true);
            }

            Session.Network.EnqueueSend(new GameEventPlayerDescription(Session));
            EnqueueBroadcast(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.PlayerKillerStatus, (int)PlayerKillerStatus), new GameMessagePublicUpdatePropertyInt(this, PropertyInt.PkLevelModifier, PkLevelModifier));
        }

        public void RevertToBrandNewCharacterEquipment(bool keepHousing)
        {
            // Destroy all items
            var inventory = GetAllPossessions();

            foreach (var item in inventory)
            {
                if (keepHousing && item.WeenieType != WeenieType.Deed) // Keep houses
                    item.DeleteObject(this);
            }

            if (ChargenClothing != null)
            {
                var chargenClothingList = ChargenClothing.Split("|");
                if (chargenClothingList.Length == 12)
                {
                    bool a = uint.TryParse(chargenClothingList[0], out var hatWcid);
                    bool b = uint.TryParse(chargenClothingList[1], out var hatPalette);
                    bool c = double.TryParse(chargenClothingList[2], out var hatShade);

                    bool d = uint.TryParse(chargenClothingList[3], out var shirtWcid);
                    bool e = uint.TryParse(chargenClothingList[4], out var shirtPalette);
                    bool f = double.TryParse(chargenClothingList[5], out var shirtShade);

                    bool g = uint.TryParse(chargenClothingList[6], out var pantsWcid);
                    bool h = uint.TryParse(chargenClothingList[7], out var pantsPalette);
                    bool i = double.TryParse(chargenClothingList[8], out var pantsShade);

                    bool j = uint.TryParse(chargenClothingList[9], out var footwearWcid);
                    bool k = uint.TryParse(chargenClothingList[10], out var footwearPalette);
                    bool l = double.TryParse(chargenClothingList[11], out var footwearShade);

                    if (a && b && c)
                    {
                        var hat = PlayerFactory.GetClothingObject(hatWcid, hatPalette, hatShade);
                        if (hat != null)
                            TryEquipObjectWithNetworking(hat, hat.ValidLocations ?? 0);
                    }

                    if (d && e && f)
                    {
                        var shirt = PlayerFactory.GetClothingObject(shirtWcid, shirtPalette, shirtShade);
                        if (shirt != null)
                            TryEquipObjectWithNetworking(shirt, shirt.ValidLocations ?? 0);
                    }

                    if (g && h && i)
                    {
                        var pants = PlayerFactory.GetClothingObject(pantsWcid, pantsPalette, pantsShade);
                        if (pants != null)
                            TryEquipObjectWithNetworking(pants, pants.ValidLocations ?? 0);
                    }

                    if (j && k && l)
                    {
                        var footwear = PlayerFactory.GetClothingObject(footwearWcid, footwearPalette, footwearShade);
                        if (footwear != null)
                            TryEquipObjectWithNetworking(footwear, footwear.ValidLocations ?? 0);
                    }
                }
            }

            PlayerFactory.GrantStarterItems(this);

            SendInventoryAndWieldedItems();
            UpdateCoinValue();
        }
    }
}
