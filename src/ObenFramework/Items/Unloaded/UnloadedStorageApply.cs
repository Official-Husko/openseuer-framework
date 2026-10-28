// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using ObenLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Items;

/// <summary>
/// Applies queued <see cref="UnloadedStorage"/> changes when their scene is ready. Containers can start a
/// little after the scene (apartment prefabs, late spawns), so a change whose container isn't there yet is
/// tried again every second for up to ten seconds before it fails. Nothing runs while no change is queued
/// for the scene.
/// </summary>
internal static class UnloadedStorageApply
{
    private const int SettleFrames = 3;
    private static readonly WaitForSecondsRealtime RetryWait = new WaitForSecondsRealtime(1f);

    private static ObenMod _framework;
    private static int _generation;
    private static readonly Dictionary<UnloadedStorageEntry, float> MissingSince = new Dictionary<UnloadedStorageEntry, float>();

    public static void Init(ObenMod framework)
    {
        _framework = framework;
        GameEvents.WhenWorldReady(framework, Schedule);
        GameEvents.LevelChanging.Subscribe(framework, Stop);
        GameEvents.MainMenu.Subscribe(framework, Stop);
    }

    private static void Schedule(string scene)
    {
        Stop();
        if (HasWork(scene))
            _framework.StartCoroutine(Run(scene, _generation));
    }

    // A running pass sees the new generation and ends on its next step.
    private static void Stop()
    {
        _generation++;
        MissingSince.Clear();
    }

    private static bool HasWork(string scene)
    {
        foreach (var entry in UnloadedStorage.Entries)
        {
            if (entry.Scene == scene && !UnloadedStorage.Settled.Contains(entry))
                return true;
        }
        return false;
    }

    private static IEnumerator Run(string scene, int generation)
    {
        for (int i = 0; i < SettleFrames; i++)
            yield return null;

        while (generation == _generation && WorldState.IsReady && WorldState.SceneName == scene)
        {
            bool waiting;
            try
            {
                waiting = ApplyScene(scene);
            }
            catch (Exception e)
            {
                ItemsLog.Failed("Applying queued container changes in " + scene, e);
                waiting = false;
            }
            if (!waiting)
                break;
            yield return RetryWait;
        }
    }

    /// <summary>One pass over the scene's changes. True when some still wait for their container.</summary>
    private static bool ApplyScene(string scene)
    {
        var byGuid = ContainersByGuid(scene);
        bool waiting = false;
        foreach (var entry in UnloadedStorage.Entries.ToArray())
        {
            if (entry.Scene != scene || UnloadedStorage.Settled.Contains(entry))
                continue;

            byGuid.TryGetValue(entry.Guid, out var matches);
            bool ambiguous = matches != null && matches.Count > 1;
            var container = matches != null && matches.Count == 1 ? matches[0] : null;
            bool done = container != null && Run(container, entry);
            var result = UnloadedStorageRules.Decide(container != null, done);

            if (result == UnloadedApply.Gone && !ambiguous && !Overdue(entry))
            {
                waiting = true;
                continue;
            }
            MissingSince.Remove(entry);

            if (result == UnloadedApply.Applied)
            {
                UnloadedStorage.Settle(entry);
                if (UnloadedStorage.Applied.HasSubscribers)
                    UnloadedStorage.Applied.Raise(new UnloadedChange(entry));
                continue;
            }

            string reason = ambiguous ? "more than one container in the scene has this id"
                : result == UnloadedApply.Gone ? $"its container did not show up within {UnloadedStorageRules.RetrySeconds} seconds"
                : entry.Give ? "the container had no room for all of it" : "the container didn't hold that many";
            ItemsLog.Warn($"A queued container change in {scene} was dropped ({entry.Mod}, container {entry.Guid}, item {entry.ItemId} x{entry.Count}): {reason}.");
            UnloadedStorage.Drop(entry);
            if (UnloadedStorage.Failed.HasSubscribers)
                UnloadedStorage.Failed.Raise(new UnloadedChange(entry, reason));
        }
        return waiting;
    }

    private static bool Overdue(UnloadedStorageEntry entry)
    {
        float now = Time.realtimeSinceStartup;
        if (!MissingSince.TryGetValue(entry, out float since))
            MissingSince[entry] = since = now;
        return UnloadedStorageRules.Expired(since, now);
    }

    private static Dictionary<string, List<Storage>> ContainersByGuid(string scene)
    {
        var map = new Dictionary<string, List<Storage>>();
        foreach (var storage in Object.FindObjectsByType<Storage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (storage.Slots == null || storage.gameObject.scene.name != scene)
                continue;
            string guid = GuidIndex.GuidOf(storage);
            if (guid == null)
                continue;
            if (!map.TryGetValue(guid, out var list))
                map[guid] = list = new List<Storage>(1);
            list.Add(storage);
        }
        return map;
    }

    // All or nothing: a give that doesn't fully fit is taken back out, a take needs the whole amount.
    private static bool Run(Storage storage, UnloadedStorageEntry entry)
    {
        if (entry.Give)
        {
            int left = Containers.Add(storage, entry.ItemId, entry.Count, null, ChangeSource.Mod);
            if (left == 0)
                return true;
            if (left < entry.Count)
                Containers.Remove(storage, entry.ItemId, entry.Count - left, null, ChangeSource.Mod);
            return false;
        }
        if (Containers.Count(storage, entry.ItemId) < entry.Count)
            return false;
        return Containers.Remove(storage, entry.ItemId, entry.Count, null, ChangeSource.Mod) == entry.Count;
    }
}
