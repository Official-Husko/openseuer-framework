using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Features;

/// <summary>
/// A part of a mod the player can switch on and off on its own (a "submod"), for example
/// one game bug fix or one cheat. Harmony patch classes marked with
/// [HarmonyPatchCategory(Id)] are applied while the feature is on and removed when it's off.
/// Anything else is set up in <see cref="OnEnable"/> and cleaned up through <see cref="Track{T}"/>.
/// </summary>
public abstract class ModFeature : ITracker
{
    private readonly List<IDisposable> _tracked = new List<IDisposable>();

    /// <summary>Stable id, unique within the mod. Also the Harmony patch category name.</summary>
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual bool EnabledByDefault => true;

    /// <summary>Shown next to the switch when a change only applies later (e.g. "after the level reloads").</summary>
    public virtual string AppliesWhen => null;

    public ObenMod Owner { get; internal set; }

    public bool Enabled { get; internal set; }

    /// <summary>Called when the feature is switched on (also at load if it's on).</summary>
    protected virtual void OnEnable() { }

    /// <summary>Called when switched off or when the mod unloads. Tracked things are cleaned up after this.</summary>
    protected virtual void OnDisable() { }

    /// <summary>Called every frame while the feature is on, if overridden.</summary>
    public virtual void OnUpdate() { }

    /// <summary>Cleans this up when the feature is switched off.</summary>
    public T Track<T>(T disposable) where T : IDisposable
    {
        _tracked.Add(disposable);
        return disposable;
    }

    internal void Enable()
    {
        // Takes its own patches off again and throws when a class fails.
        ModPatches.ApplyCategory(Owner, GetType().Assembly, Id);
        try
        {
            OnEnable();
        }
        catch
        {
            // A feature that didn't come up is never switched off, so nothing of it may keep running.
            DisposeTracked();
            ModPatches.RemoveCategory(Owner, GetType().Assembly, Id);
            throw;
        }
        Enabled = true;
    }

    internal void Disable()
    {
        Enabled = false;
        try
        {
            OnDisable();
        }
        finally
        {
            DisposeTracked();
            Owner.Harmony.UnpatchCategory(GetType().Assembly, Id);
        }
    }

    private void DisposeTracked()
    {
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            try { _tracked[i].Dispose(); }
            catch (Exception e) { Owner.Log.Error($"Cleaning up feature {Id} failed", e); }
        }
        _tracked.Clear();
    }

    internal bool OverridesUpdate =>
        GetType().GetMethod(nameof(OnUpdate))?.DeclaringType != typeof(ModFeature);
}
