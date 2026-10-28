// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>A body wearing a scene look, as <see cref="SceneLooks.Applied"/> lists it.</summary>
public readonly struct AppliedSceneLook
{
    public AppliedSceneLook(GameObject body, string modId, string look, string path)
    {
        Body = body;
        ModId = modId;
        Look = look;
        Path = path;
    }

    public GameObject Body { get; }
    public string ModId { get; }

    /// <summary>The look's file and root, for logs.</summary>
    public string Look { get; }

    /// <summary>"name", "retarget" or "skeleton".</summary>
    public string Path { get; }
}

public static partial class SceneLooks
{
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>
    /// Puts a look on a body. Bodies with the same bone names get it skinned straight onto their skeleton;
    /// humanoid bodies with other names get the look's own skeleton, driven by the body's pose. The body's
    /// own skinned renderers are hidden until the look comes off.
    /// </summary>
    public static bool Apply(ObenMod owner, LoadedSceneLook look, GameObject body, out string reason) =>
        ApplyCore(owner, look, body, false, false, out reason);

    /// <summary>Like <see cref="Apply"/>, but always onto the body's own humanoid skeleton, matching bones by name or role.</summary>
    public static bool ApplyToSkeleton(ObenMod owner, LoadedSceneLook look, GameObject body, out string reason) =>
        ApplyCore(owner, look, body, true, false, out reason);

    /// <summary>
    /// Puts a look on a live NPC and keeps its own renderers hidden when the game turns them back on.
    /// With <paramref name="skeleton"/> the look goes onto the NPC's own skeleton (see <see cref="ApplyToSkeleton"/>).
    /// </summary>
    public static bool Reskin(ObenMod owner, LoadedSceneLook look, GameObject npc, out string reason, bool skeleton = false) =>
        ApplyCore(owner, look, npc, skeleton, true, out reason);

    /// <summary>Takes the look off a body the mod put it on and shows the body's own renderers again.</summary>
    public static bool Remove(ObenMod owner, GameObject body, out string reason)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        bool removed = SceneLookApply.Remove(owner.Info.Id, body, out reason);
        FreeUnused();
        return removed;
    }

    /// <summary>Copies the body's pose onto a retargeted look once (useful with auto pose off).</summary>
    public static bool SyncPose(ObenMod owner, GameObject body, out string reason)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!SceneLookApply.Driver(owner.Info.Id, body, out var driver, out reason))
            return false;
        driver.Sync();
        return true;
    }

    /// <summary>Turns the per-frame pose copy of a retargeted look on or off.</summary>
    public static bool SetAutoPose(ObenMod owner, GameObject body, bool enabled, out string reason)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!SceneLookApply.Driver(owner.Info.Id, body, out var driver, out reason))
            return false;
        driver.Auto = enabled;
        return true;
    }

    /// <summary>The rig of a look the mod put on a body, or null with the reason.</summary>
    public static SceneLookRigView RigOf(ObenMod owner, GameObject body, out string reason)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        var state = SceneLookApply.State(body);
        reason = body == null ? "no body"
            : state == null ? "the body has no applied look"
            : state.ModId != owner.Info.Id ? "the look on this body was applied by mod " + state.ModId
            : null;
        if (reason != null)
            return null;
        var avatar = state.Avatar != null ? state.Avatar : SceneLookRigTools.HumanAvatar(body);
        return new SceneLookRigView(state.Holder, avatar, Label(state.Path), state.Driver);
    }

    /// <summary>Every body wearing a scene look right now.</summary>
    public static List<AppliedSceneLook> Applied()
    {
        var list = new List<AppliedSceneLook>();
        foreach (var state in SceneLookApply.All())
            list.Add(new AppliedSceneLook(state.Body, state.ModId, state.Look.FileName + "|" + state.Look.Root, Label(state.Path)));
        return list;
    }

    private static bool ApplyCore(ObenMod owner, LoadedSceneLook look, GameObject body, bool skeleton, bool live, out string reason)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        reason = Unavailable
            ?? (look == null ? "no look"
            : !look.Ready ? "the look is not ready: " + look.Reason
            : body == null ? "no body"
            : null);
        if (reason != null)
            return false;
        Track(owner);
        try
        {
            return live
                ? SceneLookApplyLive.Reskin(owner.Info.Id, look.Look, body, skeleton, out reason)
                : SceneLookApply.Apply(owner.Info.Id, look.Look, body, skeleton, out reason);
        }
        catch (Exception e)
        {
            reason = e.Message;
            Log.Error("apply " + owner.Info.Id, e);
            return false;
        }
    }

    // The first use by a mod arranges for everything it did to be undone when it unloads.
    private static void Track(ObenMod owner)
    {
        string id = owner.Info.Id;
        if (!Tracked.Add(id))
            return;
        owner.Track(Disposable.Create(() =>
        {
            Tracked.Remove(id);
            DropOwner(id);
        }));
    }

    private static void DropOwner(string modId)
    {
        try
        {
            SceneLookApply.RemoveMod(modId);
            foreach (var holders in Holders.Values)
                holders.Remove(modId);
            FreeUnused();
        }
        catch (Exception e)
        {
            Log.Error("dropping " + modId, e);
        }
    }

    private static void FreeUnused()
    {
        foreach (string key in new List<string>(Loaded.Keys))
            Free(key);
    }

    internal static string Label(SceneLookRigPath path) =>
        path == SceneLookRigPath.Name ? "name" : path == SceneLookRigPath.Retarget ? "retarget" : path == SceneLookRigPath.Skeleton ? "skeleton" : "refuse";

    /// <summary>Takes every look off, frees what was built and closes the files. Run when the framework unloads.</summary>
    internal static void Shutdown()
    {
        SceneLookApply.RemoveAll();
        foreach (var look in Loaded.Values)
        {
            look.Ready = false;
            look.Look = null;
            look.Reason = "released";
        }
        Loaded.Clear();
        Holders.Clear();
        Tracked.Clear();
        SceneLookNotes.Clear();
        _index = null;
        _files?.Dispose();
        _files = null;
        Log.Reset();
    }
}
