using ObenSave.Files;

namespace ObenSave;

/// <summary>Runs a conversion of a save folder and logs what it did and what failed.</summary>
internal static class SaveConversion
{
    public static ConversionSummary Run(string folder, bool compress)
    {
        var log = ObenSaveMod.Logger;
        var summary = SaveFolders.Convert(folder, compress,
            (path, e) => log?.Error($"Couldn't convert {path}, left as it was", e));
        if (summary.Converted > 0 || summary.Failed > 0)
            log?.Info($"{(compress ? "Compressed" : "Restored")} {folder}: {summary}");
        return summary;
    }
}
