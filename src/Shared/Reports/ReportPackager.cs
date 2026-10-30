#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Oben.Reports;

/// <summary>One file in a report zip: copied from disk, or text made for the report.</summary>
public sealed class ReportEntry
{
    /// <summary>Path inside the zip, with forward slashes.</summary>
    public string Name = "";

    /// <summary>The file to copy, or null for <see cref="Text"/>.</summary>
    public string? Source;

    public string? Text;

    /// <summary>Text the redactor goes over. Binary files (saves) are copied as they are.</summary>
    public bool IsText = true;

    /// <summary>A longer text file keeps only its end, where the newest lines are. 0 for no limit.</summary>
    public long MaxBytes;
}

/// <summary>What <see cref="ReportPackager.Write"/> made.</summary>
public sealed class ReportOutcome
{
    public string Path = "";
    public long Bytes;
    public List<string> Files = new List<string>();

    /// <summary>Files left out or cut, said plainly ("logs/Player.log: only the last 8 MB").</summary>
    public List<string> Notes = new List<string>();

    /// <summary>Larger than GitHub takes as an attachment.</summary>
    public bool TooBigForGitHub => Bytes > ReportPackager.GitHubLimit;
}

/// <summary>
/// Writes a report zip. Text files are read while the game may still be writing them, cut to their end when too long,
/// and go through the redactor; every text is learned from before any is written, so a user name that shows up in
/// one log is hidden in all of them.
/// </summary>
public static class ReportPackager
{
    /// <summary>GitHub's limit for a file attached to an issue.</summary>
    public const long GitHubLimit = 25L * 1024 * 1024;

    public static ReportOutcome Write(string zipPath, IEnumerable<ReportEntry> entries, ReportRedactor? redactor)
    {
        var outcome = new ReportOutcome { Path = zipPath };
        var texts = new List<(ReportEntry Entry, string Text)>();
        var binaries = new List<ReportEntry>();
        foreach (var entry in entries)
        {
            if (!entry.IsText)
            {
                if (entry.Source != null && File.Exists(entry.Source))
                    binaries.Add(entry);
                continue;
            }
            string? text = entry.Text ?? ReadText(entry, outcome.Notes);
            if (text == null)
                continue;
            redactor?.Learn(text);
            texts.Add((entry, text));
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(zipPath)!);
        string temp = zipPath + ".part";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (var (entry, text) in texts)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry.Name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
                writer.Write(redactor == null ? text : redactor.Apply(text));
                outcome.Files.Add(entry.Name);
            }
            foreach (var entry in binaries)
            {
                try
                {
                    using var source = Open(entry.Source!);
                    using var target = zip.CreateEntry(entry.Name, CompressionLevel.Optimal).Open();
                    source.CopyTo(target);
                    outcome.Files.Add(entry.Name);
                }
                catch (IOException e)
                {
                    outcome.Notes.Add(entry.Name + ": couldn't be read (" + e.Message + ")");
                }
            }
        }
        if (File.Exists(zipPath))
            File.Delete(zipPath);
        File.Move(temp, zipPath);
        outcome.Bytes = new FileInfo(zipPath).Length;
        return outcome;
    }

    private static string? ReadText(ReportEntry entry, List<string> notes)
    {
        if (entry.Source == null || !File.Exists(entry.Source))
            return null;
        try
        {
            using var stream = Open(entry.Source);
            long skip = entry.MaxBytes > 0 && stream.Length > entry.MaxBytes ? stream.Length - entry.MaxBytes : 0;
            stream.Seek(skip, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string text = reader.ReadToEnd();
            if (skip == 0)
                return text;
            notes.Add($"{entry.Name}: only the last {entry.MaxBytes / (1024 * 1024)} MB");
            // Start at a whole line; the first one is most likely cut.
            int line = text.IndexOf('\n');
            return $"(The first {skip / 1024} KB are left out.)\n" + (line >= 0 ? text.Substring(line + 1) : text);
        }
        catch (IOException e)
        {
            notes.Add(entry.Name + ": couldn't be read (" + e.Message + ")");
            return null;
        }
    }

    // Logs are open in the game while it runs; sharing everything lets them be read anyway.
    private static FileStream Open(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
}
