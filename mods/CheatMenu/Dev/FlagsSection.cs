// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Env;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// The game's global flags (story and tenement switches saved with the game), through the framework's
/// <see cref="GlobalFlags"/>: filter them by name, switch one on or off, or set a new one. A flag a mod
/// forces shows its forced value; setting it changes what it falls back to.
/// </summary>
internal static class FlagsSection
{
    private const int MaxRows = 30;

    private static string _filter = "";
    private static string _newFlag = "";

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Global flags");
        if (!GlobalFlags.IsAvailable)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        ui.TextField("Filter (Enter to apply)", _filter, t => _filter = t, t =>
        {
            _filter = t;
            ui.Window.Rebuild();
        });

        var keys = GlobalFlags.Keys();
        var shown = DevRules.Page(keys, _filter, MaxRows, out int matched);
        ui.Label($"Showing {shown.Count} of {matched} matching flags ({keys.Count} in this save). A flag the game never set reads as off.", muted: true);
        foreach (string key in shown)
        {
            string flag = key;
            string label = GlobalFlags.IsForced(flag) ? flag + " (forced by a mod)" : flag;
            ui.Toggle(label, GlobalFlags.Get(flag), on => DevActions.Change(ctx, ui, $"{flag} {(on ? "on" : "off")}", () => GlobalFlags.Set(flag, on)));
        }

        ui.TextField("Set a flag by name", _newFlag, t => _newFlag = t);
        ui.Row(r =>
        {
            r.Button("Set it on", () => SetNew(ctx, ui, true));
            r.Button("Set it off", () => SetNew(ctx, ui, false));
        });
    }

    private static void SetNew(CheatContext ctx, UiBuilder ui, bool on)
    {
        string flag = (_newFlag ?? "").Trim();
        if (flag.Length == 0)
        {
            ctx.Say("Type the flag's name first.");
            return;
        }
        _filter = flag;
        DevActions.Change(ctx, ui, $"{flag} {(on ? "on" : "off")}", () => GlobalFlags.Set(flag, on));
    }
}
