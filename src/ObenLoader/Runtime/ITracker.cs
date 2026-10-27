using System;

namespace ObenLoader.Runtime;

/// <summary>
/// Something that owns cleanup: things passed to <see cref="Track{T}"/> are disposed when the
/// owner goes away. Mods implement it (disposed on unload), and so do smaller units such as
/// framework features (disposed when switched off).
/// </summary>
public interface ITracker
{
    T Track<T>(T disposable) where T : IDisposable;
}
