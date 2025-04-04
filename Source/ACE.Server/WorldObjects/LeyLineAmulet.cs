using System;
using System.Collections.Generic;

using log4net;

using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;
using ACE.Entity.Models;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Entity;
using ACE.Common;
using ACE.Server.Factories.Tables;

namespace ACE.Server.WorldObjects
{
    public enum LeyLineEffect
    {
        None,
        ExtraSpellIntensity,
        LessManaUsage,
        MirrorSpell,
        CastExtraSpellOther,
        CastExtraSpellSelf,
        LowerFizzleChance,
        LowerCompBurnChanceAllSpells,
        LowerResistChance,
        GrantCastableSpell,
        GrantCantrip,

        LeyLineEffectCount
    }
    public class LeyLineAmulet : WorldObject
    {
        private static readonly List<SpellId> PossibleWarMagicTriggerSpells = new List<SpellId>()
        {
            SpellId.FlameBolt1,
            SpellId.FrostBolt1,
            SpellId.AcidStream1,
            SpellId.ShockWave1,
            SpellId.LightningBolt1,
            SpellId.ForceBolt1,
            SpellId.WhirlingBlade1,

            SpellId.AcidStreak1,
            SpellId.FlameStreak1,
            SpellId.ForceStreak1,
            SpellId.FrostStreak1,
            SpellId.LightningStreak1,
            SpellId.ShockwaveStreak1,
            SpellId.WhirlingBladeStreak1,

            SpellId.AcidArc1,
            SpellId.ForceArc1,
            SpellId.FrostArc1,
            SpellId.LightningArc1,
            SpellId.FlameArc1,
            SpellId.ShockArc1,
            SpellId.BladeArc1,
        };

        private static readonly List<SpellId> PossibleLifeMagicTriggerSpells = new List<SpellId>()
        {
            SpellId.HealSelf1,
            SpellId.HealOther1,

            SpellId.RevitalizeSelf1,
            SpellId.RevitalizeOther1,
            
            SpellId.HealthToManaSelf1,
            
            SpellId.StaminaToManaSelf1,

            SpellId.HarmOther1,

            SpellId.DrainHealth1,

            SpellId.HealthBolt1,
        };

        private static readonly List<SpellId> PossibleLifeMagicOtherTriggerSpells = new List<SpellId>()
        {
            SpellId.HealOther1,

            SpellId.HarmOther1,

            SpellId.DrainHealth1,

            SpellId.HealthBolt1,
        };

        private static readonly List<SpellId> PossibleLifeMagicOffensiveTriggerSpells = new List<SpellId>()
        {
            SpellId.HarmOther1,

            SpellId.DrainHealth1,

            SpellId.HealthBolt1,
        };

        private static readonly List<SpellId> PossibleOffensiveSpells = new List<SpellId>()
        {
            SpellId.FlameBolt1,
            SpellId.FrostBolt1,
            SpellId.AcidStream1,
            SpellId.ShockWave1,
            SpellId.LightningBolt1,
            SpellId.ForceBolt1,
            SpellId.WhirlingBlade1,

            SpellId.AcidStreak1,
            SpellId.FlameStreak1,
            SpellId.ForceStreak1,
            SpellId.FrostStreak1,
            SpellId.LightningStreak1,
            SpellId.ShockwaveStreak1,
            SpellId.WhirlingBladeStreak1,

            SpellId.AcidArc1,
            SpellId.ForceArc1,
            SpellId.FrostArc1,
            SpellId.LightningArc1,
            SpellId.FlameArc1,
            SpellId.ShockArc1,
            SpellId.BladeArc1,

            SpellId.HarmOther1,

            SpellId.DrainHealth1,
            SpellId.DrainStamina1,
            SpellId.DrainMana1,
            SpellId.HealthBolt1,
        };

        private static readonly List<SpellId> PossibleBeneficialSelfSpells = new List<SpellId>()
        {
            SpellId.HealSelf1,
            SpellId.RevitalizeSelf1,
            SpellId.ManaBoostSelf1,

            SpellId.HealthToManaSelf1,

            SpellId.DispelLifeBadSelf1,
            SpellId.DispelCreatureBadSelf1,
        };

        private static readonly List<SpellId> PossibleBeneficialOtherSpells = new List<SpellId>()
        {
            SpellId.HealOther1,
            SpellId.RevitalizeOther1,

            SpellId.InfuseHealth1,
            SpellId.InfuseStamina1,
            SpellId.InfuseMana1,

            SpellId.DispelLifeBadOther1,
            SpellId.DispelCreatureBadOther1,
        };

        private static readonly List<SpellId> PossibleCantrips = new List<SpellId>()
        {
            SpellId.CANTRIPFOCUS1,
            SpellId.CANTRIPWILLPOWER1,

            SpellId.CANTRIPMAGICRESISTANCE1,
            SpellId.CANTRIPMANACONVERSIONPROWESS1,

            SpellId.CANTRIPLIFEMAGICAPTITUDE1,
            SpellId.CANTRIPWARMAGICAPTITUDE1,

            SpellId.CANTRIPARMOR1,
            SpellId.CANTRIPACIDWARD1,
            SpellId.CANTRIPBLUDGEONINGWARD1,
            SpellId.CANTRIPFROSTWARD1,
            SpellId.CANTRIPSTORMWARD1,
            SpellId.CANTRIPFLAMEWARD1,
            SpellId.CANTRIPSLASHINGWARD1,
            SpellId.CANTRIPPIERCINGWARD1,

            SpellId.CantripArmorAptitude1,
        };

        public static readonly List<SpellId> PossibleAcquireSpells = new List<SpellId>()
        {
            SpellId.ArcaneBenightednessOther1,

            SpellId.BladeVulnerabilityOther1,
            SpellId.PiercingVulnerabilityOther1,
            SpellId.BludgeonVulnerabilityOther1,

            SpellId.FireVulnerabilityOther1,
            SpellId.ColdVulnerabilityOther1,
            SpellId.AcidVulnerabilityOther1,
            SpellId.LightningVulnerabilityOther1,

            SpellId.VulnerabilityOther1,
            SpellId.DefenselessnessOther1,
            SpellId.MagicYieldOther1,

            SpellId.HeavyWeaponsIneptitudeOther1,
            SpellId.LightWeaponsIneptitudeOther1,
            SpellId.FinesseWeaponsIneptitudeOther1,
            SpellId.MissileWeaponsIneptitudeOther1,
            SpellId.SpearIneptitudeOther1,
            SpellId.StaffIneptitudeOther1,
            SpellId.MaceIneptitudeOther1,
            SpellId.ThrownWeaponIneptitudeOther1,
            SpellId.UnarmedCombatIneptitudeOther1,

            SpellId.LifeMagicMasterySelf1,
            SpellId.LifeMagicIneptitudeOther1,
            SpellId.WarMagicMasterySelf1,
            SpellId.WarMagicIneptitudeOther1,

            SpellId.ArcaneEnlightenmentSelf1,
            SpellId.ArcaneEnlightenmentOther1,
            SpellId.DeceptionIneptitudeOther1,

            SpellId.ShieldIneptitudeOther1,
            SpellId.ArmorIneptitudeOther1,
        };

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int? LeyLineSchool
        {
            get => GetProperty(PropertyInt.LeyLineSchool);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.LeyLineSchool); else SetProperty(PropertyInt.LeyLineSchool, value.Value); }
        }

        public int? LeyLineEffectId
        {
            get => GetProperty(PropertyInt.LeyLineEffectId);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.LeyLineEffectId); else SetProperty(PropertyInt.LeyLineEffectId, value.Value); }
        }

        public int? LeyLineSeed
        {
            get => GetProperty(PropertyInt.LeyLineSeed);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.LeyLineSeed); else SetProperty(PropertyInt.LeyLineSeed, value.Value); }
        }

        public int? LeyLineLastDecayTime
        {
            get => GetProperty(PropertyInt.LeyLineLastDecayTime);
            set { if (!value.HasValue) RemoveProperty(PropertyInt.LeyLineLastDecayTime); else SetProperty(PropertyInt.LeyLineLastDecayTime, value.Value); }
        }

        public uint? LeyLineTriggerSpellId
        {
            get => GetProperty(PropertyDataId.LeyLineTriggerSpellId);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.LeyLineTriggerSpellId); else SetProperty(PropertyDataId.LeyLineTriggerSpellId, value.Value); }
        }

        public uint? LeyLineCastSpellId
        {
            get => GetProperty(PropertyDataId.LeyLineCastSpellId);
            set { if (!value.HasValue) RemoveProperty(PropertyDataId.LeyLineCastSpellId); else SetProperty(PropertyDataId.LeyLineCastSpellId, value.Value); }
        }

        public double? LeyLineTriggerChance
        {
            get => GetProperty(PropertyFloat.LeyLineTriggerChance);
            set { if (!value.HasValue) RemoveProperty(PropertyFloat.LeyLineTriggerChance); else SetProperty(PropertyFloat.LeyLineTriggerChance, value.Value); }
        }

        public double NextLeyLineTriggerTime = 0;
        public static double LeyLineTriggerInterval = 10;
        public override int? ItemCurMana
        {
            get => GetProperty(PropertyInt.ItemCurMana);
            set { }
        }

        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public LeyLineAmulet(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public LeyLineAmulet(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        private static ushort MaxLeyLineAmuletStructure = 200;

        public virtual void AlignLeyLineAmulet(Hotspot manaField)
        {
            if(manaField == null)
                return;

            Player playerWielder = Wielder as Player;
            if (playerWielder == null)
                return;

            var forwardCommand = playerWielder.CurrentMovementData.MovementType == MovementType.Invalid && playerWielder.CurrentMovementData.Invalid != null ? playerWielder.CurrentMovementData.Invalid.State.ForwardCommand : MotionCommand.Invalid;
            if (forwardCommand != MotionCommand.MeditateState)
                return;

            if (playerWielder.GetCreatureSkill((MagicSchool)LeyLineSchool).AdvancementClass < SkillAdvancementClass.Trained)
                return;

            //int seed = (int)(WeenieClassId + playerWielder.Guid.Low + (playerWielder.CurrentLandblock.Id.LandblockX << 8 | playerWielder.CurrentLandblock.Id.LandblockY) + DerethDateTime.UtcNowToLoreTime.Month);
            if ((playerWielder.LeyLineSeed ?? 0) == 0)
                playerWielder.LeyLineSeed = ThreadSafeRandom.Next(0, int.MaxValue / 2);
            int seed = (int)(WeenieClassId + playerWielder.LeyLineSeed + (playerWielder.CurrentLandblock.Id.LandblockX << 8 | playerWielder.CurrentLandblock.Id.LandblockY));

            Random pseudoRandom = new Random(seed); // Note that this class uses EXCLUSIVE max values instead of inclusive for our regular ThreadSafeRandom.

            var newLeyLineAlignEffectId = pseudoRandom.Next(1, (int)LeyLineEffect.LeyLineEffectCount);

            if ((LeyLineEffectId ?? 0) == 0)
            {
                // Align
                LeyLineSeed = seed;
                LeyLineLastDecayTime = (int)Time.GetUnixTime();

                SetupNewAlignment(pseudoRandom, (LeyLineEffect)newLeyLineAlignEffectId);

                Structure = 10;
                MaxStructure = (ushort)(MaxLeyLineAmuletStructure + 1); // + 1 so we keep the green bar when full
                playerWielder.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, (int)Structure));
                playerWielder.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.MaxStructure, (int)MaxStructure));

                playerWielder.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your amulet is now aligned to this ley line!", ChatMessageType.Magic));
                playerWielder.EnqueueBroadcast(new GameMessageScript(playerWielder.Guid, PlayScript.PortalStorm));

                playerWielder.TryActivateItemSpells(this);
            }
            else if(LeyLineSeed == seed && Structure < MaxLeyLineAmuletStructure)
            {
                IncreasedAlignment(10, playerWielder);
            }
            else if(LeyLineSeed != seed && Structure > 0)
            {
                DecreaseAlignment(10, playerWielder);
            }
        }

        private void IncreasedAlignment(int amount, Player player)
        {
            Structure = (ushort)Math.Min((Structure ?? 0) + amount, MaxLeyLineAmuletStructure);
            if (player != null)
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, (int)Structure));

            bool isWielded = player != null && player == Wielder;

            if (Structure < MaxLeyLineAmuletStructure)
            {
                LeyLineLastDecayTime = (int)Time.GetUnixTime();

                if (isWielded)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your amulet alignment increases!", ChatMessageType.Magic));
                    player.EnqueueBroadcast(new GameMessageScript(player.Guid, PlayScript.PortalStorm));
                }
            }
            else
            {
                if (isWielded)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your amulet alignment increases, it is now fully aligned!", ChatMessageType.Magic));
                    player.EnqueueBroadcast(new GameMessageScript(player.Guid, PlayScript.HealthUpBlue));
                }
            }
        }

        private void DecreaseAlignment(int amount, Player player, bool showDecreaseEffect = true)
        {
            Structure = (ushort)Math.Max((Structure ?? 0) - amount, 0);
            if(player != null)
                player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, (int)Structure));

            bool isWielded = player != null && player == Wielder;

            if (Structure > 0)
            {
                LeyLineLastDecayTime = (int)Time.GetUnixTime();
                if (isWielded)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your amulet alignment decreases!", ChatMessageType.Magic));
                    if(showDecreaseEffect)
                        player.EnqueueBroadcast(new GameMessageScript(player.Guid, PlayScript.PortalStorm));
                }
            }
            else
            {
                if (isWielded)
                    player.DeactivateItemSpells(this);
                ResetAmulet();

                if (player != null)
                {
                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.Structure, 0));
                    player.Session.Network.EnqueueSend(new GameMessagePublicUpdatePropertyInt(this, PropertyInt.MaxStructure, 0));
                }

                if (isWielded)
                {
                    player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Your amulet alignment decreases, it is now unaligned!", ChatMessageType.Magic));
                    player.EnqueueBroadcast(new GameMessageScript(player.Guid, PlayScript.HealthDownBlue));
                }
            }
        }

        private void GetSpell(Random pseudoRandom, out SpellId spellId, out string spellName, out bool isBeneficial, List<SpellId> possibleSpells)
        {
            spellId = possibleSpells[pseudoRandom.Next(0, possibleSpells.Count - 1)];
            Spell spell = new Spell(spellId);
            isBeneficial = spell.IsBeneficial;
            if (spell.Name.EndsWith(" I"))
                spellName = spell.Name.Substring(0, spell.Name.Length - 2);
            else if (spell.Name.EndsWith(" III"))
                spellName = spell.Name.Substring(0, spell.Name.Length - 4);
            else
                spellName = spell.Name;
        }

        private void ResetAmulet()
        {
            LeyLineSeed = null;
            LeyLineEffectId = null;
            LeyLineLastDecayTime = null;
            LeyLineTriggerSpellId = null;
            LeyLineCastSpellId = null;
            LeyLineTriggerChance = null;
            Biota.ClearSpells(BiotaDatabaseLock);

            Structure = null;
            MaxStructure = null;

            Attuned = AttunedStatus.Normal;
            LongDesc = $"{basicDescription}\n\nCurrent Effect: Unaligned";
        }

        public void OnActivate(Player player)
        {
            if (player == null)
                return;

            var level1SpellId = LeyLineCastSpellId ?? 0;
            if (LeyLineEffectId == (int)LeyLineEffect.GrantCastableSpell && level1SpellId != 0)
            {
                uint playerSkillLevel = player.GetCreatureSkill(((LeyLineSchool ?? 0) == (int)MagicSchool.WarMagic) ? Skill.WarMagic : Skill.LifeMagic).Current;

                var maxLevel = 6;
                Spell level1Spell = new Spell(level1SpellId);
                if (!level1Spell.IsBeneficial)
                {
                    maxLevel = 4;
                    playerSkillLevel = (uint)(playerSkillLevel * 0.5f);
                }

                for (int level = 1; level <= maxLevel; level++)
                {
                    SpellId grantedSpellIdAttempt = SpellLevelProgression.GetSpellAtLevel((SpellId)level1SpellId, level, true);
                    Spell grantedSpellAttempt = new Spell(grantedSpellIdAttempt);

                    if (playerSkillLevel >= (int)grantedSpellAttempt.Power - 50)
                        player.LearnSpellWithNetworking((uint)grantedSpellIdAttempt);
                    else
                        break;
                }
            }
        }

        public void OnDeactivate(Player player)
        {
            if (player == null)
                return;

            var level1SpellId = LeyLineCastSpellId ?? 0;
            if (LeyLineEffectId == (int)LeyLineEffect.GrantCastableSpell && level1SpellId != 0)
            {
                for (int level = 1; level <= 6; level++)
                {
                    SpellId spellId = SpellLevelProgression.GetSpellAtLevel((SpellId)level1SpellId, level, false);
                    if (player.HandleActionMagicRemoveSpellId((uint)spellId, true))
                    {
                        var spell = new Server.Entity.Spell(spellId);
                        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You forget the {spell.Name} spell.", ChatMessageType.Broadcast));
                    }
                }
            }
        }

        private static string basicDescription = "An amulet used to align oneself with the mana currents that flow throughout the land.";

        private void SetupNewAlignment(Random pseudoRandom, LeyLineEffect leyLineEffect)
        {
            if (WeenieClassId != 50056 && WeenieClassId != 50057)
            {
                log.Debug($"{Name}.Setup: Weenie {WeenieClassId} is not a ley line amulet!");
                return;
            }

            var isWarAmulet = LeyLineSchool == (int)MagicSchool.WarMagic;
            SpellId triggerSpellId;
            SpellId castSpellId;
            string triggerSpellName;
            string castSpellName;
            string effect;
            bool isBeneficial;

            if (leyLineEffect != LeyLineEffect.None)
                Attuned = AttunedStatus.Attuned;
            LeyLineEffectId = (int)leyLineEffect;

            switch ((LeyLineEffect)(LeyLineEffectId ?? 0))
            {
                case LeyLineEffect.MirrorSpell:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicTriggerSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    LeyLineCastSpellId = (uint)triggerSpellId;
                    LeyLineTriggerChance = 0.3;
                    effect = $"When casting {triggerSpellName} there is a chance a second {triggerSpellName} of a lower level will also be cast.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nTrigger Spell: {triggerSpellName}\n\nCasted Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.CastExtraSpellOther:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicOtherTriggerSpells);
                    GetSpell(pseudoRandom, out castSpellId, out castSpellName, out _, isBeneficial ? PossibleBeneficialOtherSpells : PossibleOffensiveSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    LeyLineCastSpellId = (uint)castSpellId;
                    LeyLineTriggerChance = 0.3;
                    effect = $"When casting {triggerSpellName} there is a chance {castSpellName} of a lower level will also be cast on your target.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nTrigger Spell: {triggerSpellName}\n\nCasted Spell: {castSpellName}";
                    break;

                case LeyLineEffect.CastExtraSpellSelf:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicTriggerSpells);
                    GetSpell(pseudoRandom, out castSpellId, out castSpellName, out _, PossibleBeneficialSelfSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    LeyLineCastSpellId = (uint)castSpellId;
                    LeyLineTriggerChance = 0.3;
                    effect = $"When casting {triggerSpellName} there is a chance {castSpellName} of a lower level will also be cast on yourself.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nTrigger Spell: {triggerSpellName}\n\nCasted Spell: {castSpellName}";
                    break;

                case LeyLineEffect.ExtraSpellIntensity:
                    effect = "ExtraSpellIntensity";
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicTriggerSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    LeyLineTriggerChance = 1.0;
                    effect = $"{triggerSpellName} spell effects are stronger than usual.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nAffected Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.LessManaUsage:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicTriggerSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    effect = $"{triggerSpellName} mana costs are lower than usual.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nAffected Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.LowerFizzleChance:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicTriggerSpells);
                    effect = $"{triggerSpellName} fizzle rate is lower than usual.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nAffected Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.LowerResistChance:
                    GetSpell(pseudoRandom, out triggerSpellId, out triggerSpellName, out isBeneficial, isWarAmulet ? PossibleWarMagicTriggerSpells : PossibleLifeMagicOffensiveTriggerSpells);
                    LeyLineTriggerSpellId = (uint)triggerSpellId;
                    effect = $"{triggerSpellName} resist rate is lower than usual.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nAffected Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.LowerCompBurnChanceAllSpells:
                    effect = $"Component burn chances are lower than usual.";
                    triggerSpellName = isWarAmulet ? "All War Magic Spells" : "All Life Magic Spells";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nAffected Spell: {triggerSpellName}";
                    break;

                case LeyLineEffect.GrantCantrip:
                    GetSpell(pseudoRandom, out castSpellId, out _, out _, PossibleCantrips);
                    Biota.GetOrAddKnownSpell((int)castSpellId, BiotaDatabaseLock, out _);
                    LongDesc = basicDescription;
                    break;

                case LeyLineEffect.GrantCastableSpell:
                    GetSpell(pseudoRandom, out castSpellId, out castSpellName, out _, PossibleAcquireSpells);
                    LeyLineCastSpellId = (uint)castSpellId;
                    effect = $"Grants the {castSpellName} spell.";
                    LongDesc = $"{basicDescription}\n\nCurrent Effect: {effect}\n\nSpell: {castSpellName}";
                    break;
            }
        }

        private static double DecayInterval = 3600; // 1 hour
        public void CheckAlignmentDecay(Player player, double currentUnixTime)
        {
            if (LeyLineLastDecayTime != null)
            {
                var secondsSinceLastDecay = currentUnixTime - LeyLineLastDecayTime;
                if (secondsSinceLastDecay >= DecayInterval) 
                {
                    var amount = (int)(secondsSinceLastDecay / DecayInterval);
                    DecreaseAlignment(amount, player, false);
                }
            }
        }
    }
}
