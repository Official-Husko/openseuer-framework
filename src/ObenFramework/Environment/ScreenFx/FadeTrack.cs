using System;

namespace ObenFramework.Env;

/// <summary>
/// A few numbers that glide from where they are to a target over a set time: a vignette's colour and
/// strength, a blur amount. Starting a new fade begins from the values shown right now, so a fade that is
/// cut short by another never jumps. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class FadeTrack
{
    private readonly float[] _from;
    private readonly float[] _to;
    private float _duration;
    private float _elapsed;

    public FadeTrack(int channels)
    {
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels));
        _from = new float[channels];
        _to = new float[channels];
        Current = new float[channels];
    }

    /// <summary>What is shown now. Read it, don't write it.</summary>
    public float[] Current { get; }

    /// <summary>True while a fade is under way.</summary>
    public bool Running => _elapsed < _duration;

    /// <summary>Fades to <paramref name="target"/> over <paramref name="seconds"/>; 0 or less jumps there at once.</summary>
    public void FadeTo(float[] target, float seconds)
    {
        if (target == null || target.Length != Current.Length)
            throw new ArgumentException("The target needs one value per channel.", nameof(target));
        Array.Copy(Current, _from, Current.Length);
        Array.Copy(target, _to, Current.Length);
        _elapsed = 0f;
        _duration = seconds > 0f && !float.IsInfinity(seconds) ? seconds : 0f;
        if (_duration <= 0f)
            Array.Copy(target, Current, Current.Length);
    }

    /// <summary>Moves the fade on by <paramref name="seconds"/>. Returns true while it still runs.</summary>
    public bool Step(float seconds)
    {
        if (!Running)
            return false;
        _elapsed += Math.Max(0f, seconds);
        float t = _elapsed >= _duration ? 1f : _elapsed / _duration;
        for (int i = 0; i < Current.Length; i++)
            Current[i] = _from[i] + (_to[i] - _from[i]) * t;
        return Running;
    }
}
