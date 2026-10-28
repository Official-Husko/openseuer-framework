using System;
using ObenLoader.Config;
using ObenLoader.Logging;

namespace ObenCoop.Identity;

/// <summary>
/// Who this install is to other players: a GUID made once and kept in the mod folder, so a player who
/// comes back is recognised (profiles and saves can be kept per person). A second test copy of the
/// game on the same machine gets its own file, otherwise both would be "the same player" and the
/// host would turn the second away.
/// </summary>
internal static class CoopIdentity
{
    private sealed class IdentityFile
    {
        public string Id = "";
    }

    /// <summary>Reads the identity file, creating it on first use.</summary>
    public static Guid LoadOrCreate(string path, ModLogger log)
    {
        try
        {
            var file = JsonFile.Read<IdentityFile>(path);
            if (Guid.TryParse(file.Id, out var id) && id != Guid.Empty)
                return id;
        }
        catch (Exception e)
        {
            log.Warn($"Couldn't read {path}, making a new identity. {e.Message}");
        }

        var created = Guid.NewGuid();
        try
        {
            JsonFile.Write(path, new IdentityFile { Id = created.ToString("D") });
        }
        catch (Exception e)
        {
            log.Error($"Couldn't save the identity to {path}; it will change on the next start.", e);
        }
        return created;
    }

    public static string FileName(string suffix) => "identity" + suffix + ".json";
}
