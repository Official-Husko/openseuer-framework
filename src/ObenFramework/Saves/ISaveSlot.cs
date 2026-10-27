namespace ObenFramework.Saves;

/// <summary>What the registry needs from a registered <see cref="ModSaveData{T}"/>, whatever its type.</summary>
internal interface ISaveSlot
{
    /// <summary>File name inside the save's mods folder.</summary>
    string FileName { get; }

    /// <summary>Replaces the value with the file's content, or with defaults when there is none.</summary>
    void ReadFrom(string saveFolder);

    /// <summary>Writes the current value into the save folder. Failures are logged, never thrown.</summary>
    void WriteTo(string saveFolder);

    /// <summary>Back to a new object, as for a new game.</summary>
    void Reset();

    /// <summary>The value as JSON text, for handing to a reloaded copy of the mod.</summary>
    string Snapshot();

    void Restore(string json);
}
