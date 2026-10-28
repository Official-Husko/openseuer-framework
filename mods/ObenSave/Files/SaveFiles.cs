#nullable disable
using System;
using System.IO;
using ObenSave.Format;

namespace ObenSave.Files;

/// <summary>What happened to one file in <see cref="SaveFiles.Compress"/> or <see cref="SaveFiles.Restore"/>.</summary>
public enum ConvertResult
{
    /// <summary>The file was rewritten in the other format.</summary>
    Converted,

    /// <summary>The file already was in the wanted format.</summary>
    Unchanged,

    /// <summary>The file can't be converted (for example it is UTF-16) and was left as it is.</summary>
    Skipped,
}

/// <summary>
/// Reading and converting save files on disk. The game's files are written by the game itself; this
/// only ever turns a finished file into the other format, through a temp file in the same folder, so
/// the file is at every moment either the complete old one or the complete new one. No Unity types.
/// </summary>
public static class SaveFiles
{
    private const string TempSuffix = ".obensave.tmp";

    /// <summary>True for the files the game reads through ReadSaveFile: *.tnmt and *.dialogue.</summary>
    public static bool IsSaveFileName(string path)
    {
        string name = Path.GetFileName(path);
        return name.EndsWith(".tnmt", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".dialogue", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the file starts with the container magic. Reads 8 bytes.</summary>
    public static bool IsContainerFile(string path)
    {
        var head = new byte[SaveContainer.MagicSize];
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            int read = ReadFully(stream, head);
            return SaveContainer.StartsWithMagic(head, read);
        }
    }

    /// <summary>
    /// Decodes the file if it is a container. Returns false (and reads no more than 8 bytes) for a
    /// vanilla file, which the caller leaves to the game. Throws for a container that can't be decoded.
    /// </summary>
    public static bool TryReadContainer(string path, out string text)
    {
        text = null;
        if (!IsContainerFile(path))
            return false;
        text = SaveContainer.ReadText(File.ReadAllBytes(path));
        return true;
    }

    /// <summary>The text of a save file in either format, as the game's own ReadSaveFile would return it.</summary>
    public static string ReadText(string path) => SaveContainer.ReadText(File.ReadAllBytes(path));

    /// <summary>Replaces a plain file with a container. The result is checked before the original is touched.</summary>
    public static ConvertResult Compress(string path)
    {
        byte[] original = File.ReadAllBytes(path);
        if (SaveContainer.IsContainer(original))
            return ConvertResult.Unchanged;

        byte[] container;
        try
        {
            container = SaveContainer.Encode(original);
        }
        catch (InvalidDataException)
        {
            return ConvertResult.Skipped;
        }

        if (!Same(SaveContainer.ToVanillaBytes(container), original))
            throw new InvalidDataException("Compressing " + Path.GetFileName(path) + " did not round-trip, left unchanged.");
        Replace(path, container);
        return ConvertResult.Converted;
    }

    /// <summary>Replaces a container with the plain file the game would have written.</summary>
    public static ConvertResult Restore(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (!SaveContainer.IsContainer(data))
            return ConvertResult.Unchanged;

        // Decoding checks the length and CRC-32, so a damaged file is reported and never overwritten.
        Replace(path, SaveContainer.ToVanillaBytes(data));
        return ConvertResult.Converted;
    }

    /// <summary>Writes bytes to a file through a temp file, keeping the old modification time if the file existed.</summary>
    public static void Replace(string path, byte[] bytes)
    {
        DateTime written = File.GetLastWriteTimeUtc(path);
        string temp = path + TempSuffix;
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Replace(temp, path, null);
            File.SetLastWriteTimeUtc(path, written);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = stream.Read(buffer, total, buffer.Length - total);
            if (n <= 0)
                break;
            total += n;
        }
        return total;
    }

    private static bool Same(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }
}
