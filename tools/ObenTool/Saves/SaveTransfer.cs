using System.Text.Encodings.Web;
using System.Text.Json;
using ObenSave.Files;
using ObenSave.Format;

namespace ObenTool.Saves;

/// <summary>Copies a save folder somewhere else, converting every save file to the wanted format on the way.</summary>
public static class SaveTransfer
{
    public sealed record Result(int Files, int Converted, long BytesIn, long BytesOut);

    /// <summary>Writes a slot's files into <paramref name="destination"/>. The slot itself is not changed.</summary>
    public static Result Export(SaveSlot slot, string destination, bool compressed, bool pretty, bool force)
    {
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any() && !force)
            throw new ArgumentException($"{destination} is not empty. Pick another folder or pass --force.");
        return CopyConverted(slot.Path, destination, compressed, pretty);
    }

    /// <summary>
    /// Writes a folder of save files (either format) into a slot. Everything is converted and checked in a
    /// staging folder first, so a bad input never touches an existing slot.
    /// </summary>
    public static Result Import(string source, SaveSlot target, bool compressed, bool force)
    {
        if (!SaveStore.HasSave(source))
            throw new ArgumentException($"{source} has no Globals.tnmt or Info.tnmt, so it is not a save folder.");
        if (Directory.Exists(target.Path) && !force)
            throw new ArgumentException($"{target.Character}/{target.Name} already exists. Pass --force to replace it.");

        string staging = target.Path + ".importing";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);

        try
        {
            var result = CopyConverted(source, staging, compressed, pretty: false);
            if (Directory.Exists(target.Path))
                Directory.Delete(target.Path, true);
            Directory.Move(staging, target.Path);
            return result;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    private static Result CopyConverted(string source, string destination, bool compressed, bool pretty)
    {
        if (pretty && compressed)
            throw new ArgumentException("--pretty makes readable files, so it only works with --format plain.");

        Directory.CreateDirectory(destination);
        int files = 0, converted = 0;
        long bytesIn = 0, bytesOut = 0;
        foreach (string path in Directory.GetFiles(source, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            byte[] data = File.ReadAllBytes(path);
            byte[] output = SaveFiles.IsSaveFileName(path) ? Convert(data, path, compressed, pretty) : data;
            File.WriteAllBytes(target, output);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(path));

            files++;
            bytesIn += data.Length;
            bytesOut += output.Length;
            if (!ReferenceEquals(data, output) && !data.AsSpan().SequenceEqual(output))
                converted++;
        }
        return new Result(files, converted, bytesIn, bytesOut);
    }

    private static byte[] Convert(byte[] data, string path, bool compressed, bool pretty)
    {
        // Reading as plain first checks the container (length and CRC-32) before anything is written.
        byte[] plain = SaveContainer.ToVanillaBytes(data);
        if (compressed)
        {
            try
            {
                return SaveContainer.Encode(plain);
            }
            catch (InvalidDataException)
            {
                return data;
            }
        }
        if (pretty && path.EndsWith(".tnmt", StringComparison.OrdinalIgnoreCase))
            return Indent(plain);
        return plain;
    }

    // Rewrites the JSON with indentation. Numbers are copied as written; strings may be escaped differently.
    private static byte[] Indent(byte[] plain)
    {
        var info = SaveContainer.Inspect(plain);
        string text = SaveContainer.ReadText(plain);
        using var doc = JsonDocument.Parse(text);
        using var stream = new MemoryStream();
        if (info.HasBom)
            stream.Write(new byte[] { 0xEF, 0xBB, 0xBF });
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            doc.RootElement.WriteTo(writer);
        return stream.ToArray();
    }
}
