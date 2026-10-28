namespace ObenTool.Game;

/// <summary>
/// The game's Obenseuer_Data/boot.config. Unity's "force single instance" is the empty
/// <c>single-instance=</c> line in it: while it's there a second copy of the game quits at once,
/// which rules out testing co-op on one machine. The installer can take it out (keeping a backup)
/// and put it back.
/// </summary>
public sealed class BootConfig
{
    private const string SingleInstanceLine = "single-instance=";
    private const string BackupSuffix = ".obenbak";

    private readonly string _path;

    public BootConfig(GameInstall game)
    {
        _path = Path.Combine(game.DataDir, "boot.config");
    }

    public bool AllowsMultipleCopies =>
        !File.Exists(_path) || !File.ReadAllLines(_path).Any(l => l.Trim() == SingleInstanceLine);

    public string AllowMultipleCopies()
    {
        if (AllowsMultipleCopies)
            return "More than one copy of the game can already run";
        string backup = _path + BackupSuffix;
        if (!File.Exists(backup))
            File.Copy(_path, backup);
        File.WriteAllLines(_path, File.ReadAllLines(_path).Where(l => l.Trim() != SingleInstanceLine));
        return "More than one copy of the game can run now (boot.config, backup kept)";
    }

    /// <summary>Puts the game's own boot.config back if the installer ever changed it.</summary>
    public string? Restore()
    {
        string backup = _path + BackupSuffix;
        if (!File.Exists(backup))
            return null;
        File.Copy(backup, _path, overwrite: true);
        File.Delete(backup);
        return "Restored boot.config";
    }
}
