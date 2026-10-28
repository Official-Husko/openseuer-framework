// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Dialogue;

/// <summary>One line of a mod's conversation.</summary>
public sealed class ConversationLine
{
    /// <summary>The line's id inside the conversation: 1 to 48 letters, digits, '_' or '-'.</summary>
    public string Key { get; set; }

    /// <summary>The player says it (a choice when several player lines follow one line); otherwise the speaker does.</summary>
    public bool Player { get; set; }

    /// <summary>Up to 1000 characters. Can be null when <see cref="TextCallback"/> is set.</summary>
    public string Text { get; set; }

    /// <summary>A Lua condition (up to 500 characters); the line only shows while it is true.</summary>
    public string Condition { get; set; }

    /// <summary>Lua run when the line shows (up to 500 characters), such as <c>Variable["MyMod_Met"] = true</c>.</summary>
    public string Script { get; set; }

    /// <summary>Keys of the lines that can follow; none ends the conversation.</summary>
    public string[] Next { get; set; }

    /// <summary>Asked for the text each time the line shows; <see cref="Text"/> is used when it returns null.</summary>
    public Func<string> TextCallback { get; set; }
}

/// <summary>A conversation a mod adds (<see cref="DialogueAuthoring.Add"/>) or joins into a game conversation as a branch.</summary>
public sealed class ConversationDefinition
{
    /// <summary>1 to 48 letters, digits, '_' or '-'. The conversation's title is "modId.key".</summary>
    public string Key { get; set; }

    /// <summary>
    /// The dialogue database actor who talks (such as "Bazaar/Trader/Jens Brom"), or null for a nameless voice.
    /// A branch always uses the speaker of the line it is attached to.
    /// </summary>
    public string Speaker { get; set; }

    /// <summary>1 to 200 lines. The first one opens the conversation.</summary>
    public List<ConversationLine> Lines { get; set; } = new List<ConversationLine>();
}

/// <summary>Why a conversation, response or branch was not added.</summary>
public enum ConversationRefusal
{
    None,

    /// <summary>The dialogue database is not loaded yet.</summary>
    NotReady,

    /// <summary>A save or load is running.</summary>
    Busy,

    /// <summary>A key, text, Lua snippet or link breaks the rules; the reason says which.</summary>
    BadInput,

    /// <summary>This mod already added a conversation with that key, or the database has that title.</summary>
    AlreadyExists,

    /// <summary>The speaker is not an actor in the dialogue database, or is the player.</summary>
    UnknownSpeaker,

    /// <summary>The dialogue database has no player actor.</summary>
    NoPlayerActor,

    /// <summary>The conversation to join, or the line to attach to, isn't there (or matches several lines).</summary>
    NoAnchor,

    /// <summary>A Dialogue System call threw; it is logged.</summary>
    Failed,
}

/// <summary>The outcome of adding a conversation, response or branch.</summary>
public readonly struct DialogueEditResult
{
    internal DialogueEditResult(string key, ConversationRefusal refusal, string reason, bool waiting = false)
    {
        Key = key;
        Refusal = refusal;
        Reason = reason;
        Waiting = waiting;
    }

    /// <summary>The conversation title, or the response or branch key to remove it with. Null when refused.</summary>
    public string Key { get; }

    public ConversationRefusal Refusal { get; }

    /// <summary>Why it was refused, in words; null when it worked.</summary>
    public string Reason { get; }

    public bool Added => Key != null;

    /// <summary>
    /// Added, but the conversation or the line it attaches to isn't loaded yet; it is linked as soon as it is.
    /// </summary>
    public bool Waiting { get; }

    public override string ToString() => Added ? (Waiting ? "waiting " : "added ") + Key : Refusal + ": " + Reason;
}
