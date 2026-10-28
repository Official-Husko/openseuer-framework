using ObenLoader.Runtime;

namespace ObenUIKit;

/// <summary>A hint waiting in the queue or showing. <see cref="Hide"/> takes it away early.</summary>
public sealed class HudHint
{
    internal HudHint(ITracker owner, string title, string text, float seconds, bool force)
    {
        Owner = owner;
        Title = title;
        Text = text;
        Remaining = seconds;
        Force = force;
    }

    public string Title { get; }

    public string Text { get; }

    /// <summary>True until the hint has shown for its time, was hidden, or its mod unloaded.</summary>
    public bool IsActive { get; internal set; } = true;

    internal ITracker Owner { get; }

    /// <summary>Seconds left to show. The clock only runs while the hint is visible.</summary>
    internal float Remaining { get; set; }

    /// <summary>Show even when the player switched the game's hints off.</summary>
    internal bool Force { get; }

    /// <summary>Takes the hint away now, or removes it from the queue.</summary>
    public void Hide() => HudRuntime.Current?.RemoveHint(this);
}
