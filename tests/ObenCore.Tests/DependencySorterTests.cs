using ObenLoader.Mods;

namespace ObenCore.Tests;

// The sorter looks up already-running mods through the stub registry, which is shared static
// state, so everything touching it lives in this one class.
public class DependencySorterTests : IDisposable
{
    public DependencySorterTests() => ModRegistry.Running.Clear();

    public void Dispose() => ModRegistry.Running.Clear();

    private static ModInfo Mod(string id, string version = "1.0.0", string[] loadAfter = null,
        params (string id, string min)[] dependencies)
    {
        var manifest = new ModManifest { Id = id, Version = version };
        foreach (var (depId, min) in dependencies)
            manifest.Dependencies[depId] = min;
        if (loadAfter != null)
            manifest.LoadAfter.AddRange(loadAfter);
        return new ModInfo(manifest, "/mods/" + id);
    }

    private static ModInfo Needs(string id, params string[] dependencies) =>
        Mod(id, dependencies: dependencies.Select(d => (d, "*")).ToArray());

    private static string[] Ids(IEnumerable<ModInfo> mods) => mods.Select(m => m.Id).ToArray();

    [Fact]
    public void IndependentModsComeOutAlphabetically()
    {
        var sorted = DependencySorter.Sort(new[] { Mod("c"), Mod("a"), Mod("b") });
        Assert.Equal(new[] { "a", "b", "c" }, Ids(sorted));
    }

    [Fact]
    public void DependenciesComeFirst()
    {
        var sorted = DependencySorter.Sort(new[] { Needs("a", "z"), Mod("z") });
        Assert.Equal(new[] { "z", "a" }, Ids(sorted));
    }

    [Fact]
    public void ChainsAreOrderedEndToEnd()
    {
        var sorted = DependencySorter.Sort(new[] { Needs("a", "b"), Needs("b", "c"), Needs("c", "d"), Mod("d") });
        Assert.Equal(new[] { "d", "c", "b", "a" }, Ids(sorted));
    }

    [Fact]
    public void SharedDependencyIsListedOnce()
    {
        var sorted = DependencySorter.Sort(new[] { Needs("a", "core"), Needs("b", "core"), Mod("core") });
        Assert.Equal(new[] { "core", "a", "b" }, Ids(sorted));
    }

    [Fact]
    public void NothingIsMarkedFailedWhenAllIsFine()
    {
        var sorted = DependencySorter.Sort(new[] { Needs("a", "b"), Mod("b") });
        Assert.All(sorted, m => Assert.NotEqual(ModState.Failed, m.State));
    }

    [Fact]
    public void MissingDependencyFailsTheMod()
    {
        var a = Needs("a", "ghost");
        var b = Mod("b");
        var sorted = DependencySorter.Sort(new[] { a, b });

        Assert.Equal(new[] { "b" }, Ids(sorted));
        Assert.Equal(ModState.Failed, a.State);
        Assert.Contains("ghost", a.Error);
        Assert.Contains("isn't installed", a.Error);
    }

    [Fact]
    public void FailureSpreadsToEverythingThatNeedsTheFailedMod()
    {
        var a = Needs("a", "b");
        var b = Needs("b", "c");
        var c = Needs("c", "ghost");
        var other = Mod("other");

        var sorted = DependencySorter.Sort(new[] { a, b, c, other });

        Assert.Equal(new[] { "other" }, Ids(sorted));
        Assert.Equal(ModState.Failed, a.State);
        Assert.Equal(ModState.Failed, b.State);
        Assert.Contains("failed to load", b.Error);
    }

    [Fact]
    public void ModThatAlreadyFailedIsLeftOutAndTakesDependentsDown()
    {
        var broken = Mod("broken");
        broken.Fail("bad dll");
        var user = Needs("user", "broken");

        var sorted = DependencySorter.Sort(new[] { broken, user });

        Assert.Empty(sorted);
        Assert.Equal("bad dll", broken.Error);
        Assert.Equal(ModState.Failed, user.State);
        Assert.Contains("'broken'", user.Error);
    }

    [Fact]
    public void TooOldDependencyFailsTheMod()
    {
        var a = Mod("a", dependencies: ("lib", "2.0.0"));
        var lib = Mod("lib", "1.5.0");

        var sorted = DependencySorter.Sort(new[] { a, lib });

        Assert.Equal(new[] { "lib" }, Ids(sorted));
        Assert.Equal(ModState.Failed, a.State);
        Assert.Contains("2.0.0 or newer", a.Error);
        Assert.Contains("1.5.0", a.Error);
    }

    [Fact]
    public void DependencyAtExactlyTheMinimumVersionIsAccepted()
    {
        var a = Mod("a", dependencies: ("lib", "1.5.0"));
        var sorted = DependencySorter.Sort(new[] { a, Mod("lib", "1.5.0") });
        Assert.Equal(new[] { "lib", "a" }, Ids(sorted));
    }

    [Fact]
    public void RunningModOutsideTheBatchSatisfiesADependency()
    {
        var framework = Mod("framework", "1.2.0");
        framework.State = ModState.Loaded;
        ModRegistry.Running["framework"] = framework;

        var a = Mod("a", dependencies: ("framework", "1.0.0"));
        var sorted = DependencySorter.Sort(new[] { a });

        Assert.Equal(new[] { "a" }, Ids(sorted));
    }

    [Fact]
    public void RunningModOutsideTheBatchStillNeedsTheRightVersion()
    {
        var framework = Mod("framework", "0.9.0");
        framework.State = ModState.Loaded;
        ModRegistry.Running["framework"] = framework;

        var a = Mod("a", dependencies: ("framework", "1.0.0"));
        var sorted = DependencySorter.Sort(new[] { a });

        Assert.Empty(sorted);
        Assert.Contains("1.0.0 or newer", a.Error);
    }

    [Theory]
    [InlineData(ModState.Disabled)]
    [InlineData(ModState.Pending)]
    [InlineData(ModState.Failed)]
    public void KnownButNotRunningModDoesNotCount(ModState state)
    {
        var off = Mod("off");
        off.State = state;
        ModRegistry.Running["off"] = off;

        var a = Needs("a", "off");
        var sorted = DependencySorter.Sort(new[] { a });

        Assert.Empty(sorted);
        Assert.Equal(ModState.Failed, a.State);
    }

    [Fact]
    public void TwoModCycleFailsBoth()
    {
        var a = Needs("a", "b");
        var b = Needs("b", "a");
        var c = Mod("c");

        var sorted = DependencySorter.Sort(new[] { a, b, c });

        Assert.Equal(new[] { "c" }, Ids(sorted));
        Assert.Equal(ModState.Failed, a.State);
        Assert.Equal(ModState.Failed, b.State);
        Assert.Equal("Circular dependency.", a.Error);
        Assert.Contains("failed to load", b.Error);
    }

    [Fact]
    public void SelfDependencyIsACycle()
    {
        var a = Needs("a", "a");
        var sorted = DependencySorter.Sort(new[] { a });
        Assert.Empty(sorted);
        Assert.Equal(ModState.Failed, a.State);
    }

    [Fact]
    public void ModDependingOnACycleFailsToo()
    {
        var a = Needs("a", "b");
        var b = Needs("b", "c");
        var c = Needs("c", "a");
        var d = Needs("d", "a");
        var free = Mod("free");

        var sorted = DependencySorter.Sort(new[] { a, b, c, d, free });

        Assert.Equal(new[] { "free" }, Ids(sorted));
        Assert.All(new[] { a, b, c, d }, m => Assert.Equal(ModState.Failed, m.State));
    }

    [Fact]
    public void LoadAfterPutsTheHintedModFirst()
    {
        var sorted = DependencySorter.Sort(new[] { Mod("a", loadAfter: new[] { "z" }), Mod("z") });
        Assert.Equal(new[] { "z", "a" }, Ids(sorted));
    }

    [Fact]
    public void LoadAfterForAnAbsentModIsIgnored()
    {
        var a = Mod("a", loadAfter: new[] { "ghost" });
        var sorted = DependencySorter.Sort(new[] { a });
        Assert.Equal(new[] { "a" }, Ids(sorted));
        Assert.NotEqual(ModState.Failed, a.State);
    }

    [Fact]
    public void LoadAfterLoopsAreBrokenWithoutFailing()
    {
        var a = Mod("a", loadAfter: new[] { "b" });
        var b = Mod("b", loadAfter: new[] { "a" });

        var sorted = DependencySorter.Sort(new[] { a, b });

        Assert.Equal(new[] { "a", "b" }, Ids(sorted));
        Assert.NotEqual(ModState.Failed, a.State);
        Assert.NotEqual(ModState.Failed, b.State);
    }

    [Fact]
    public void LongerLoadAfterLoopsAreBrokenWithoutFailing()
    {
        var a = Mod("a", loadAfter: new[] { "b" });
        var b = Mod("b", loadAfter: new[] { "c" });
        var c = Mod("c", loadAfter: new[] { "a" });

        var sorted = DependencySorter.Sort(new[] { a, b, c });

        Assert.Equal(3, sorted.Count);
        Assert.All(sorted, m => Assert.NotEqual(ModState.Failed, m.State));
    }

    [Fact]
    public void LoadAfterThatWouldCloseALoopThroughAHintedChainIsSkipped()
    {
        // a would like to follow b, b would like to follow c, but c really needs a.
        var a = Mod("a", loadAfter: new[] { "b" });
        var b = Mod("b", loadAfter: new[] { "c" });
        var c = Needs("c", "a");

        var sorted = DependencySorter.Sort(new[] { a, b, c });

        Assert.Equal(3, sorted.Count);
        Assert.True(Array.IndexOf(Ids(sorted), "a") < Array.IndexOf(Ids(sorted), "c"));
    }

    [Fact]
    public void LoadAfterAgainstARealDependencyKeepsTheDependencyOrder()
    {
        // b needs a, and a would like to come after b: the hard dependency wins.
        var a = Mod("a", loadAfter: new[] { "b" });
        var b = Needs("b", "a");

        var sorted = DependencySorter.Sort(new[] { a, b });

        Assert.Equal(new[] { "a", "b" }, Ids(sorted));
        Assert.All(sorted, m => Assert.NotEqual(ModState.Failed, m.State));
    }

    [Fact]
    public void LoadAfterDoesNotRequireTheHintedModToLoad()
    {
        var hinted = Needs("hinted", "ghost");
        var a = Mod("a", loadAfter: new[] { "hinted" });

        var sorted = DependencySorter.Sort(new[] { a, hinted });

        Assert.Equal(new[] { "a" }, Ids(sorted));
        Assert.NotEqual(ModState.Failed, a.State);
    }

    [Fact]
    public void EmptyBatchGivesEmptyResult()
    {
        Assert.Empty(DependencySorter.Sort(Array.Empty<ModInfo>()));
    }
}
