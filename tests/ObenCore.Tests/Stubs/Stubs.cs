// Stand-ins for the few types the linked sources touch outside the set that is linked.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    /// <summary>Only here so Disposable.Destroying compiles.</summary>
    public class Object
    {
        public static void Destroy(Object obj) { }
    }
}

namespace ObenLoader
{
    /// <summary>ModInfo.Instance has this type.</summary>
    public class ObenMod { }
}

namespace ObenLoader.Mods
{
    /// <summary>
    /// The mods the sorter treats as already running. The real registry is a Unity-bound static;
    /// the sorter only ever calls Find.
    /// </summary>
    internal static class ModRegistry
    {
        public static readonly Dictionary<string, ModInfo> Running = new();

        public static ModInfo Find(string id) => id != null && Running.TryGetValue(id, out var mod) ? mod : null;
    }
}

namespace ObenLoader.Logging
{
    /// <summary>Records errors instead of writing to the loader's log hub.</summary>
    public sealed class ModLogger
    {
        public List<string> Errors { get; } = new();

        public void Error(string message, Exception e) => Errors.Add(message + "\n" + e);
    }
}
