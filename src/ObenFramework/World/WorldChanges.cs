using System;
using ObenFramework.Game;

namespace ObenFramework.World;

/// <summary>Who caused a change in the world.</summary>
public enum ChangeSource
{
    /// <summary>The game itself: a character, a timer, a script, a relay.</summary>
    Game,

    /// <summary>The local player, by using something or through the building menu.</summary>
    LocalPlayer,

    /// <summary>A mod, through the framework's setters or inside a <see cref="WorldChanges.Enter"/> scope.</summary>
    Mod,
}

/// <summary>
/// Tells the world events where a change came from. The framework's own setters (<c>Doors.Open</c> and
/// friends) run inside a <see cref="ChangeSource.Mod"/> scope, so a mod that mirrors changes to other
/// machines skips events it caused itself and forwards only <see cref="ChangeSource.Game"/> and
/// <see cref="ChangeSource.LocalPlayer"/>. Mods that call the game's methods directly should wrap the
/// call the same way:
/// <code>using (WorldChanges.Enter(ChangeSource.Mod)) door.ForceOpen();</code>
/// </summary>
public static class WorldChanges
{
    private static int _player;
    private static int _mod;

    /// <summary>The source events raised right now are attributed to.</summary>
    public static ChangeSource Current => _mod > 0 ? ChangeSource.Mod : _player > 0 ? ChangeSource.LocalPlayer : ChangeSource.Game;

    /// <summary>Starts attributing changes to <paramref name="source"/> until the scope is disposed.</summary>
    public static ChangeScope Enter(ChangeSource source)
    {
        Adjust(source, 1);
        return new ChangeScope(source);
    }

    /// <summary>Shorthand for <c>Enter(ChangeSource.Mod)</c>.</summary>
    public static ChangeScope EnterMod() => Enter(ChangeSource.Mod);

    /// <summary>
    /// Events are only raised in a settled world: not while a save loads and not between scenes, when
    /// the game flips thousands of switches while applying saved state.
    /// </summary>
    internal static bool Active => WorldState.IsReady && !WorldState.IsLoadingSave;

    internal static void Adjust(ChangeSource source, int by)
    {
        if (source == ChangeSource.Mod)
            _mod = Math.Max(0, _mod + by);
        else if (source == ChangeSource.LocalPlayer)
            _player = Math.Max(0, _player + by);
    }

    internal static void Reset() => _player = _mod = 0;
}

/// <summary>Ends a <see cref="WorldChanges.Enter"/> scope when disposed.</summary>
public readonly struct ChangeScope : IDisposable
{
    private readonly ChangeSource _source;

    internal ChangeScope(ChangeSource source)
    {
        _source = source;
    }

    public void Dispose() => WorldChanges.Adjust(_source, -1);
}
