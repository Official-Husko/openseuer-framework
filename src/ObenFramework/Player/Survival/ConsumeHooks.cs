// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;
using OS.Items;

namespace ObenFramework.Player.Survival;

/// <summary>An item the player is about to eat, drink or otherwise consume.</summary>
public sealed class ConsumeContext
{
    internal ConsumeContext(ItemData data)
    {
        Data = data;
    }

    /// <summary>The game's item instance (amount, owner, metadata).</summary>
    public ItemData Data { get; }

    public int ItemId => Data.item.ID;

    public string ItemTitle => Data.item.Title;

    /// <summary>True once any handler cancelled it.</summary>
    public bool Cancelled { get; private set; }

    /// <summary>
    /// Stops the item being used: nothing is eaten, the stack stays, no effects apply. It stays cancelled
    /// whatever later handlers do. Show the player a reason yourself if they would wonder.
    /// </summary>
    public void Cancel() => Cancelled = true;
}

/// <summary>An item that was consumed, after its effects applied.</summary>
public readonly struct ConsumedItem
{
    public ConsumedItem(int itemId, string title)
    {
        ItemId = itemId;
        Title = title;
    }

    public int ItemId { get; }
    public string Title { get; }
}

/// <summary>
/// Look at or veto food, drinks and other consumables before the player uses them, from the inventory
/// and from the world. Handlers run in the order they were added, each in its own try/catch.
/// </summary>
public static class ConsumeHooks
{
    private static readonly HandlerList<Action<ConsumeContext>> Handlers = new HandlerList<Action<ConsumeContext>>();

    /// <summary>Adds a handler for as long as <paramref name="owner"/> is loaded, or until the result is disposed.</summary>
    public static IDisposable Intercept(ObenMod owner, Action<ConsumeContext> handler)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return owner.Track(Handlers.Add(handler));
    }

    internal static bool HasHandlers => Handlers.Count > 0;

    /// <summary>Runs the handlers; false when one of them cancelled.</summary>
    internal static bool Allow(ItemData data)
    {
        if (Handlers.Count == 0 || data == null || data.item == null)
            return true;

        var context = new ConsumeContext(data);
        Handlers.ForEach(h => h(context), GameEvents.Log, "ConsumeHooks");
        return !context.Cancelled;
    }
}
