using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ObenLoader.Mods;

/// <summary>
/// Lets mods reference each other's DLLs. Mono only searches Obenseuer_Data/Managed on its own, so
/// anything in a mod folder is resolved here by simple name, always to the newest loaded copy
/// (under its original name and its reload name).
/// </summary>
internal static class AssemblyResolver
{
    private static readonly Dictionary<string, string> PathsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Assembly> Current = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
    private static bool _hooked;

    public static void RegisterPath(string dllPath)
    {
        PathsByName[Path.GetFileNameWithoutExtension(dllPath)] = dllPath;
        if (_hooked)
            return;
        AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        _hooked = true;
    }

    /// <summary>Where a mod's DLL of this simple name is, if one was registered.</summary>
    public static bool TryGetPath(string name, out string path) => PathsByName.TryGetValue(name, out path);

    public static void SetCurrent(Assembly assembly, string originalName)
    {
        Current[originalName] = assembly;
        Current[assembly.GetName().Name] = assembly;
    }

    private static Assembly OnResolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name;

        if (Current.TryGetValue(name, out var current))
            return current;

        return PathsByName.TryGetValue(name, out string path) && File.Exists(path)
            ? Assembly.LoadFrom(path)
            : null;
    }
}
