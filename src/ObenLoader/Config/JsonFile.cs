using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace ObenLoader.Config;

/// <summary>Small helpers for reading and writing JSON files with the game's Newtonsoft.Json.</summary>
public static class JsonFile
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ObjectCreationHandling = ObjectCreationHandling.Replace,
    };

    // Another copy of the game running from the same folder can be writing a file right then.
    private const int Attempts = 10;
    private const int WaitMs = 25;

    private static readonly int ProcessId = Process.GetCurrentProcess().Id;

    public static T Read<T>(string path) where T : new()
    {
        string text = ReadText(path);
        if (text == null)
            return new T();
        return JsonConvert.DeserializeObject<T>(text, Settings) ?? new T();
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
