using System;
using ObenLoader.Runtime;
using ObenUIKit.Runtime;

namespace ObenUIKit.Windows;

/// <summary>Small modal windows: ask a yes or no question.</summary>
internal static class Dialogs
{
    private const float Width = 620f;
    private const float Height = 460f;

    private static int _count;

    public static UiWindow Confirm(KitContext kit, ITracker owner, string title, string text, Action onYes, string yes, string no)
    {
        UiWindow window = null;
        // Every question gets its own window; the same title twice shouldn't hand back the first one.
        window = UiKit.Open(owner, "kit.confirm." + ++_count, title,
            ui => ui.Label(text),
            footer: f =>
            {
                f.Button(yes, () =>
                {
                    window.Close();
                    try { onYes?.Invoke(); }
                    catch (Exception e) { kit.Log.Error($"The answer to '{title}' failed", e); }
                });
                // A lambda: the footer is built inside UiKit.Open, before 'window' is assigned.
                f.Button(no, () => window.Close());
            },
            width: Width, height: Height);
        return window;
    }
}
