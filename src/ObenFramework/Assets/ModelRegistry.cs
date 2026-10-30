// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// Loads 3D models from OBJ (and MTL) files in a mod's folder and keeps them by name, so other modules
/// (items, furniture, props) can ask for a template to copy. Parsing and laying out the vertices run on a
/// worker thread, and the mesh and materials are built over the next few frames, so a large file never
/// stalls the game. Templates are destroyed when the mod that loaded them unloads.
/// </summary>
/// <remarks>
/// Models are named <c>modId/key</c>. The materials use the game's Standard shader, with the colour,
/// emission, diffuse texture and transparency (Fade mode) from the MTL file.
/// </remarks>
public static class ModelRegistry
{
    private const int MaxKeyLength = 64;

    /// <summary>
    /// Loads a model in the background and calls <paramref name="done"/> on the main thread with its
    /// template, or <paramref name="failed"/> with the reason. Loading the same key again returns the
    /// finished template. Nothing is called if the mod unloads first.
    /// </summary>
    /// <param name="key">Name for the model, letters, digits, '.', '_' or '-'; unique within the mod.</param>
    /// <param name="path">The .obj file, relative to the mod folder.</param>
    public static void Load(ObenMod owner, string key, string path, ModelOptions options = null,
        Action<ModelTemplate> done = null, Action<string> failed = null)
    {
        var request = new ModelRequest(done, failed);
        string full;
        ModelOptions copy;
        ModAssetSet set;
        try
        {
            set = AssetSets.For(owner);
            full = Start(owner, set, key, path, options, out copy, out var existing);
            if (existing != null)
            {
                Deliver(set, request, existing, null);
                return;
            }
        }
        catch (Exception e)
        {
            Deliver(null, request, null, e.Message);
            return;
        }

        if (set.ModelWaiting.TryGetValue(key, out var waiting))
        {
            waiting.Add(request);
            return;
        }
        set.ModelWaiting[key] = new List<ModelRequest> { request };
        owner.StartCoroutine(Run(set, key, full, copy));
    }

    /// <summary>
    /// Loads a model right now on the calling thread and returns it. Fine for small models; use
    /// <see cref="Load"/> for anything big. Throws with the reason when the model can't be loaded.
    /// </summary>
    public static ModelTemplate LoadNow(ObenMod owner, string key, string path, ModelOptions options = null)
    {
        var set = AssetSets.For(owner);
        string full = Start(owner, set, key, path, options, out var copy, out var existing);
        if (existing != null)
            return existing;

        var prepared = ModelPrepare.Run(key, full, copy);
        if (prepared.Error != null)
            throw new InvalidDataException($"Model {key}: {prepared.Error}");

        var build = new ModelBuild(set, prepared, copy);
        build.BuildMesh();
        while (build.BuildNextMaterial()) { }
        var template = build.Assemble();
        if (template == null)
        {
            build.Abort();
            throw new InvalidDataException($"Model {key}: {build.Error}");
        }
        Register(set, template, prepared);
        return template;
    }

    /// <summary>Finds a loaded model by its full name, <c>modId/key</c>. Returns null when there is none.</summary>
    public static ModelTemplate Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        int slash = name.IndexOf('/');
        if (slash <= 0 || slash == name.Length - 1)
            return null;
        var set = AssetSets.Find(name.Substring(0, slash));
        return set != null && set.Models.TryGetValue(name.Substring(slash + 1), out var template) && template.IsAlive ? template : null;
    }

    /// <summary>Finds one of a mod's own models by the key it was loaded with.</summary>
    public static ModelTemplate Find(ObenMod owner, string key) => Find(owner.Info.Id + "/" + key);

    /// <summary>Every loaded model of every mod.</summary>
    public static List<ModelTemplate> All()
    {
        var all = new List<ModelTemplate>();
        foreach (var set in AssetSets.All())
        {
            foreach (var template in set.Models.Values)
            {
                if (template.IsAlive)
                    all.Add(template);
            }
        }
        return all;
    }

    /// <summary>Makes a copy of a loaded model by full name. Returns null when no such model is loaded.</summary>
    public static GameObject Spawn(string name, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var template = Find(name);
        return template != null ? template.Instantiate(position, rotation, parent) : null;
    }

    // Checks the arguments and returns the full path, or the already loaded template for this key.
    private static string Start(ObenMod owner, ModAssetSet set, string key, string path, ModelOptions options,
        out ModelOptions copy, out ModelTemplate existing)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength || !ValidKey(key))
            throw new ArgumentException("key must be 1 to 64 characters of letters, digits, '.', '_' or '-'", nameof(key));
        copy = options != null ? options.Copy() : new ModelOptions();
        string problem = copy.Check();
        if (problem != null)
            throw new ArgumentException(problem, nameof(options));

        string full = ModAssets.ResolveExisting(owner, path);
        existing = null;
        if (set.Models.TryGetValue(key, out var loaded) && loaded.IsAlive)
        {
            if (!string.Equals(loaded.SourcePath, full, StringComparison.Ordinal))
                throw new ArgumentException($"Model key '{key}' is already used for {loaded.SourcePath}.", nameof(key));
            existing = loaded;
        }
        return full;
    }

    private static bool ValidKey(string key)
    {
        foreach (char c in key)
        {
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-'))
                return false;
        }
        return true;
    }

    private static IEnumerator Run(ModAssetSet set, string key, string full, ModelOptions options)
    {
        var task = Task.Run(() => ModelPrepare.Run(key, full, options));
        while (!task.IsCompleted)
            yield return null;
        if (set.Released)
            yield break;

        PreparedModel prepared = task.IsFaulted ? new PreparedModel { Key = key, Path = full, Error = task.Exception.GetBaseException().Message } : task.Result;
        if (prepared.Error != null)
        {
            Complete(set, key, null, key + ": " + prepared.Error);
            yield break;
        }

        var build = new ModelBuild(set, prepared, options);
        build.BuildMesh();
        yield return null;
        while (!set.Released && build.BuildNextMaterial())
            yield return null;
        if (set.Released)
            yield break;

        var template = build.Assemble();
        if (template == null)
            build.Abort();
        else
            Register(set, template, prepared);
        Complete(set, key, template, key + ": " + build.Error);
    }

    private static void Register(ModAssetSet set, ModelTemplate template, PreparedModel prepared)
    {
        set.Models[template.Key] = template;
        foreach (string warning in prepared.Warnings)
            AssetsLog.Warn($"{set.ModId}: model {template.Key}: {warning}");
        AssetsLog.Info($"{set.ModId}: model {template.Key} loaded ({template.VertexCount} vertices, {template.TriangleCount} triangles, {template.Materials.Length} materials).");
    }

    private static void Complete(ModAssetSet set, string key, ModelTemplate template, string error)
    {
        if (!set.ModelWaiting.TryGetValue(key, out var waiting))
            return;
        set.ModelWaiting.Remove(key);
        foreach (var request in waiting)
            Deliver(set, request, template, error ?? "the model could not be built");
    }

    private static void Deliver(ModAssetSet set, ModelRequest request, ModelTemplate template, string error)
    {
        string source = set != null ? set.ModId + ": " : "";
        try
        {
            if (template != null)
                request.Done?.Invoke(template);
            else if (request.Failed != null)
                request.Failed(error);
            else
                AssetsLog.Warn(source + "model not loaded: " + error);
        }
        catch (Exception e)
        {
            AssetsLog.Error(source + "a model callback threw", e);
        }
    }
}
