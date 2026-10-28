using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.World;

public static partial class Landmarks
{
    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<LandmarkFile>(framework, "landmarks");
        _save.DataLoaded.Subscribe(framework, data => LandmarkStore.Clean(data));
        LandmarkStore.Clean(_save.Value);

        LandmarkRestore.Create(framework);
        GameEvents.WhenWorldReady(framework, _ => LandmarkRestore.Wait());
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, ForgetScene);
        ModRegistry.ModUnloading += OnModUnloading;
        ModRegistry.ModLoaded += OnModLoaded;
        framework.Track(Disposable.Create(() =>
        {
            ModRegistry.ModUnloading -= OnModUnloading;
            ModRegistry.ModLoaded -= OnModLoaded;
            foreach (string full in new List<string>(Custom.Keys))
                Withdraw(full);
            Custom.Clear();
            Found.Clear();
            ForgetScene();
            DestroyDot();
            _save = null;
            Log.Reset();
        }));
    }

    private static void OnModUnloading(ModInfo mod)
    {
        try { WithdrawMod(mod.Id); }
        catch (Exception e) { Log.Error("mod unloading", e); }
    }

    private static void OnModLoaded(ModInfo mod)
    {
        if (!WorldState.IsReady)
            return;
        try { Restore(id => id == mod.Id); }
        catch (Exception e) { Log.Error("mod loaded", e); }
    }
}

/// <summary>
/// Puts the saved mod landmarks back once the map has loaded: the map fills its lists a moment after a
/// load (MapController waits 0.7 s), so this waits for that. Disabled while there is nothing to wait for.
/// </summary>
internal sealed class LandmarkRestore : MonoBehaviour
{
    private static LandmarkRestore _instance;

    public static void Create(ObenMod framework)
    {
        _instance = framework.CreateObject("LandmarkRestore").AddComponent<LandmarkRestore>();
        _instance.enabled = false;
        framework.Track(Disposable.Create(() => _instance = null));
    }

    public static void Wait()
    {
        if (_instance != null)
            _instance.enabled = true;
    }

    private void Update()
    {
        var controller = MapController.instance;
        if (controller == null)
        {
            enabled = false;
            return;
        }
        if (controller.loadingMap || SaveGate.IsBusy)
            return;
        enabled = false;
        try { Landmarks.Restore(); }
        catch (Exception e) { Landmarks.Log.Error("Restore", e); }
    }
}
