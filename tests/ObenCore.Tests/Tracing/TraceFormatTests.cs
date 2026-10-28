using DevBridge.Tracing;

namespace ObenCore.Tests.Tracing;

[Collection("tracer")]
public class TraceFormatTests
{
    private sealed class Plain
    {
    }

    private sealed class Named
    {
        public override string ToString() => "named thing";
    }

    private enum Mood
    {
        Calm,
    }

    private static void Sample(int a, string b, out int c, ref float d) => c = (int)d + a + b.Length;

    [Fact]
    public void FormatsCommonValues()
    {
        Assert.Equal("null", TraceFormat.Value(null));
        Assert.Equal("\"hi\"", TraceFormat.Value("hi"));
        Assert.Equal("true", TraceFormat.Value(true));
        Assert.Equal("1.5", TraceFormat.Value(1.5f));
        Assert.Equal("0.333", TraceFormat.Value(1d / 3d));
        Assert.Equal("42", TraceFormat.Value(42));
        Assert.Equal("Calm", TraceFormat.Value(Mood.Calm));
        Assert.Equal("Int32[3]", TraceFormat.Value(new[] { 1, 2, 3 }));
        Assert.Equal("List[2]", TraceFormat.Value(new List<string> { "a", "b" }));
        Assert.Equal("typeof(String)", TraceFormat.Value(typeof(string)));
    }

    [Fact]
    public void ObjectsShowTheirOwnToStringOrTheirType()
    {
        Assert.Equal("Plain", TraceFormat.Value(new Plain()));
        Assert.Equal("named thing", TraceFormat.Value(new Named()));
    }

    [Fact]
    public void DescribeHookComesBeforeToString()
    {
        try
        {
            TraceFormat.Describe = v => v is Named ? "described" : null;
            Assert.Equal("described", TraceFormat.Value(new Named()));
            Assert.Equal("Plain", TraceFormat.Value(new Plain()));
        }
        finally
        {
            TraceFormat.Describe = null;
        }
    }

    [Fact]
    public void LongTextIsCut()
    {
        string text = TraceFormat.Value(new string('x', 500));
        Assert.Equal(TraceFormat.MaxText + 5, text.Length); // quotes and "..."
        Assert.EndsWith("...\"", text);
    }

    [Fact]
    public void ArgsMarkOutAndRef()
    {
        var parameters = typeof(TraceFormatTests).GetMethod(nameof(Sample), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetParameters();
        Assert.Equal("1, \"two\", out, ref 3", TraceFormat.Args(parameters, new object[] { 1, "two", 0, 3f }));
        Assert.Equal("", TraceFormat.Args(Array.Empty<System.Reflection.ParameterInfo>(), null));
    }

    [Fact]
    public void MethodNamesReadLikeCalls()
    {
        Assert.Equal("TraceFormatTests.Sample", TraceFormat.Method(typeof(TraceFormatTests).GetMethod(nameof(Sample), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!));
        Assert.Equal("List.Count", TraceFormat.Method(typeof(List<int>).GetProperty("Count")!.GetGetMethod()!));
        Assert.Equal("List.Capacity=", TraceFormat.Method(typeof(List<int>).GetProperty("Capacity")!.GetSetMethod()!));
    }

    [Theory]
    [InlineData("CheatMenu-r3", "CheatMenu")]
    [InlineData("ObenFramework-r31", "ObenFramework")]
    [InlineData("CheatMenu", "CheatMenu")]
    [InlineData("Some-rocket", "Some-rocket")]
    [InlineData("Mod-r", "Mod-r")]
    [InlineData("", "")]
    public void ModNamesLoseTheReloadSuffix(string assembly, string expected) => Assert.Equal(expected, TraceFormat.ModName(assembly));
}
