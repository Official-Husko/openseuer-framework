using UnityEngine;

namespace ObenFramework.Dialogue;

/// <summary>Which conversation a <see cref="Conversations.Started"/> or <see cref="Conversations.Ended"/> event is about.</summary>
public readonly struct ConversationInfo
{
    public ConversationInfo(string title, Transform actor, Transform conversant)
    {
        Title = title;
        Actor = actor;
        Conversant = conversant;
    }

    /// <summary>The conversation's title in the dialogue database.</summary>
    public string Title { get; }

    /// <summary>The first speaker; in the game's conversations this is the player.</summary>
    public Transform Actor { get; }

    /// <summary>The other speaker, usually the NPC. Null for conversations started without one.</summary>
    public Transform Conversant { get; }
}

/// <summary>The result of trying to start a conversation.</summary>
public enum ConversationStart
{
    /// <summary>The conversation is running.</summary>
    Started,

    /// <summary>There is no playable world yet (main menu, loading) or the game's dialogue UI doesn't exist.</summary>
    NotInWorld,

    /// <summary>Another conversation is already running.</summary>
    Busy,

    /// <summary>No NPC with that name is in the loaded scene.</summary>
    UnknownNpc,

    /// <summary>The dialogue database has no conversation with that title.</summary>
    UnknownConversation,

    /// <summary>The NPC can't talk right now (the player is in the air, in a contest, waiting, or the NPC is using a door).</summary>
    CannotTalk,

    /// <summary>The conversation exists but none of its entries is valid right now (every condition failed), so it didn't open.</summary>
    NoValidEntry,
}
