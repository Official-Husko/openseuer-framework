using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Quests;

/// <summary>
/// Turns the game's argument-less TaskUpdated / ObjectiveUpdated events into <see cref="QuestLog.TaskChanged"/>
/// and <see cref="QuestLog.ObjectiveChanged"/>. The game raises them in the middle of its own edits, when
/// the state is half written, so they only mark the state dirty; the comparison runs in LateUpdate of the
/// next frame. The component is disabled while nothing is dirty, so it costs nothing in between.
/// </summary>
internal sealed class QuestWatcher : MonoBehaviour
{
    private static QuestWatcher _instance;

    private readonly QuestSnapshot _snapshot = new QuestSnapshot();
    private readonly List<TaskChange> _taskChanges = new List<TaskChange>(8);
    private readonly List<ObjectiveChange> _objectiveChanges = new List<ObjectiveChange>(8);
    private TaskController _controller;
    private bool _dirty;
    private bool _rebaseline = true;

    public static void Attach(ObenMod framework)
    {
        _instance = framework.CreateObject("QuestWatcher").AddComponent<QuestWatcher>();
        _instance.enabled = false;

        TaskController.TaskUpdated += OnGameUpdated;
        TaskController.ObjectiveUpdated += OnGameUpdated;
        framework.Track(Disposable.Create(() =>
        {
            TaskController.TaskUpdated -= OnGameUpdated;
            TaskController.ObjectiveUpdated -= OnGameUpdated;
            _instance = null;
        }));

        // After a load the state is whatever the save held; that is not a change. Same for a new scene's
        // controller. Starting mid-game (hot reload) records the current state the same way.
        GameEvents.Loading.Subscribe(framework, Rebaseline);
        GameEvents.Loaded.Subscribe(framework, Rebaseline);
        GameEvents.WorldReady.Subscribe(framework, _ => EnsureBaseline());
        if (WorldState.IsReady)
            Rebaseline();
    }

    /// <summary>Asks for a comparison on the next frame. For edits the game doesn't announce itself.</summary>
    public static void Poke()
    {
        if (_instance != null)
            _instance.MarkDirty();
    }

    private static void OnGameUpdated() => Poke();

    private static void Rebaseline()
    {
        if (_instance == null)
            return;
        _instance._rebaseline = true;
        _instance.enabled = true;
    }

    private static void EnsureBaseline()
    {
        if (_instance != null && _instance._rebaseline)
            _instance.enabled = true;
    }

    private void MarkDirty()
    {
        _dirty = true;
        enabled = true;
    }

    private void LateUpdate()
    {
        _dirty = false;
        try
        {
            Compare();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Comparing the quest state failed", e);
            _snapshot.Clear();
            _rebaseline = true;
        }
        // A handler may have edited quests again, which marks the state dirty during Compare.
        enabled = _dirty;
    }

    private void Compare()
    {
        var controller = TaskController.instance;
        if (controller == null)
        {
            _controller = null;
            _rebaseline = true;
            return;
        }

        // While a save loads the lists are rebuilt under us; wait for Loaded to record the result.
        if (WorldState.IsLoadingSave)
        {
            _rebaseline = true;
            return;
        }

        if (_rebaseline || !ReferenceEquals(controller, _controller))
        {
            _controller = controller;
            _rebaseline = false;
            _snapshot.Clear();
            _snapshot.Update(controller, null, null);
            return;
        }

        _snapshot.Update(controller, _taskChanges, _objectiveChanges);
        for (int i = 0; i < _taskChanges.Count; i++)
            QuestLog.TaskChanged.Raise(_taskChanges[i], GameEvents.Log);
        for (int i = 0; i < _objectiveChanges.Count; i++)
            QuestLog.ObjectiveChanged.Raise(_objectiveChanges[i], GameEvents.Log);
        _taskChanges.Clear();
        _objectiveChanges.Clear();
    }
}
