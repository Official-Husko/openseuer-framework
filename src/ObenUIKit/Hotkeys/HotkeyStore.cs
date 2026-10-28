using System.Collections.Generic;
using ObenLoader.Config;
using ObenLoader.Logging;

namespace ObenUIKit;

/// <summary>The shape of hotkeys.json: "mod id/hotkey id" to a chord such as "Ctrl+F5" ("" is unbound).</summary>
internal sealed class HotkeyFile
{
    public Dictionary<string, string> Bindings = new Dictionary<string, string>();
}

/// <summary>Reads and writes the players' chosen hotkeys. Only choices that differ from the default are kept.</summary>
internal sealed class HotkeyStore
{
    private readonly ConfigFile<HotkeyFile> _file;
    private readonly ModLogger _log;

    public HotkeyStore(ConfigFile<HotkeyFile> file, ModLogger log)
    {
        _file = file;
        _log = log;
        _file.Value.Bindings ??= new Dictionary<string, string>();
    }

    /// <summary>The chord the player picked for this hotkey, if any.</summary>
    public bool TryGet(Hotkey hotkey, out KeyChord chord)
    {
        chord = default;
        if (!_file.Value.Bindings.TryGetValue(hotkey.StoreKey, out string text))
            return false;
        if (KeyChord.TryParse(text, out chord))
            return true;
        _log.Warn($"Ignoring the saved key '{text}' for {hotkey.StoreKey}, it isn't a key.");
        return false;
    }

    public void Save(Hotkey hotkey)
    {
        var bindings = _file.Value.Bindings;
        if (hotkey.IsDefault)
            bindings.Remove(hotkey.StoreKey);
        else
            bindings[hotkey.StoreKey] = hotkey.Chord.Serialize();
        _file.Save();
    }
}
