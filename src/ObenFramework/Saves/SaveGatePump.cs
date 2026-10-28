using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Saves;

/// <summary>
/// Runs the actions queued by <see cref="SaveGate.WhenIdle"/> once the gate opens. The component
/// is only enabled while something is waiting, so it costs nothing the rest of the time.
/// </summary>
internal sealed class SaveGatePump : MonoBehaviour
{
    private static SaveGatePump _instance;

    private readonly List<Waiting> _waiting = new List<Waiting>();
    private readonly List<Action> _due = new List<Action>();

    private sealed class Waiting
    {
        public Action Action;
    }

    public static void Create(ObenMod framework)
    {
        _instance = framework.CreateObject("SaveGatePump").AddComponent<SaveGatePump>();
        _instance.enabled = false;
        framework.Track(Disposable.Create(() => _instance = null));
    }

    public static void Enqueue(ITracker owner, Action action)
    {
        if (_instance == null)
            return;
        var entry = new Waiting { Action = action };
        _instance._waiting.Add(entry);
        _instance.enabled = true;
        owner.Track(Disposable.Create(() =>
        {
            if (_instance != null)
                _instance._waiting.Remove(entry);
        }));
    }

    private void Update()
    {
        if (_waiting.Count == 0)
        {
            enabled = false;
            return;
        }
        if (SaveGate.IsBusy)
            return;

        _due.Clear();
        foreach (var entry in _waiting)
            _due.Add(entry.Action);
        _waiting.Clear();
        enabled = false;

        foreach (var action in _due)
            SaveGate.Run(action);
        _due.Clear();
    }
}
