using System;
using ObenLoader.Runtime;

namespace ObenUIKit;

/// <summary>Boxes of text on the HUD.</summary>
public static partial class Hud
{
    /// <summary>
    /// Adds a box of text at the top left, under the bars, that follows <paramref name="text"/> (several lines
    /// are fine). The function is read four times a second, so keep it cheap; if it throws, the readout is
    /// removed. The text shows as it is: rich text tags in it are not read. Returns null when four readouts
    /// are already up.
    /// </summary>
    /// <param name="owner">The mod or feature the readout belongs to.</param>
    /// <param name="title">A grey line on top; empty leaves it out.</param>
    /// <param name="text">What the box shows.</param>
    public static HudReadout Readout(ITracker owner, string title, Func<string> text)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        return Live.AddReadout(owner, title ?? "", text);
    }
}
