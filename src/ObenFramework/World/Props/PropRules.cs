// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.World.Props;

/// <summary>
/// The limits and checks for world props, and the operations on the saved list. No Unity types, so the
/// rules are covered by unit tests; a save file edited by hand or written by a newer version goes
/// through <see cref="Clean"/> before anything is built from it.
/// </summary>
public static class PropRules
{
    public const int MaxPerMod = 256;
    public const int MaxLive = 1024;
    public const int MaxIdLength = 64;
    public const int MaxKeyLength = 128;
    public const int MaxSceneLength = 128;
    public const int MaxInteractKeyLength = 64;
    public const int MaxDataLength = 256;
    public const float MinScale = 0.01f;
    public const float MaxScale = 100f;

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool Finite(float[] values, int length)
    {
        if (values == null || values.Length != length)
            return false;
        foreach (float value in values)
        {
            if (!Finite(value))
                return false;
        }
        return true;
    }

    /// <summary>Scale is finite and between <see cref="MinScale"/> and <see cref="MaxScale"/> on each axis.</summary>
    public static bool ScaleAllowed(float[] scale)
    {
        if (!Finite(scale, 3))
            return false;
        foreach (float axis in scale)
        {
            if (axis < MinScale || axis > MaxScale)
                return false;
        }
        return true;
    }

    public static bool PoseAllowed(float[] position, float[] rotation, float[] scale) =>
        Finite(position, 3) && Finite(rotation, 4) && ScaleAllowed(scale);

    public static bool IdAllowed(string id) => !string.IsNullOrEmpty(id) && id.Length <= MaxIdLength;

    /// <summary>Splits a model name <c>modId/key</c>. The key uses the model registry's characters.</summary>
    public static bool SplitModelKey(string key, out string modId, out string shortKey)
    {
        modId = null;
        shortKey = null;
        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)
            return false;
        int at = key.IndexOf('/');
        if (at <= 0 || at == key.Length - 1)
            return false;
        modId = key.Substring(0, at);
        shortKey = key.Substring(at + 1);
        return PlainKey(shortKey);
    }

    /// <summary>Letters, digits, '.', '_' or '-'.</summary>
    public static bool PlainKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxIdLength)
            return false;
        foreach (char c in key)
        {
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-'))
                return false;
        }
        return true;
    }

    /// <summary>Why a light can't be built, or null when it can.</summary>
    public static string LightProblem(WorldPropLight kind, float range, float intensity, float spotAngle)
    {
        if (kind != WorldPropLight.Point && kind != WorldPropLight.Spot)
            return "the light must be a point or a spot light";
        if (!Finite(range) || range <= 0f)
            return "range must be a finite number above 0";
        if (!Finite(intensity) || intensity < 0f)
            return "intensity must be a finite number that is not negative";
        if (!Finite(spotAngle) || spotAngle <= 0f || spotAngle >= 180f)
            return "spotAngle must be a finite number between 0 and 180";
        if (kind == WorldPropLight.Spot && spotAngle < 1f)
            return "spotAngle must be at least 1 for a spot light";
        return null;
    }

    /// <summary>Whether the kind-specific fields of a record make sense.</summary>
    public static bool KindAllowed(PropRecord record)
    {
        switch (record.Kind)
        {
            case WorldPropKind.Model:
                return SplitModelKey(record.Key, out _, out _);
            case WorldPropKind.Primitive:
                return record.Alt >= (int)WorldPrimitive.Cube && record.Alt <= (int)WorldPrimitive.Plane && string.IsNullOrEmpty(record.Key);
            case WorldPropKind.Furniture:
                return !string.IsNullOrEmpty(record.Key) && record.Key.Length <= MaxKeyLength && record.Alt >= 0;
            case WorldPropKind.Item:
                return record.Alt > 0 && string.IsNullOrEmpty(record.Key);
            case WorldPropKind.Light:
                return Finite(record.LightColor, 4)
                    && LightProblem(record.LightKind, record.LightRange, record.LightIntensity, record.LightSpotAngle) == null;
            case WorldPropKind.Anchor:
                return string.IsNullOrEmpty(record.Key);
            default:
                return false;
        }
    }

    /// <summary>Whether a whole record is safe to keep and build.</summary>
    public static bool Valid(PropRecord record)
    {
        if (record == null || string.IsNullOrEmpty(record.Mod) || record.Mod.Length > MaxKeyLength)
            return false;
        if (!IdAllowed(record.Id))
            return false;
        if (string.IsNullOrEmpty(record.Scene) || record.Scene.Length > MaxSceneLength)
            return false;
        if (!PoseAllowed(record.Position, record.Rotation, record.Scale))
            return false;
        if (record.InteractKey != null && record.InteractKey.Length > MaxInteractKeyLength)
            return false;
        if (record.Title != null && record.Title.Length > MaxInteractKeyLength)
            return false;
        if (record.Data != null && record.Data.Length > MaxDataLength)
            return false;
        if (!record.Collider && !string.IsNullOrEmpty(record.InteractKey))
            return false;
        return KindAllowed(record);
    }

    /// <summary>A copy of <paramref name="file"/> with only valid records, at most <see cref="MaxPerMod"/> per mod and no duplicate ids.</summary>
    public static PropFile Clean(PropFile file)
    {
        var clean = new PropFile();
        if (file == null || file.Props == null)
            return clean;
        foreach (var record in file.Props)
        {
            if (!Valid(record) || !HasRoom(clean, record.Mod, record.Id))
                continue;
            record.Key ??= "";
            record.InteractKey ??= "";
            record.Title ??= "";
            record.Data ??= "";
            Put(clean, record);
        }
        return clean;
    }

    public static PropRecord Find(PropFile file, string mod, string id)
    {
        foreach (var record in file.Props)
        {
            if (record.Mod == mod && record.Id == id)
                return record;
        }
        return null;
    }

    /// <summary>A mod may replace one of its props or add one while it has fewer than <see cref="MaxPerMod"/>.</summary>
    public static bool HasRoom(PropFile file, string mod, string id) =>
        Find(file, mod, id) != null || Count(file, mod) < MaxPerMod;

    /// <summary>Adds the record, replacing any with the same mod and id.</summary>
    public static void Put(PropFile file, PropRecord record)
    {
        file.Props.RemoveAll(r => r.Mod == record.Mod && r.Id == record.Id);
        file.Props.Add(record);
    }

    /// <summary>Removes one prop of a mod, or all of them when <paramref name="id"/> is null. Returns how many went.</summary>
    public static int Drop(PropFile file, string mod, string id) =>
        file.Props.RemoveAll(r => r.Mod == mod && (id == null || r.Id == id));

    public static int Count(PropFile file, string mod)
    {
        int count = 0;
        foreach (var record in file.Props)
        {
            if (record.Mod == mod)
                count++;
        }
        return count;
    }

    /// <summary>The records that belong in <paramref name="scene"/>, in the order they were placed.</summary>
    public static List<PropRecord> InScene(PropFile file, string scene) => file.Props.FindAll(r => r.Scene == scene);
}
