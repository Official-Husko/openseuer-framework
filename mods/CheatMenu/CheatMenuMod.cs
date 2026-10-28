using System;
using CheatMenu.Cheats;
using CheatMenu.Sections;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Runtime;
using ObenUIKit;
using ObenUIKit.Menu;
using ObenUIKit.Windows;
using UnityEngine;

namespace CheatMenu;

/// <summary>
/// Cheats in one kit window on a hotkey (Insert by default) and as a page in the MODS menu.
/// Every cheat goes through the framework or the game's own public routines.
/// </summary>
public sealed class CheatMenuMod : ObenMod
{
    public const string WindowId = "cheatmenu.window";

    private ConfigFile<CheatMenuConfig> _config;
    private CheatContext _context;
    private UiWindow _window;
    private KeyCode _key = KeyCode.Insert;

    public override void OnLoad()
    {
        _config = LoadConfig<CheatMenuConfig>();
        _config.Value.Normalise();
        _config.Save(); // Writes the defaults on first run so the file is easy to find.
        _config.Changed += _ => ReadKey();
        ReadKey();

        _context = new CheatContext(this, _config);
        ModMenu.AddPage(this, new ModPage("Cheats", ui => CheatPanel.Build(ui, _context)) { Order = 20 });

        // The framework keeps the multiplier across level loads and removes it when this mod unloads.
        MoveSpeed.Init(this, _config.Value.MoveSpeed);
        Track(Disposable.Create(_context.Flush));
    }

    public override void OnQuit() => _context.Flush();

    public override void OnUpdate()
    {
        _context.Tick();
        if (_key == KeyCode.None || !Input.GetKeyDown(_key))
            return;

        if (_window != null && _window.IsOpen)
            _window.Close();
        else if (OpenGate.CanOpen())
            Open();
    }

    /// <summary>Opens the cheat window if it isn't open. The hotkey checks are the caller's business.</summary>
    public void Open()
    {
        try
        {
            _window = UiKit.Open(this, WindowId, "Cheats", ui => CheatPanel.Build(ui, _context));
        }
        catch (Exception e)
        {
            Log.Error("Couldn't open the cheat window", e);
        }
    }

    private void ReadKey()
    {
        string name = _config.Value.Key;
        if (Enum.TryParse(name, true, out KeyCode parsed) && parsed != KeyCode.None)
        {
            _key = parsed;
            return;
        }
        Log.Warn($"'{name}' isn't a key name; using Insert.");
        _key = KeyCode.Insert;
    }
}
