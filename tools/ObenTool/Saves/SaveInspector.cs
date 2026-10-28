using System.Text.Json;
using ObenSave.Files;
using ObenSave.Format;

namespace ObenTool.Saves;

/// <summary>What is known about one save file: its format and, for a readable .tnmt, the header the game stores in it.</summary>
public sealed class FileReport
{
    public string Name { get; init; } = "";
    public SaveFileInfo? Format { get; init; }

    /// <summary>Set when the file could not be read or decoded.</summary>
    public string? Problem { get; init; }

    public string? SaveName { get; init; }
    public string? LevelName { get; init; }
    public string? CharacterName { get; init; }
    public string? Date { get; init; }
    public string? ApplicationVersion { get; init; }

    /// <summary>Number of saved objects, or -1 when the file has no object list.</summary>
    public int Objects { get; init; } = -1;

    public List<KeyValuePair<string, int>> Types { get; init; } = new();
}

/// <summary>Reads save files of either format for display.</summary>
public static class SaveInspector
{
    public static FileReport Inspect(string path)
    {
        string name = Path.GetFileName(path);
        try
        {
            byte[] data = File.ReadAllBytes(path);
            var format = SaveContainer.Inspect(data);
            string text = SaveContainer.ReadText(data);
            return name.EndsWith(".tnmt", StringComparison.OrdinalIgnoreCase)
                ? ReadHeader(name, format, text)
                : new FileReport { Name = name, Format = format };
        }
        catch (Exception e) when (e is InvalidDataException or IOException or JsonException)
        {
            return new FileReport { Name = name, Problem = e.Message };
        }
    }

    private static FileReport ReadHeader(string name, SaveFileInfo format, string text)
    {
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        int objects = -1;
        var types = new Dictionary<string, int>();
        if (root.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            objects = data.GetArrayLength();
            foreach (var entry in data.EnumerateArray())
            {
                string type = "?";
                if (entry.TryGetProperty("Data", out var inner) && inner.ValueKind == JsonValueKind.Object
                    && inner.TryGetProperty("$type", out var t) && t.ValueKind == JsonValueKind.String)
                    type = t.GetString() ?? "?";
                types[type] = types.GetValueOrDefault(type) + 1;
            }
        }

        return new FileReport
        {
            Name = name,
            Format = format,
            SaveName = Text(root, "SaveName"),
            LevelName = Text(root, "LevelName"),
            CharacterName = Text(root, "CharacterName"),
            Date = Text(root, "Date"),
            ApplicationVersion = Text(root, "ApplicationVersion"),
            Objects = objects,
            Types = types.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList(),
        };
    }

    private static string? Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>"412 KB" or "1.2 MB".</summary>
    public static string Size(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:0.0} MB" : $"{Math.Max(1, (bytes + 1023) / 1024)} KB";

    /// <summary>"plain", "compressed" or "mixed" for the save files below a folder; "none" when there are none.</summary>
    public static string FormatOf(string folder)
    {
        SaveFolders.Count(folder, out int plain, out int containers);
        return (plain, containers) switch
        {
            (0, 0) => "none",
            (_, 0) => "plain",
            (0, _) => "compressed",
            _ => "mixed",
        };
    }
}
