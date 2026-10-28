using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Events;

/// <summary>
/// Raises <see cref="GameEvents.LoadAborted"/> when an announced level change or save load doesn't
/// happen (see <see cref="LoadAbortRules"/>). The component is only enabled between an announcement and
/// the load starting or being dropped, so it costs nothing the rest of the time.
/// </summary>
internal sealed class LoadAbortWatcher : MonoBehaviour
{
    private static readonly LoadAbortRules Rules = new LoadAbortRules();
    private static LoadAbortWatcher _instance;

    /// <summary>An announced load is being watched.</summary>
    public static bool Pending => Rules.Pending;

    public static void Create(ObenMod framework)
    {
        Rules.Reset();
        _instance = framework.CreateObject("LoadAbortWatcher").AddComponent<LoadAbortWatcher>();
        _instance.enabled = false;
        framework.Track(Disposable.Create(() =>
        {
            _instance = null;
            Rules.Reset();
        }));
    }

    /// <summary>The game announced a level change or a save load. Call before the world is marked not ready.</summary>
    public static void Announced(bool levelChange)
    {
        Rules.Announced(Time.frameCount, levelChange, WorldState.IsReady);
        if (_instance != null)
            _instance.enabled = true;
    }

    /// <summary>A scene load or the save restore started.</summary>
    public static void Proceeding()
    {
        Rules.Proceeding();
        if (_instance != null)
            _instance.enabled = false;
    }

    private void Update()
    {
        if (!Rules.Pending)
        {
            enabled = false;
            return;
        }
        if (!Rules.Check(Time.frameCount, SaveController.Loading, ScreenIdle()))
            return;

        enabled = false;
        // The level is still there: it is as ready as it was before the announcement.
        if (Rules.WasReady && WorldState.InWorld && PlayerLocator.instance != null)
            WorldState.IsReady = true;
        GameEvents.Log?.Info($"The {(Rules.LevelChange ? "level change" : "save load")} the game announced didn't happen; still in {WorldState.SceneName} (ready: {WorldState.IsReady})");
        GameEvents.LoadAborted.Raise(Rules.LevelChange, GameEvents.Log);
    }

    private static bool ScreenIdle()
    {
        var screen = LoadingScreen.instance;
        return screen != null && screen.LevelLoadDone && !screen.FadeActive && !screen.Fading;
    }
}
