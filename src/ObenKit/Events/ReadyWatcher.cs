using ObenKit.Game;
using ObenLoader;
using UnityEngine;

namespace ObenKit.Events;

/// <summary>
/// Decides when the world is ready: a gameplay scene, no save load in progress, the player exists,
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

    public static void Create(ObenMod kit)
    {
        _instance = kit.CreateObject("ReadyWatcher").AddComponent<ReadyWatcher>();
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
        if (WorldState.IsLoadingSave || !WorldState.InWorld || PlayerLocator.instance == null)
            return;
        if (--_framesLeft > 0 || Time.realtimeSinceStartup < _readyAt)
            return;

        enabled = false;
        WorldState.IsReady = true;
        GameEvents.WorldReady.Raise(WorldState.SceneName, GameEvents.Log);
    }
}
