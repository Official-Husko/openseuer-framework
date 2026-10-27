using System;

namespace ObenLoader.Logging;

/// <summary>Named logger. Each mod gets one tagged with its id.</summary>
public sealed class ModLogger
{
    public string Source { get; }

    public ModLogger(string source)
    {
        Source = source;
    }

    public void Debug(string message) => LogHub.Write(LogLevel.Debug, Source, message);
    public void Info(string message) => LogHub.Write(LogLevel.Info, Source, message);
    public void Warn(string message) => LogHub.Write(LogLevel.Warning, Source, message);
    public void Error(string message) => LogHub.Write(LogLevel.Error, Source, message);
    public void Error(string message, Exception e) => LogHub.Write(LogLevel.Error, Source, message + "\n" + e);
}
