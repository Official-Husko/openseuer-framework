using ObenFramework.Env;
using ObenLoader.Runtime;

namespace ObenCore.Tests.Env;

public class OwnedValuesTests
{
    private sealed class Owner : ITracker
    {
        public List<IDisposable> Tracked { get; } = new();

        public T Track<T>(T disposable) where T : IDisposable
        {
            Tracked.Add(disposable);
            return disposable;
        }

        public void Unload()
        {
            foreach (var d in Tracked)
                d.Dispose();
            Tracked.Clear();
        }
    }

    private readonly OwnedValues<int> _values = new();

    [Fact]
    public void NewestValueWins()
    {
        var a = new Owner();
        var b = new Owner();
        _values.Set(a, "a", "fog", 1);
        _values.Set(b, "b", "fog", 2);
        Assert.True(_values.TryGet("fog", out int value));
        Assert.Equal(2, value);
    }

    [Fact]
    public void SettingAgainMakesTheOwnerNewest()
    {
        var a = new Owner();
        var b = new Owner();
        _values.Set(a, "a", "", 1);
        _values.Set(b, "b", "", 2);
        _values.Set(a, "a", "", 3);
        Assert.Equal(2, _values.Count);
        Assert.True(_values.TryGet("", out int value));
        Assert.Equal(3, value);
    }

    [Fact]
    public void RemovingTheNewestFallsBackToTheOlder()
    {
        var a = new Owner();
        var b = new Owner();
        _values.Set(a, "a", "k", 1);
        _values.Set(b, "b", "k", 2);
        Assert.True(_values.Remove(b, "k"));
        Assert.True(_values.TryGet("k", out int value));
        Assert.Equal(1, value);
        Assert.False(_values.Remove(b, "k"));
    }

    [Fact]
    public void KeysAreIndependent()
    {
        var a = new Owner();
        _values.Set(a, "a", "x", 1);
        _values.Set(a, "a", "y", 2);
        Assert.True(_values.TryGet("x", out int x));
        Assert.Equal(1, x);
        Assert.False(_values.TryGet("z", out _));
        Assert.Equal(new[] { "x", "y" }, _values.Keys());
    }

    [Fact]
    public void UnloadRemovesEverythingAndReportsTheKeys()
    {
        var released = new List<string>();
        _values.Released = released.Add;
        var a = new Owner();
        var b = new Owner();
        _values.Set(a, "a", "x", 1);
        _values.Set(a, "a", "y", 2);
        _values.Set(b, "b", "x", 3);

        a.Unload();

        Assert.Equal(1, _values.Count);
        Assert.Contains("x", released);
        Assert.Contains("y", released);
        Assert.True(_values.TryGet("x", out int x));
        Assert.Equal(3, x);
        Assert.False(_values.Has("y"));
    }

    [Fact]
    public void EachOwnerIsTrackedOnce()
    {
        var a = new Owner();
        _values.Set(a, "a", "x", 1);
        _values.Set(a, "a", "y", 2);
        _values.Set(a, "a", "x", 3);
        Assert.Single(a.Tracked);
    }

    [Fact]
    public void PickTakesEachPartFromTheNewestThatSetsIt()
    {
        var sky = new OwnedValues<(float? fog, float? sun)>();
        var a = new Owner();
        var b = new Owner();
        sky.Set(a, "a", "", (0.5f, 2f));
        sky.Set(b, "b", "", (0.1f, null));
        Assert.Equal(0.1f, sky.Pick<float>("", v => v.fog));
        Assert.Equal(2f, sky.Pick<float>("", v => v.sun));
        sky.Remove(a, "");
        Assert.Null(sky.Pick<float>("", v => v.sun));
    }

    [Fact]
    public void ClearDropsWithoutReporting()
    {
        var released = new List<string>();
        _values.Released = released.Add;
        var a = new Owner();
        _values.Set(a, "a", "x", 1);
        _values.Clear();
        a.Unload();
        Assert.True(_values.IsEmpty);
        Assert.Empty(released);
    }
}
