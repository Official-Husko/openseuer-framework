using ObenFramework.Events;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenCore.Tests;

public class GameEventTests
{
    private readonly GameEvent<string> _event = new("Test");
    private readonly ModLogger _log = new();

    private sealed class Owner : ITracker
    {
        public List<IDisposable> Tracked { get; } = new();

        public T Track<T>(T disposable) where T : IDisposable
        {
            Tracked.Add(disposable);
            return disposable;
        }

        public void DisposeAll()
        {
            foreach (var d in Tracked)
                d.Dispose();
        }
    }

    [Fact]
    public void HandlersWithAndWithoutThePayloadBothRun()
    {
        var calls = new List<string>();
        _event.Subscribe(folder => calls.Add("with " + folder));
        _event.Subscribe(() => calls.Add("without"));

        _event.Raise("Slot1", _log);

        Assert.Equal(new[] { "with Slot1", "without" }, calls);
    }

    [Fact]
    public void APayloadFreeHandlerEndsWithItsOwnerOrItsSubscription()
    {
        var owner = new Owner();
        int owned = 0, disposed = 0;
        _event.Subscribe(owner, () => owned++);
        var sub = _event.Subscribe(() => disposed++);

        _event.Raise("a", _log);
        owner.DisposeAll();
        sub.Dispose();
        _event.Raise("b", _log);

        Assert.Equal(1, owned);
        Assert.Equal(1, disposed);
    }

    [Fact]
    public void ANullPayloadFreeHandlerIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => _event.Subscribe((Action)null!));
    }

    [Fact]
    public void AnErrorInAPayloadFreeHandlerNamesWhereTheHandlerCameFrom()
    {
        // GC.EndNoGCRegion throws outside a no-GC region, and it lives in the runtime's own assembly,
        // so the message shows whether the wrapper or the real handler was blamed.
        _event.Subscribe(GC.EndNoGCRegion);

        _event.Raise("a", _log);

        var error = Assert.Single(_log.Errors);
        Assert.Contains("from System.Private.CoreLib", error);
    }
}
