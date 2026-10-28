using System;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.World.Props;

/// <summary>
/// Keeps the world's props in step with scenes and mods: builds them when a world is ready, takes them
/// down before a level change and when their mod unloads, and builds them again when it loads.
/// </summary>
internal static class PropsModule
{
    public static void Init(ObenMod framework)
    {
        WorldProps.Init(framework);
        PropRetry.Create(framework);
        GameEvents.WhenWorldReady(framework, Restore);
        GameEvents.LevelChanging.Subscribe(framework, WorldProps.DespawnAll);
        GameEvents.MainMenu.Subscribe(framework, WorldProps.DespawnAll);

        SceneManager.sceneUnloaded += OnSceneUnloaded;
        ModRegistry.ModUnloading += OnModUnloading;
        ModRegistry.ModLoaded += OnModLoaded;
        framework.Track(Disposable.Create(() =>
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            ModRegistry.ModUnloading -= OnModUnloading;
            ModRegistry.ModLoaded -= OnModLoaded;
            PropHolder.Destroy();
        }));
    }

    private static void Restore(string scene)
    {
        if (WorldProps.Restore(scene) > 0)
            PropRetry.Schedule();
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        try
        {
            foreach (var info in WorldProps.All(scene.name))
            {
                if (info.Object != null)
                    UnityEngine.Object.Destroy(info.Object);
            }
        }
        catch (Exception e) { WorldProps.Log.Error("scene unloaded", e); }
    }

    private static void OnModUnloading(ModInfo mod)
    {
        try { WorldProps.DespawnMod(mod.Id); }
        catch (Exception e) { WorldProps.Log.Error("mod unloading", e); }
    }

    // A mod that loaded mid-game may own props in this scene, or the models other props use.
    private static void OnModLoaded(ModInfo mod)
    {
        if (WorldState.IsReady)
            PropRetry.Schedule();
    }
}

/// <summary>
/// Tries again, once a second for a while, to build props that are waiting for a model (models load in
/// the background, often after the world is ready). Disabled, and free, while nothing waits.
/// </summary>
internal sealed class PropRetry : MonoBehaviour
{
    private const float Interval = 1f;
    private const float GiveUpAfter = 30f;

    private static PropRetry _instance;
    private float _next;
    private float _until;

    public static void Create(ObenMod framework)
    {
        _instance = framework.CreateObject("PropRetry").AddComponent<PropRetry>();
        _instance.enabled = false;
        framework.Track(Disposable.Create(() => _instance = null));
    }

    public static void Schedule()
    {
        if (_instance == null)
            return;
        _instance._next = Time.realtimeSinceStartup;
        _instance._until = Time.realtimeSinceStartup + GiveUpAfter;
        _instance.enabled = true;
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        if (now < _next)
            return;
        _next = now + Interval;
        if (!WorldState.IsReady)
            return;
        int waiting = 0;
        try { waiting = WorldProps.Restore(WorldState.SceneName); }
        catch (Exception e) { WorldProps.Log.Error("retry", e); }
        if (waiting == 0 || now > _until)
            enabled = false;
    }
}
