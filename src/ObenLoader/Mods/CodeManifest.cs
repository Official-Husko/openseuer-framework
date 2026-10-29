// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.IO;
using Mono.Cecil;

namespace ObenLoader.Mods;

/// <summary>
/// A mod declared in code: for a mod folder without a mod.json, the first DLL in it with a class marked
/// <see cref="ObenModInfoAttribute"/> gives the manifest. The DLL is read with Cecil, not loaded, so a folder
/// that turns out not to be a mod costs nothing.
/// </summary>
internal static class CodeManifest
{
    private const string InfoAttribute = "ObenLoader.ObenModInfoAttribute";
    private const string DependencyAttribute = "ObenLoader.ObenModDependencyAttribute";

    /// <summary>The manifest of the code-declared mod in <paramref name="dir"/>, or null when there is none.</summary>
    public static ModManifest Read(string dir, Action<string> warn)
    {
        foreach (string dll in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
        {
            var manifest = ReadAssembly(dll, warn);
            if (manifest != null)
                return manifest;
        }
        return null;
    }

    /// <summary>The manifest from one DLL, or null when no class in it carries the attribute.</summary>
    public static ModManifest ReadAssembly(string path, Action<string> warn)
    {
        try
        {
            using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadingMode = ReadingMode.Deferred });
            foreach (var type in assembly.MainModule.Types)
            {
                var info = Find(type, InfoAttribute);
                if (info != null)
                    return Build(type, info, Path.GetFileName(path));
            }
        }
        catch (Exception e) when (e is BadImageFormatException || e is IOException || e is UnauthorizedAccessException)
        {
            warn?.Invoke($"Couldn't read {Path.GetFileName(path)} for a mod declared in code: {e.Message}");
        }
        return null;
    }

    private static ModManifest Build(TypeDefinition type, CustomAttribute info, string dll)
    {
        var manifest = new ModManifest
        {
            Id = Text(info, 0),
            Name = Text(info, 1),
            Version = Text(info, 2) ?? "0.0.0",
        };
        // Only this DLL is the mod; others next to it are its libraries, as with an assemblies list.
        manifest.Assemblies.Add(dll);
        foreach (var property in info.Properties)
        {
            object value = property.Argument.Value;
            switch (property.Name)
            {
                case nameof(ObenModInfoAttribute.Author): manifest.Author = value as string; break;
                case nameof(ObenModInfoAttribute.Description): manifest.Description = value as string; break;
                case nameof(ObenModInfoAttribute.GameVersion): manifest.GameVersion = value as string; break;
                case nameof(ObenModInfoAttribute.AnyGameVersion): manifest.AnyGameVersion = value is bool any && any; break;
                case nameof(ObenModInfoAttribute.HotReload): manifest.HotReload = value is bool reload && reload; break;
            }
        }
        foreach (var attribute in type.CustomAttributes)
        {
            if (attribute.AttributeType.FullName == DependencyAttribute && Text(attribute, 0) is string id)
                manifest.Dependencies[id] = Text(attribute, 1) ?? "*";
        }
        return manifest;
    }

    private static CustomAttribute Find(TypeDefinition type, string name)
    {
        foreach (var attribute in type.CustomAttributes)
        {
            if (attribute.AttributeType.FullName == name)
                return attribute;
        }
        return null;
    }

    private static string Text(CustomAttribute attribute, int index) =>
        index < attribute.ConstructorArguments.Count ? attribute.ConstructorArguments[index].Value as string : null;
}
