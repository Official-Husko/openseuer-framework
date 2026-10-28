using ObenLoader.Logging;
using ObenUIKit.Theme;
using ObenUIKit.Windows;

namespace ObenUIKit.Runtime;

/// <summary>What windows and controls need from the kit: the look, sounds, the canvas and the window stack.</summary>
internal sealed class KitContext
{
    public KitRoot Root;
    public ThemeCapture Themes;
    public UiSounds Sounds;
    public WindowStack Windows;
    public KitTick Tick;
    public ModLogger Log;

    public UiTheme Theme => Themes.Current;
}
