using System;

namespace ObenLoader.Runtime;

/// <summary>Turns a cleanup action into an <see cref="IDisposable"/> that runs at most once.</summary>
public sealed class Disposable : IDisposable
{
    private Action _onDispose;

    private Disposable(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public static IDisposable Create(Action onDispose) => new Disposable(onDispose);

    /// <summary>Destroys a Unity object (GameObject, component, texture...) on dispose.</summary>
    public static IDisposable Destroying(UnityEngine.Object obj) => new Disposable(() =>
    {
        if (obj != null)
            UnityEngine.Object.Destroy(obj);
    });

    public void Dispose()
    {
        var action = _onDispose;
        _onDispose = null;
        action?.Invoke();
    }
}
