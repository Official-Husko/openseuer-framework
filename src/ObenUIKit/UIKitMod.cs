using ObenFramework.SceneLook;
using ObenFramework.Ui;
using ObenLoader;
using ObenLoader.Mods;
using ObenUIKit.Computer;
using ObenUIKit.Controls;
using ObenUIKit.Input;
using ObenUIKit.Menu;
using ObenUIKit.Menu.Changelog;
using ObenUIKit.Menu.Pages;
using ObenUIKit.Menu.Settings;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;

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
        // The menu font draws "<" and ">" as stray marks; text the kit shows as it is borrows TextMeshPro's.
        KitText.BracketFont = TMPro.TMP_Settings.defaultFontAsset != null ? TMPro.TMP_Settings.defaultFontAsset.name : null;
        ModSettings.Init(this);
        Track(ObenLoader.Runtime.Disposable.Create(UiSprites.Clear));
        ModMenu.Init(this);
        ModMenu.AddPage(this, ModsPage.Create());
        ModMenu.AddPage(this, SettingsPage.Create());
        ModMenu.AddPage(this, HotkeysPage.Create());
        ModMenu.AddPage(this, WhatsNewPage.Create());
        ModMenu.AddPage(this, ErrorsPage.Create());
        ModMenu.AddPage(this, ReportPage.Create());

        var config = LoadConfig<KitConfig>();
        Settings = config.Value;
        config.Changed += value => Settings = value;
        ModSettings.Register(this, config, "UI kit");
        ModActions.Register(this, "Read What's New", () =>
        {
            ModMenu.Open(WhatsNewPage.Title);
            return null;
        }, "Opens the release notes of every mod.");
        // Shown with the framework, whose check it is; tracked here too, as the button's code is the kit's.
        Track(ModActions.Register(ModRegistry.Find("ObenFramework")?.Instance ?? this, "Run scene looks checks", SceneLooks.Checks,
            "Read only. Lists the character looks of the scene you're in, then every body and the NPC and character names, in the framework log.",
            group: "Scene looks"));
        ChangelogFeed.Init(this);
        WhatsNewPrompt.Init(this, config);
        MainMenuEntry.Init(this);
        MainMenuButtons.Init(this);
        PauseMenuEntry.Init(this);
        PauseMenuButtons.Init(this);
        PauseMenuVersion.Init(this);
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
