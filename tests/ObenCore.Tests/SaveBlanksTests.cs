using Newtonsoft.Json;
using ObenFramework.Saves;
using ObenLoader.Config;

namespace ObenCore.Tests;

public class SaveBlanksTests
{
    public sealed class Part
    {
        public int Count;
        public List<int> Numbers;
    }

    public sealed class Data
    {
        public int Level = 3;
        public string Name;
        public Part Inner;
        public Part Kept = new Part { Count = 9 };
        public Part[] Items;
        public List<Part> Rows;
        public Dictionary<string, Part> ByName;
        public int[,] Grid;
        public HashSet<string> Tags { get; set; }
        public List<string> Fixed { get; } = new List<string>();
        public IList<int> Abstract;
        public object Anything;
        public Uri Address;
        [JsonIgnore] public Part Ignored;
    }

    public sealed class Link
    {
        public Link Next;
        public List<Link> Children;
    }

    public sealed class Ring
    {
        public RingPart Part;
    }

    public sealed class RingPart
    {
        public Ring Back;
    }

    public sealed class Holder
    {
        public Ring Ring;
        public Link Link;
    }

    private static Data Read(string json)
    {
        var data = JsonConvert.DeserializeObject<Data>(json, JsonFile.Settings);
        SaveBlanks.Fill(data);
        return data;
    }

    [Fact]
    public void NullClassesListsArraysAndDictionariesComeBackEmpty()
    {
        var data = Read("{}");
        Assert.NotNull(data.Inner);
        Assert.Equal(0, data.Inner.Count);
        Assert.Empty(data.Items);
        Assert.Empty(data.Rows);
        Assert.Empty(data.ByName);
        Assert.Empty(data.Tags);
        Assert.Equal(2, data.Grid.Rank);
        Assert.Empty(data.Grid);
    }

    [Fact]
    public void FillsAllTheWayDown()
    {
        var data = Read("{\"Inner\":{\"Count\":4}}");
        Assert.Equal(4, data.Inner.Count);
        Assert.Empty(data.Inner.Numbers);
        Assert.Empty(data.Kept.Numbers);
    }

    [Fact]
    public void ListItemsAndDictionaryValuesAreFilledButNullItemsStayNull()
    {
        var data = Read("{\"Items\":[{\"Count\":1},null],\"Rows\":[{\"Count\":2}],\"ByName\":{\"a\":{\"Count\":3},\"b\":null}}");
        Assert.Empty(data.Items[0].Numbers);
        Assert.Null(data.Items[1]);
        Assert.Empty(data.Rows[0].Numbers);
        Assert.Empty(data.ByName["a"].Numbers);
        Assert.Null(data.ByName["b"]);
    }

    [Fact]
    public void InitializersAndValuesInTheFileAreKept()
    {
        var data = Read("{\"Level\":5,\"Kept\":null,\"Rows\":[{\"Count\":7}]}");
        Assert.Equal(5, data.Level);
        // An explicit null in the file doesn't overwrite an initializer.
        Assert.Equal(9, data.Kept.Count);
        Assert.Single(data.Rows);
        Assert.Equal(7, data.Rows[0].Count);
    }

    [Fact]
    public void AFieldMissingFromTheFileKeepsItsInitialValue()
    {
        Assert.Equal(3, Read("{\"Name\":\"old\"}").Level);
    }

    [Fact]
    public void StringsInterfacesObjectsAndIgnoredMembersStayNull()
    {
        var data = Read("{}");
        Assert.Null(data.Name);
        Assert.Null(data.Abstract);
        Assert.Null(data.Anything);
        Assert.Null(data.Address);
        Assert.Null(data.Ignored);
    }

    [Fact]
    public void GetterOnlyMembersAreLeftButStillWalked()
    {
        var data = new Data();
        SaveBlanks.Fill(data);
        Assert.Empty(data.Fixed);
    }

    [Fact]
    public void AClassThatHoldsItsOwnTypeIsNotFilled()
    {
        var link = new Link();
        SaveBlanks.Fill(link);
        Assert.Null(link.Next);
        // An empty list ends the chain, so a list of itself is fine.
        Assert.Empty(link.Children);
        Assert.False(SaveBlanks.CanFill(typeof(Link)));
    }

    [Fact]
    public void AClassThatReachesItselfThroughAnotherIsNotFilled()
    {
        var holder = new Holder();
        SaveBlanks.Fill(holder);
        Assert.Null(holder.Ring);
        Assert.Null(holder.Link);
        Assert.False(SaveBlanks.CanFill(typeof(Ring)));
        Assert.False(SaveBlanks.CanFill(typeof(RingPart)));
    }

    [Fact]
    public void AGraphWithACycleEnds()
    {
        var a = new Link();
        var b = new Link { Next = a };
        a.Next = b;
        SaveBlanks.Fill(a);
        Assert.Empty(a.Children);
        Assert.Empty(b.Children);
    }

    [Fact]
    public void WhatNeedsNoFillingRoundTripsUnchanged()
    {
        var data = new Data { Name = "2026-10-12", Inner = new Part { Count = 4, Numbers = new List<int> { 1, 2 } } };
        SaveBlanks.Fill(data);
        string json = JsonConvert.SerializeObject(data, JsonFile.Settings);
        var back = Read(json);
        Assert.Equal("2026-10-12", back.Name);
        Assert.Equal(new List<int> { 1, 2 }, back.Inner.Numbers);
        Assert.Equal(json, JsonConvert.SerializeObject(back, JsonFile.Settings));
    }

    [Fact]
    public void NullIsFine() => SaveBlanks.Fill(null);
}
