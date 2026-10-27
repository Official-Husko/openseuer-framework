using System.Collections.Generic;

namespace ObenLoader.Runtime;

/// <summary>
/// A place for mods (and the framework) to park state while they are reloaded. It lives in the
/// loader, which is never reloaded, so data survives even when the framework itself reloads.
/// Values should be plain data (strings, JSON) rather than objects from the reloading assembly.
/// </summary>
public static class ReloadState
{
    private static readonly Dictionary<string, object> Values = new Dictionary<string, object>();

    public static void Put(string key, object value) => Values[key] = value;

    public static bool Take<T>(string key, out T value)
    {
        if (Values.TryGetValue(key, out var raw) && raw is T typed)
        {
            Values.Remove(key);
            value = typed;
            return true;
        }
        value = default;
        return false;
    }
}
