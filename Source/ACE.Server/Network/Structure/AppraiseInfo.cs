using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACE.Database;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.WorldObjects;

namespace ACE.Server.Network.Structure
{
    /// <summary>
    /// Handles calculating and sending all object appraisal info
    /// </summary>
    public class AppraiseInfo
    {
        private const uint EnchantmentMask = 0x80000000;

        public IdentifyResponseFlags Flags;

        public bool Success;    // assessment successful?

        public Dictionary<PropertyInt, int> PropertiesInt;
        public Dictionary<PropertyInt64, long> PropertiesInt64;
        public Dictionary<PropertyBool, bool> PropertiesBool;
        public Dictionary<PropertyFloat, double> PropertiesFloat;
        public Dictionary<PropertyString, string> PropertiesString;
        public Dictionary<PropertyDataId, uint> PropertiesDID;
        public Dictionary<PropertyInstanceId, uint> PropertiesIID;

        public List<uint> SpellBook;

        public ArmorProfile ArmorProfile;
        public CreatureProfile CreatureProfile;
        public WeaponProfile WeaponProfile;
        public HookProfile HookProfile;

        public ArmorMask ArmorHighlight;
        public ArmorMask ArmorColor;
        public WeaponMask WeaponHighlight;
        public WeaponMask WeaponColor;
        public ResistMask ResistHighlight;
        public ResistMask ResistColor;

        public ArmorLevel ArmorLevels;

        public bool IsArmorCapped = false;
        public bool IsArmorBuffed = false;

        // This helps ensure the item will identify properly. Some "items" are technically "Creatures".
        private bool NPCLooksLikeObject;

        public AppraiseInfo()
        {
            Flags = IdentifyResponseFlags.None;
            Success = false;
        }

        /// <summary>
        /// Construct all of the info required for appraising any WorldObject
        /// </summary>
        public AppraiseInfo(WorldObject wo, Player examiner, bool success = true)
        {
            BuildProfile(wo, examiner, success);
        }

        public void BuildProfile(WorldObject wo, Player examiner, bool success = true)
        {
            //Console.WriteLine("Appraise: " + wo.Guid);
            Success = success;

            BuildProperties(wo);
            BuildSpells(wo);

            // Help us make sure the item identify properly
            NPCLooksLikeObject = wo.GetProperty(PropertyBool.NpcLooksLikeObject) ?? false;

            if (PropertiesIID.ContainsKey(PropertyInstanceId.AllowedWielder) && !PropertiesBool.ContainsKey(PropertyBool.AppraisalHasAllowedWielder))
                PropertiesBool.Add(PropertyBool.AppraisalHasAllowedWielder, true);

            if (PropertiesIID.ContainsKey(PropertyInstanceId.AllowedActivator) && !PropertiesBool.ContainsKey(PropertyBool.AppraisalHasAllowedActivator))
                PropertiesBool.Add(PropertyBool.AppraisalHasAllowedActivator, true);

            if (PropertiesString.ContainsKey(PropertyString.ScribeAccount) && !examiner.IsAdmin && !examiner.IsSentinel && !examiner.IsEnvoy && !examiner.IsArch && !examiner.IsPsr)
                PropertiesString.Remove(PropertyString.ScribeAccount);

            if (PropertiesString.ContainsKey(PropertyString.HouseOwnerAccount) && !examiner.IsAdmin && !examiner.IsSentinel && !examiner.IsEnvoy && !examiner.IsArch && !examiner.IsPsr)
                PropertiesString.Remove(PropertyString.HouseOwnerAccount);

            if (PropertiesInt.ContainsKey(PropertyInt.Lifespan))
                PropertiesInt[PropertyInt.RemainingLifespan] = wo.GetRemainingLifespan();

            if (PropertiesInt.TryGetValue(PropertyInt.Faction1Bits, out var faction1Bits))
            {
                // hide any non-default factions, prevent client from displaying ???
                // this is only needed for non-standard faction creatures that use templates, to hide the ??? in the client
                var sendBits = faction1Bits & (int)FactionBits.ValidFactions;
                if (sendBits != faction1Bits)
                {
                    if (sendBits != 0)
                        PropertiesInt[PropertyInt.Faction1Bits] = sendBits;
                    else
                        PropertiesInt.Remove(PropertyInt.Faction1Bits);
                }
            }

            // armor / clothing / shield
            if (wo is Clothing || wo.IsShield)
                BuildArmor(wo);

            if (wo is Creature creature)
                BuildCreature(creature);

            if (wo.Damage != null && !(wo is Clothing) || wo is MeleeWeapon || wo is Missile || wo is MissileLauncher || wo is Ammunition || wo is Caster)
                BuildWeapon(wo);

            // TODO: Resolve this issue a better way?
            // Because of the way ACE handles default base values in recipe system (or rather the lack thereof)
            // we need to check the following weapon properties to see if they're below expected minimum and adjust accordingly
            // The issue is that the recipe system likely added 0.005 to 0 instead of 1, which is what *should* have happened.
            if (wo.WeaponMagicDefense.HasValue && wo.WeaponMagicDefense.Value > 0 && wo.WeaponMagicDefense.Value < 1 && ((wo.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) != 0)
                PropertiesFloat[PropertyFloat.WeaponMagicDefense] += 1;
            if (wo.WeaponMissileDefense.HasValue && wo.WeaponMissileDefense.Value > 0 && wo.WeaponMissileDefense.Value < 1 && ((wo.GetProperty(PropertyInt.ImbueStackingBits) ?? 0) & 1) != 0)
                PropertiesFloat[PropertyFloat.WeaponMissileDefense] += 1;

            // Mask real value of AbsorbMagicDamage and/or Add AbsorbMagicDamage for ImbuedEffectType.IgnoreSomeMagicProjectileDamage
            if (PropertiesFloat.ContainsKey(PropertyFloat.AbsorbMagicDamage) || wo.HasImbuedEffect(ImbuedEffectType.IgnoreSomeMagicProjectileDamage))
                PropertiesFloat[PropertyFloat.AbsorbMagicDamage] = 1;

            if (wo is PressurePlate)
            {
                if (PropertiesInt.ContainsKey(PropertyInt.ResistLockpick))
                    PropertiesInt.Remove(PropertyInt.ResistLockpick);

                if (PropertiesInt.ContainsKey(PropertyInt.Value))
                    PropertiesInt.Remove(PropertyInt.Value);

                if (PropertiesInt.ContainsKey(PropertyInt.EncumbranceVal))
                    PropertiesInt.Remove(PropertyInt.EncumbranceVal);

                PropertiesString.Add(PropertyString.ShortDesc, wo.Active ? "Status: Armed" : "Status: Disarmed");
            }
            else if (wo is Door || wo is Chest)
            {
                // If wo is not locked, do not send ResistLockpick value. If ResistLockpick is sent for unlocked objects, id panel shows bonus to Lockpick skill
                if (!wo.IsLocked && PropertiesInt.ContainsKey(PropertyInt.ResistLockpick))
                    PropertiesInt.Remove(PropertyInt.ResistLockpick);

                // If wo is locked, append skill check percent, as int, to properties for id panel display on chances of success
                if (wo.IsLocked)
                {
                    var resistLockpick = LockHelper.GetResistLockpick(wo);

                    if (resistLockpick != null)
                    {
                        PropertiesInt[PropertyInt.ResistLockpick] = (int)resistLockpick;

                        var pickSkill = examiner.Skills[Skill.Lockpick].Current;

                        var successChance = SkillCheck.GetSkillChance((int)pickSkill, (int)resistLockpick) * 100;

                        if (!PropertiesInt.ContainsKey(PropertyInt.AppraisalLockpickSuccessPercent))
                            PropertiesInt.Add(PropertyInt.AppraisalLockpickSuccessPercent, (int)successChance);
                    }
                }
                // if wo has DefaultLocked property and is unlocked, add that state to the property buckets
                else if (PropertiesBool.ContainsKey(PropertyBool.DefaultLocked))
                    PropertiesBool[PropertyBool.Locked] = false;
            }

            if (wo is Corpse)
            {
                PropertiesBool.Clear();
                PropertiesDID.Clear();
                PropertiesFloat.Clear();
                PropertiesInt64.Clear();

                var discardInts = PropertiesInt.Where(x => x.Key != PropertyInt.EncumbranceVal && x.Key != PropertyInt.Value).Select(x => x.Key).ToList();
                foreach (var key in discardInts)
                    PropertiesInt.Remove(key);
                var discardString = PropertiesString.Where(x => x.Key != PropertyString.LongDesc && x.Key != PropertyString.Use).Select(x => x.Key).ToList();
                foreach (var key in discardString)
                    PropertiesString.Remove(key);

                PropertiesInt[PropertyInt.Value] = 0;
            }

            if (wo is Portal)
            {
                if (PropertiesInt.ContainsKey(PropertyInt.EncumbranceVal))
                    PropertiesInt.Remove(PropertyInt.EncumbranceVal);
            }

            if (wo is SlumLord slumLord)
            {                
                PropertiesBool.Clear();
                PropertiesDID.Clear();
                PropertiesFloat.Clear();
                PropertiesIID.Clear();
                //PropertiesInt.Clear();
                PropertiesInt64.Clear();
                PropertiesString.Clear();

                var longDesc = "";

                if (slumLord.HouseOwner.HasValue && slumLord.HouseOwner.Value > 0)
                {
                    longDesc = $"The current maintenance has {(slumLord.IsRentPaid() || !PropertyManager.GetBool("house_rent_enabled").Item ? "" : "not ")}been paid.\n";

                    PropertiesInt.Clear();
                }
                else
                {
                    //longDesc = $"This house is {(slumLord.HouseStatus == HouseStatus.Disabled ? "not " : "")}available for purchase.\n"; // this was the retail msg.
                    longDesc = $"This {(slumLord.House.HouseType == HouseType.Undef ? "house" : slumLord.House.HouseType.ToString().ToLower())} is {(slumLord.House.HouseStatus == HouseStatus.Disabled ? "not " : "")}available for purchase.\n";

                    var discardInts = PropertiesInt.Where(x => x.Key != PropertyInt.HouseStatus && x.Key != PropertyInt.HouseType && x.Key != PropertyInt.MinLevel && x.Key != PropertyInt.MaxLevel && x.Key != PropertyInt.AllegianceMinLevel && x.Key != PropertyInt.AllegianceMaxLevel).Select(x => x.Key).ToList();
                    foreach (var key in discardInts)
                        PropertiesInt.Remove(key);
                }

                if (slumLord.HouseRequiresMonarch)
                    longDesc += "You must be a monarch to purchase and maintain this dwelling.\n";

                if (slumLord.AllegianceMinLevel.HasValue)
                {
                    var allegianceMinLevel = PropertyManager.GetLong("mansion_min_rank", -1).Item;
                    if (allegianceMinLevel == -1)
                        allegianceMinLevel = slumLord.AllegianceMinLevel.Value;

                    longDesc += $"Restricted to characters of allegiance rank {allegianceMinLevel} or greater.\n";
                }

                PropertiesString.Add(PropertyString.LongDesc, longDesc);
            }

            if (wo is Container)
            {
                if (PropertiesInt.ContainsKey(PropertyInt.Value))
                    PropertiesInt[PropertyInt.Value] = DatabaseManager.World.GetCachedWeenie(wo.WeenieClassId).GetValue() ?? 0; // Value is masked to base value of Weenie
            }

            if (wo is Storage)
            {
                var longDesc = "";

                if (wo.HouseOwner.HasValue && wo.HouseOwner.Value > 0)
                    longDesc = $"Owned by {wo.ParentLink.HouseOwnerName}\n";

                var discardString = PropertiesString.Where(x => x.Key != PropertyString.Use).Select(x => x.Key).ToList();
                foreach (var key in discardString)
                    PropertiesString.Remove(key);

                PropertiesString.Add(PropertyString.LongDesc, longDesc);
            }

            if (wo is Hook)
            {
                // If the hook has any inventory, we need to send THOSE properties instead.
                var hook = wo as Container;

                string baseDescString = "";
                if (wo.ParentLink.HouseOwner != null)
                {
                    // This is for backwards compatibility. This value was not set/saved in earlier versions.
                    // It will get the player's name and save that to the HouseOwnerName property of the house. This is now done when a player purchases a house.
                    if (wo.ParentLink.HouseOwnerName == null)
                    {
                        var houseOwnerPlayer = PlayerManager.FindByGuid((uint)wo.ParentLink.HouseOwner);
                        if (houseOwnerPlayer != null)
                        {
                            wo.ParentLink.HouseOwnerName = houseOwnerPlayer.Name;
                            wo.ParentLink.SaveBiotaToDatabase();
                        }
                    }
                    baseDescString = "This hook is owned by " + wo.ParentLink.HouseOwnerName + ". "; //if house is owned, display this text
                }

                var containsString = "";
                if (hook.Inventory.Count == 1)
                {
                    WorldObject hookedItem = hook.Inventory.First().Value;

                    // Hooked items have a custom "description", containing the desc of the sub item and who the owner of the house is (if any)
                    BuildProfile(hookedItem, examiner, success);

                    containsString = "It contains: \n";

                    if (!string.IsNullOrWhiteSpace(hookedItem.LongDesc))
                    {
                        containsString += hookedItem.LongDesc;
                    }
                    //else if (PropertiesString.ContainsKey(PropertyString.ShortDesc) && PropertiesString[PropertyString.ShortDesc] != null)
                    //{
                    //    containsString += PropertiesString[PropertyString.ShortDesc];
                    //}
                    else
                    {
                        containsString += hookedItem.Name;
                    }

                    BuildHookProfile(hookedItem);
                }

                //if (PropertiesString.ContainsKey(PropertyString.LongDesc) && PropertiesString[PropertyString.LongDesc] != null)
                //    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;
                ////else if (PropertiesString.ContainsKey(PropertyString.ShortDesc) && PropertiesString[PropertyString.ShortDesc] != null)
                ////    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;
                //else
                //    PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;

                PropertiesString[PropertyString.LongDesc] = baseDescString + containsString;

                PropertiesInt.Remove(PropertyInt.Structure);

                // retail should have removed this property and then server side built the same result for the hook longdesc replacement but didn't and ends up with some odd looking appraisals as seen on video/pcaps
                //PropertiesInt.Remove(PropertyInt.AppraisalLongDescDecoration);
            }

            if (wo is ManaStone)
            {
                var useMessage = "";

                if (wo.ItemCurMana.HasValue)
                    useMessage = "Use on a magic item to give the stone's stored Mana to that item.";
                else
                    useMessage = "Use on a magic item to destroy that item and drain its Mana.";

                PropertiesString[PropertyString.Use] = useMessage;
            }

            if (wo is CraftTool && (wo.ItemType == ItemType.TinkeringMaterial || wo.WeenieClassId >= 36619 && wo.WeenieClassId <= 36628 || wo.WeenieClassId >= 36634 && wo.WeenieClassId <= 36636))
            {
                if (PropertiesInt.ContainsKey(PropertyInt.Structure))
                    PropertiesInt.Remove(PropertyInt.Structure);
            }

            if (!Success)
            {
                // todo: what specifically to keep/what to clear

                //PropertiesBool.Clear();
                //PropertiesDID.Clear();
                //PropertiesFloat.Clear();
                //PropertiesIID.Clear();
                //PropertiesInt.Clear();
                //PropertiesInt64.Clear();
                //PropertiesString.Clear();

                if (PropertiesInt.ContainsKey(PropertyInt.Value))
                    PropertiesInt.Remove(PropertyInt.Value);
            }

            if (wo.ScribeIID == examiner.Guid.Full)
            {
                var realName = wo.ScribeName.Replace(" [HC]", "");
                PropertiesString[PropertyString.ScribeName] = realName + (examiner.IsHardcore ? " [HC]" : "");
            }

            BuildFlags();
        }

        private void BuildProperties(WorldObject wo)
        {
            PropertiesInt = wo.GetAllPropertyIntWhere(ClientProperties.PropertiesInt);
            PropertiesInt64 = wo.GetAllPropertyInt64Where(ClientProperties.PropertiesInt64);
            PropertiesBool = wo.GetAllPropertyBoolsWhere(ClientProperties.PropertiesBool);
            PropertiesFloat = wo.GetAllPropertyFloatWhere(ClientProperties.PropertiesDouble);
            PropertiesString = wo.GetAllPropertyStringWhere(ClientProperties.PropertiesString);
            PropertiesDID = wo.GetAllPropertyDataIdWhere(ClientProperties.PropertiesDataId);
            PropertiesIID = wo.GetAllPropertyInstanceIdWhere(ClientProperties.PropertiesInstanceId);

            if (wo is Player player)
            {
                // handle character options
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourDateOfBirth))
                    PropertiesString.Remove(PropertyString.DateOfBirth);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourAge))
                    PropertiesInt.Remove(PropertyInt.Age);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourChessRank))
                    PropertiesInt.Remove(PropertyInt.ChessRank);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourFishingSkill))
                    PropertiesInt.Remove(PropertyInt.FakeFishingSkill);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourNumberOfDeaths))
                    PropertiesInt.Remove(PropertyInt.NumDeaths);
                if (!player.GetCharacterOption(CharacterOption.AllowOthersToSeeYourNumberOfTitles))
                    PropertiesInt.Remove(PropertyInt.NumCharacterTitles);

                // handle dynamic properties for appraisal
                if (player.Allegiance != null && player.AllegianceNode != null)
                {
                    if (player.Allegiance.AllegianceName != null)
                        PropertiesString[PropertyString.AllegianceName] = player.Allegiance.AllegianceName;

                    if (player.AllegianceNode.IsMonarch)
                    {
                        PropertiesInt[PropertyInt.AllegianceFollowers] = player.AllegianceNode.TotalFollowers;
                    }
                    else
                    {
                        var monarch = player.Allegiance.Monarch;
                        var patron = player.AllegianceNode.Patron;

                        PropertiesString[PropertyString.MonarchsTitle] = AllegianceTitle.GetTitle((HeritageGroup)(monarch.Player.Heritage ?? 0), (Gender)(monarch.Player.Gender ?? 0), monarch.Rank) + " " + monarch.Player.Name;
                        PropertiesString[PropertyString.PatronsTitle] = AllegianceTitle.GetTitle((HeritageGroup)(patron.Player.Heritage ?? 0), (Gender)(patron.Player.Gender ?? 0), patron.Rank) + " " + patron.Player.Name;
                    }
                }

                if (player.Fellowship != null)
                    PropertiesString[PropertyString.Fellowship] = player.Fellowship.FellowshipName;
            }

            AddPropertyEnchantments(wo);
        }

        private void AddPropertyEnchantments(WorldObject wo)
        {
            if (wo == null) return;

            if (PropertiesInt.ContainsKey(PropertyInt.ArmorLevel))
            {
                PropertiesInt[PropertyInt.ArmorLevel] += wo.EnchantmentManager.GetArmorMod();

                if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
                {
                    var baseArmor = PropertiesInt[PropertyInt.ArmorLevel];

                    var wielder = wo.Wielder as Player;
                    if (wielder != null && ((wo.ClothingPriority ?? 0) & (CoverageMask)CoverageMaskHelper.Underwear) == 0)
                    {
                        int armor;
                        if (wo.IsShield)
                            armor = (int)wielder.GetSkillModifiedShieldLevel(baseArmor);
                        else
                            armor = (int)wielder.GetSkillModifiedArmorLevel(baseArmor);

                        if (armor < baseArmor)
                        {
                            PropertiesInt[PropertyInt.ArmorLevel] = armor;
                            IsArmorCapped = true;
                        }
                        else if (armor > baseArmor)
                        {
                            PropertiesInt[PropertyInt.ArmorLevel] = armor;
                            IsArmorBuffed = true;
                        }
                    }
                }
            }

            if (wo.ItemSkillLimit != null)
                PropertiesInt[PropertyInt.AppraisalItemSkill] = (int)wo.ItemSkillLimit;
            else
                PropertiesInt.Remove(PropertyInt.AppraisalItemSkill);

            if (PropertiesFloat.ContainsKey(PropertyFloat.WeaponDefense) && !(wo is Ammunition))
            {
                var defenseMod = wo.EnchantmentManager.GetDefenseMod();
                var auraDefenseMod = wo.Wielder != null && wo.IsEnchantable ? wo.Wielder.EnchantmentManager.GetDefenseMod() : 0.0f;

                PropertiesFloat[PropertyFloat.WeaponDefense] += defenseMod + auraDefenseMod;
            }

            if (PropertiesFloat.TryGetValue(PropertyFloat.ManaConversionMod, out var manaConvMod))
            {
                if (manaConvMod != 0)
                {
                    // hermetic link/void
                    var enchantmentMod = ResistMaskHelper.GetManaConversionMod(wo);

                    if (enchantmentMod != 1.0f)
                    {
                        PropertiesFloat[PropertyFloat.ManaConversionMod] *= enchantmentMod;

                        ResistHighlight = ResistMaskHelper.GetHighlightMask(wo);
                        ResistColor = ResistMaskHelper.GetColorMask(wo);
                    }
                }
                else if (!PropertyManager.GetBool("show_mana_conv_bonus_0").Item)
                {
                    PropertiesFloat.Remove(PropertyFloat.ManaConversionMod);
                }
            }

            if (PropertiesFloat.ContainsKey(PropertyFloat.ElementalDamageMod))
            {
                var enchantmentBonus = ResistMaskHelper.GetElementalDamageBonus(wo);

                if (enchantmentBonus != 0)
                {
                    PropertiesFloat[PropertyFloat.ElementalDamageMod] += enchantmentBonus;

                    ResistHighlight = ResistMaskHelper.GetHighlightMask(wo);
                    ResistColor = ResistMaskHelper.GetColorMask(wo);
                }
            }

            var appraisalLongDescDecoration = AppraisalLongDescDecorations.None;

            if (wo.ItemWorkmanship > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.PrependWorkmanship;
            if (wo.MaterialType > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.PrependMaterial;
            if (wo.GemType > 0 && wo.GemCount > 0)
                appraisalLongDescDecoration |= AppraisalLongDescDecorations.AppendGemInfo;

            if (appraisalLongDescDecoration > 0 && wo.LongDesc != null && wo.LongDesc.StartsWith(wo.Name))
                PropertiesInt[PropertyInt.AppraisalLongDescDecoration] = (int)appraisalLongDescDecoration;
            else
                PropertiesInt.Remove(PropertyInt.AppraisalLongDescDecoration);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.CustomDM)
            {
                if (wo.WeenieClassId == (uint)Factories.Enum.WeenieClassName.explorationContract)
                {
                    var player = wo.Container as Player ?? wo.Container?.Container as Player ?? wo.Wielder as Player;

                    if (player != null)
                    {
                        PropertiesString[PropertyString.LongDesc] = $"{player.Name}'s Current Assignments:";

                        var hasAssignments = false;
                        var assignment1Complete = false;
                        var assignment2Complete = false;
                        var assignment3Complete = false;
                        if (player.Exploration1LandblockId != 0 && player.Exploration1Description.Length > 0)
                        {
                            hasAssignments = true;
                            assignment1Complete = player.Exploration1LandblockReached && player.Exploration1KillProgressTracker <= 0 && player.Exploration1MarkerProgressTracker <= 0;
                        }
                        if (player.Exploration2LandblockId != 0 && player.Exploration2Description.Length > 0)
                        {
                            hasAssignments = true;
                            assignment2Complete = player.Exploration2LandblockReached && player.Exploration2KillProgressTracker <= 0 && player.Exploration2MarkerProgressTracker <= 0;
                        }
                        if (player.Exploration3LandblockId != 0 && player.Exploration3Description.Length > 0)
                        {
                            hasAssignments = true;
                            assignment3Complete = player.Exploration3LandblockReached && player.Exploration3KillProgressTracker <= 0 && player.Exploration3MarkerProgressTracker <= 0;
                        }

                        var msg1 = "";
                        var msg2 = "";
                        var msg3 = "";
                        if (player.Exploration1LandblockId != 0 && player.Exploration1Description.Length > 0)
                            msg1 = $"{player.Exploration1Description} {(assignment1Complete ? "\n    Complete!" : $"\n    Reached: {(player.Exploration1LandblockReached ? "Yes" : "No")}\n    Kills remaining: {player.Exploration1KillProgressTracker}\n    Markers remaining: {player.Exploration1MarkerProgressTracker}")}";
                        if (player.Exploration2LandblockId != 0 && player.Exploration2Description.Length > 0)
                            msg2 = $"{player.Exploration2Description} {(assignment2Complete ? "\n    Complete!" : $"\n    Reached: {(player.Exploration2LandblockReached ? "Yes" : "No")}\n    Kills remaining: {player.Exploration2KillProgressTracker}\n    Markers remaining: {player.Exploration2MarkerProgressTracker}")}";
                        if (player.Exploration3LandblockId != 0 && player.Exploration3Description.Length > 0)
                            msg3 = $"{player.Exploration3Description} {(assignment3Complete ? "\n    Complete!" : $"\n    Reached: {(player.Exploration3LandblockReached ? "Yes" : "No")}\n    Kills remaining: {player.Exploration3KillProgressTracker}\n    Markers remaining: {player.Exploration3MarkerProgressTracker}")}";

                        if (!hasAssignments)
                            PropertiesString[PropertyString.LongDesc] += " None";
                        else
                        {
                            var count = 0;
                            if (msg1.Length > 0)
                            {
                                count++;
                                PropertiesString[PropertyString.LongDesc] += $"\n\n{count:N0}. {msg1}";
                            }
                            if (msg2.Length > 0)
                            {
                                count++;
                                PropertiesString[PropertyString.LongDesc] += $"\n\n{count:N0}. {msg2}";
                            }
                            if (msg3.Length > 0)
                            {
                                count++;
                                PropertiesString[PropertyString.LongDesc] += $"\n\n{count:N0}. {msg3}";
                            }
                        }
                    }
                }

                string extraPropertiesText;
                if (PropertiesString.TryGetValue(PropertyString.Use, out var useText) && useText.Length > 0)
                    extraPropertiesText = $"{useText}\n\n";
                else
                    extraPropertiesText = "";

                bool hasExtraPropertiesText = false;
                if (wo.GameplayMode != GameplayModes.Regular && !(wo.Stuck && (int)(wo.ItemUseable ?? 0) < 2))
                {
                    if (wo.GameplayMode == GameplayModes.InitialMode)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"This item is useable by anyone.\n";
                        hasExtraPropertiesText = true;
                    }
                    else if (wo.GameplayMode >= GameplayModes.HardcorePK)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"This item is useable by hardcore PKs and less restrictive gameplay modes.\n";
                        hasExtraPropertiesText = true;
                    }
                    else if(wo.GameplayMode >= GameplayModes.HardcoreNPK)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"This item is useable by hardcore NPKs and less restrictive gameplay modes.\n";
                        hasExtraPropertiesText = true;
                    }
                    else if (wo.GameplayMode >= GameplayModes.SoloSelfFound)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        if (wo.GameplayModeExtraIdentifier == 0 || wo.GameplayModeIdentifierString == "")
                            extraPropertiesText += $"This item is useable by solo self-found and less restrictive gameplay modes.\n";
                        else
                            extraPropertiesText += $"This item is useable by {wo.GameplayModeIdentifierString} and less restrictive gameplay modes.\n";
                        hasExtraPropertiesText = true;
                    }
                }

                if(wo.WeenieType == WeenieType.MeleeWeapon && wo.IsLightWeapon)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"This weapon feels light enough to dual wield.\n";
                    hasExtraPropertiesText = true;
                }

                if (wo.ArmorLevel != null && wo.ArmorLevel != 0 && wo.CurrentWieldedLocation != null)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Base Armor Level: {wo.ArmorLevel}.";
                    hasExtraPropertiesText = true;
                }

                if (wo.IsShield)
                {
                    var BlockModBase = (float)(wo.BlockMod ?? 1);
                    var BlockModEnchanted = BlockModBase + wo.EnchantmentManager.GetBlockMod();
                    var BlockModPercent = BlockModEnchanted * 100;
                    var buffOrDebuff = 0;
                    if (BlockModEnchanted > BlockModBase)
                        buffOrDebuff = 1;
                    else if (BlockModEnchanted < BlockModBase)
                        buffOrDebuff = -1;

                    if (BlockModPercent != 0)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"Bonus to Block Chance: {(BlockModPercent > 0 ? "+" : "")}{BlockModPercent.ToString("0.0")}%. {(buffOrDebuff > 0 ? "(Buffed)" : "")}{(buffOrDebuff < 0 ? "(Debuffed)" : "")}";
                        hasExtraPropertiesText = true;
                    }
                }

                bool hasMissileDefenseCap = PropertiesFloat.TryGetValue(PropertyFloat.MissileDefenseCap, out var missileDefenseCap);
                bool combinedMeleeMissileCap = false;
                if (PropertiesFloat.TryGetValue(PropertyFloat.MeleeDefenseCap, out var meleeDefenseCap) && meleeDefenseCap != 0)
                {
                    if (hasMissileDefenseCap && missileDefenseCap == meleeDefenseCap)
                    {
                        // We have both melee and missile entries and they are the same value, group them up into "Evasion"
                        combinedMeleeMissileCap = true;
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"Max Evasion Chance: {(meleeDefenseCap > 0 ? "+" : "")}{meleeDefenseCap.ToString("0.0")}%.";
                        hasExtraPropertiesText = true;
                    }
                    else
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"Max Melee Evasion Chance: {(meleeDefenseCap > 0 ? "+" : "")}{meleeDefenseCap.ToString("0.0")}%.";
                        hasExtraPropertiesText = true;
                    }
                }

                if (!combinedMeleeMissileCap && hasMissileDefenseCap && missileDefenseCap != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Max Missile Evasion Chance: {(missileDefenseCap > 0 ? "+" : "")}{missileDefenseCap.ToString("0.0")}%.";
                    hasExtraPropertiesText = true;
                }

                if (PropertiesFloat.TryGetValue(PropertyFloat.MagicDefenseCap, out var magicDefenseCap) && magicDefenseCap != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Max Magic Resistance Chance: {(magicDefenseCap > 0 ? "+" : "")}{magicDefenseCap.ToString("0.0")}%.";
                    hasExtraPropertiesText = true;
                }

                if (PropertiesFloat.TryGetValue(PropertyFloat.ComponentBurnRateMod, out var componentBurnRateMod) && componentBurnRateMod != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Bonus to Component Burn Rate: {(componentBurnRateMod > 0 ? "+" : "")}{(componentBurnRateMod * 100).ToString("0.0")}%.";
                    hasExtraPropertiesText = true;
                }

                var maxTinkers = wo.GetMaxTinkerCount();
                if (maxTinkers > 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Tinkering Count: {wo.NumTimesTinkered}/{maxTinkers}.\n";
                    extraPropertiesText += $"Minimum Salvage Workmanship: {wo.GetMinSalvageQualityForTinkering()}.";
                    hasExtraPropertiesText = true;
                }

                if (wo.TinkerLog != null)
                {
                    var tinkers = wo.TinkerLog.Split(",");
                    if (tinkers.Length > 0)
                    {
                        if (hasExtraPropertiesText)
                            extraPropertiesText += "\n";
                        extraPropertiesText += $"Tinkered with: ";

                        bool first = true;
                        foreach (var tinker in tinkers)
                        {
                            if (!first)
                                extraPropertiesText += ", ";
                            if (int.TryParse(tinker, out var materialId))
                            {
                                extraPropertiesText += RecipeManager.GetMaterialName((MaterialType)materialId);
                                first = false;
                            }
                        }
                        extraPropertiesText += $".";

                        hasExtraPropertiesText = true;
                    }
                }

                if (wo.CanHaveExtraSpells())
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";
                    extraPropertiesText += $"Extra Spells Count: {wo.ExtraSpellsCount ?? 0}/{wo.GetMaxExtraSpellsCount()}.";
                    hasExtraPropertiesText = true;
                }

                if ((wo.ExtraHealthRegenPool ?? 0) != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";

                    var verb = "Adds";
                    var complement = "to";
                    if (wo.ExtraHealthRegenPool < 0)
                    {
                        verb = "Removes";
                        complement = "from";
                    }
                    extraPropertiesText += $"{verb} {wo.ExtraHealthRegenPool:N0} {complement} your Extra Health Regeneration pool when consumed.";
                    hasExtraPropertiesText = true;
                }

                if ((wo.ExtraStaminaRegenPool ?? 0) != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";

                    var verb = "Adds";
                    var complement = "to";
                    if (wo.ExtraStaminaRegenPool < 0)
                    {
                        verb = "Removes";
                        complement = "from";
                    }
                    extraPropertiesText += $"{verb} {wo.ExtraStaminaRegenPool:N0} {complement} your Extra Stamina Regeneration pool when consumed.";
                    hasExtraPropertiesText = true;
                }

                if ((wo.ExtraManaRegenPool ?? 0) != 0)
                {
                    if (hasExtraPropertiesText)
                        extraPropertiesText += "\n";

                    var verb = "Adds";
                    var complement = "to";
                    if (wo.ExtraManaRegenPool < 0)
                    {
                        verb = "Removes";
                        complement = "from";
                    }
                    extraPropertiesText += $"{verb} {wo.ExtraManaRegenPool:N0} {complement} your Extra Mana Regeneration pool when consumed.";
                    hasExtraPropertiesText = true;
                }

                if (wo is Corpse corpse && !corpse.IsMonster)
                    PropertiesString[PropertyString.LongDesc] += $"\n\nContains {corpse.VitaeCpPool} Vitae.\n";

                if (wo.ImbuedEffect == ImbuedEffectType.ElementalRending)
                    PropertiesInt[PropertyInt.ImbuedEffect] = (int)ImbuedEffectType.NetherRending; // The client has been modified to read "Elem. Rending" instead.

                if (hasExtraPropertiesText)
                    PropertiesString[PropertyString.Use] = extraPropertiesText.TrimEnd('\n');
            }
        }

        private void BuildSpells(WorldObject wo)
        {
            SpellBook = new List<uint>();

            if (wo is Creature)
                return;

            // add primary spell, if exists
            if (wo.SpellDID.HasValue)
                SpellBook.Add(wo.SpellDID.Value);

            // add proc spell, if exists
            if (wo.ProcSpell.HasValue)
                SpellBook.Add(wo.ProcSpell.Value);

            var woSpellDID = wo.SpellDID;   // prevent recursive lock
            var woProcSpell = wo.ProcSpell;

            foreach (var spellId in wo.Biota.GetKnownSpellsIdsWhere(i => i != woSpellDID && i != woProcSpell, wo.BiotaDatabaseLock))
                SpellBook.Add((uint)spellId);
        }

        private void AddEnchantments(WorldObject wo)
        {
            if (wo == null) return;

            // get all currently active item enchantments on the item
            var woEnchantments = wo.EnchantmentManager.GetEnchantments(MagicSchool.ItemEnchantment);

            foreach (var enchantment in woEnchantments)
                SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);

            // show auras from wielder, if applicable

            // this technically wasn't a feature in retail

            if (wo.Wielder != null && wo.IsEnchantable && wo.WeenieType != WeenieType.Clothing && !wo.IsShield && PropertyManager.GetBool("show_aura_buff").Item)
            {
                // get all currently active item enchantment auras on the player
                var wielderEnchantments = wo.Wielder.EnchantmentManager.GetEnchantments(MagicSchool.ItemEnchantment);

                // Only show reflected Auras from player appropriate for wielded weapons
                foreach (var enchantment in wielderEnchantments)
                {
                    if (wo is Caster)
                    {
                        // Caster weapon only item Auras
                        if ((enchantment.SpellCategory == SpellCategory.DefenseModRaising)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.ManaConversionModRaising)
                            || (enchantment.SpellCategory == SpellCategory.SpellDamageRaising))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                    else if (wo is Missile || wo is Ammunition)
                    {
                        if ((enchantment.SpellCategory == SpellCategory.DamageRaising)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaisingRare))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                    else
                    {
                        // Other weapon type Auras
                        if ((enchantment.SpellCategory == SpellCategory.AttackModRaising)
                            || (enchantment.SpellCategory == SpellCategory.AttackModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaising)
                            || (enchantment.SpellCategory == SpellCategory.DamageRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaising)
                            || (enchantment.SpellCategory == SpellCategory.DefenseModRaisingRare)
                            || (enchantment.SpellCategory == SpellCategory.WeaponTimeRaising)
                            || (enchantment.SpellCategory == SpellCategory.WeaponTimeRaisingRare))
                        {
                            SpellBook.Add((uint)enchantment.SpellId | EnchantmentMask);
                        }
                    }
                }
            }
        }

        private void BuildArmor(WorldObject wo)
        {
            if (!Success)
                return;

            ArmorProfile = new ArmorProfile(wo);
            ArmorHighlight = ArmorMaskHelper.GetHighlightMask(wo, IsArmorCapped || IsArmorBuffed);
            ArmorColor = ArmorMaskHelper.GetColorMask(wo, IsArmorBuffed);

            AddEnchantments(wo);
        }

        private void BuildCreature(Creature creature)
        {
            CreatureProfile = new CreatureProfile(creature, Success);

            // only creatures?
            ResistHighlight = ResistMaskHelper.GetHighlightMask(creature);
            ResistColor = ResistMaskHelper.GetColorMask(creature);

            if (Common.ConfigManager.Config.Server.WorldRuleset == Common.Ruleset.EoR)
            {
	            if (Success && (creature is Player || !creature.Attackable))
	                ArmorLevels = new ArmorLevel(creature);
	
	            AddRatings(creature);
        	}

            if (NPCLooksLikeObject)
            {
                var weenie = creature.Weenie ?? DatabaseManager.World.GetCachedWeenie(creature.WeenieClassId);

                if (!weenie.GetProperty(PropertyInt.EncumbranceVal).HasValue)
                    PropertiesInt.Remove(PropertyInt.EncumbranceVal);
            }
            else
                PropertiesInt.Remove(PropertyInt.EncumbranceVal);

            // see notes in CombatPet.Init()
            if (creature is CombatPet && PropertiesInt.ContainsKey(PropertyInt.Faction1Bits))
                PropertiesInt.Remove(PropertyInt.Faction1Bits);
        }

        private void AddRatings(Creature creature)
        {
            if (!Success)
                return;

            var damageRating = creature.GetDamageRating();

            // include heritage / weapon type rating?
            var weapon = creature.GetEquippedWeapon() ?? creature.GetEquippedWand();
            if (creature.GetHeritageBonus(weapon))
                damageRating += 5;

            // factor in weakness here?

            var damageResistRating = creature.GetDamageResistRating();

            // factor in nether dot damage here?

            var critRating = creature.GetCritRating();
            var critDamageRating = creature.GetCritDamageRating();

            var critResistRating = creature.GetCritResistRating();
            var critDamageResistRating = creature.GetCritDamageResistRating();

            var healingBoostRating = creature.GetHealingBoostRating();
            var dotResistRating = creature.GetDotResistanceRating();
            var netherResistRating = creature.GetNetherResistRating();

            var lifeResistRating = creature.GetLifeResistRating();  // drain / harm resistance
            var gearMaxHealth = creature.GetGearMaxHealth();

            var pkDamageRating = creature.GetPKDamageRating();
            var pkDamageResistRating = creature.GetPKDamageResistRating();

            if (damageRating != 0)
                PropertiesInt[PropertyInt.DamageRating] = damageRating;
            if (damageResistRating != 0)
                PropertiesInt[PropertyInt.DamageResistRating] = damageResistRating;

            if (critRating != 0)
                PropertiesInt[PropertyInt.CritRating] = critRating;
            if (critDamageRating != 0)
                PropertiesInt[PropertyInt.CritDamageRating] = critDamageRating;

            if (critResistRating != 0)
                PropertiesInt[PropertyInt.CritResistRating] = critResistRating;
            if (critDamageResistRating != 0)
                PropertiesInt[PropertyInt.CritDamageResistRating] = critDamageResistRating;

            if (healingBoostRating != 0)
                PropertiesInt[PropertyInt.HealingBoostRating] = healingBoostRating;
            if (netherResistRating != 0)
                PropertiesInt[PropertyInt.NetherResistRating] = netherResistRating;
            if (dotResistRating != 0)
                PropertiesInt[PropertyInt.DotResistRating] = dotResistRating;

            if (lifeResistRating != 0)
                PropertiesInt[PropertyInt.LifeResistRating] = lifeResistRating;
            if (gearMaxHealth != 0)
                PropertiesInt[PropertyInt.GearMaxHealth] = gearMaxHealth;

            if (pkDamageRating != 0)
                PropertiesInt[PropertyInt.PKDamageRating] = pkDamageRating;
            if (pkDamageResistRating != 0)
                PropertiesInt[PropertyInt.PKDamageResistRating] = pkDamageResistRating;

            // add ratings from equipped items?
        }

        private void BuildWeapon(WorldObject weapon)
        {
            if (!Success)
                return;

            var weaponProfile = new WeaponProfile(weapon);

            //WeaponHighlight = WeaponMaskHelper.GetHighlightMask(weapon, wielder);
            //WeaponColor = WeaponMaskHelper.GetColorMask(weapon, wielder);
            WeaponHighlight = WeaponMaskHelper.GetHighlightMask(weaponProfile);
            WeaponColor = WeaponMaskHelper.GetColorMask(weaponProfile);

            if (!(weapon is Caster))
                WeaponProfile = weaponProfile;

            // item enchantments can also be on wielder currently
            AddEnchantments(weapon);
        }

        private void BuildHookProfile(WorldObject hookedItem)
        {
            HookProfile = new HookProfile();
            if (hookedItem.Inscribable)
                HookProfile.Flags |= HookFlags.Inscribable;
            if (hookedItem is Healer)
                HookProfile.Flags |= HookFlags.IsHealer;
            if (hookedItem is Food)
                HookProfile.Flags |= HookFlags.IsFood;
            if (hookedItem is Lockpick)
                HookProfile.Flags |= HookFlags.IsLockpick;
            if (hookedItem.ValidLocations != null)
                HookProfile.ValidLocations = hookedItem.ValidLocations.Value;
            if (hookedItem.AmmoType != null)
                HookProfile.AmmoType = hookedItem.AmmoType.Value;
        }

        /// <summary>
        /// Constructs the bitflags for appraising a WorldObject
        /// </summary>
        private void BuildFlags()
        {
            if (PropertiesInt.Count > 0)
                Flags |= IdentifyResponseFlags.IntStatsTable;
            if (PropertiesInt64.Count > 0)
                Flags |= IdentifyResponseFlags.Int64StatsTable;         				
			if (PropertiesBool.Count > 0)
                Flags |= IdentifyResponseFlags.BoolStatsTable;
            if (PropertiesFloat.Count > 0)
                Flags |= IdentifyResponseFlags.FloatStatsTable;
            if (PropertiesString.Count > 0)
                Flags |= IdentifyResponseFlags.StringStatsTable;
            if (PropertiesDID.Count > 0)
                Flags |= IdentifyResponseFlags.DidStatsTable;
            if (SpellBook.Count > 0)
                Flags |= IdentifyResponseFlags.SpellBook;

            if (ResistHighlight != 0)
                Flags |= IdentifyResponseFlags.ResistEnchantmentBitfield;
            if (ArmorProfile != null)
                Flags |= IdentifyResponseFlags.ArmorProfile;
            if (CreatureProfile != null && !NPCLooksLikeObject)
                Flags |= IdentifyResponseFlags.CreatureProfile;
            if (WeaponProfile != null)
                Flags |= IdentifyResponseFlags.WeaponProfile;
            if (HookProfile != null)
                Flags |= IdentifyResponseFlags.HookProfile;
            if (ArmorHighlight != 0)
                Flags |= IdentifyResponseFlags.ArmorEnchantmentBitfield;
            if (WeaponHighlight != 0)
                Flags |= IdentifyResponseFlags.WeaponEnchantmentBitfield;
            if (ArmorLevels != null)
                Flags |= IdentifyResponseFlags.ArmorLevels;
        }
    }

    public static class AppraiseInfoExtensions
    {
        /// <summary>
        /// Writes the AppraiseInfo to the network stream
        /// </summary>
        public static void Write(this BinaryWriter writer, AppraiseInfo info)
        {
            writer.Write((uint)info.Flags);
            writer.Write(Convert.ToUInt32(info.Success));
            if (info.Flags.HasFlag(IdentifyResponseFlags.IntStatsTable))
                writer.Write(info.PropertiesInt);
            if (info.Flags.HasFlag(IdentifyResponseFlags.Int64StatsTable))
                writer.Write(info.PropertiesInt64);
            if (info.Flags.HasFlag(IdentifyResponseFlags.BoolStatsTable))
                writer.Write(info.PropertiesBool);
            if (info.Flags.HasFlag(IdentifyResponseFlags.FloatStatsTable))
                writer.Write(info.PropertiesFloat);
            if (info.Flags.HasFlag(IdentifyResponseFlags.StringStatsTable))
                writer.Write(info.PropertiesString);
            if (info.Flags.HasFlag(IdentifyResponseFlags.DidStatsTable))
                writer.Write(info.PropertiesDID);
            if (info.Flags.HasFlag(IdentifyResponseFlags.SpellBook))
                writer.Write(info.SpellBook);
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorProfile))
                writer.Write(info.ArmorProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.CreatureProfile))
                writer.Write(info.CreatureProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponProfile))
                writer.Write(info.WeaponProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.HookProfile))
                writer.Write(info.HookProfile);
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorEnchantmentBitfield))
            {
                writer.Write((ushort)info.ArmorHighlight);
                writer.Write((ushort)info.ArmorColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.WeaponEnchantmentBitfield))
            {
                writer.Write((ushort)info.WeaponHighlight);
                writer.Write((ushort)info.WeaponColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.ResistEnchantmentBitfield))
            {
                writer.Write((ushort)info.ResistHighlight);
                writer.Write((ushort)info.ResistColor);
            }
            if (info.Flags.HasFlag(IdentifyResponseFlags.ArmorLevels))
                writer.Write(info.ArmorLevels);
        }

        private static readonly PropertyIntComparer PropertyIntComparer = new PropertyIntComparer(16);
        private static readonly PropertyInt64Comparer PropertyInt64Comparer = new PropertyInt64Comparer(8);
        private static readonly PropertyBoolComparer PropertyBoolComparer = new PropertyBoolComparer(8);
        private static readonly PropertyFloatComparer PropertyFloatComparer = new PropertyFloatComparer(8);
        private static readonly PropertyStringComparer PropertyStringComparer = new PropertyStringComparer(8);
        private static readonly PropertyDataIdComparer PropertyDataIdComparer = new PropertyDataIdComparer(8);

        // TODO: generics
        public static void Write(this BinaryWriter writer, Dictionary<PropertyInt, int> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyIntComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyInt, int>(_properties, PropertyIntComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyInt64, long> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyInt64Comparer.NumBuckets);

            var properties = new SortedDictionary<PropertyInt64, long>(_properties, PropertyInt64Comparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyBool, bool> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyBoolComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyBool, bool>(_properties, PropertyBoolComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(Convert.ToUInt32(kvp.Value));
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyFloat, double> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyFloatComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyFloat, double>(_properties, PropertyFloatComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyString, string> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyStringComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyString, string>(_properties, PropertyStringComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.WriteString16L(kvp.Value);
            }
        }

        public static void Write(this BinaryWriter writer, Dictionary<PropertyDataId, uint> _properties)
        {
            PackableHashTable.WriteHeader(writer, _properties.Count, PropertyDataIdComparer.NumBuckets);

            var properties = new SortedDictionary<PropertyDataId, uint>(_properties, PropertyDataIdComparer);

            foreach (var kvp in properties)
            {
                writer.Write((uint)kvp.Key);
                writer.Write(kvp.Value);
            }
        }
    }
}
