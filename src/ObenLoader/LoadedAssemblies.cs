using System.Reflection;
using ObenLoader.Mods;

namespace ObenLoader;

/// <summary>Information about assemblies the loader manages.</summary>
public static class LoadedAssemblies
{
    /// <summary>
    /// True for old copies left behind by hot reloads. Reflection helpers should skip these so they
    /// find the types that are actually running.
    /// </summary>
    public static bool IsStale(Assembly assembly) => AssemblyLoading.IsStale(assembly);
}
