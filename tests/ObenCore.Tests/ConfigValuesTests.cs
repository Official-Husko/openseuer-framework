using ObenLoader.Config;

namespace ObenCore.Tests;

public class ConfigValuesTests
{
    public class Sample
    {
        public float Speed = 1f;
        public bool Enabled;
        public string Name = "default";
        public List<int> Ids = new List<int> { 1, 2 };
        public int Hidden = 7;
        public string Label { get; set; } = "label";
    }

    [Fact]
    public void NoChangesGiveNull()
    {
        var a = ConfigValues.Snapshot(new Sample());
        var b = ConfigValues.Snapshot(new Sample());
        Assert.Null(ConfigValues.Changes(a, b));
    }

    [Fact]
    public void ListsEachChangedValue()
    {
        var sample = new Sample();
        var before = ConfigValues.Snapshot(sample);
        sample.Speed = 2.5f;
        sample.Enabled = true;
        sample.Name = "new";
        Assert.Equal("Speed 1.0 -> 2.5, Enabled false -> true, Name \"default\" -> \"new\"",
            ConfigValues.Changes(before, ConfigValues.Snapshot(sample)));
    }

    [Fact]
    public void ANullStringReadsAsNone()
    {
        var sample = new Sample();
        var before = ConfigValues.Snapshot(sample);
        sample.Name = null;
        Assert.Equal("Name \"default\" -> (none)", ConfigValues.Changes(before, ConfigValues.Snapshot(sample)));
    }

    [Fact]
    public void LongValuesAreCut()
    {
        var sample = new Sample();
        var before = ConfigValues.Snapshot(sample);
        sample.Ids = Enumerable.Range(0, 100).ToList();
        string changes = ConfigValues.Changes(before, ConfigValues.Snapshot(sample));
        Assert.StartsWith("Ids [1,2] -> [0,1,2,", changes);
        Assert.EndsWith("...", changes);
        Assert.True(changes.Length < 100);
    }

    [Fact]
    public void CopiesFieldsAndSettableProperties()
    {
        var target = new Sample { Speed = 9f, Enabled = true, Name = "x", Ids = new List<int>(), Hidden = 1, Label = "y" };
        ConfigValues.CopyInto(new Sample(), target);
        Assert.Equal(1f, target.Speed);
        Assert.False(target.Enabled);
        Assert.Equal("default", target.Name);
        Assert.Equal(new[] { 1, 2 }, target.Ids);
        Assert.Equal(7, target.Hidden);
        Assert.Equal("label", target.Label);
    }
}
