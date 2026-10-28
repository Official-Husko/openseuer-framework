// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using ObenFramework.World;
using ObenLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.SceneLook;

/// <summary>
/// Character looks taken straight from the game's scene files: every NPC and posed character a level
/// contains, with its meshes, skeleton, materials, textures and blend shapes. Find them with
/// <see cref="List(string,bool)"/> and the Find methods (no scene has to be loaded), load one, and put it
/// on any body: a mod NPC, a live NPC, a prop with an animator. Reading uses the game's own files in its
/// data folder, read-only; it is tied to the Unity version the layouts were checked against and switches
/// itself off on any other.
/// </summary>
/// <remarks>
/// Scanning a scene for its looks reads its file once (a fraction of a second to a second and a half for
/// the biggest levels) and is cached for the session; loading a look reads its meshes and textures. Both
/// run on the calling thread, so do them at a loading moment, not every frame. Loaded looks are shared
/// between mods; one stays loaded until every mod that loaded it released it or unloaded, and every look a
/// mod put on a body comes off when that mod unloads.
/// </remarks>
public static partial class SceneLooks
{
    internal static readonly OnceLog Log = new OnceLog("Scene looks");

    private static readonly Dictionary<string, LoadedSceneLook> Loaded = new Dictionary<string, LoadedSceneLook>();
    private static readonly Dictionary<string, HashSet<string>> Holders = new Dictionary<string, HashSet<string>>();
    private static SceneLookFiles _files;
    private static SceneLookIndex _index;

    /// <summary>Why scene looks are off, or null when they work in this build.</summary>
    public static string Unavailable =>
        Application.unityVersion == SceneLookFile.UnityVersion ? null : "Unity " + Application.unityVersion + " is not " + SceneLookFile.UnityVersion + ", scene looks are off";

    /// <summary>The looks of a scene by its name (see <c>WorldLocation.KnownScenes</c>); with <paramref name="charactersOnly"/> only characters.</summary>
    public static SceneLookInfo[] List(string scene, bool charactersOnly = true)
    {
        var looks = Guard("list " + scene, () =>
        {
            var found = Index().List(scene, out string reason) ?? throw new LookException(reason);
            return charactersOnly ? SceneLookIndex.Characters(found) : found;
        });
        return looks != null ? looks.ToArray() : new SceneLookInfo[0];
    }

    /// <summary>A look by body or character name in a scene, ignoring case, or null.</summary>
    public static SceneLookInfo FindByName(string scene, string name) =>
        Guard("find " + scene + " " + name, () => Index().FindByName(scene, name, out string reason) ?? throw new LookException(reason));

    /// <summary>The first look of the game's character <paramref name="characterId"/> in any scene, or null. May scan many scenes.</summary>
    public static SceneLookInfo FindByCharacter(int characterId) =>
        Guard("find character " + characterId, () => Index().FindByCharacter(characterId, out string reason) ?? throw new LookException(reason));

    public static SceneLookInfo FindByCharacter(string scene, int characterId) =>
        Guard("find character " + scene + " " + characterId, () => Index().FindByCharacter(scene, characterId, out string reason) ?? throw new LookException(reason));

    /// <summary>Loads a look found with <see cref="List(string,bool)"/> or a Find method. Check <see cref="LoadedSceneLook.Ready"/>.</summary>
    public static LoadedSceneLook Load(ObenMod owner, SceneLookInfo info)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (info == null) throw new ArgumentNullException(nameof(info));
        if (info.Level == null || info.Root == 0)
            return Failed(info.Scene, info.Path, "the look info was not made by List or a Find method");
        return LoadCore(owner, info);
    }

    /// <summary>Loads the look of a body by its path (or its name when only one body has it) in a scene.</summary>
    public static LoadedSceneLook Load(ObenMod owner, string scene, string rendererRoot)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        string problem = Unavailable;
        if (problem != null)
            return Failed(scene, rendererRoot, problem);
        SceneLookInfo info;
        string reason;
        try { info = Index().Root(scene, rendererRoot, out reason); }
        catch (Exception e)
        {
            info = null;
            reason = e.Message;
        }
        return info != null ? LoadCore(owner, info) : Failed(scene, rendererRoot, reason);
    }

    /// <summary>Stops <paramref name="owner"/> holding a look; it is freed once nobody holds it and no body wears it.</summary>
    public static void Release(ObenMod owner, LoadedSceneLook look)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (look?.Key == null || !look.Ready)
            return;
        if (Holders.TryGetValue(look.Key, out var holders))
            holders.Remove(owner.Info.Id);
        Free(look.Key);
    }

    /// <summary>How many looks are loaded, scenes scanned and bodies wear a look, for diagnostics.</summary>
    public static string Status() =>
        $"{Loaded.Count} looks loaded, {(_index != null ? _index.ScannedCount : 0)} scenes scanned, {SceneLookApply.Count} bodies wearing one, {(_files != null ? _files.OpenCount : 0)} files open";

    private static LoadedSceneLook LoadCore(ObenMod owner, SceneLookInfo info)
    {
        string problem = Unavailable;
        if (problem != null)
            return Failed(info.Scene, info.Path, problem);
        string key = info.Level + "|" + info.Root;
        Track(owner);
        if (Loaded.TryGetValue(key, out var cached))
        {
            Hold(owner, key);
            return cached;
        }
        SceneLookLook data;
        string reason;
        try { data = SceneLookLoad.Load(Files(), info.Level, info.Root, out reason); }
        catch (Exception e)
        {
            data = null;
            reason = e.Message;
        }
        if (data == null)
            return Failed(info.Scene, info.Path, reason);

        var look = Fill(info, data, key);
        Loaded[key] = look;
        Hold(owner, key);
        return look;
    }

    private static LoadedSceneLook Fill(SceneLookInfo info, SceneLookLook data, string key)
    {
        var bones = new HashSet<string>();
        int vertices = 0;
        foreach (var renderer in data.Renderers)
        {
            vertices += (int)renderer.Mesh.VertexCount;
            foreach (var bone in renderer.Bones)
                bones.Add(bone.Name);
        }
        var names = new List<string>(bones);
        names.Sort(StringComparer.Ordinal);
        return new LoadedSceneLook
        {
            Scene = info.Scene,
            Path = info.Path,
            Name = info.Name,
            Ready = true,
            Look = data,
            Key = key,
            MeshCount = data.Renderers.Count,
            VertexCount = vertices,
            BoneNames = names,
        };
    }

    private static LoadedSceneLook Failed(string scene, string path, string reason)
    {
        Log.Warn("load " + scene + " " + path, reason);
        return new LoadedSceneLook { Scene = scene, Path = path, Failed = true, Reason = reason };
    }

    private static void Hold(ObenMod owner, string key)
    {
        if (!Holders.TryGetValue(key, out var holders))
            Holders[key] = holders = new HashSet<string>();
        holders.Add(owner.Info.Id);
    }

    // Frees a look nobody holds and no body wears.
    private static void Free(string key)
    {
        if (!Loaded.TryGetValue(key, out var look))
            return;
        if (Holders.TryGetValue(key, out var holders) && holders.Count > 0)
            return;
        if (SceneLookApply.BodyOf(look.Look) != null)
            return;
        Loaded.Remove(key);
        Holders.Remove(key);
        SceneLookApply.Release(look.Look);
        SceneLookNotes.Forget(look.Look);
        look.Ready = false;
        look.Look = null;
        look.Reason = "released";
    }

    private static T Guard<T>(string what, Func<T> action) where T : class
    {
        string problem = Unavailable;
        if (problem != null)
        {
            Log.Warn("off", problem);
            return null;
        }
        try { return action(); }
        catch (LookException e)
        {
            Log.Warn(what, e.Message);
            return null;
        }
        catch (Exception e)
        {
            Log.Error(what, e);
            return null;
        }
    }

    private static SceneLookFiles Files() => _files ??= new SceneLookFiles(Application.dataPath);

    private static SceneLookIndex Index() => _index ??= new SceneLookIndex(Files(), BuildScenes());

    private static string[] BuildScenes()
    {
        var scenes = new string[SceneManager.sceneCountInBuildSettings];
        for (int i = 0; i < scenes.Length; i++)
            scenes[i] = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
        return scenes;
    }

    /// <summary>A plain refusal with a reason, as opposed to a bug.</summary>
    private sealed class LookException : Exception
    {
        public LookException(string message) : base(message) { }
    }
}
