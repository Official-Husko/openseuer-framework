// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;

namespace ObenFramework.Player.Survival;

/// <summary>What the player's mind does when depression reaches its worst.</summary>
public enum BreakdownKind
{
    /// <summary>Kills the player.</summary>
    Suicide,

    /// <summary>Cuts the player (adds bleeding).</summary>
    SelfHarm,

    /// <summary>Eats mushrooms from the inventory.</summary>
    Mushrooms,

    /// <summary>Drinks alcohol from the inventory.</summary>
    Alcohol,
}

/// <summary>One thing the player's mind is about to do during a breakdown.</summary>
public sealed class BreakdownContext
{
    internal BreakdownContext(BreakdownKind kind)
    {
        Kind = kind;
    }

    public BreakdownKind Kind { get; }

    public bool Cancelled { get; private set; }

    /// <summary>Stops this act. The game then tries its next one (for example alcohol after mushrooms), which you can cancel too.</summary>
    public void Cancel() => Cancelled = true;
}

/// <summary>
/// When effective depression reaches 100 the game rolls what the player does: die, hurt themselves, eat
/// mushrooms or drink. Handlers see each of those and can cancel it, for a mod that makes the low point
/// survivable. Only the automatic reaction is covered; the same calls made on purpose elsewhere are not.
/// </summary>
public static class BreakdownHooks
{
    private static readonly HandlerList<Action<BreakdownContext>> Handlers = new HandlerList<Action<BreakdownContext>>();

    /// <summary>Adds a handler for as long as <paramref name="owner"/> is loaded, or until the result is disposed.</summary>
    public static IDisposable Intercept(ObenMod owner, Action<BreakdownContext> handler)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return owner.Track(Handlers.Add(handler));
    }

    /// <summary>Runs the handlers; false when one of them cancelled.</summary>
    internal static bool Allow(BreakdownKind kind)
    {
        if (Handlers.Count == 0)
            return true;

        var context = new BreakdownContext(kind);
        Handlers.ForEach(h => h(context), GameEvents.Log, "BreakdownHooks");
        return !context.Cancelled;
    }
}
