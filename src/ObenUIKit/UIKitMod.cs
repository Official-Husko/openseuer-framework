using ObenFramework.Ui;
using ObenLoader;
using ObenUIKit.Computer;
using ObenUIKit.Input;
using ObenUIKit.Menu;
using ObenUIKit.Menu.Changelog;
using ObenUIKit.Menu.Pages;
using ObenUIKit.Menu.Settings;
using ObenUIKit.Runtime;

namespace ObenUIKit;

/// <summary>Entry point of the UI kit. It's essential: the mod menu that switches other mods lives here.</summary>
public sealed class UIKitMod : ObenMod
{
    internal static UIKitMod Instance { get; private set; }

    /// <summary>The kit's own settings (defaults while it isn't loaded).</summary>
    internal static KitConfig Settings { get; private set; } = new KitConfig();

    public override void OnLoad()
    {
        Instance = this;
        InputLock.Init(this);
        // The framework's UiState answers for every UI mod; the kit reports its windows and typing.
        UiState.Report(this, UiActivity.ModWindow, () => UiKit.AnyWindowOpen);
        UiState.Report(this, UiActivity.Typing, () => InputFocus.Busy);
        Track(new KitRuntime(this));
        Track(new HudRuntime(KitRuntime.Current.Kit));
        Track(new PromptRuntime(KitRuntime.Current.Kit));
        Track(new GameHudLeases(this));
        Hotkeys.Init(this);
        ModSettings.Init(this);
        ModMenu.Init(this);
        ModMenu.AddPage(this, ModsPage.Create());
        ModMenu.AddPage(this, SettingsPage.Create());
        ModMenu.AddPage(this, HotkeysPage.Create());
        ModMenu.AddPage(this, WhatsNewPage.Create());
        ModMenu.AddPage(this, ErrorsPage.Create());

        var config = LoadConfig<KitConfig>();
        Settings = config.Value;
        config.Changed += value => Settings = value;
        ModSettings.Register(this, config, "UI kit");
        ModActions.Register(this, "Read What's New", () =>
        {
            ModMenu.Open(WhatsNewPage.Title);
            return null;
        }, "Opens the release notes of every mod.");
        ChangelogFeed.Init(this);
        WhatsNewPrompt.Init(this, config);
        MainMenuEntry.Init(this);
        MainMenuButtons.Init(this);
        PauseMenuEntry.Init(this);
        ComputerApps.Init(this);
        GameCheckNotice.Init(this);
        MenuStatusLine.Init(this);
        Log.Info("Ready.");
    }

    public override void OnUpdate() => Hotkeys.Tick();

    // A slider dragged just before the game closes still gets written.
    public override void OnQuit() => ModSettings.FlushAll();

    public override void OnUnload()
    {
        Instance = null;
        Settings = new KitConfig();
    }
}
