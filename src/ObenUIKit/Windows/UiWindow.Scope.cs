using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenUIKit.Windows;

/// <summary>The window as a lifetime scope: things it owns, errors in its callbacks, state kept across rebuilds.</summary>
public sealed partial class UiWindow
{
    private readonly List<IDisposable> _scope = new List<IDisposable>();
    private readonly Dictionary<string, int> _remembered = new Dictionary<string, int>();
    private int _buildSerial;

    /// <summary>
    /// Disposes <paramref name="disposable"/> when the window closes (or right away if it already has).
    /// The window is an <see cref="ITracker"/>, so event subscriptions, hotkeys and HUD pieces can use it
    /// as their owner and go away with it.
    /// </summary>
    public T Track<T>(T disposable) where T : IDisposable
    {
        if (disposable == null)
            return disposable;
        if (IsOpen)
            _scope.Add(disposable);
        else
            DisposeOne(disposable);
        return disposable;
    }

    /// <summary>
    /// Runs a mod callback for this window. An exception is logged with the window's id; with
    /// <see cref="WindowOptions.CloseOnError"/> the window closes as well.
    /// </summary>
    public void Run(string what, Action action)
    {
        if (action == null)
            return;
        try { action(); }
        catch (Exception e) { Fault($"'{what}' in window '{Id}' failed", e); }
    }

    /// <summary>Logs a failure and closes the window when it was opened with <see cref="WindowOptions.CloseOnError"/>.</summary>
    internal void Fault(string message, Exception e)
    {
        _kit.Log.Error(message, e);
        if (_options.CloseOnError && IsOpen)
        {
            _kit.Log.Warn($"Window '{Id}' closed after the error above");
            Close(WindowCloseReason.Error);
        }
    }

    /// <summary>
    /// A small number kept across <see cref="Rebuild"/> (the tab picked in a tab strip, the scroll of a list).
    /// Keys are made from the order controls are built in, so they stay stable as long as the build does.
    /// </summary>
    internal int Remembered(string key, int fallback) => _remembered.TryGetValue(key, out int value) ? value : fallback;

    internal void Remember(string key, int value) => _remembered[key] = value;

    /// <summary>A key for the next control that wants to remember something, unique within one build.</summary>
    internal string NextKey(string kind) => kind + "#" + ++_buildSerial;

    private void BeginBuildPass() => _buildSerial = 0;

    private void DisposeScope()
    {
        for (int i = _scope.Count - 1; i >= 0; i--)
            DisposeOne(_scope[i]);
        _scope.Clear();
    }

    private void DisposeOne(IDisposable disposable)
    {
        try { disposable.Dispose(); }
        catch (Exception e) { _kit.Log.Error($"Cleaning up after window '{Id}' failed", e); }
    }
}
