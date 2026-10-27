using ObenFramework.Events;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenCore.Tests;

public class HandlerListTests
{
    private readonly HandlerList<Action> _list = new();
    private readonly ModLogger _log = new();

    private void Raise() => _list.ForEach(h => h(), _log, "Test");

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
    public void RaisesHandlersInSubscriptionOrder()
    {
        var calls = new List<int>();
        _list.Add(() => calls.Add(1));
        _list.Add(() => calls.Add(2));
        _list.Add(() => calls.Add(3));
        Raise();
        Assert.Equal(new[] { 1, 2, 3 }, calls);
        Assert.Equal(3, _list.Count);
    }

    [Fact]
    public void RaisingWithoutHandlersIsFine()
    {
        Raise();
        Assert.Empty(_log.Errors);
    }

    [Fact]
    public void DisposingTheSubscriptionRemovesTheHandler()
    {
        int calls = 0;
        var sub = _list.Add(() => calls++);
        Raise();
        sub.Dispose();
        sub.Dispose();
        Raise();
        Assert.Equal(1, calls);
        Assert.Equal(0, _list.Count);
    }

    [Fact]
    public void RemoveTakesOutOneSubscriptionOfADelegate()
    {
        int calls = 0;
        Action handler = () => calls++;
        _list.Add(handler);
        _list.Add(handler);
        _list.Remove(handler);
        Raise();
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SubscriptionsTiedToAnOwnerEndWithTheOwner()
    {
        var owner = new Owner();
        int calls = 0;
        _list.Add(owner, () => calls++);
        Raise();
        owner.DisposeAll();
        Raise();
        Assert.Equal(1, calls);
        Assert.Single(owner.Tracked);
    }

    [Fact]
    public void UnsubscribingDuringARaiseStillRunsTheRestOfThatRaise()
    {
        var calls = new List<string>();
        IDisposable second = null;
        _list.Add(() =>
        {
            calls.Add("first");
            second.Dispose();
        });
        second = _list.Add(() => calls.Add("second"));

        Raise();
        Assert.Equal(new[] { "first", "second" }, calls);

        calls.Clear();
        Raise();
        Assert.Equal(new[] { "first" }, calls);
    }

    [Fact]
    public void SubscribingDuringARaiseTakesEffectNextRaise()
    {
        int lateCalls = 0;
        bool added = false;
        _list.Add(() =>
        {
            if (added)
                return;
            added = true;
            _list.Add(() => lateCalls++);
        });

        Raise();
        Assert.Equal(0, lateCalls);
        Raise();
        Assert.Equal(1, lateCalls);
    }

    [Fact]
    public void AThrowingHandlerIsLoggedAndDoesNotStopTheOthers()
    {
        var calls = new List<string>();
        _list.Add(() => calls.Add("before"));
        _list.Add(() => throw new InvalidOperationException("boom"));
        _list.Add(() => calls.Add("after"));

        Raise();

        Assert.Equal(new[] { "before", "after" }, calls);
        var error = Assert.Single(_log.Errors);
        Assert.Contains("Test handler", error);
        Assert.Contains("boom", error);
    }

    [Fact]
    public void ThrowingHandlersStayRegistered()
    {
        int attempts = 0;
        _list.Add(() =>
        {
            attempts++;
            throw new InvalidOperationException();
        });
        Raise();
        Raise();
        Assert.Equal(2, attempts);
        Assert.Equal(2, _log.Errors.Count);
    }
}
