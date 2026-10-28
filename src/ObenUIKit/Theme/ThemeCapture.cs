using System;
using ObenFramework.Game;
using ObenLoader.Logging;

namespace ObenUIKit.Theme;

/// <summary>
/// Keeps two captured looks, one from the main menu and one from a level's pause menu, and hands
/// out the one that fits where the player is. Captured values are assets and plain colours, so
/// they stay usable after the scene they came from is gone.
/// </summary>
internal sealed class ThemeCapture
{
    private readonly ModLogger _log;
    private UiTheme _menu;
    private UiTheme _world;
    private UiTheme _fallback;

    public event Action Changed;

    public ThemeCapture(ModLogger log)
    {
        _log = log;
    }

    public UiTheme Current
    {
        get
        {
            var preferred = WorldState.InWorld ? _world ?? _menu : _menu ?? _world;
            return preferred ?? Fallback();
        }
    }

    public void CaptureMainMenu() => Capture(ThemeLevel.MainMenu, ThemeProbes.MainMenu, ref _menu);

    public void CaptureWorld() => Capture(ThemeLevel.World, ThemeProbes.World, ref _world);

    private void Capture(ThemeLevel level, Func<UiTheme, bool> probe, ref UiTheme slot)
    {
        var theme = new UiTheme { Level = level };
        try
        {
            if (!probe(theme))
            {
                _log.Debug($"No {level} UI to take the look from yet");
                return;
            }
            ThemeProbes.FontByName(theme);
        }
        catch (Exception e)
        {
            _log.Warn($"Couldn't read the {level} look, keeping the previous one: {e.Message}");
            return;
        }

        slot = theme;
        _log.Info($"Took the UI look from the {level} ({(theme.Font != null ? theme.Font.name : "no font")})");
        Changed?.Invoke();
    }

    private UiTheme Fallback()
    {
        if (_fallback == null)
        {
            _fallback = new UiTheme { Level = ThemeLevel.Fallback };
            ThemeProbes.FontByName(_fallback);
        }
        return _fallback;
    }
}
