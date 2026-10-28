// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Env;

/// <summary>A <see cref="SkyChannel{T}"/> without its value type, so channels of all kinds can be swept together.</summary>
internal interface ISkyChannel
{
    bool Held { get; }

    /// <summary>Lets go without writing anything (the target is gone with its scene).</summary>
    void Forget();

    /// <summary>Before a game writer runs: put the game's value back where ours still stands.</summary>
    void Suspend();

    /// <summary>After a game writer ran: whatever is there now is the game's value.</summary>
    void Capture();
}

/// <summary>
/// One overridable value of the sky (fog density, the sun's colour) on one target. While it holds a value
/// it remembers the game's own value underneath. Anything the game writes in between counts as the new
/// value underneath, and letting go restores only when our value is still the one in place, so a newer
/// value the game wrote is never stomped on. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class SkyChannel<T> : ISkyChannel where T : struct
{
    private readonly Func<object, T> _read;
    private readonly Action<object, T> _write;
    private bool _held;
    private T _vanilla;
    private T _applied;
    private object _target;

    public SkyChannel(Func<object, T> read, Action<object, T> write)
    {
        _read = read;
        _write = write;
    }

    public bool Held => _held;

    public void Forget() => _held = false;

    public void Suspend()
    {
        if (_held && Same(_read(_target), _applied))
            _write(_target, _vanilla);
    }

    public void Capture()
    {
        if (_held)
            _vanilla = _read(_target);
    }

    /// <summary>Writes <paramref name="want"/> to <paramref name="target"/>, or lets go when it is null.</summary>
    public void Apply(object target, T? want)
    {
        if (_held && !ReferenceEquals(_target, target))
            _held = false;
        if (!want.HasValue)
        {
            if (_held && Same(_read(_target), _applied))
                _write(_target, _vanilla);
            _held = false;
            return;
        }

        T current = _read(target);
        if (!_held || !Same(current, _applied))
        {
            _vanilla = current;
            _target = target;
            _held = true;
        }
        _write(target, want.Value);
        _applied = _read(target);
    }

    private static bool Same(T a, T b) => EqualityComparer<T>.Default.Equals(a, b);
}
