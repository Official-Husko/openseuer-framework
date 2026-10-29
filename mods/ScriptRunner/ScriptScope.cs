using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using UnityEngine;

namespace ScriptRunner;

/// <summary>
/// Everything one named script set up: its Harmony patches, objects, coroutines and anything it
/// tracked. Running the script again under the same name tears the old scope down first, so a
/// script can be edited and re-run as often as needed without piling up patches.
/// </summary>
public sealed class ScriptScope : ITracker, IDisposable
{
    private readonly List<IDisposable> _tracked = new List<IDisposable>();
    private Harmony _harmony;

    public string Name { get; }
    public ModLogger Log { get; }

    /// <summary>Harmony instance for this script only; unpatched when the scope goes away.</summary>
    public Harmony Harmony => _harmony ??= new Harmony("script:" + Name);

    internal ScriptScope(string name)
    {
        Name = name;
        Log = new ModLogger("script:" + name);
    }

    public T Track<T>(T disposable) where T : IDisposable
    {
        _tracked.Add(disposable);
        return disposable;
    }

    /// <summary>Applies every [HarmonyPatch] class in the script.</summary>
    public void PatchAll(Type anyTypeInScript) => Harmony.PatchAll(anyTypeInScript.Assembly);

    /// <summary>A GameObject that survives scene changes and goes away with the scope.</summary>
    public GameObject CreateObject(string name)
    {
        var go = new GameObject($"script:{Name}:{name}");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Track(Disposable.Destroying(go));
        return go;
    }

    /// <summary>Runs a coroutine until it ends or the scope goes away.</summary>
    public Coroutine StartCoroutine(IEnumerator routine)
    {
        var host = ScriptRunnerMod.Instance.Host;
        var coroutine = host.StartCoroutine(routine);
        Track(Disposable.Create(() =>
        {
            if (host != null && coroutine != null)
                host.StopCoroutine(coroutine);
        }));
        return coroutine;
    }

    public void Dispose()
    {
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            try { _tracked[i].Dispose(); }
            catch (Exception e) { Log.Error("Cleanup failed", e); }
        }
        _tracked.Clear();
        if (_harmony != null)
            Unpatching.RemoveAll(_harmony.Id, (method, e) => Log.Warn($"Taking the script's patch off {method.Name} failed: {e.Message}"));
    }
}
