using System;

namespace ObenLoader.Config;

/// <summary>
/// A <see cref="ConfigFile{T}"/> without its type, for code that lists every mod's settings (the mod menu's
/// Settings page) and edits them through reflection.
/// </summary>
public interface IConfigFile
{
    /// <summary>Full path of the JSON file.</summary>
    string Path { get; }

    /// <summary>The settings class.</summary>
    Type ValueType { get; }

    /// <summary>The settings object as it is now. It is replaced by a reload.</summary>
    object Value { get; }

    /// <summary>A fresh object with the default values.</summary>
    object Defaults { get; }

    /// <summary>Writes <see cref="Value"/> to the file and raises the config's change event.</summary>
    void Save();
}
