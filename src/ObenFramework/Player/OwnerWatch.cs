using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>
/// Remembers which mods registered something with an API and runs a cleanup for a mod when it unloads.
/// Each mod is tracked once, however often it calls the API, so a slider that sets a value every frame
/// does not pile up cleanup entries on the mod.
/// </summary>
internal sealed class OwnerWatch
{
    private readonly HashSet<string> _owners = new HashSet<string>();
    private readonly Action<string> _onGone;

    /// <param name="onGone">Called with the mod id when a watched mod unloads.</param>
    public OwnerWatch(Action<string> onGone)
    {
        _onGone = onGone;
    }

    /// <summary>Starts watching <paramref name="owner"/> if it isn't yet. Returns its id.</summary>
    public string Watch(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        string id = owner.Info.Id;
        if (_owners.Add(id))
        {
            owner.Track(Disposable.Create(() =>
            {
                if (_owners.Remove(id))
                    _onGone(id);
            }));
        }
        return id;
    }

    /// <summary>Forgets every mod without running the cleanups (the framework itself is going away).</summary>
    public void Clear() => _owners.Clear();
}
