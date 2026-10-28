using System;
using System.Collections;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;

namespace ObenFramework.World;

public static partial class Landmarks
{
    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<LandmarkFile>(framework, "landmarks");
        _save.DataLoaded.Subscribe(framework, data => LandmarkStore.Clean(data));
        LandmarkStore.Clean(_save.Value);

        GameEvents.WhenWorldReady(framework, _ => framework.StartCoroutine(RestoreWhenMapLoads()));
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

    // The map fills its lists a moment after a load (MapController waits 0.7 s); wait for it.
    private static IEnumerator RestoreWhenMapLoads()
    {
        var controller = MapController.instance;
        while (controller != null && (controller.loadingMap || SaveGate.IsBusy))
            yield return null;
        if (controller == null || controller != MapController.instance)
            yield break;
        try { Restore(); }
        catch (Exception e) { Log.Error("Restore", e); }
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
