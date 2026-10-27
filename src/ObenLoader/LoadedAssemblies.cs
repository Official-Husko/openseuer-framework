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

    /// <summary>
    /// Loads an assembly from memory (a compiled script, say) so that its references to mod
    /// assemblies bind to the copies running now, not the first ones Mono saw before a hot reload.
    /// Give each image its own assembly name; Mono never loads the same name twice.
    /// </summary>
    public static Assembly LoadFromMemory(byte[] image, byte[] symbols = null)
    {
        if (AssemblyRewriter.Active)
            (image, symbols) = AssemblyRewriter.Prepare(image, symbols, rename: false);
        return symbols != null ? Assembly.Load(image, symbols) : Assembly.Load(image);
    }
}
