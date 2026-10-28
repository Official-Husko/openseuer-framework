using System;
using System.Collections.Generic;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.Env;

/// <summary>A flag that went on or off, appeared or was removed, as raised by <see cref="GlobalFlags.FlagChanged"/>.</summary>
public readonly struct FlagChange
{
    public FlagChange(string key, bool active, bool wasActive, bool exists, ChangeSource source)
    {
        Key = key;
        Active = active;
        WasActive = wasActive;
        Exists = exists;
        Source = source;
    }

    public string Key { get; }

    /// <summary>The flag's value after the change, as <see cref="GlobalFlags.Get"/> reads it.</summary>
    public bool Active { get; }

    public bool WasActive { get; }

    /// <summary>False when the change removed the flag.</summary>
    public bool Exists { get; }

    public ChangeSource Source { get; }
}

/// <summary>
/// The game's global flags (<c>GlobalState</c>): named on/off switches with an optional value, saved with the
/// game across every level. Story scripts, the tenement and quests set them; a missing flag reads as off.
/// <see cref="Set"/> changes a flag for good (it is saved). <see cref="Force"/> overrides one for as long as
/// the mod wants it: the game's own value is kept underneath, never reaches the save file, and comes back
/// when the mod releases it or unloads.
/// </summary>
public static class GlobalFlags
{
    /// <summary>
    /// A flag's value changed: the game or a mod set it, a force started or ended. Writes that leave the
    /// value as it was are not raised, and nothing is raised while a save loads.
    /// </summary>
    public static readonly WorldEvent<FlagChange> FlagChanged = new WorldEvent<FlagChange>(nameof(FlagChanged));

    /// <summary>True when the current scene has the global state (every gameplay scene).</summary>
    public static bool IsAvailable => GlobalState.instance != null;

    /// <summary>True when the flag exists and is on. False when it is missing or there is no world.</summary>
    public static bool Get(string key)
    {
        RequireKey(key);
        var store = GlobalState.instance;
        if (store == null)
            return false;
        var info = store.GetState(key);
        return info != null && info.active;
    }

    /// <summary>True when the flag has been set at some point, on or off.</summary>
    public static bool Exists(string key)
    {
        RequireKey(key);
        var store = GlobalState.instance;
        return store != null && store.state.ContainsKey(key);
    }

    /// <summary>The value stored with the flag, or null. Some game flags carry a number or a text.</summary>
    public static object GetData(string key)
    {
        RequireKey(key);
        var store = GlobalState.instance;
        var info = store != null ? store.GetState(key) : null;
        return info != null ? info.data : null;
    }

    /// <summary>Every flag name, sorted.</summary>
    public static List<string> Keys()
    {
        var keys = new List<string>();
        var store = GlobalState.instance;
        if (store != null)
            keys.AddRange(store.state.Keys);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    /// <summary>
    /// Turns a flag on or off for good, keeping its stored value, the way the game's scripts do. It is saved
    /// with the game. A forced flag keeps showing the forced value; this changes what it falls back to.
    /// </summary>
    public static void Set(string key, bool active = true)
    {
        RequireKey(key);
        var store = Store;
        using (WorldChanges.EnterMod())
        {
            var before = store.GetState(key);
            store.SetState(key, active, before != null ? before.data : null);
        }
    }

    /// <summary>Turns a flag on or off and replaces its stored value (any object the game's save can write).</summary>
    public static void Set(string key, bool active, object data)
    {
        RequireKey(key);
        var store = Store;
        using (WorldChanges.EnterMod())
            store.SetState(key, active, data);
    }

    /// <summary>
    /// Deletes a flag, so it reads as missing again. The game never does this itself; use it to undo a flag
    /// your mod made up. Not allowed on a forced flag. True when there was one.
    /// </summary>
    public static bool Remove(string key)
    {
        RequireKey(key);
        if (FlagOverrides.IsForced(key))
            throw new InvalidOperationException($"Flag '{key}' is forced by {FlagOverrides.OwnerOf(key)}; release it first.");
        var store = Store;
        bool wasActive = Get(key);
        if (!store.state.Remove(key))
            return false;
        FlagOverrides.Raise(key, false, wasActive, false, ChangeSource.Mod);
        return true;
    }

    /// <summary>
    /// Makes the flag read <paramref name="active"/> until <paramref name="owner"/> releases it or unloads.
    /// The game can still write the flag meanwhile; those writes are kept and show again when the force ends.
    /// Forced values are never written into the save. When several mods force one flag, the newest wins.
    /// </summary>
    public static void Force(ObenMod owner, string key, bool active)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        RequireKey(key);
        FlagOverrides.Force(owner, key, active);
    }

    /// <summary>Ends <paramref name="owner"/>'s force on the flag. True when it had one.</summary>
    public static bool Release(ObenMod owner, string key)
    {
        RequireKey(key);
        return owner != null && FlagOverrides.Release(owner, key);
    }

    /// <summary>True while some mod forces the flag.</summary>
    public static bool IsForced(string key) => key != null && FlagOverrides.IsForced(key);

    /// <summary>Every force in place, oldest first.</summary>
    public static List<OwnedValue<bool>> Forced() => FlagOverrides.Entries;

    private static GlobalState Store
    {
        get
        {
            var store = GlobalState.instance;
            if (store == null)
                throw new InvalidOperationException("There are no global flags here: not in a gameplay scene.");
            return store;
        }
    }

    private static void RequireKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("A flag name is required.", nameof(key));
    }
}
