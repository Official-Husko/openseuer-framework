using System.Text.RegularExpressions;
using ObenUIKit.Menu.Errors;

namespace ObenCore.Tests.Kit;

public class StackTextTests
{
    private static readonly StackColors Colors = new()
    {
        Error = "E", Caught = "C", Muted = "M", Text = "T", Method = "F", File = "L", Number = "N",
    };

    // The text a player sees, without the color, font and noparse tags.
    private static string Plain(string rich) => Regex.Replace(rich, "<noparse>(.)</noparse>|</?(color|font)(=[^>]*)?>", "$1");

    [Theory]
    [InlineData("S+<>c.<Run>b__0_0", "S.Run (lambda)")]
    [InlineData("DevBridge.Server.BridgeServer+<>c__DisplayClass16_0.<RunOnMainThread>b__0", "DevBridge.Server.BridgeServer.RunOnMainThread (lambda)")]
    [InlineData("Loader.Mods+<Load>d__12.MoveNext", "Loader.Mods.Load (async or iterator)")]
    [InlineData("Foo.Bar.<Run>g__Check|3_0", "Foo.Bar.Run.Check")]
    [InlineData("Outer+Inner.Method", "Outer.Inner.Method")]
    [InlineData("(wrapper dynamic-method) PlayerController.DMD<PlayerController::Update>", "PlayerController.Update (patched)")]
    [InlineData("UnityEngine.Debug:LogError", "UnityEngine.Debug.LogError")]
    public void CompilerNamesReadLikeTheSource(string method, string expected) =>
        Assert.Equal(expected, StackText.Readable(method));

    [Fact]
    public void MonoFrameIsSplitIntoParts()
    {
        string rich = StackText.Line(
            "  at ObenUIKit.Menu.ModMenu.Build (ObenUIKit.Controls.UiBuilder ui) [0x000e3] in /home/x/src/ObenUIKit/Menu/ModMenu.cs:131 ",
            "E", Colors);

        Assert.Equal("at ObenUIKit.Menu.ModMenu.Build (UiBuilder ui)  ModMenu.cs:131", Plain(rich));
        Assert.Contains("<color=T>ModMenu.</color><color=F>Build</color>", rich);
        Assert.Contains("<color=L>ModMenu.cs</color>", rich);
        Assert.Contains("<color=N>131</color>", rich);
    }

    [Fact]
    public void FrameWithoutSymbolsDropsTheModuleId()
    {
        string rich = StackText.Line("at ObenUIKit.Foo.Bar () [0x00000] in <4449e9424e7e4d4d81a660907c68cb97>:0", "E", Colors);

        Assert.Equal("at ObenUIKit.Foo.Bar ()", Plain(rich));
    }

    [Fact]
    public void LambdaFrameKeepsItsNote()
    {
        string rich = StackText.Line("at S+<>c.<Run>b__0_0 (ObenUIKit.Controls.UiBuilder ui) [0x00013] in m06-broken.cs:9", "E", Colors);

        Assert.Equal("at S.Run (lambda) (UiBuilder ui)  m06-broken.cs:9", Plain(rich));
        Assert.Contains("<color=F>Run</color>", rich);
        Assert.DoesNotContain("<>", Plain(rich));
    }

    [Theory]
    [InlineData("at System.Reflection.RuntimeMethodInfo.Invoke (System.Object obj) [0x0006a] in <abc>:0")]
    [InlineData("at (wrapper managed-to-native) System.Reflection.RuntimeMethodInfo.InternalInvoke(System.Reflection.RuntimeMethodInfo,object,object[],System.Exception&)")]
    [InlineData("UnityEngine.Debug:LogError (object)")]
    [InlineData("(wrapper managed-to-native) UnityEngine.Object:Destroy (UnityEngine.Object)")]
    [InlineData("--- End of stack trace from previous location where exception was thrown ---")]
    public void LibraryFramesAndMarkersAreDimmed(string line)
    {
        string rich = StackText.Line(line, "E", Colors);

        Assert.DoesNotContain("<color=F>", rich);
        Assert.DoesNotContain("<color=T>", rich);
        Assert.StartsWith("<color=M>", rich);
    }

    [Fact]
    public void PatchedGameMethodIsNotDimmed()
    {
        string rich = StackText.Line("at (wrapper dynamic-method) PlayerController.DMD<PlayerController::Update>(PlayerController)", "E", Colors);

        Assert.Contains("<color=F>Update</color>", rich);
        Assert.Contains("(patched)", Plain(rich));
    }

    [Fact]
    public void UnityFrameWithFile()
    {
        string rich = StackText.Line("CheatMenu.CheatPanel:Build (ObenUIKit.Controls.UiBuilder) (at C:/src/CheatPanel.cs:42)", "E", Colors);

        Assert.Equal("at CheatMenu.CheatPanel.Build (UiBuilder)  CheatPanel.cs:42", Plain(rich));
    }

    [Fact]
    public void ExceptionLinesColorTheType()
    {
        Assert.Equal("<color=E>InvalidOperationException</color><color=T>: test page failure</color>",
            StackText.Line("System.InvalidOperationException: test page failure", "E", Colors));
        Assert.Equal("Caused by IOException: disk full", Plain(StackText.Line(" ---> System.IO.IOException: disk full", "E", Colors)));
        Assert.Equal("Rethrown as KeyNotFoundException", Plain(StackText.Line("Rethrow as KeyNotFoundException", "C", Colors)));
    }

    [Fact]
    public void TitleUsesTheProblemsColor()
    {
        Assert.Equal("<color=C>Couldn't open the cheat window</color>", StackText.Title("Couldn't open the cheat window", "C", Colors));
        Assert.Equal("<color=E>NullReferenceException</color><color=T>: Object reference not set</color>",
            StackText.Title("NullReferenceException: Object reference not set", "E", Colors));
    }

    [Fact]
    public void AngleBracketsStayText()
    {
        string rich = StackText.Line("Value was <b>bold</b> and </noparse> too", "E", Colors);

        Assert.Equal("<color=M>Value was <noparse><</noparse>b>bold<noparse><</noparse>/b> and <noparse><</noparse>/noparse> too</color>", rich);
    }

    [Fact]
    public void UnityExceptionFrameWithDots()
    {
        string rich = StackText.Line("S+Loader.<Items>b__0_0 (System.Int32 x) (at errtest.cs:13)", "E", Colors);

        Assert.Equal("at S.Loader.Items (lambda) (Int32 x)  errtest.cs:13", Plain(rich));
        Assert.Contains("<color=F>Items</color>", rich);
    }

    [Fact]
    public void GenericArityIsDropped() =>
        Assert.Equal("at System.Collections.Generic.List[T].get_Item (Int32 index)",
            Plain(StackText.Line("System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <4449e9424e7e4d4d81a660907c68cb97>:0)", "E", Colors)));

    [Theory]
    [InlineData("Retry.Later (soon)")]
    [InlineData("Parameter name: index")]
    public void TextThatOnlyLooksLikeAFrameStaysText(string line) =>
        Assert.Equal("<color=M>" + line + "</color>", StackText.Line(line, "E", Colors));

    [Fact]
    public void InnerExceptionOnTheSameLineGetsItsOwn()
    {
        var lines = StackText.Lines(
            "System.InvalidOperationException: outer ---> System.IndexOutOfRangeException: inner\r\n  at A.B () [0x0] in x.cs:1 \n\n  --- End of inner exception stack trace ---");

        Assert.Equal(new[]
        {
            "System.InvalidOperationException: outer",
            "---> System.IndexOutOfRangeException: inner",
            "at A.B () [0x0] in x.cs:1",
            "--- End of inner exception stack trace ---",
        }, lines);
    }

    [Fact]
    public void EmptyLineIsEmpty() => Assert.Equal("", StackText.Line("   ", "E", Colors));
}
