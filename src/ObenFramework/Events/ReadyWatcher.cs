using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Events;

/// <summary>
/// Decides when the world is ready: a gameplay scene, no save load announced or in progress, the player exists,
/// and a short settle time has passed (the game moves the player to the entry point about 0.1 s
/// after LoadingDone). The component is only enabled while waiting, so it costs nothing otherwise.
/// </summary>
internal sealed class ReadyWatcher : MonoBehaviour
{
    private const float SettleSeconds = 0.25f;
    private const int SettleFrames = 2;

    private static ReadyWatcher _instance;

    private float _readyAt;
    private int _framesLeft;

    public static void Create(ObenMod framework)
    {
        _instance = framework.CreateObject("ReadyWatcher").AddComponent<ReadyWatcher>();
        _instance.enabled = false;
    }

    /// <summary>Starts (or restarts) the wait.</summary>
    public static void Arm()
    {
        if (_instance == null)
            return;
        _instance._readyAt = Time.realtimeSinceStartup + SettleSeconds;
        _instance._framesLeft = SettleFrames;
        _instance.enabled = true;
    }

    private void Update()
    {
        if (WorldState.InMainMenu)
        {
            enabled = false;
            return;
        }
        // SaveGate covers the frames between the new scene opening and the game's LoadingStarted: the
        // loading screen holds the load back there, and the level still has its own state, not the save's.
        if (WorldState.IsLoadingSave || SaveGate.IsLoading || !WorldState.InWorld || PlayerLocator.instance == null)
            return;
        if (--_framesLeft > 0 || Time.realtimeSinceStartup < _readyAt)
            return;

        enabled = false;
        WorldState.IsReady = true;
        GameEvents.WorldReady.Raise(WorldState.SceneName, GameEvents.Log);
    }
}
