// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Features;
using ObenUIKit;
using ObenUIKit.Menu;
using UnityEngine;

namespace CheatMenu.Dev;

/// <summary>
/// The developer menu, off by default: while on, F8 (rebindable on the Hotkeys page) opens it, the MODS menu
/// gets a Developer page with the same content, and the Cheats window links to it. Off, none of it exists.
/// </summary>
internal sealed class DevMenuFeature : ModFeature
{
    public const string Key = "DevMenu";

    private readonly CheatContext _ctx;

    public DevMenuFeature(CheatContext ctx) => _ctx = ctx;

    /// <summary>The feature while it is on, else null.</summary>
    internal static DevMenuFeature Current { get; private set; }

    public override string Id => Key;
    public override string Name => "Developer menu";
    public override string Description =>
        "F8 opens a menu for testing: quests, global flags, watches (also over the game with F9), the session and story state, player and world tools, NPCs and the log. Changes mark the save.";
    public override bool EnabledByDefault => false;

    internal void Open() => DevWindow.Toggle(_ctx);

    protected override void OnEnable()
    {
        DevWindow.Init(this);
        Hotkeys.Register(this, "dev", "Open or close the developer menu", KeyCode.F8, Open, scope: HotkeyScope.Anywhere);
        Hotkeys.Register(this, "watches", "Show or hide the watches over the game", KeyCode.F9, WatchHud.Toggle);
        WatchHud.Init(this, _ctx);
        ModMenu.AddPage(this, new ModPage("Developer", ui => DevWindow.Build(ui, _ctx)) { Order = 21 });
        Current = this;
    }

    protected override void OnDisable()
    {
        Current = null;
        DevWindow.Init(null);
        WatchHud.Init(null, _ctx);
    }
}
