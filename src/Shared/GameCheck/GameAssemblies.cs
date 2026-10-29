#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Oben.Checks;

public enum TypeStatus
{
    Found,
    /// <summary>The assembly was read but has no such type.</summary>
    Missing,
    /// <summary>The assembly the type should be in couldn't be found at all.</summary>
    NoAssembly,
}

/// <summary>
/// The installed game's assemblies, read with Cecil. Mod DLLs are loaded through the same resolver, so
/// a type reference in a mod resolves against the game's own files.
/// </summary>
public sealed class GameAssemblies : IDisposable
{
    private static readonly string[] CodeAssemblies = { "Assembly-CSharp.dll", "Assembly-CSharp-firstpass.dll" };

    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly List<ModuleDefinition> _loaded = new();
    private readonly List<ModuleDefinition> _code = new();

    public string ManagedDir { get; }

    public GameAssemblies(string managedDir)
    {
        ManagedDir = managedDir;
        _resolver.AddSearchDirectory(managedDir);
        foreach (string name in CodeAssemblies)
        {
            string path = Path.Combine(managedDir, name);
            if (File.Exists(path))
                _code.Add(Load(path));
        }
        if (_code.Count == 0)
            throw new FileNotFoundException($"No Assembly-CSharp.dll in {managedDir}.", managedDir);
    }

    /// <summary>Mods can reference each other, so their folders are searched too.</summary>
    public void AddSearchDirectory(string dir) => _resolver.AddSearchDirectory(dir);

    public ModuleDefinition Load(string path)
    {
        var module = ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = _resolver });
        _loaded.Add(module);
        return module;
    }

    /// <summary>Looks a type up by full name ("Namespace.Outer/Nested") in the game's own code assemblies.</summary>
    public TypeDefinition? FindType(string fullName)
    {
        fullName = fullName.Replace('+', '/');
        foreach (var module in _code)
        {
            var type = module.GetType(fullName);
            if (type != null)
                return type;
        }
        return null;
    }

    public TypeStatus Resolve(TypeReference reference, out TypeDefinition? definition)
    {
        definition = null;
        try
        {
            definition = reference.GetElementType().Resolve();
            return definition != null ? TypeStatus.Found : TypeStatus.Missing;
        }
        catch (AssemblyResolutionException)
        {
            return TypeStatus.NoAssembly;
        }
    }

    public void Dispose()
    {
        foreach (var module in _loaded)
            module.Dispose();
        _resolver.Dispose();
    }
}
