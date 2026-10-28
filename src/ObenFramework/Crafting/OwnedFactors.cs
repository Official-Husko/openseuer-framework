using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>
/// A <see cref="FactorTable"/> whose entries belong to mods: everything a mod set is removed when it unloads.
/// Each owner is tracked once however many factors it sets.
/// </summary>
internal sealed class OwnedFactors
{
    private readonly HashSet<ITracker> _tracked = new HashSet<ITracker>();

    public FactorTable Table { get; } = new FactorTable();

    public void Set(ITracker owner, string key, float factor)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        Table.Set(owner, key, factor);
        if (_tracked.Add(owner))
            owner.Track(Disposable.Create(() => Forget(owner)));
    }

    public bool Remove(ITracker owner, string key) => owner != null && Table.Remove(owner, key);

    public int Clear(ITracker owner) => owner != null ? Table.RemoveOwner(owner) : 0;

    private void Forget(ITracker owner)
    {
        Table.RemoveOwner(owner);
        _tracked.Remove(owner);
    }
}
