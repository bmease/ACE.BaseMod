﻿using ACE.Server.Network;
using ACE.Server.Network.GameMessages.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Shared.Helpers;
public static class PlayerSpellExtensions
{
    /// <summary>
    /// Learns a spell, returning true or felse based on 
    /// </summary>
    public static bool LearnSpellById(this Player player, SpellId spellId)
    {
        if (Enum.IsDefined(typeof(SpellId), spellId))
        {
            return player.TryLearnSpell((uint)spellId);
           
        }

        return false;
    }

    /// <summary>
    /// Reimplementation of ACE version returning true if the spell was learned successfully
    /// </summary>
    public static bool TryLearnSpell(this Player player, uint spellId, bool uiOutput = true)
    {
        var spells = DatManager.PortalDat.SpellTable;

        if (!spells.Spells.ContainsKey(spellId))
        {
            GameMessageSystemChat errorMessage = new GameMessageSystemChat("SpellID not found in Spell Table", ChatMessageType.Broadcast);
            player.Session.Network.EnqueueSend(errorMessage);
            return false;
        }

        if (!player.AddKnownSpell(spellId))
        {
            if (uiOutput)
            {
                GameMessageSystemChat errorMessage = new GameMessageSystemChat("You already know that spell!", ChatMessageType.Broadcast);
                player.Session.Network.EnqueueSend(errorMessage);
            }
            return false;
        }

        GameEventMagicUpdateSpell updateSpellEvent = new GameEventMagicUpdateSpell(player.Session, (ushort)spellId);
        player.Session.Network.EnqueueSend(updateSpellEvent);

        // Check to see if we echo output to the client text area and do playscript animation
        if (uiOutput)
        {
            // Always seems to be this SkillUpPurple effect
            player.ApplyVisualEffects(PlayScript.SkillUpPurple);

            string message = $"You learn the {spells.Spells[spellId].Name} spell.\n";
            GameMessageSystemChat learnMessage = new GameMessageSystemChat(message, ChatMessageType.Broadcast);
            player.Session.Network.EnqueueSend(learnMessage);
        }
        else
        {
            player.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(player.Session, "You have learned a new spell."));
        }

        return true;
    }
}
