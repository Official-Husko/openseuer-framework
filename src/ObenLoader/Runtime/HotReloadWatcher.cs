using System.Collections.Generic;
using ObenLoader.Mods;
using UnityEngine;

namespace ObenLoader.Runtime;

/// <summary>
/// Polls the DLLs of hot-reload mods once a second and reloads a mod once its files have been
/// stable for one poll (so a half-copied build is never picked up). Polling is used instead of
/// FileSystemWatcher because the latter is unreliable under Wine.
/// </summary>
internal sealed class HotReloadWatcher
{
    private const float PollInterval = 1f;

    private readonly Dictionary<ModInfo, System.DateTime> _seenChange = new Dictionary<ModInfo, System.DateTime>();
    private readonly List<ModInfo> _ready = new List<ModInfo>();
    private float _nextPoll;

    public bool Enabled { get; set; } = true;

    public void Tick()
    {
        if (!Enabled || Time.unscaledTime < _nextPoll)
            return;
        _nextPoll = Time.unscaledTime + PollInterval;

        _ready.Clear();
        var mods = ModRegistry.Mods;
        for (int i = 0; i < mods.Count; i++)
        {
            var mod = mods[i];
            if (!mod.CanHotReload || mod.State != ModState.Loaded)
                continue;

            System.DateTime written;
            try { written = AssemblyLoading.LastWrite(mod); }
            catch (System.IO.IOException) { continue; }

            if (written <= mod.LoadedWriteTime)
            {
                _seenChange.Remove(mod);
                continue;
            }

            // First sighting: wait one more poll to make sure the copy finished.
            if (!_seenChange.TryGetValue(mod, out var previous) || previous != written)
            {
                _seenChange[mod] = written;
                continue;
            }

            _seenChange.Remove(mod);
            _ready.Add(mod);
        }

        // One batch: a solution build usually changes the framework and several mods at once.
        if (_ready.Count > 0)
            ModRegistry.ReloadSet(_ready.ToArray());
    }
}
