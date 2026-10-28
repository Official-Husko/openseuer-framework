using ObenFramework.Console;
using ObenLoader;
using ObenSave.Features;
using ObenSave.Files;

namespace ObenSave.Commands;

/// <summary>The "obensave" console command: count save files by format, or convert them all.</summary>
internal static class SaveConsoleCommand
{
    private const string Usage = "Usage: obensave status | compress | restore";

    public static void Register(ObenMod owner) =>
        GameConsole.Register(owner, "obensave", "obensave [status|compress|restore]", Run);

    private static string Run(string[] args)
    {
        string action = args.Length == 0 ? "status" : args[0];
        string folder = CompressedSavesFeature.SavesFolder;
        switch (action)
        {
            case "status":
                SaveFolders.Count(folder, out int plain, out int containers);
                return $"Save files in {folder}: {containers} compressed, {plain} plain.";
            case "compress":
                return Convert(folder, true);
            case "restore":
                return Convert(folder, false);
            default:
                return Usage;
        }
    }

    private static string Convert(string folder, bool compress)
    {
        if (SaveController.Loading)
            return "A save is loading right now, try again in a moment.";
        var summary = SaveConversion.Run(folder, compress);
        return $"{(compress ? "Compress" : "Restore")}: {summary}.";
    }
}
