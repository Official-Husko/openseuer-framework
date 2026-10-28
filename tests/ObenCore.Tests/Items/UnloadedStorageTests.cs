using ObenFramework.Items;

namespace ObenCore.Tests.Items;

public class UnloadedStorageTests
{
    // Shaped like a level .tnmt: a container with three kinds of slot, and an entry that isn't a container.
    private const string Save = "{\"SaveName\":\"Slot1\",\"Data\":["
        + "{\"GUID\":\"g1\",\"Data\":{\"$type\":\"Storage\",\"storageSlots\":["
        + "{\"$id\":\"1\",\"itemId\":5,\"itemAmount\":3,\"ownerId\":-1},"
        + "{\"$id\":\"2\",\"itemId\":-1,\"itemAmount\":0,\"ownerId\":-1},"
        + "null,"
        + "{\"$id\":\"3\",\"itemId\":7,\"itemAmount\":0,\"ownerId\":-1},"
        + "{\"$id\":\"4\",\"itemId\":9,\"itemAmount\":2.0,\"ownerId\":44}]}},"
        + "{\"GUID\":\"g2\",\"Data\":{\"$type\":\"Other\",\"x\":1}}]}";

    private static UnloadedStorageEntry Entry(string mod, int itemId, int count, bool give, string scene = "Bazaar") =>
        new UnloadedStorageEntry { Mod = mod, Scene = scene, Guid = "g1", ItemId = itemId, Count = count, Give = give };

    [Fact]
    public void MergesChangesInARowToTheSameContainerAndItem()
    {
        var list = new List<UnloadedStorageEntry>();
        var settled = new HashSet<UnloadedStorageEntry>();
        Assert.True(UnloadedStorageRules.Append(list, Entry("a", 5, 2, false), settled));
        Assert.True(UnloadedStorageRules.Append(list, Entry("a", 5, 3, false), settled));
        Assert.Single(list);
        Assert.Equal(5, list[0].Count);

        Assert.True(UnloadedStorageRules.Append(list, Entry("a", 5, 1, true), settled));
        Assert.Equal(2, list.Count); // a give doesn't merge with a take
        Assert.True(UnloadedStorageRules.Append(list, Entry("a", 6, 1, true), settled));
        Assert.Equal(3, list.Count); // another item
    }

    [Fact]
    public void NeverMergesIntoAnAppliedChangeOrPastTheCountLimit()
    {
        var list = new List<UnloadedStorageEntry>();
        var settled = new HashSet<UnloadedStorageEntry>();
        UnloadedStorageRules.Append(list, Entry("a", 6, 1, true), settled);
        settled.Add(list[0]);
        UnloadedStorageRules.Append(list, Entry("a", 6, 1, true), settled);
        Assert.Equal(2, list.Count);

        list[1].Count = UnloadedStorageRules.MaxCount;
        UnloadedStorageRules.Append(list, Entry("a", 6, 1, true), settled);
        Assert.Equal(3, list.Count);
    }

    [Fact]
    public void RefusesChangesPastAModsLimit()
    {
        var list = new List<UnloadedStorageEntry>();
        var settled = new HashSet<UnloadedStorageEntry>();
        for (int i = 0; i < UnloadedStorageRules.MaxEntriesPerMod; i++)
            list.Add(Entry("b", 10 + i, 1, false));
        Assert.False(UnloadedStorageRules.Append(list, Entry("b", 9, 1, false), settled));
        Assert.True(UnloadedStorageRules.Append(list, Entry("c", 9, 1, false), settled));
    }

    [Fact]
    public void DecidesTheOutcomeOfAnApply()
    {
        Assert.Equal(UnloadedApply.Gone, UnloadedStorageRules.Decide(false, false));
        Assert.Equal(UnloadedApply.Gone, UnloadedStorageRules.Decide(false, true));
        Assert.Equal(UnloadedApply.Applied, UnloadedStorageRules.Decide(true, true));
        Assert.Equal(UnloadedApply.Refused, UnloadedStorageRules.Decide(true, false));
    }

    [Fact]
    public void OnlyAnAppliedChangeOfTheSavedSceneLeavesTheQueue()
    {
        var entry = Entry("a", 5, 1, false);
        Assert.True(UnloadedStorageRules.Durable(entry, true, "Bazaar"));
        Assert.False(UnloadedStorageRules.Durable(entry, false, "Bazaar"));
        entry.Scene = "Other";
        Assert.False(UnloadedStorageRules.Durable(entry, true, "Bazaar"));
    }

    [Fact]
    public void WaitsTheRetryWindowForAMissingContainer()
    {
        Assert.False(UnloadedStorageRules.Expired(0f, UnloadedStorageRules.RetrySeconds - 1));
        Assert.True(UnloadedStorageRules.Expired(0f, UnloadedStorageRules.RetrySeconds));
        Assert.False(UnloadedStorageRules.Expired(5f, 14f));
    }

    [Theory]
    [InlineData("Interior Player Tenement", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("../Globals", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    public void ChecksSceneNames(string? scene, bool valid)
    {
        Assert.Equal(valid, UnloadedStorageRules.ValidScene(scene));
    }

    [Fact]
    public void CleanDropsInvalidEntries()
    {
        var file = new UnloadedStorageFile();
        file.Entries.Add(Entry("a", 5, 1, false));
        file.Entries.Add(Entry("a", 1, 1, false));                                   // money
        file.Entries.Add(Entry("a", 5, 0, false));                                   // nothing
        file.Entries.Add(Entry("a", 5, UnloadedStorageRules.MaxCount + 1, false));   // too many
        file.Entries.Add(Entry("", 5, 1, false));                                    // no mod
        file.Entries.Add(null);
        Assert.Single(UnloadedStorageRules.Clean(file).Entries);
        Assert.Empty(UnloadedStorageRules.Clean(null).Entries);
    }

    [Fact]
    public void ReadsTheStacksOfAContainer()
    {
        var container = UnloadedStorageReader.Parse(Save, "g1", out string? refusal);
        Assert.Null(refusal);
        Assert.NotNull(container);
        Assert.Equal(2, container!.Stacks.Count);
        Assert.Equal((0, 5, 3, -1), (container.Stacks[0].Slot, container.Stacks[0].ItemId, container.Stacks[0].Amount, container.Stacks[0].OwnerId));
        Assert.Equal((4, 9, 2, 44), (container.Stacks[1].Slot, container.Stacks[1].ItemId, container.Stacks[1].Amount, container.Stacks[1].OwnerId));
        Assert.Equal(3, container.Amount(5));
        Assert.Equal(0, container.Amount(7));
    }

    [Theory]
    [InlineData("g2")]   // not a container
    [InlineData("zz")]   // unknown
    public void RefusesIdsThatArentOneContainer(string guid)
    {
        Assert.Null(UnloadedStorageReader.Parse(Save, guid, out string? refusal));
        Assert.NotNull(refusal);
    }

    [Fact]
    public void RefusesAnAmbiguousIdAReferenceSlotAndBrokenJson()
    {
        string twice = Save.Replace("\"GUID\":\"g2\"", "\"GUID\":\"g1\"").Replace("\"$type\":\"Other\",\"x\":1", "\"$type\":\"Storage\",\"storageSlots\":[]");
        Assert.Null(UnloadedStorageReader.Parse(twice, "g1", out string? refusal));
        Assert.Contains("more than one", refusal);

        string reference = Save.Replace("{\"$id\":\"2\",\"itemId\":-1,\"itemAmount\":0,\"ownerId\":-1}", "{\"$ref\":\"1\"}");
        Assert.Null(UnloadedStorageReader.Parse(reference, "g1", out refusal));
        Assert.Contains("reference", refusal);

        Assert.Null(UnloadedStorageReader.Parse("{not json", "g1", out refusal));
        Assert.NotNull(refusal);
        Assert.Null(UnloadedStorageReader.Parse("{\"SaveName\":\"x\"}", "g1", out refusal));
        Assert.Contains("Data", refusal);
    }

    [Fact]
    public void ListsTheContainerIdsOnce()
    {
        string twice = Save.Replace("\"GUID\":\"g2\"", "\"GUID\":\"g1\"").Replace("\"$type\":\"Other\",\"x\":1", "\"$type\":\"Storage\",\"storageSlots\":[]");
        Assert.Equal(new[] { "g1" }, UnloadedStorageReader.ParseGuids(Save, out _));
        Assert.Equal(new[] { "g1" }, UnloadedStorageReader.ParseGuids(twice, out _));
        Assert.Empty(UnloadedStorageReader.ParseGuids("[]", out string? refusal));
        Assert.NotNull(refusal);
    }

    [Fact]
    public void ReadsFromAFileAndExplainsAMissingOne()
    {
        string dir = Path.Combine(Path.GetTempPath(), "oben-unloaded-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "Bazaar.tnmt");
            File.WriteAllText(path, Save, new System.Text.UTF8Encoding(true));
            Assert.Equal(3, UnloadedStorageReader.Read(path, "g1", out _)!.Amount(5));
            Assert.Null(UnloadedStorageReader.Read(Path.Combine(dir, "Nowhere.tnmt"), "g1", out string? refusal));
            Assert.Contains("no file", refusal);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
