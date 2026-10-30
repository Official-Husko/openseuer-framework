#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Mono.Cecil;

namespace Oben.Reports;

/// <summary>BepInEx as found in a game folder: its version, how it hooks in, and every plugin and patcher.</summary>
public sealed class BepInExInfo
{
    /// <summary>The version of its core assembly (BepInEx.dll for 5, BepInEx.Core.dll for 6).</summary>
    public string? Version;

    /// <summary>The proxy that starts it with the game ("winhttp.dll"), null when there is none.</summary>
    public string? Doorstop;

    public List<PluginEntry> Plugins = new List<PluginEntry>();
    public List<PluginEntry> Patchers = new List<PluginEntry>();
}

/// <summary>One DLL in BepInEx's plugins or patchers folder.</summary>
public sealed class PluginEntry
{
    /// <summary>Path inside the BepInEx folder, with forward slashes: "plugins/SomeMod/SomeMod.dll".</summary>
    public string? File;

    /// <summary>From the plugin's [BepInPlugin] attribute; null for patchers and plain libraries.</summary>
    public string? Guid;

    public string? Name;
    public string? Version;

    /// <summary>The first 12 hex digits of the file's SHA-256, to tell two builds of one version apart.</summary>
    public string? Sha256;
}

/// <summary>Looks for BepInEx in a game folder without loading anything from it.</summary>
public static class BepInExScan
{
    private static readonly string[] Proxies = { "winhttp.dll", "version.dll", "winmm.dll" };

    /// <summary>What is installed, or null when the folder has no BepInEx.</summary>
    public static BepInExInfo? Read(string gameDir)
    {
        string root = Path.Combine(gameDir, "BepInEx");
        if (!Directory.Exists(root))
            return null;

        var info = new BepInExInfo();
        foreach (string core in new[] { "core/BepInEx.dll", "core/BepInEx.Core.dll" })
        {
            string path = Path.Combine(root, core);
            if (info.Version == null && System.IO.File.Exists(path))
                info.Version = AssemblyVersion(path);
        }
        foreach (string proxy in Proxies)
        {
            if (System.IO.File.Exists(Path.Combine(gameDir, proxy)) && System.IO.File.Exists(Path.Combine(gameDir, "doorstop_config.ini")))
            {
                info.Doorstop = proxy;
                break;
            }
        }
        Collect(root, "plugins", info.Plugins);
        Collect(root, "patchers", info.Patchers);
        return info;
    }

    private static void Collect(string root, string folder, List<PluginEntry> into)
    {
        string dir = Path.Combine(root, folder);
        if (!Directory.Exists(dir))
            return;
        var files = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            var entry = new PluginEntry { File = folder + "/" + file.Substring(dir.Length + 1).Replace('\\', '/'), Sha256 = ShortHash(file) };
            ReadPlugin(file, entry);
            into.Add(entry);
        }
    }

    // The [BepInPlugin(guid, name, version)] of the first plugin class, else the assembly's own name and version.
    private static void ReadPlugin(string path, PluginEntry entry)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadSymbols = false });
            entry.Name = assembly.Name.Name;
            entry.Version = assembly.Name.Version?.ToString();
            foreach (var type in assembly.MainModule.Types)
            {
                foreach (var attribute in type.CustomAttributes)
                {
                    if (attribute.AttributeType.Name != "BepInPlugin" || attribute.ConstructorArguments.Count < 3)
                        continue;
                    entry.Guid = attribute.ConstructorArguments[0].Value as string;
                    entry.Name = attribute.ConstructorArguments[1].Value as string ?? entry.Name;
                    entry.Version = attribute.ConstructorArguments[2].Value as string ?? entry.Version;
                    return;
                }
            }
        }
        catch (Exception)
        {
            // Not a .NET assembly (a native helper) or unreadable: the file name and hash still say what it is.
        }
    }

    private static string? AssemblyVersion(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadSymbols = false });
            return assembly.Name.Version?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ShortHash(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Substring(0, 12).ToLowerInvariant();
        }
        catch (IOException)
        {
            return null;
        }
    }
}
