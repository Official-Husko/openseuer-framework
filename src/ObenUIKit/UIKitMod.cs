using ObenLoader;
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
        Track(new KitRuntime(this));
        Hotkeys.Init(this);
        ModMenu.Init(this);
        ModMenu.AddPage(this, ModsPage.Create());
        ModMenu.AddPage(this, SettingsPage.Create());
        ModMenu.AddPage(this, HotkeysPage.Create());
        ModMenu.AddPage(this, WhatsNewPage.Create());
        ModMenu.AddPage(this, ErrorsPage.Create());

        var config = LoadConfig<KitConfig>();
        ModSettings.Register(this, config, "UI kit");
        ChangelogFeed.Init(this);
        WhatsNewPrompt.Init(this, config);
        MainMenuEntry.Init(this);
        PauseMenuEntry.Init(this);
        Log.Info("Ready.");
    }

    public override void OnUpdate() => Hotkeys.Tick();

    public override void OnUnload() => Instance = null;
}
