using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Entity.Models;
using ACE.Server.Entity;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;

namespace ACE.Server.WorldObjects
{
    public class SpellConduit : WorldObject
    {
        /// <summary>
        /// A new biota be created taking all of its values from weenie.
        /// </summary>
        public SpellConduit(Weenie weenie, ObjectGuid guid) : base(weenie, guid)
        {
            SetEphemeralValues();
        }

        /// <summary>
        /// Restore a WorldObject from the database.
        /// </summary>
        public SpellConduit(Biota biota) : base(biota)
        {
            SetEphemeralValues();
        }

        private void SetEphemeralValues()
        {
        }

        public override void ActOnUse(WorldObject activator)
        {
            if (!(activator is Player player))
                return;

            if (SpellDID.HasValue)
            {
                var spell = new Spell((uint)SpellDID);
                if (spell.IsSelfTargeted)
                    player.HandleActionCastTargetedSpell(player.Guid.Full, spell.Id, this, true);
                else if (spell.NonComponentTargetType == ItemType.None)
                    player.HandleActionMagicCastUnTargetedSpell(spell.Id, this, true);
                else
                {
                    uint targetId = 0;

                    var lastAttackTarget = player.LastAttackTarget;
                    if (!spell.IsBeneficial && lastAttackTarget != null)
                        targetId = lastAttackTarget.Guid.Full;
                    else
                        targetId = player.RequestedAppraisalTarget ?? 0;

                    if (targetId == Guid.Full) // If the player activates the SpellConduit using the mouse the first click will select the SpellConduit, so we need this to actually get the real target.
                        targetId = player.PreviousRequestedAppraisalTarget;

                    if (targetId != 0 && targetId != player.Guid.Full)
                        player.HandleActionCastTargetedSpell(targetId, spell.Id, this, true);
                    else
                        player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, $"You cannot cast this spell upon yourself"));
                }
                return;
            }
            else if(player.SpellConduitToAttune == null)
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Spell attuning process started: the next valid spell you cast will be attuned to this spell conduit. To cancel this process use the spell conduit again.", ChatMessageType.Magic));
                player.SpellConduitToAttune = this;
            }
            else
            {
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"You cancel the spell attuning process.", ChatMessageType.Magic));
                player.SpellConduitToAttune = null;
            }
        }

        public void AttuneSpell(Player player, Spell spell)
        {
            var validLevel = Level ?? 0;
            var spellLevel = spell.Level;
            if (validLevel != 0 && spellLevel != validLevel)
            {
                player.SpellConduitToAttune = null;
                player.Session.Network.EnqueueSend(new GameMessageSystemChat($"Attuning process failed: spell is of the wrong level.", ChatMessageType.Magic));
                return;
            }

            player.SpellConduitToAttune = null;
            player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{Name} successfully attuned to {spell.Name}.", ChatMessageType.Magic));

            Name = $"Spell Conduit: {spell.Name}";
            Level = (int)spellLevel;
            Use = $"Use this conduit to cast {spell.Name} without the aid of magical implements.";
            LongDesc = null;
            SpellDID = spell.Id;
            IconId = spell.IconID;
            CooldownId = 222;
            if (spell.IsSelfTargeted)
                IconOverlayId = 0x060013F3;
            switch(spellLevel)
            {
                default:
                case 1:
                    IconUnderlayId = 0x060013F4;
                    CooldownDuration = 5.0f;
                    break;
                case 2:
                    IconUnderlayId = 0x060013F5;
                    CooldownDuration = 6.25f;
                    break;
                case 3:
                    IconUnderlayId = 0x060013F6;
                    CooldownDuration = 7.81f;
                    break;
                case 4:
                    IconUnderlayId = 0x060013F7;
                    CooldownDuration = 9.76f;
                    break;
                case 5:
                    IconUnderlayId = 0x060013F8;
                    CooldownDuration = 12.20f;
                    break;
                case 6:
                    IconUnderlayId = 0x060013F9;
                    CooldownDuration = 15.25f;
                    break;
                case 7:
                    IconUnderlayId = 0x06001F63;
                    CooldownDuration = 19.07f;
                    break;
            }
            player.Session.Network.EnqueueSend(new GameMessageUpdateObject(this));
        }

        public void StartCooldown(Player player)
        {
            player.EnchantmentManager.StartCooldown(this);
        }
    }
}
