#nullable disable
using System;
using System.Collections.Generic;
using System.IO;

namespace ObenSave.Files;

/// <summary>What converting a folder tree did.</summary>
public sealed class ConversionSummary
{
    public int Converted;
    public int Unchanged;
    public int Skipped;
    public int Failed;
    public long BytesBefore;
    public long BytesAfter;

    public override string ToString() =>
        $"{Converted} converted, {Unchanged} already done, {Skipped} skipped, {Failed} failed"
        + (Converted > 0 ? $" ({BytesBefore / 1024} KB -> {BytesAfter / 1024} KB)" : "");
}

/// <summary>Converts every save file under a folder (a Saves folder, a character, or one slot). No Unity types.</summary>
public static class SaveFolders
{
    /// <summary>
    /// Compresses (or restores) every *.tnmt and *.dialogue file below <paramref name="root"/>. A file that
    /// fails is reported through <paramref name="onError"/> and left as it was; the rest carry on.
    /// </summary>
    public static ConversionSummary Convert(string root, bool compress, Action<string, Exception> onError = null)
    {
        var summary = new ConversionSummary();
        if (!Directory.Exists(root))
            return summary;

        foreach (string path in Files(root))
        {
            try
            {
                long before = new FileInfo(path).Length;
                var result = compress ? SaveFiles.Compress(path) : SaveFiles.Restore(path);
                switch (result)
                {
                    case ConvertResult.Converted:
                        summary.Converted++;
                        summary.BytesBefore += before;
                        summary.BytesAfter += new FileInfo(path).Length;
                        break;
                    case ConvertResult.Unchanged:
                        summary.Unchanged++;
                        break;
                    default:
                        summary.Skipped++;
                        break;
                }
            }
            catch (Exception e)
            {
                summary.Failed++;
                onError?.Invoke(path, e);
            }
        }
        return summary;
    }

    /// <summary>Counts the save files below a folder by format.</summary>
    public static void Count(string root, out int plain, out int containers)
    {
        plain = 0;
        containers = 0;
        if (!Directory.Exists(root))
            return;
        foreach (string path in Files(root))
        {
            try
            {
                if (SaveFiles.IsContainerFile(path))
                    containers++;
                else
                    plain++;
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The save files below a folder, in a stable order.</summary>
    public static List<string> Files(string root)
    {
        var files = new List<string>();
        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            if (SaveFiles.IsSaveFileName(path))
                files.Add(path);
        }
        files.Sort(StringComparer.Ordinal);
        return files;
    }
}
