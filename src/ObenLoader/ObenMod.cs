using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using ObenLoader.Config;
using ObenLoader.Logging;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenLoader;

/// <summary>
/// Base class for a mod's entry point. The loader creates one instance per load; a hot reload
/// tears the old instance down (<see cref="OnUnload"/>) and creates a new one.
/// Override only the callbacks you need; the host skips the rest.
/// </summary>
public abstract class ObenMod : ITracker
{
    private readonly List<IDisposable> _tracked = new List<IDisposable>();
    private Harmony _harmony;

    public ModInfo Info { get; internal set; }
    public ModLogger Log { get; internal set; }

    /// <summary>The mod's folder inside &lt;game&gt;/Mods.</summary>
    public string Directory => Info.Directory;

    /// <summary>Harmony instance named after the mod id. Created on first use, unpatched on unload.</summary>
    public Harmony Harmony => _harmony ??= new Harmony(Info.Id);

    /// <summary>Set once OnLoad finished, so a failed load doesn't run OnUnload.</summary>
    internal bool Started { get; set; }

    /// <summary>Path inside the mod folder.</summary>
    public string PathTo(string relative) => Path.Combine(Info.Directory, relative);

    /// <summary>Loads (or creates) &lt;mod folder&gt;/&lt;fileName&gt; as typed settings.</summary>
    public ConfigFile<T> LoadConfig<T>(string fileName = "config.json") where T : class, new()
        => new ConfigFile<T>(PathTo(fileName), Log);

    /// <summary>
    /// Registers something to dispose when the mod unloads (event subscriptions, spawned objects,
    /// registrations). Framework APIs that take the mod as owner do this for you.
    /// </summary>
    public T Track<T>(T disposable) where T : IDisposable
    {
        _tracked.Add(disposable);
        return disposable;
    }

    /// <summary>
    /// Creates a GameObject that survives scene changes and is destroyed when the mod unloads.
    /// Handy as a home for the mod's own MonoBehaviours.
    /// </summary>
    public GameObject CreateObject(string name)
    {
        var go = new GameObject($"{Info.Id}:{name}");
        UnityEngine.Object.DontDestroyOnLoad(go);
        Track(Disposable.Destroying(go));
        return go;
    }

    /// <summary>Runs a coroutine on the loader's host object. It is stopped when the mod unloads.</summary>
    public Coroutine StartCoroutine(IEnumerator routine)
    {
        var host = ModHost.Instance;
        var coroutine = host.StartCoroutine(routine);
        Track(Disposable.Create(() =>
        {
            if (host != null && coroutine != null)
                host.StopCoroutine(coroutine);
        }));
        return coroutine;
    }

    /// <summary>Called once per load, right after the assemblies are loaded. On first launch no scene exists yet.</summary>
    public virtual void OnLoad() { }

    /// <summary>Called after every mod in the same batch finished <see cref="OnLoad"/>.</summary>
    public virtual void OnAllLoaded() { }

    /// <summary>Called before the mod is unloaded or reloaded. Undo anything not covered by <see cref="Track{T}"/>.</summary>
    public virtual void OnUnload() { }

    public virtual void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }
    public virtual void OnUpdate() { }
    public virtual void OnLateUpdate() { }
    public virtual void OnFixedUpdate() { }
    public virtual void OnGUI() { }
    public virtual void OnQuit() { }

    internal void DisposeTracked()
    {
        for (int i = _tracked.Count - 1; i >= 0; i--)
        {
            try { _tracked[i].Dispose(); }
            catch (Exception e) { Log.Error("Cleanup failed", e); }
        }
        _tracked.Clear();
    }
}
