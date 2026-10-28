using System;
using System.IO;
using ObenLoader;
using ObenLoader.Config;

namespace ObenUIKit.Menu.Changelog;

/// <summary>
/// Reads changelog.json from the kit's own folder, where the build copies it from the repo root.
/// The file is read again when it changes, so a rebuilt changelog shows up without a restart.
/// </summary>
internal static class ChangelogFeed
{
    public const string FileName = "changelog.json";

    private static string _path;
    private static ChangelogDocument _document;
    private static DateTime _stamp;

    public static void Init(ObenMod kit)
    {
        _path = kit.PathTo(FileName);
        _document = null;
    }

    /// <summary>The changelog, or null when the mod folder has none or it can't be read.</summary>
    public static ChangelogDocument Load()
    {
        if (_path == null || !File.Exists(_path))
            return null;
        DateTime stamp = File.GetLastWriteTimeUtc(_path);
        if (_document == null || stamp != _stamp)
        {
            JsonFile.TryRead(_path, out _document, out _);
            _stamp = stamp;
        }
        return _document;
    }
}
