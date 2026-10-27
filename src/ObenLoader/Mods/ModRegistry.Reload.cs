using System;
using System.Collections.Generic;
using System.Linq;
using ObenLoader.Runtime;

namespace ObenLoader.Mods;

/// <summary>Hot reload: swapping a mod's code while the game keeps running.</summary>
public static partial class ModRegistry
{
    internal static HotReloadWatcher Watcher { get; } = new HotReloadWatcher();

    /// <summary>Reload hot-reload mods by themselves when their DLLs change. Saved to settings.</summary>
    public static bool AutoReload
    {
        get => _settings.Value.AutoReload;
        set
        {
            _settings.Value.AutoReload = value;
            _settings.Save();
            Watcher.Enabled = value;
        }
    }

    /// <summary>Reloads a mod and everything loaded that depends on it.</summary>
    public static void Reload(string id, Action<string> done = null) => Queue(id, done, ModLifecycle.Reload);

    /// <summary>Reloads every loaded hot-reload mod as one batch.</summary>
    public static void ReloadAll(Action<string> done = null) =>
        RunReload(() => All.Where(m => m.State == ModState.Loaded && m.CanHotReload).ToList(), done);

    /// <summary>Reloads several mods (plus everything built on them) as one batch.</summary>
    internal static void ReloadSet(IReadOnlyList<ModInfo> mods, Action<string> done = null) =>
        RunReload(() => mods, done);

    private static void RunReload(Func<IReadOnlyList<ModInfo>> pick, Action<string> done)
    {
        MainThread.Post(() =>
        {
            string error;
            try { error = ModLifecycle.ReloadSet(pick()); }
            catch (Exception e)
            {
                error = e.Message;
                _log.Error("Reload failed", e);
            }
            if (error != null)
                _log.Warn(error);
            done?.Invoke(error);
        });
    }
}
