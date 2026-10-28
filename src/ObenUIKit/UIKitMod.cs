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
        ModMenu.Init(this);
        ModMenu.AddPage(this, ModsPage.Create());
        ModMenu.AddPage(this, SettingsPage.Create());
        ModMenu.AddPage(this, HotkeysPage.Create());
        ModMenu.AddPage(this, WhatsNewPage.Create());
        ModMenu.AddPage(this, ErrorsPage.Create());

        var config = LoadConfig<KitConfig>();
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
        Log.Info("Ready.");
    }

    public override void OnUpdate() => Hotkeys.Tick();

    public override void OnUnload() => Instance = null;
}
