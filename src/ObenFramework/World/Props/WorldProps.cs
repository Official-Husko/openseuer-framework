// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.World.Props;

/// <summary>
/// Objects mods place in the world that stay there: they are kept in the save (in the framework's own
/// side file, never in the game's) and built again whenever their scene loads. A prop is a model, a
/// primitive, the look of a piece of furniture or an item, a light or an empty anchor, and can be made
/// usable with the game's interact key. Props are named by the mod and an id of the mod's choosing;
/// placing the same id again replaces the old one.
/// </summary>
/// <remarks>
/// Props of a mod that isn't loaded stay in the save untouched and are built again once it is. When a mod
/// unloads its props leave the world (and come back when it loads again). Everything here runs on the main
/// thread and refuses to write while the game saves or loads; a refused call returns null or false, logs
/// the reason once and leaves it in <see cref="LastProblem"/>.
/// </remarks>
public static partial class WorldProps
{
    internal static readonly OnceLog Log = new OnceLog("World props");

    /// <summary>A prop was built in the world (placed, or rebuilt because its scene loaded).</summary>
    public static readonly WorldEvent<WorldPropInfo> Spawned = new WorldEvent<WorldPropInfo>(nameof(Spawned));

    /// <summary>Why the last refused call was refused, or null after a call that went through.</summary>
    public static string LastProblem { get; private set; }

    /// <summary>
    /// Places a prop, or replaces the mod's prop with the same id. Returns the live object when
    /// <paramref name="scene"/> is loaded; for another scene the prop is only recorded and null is returned.
    /// </summary>
    public static GameObject Place(ObenMod owner, string propId, string scene, Vector3 position, Quaternion rotation,
        Vector3 scale, WorldPropSource source, WorldPropOptions options = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (source == null) throw new ArgumentNullException(nameof(source));
        RequireId(propId);
        if (string.IsNullOrEmpty(scene) || scene.Length > PropRules.MaxSceneLength)
            throw new ArgumentException("scene must be 1 to 128 characters", nameof(scene));
        var settings = options != null ? options.Copy() : new WorldPropOptions();
        var record = Record(owner.Info.Id, propId, scene, position, rotation, scale, source, settings);
        if (!Ready("Place"))
            return null;
        if (!PropRules.PoseAllowed(record.Position, record.Rotation, record.Scale))
            return Refuse<GameObject>("Place pose", $"the pose must be finite and the scale {PropRules.MinScale} to {PropRules.MaxScale} on each axis");
        if (!PropRules.Valid(record))
            return Refuse<GameObject>("Place options", "the options don't fit (an interact key needs a collider; keys, titles and data have length limits)");
        if (!PropRules.HasRoom(Store, record.Mod, record.Id))
            return Refuse<GameObject>("Place cap " + record.Mod, $"one mod keeps at most {PropRules.MaxPerMod} props");
        string problem = PropBuilder.Check(record.Kind, record.Key, record.Alt);
        if (problem != null)
            return Refuse<GameObject>($"Place source {record.Kind} {record.Key}", problem);

        Despawn(record.Mod, record.Id);
        PropRules.Put(Store, record);
        LastProblem = null;
        return SceneManager.GetSceneByName(scene).isLoaded ? Spawn(record) : null;
    }

    /// <summary>Places a prop in the active scene at scale one.</summary>
    public static GameObject Place(ObenMod owner, string propId, Vector3 position, Quaternion rotation,
        WorldPropSource source, WorldPropOptions options = null) =>
        Place(owner, propId, SceneManager.GetActiveScene().name, position, rotation, Vector3.one, source, options);

    /// <summary>Moves one of the mod's props. Static props can't move; place them again instead.</summary>
    public static bool Move(ObenMod owner, string propId, Vector3 position, Quaternion rotation)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        RequireId(propId);
        if (!Ready("Move"))
            return false;
        var pos = PropPose.Array(position);
        var rot = PropPose.Array(rotation);
        if (!PropRules.Finite(pos, 3) || !PropRules.Finite(rot, 4))
            return Refuse<bool>("Move pose", "position and rotation must be finite");
        var record = PropRules.Find(Store, owner.Info.Id, propId);
        if (record == null)
            return Refuse<bool>("Move missing", $"{owner.Info.Id} has no prop '{propId}'");
        if (record.Static)
            return Refuse<bool>("Move static", "a static prop can't be moved, place it again instead");

        record.Position = pos;
        record.Rotation = rot;
        var live = LiveObject(record.Mod, record.Id);
        if (live != null)
            live.transform.SetPositionAndRotation(position, rotation);
        LastProblem = null;
        return true;
    }

    /// <summary>Removes one of the mod's props from the world and the save.</summary>
    public static bool Remove(ObenMod owner, string propId)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        RequireId(propId);
        if (!Ready("Remove"))
            return false;
        if (PropRules.Find(Store, owner.Info.Id, propId) == null)
            return Refuse<bool>("Remove missing", $"{owner.Info.Id} has no prop '{propId}'");
        Despawn(owner.Info.Id, propId);
        PropRules.Drop(Store, owner.Info.Id, propId);
        LastProblem = null;
        return true;
    }

    /// <summary>Removes every prop of the mod, in every scene. Returns how many there were.</summary>
    public static int Clear(ObenMod owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (!Ready("Clear"))
            return 0;
        foreach (var record in Store.Props.FindAll(r => r.Mod == owner.Info.Id))
            Despawn(record.Mod, record.Id);
        LastProblem = null;
        return PropRules.Drop(Store, owner.Info.Id, null);
    }

    /// <summary>The live object of one of the mod's props, or null when it isn't in the world.</summary>
    public static GameObject Get(ObenMod owner, string propId)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return LiveObject(owner.Info.Id, propId);
    }

    /// <summary>The mod's props, all of them or those of one scene.</summary>
    public static List<WorldPropInfo> List(ObenMod owner, string scene = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return All(scene, owner.Info.Id);
    }

    /// <summary>Every recorded prop of every mod (installed or not), optionally only one scene's or one mod's.</summary>
    public static List<WorldPropInfo> All(string scene = null, string modId = null)
    {
        var list = new List<WorldPropInfo>();
        if (_save == null)
            return list;
        foreach (var record in Store.Props)
        {
            if ((scene == null || record.Scene == scene) && (modId == null || record.Mod == modId))
                list.Add(new WorldPropInfo(record, LiveObject(record.Mod, record.Id)));
        }
        return list;
    }

    private static PropRecord Record(string mod, string propId, string scene, Vector3 position, Quaternion rotation,
        Vector3 scale, WorldPropSource source, WorldPropOptions settings) => new PropRecord
        {
            Mod = mod,
            Id = propId,
            Scene = scene,
            Kind = source.Kind,
            Key = source.Key ?? "",
            Alt = source.Alt,
            Position = PropPose.Array(position),
            Rotation = PropPose.Array(rotation),
            Scale = PropPose.Array(scale),
            Collider = settings.Collider,
            Static = settings.IsStatic,
            InteractKey = settings.InteractKey ?? "",
            Title = settings.Title ?? "",
            Data = settings.Data ?? "",
            LightKind = source.LightKind,
            LightColor = PropPose.Array(source.LightColor),
            LightRange = source.LightRange,
            LightIntensity = source.LightIntensity,
            LightSpotAngle = source.LightSpotAngle,
        };

    // Writes need the store, a world and an idle save system.
    private static bool Ready(string call)
    {
        if (_save == null)
            return Refuse<bool>(call + " off", "the framework's world props are not set up");
        if (!WorldState.InWorld)
            return Refuse<bool>(call + " menu", "there is no game loaded, so nothing can be recorded");
        if (SaveGate.IsBusy)
            return Refuse<bool>(call + " busy", "the game is saving or loading, try again once it is done");
        return true;
    }

    private static T Refuse<T>(string where, string why)
    {
        LastProblem = why;
        Log.Warn(where, why);
        return default;
    }

    private static void RequireId(string propId)
    {
        if (!PropRules.IdAllowed(propId))
            throw new ArgumentException("propId must be 1 to 64 characters", nameof(propId));
    }
}
