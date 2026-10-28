// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;

namespace ObenFramework.Player.Survival;

/// <summary>
/// One hit on the player, before it lands. Change <see cref="Amount"/> to scale it or call
/// <see cref="Cancel"/> to stop it.
/// </summary>
public sealed class DamageContext
{
    private float _amount;

    internal DamageContext(float amount, DamageTypeNames reason, bool silent)
    {
        Original = amount;
        _amount = amount;
        Reason = reason;
        Silent = silent;
    }

    /// <summary>What the game was about to apply (bleeding added instead of health lost for <see cref="DamageTypeNames.Bleed"/>).</summary>
    public float Original { get; }

    /// <summary>What will be applied. Later handlers see earlier handlers' changes. Negative or invalid values count as 0.</summary>
    public float Amount
    {
        get => _amount;
        set => _amount = float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }

    public DamageTypeNames Reason { get; }

    /// <summary>The caller asked for no hurt sound or screen effect.</summary>
    public bool Silent { get; }

    /// <summary>True once any handler cancelled the hit.</summary>
    public bool Cancelled { get; private set; }

    /// <summary>Stops the hit from happening. It stays cancelled whatever later handlers do.</summary>
    public void Cancel() => Cancelled = true;
}

/// <summary>
/// Look at, scale or cancel every hit the player takes. Handlers run in the order they were added, each
/// in its own try/catch. Use <see cref="PlayerEvents.Damaged"/> instead when you only need to know what
/// was lost. A handler that damages the player itself is not called again for its own hit.
/// </summary>
public static class DamageHooks
{
    private static readonly HandlerList<Action<DamageContext>> Handlers = new HandlerList<Action<DamageContext>>();
    private static bool _running;

    /// <summary>Adds a handler for as long as <paramref name="owner"/> is loaded, or until the result is disposed.</summary>
    public static IDisposable Intercept(ObenMod owner, Action<DamageContext> handler)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return owner.Track(Handlers.Add(handler));
    }

    /// <summary>Runs the handlers; returns the amount the game should apply (0 when cancelled).</summary>
    internal static float Dispatch(float damage, DamageTypeNames reason, bool silent)
    {
        if (Handlers.Count == 0 || _running || !(damage > 0f))
            return damage;

        var context = new DamageContext(damage, reason, silent);
        _running = true;
        try
        {
            Handlers.ForEach(h => h(context), GameEvents.Log, "DamageHooks");
        }
        finally
        {
            _running = false;
        }
        return context.Cancelled ? 0f : context.Amount;
    }
}
