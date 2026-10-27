using System.IO;
using System.Text;

namespace ObenFramework.Saves;

/// <summary>Where mod data lives inside a save folder.</summary>
internal static class SaveFolders
{
    /// <summary>
    /// Sub folder of a save holding all mod files. The game's own files are Info.tnmt, Globals.tnmt,
    /// one tnmt per level, Globals.dialogue and the map PNG; none of them collide with this.
    /// </summary>
    public const string ModsFolder = "mods";

    public static string FileFor(string saveFolder, string fileName) =>
        Path.Combine(saveFolder, ModsFolder, fileName);

    /// <summary>
    /// File name for a mod's data: "&lt;modId&gt;.json", or "&lt;modId&gt;.&lt;key&gt;.json" for further
    /// objects of the same mod. Characters that don't belong in a file name become underscores.
    /// </summary>
    public static string FileName(string modId, string key)
    {
        var name = new StringBuilder(Clean(modId));
        if (!string.IsNullOrEmpty(key))
            name.Append('.').Append(Clean(key));
        return name.Append(".json").ToString();
    }

    /// <summary>The folder the game loaded last, or null at the main menu and in a new game.</summary>
    public static string LoadedFolder()
    {
        string folder = SaveController.LastSaveFolderPath;
        return string.IsNullOrEmpty(folder) ? null : folder;
    }

    private static string Clean(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                chars[i] = '_';
        }
        return new string(chars);
    }
}
