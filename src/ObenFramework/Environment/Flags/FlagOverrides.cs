using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.Env;

/// <summary>
/// Keeps forced flags in the game's dictionary while remembering what the game had underneath. Values are
/// written straight into <c>GlobalState.state</c> so the forcing itself does not look like a game write.
/// The game's writes to a forced flag (see <see cref="FlagPatches"/>) update the remembered value; saving
/// swaps the remembered values in and back out, so a force never reaches the save file.
/// </summary>
internal static class FlagOverrides
{
    private sealed class Underneath
    {
        public GlobalState Store;
        public bool Existed;
        public bool Active;
    }

    private static readonly OwnedValues<bool> Forces = new OwnedValues<bool> { Released = Apply };
    private static readonly Dictionary<string, Underneath> Vanilla = new Dictionary<string, Underneath>();
    private static bool _suspended;

    public static List<OwnedValue<bool>> Entries => Forces.Entries;

    public static bool Any => !Forces.IsEmpty;

    public static bool IsForced(string key) => Forces.Has(key);

    public static bool TryGetForced(string key, out bool active) => Forces.TryGet(key, out active);

    public static string OwnerOf(string key)
    {
        var entries = Forces.Entries;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (entries[i].Key == key)
                return entries[i].OwnerId;
        }
        return null;
    }

    public static void Force(ObenMod owner, string key, bool active)
    {
        Forces.Set(owner, owner.Info.Id, key, active);
        Apply(key);
    }

    public static bool Release(ObenMod owner, string key)
    {
        if (!Forces.Remove(owner, key))
            return false;
        Apply(key);
        return true;
    }

    /// <summary>Writes the forced value for <paramref name="key"/>, or puts the game's value back when nobody forces it.</summary>
    public static void Apply(string key)
    {
        var store = GlobalState.instance;
        if (store == null || _suspended)
            return;

        if (!Forces.TryGet(key, out bool want))
        {
            Restore(store, key);
            return;
        }

        if (!Vanilla.TryGetValue(key, out var under) || under.Store != store)
        {
            var info = store.GetState(key);
            under = new Underneath { Store = store, Existed = info != null, Active = info != null && info.active };
            Vanilla[key] = under;
        }
        bool before = Read(store, key);
        Write(store, key, want);
        if (before != want)
            Raise(key, want, before, true, ChangeSource.Mod);
    }

    /// <summary>The game wrote a forced flag: keep what it wrote as the value underneath and force again.</summary>
    public static void GameWrote(GlobalState store, string key)
    {
        if (_suspended || !Forces.TryGet(key, out bool want))
            return;
        var info = store.GetState(key);
        Vanilla[key] = new Underneath { Store = store, Existed = info != null, Active = info != null && info.active };
        Write(store, key, want);
    }

    /// <summary>Forces again after a level load: the new scene's state came from the save, without the forces.</summary>
    public static void ReapplyAll()
    {
        foreach (string key in Forces.Keys())
            Apply(key);
    }

    /// <summary>Before the game serialises the flags: show it the values underneath.</summary>
    public static void SuspendForSave(GlobalState store)
    {
        if (_suspended || Forces.IsEmpty)
            return;
        foreach (string key in Forces.Keys())
        {
            if (Vanilla.TryGetValue(key, out var under) && under.Store == store)
                WriteUnderneath(store, key, under);
        }
        _suspended = true;
    }

    public static void ResumeAfterSave(GlobalState store)
    {
        if (!_suspended)
            return;
        _suspended = false;
        foreach (string key in Forces.Keys())
        {
            // A force whose value underneath was never read from this store is applied by ReapplyAll instead.
            if (Forces.TryGet(key, out bool want) && Vanilla.TryGetValue(key, out var under) && under.Store == store)
                Write(store, key, want);
        }
    }

    /// <summary>The framework unloads: put every forced flag back.</summary>
    public static void RestoreAll()
    {
        _suspended = false;
        var store = GlobalState.instance;
        var keys = Forces.Keys();
        Forces.Clear();
        if (store != null)
        {
            foreach (string key in keys)
                Restore(store, key);
        }
        Vanilla.Clear();
    }

    internal static void Raise(string key, bool active, bool wasActive, bool exists, ChangeSource source)
    {
        if (GlobalFlags.FlagChanged.HasSubscribers && !WorldState.IsLoadingSave)
            GlobalFlags.FlagChanged.Raise(new FlagChange(key, active, wasActive, exists, source));
    }

    private static void Restore(GlobalState store, string key)
    {
        if (!Vanilla.TryGetValue(key, out var under))
            return;
        Vanilla.Remove(key);
        if (under.Store != store)
            return;
        bool before = Read(store, key);
        WriteUnderneath(store, key, under);
        bool after = Read(store, key);
        if (before != after || !under.Existed)
            Raise(key, after, before, under.Existed, ChangeSource.Mod);
    }

    private static void WriteUnderneath(GlobalState store, string key, Underneath under)
    {
        if (under.Existed)
            Write(store, key, under.Active);
        else
            store.state.Remove(key);
    }

    private static bool Read(GlobalState store, string key) => store.state.TryGetValue(key, out var info) && info != null && info.active;

    private static void Write(GlobalState store, string key, bool active)
    {
        if (store.state.TryGetValue(key, out var info) && info != null)
            info.active = active;
        else
            store.state[key] = new GlobalState.GlobalStateInfo(active);
    }
}
