using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;

namespace ObenLoader.Mods;

/// <summary>
/// Finds what Cecil has to look into while <see cref="AssemblyRewriter"/> writes an assembly again: an
/// enum from another assembly as a parameter's default value, for example, needs that enum's underlying
/// type. A reference to a renamed copy ("ObenCoop.Net-r2") is read from the original file, which has the
/// same metadata; mods' DLLs come from their folders, everything else from where the running copy was
/// loaded. Files are read into memory, so nothing stays open for the next build to trip over.
/// </summary>
internal sealed class RewriteResolver : IAssemblyResolver
{
    private readonly Dictionary<string, AssemblyDefinition> _read = new Dictionary<string, AssemblyDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, string> _originalName;

    public RewriteResolver(Func<string, string> originalName)
    {
        _originalName = originalName;
    }

    public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());

    public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
    {
        string simple = _originalName(name.Name);
        if (_read.TryGetValue(simple, out var known))
            return known;
        string path = Find(simple) ?? throw new AssemblyResolutionException(name);
        var definition = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(path)), new ReaderParameters { AssemblyResolver = this });
        _read[simple] = definition;
        return definition;
    }

    public void Dispose()
    {
        foreach (var definition in _read.Values)
            definition.Dispose();
        _read.Clear();
    }

    private static string Find(string simple)
    {
        if (AssemblyResolver.TryGetPath(simple, out string path) && File.Exists(path))
            return path;
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (loaded.IsDynamic || !string.Equals(loaded.GetName().Name, simple, StringComparison.OrdinalIgnoreCase))
                continue;
            string location;
            try
            {
                location = loaded.Location;
            }
            catch (NotSupportedException)
            {
                continue;
            }
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                return location;
        }
        return null;
    }
}
