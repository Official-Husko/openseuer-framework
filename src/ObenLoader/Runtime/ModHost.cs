using System;
using ObenLoader.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenLoader.Runtime;

/// <summary>Hidden, persistent GameObject that forwards Unity callbacks to mods.</summary>
internal sealed class ModHost : MonoBehaviour
{
    private const float LogFlushInterval = 1f;

    private static readonly Action<ObenMod> UpdateCall = m => m.OnUpdate();
    private static readonly Action<ObenMod> LateUpdateCall = m => m.OnLateUpdate();
    private static readonly Action<ObenMod> FixedUpdateCall = m => m.OnFixedUpdate();
    private static readonly Action<ObenMod> GuiCall = m => m.OnGUI();
    private static readonly Action<ObenMod> QuitCall = m => m.OnQuit();

    private readonly ModCallbacks _update = new ModCallbacks(nameof(ObenMod.OnUpdate));
    private readonly ModCallbacks _lateUpdate = new ModCallbacks(nameof(ObenMod.OnLateUpdate));
    private readonly ModCallbacks _fixedUpdate = new ModCallbacks(nameof(ObenMod.OnFixedUpdate));
    private readonly ModCallbacks _gui = new ModCallbacks(nameof(ObenMod.OnGUI));
    private readonly ModCallbacks _sceneLoaded = new ModCallbacks(nameof(ObenMod.OnSceneLoaded));
    private readonly ModCallbacks _quit = new ModCallbacks(nameof(ObenMod.OnQuit));

    private ModLogger _log;
    private float _nextFlush;

    /// <summary>The single host instance. Also runs coroutines on behalf of mods.</summary>
    internal static ModHost Instance { get; private set; }

    public static ModHost Create(ModLogger log)
    {
        var go = new GameObject("ObenLoader");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        var host = go.AddComponent<ModHost>();
        host._log = log;
        Instance = host;
        SceneManager.sceneLoaded += host.OnSceneLoaded;
        return host;
    }

    public void Register(ObenMod mod)
    {
        _update.AddIfOverridden(mod);
        _lateUpdate.AddIfOverridden(mod);
        _fixedUpdate.AddIfOverridden(mod);
        _gui.AddIfOverridden(mod);
        _sceneLoaded.AddIfOverridden(mod);
        _quit.AddIfOverridden(mod);
    }

    public void Unregister(ObenMod mod)
    {
        _update.Remove(mod);
        _lateUpdate.Remove(mod);
        _fixedUpdate.Remove(mod);
        _gui.Remove(mod);
        _sceneLoaded.Remove(mod);
        _quit.Remove(mod);
    }

    private void Awake() => MainThread.CaptureThread();

    private void Update()
    {
        LogHub.Frame = Time.frameCount;
        MainThread.Drain(_log);
        Mods.ModRegistry.Watcher.Tick();
        _update.Invoke(UpdateCall, _log);

        if (Time.unscaledTime >= _nextFlush)
        {
            _nextFlush = Time.unscaledTime + LogFlushInterval;
            LogHub.Flush();
        }
    }

    private void LateUpdate() => _lateUpdate.Invoke(LateUpdateCall, _log);
    private void FixedUpdate() => _fixedUpdate.Invoke(FixedUpdateCall, _log);

    private void OnGUI()
    {
        if (_gui.Count > 0)
            _gui.Invoke(GuiCall, _log);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _log.Info($"Scene loaded: {scene.name} ({mode})");
        _sceneLoaded.Invoke(m => m.OnSceneLoaded(scene, mode), _log);
    }

    private void OnApplicationQuit()
    {
        _quit.Invoke(QuitCall, _log);
        _log.Info("Game closing");
        LogHub.Shutdown();
    }
}
