// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Dialogue;

/// <summary>Where a response or branch attaches: an NPC line of a game conversation that leads to the player's choices.</summary>
public abstract class AnchorDefinition
{
    /// <summary>The game conversation's title, such as "Bazaar/Trader/Jens Brom".</summary>
    public string Conversation { get; set; }

    /// <summary>
    /// The NPC line's text. Matched exactly, else ignoring case and spaces at the ends, else also ignoring the
    /// Dialogue System's pause codes (<c>\,</c>); it has to match exactly one line. Leave null to use <see cref="AnchorId"/>.
    /// </summary>
    public string AnchorText { get; set; }

    /// <summary>The NPC line's entry id (<c>dialogue.lines</c> on the bridge lists them), when <see cref="AnchorText"/> is null.</summary>
    public int AnchorId { get; set; } = -1;

    /// <summary>Where the new choice goes among the line's links: -1 for last, 0 for first.</summary>
    public int InsertAt { get; set; } = -1;

    /// <summary>The link's priority from 0 (Low) to 4 (High), or -1 to use the priority of the line's other choices.</summary>
    public int Priority { get; set; } = -1;

    /// <summary>Runs when the player picks the choice (from the Dialogue System's script, during the conversation).</summary>
    public Action OnPicked { get; set; }
}

/// <summary>A single extra player choice on a game NPC line, with an optional one-line NPC reply.</summary>
public sealed class ResponseDefinition : AnchorDefinition
{
    /// <summary>What the player says, up to 1000 characters.</summary>
    public string PlayerText { get; set; }

    /// <summary>What the NPC answers before the conversation ends, or null to end at once.</summary>
    public string NpcReply { get; set; }

    /// <summary>A Lua condition; the choice only shows while it is true. Null shows it always.</summary>
    public string Condition { get; set; }
}

/// <summary>A whole sub-conversation of choices and lines joined to a game NPC line.</summary>
public sealed class BranchDefinition : AnchorDefinition
{
    /// <summary>
    /// The lines. The first one is what the anchor leads to: usually a player line (a new menu choice). Its
    /// speaker is always the anchor's NPC, so leave <see cref="ConversationDefinition.Speaker"/> null.
    /// </summary>
    public ConversationDefinition Lines { get; set; }
}
