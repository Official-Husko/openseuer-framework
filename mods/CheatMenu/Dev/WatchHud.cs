// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.Text;
using ObenLoader.Runtime;
using ObenUIKit;

namespace CheatMenu.Dev;

/// <summary>
/// The watches over the game: the player's watches and the ones mods added, in a HUD readout at the top
/// left (the reference's floating watch panel). It shows while the developer menu is on and the Watches
/// part's "Show them over the game" is set, which is kept in the config; F9 switches it. Like every
/// HUD piece it steps aside while a window or the pause menu is open.
/// </summary>
internal static class WatchHud
{
    private const int MaxText = 160;

    private static readonly List<LiveWatch> Resolved = new List<LiveWatch>();
    private static readonly List<string> Broken = new List<string>();
    private static readonly StringBuilder Text = new StringBuilder();
    private static HudReadout _readout;
    private static CheatContext _ctx;
    private static ITracker _owner;
    private static bool _stale = true;

    public static bool IsShown => _readout != null && _readout.IsActive;

    /// <summary>Called when the developer menu goes on (with its owner) or off (null).</summary>
    public static void Init(ITracker owner, CheatContext ctx)
    {
        _owner = owner;
        _ctx = ctx;
        _readout = null;
        _stale = true;
        if (owner != null && ctx.Config.Value.WatchesOnScreen)
            Show();
    }

    /// <summary>Shows or hides the box and remembers the choice.</summary>
    public static void Set(bool on)
    {
        if (_ctx == null)
            return;
        _ctx.Config.Value.WatchesOnScreen = on;
        _ctx.Config.Save();
        if (on)
            Show();
        else
            Hide();
    }

    public static void Toggle() => Set(!IsShown);

    /// <summary>The player's watch list changed: resolve it again on the next read.</summary>
    public static void Changed() => _stale = true;

    private static void Show()
    {
        if (IsShown || _owner == null)
            return;
        _readout = Hud.Readout(_owner, "Watches", Compose);
        if (_readout == null)
            _ctx.Say("The HUD already has four readouts; close one of them first.");
    }

    private static void Hide()
    {
        _readout?.Remove();
        _readout = null;
    }

    private static string Compose()
    {
        if (_stale)
            Resolve();
        Text.Clear();
        foreach (var watch in Resolved)
            Line(watch.Key, watch.Read());
        foreach (string key in Broken)
            Line(key, "can't be read");
        foreach (var watch in DevMenu.Watches)
            Line(watch.ModId + " " + watch.Name, WatchesSection.Read(watch));
        return Text.Length == 0 ? "Nothing is watched. Add watches in the developer menu (F8)." : Text.ToString();
    }

    private static void Line(string name, string value)
    {
        if (Text.Length > 0)
            Text.Append('\n');
        Text.Append(name).Append(" = ").Append(DevRules.Cut(value, MaxText));
    }

    // Finding types and members is the expensive part, so it happens once per change of the list.
    private static void Resolve()
    {
        _stale = false;
        Resolved.Clear();
        Broken.Clear();
        foreach (string key in _ctx.Config.Value.Watches)
        {
            LiveWatch watch = null;
            if (DevRules.TrySplitWatch(key, out string type, out string path))
                watch = LiveWatch.Create(type, path, out _);
            if (watch != null)
                Resolved.Add(watch);
            else
                Broken.Add(key);
        }
    }
}
