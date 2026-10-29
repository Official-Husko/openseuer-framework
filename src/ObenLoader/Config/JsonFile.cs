using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ObenLoader.Config;

/// <summary>Small helpers for reading and writing JSON files with the game's Newtonsoft.Json.</summary>
public static class JsonFile
{
    /// <summary>
    /// Indented, without nulls, Unity's small structs as their fields (<see cref="UnityStructJson"/>) and enums by
    /// name ("F7", "Lamp, Fridge"), so a file reads well when edited by hand. Numbers from older files still read.
    /// </summary>
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = { new UnityStructJson(), new StringEnumConverter() },
    };

    // Another copy of the game running from the same folder can be writing a file right then.
    private const int Attempts = 10;
    private const int WaitMs = 25;

    // A file with more bad values than this is taken as broken as a whole, not skipped value by value.
    private const int MaxSkipped = 20;

    private static readonly int ProcessId = Process.GetCurrentProcess().Id;

    public static T Read<T>(string path) where T : new()
    {
        string text = ReadText(path);
        if (text == null)
            return new T();
        return JsonConvert.DeserializeObject<T>(text, Settings) ?? new T();
    }

    /// <summary>
    /// Reads the file like <see cref="Read{T}(string)"/>, except that a value that doesn't fit its field (a
    /// misspelt choice, text where a number belongs) leaves that field at the default <typeparamref name="T"/>
    /// gives it, instead of failing the whole file. Each one is added to <paramref name="skipped"/>, path and
    /// reason. A file that isn't JSON, or has more than a few bad values, still throws.
    /// </summary>
    public static T Read<T>(string path, List<string> skipped) where T : new()
    {
        string text = ReadText(path);
        if (text == null)
            return new T();
        var settings = new JsonSerializerSettings
        {
            Formatting = Settings.Formatting,
            NullValueHandling = Settings.NullValueHandling,
            ObjectCreationHandling = Settings.ObjectCreationHandling,
            Converters = Settings.Converters,
            Error = (sender, e) =>
            {
                // Handled where it happened (the error is passed up through every enclosing object), for a value
                // inside an object or list; the reader carries on with the next one.
                if (e.CurrentObject != e.ErrorContext.OriginalObject || e.ErrorContext.Member == null || skipped.Count >= MaxSkipped)
                    return;
                skipped.Add($"{e.ErrorContext.Path}: {e.ErrorContext.Error.Message}");
                e.ErrorContext.Handled = true;
            },
        };
        return JsonConvert.DeserializeObject<T>(text, settings) ?? new T();
    }

    public static bool TryRead<T>(string path, out T value, out Exception error)
    {
        try
        {
            string text = ReadText(path) ?? throw new FileNotFoundException("No such file.", path);
            value = JsonConvert.DeserializeObject<T>(text, Settings);
            error = null;
            return value != null;
        }
        catch (Exception e)
        {
            value = default;
            error = e;
            return false;
        }
    }

    /// <summary>
    /// Writes through a temp file so a crash mid-write can't leave a half-written file, and swaps it
    /// in with one replace, so nothing reading the file at that moment finds it missing. Each process
    /// has its own temp file.
    /// </summary>
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + "." + ProcessId + ".tmp";
        File.WriteAllText(tmp, JsonConvert.SerializeObject(value, Settings), new UTF8Encoding(false));
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
                return;
            }
            catch (IOException) when (attempt < Attempts)
            {
                Thread.Sleep(WaitMs);
            }
            catch
            {
                File.Delete(tmp);
                throw;
            }
        }
    }

    // The file's text, or null when there is none. A file being replaced by another process can't be
    // opened for a moment; that is waited out instead of read as missing.
    private static string ReadText(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException) when (attempt < Attempts)
            {
                Thread.Sleep(WaitMs);
            }
        }
    }
}
