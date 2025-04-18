using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Database;
using ACE.DatLoader;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects.Entity;

namespace ACE.Server.WorldObjects
{
    partial class Player
    {
        /// <summary>
        /// Handles the GameAction 0x46 - RaiseSkill network message from client
        /// </summary>
        public bool HandleActionRaiseSkill(Skill skill, uint amount)
        {
            var creatureSkill = GetCreatureSkill(skill, false);

            if (creatureSkill == null || creatureSkill.AdvancementClass < SkillAdvancementClass.Trained)
            {
                log.Error($"{Name}.HandleActionRaiseSkill({skill}, {amount}) - trained or specialized skill not found");
                Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, creatureSkill));
                return false;
            }

            if (amount > AvailableExperience)
            {
                //log.Error($"{Name}.HandleActionRaiseSkill({skill}, {amount}) - amount > AvailableExperience ({AvailableExperience})");
                Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, creatureSkill));
                return false;
            }

            var prevRank = creatureSkill.Ranks;

            if (!SpendSkillXp(creatureSkill, amount))
            {
                ChatPacket.SendServerMessage(Session, $"You do not have enough experience to raise your {skill.ToSentence()} skill.", ChatMessageType.Broadcast);
                Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, creatureSkill));
                return false;
            }

            Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, creatureSkill));

            if (prevRank != creatureSkill.Ranks)
            {
                // if the skill ranks out at the top of our xp chart
                // then we will start fireworks effects and have special text!
                var suffix = "";
                if (creatureSkill.IsMaxRank)
                {
                    // fireworks on rank up is 0x8D
                    PlayParticleEffect(PlayScript.WeddingBliss, Guid);
                    suffix = $" and has reached its upper limit";
                }

                var sound = new GameMessageSound(Guid, Sound.RaiseTrait);
                var msg = new GameMessageSystemChat($"Your base {skill.ToSentence()} skill is now {creatureSkill.Base}{suffix}!", ChatMessageType.Advancement);

                Session.Network.EnqueueSend(sound, msg);

                // retail was missing the 'raise skill' runrate hook here
                if (skill == Skill.Run && PropertyManager.GetBool("runrate_add_hooks").Item)
                    HandleRunRateUpdate();
            }

            return true;
        }

        private bool SpendSkillXp(CreatureSkill creatureSkill, uint amount, bool sendNetworkUpdate = true)
        {
            if(creatureSkill.IsSecondary)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You cannot raise your {creatureSkill.Skill.ToSentence()} skill directly as it's set as a secondary skill of your {creatureSkill.SecondaryTo.ToSentence()} skill.", ChatMessageType.Advancement));
                return true;
            }

            var skillXPTable = GetSkillXPTable(creatureSkill.AdvancementClass);
            if (skillXPTable == null)
            {
                log.Error($"{Name}.SpendSkillXp({creatureSkill.Skill}, {amount}) - player tried to raise {creatureSkill.AdvancementClass} skill");
                return false;
            }

            // ensure skill is not already max rank
            if (creatureSkill.IsMaxRank)
            {
                log.Error($"{Name}.SpendSkillXp({creatureSkill.Skill}, {amount}) - player tried to raise skill beyond max rank");
                return false;
            }

            // the client should already handle this naturally,
            // but ensure player can't spend xp beyond the max rank
            var amountToEnd = creatureSkill.ExperienceLeft;

            if (amount > amountToEnd)
            {
                //log.Error($"{Name}.SpendSkillXp({creatureSkill.Skill}, {amount}) - player tried to raise skill beyond {amountToEnd} experience");
                return false;   // returning error here, instead of setting amount to amountToEnd
            }

            // everything looks good at this point,
            // spend xp on skill
            if (!SpendXP(amount, sendNetworkUpdate))
            {
                log.Error($"{Name}.SpendSkillXp({creatureSkill.Skill}, {amount}) - SpendXP failed");
                return false;
            }

            creatureSkill.ExperienceSpent += amount;

            // calculate new rank
            creatureSkill.Ranks = (ushort)CalcSkillRank(creatureSkill.AdvancementClass, creatureSkill.ExperienceSpent);

            return true;
        }

        /// <summary>
        /// Handles the GameAction 0x47 - TrainSkill network message from client
        /// </summary>
        public bool HandleActionTrainSkill(Skill skill, int creditsSpent)
        {
            if (creditsSpent > AvailableSkillCredits)
            {
                log.Error($"{Name}.HandleActionTrainSkill({skill}, {creditsSpent}) - not enough skill credits ({AvailableSkillCredits})");
                return false;
            }

            // get the actual cost to train the skill.
            if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out var skillBase))
            {
                log.Error($"{Name}.HandleActionTrainSkill({skill}, {creditsSpent}) - couldn't find skill base");
                return false;
            }

            if (creditsSpent != skillBase.TrainedCost)
            {
                log.Error($"{Name}.HandleActionTrainSkill({skill}, {creditsSpent}) - client value differs from skillBase.TrainedCost({skillBase.TrainedCost})");
                return false;
            }

            // attempt to train the specified skill
            var success = TrainSkill(skill, creditsSpent);

            var availableSkillCredits = $"You now have {AvailableSkillCredits} credits available.";

            if (success)
            {
                var updateSkill = new GameMessagePrivateUpdateSkill(this, GetCreatureSkill(skill));
                var skillCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

                var msg = new GameMessageSystemChat($"{skill.ToSentence()} trained. {availableSkillCredits}", ChatMessageType.Advancement);

                Session.Network.EnqueueSend(updateSkill, skillCredits, msg);
            }
            else
                Session.Network.EnqueueSend(new GameMessageSystemChat($"Failed to train {skill.ToSentence()}! {availableSkillCredits}", ChatMessageType.Advancement));

            return success;
        }

        public bool TrainSkill(Skill skill)
        {
            // get the amount of skill credits required to train this skill
            if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out var skillBase))
            {
                log.Error($"{Name}.TrainSkill({skill}) - couldn't find skill base");
                return false;
            }

            // attempt to train the specified skill
            return TrainSkill(skill, skillBase.TrainedCost);
        }

        /// <summary>
        /// Sets the skill to trained status for a character
        /// </summary>
        public bool TrainSkill(Skill skill, int creditsSpent, bool applyCreationBonusXP = false)
        {
            var creatureSkill = GetCreatureSkill(skill);

            if (creatureSkill.AdvancementClass >= SkillAdvancementClass.Trained || creditsSpent > AvailableSkillCredits)
                return false;

            creatureSkill.AdvancementClass = SkillAdvancementClass.Trained;
            creatureSkill.Ranks = 0;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                creatureSkill.InitLevel = 5;
            }
            else
            {
                creatureSkill.InitLevel = 0;
                if (applyCreationBonusXP)
                {
                    creatureSkill.ExperienceSpent = 526;
                    creatureSkill.Ranks = 5;
                }
                else
                    creatureSkill.ExperienceSpent = 0;
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                UpdateCustomSkillFormulae(true);

            AvailableSkillCredits -= creditsSpent;

            // Tinkering skills can be reset at Asheron's Castle and Enlightenment, so if player has the augmentation when they train the skill again immediately specialize it again.
            if (IsSkillSpecializedViaAugmentation(skill, out var playerHasAugmentation) && playerHasAugmentation)
                SpecializeSkill(skill, 0, false);

            return true;
        }

        public bool SpecializeSkill(Skill skill, bool resetSkill = true)
        {
            // get the amount of skill credits required to upgrade this skill
            // from trained -> specialized
            if (!DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)skill, out var skillBase))
            {
                log.Error($"{Name}.SpecializeSkill({skill}, {resetSkill}) - couldn't find skill base");
                return false;
            }

            // attempt to specialize the specified skill
            return SpecializeSkill(skill, skillBase.UpgradeCostFromTrainedToSpecialized);
        }

        /// <summary>
        /// Sets the skill to specialized status
        /// </summary>
        /// <param name="resetSkill">only set to TRUE during character creation. set to FALSE during temple / asheron's castle</param>
        public bool SpecializeSkill(Skill skill, int creditsSpent, bool resetSkill = true)
        {
            var creatureSkill = GetCreatureSkill(skill);

            if (creatureSkill.AdvancementClass != SkillAdvancementClass.Trained || creditsSpent > AvailableSkillCredits)
                return false;

            creatureSkill.InitLevel = 10;
            creatureSkill.AdvancementClass = SkillAdvancementClass.Specialized;

            if (resetSkill)
            {
                // this path only during char creation
                creatureSkill.Ranks = 0;
                creatureSkill.ExperienceSpent = 0;
            }
            else
            {
                // this path only during temple / asheron's castle
                creatureSkill.Ranks = (ushort)CalcSkillRank(SkillAdvancementClass.Specialized, creatureSkill.ExperienceSpent);
            }

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                UpdateCustomSkillFormulae(true);

            if (creatureSkill.IsSecondary)
                creatureSkill.UpdateSecondarySkill();

            AvailableSkillCredits -= creditsSpent;

            return true;
        }

        /// <summary>
        /// Sets the skill to untrained status
        /// </summary>
        public bool UntrainSkill(Skill skill, int creditsSpent)
        {
            var creatureSkill = GetCreatureSkill(skill);

            if (creatureSkill == null || creatureSkill.AdvancementClass == SkillAdvancementClass.Specialized)
                return false;

            if (creatureSkill.AdvancementClass < SkillAdvancementClass.Trained)
            {
                // only used to initialize untrained skills for character creation?
                creatureSkill.AdvancementClass = SkillAdvancementClass.Untrained;       // should this always be Untrained? what about Inactive?
                creatureSkill.InitLevel = 0;
                creatureSkill.Ranks = 0;
                creatureSkill.ExperienceSpent = 0;
            }
            else
            {
                // refund xp and skill credits
                if (!creatureSkill.IsSecondary)
                {
                    RefundXP(creatureSkill.ExperienceSpent);

                    foreach (var entry in Skills)
                    {
                        if (entry.Value.SecondaryTo == creatureSkill.Skill)
                        {
                            entry.Value.SecondaryTo = Skill.None;
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {entry.Value.Skill.ToSentence()} skill is no longer set as a secondary skill!", ChatMessageType.WorldBroadcast));
                        }
                    }
                }
                else
                {
                    creatureSkill.SecondaryTo = Skill.None;
                    Session.Network.EnqueueSend(new GameMessageSystemChat($"Your {creatureSkill.Skill.ToSentence()} skill is no longer set as a secondary skill!", ChatMessageType.WorldBroadcast));
                }

                // temple untraining 'always trained' skills:
                // cannot be untrained, but skill XP can be recovered
                if (IsSkillUntrainable(skill, HeritageGroup))
                {
                    creatureSkill.AdvancementClass = SkillAdvancementClass.Untrained;
                    creatureSkill.InitLevel = 0;
                    AvailableSkillCredits += creditsSpent;
                }

                creatureSkill.Ranks = 0;
                creatureSkill.ExperienceSpent = 0;

                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    UpdateCustomSkillFormulae(true);
            }

            return true;
        }

        /// <summary>
        /// Lowers skill from Specialized to Trained and returns both skill credits and invested XP
        /// </summary>
        public bool UnspecializeSkill(Skill skill, int creditsSpent)
        {
            var creatureSkill = GetCreatureSkill(skill);

            if (creatureSkill == null || creatureSkill.AdvancementClass != SkillAdvancementClass.Specialized)
                return false;

            // refund xp and skill credits
            if (!creatureSkill.IsSecondary)
                RefundXP(creatureSkill.ExperienceSpent);

            // salvaging / tinkering skills specialized through augmentation only
            // cannot be unspecialized here, only refund xp
            if (!IsSkillSpecializedViaAugmentation(skill, out var playerHasAugmentation) || !playerHasAugmentation)
            {
                creatureSkill.AdvancementClass = SkillAdvancementClass.Trained;
                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                    creatureSkill.InitLevel = 5;
                else
                    creatureSkill.InitLevel = 0;
                AvailableSkillCredits += creditsSpent;
            }

            creatureSkill.Ranks = 0;
            creatureSkill.ExperienceSpent = 0;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                UpdateCustomSkillFormulae(true);

            if (creatureSkill.IsSecondary)
                creatureSkill.UpdateSecondarySkill();

            return true;
        }

        /// <summary>
        /// Increases a skill by some amount of points
        /// </summary>
        public void AwardSkillPoints(Skill skill, uint amount)
        {
            var creatureSkill = GetCreatureSkill(skill);

            for (var i = 0; i < amount; i++)
            {
                // get skill xp required for next rank
                var xpToNextRank = GetXpToNextRank(creatureSkill);

                if (xpToNextRank != null)
                    AwardSkillXP(skill, xpToNextRank.Value);
                else
                    return;
            }
        }

        /// <summary>
        /// Wrapper method used for increasing totalXP and then using the amount granted by HandleActionRaiseSkill
        /// </summary>
        public void AwardSkillXP(Skill skill, uint amount, bool alertPlayer = false)
        {
            var playerSkill = GetCreatureSkill(skill);

            if (playerSkill.AdvancementClass < SkillAdvancementClass.Trained || playerSkill.IsMaxRank)
                return;

            if (playerSkill.IsSecondary)
                return;

            amount = Math.Min(amount, playerSkill.ExperienceLeft);

            GrantXP(amount, XpType.Emote, ShareType.None);
            var raiseChain = new ActionChain();
            raiseChain.AddDelayForOneTick();
            raiseChain.AddAction(this, () =>
            {
                HandleActionRaiseSkill(skill, amount);
            });
            raiseChain.EnqueueChain();

            if (alertPlayer)
                Session.Network.EnqueueSend(new GameMessageSystemChat($"You've earned {amount:N0} experience in your {playerSkill.Skill.ToSentence()} skill.", ChatMessageType.Broadcast));
        }

        public void SpendAllAvailableSkillXp(CreatureSkill creatureSkill, bool sendNetworkUpdate = true)
        {
            var amountRemaining = creatureSkill.ExperienceLeft;

            if (amountRemaining > AvailableExperience)
                amountRemaining = (uint)AvailableExperience;

            SpendSkillXp(creatureSkill, amountRemaining, sendNetworkUpdate);
        }

        /// <summary>
        /// Grants skill XP proportional to the player's skill level
        /// </summary>
        public void GrantLevelProportionalSkillXP(Skill skill, double percent, long min, long max)
        {
            var creatureSkill = GetCreatureSkill(skill, false);
            if (creatureSkill == null || creatureSkill.IsMaxRank)
                return;

            var nextLevelXP = GetXPBetweenSkillLevels(creatureSkill.AdvancementClass, creatureSkill.Ranks, creatureSkill.Ranks + 1);
            if (nextLevelXP == null)
                return;

            var amount = (uint)Math.Round(nextLevelXP.Value * percent);

            if (max > 0 && max <= uint.MaxValue)
                amount = Math.Min(amount, (uint)max);

            amount = Math.Min(amount, creatureSkill.ExperienceLeft);

            if (min > 0)
                amount = Math.Max(amount, (uint)min);

            //Console.WriteLine($"{Name}.GrantLevelProportionalSkillXP({skill}, {percent}, {max:N0})");
            //Console.WriteLine($"Amount: {amount:N0}");

            AwardSkillXP(skill, amount, true);
        }

        /// <summary>
        /// Returns the remaining XP required to the next skill level
        /// </summary>
        public uint? GetXpToNextRank(CreatureSkill skill)
        {
            if (skill.AdvancementClass < SkillAdvancementClass.Trained || skill.IsMaxRank)
                return null;

            var skillXPTable = GetSkillXPTable(skill.AdvancementClass);

            return skillXPTable[skill.Ranks + 1] - skill.ExperienceSpent;
        }

        /// <summary>
        /// Returns the XP curve table based on trained or specialized skill
        /// </summary>
        public static List<uint> GetSkillXPTable(SkillAdvancementClass status)
        {
            var xpTable = DatManager.PortalDat.XpTable;

            switch (status)
            {
                case SkillAdvancementClass.Trained:
                    return xpTable.TrainedSkillXpList;

                case SkillAdvancementClass.Specialized:
                    return xpTable.SpecializedSkillXpList;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns the skill XP required to go between fromRank and toRank
        /// </summary>
        public ulong? GetXPBetweenSkillLevels(SkillAdvancementClass status, int fromRank, int toRank)
        {
            var skillXPTable = GetSkillXPTable(status);
            if (skillXPTable == null)
                return null;

            return skillXPTable[toRank] - skillXPTable[fromRank];
        }

        /// <summary>
        /// Returns the maximum rank that can be purchased with an xp amount
        /// </summary>
        /// <param name="sac">Trained or specialized skill</param>
        /// <param name="xpAmount">The amount of xp used to make the purchase</param>
        public static int CalcSkillRank(SkillAdvancementClass sac, uint xpAmount)
        {
            var rankXpTable = GetSkillXPTable(sac);
            for (var i = rankXpTable.Count - 1; i >= 0; i--)
            {
                var rankAmount = rankXpTable[i];
                if (xpAmount >= rankAmount)
                    return i;
            }
            return -1;
        }

        private const int magicSkillCheckMargin = 50;

        public bool CanReadScroll(Scroll scroll)
        {
            var power = (int)scroll.Spell.Power;

            // level 1/7/8 scrolls can be learned by anyone?
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM && (power < 50 || power >= 300)) return true;

            var playerSkill = GetCreatureSkill(scroll.Spell.School);

            var minSkill = power - magicSkillCheckMargin;

            return playerSkill.AdvancementClass >= SkillAdvancementClass.Trained && playerSkill.Current >= minSkill;
        }

        public void AddSkillCredits(int amount)
        {
            TotalSkillCredits += amount;
            AvailableSkillCredits += amount;

            Session.Network.EnqueueSend(new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0));

            if (amount > 1)
                SendTransientError($"You have been awarded {amount:N0} additional skill credits.");
            else
                SendTransientError("You have been awarded an additional skill credit.");
        }

        /// <summary>
        /// Called on player login
        /// If a player has any skills trained that require updates from ACE-World-16-Patches,
        /// ensure these updates are installed, and if they aren't, send a helpful message to player with instructions for installation
        /// </summary>
        public void HandleDBUpdates()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
            {
                // dirty fighting
                var dfSkill = GetCreatureSkill(Skill.DirtyFighting);
                if (dfSkill.AdvancementClass >= SkillAdvancementClass.Trained)
                {
                    foreach (var spellID in SpellExtensions.DirtyFightingSpells)
                    {
                        var spell = new Server.Entity.Spell(spellID);
                        if (spell.NotFound)
                        {
                            var actionChain = new ActionChain();
                            actionChain.AddDelaySeconds(3.0f);
                            actionChain.AddAction(this, () =>
                            {
                                Session.Network.EnqueueSend(new GameMessageSystemChat("To install Dirty Fighting, please apply the latest patches from https://github.com/ACEmulator/ACE-World-16PY-Patches", ChatMessageType.Broadcast));
                            });
                            actionChain.EnqueueChain();
                        }
                        break;  // performance improvement: only check first spell
                    }
                }

                // void magic
                var voidSkill = GetCreatureSkill(Skill.VoidMagic);
                if (voidSkill.AdvancementClass >= SkillAdvancementClass.Trained)
                {
                    foreach (var spellID in SpellExtensions.VoidMagicSpells)
                    {
                        var spell = new Server.Entity.Spell(spellID);
                        if (spell.NotFound)
                        {
                            var actionChain = new ActionChain();
                            actionChain.AddDelaySeconds(3.0f);
                            actionChain.AddAction(this, () =>
                            {
                                Session.Network.EnqueueSend(new GameMessageSystemChat("To install Void Magic, please apply the latest patches from https://github.com/ACEmulator/ACE-World-16PY-Patches", ChatMessageType.Broadcast));
                            });
                            actionChain.EnqueueChain();
                        }
                        break;  // performance improvement: only check first spell (measured 102ms to check 75 uncached void spells)
                    }
                }

                // summoning
                var summoning = GetCreatureSkill(Skill.Summoning);
                if (summoning.AdvancementClass >= SkillAdvancementClass.Trained)
                {
                    uint essenceWCID = 48878;
                    var weenie = DatabaseManager.World.GetCachedWeenie(essenceWCID);
                    if (weenie == null)
                    {
                        var actionChain = new ActionChain();
                        actionChain.AddDelaySeconds(3.0f);
                        actionChain.AddAction(this, () =>
                        {
                            Session.Network.EnqueueSend(new GameMessageSystemChat("To install Summoning, please apply the latest patches from https://github.com/ACEmulator/ACE-World-16PY-Patches", ChatMessageType.Broadcast));
                        });
                        actionChain.EnqueueChain();
                    }
                }
            }
        }

        public static HashSet<Skill> MeleeSkills = new HashSet<Skill>()
        {
            Skill.LightWeapons,
            Skill.HeavyWeapons,
            Skill.FinesseWeapons,
            Skill.DualWield,
            Skill.TwoHandedCombat,

            // legacy
            Skill.Axe,
            Skill.Dagger,
            Skill.Mace,
            Skill.Spear,
            Skill.Staff,
            Skill.Sword,
            Skill.UnarmedCombat
        };

        public static HashSet<Skill> MissileSkills = new HashSet<Skill>()
        {
            Skill.MissileWeapons,

            // legacy
            Skill.Bow,
            Skill.Crossbow,
            Skill.Sling,
            Skill.ThrownWeapon
        };

        public static HashSet<Skill> MagicSkills = new HashSet<Skill>()
        {
            Skill.CreatureEnchantment,
            Skill.ItemEnchantment,
            Skill.LifeMagic,
            Skill.VoidMagic,
            Skill.WarMagic
        };

        public static List<Skill> AlwaysTrained = new List<Skill>()
        {
            Skill.ArcaneLore,
            Skill.Jump,
            Skill.Loyalty,
            Skill.MagicDefense,
            Skill.Run,
            Skill.Salvaging
        };

        public static List<Skill> AugSpecSkills = new List<Skill>()
        {
            Skill.ArmorTinkering,
            Skill.ItemTinkering,
            Skill.MagicItemTinkering,
            Skill.WeaponTinkering,
            Skill.Salvaging
        };

        public static bool IsSkillUntrainable(Skill skill, HeritageGroup heritageGroup)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                return !AlwaysTrained.Contains(skill);
            else
            {
                if (AlwaysTrained.Contains(skill))
                    return false;

                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
                {
                    switch (heritageGroup)
                    {
                        case HeritageGroup.Aluvian:
                            if (skill == Skill.Dagger || skill == Skill.AssessPerson)
                                return false;
                            break;
                        case HeritageGroup.Gharundim:
                            if (skill == Skill.Staff || skill == Skill.ItemTinkering)
                                return false;
                            break;
                        case HeritageGroup.Sho:
                            if (skill == Skill.UnarmedCombat)
                                return false;
                            break;
                    }
                }
                return true;
            }
        }

        public bool IsSkillSpecializedViaAugmentation(Skill skill, out bool playerHasAugmentation)
        {
            playerHasAugmentation = false;

            switch (skill)
            {
                case Skill.ArmorTinkering:
                    playerHasAugmentation = AugmentationSpecializeArmorTinkering > 0;
                    break;

                case Skill.ItemTinkering:
                    playerHasAugmentation = AugmentationSpecializeItemTinkering > 0;
                    break;

                case Skill.MagicItemTinkering:
                    playerHasAugmentation = AugmentationSpecializeMagicItemTinkering > 0;
                    break;

                case Skill.WeaponTinkering:
                    playerHasAugmentation = AugmentationSpecializeWeaponTinkering > 0;
                    break;

                case Skill.Salvaging:
                    playerHasAugmentation = AugmentationSpecializeSalvaging > 0;
                    break;
            }

            return AugSpecSkills.Contains(skill);
        }

        public override bool GetHeritageBonus(WorldObject weapon)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                return false;

            if (weapon == null || !weapon.IsMasterable)
                return false;

            if (PropertyManager.GetBool("universal_masteries").Item)
            {
                // https://asheron.fandom.com/wiki/Spring_2014_Update
                // end of retail - universal masteries
                return true;
            }
            else
                return GetHeritageBonus(GetWeaponType(weapon));
        }

        public bool GetHeritageBonus(WeaponType weaponType)
        {
            switch (HeritageGroup)
            {
                case HeritageGroup.Aluvian:
                    if (weaponType == WeaponType.Dagger || weaponType == WeaponType.Bow)
                        return true;
                    break;
                case HeritageGroup.Gharundim:
                    if (weaponType == WeaponType.Staff || weaponType == WeaponType.Magic)
                        return true;
                    break;
                case HeritageGroup.Sho:
                    if (weaponType == WeaponType.Unarmed || weaponType == WeaponType.Bow)
                        return true;
                    break;
                case HeritageGroup.Viamontian:
                    if (weaponType == WeaponType.Sword || weaponType == WeaponType.Crossbow)
                        return true;
                    break;
                case HeritageGroup.Shadowbound: // umbraen
                case HeritageGroup.Penumbraen:
                    if (weaponType == WeaponType.Unarmed || weaponType == WeaponType.Crossbow)
                        return true;
                    break;
                case HeritageGroup.Gearknight:
                    if (weaponType == WeaponType.Mace || weaponType == WeaponType.Crossbow)
                        return true;
                    break;
                case HeritageGroup.Undead:
                    if (weaponType == WeaponType.Axe || weaponType == WeaponType.Thrown)
                        return true;
                    break;
                case HeritageGroup.Empyrean:
                    if (weaponType == WeaponType.Sword || weaponType == WeaponType.Magic)
                        return true;
                    break;
                case HeritageGroup.Tumerok:
                    if (weaponType == WeaponType.Spear || weaponType == WeaponType.Thrown)
                        return true;
                    break;
                case HeritageGroup.Lugian:
                    if (weaponType == WeaponType.Axe || weaponType == WeaponType.Thrown)
                        return true;
                    break;
                case HeritageGroup.Olthoi:
                case HeritageGroup.OlthoiAcid:
                    break;
            }
            return false;
        }

        /// <summary>
        /// If the WeaponType is missing from a weapon, tries to convert from WeaponSkill (for old data)
        /// </summary>
        public WeaponType GetWeaponType(WorldObject weapon)
        {
            if (weapon == null)
                return WeaponType.Undef;    // unarmed?

            if (weapon is Caster)
                return WeaponType.Magic;

            var weaponType = weapon.GetProperty(PropertyInt.WeaponType);
            if (weaponType != null)
                return (WeaponType)weaponType;

            var weaponSkill = weapon.GetProperty(PropertyInt.WeaponSkill);
            if (weaponSkill != null && SkillToWeaponType.TryGetValue((Skill)weaponSkill, out WeaponType converted))
                return converted;
            else
                return WeaponType.Undef;
        }

        public static Dictionary<Skill, WeaponType> SkillToWeaponType = new Dictionary<Skill, WeaponType>()
        {
            { Skill.UnarmedCombat, WeaponType.Unarmed },
            { Skill.Sword, WeaponType.Sword },
            { Skill.Axe, WeaponType.Axe },
            { Skill.Mace, WeaponType.Mace },
            { Skill.Spear, WeaponType.Spear },
            { Skill.Dagger, WeaponType.Dagger },
            { Skill.Staff, WeaponType.Staff },
            { Skill.Bow, WeaponType.Bow },
            { Skill.Crossbow, WeaponType.Crossbow },
            { Skill.ThrownWeapon, WeaponType.Thrown },
            { Skill.TwoHandedCombat, WeaponType.TwoHanded },
            { Skill.CreatureEnchantment, WeaponType.Magic },    // only for war/void?
            { Skill.ItemEnchantment, WeaponType.Magic },
            { Skill.LifeMagic, WeaponType.Magic },
            { Skill.WarMagic, WeaponType.Magic },
            { Skill.VoidMagic, WeaponType.Magic },
        };

        public void HandleSkillCreditRefund()
        {
            if (!(GetProperty(PropertyBool.UntrainedSkills) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your trained skills have been reset due to an error with skill credits.\nYou have received a refund for these skill credits and experience.", ChatMessageType.Broadcast));

                RemoveProperty(PropertyBool.UntrainedSkills);
            });
            actionChain.EnqueueChain();
        }

        public void HandleSkillSpecCreditRefund()
        {
            if (!(GetProperty(PropertyBool.UnspecializedSkills) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your specialized skills have been unspecialized due to an error with skill credits.\nYou have received a refund for these skill credits and experience.", ChatMessageType.Broadcast));

                RemoveProperty(PropertyBool.UnspecializedSkills);
            });
            actionChain.EnqueueChain();
        }

        public void HandleFreeSkillResetRenewal()
        {
            if (!(GetProperty(PropertyBool.FreeSkillResetRenewed) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your opportunity to change your skills is renewed! Visit Fianhe to reset your skills.", ChatMessageType.Magic));

                RemoveProperty(PropertyBool.FreeSkillResetRenewed);

                QuestManager.Erase("UsedFreeSkillReset");
            });
            actionChain.EnqueueChain();
        }

        public void HandleFreeAttributeResetRenewal()
        {
            if (!(GetProperty(PropertyBool.FreeAttributeResetRenewed) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                // Your opportunity to change your attributes is renewed! Visit Chafulumisa to reset your skills [sic attributes].
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your opportunity to change your attributes is renewed! Visit Chafulumisa to reset your attributes.", ChatMessageType.Magic));

                RemoveProperty(PropertyBool.FreeAttributeResetRenewed);

                QuestManager.Erase("UsedFreeAttributeReset");
            });
            actionChain.EnqueueChain();
        }

        public void HandleSkillTemplesReset()
        {
            if (!(GetProperty(PropertyBool.SkillTemplesTimerReset) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("The Temples of Forgetfulness and Enlightenment have had the timer for their use reset due to skill changes.", ChatMessageType.Magic));

                RemoveProperty(PropertyBool.SkillTemplesTimerReset);

                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
                {
                    QuestManager.Erase("ForgetfulnessGems1");
                    QuestManager.Erase("ForgetfulnessGems2");
                    QuestManager.Erase("ForgetfulnessGems3");
                    QuestManager.Erase("ForgetfulnessGems4");
                    QuestManager.Erase("Forgetfulness6days");
                    QuestManager.Erase("Forgetfulness13days");
                    QuestManager.Erase("Forgetfulness20days");
                }
                else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                {
                    QuestManager.Erase("AttributeLoweringGemPickedUp");
                    QuestManager.Erase("AttributeRaisingGemPickedUp");
                    QuestManager.Erase("SkillEnlightenmentGemPickedUp");
                    QuestManager.Erase("SkillForgetfulnessGemPickedUp");
                    QuestManager.Erase("SkillPrimaryGemPickedUp");
                    QuestManager.Erase("SkillSecondaryGemPickedUp");
                }
                else if (Common.ConfigManager.Config.Server.WorldRuleset <= Common.Ruleset.Infiltration)
                {
                    QuestManager.Erase("AttributeLoweringGemPickedUp");
                    QuestManager.Erase("AttributeRaisingGemPickedUp");
                    QuestManager.Erase("SkillAlterationGemPickedUp");
                }
            });
            actionChain.EnqueueChain();
        }

        public void HandleFreeMasteryResetRenewal()
        {
            if (!(GetProperty(PropertyBool.FreeMasteryResetRenewed) ?? false)) return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Your opportunity to change your Masteries is renewed!", ChatMessageType.Magic));

                RemoveProperty(PropertyBool.FreeMasteryResetRenewed);

                QuestManager.Erase("UsedFreeMeleeMasteryReset");
                QuestManager.Erase("UsedFreeRangedMasteryReset");
                QuestManager.Erase("UsedFreeSummoningMasteryReset");
            });
            actionChain.EnqueueChain();
        }

        public void HandleMigrateCharacterVersion1To2()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            var version = GetProperty(PropertyInt.Version) ?? 0;

            if (version != 1)
                return;

            var actionChain = new ActionChain();
            actionChain.AddDelaySeconds(5.0f);
            actionChain.AddAction(this, () =>
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Due to changes in skill formulae you're entitled to change your Focus to Self!", ChatMessageType.Magic));

                var failed = false;
                var amount = Math.Min(Focus.StartingValue / 10, 9);
                var list = new List<WorldObject>();
                for(int i = 0; i < amount; i++)
                {
                    var gem = WorldObjectFactory.CreateNewWorldObject(23058); // Focus to Self Gem
                    if (!TryCreateInInventoryWithNetworking(gem))
                    {
                        gem.Destroy(); // Clean up on creation failure
                        failed = true;
                        break;
                    }
                    else
                        list.Add(gem);
                }

                if (!failed)
                    SetProperty(PropertyInt.Version, 2);
                else
                {
                    foreach(var gem in list)
                    {
                        if (TryRemoveFromInventoryWithNetworking(gem.Guid, out var item, RemoveFromInventoryAction.ConsumeItem))
                            item.Destroy();
                    }
                    Session.Network.EnqueueSend(new GameMessageSystemChat("Failed to generate your gems! Please make sure you have enough space in your inventory and relog to try again.", ChatMessageType.Magic));
                }
            });
            actionChain.EnqueueChain();
        }

        public void HandleMigrateCharacterVersion2To3()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset != Common.Ruleset.CustomDM)
                return;

            var version = GetProperty(PropertyInt.Version) ?? 0;

            if (version != 2)
                return;

            var warMagic = GetCreatureSkill(Skill.WarMagic);

            if (warMagic.AdvancementClass == SkillAdvancementClass.Specialized)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Due to changes in War Magic skill credit costs you're entitled to extra skill credits!", ChatMessageType.Magic));
                AddSkillCredits(8);
            }
            else if (warMagic.AdvancementClass == SkillAdvancementClass.Trained)
            {
                Session.Network.EnqueueSend(new GameMessageSystemChat("Due to changes in War Magic skill credit costs you're entitled to extra skill credits!", ChatMessageType.Magic));
                AddSkillCredits(4);
            }

            SetProperty(PropertyInt.Version, 3);
        }

        /// <summary>
        /// Resets the skill, refunds all experience and skill credits, if allowed.
        /// </summary>
        public bool ResetSkill(Skill skill, bool refund = true, bool silent = false)
        {
            var creatureSkill = GetCreatureSkill(skill, false);

            if (creatureSkill == null || creatureSkill.AdvancementClass < SkillAdvancementClass.Trained)
                return false;

            // gather skill credits to refund
            DatManager.PortalDat.SkillTable.SkillBaseHash.TryGetValue((uint)creatureSkill.Skill, out var skillBase);

            if (skillBase == null)
                return false;

            // salvage / tinkering skills specialized via augmentations
            // Salvaging cannot be untrained or unspecialized => skillIsSpecializedViaAugmentation && !untrainable
            IsSkillSpecializedViaAugmentation(creatureSkill.Skill, out var skillIsSpecializedViaAugmentation);

            var typeOfSkill = creatureSkill.AdvancementClass.ToString().ToLower() + " ";
            var untrainable = IsSkillUntrainable(skill, HeritageGroup);
            var creditRefund = (creatureSkill.AdvancementClass == SkillAdvancementClass.Specialized && !(skillIsSpecializedViaAugmentation && !untrainable)) || untrainable;

            if (creatureSkill.AdvancementClass == SkillAdvancementClass.Specialized && !(skillIsSpecializedViaAugmentation && !untrainable))
            {
                creatureSkill.AdvancementClass = SkillAdvancementClass.Trained;
                creatureSkill.InitLevel = 0;
                if (!skillIsSpecializedViaAugmentation) // Tinkering skills can be unspecialized, but do not refund upgrade cost.
                    AvailableSkillCredits += skillBase.UpgradeCostFromTrainedToSpecialized;
            }

            // temple untraining 'always trained' skills:
            // cannot be untrained, but skill XP can be recovered
            if (untrainable)
            {
                creatureSkill.AdvancementClass = SkillAdvancementClass.Untrained;
                creatureSkill.InitLevel = 0;
                AvailableSkillCredits += skillBase.TrainedCost;
            }

            if (refund && creatureSkill.SecondaryTo == Skill.None)
                RefundXP(creatureSkill.ExperienceSpent);
            else if (creatureSkill.SecondaryTo != Skill.None)
                creatureSkill.SecondaryTo = Skill.None;

            creatureSkill.ExperienceSpent = 0;
            creatureSkill.Ranks = 0;

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                switch(creatureSkill.AdvancementClass)
                {
                    case SkillAdvancementClass.Specialized: creatureSkill.InitLevel = 10; break;
                    case SkillAdvancementClass.Trained: creatureSkill.InitLevel = 5; break;
                    default: creatureSkill.InitLevel = 0; break;
                }
                UpdateCustomSkillFormulae(true);
            }

            var updateSkill = new GameMessagePrivateUpdateSkill(this, creatureSkill);
            var availableSkillCredits = new GameMessagePrivateUpdatePropertyInt(this, PropertyInt.AvailableSkillCredits, AvailableSkillCredits ?? 0);

            if (!silent)
            {
                var msg = $"Your {(untrainable ? $"{typeOfSkill}" : "")}{skill.ToSentence()} skill has been {(untrainable ? "removed" : "reset")}. ";
                msg += $"All the experience {(creditRefund ? "and skill credits " : "")}that you spent on this skill have been refunded to you.";

                if (refund)
                    Session.Network.EnqueueSend(updateSkill, availableSkillCredits, new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
                else
                    Session.Network.EnqueueSend(updateSkill, new GameMessageSystemChat(msg, ChatMessageType.Broadcast));
            }
            else if (refund)
                Session.Network.EnqueueSend(updateSkill, availableSkillCredits);
            else
                Session.Network.EnqueueSend(updateSkill);

            return true;
        }

        public void UpdateCustomSkillFormulae(bool silent = false)
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                // Custom Arcane Lore formula needs some work arounds to work properly.
                // The way we do this for creatures and players differs.
                // For creatures we just override the Base and Current values to use the new formula.
                // For players we cannot do that as the client calculates the skill value independently and will display wrong values on the skill list.
                // To work around this we actually change the player's skill InitLevel.
                var skill = GetCreatureSkill(Skill.ArcaneLore);
                if (skill != null)
                {
                    var previous = skill.InitLevel;
                    skill.InitLevel = ((uint)Level / 2) + 10;

                    switch (skill.AdvancementClass)
                    {
                        case SkillAdvancementClass.Trained:
                            skill.InitLevel += 5;
                            break;
                        case SkillAdvancementClass.Specialized:
                            skill.InitLevel += 10;
                            break;
                    }

                    if (previous != skill.InitLevel && Session != null)
                    {
                        Session.Network.EnqueueSend(new GameMessagePrivateUpdateSkill(this, skill));
                        if(!silent)
                            Session.Network.EnqueueSend(new GameMessageSystemChat($"Your base {Skill.ArcaneLore.ToSentence()} skill is now {skill.Base}!", ChatMessageType.Advancement));
                    }
                }
            }
        }

        static Player()
        {
            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.Infiltration)
            {
                AlwaysTrained.Remove(Skill.ArcaneLore);

                PlayerSkills.Remove(Skill.TwoHandedCombat);
                PlayerSkills.Remove(Skill.HeavyWeapons);
                PlayerSkills.Remove(Skill.LightWeapons);
                PlayerSkills.Remove(Skill.FinesseWeapons);
                PlayerSkills.Remove(Skill.MissileWeapons);
                PlayerSkills.Remove(Skill.Shield);
                PlayerSkills.Remove(Skill.DualWield);
                PlayerSkills.Remove(Skill.Recklessness);
                PlayerSkills.Remove(Skill.SneakAttack);
                PlayerSkills.Remove(Skill.DirtyFighting);
                PlayerSkills.Remove(Skill.VoidMagic);
                PlayerSkills.Remove(Skill.Summoning);

                PlayerSkills.Add(Skill.Axe);
                PlayerSkills.Add(Skill.Bow);
                PlayerSkills.Add(Skill.Crossbow);
                PlayerSkills.Add(Skill.Dagger);
                PlayerSkills.Add(Skill.Mace);
                PlayerSkills.Add(Skill.Spear);
                PlayerSkills.Add(Skill.Staff);
                PlayerSkills.Add(Skill.Sword);
                PlayerSkills.Add(Skill.ThrownWeapon);
                PlayerSkills.Add(Skill.UnarmedCombat);
                PlayerSkills.Add(Skill.Salvaging);
            }
            else if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                AlwaysTrained.Remove(Skill.Salvaging);

                PlayerSkills.Remove(Skill.TwoHandedCombat);
                PlayerSkills.Remove(Skill.HeavyWeapons);
                PlayerSkills.Remove(Skill.LightWeapons);
                PlayerSkills.Remove(Skill.FinesseWeapons);
                PlayerSkills.Remove(Skill.MissileWeapons);
                PlayerSkills.Remove(Skill.DualWield);
                PlayerSkills.Remove(Skill.Recklessness);
                PlayerSkills.Remove(Skill.SneakAttack);
                PlayerSkills.Remove(Skill.DirtyFighting);
                PlayerSkills.Remove(Skill.VoidMagic);
                PlayerSkills.Remove(Skill.Summoning);

                PlayerSkills.Add(Skill.Axe);
                PlayerSkills.Add(Skill.Bow);
                PlayerSkills.Add(Skill.Crossbow);
                PlayerSkills.Add(Skill.Dagger);
                PlayerSkills.Add(Skill.Mace);
                PlayerSkills.Add(Skill.Spear);
                PlayerSkills.Add(Skill.Staff);
                PlayerSkills.Add(Skill.Sword);
                PlayerSkills.Add(Skill.ThrownWeapon);
                PlayerSkills.Add(Skill.UnarmedCombat);
                PlayerSkills.Add(Skill.Salvaging);
                PlayerSkills.Add(Skill.Awareness);
                PlayerSkills.Add(Skill.Appraise);
                PlayerSkills.Add(Skill.Armor);
                PlayerSkills.Add(Skill.Sneaking);

                PlayerSkills.Remove(Skill.AssessPerson);
                PlayerSkills.Remove(Skill.ItemEnchantment);
                PlayerSkills.Remove(Skill.CreatureEnchantment);
                PlayerSkills.Remove(Skill.Crossbow);
                PlayerSkills.Remove(Skill.Mace);
                PlayerSkills.Remove(Skill.Staff);

                PlayerSkills.Remove(Skill.WeaponTinkering);
                PlayerSkills.Remove(Skill.ArmorTinkering);
                PlayerSkills.Remove(Skill.MagicItemTinkering);
                PlayerSkills.Remove(Skill.ItemTinkering);

                NoLog_Landblocks.Add(0xB095); // Smuggler's Den
            }
        }

        /// <summary>
        /// All of the skills players have access to @ end of retail
        /// </summary>
        public static HashSet<Skill> PlayerSkills = new HashSet<Skill>()
        {
            Skill.MeleeDefense,
            Skill.MissileDefense,
            Skill.ArcaneLore,
            Skill.MagicDefense,
            Skill.ManaConversion,
            Skill.ItemTinkering,
            Skill.AssessPerson,
            Skill.Deception,
            Skill.Healing,
            Skill.Jump,
            Skill.Lockpick,
            Skill.Run,
            Skill.AssessCreature,
            Skill.WeaponTinkering,
            Skill.ArmorTinkering,
            Skill.MagicItemTinkering,
            Skill.CreatureEnchantment,
            Skill.ItemEnchantment,
            Skill.LifeMagic,
            Skill.WarMagic,
            Skill.Leadership,
            Skill.Loyalty,
            Skill.Fletching,
            Skill.Alchemy,
            Skill.Cooking,
            Skill.Salvaging,
            Skill.TwoHandedCombat,
            Skill.VoidMagic,
            Skill.HeavyWeapons,
            Skill.LightWeapons,
            Skill.FinesseWeapons,
            Skill.MissileWeapons,
            Skill.Shield,
            Skill.DualWield,
            Skill.Recklessness,
            Skill.SneakAttack,
            Skill.DirtyFighting,
            Skill.Summoning
        };
    }
}
