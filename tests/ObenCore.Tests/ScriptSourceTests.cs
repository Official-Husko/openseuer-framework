using ObenTool.Scripting;

namespace ObenCore.Tests;

public class ScriptSourceTests
{
    private const string WholeFile = """
        public static class MyScript
        {
            public static object Run(ScriptRunner.ScriptScope scope) => 1;
        }
        """;

    [Fact]
    public void WholeFileIsUsedAsItIs()
    {
        Assert.True(ScriptSource.IsWholeFile(WholeFile));
        Assert.Equal(WholeFile, ScriptSource.Prepare(WholeFile, "x.cs"));
    }

    [Theory]
    [InlineData("1 + 1")]
    [InlineData("var x = 1; x")]
    [InlineData("Run(1)")]
    [InlineData("static class Foo {}")]
    public void LooseTextIsNotAWholeFile(string code)
    {
        Assert.False(ScriptSource.IsWholeFile(code));
    }

    [Fact]
    public void LoneExpressionIsReturned()
    {
        string source = ScriptSource.Prepare("  ItemDatabase.database.Count  ", "eval");
        Assert.Contains("return (object)(ItemDatabase.database.Count);", source);
        Assert.DoesNotContain("return null;", source);
    }

    [Fact]
    public void StatementsRunAndReturnNullAtTheEnd()
    {
        string source = ScriptSource.Prepare("var n = 1;\nLog(n);", "eval");
        Assert.Contains("var n = 1;\nLog(n);", source);
        Assert.Contains("return null;", source);
        Assert.DoesNotContain("return (object)(", source);
    }

    [Fact]
    public void StatementsMayReturnTheirOwnValue()
    {
        string source = ScriptSource.Prepare("var n = 5; return n;", "eval");
        Assert.Contains("var n = 5; return n;", source);
        Assert.Contains("return null;", source);
    }

    [Fact]
    public void BlockCountsAsStatementsEvenWithoutASemicolon()
    {
        string source = ScriptSource.Prepare("{ }", "eval");
        Assert.Contains("return null;", source);
        Assert.DoesNotContain("return (object)(", source);
    }

    [Fact]
    public void WrapperHasTheScopeParameterAndDefaultUsings()
    {
        string source = ScriptSource.Prepare("1", "eval");
        Assert.Contains("public static class ObenScript", source);
        Assert.Contains("public static object Run(ScriptScope Scope)", source);
        foreach (string u in ScriptSource.DefaultUsings)
            Assert.Contains($"using {u};", source);
        Assert.Contains("using Object = UnityEngine.Object;", source);
    }

    [Fact]
    public void LineDirectiveMapsToTheLabelAndLineOne()
    {
        string source = ScriptSource.Prepare("1", "check.cs");
        Assert.Contains("#line 1 \"check.cs\"", source);
        Assert.Contains("#line default", source);
    }

    [Fact]
    public void UserCodeStartsRightAfterTheLineDirective()
    {
        var lines = ScriptSource.Prepare("var a = 1;", "f.cs").Split('\n');
        int directive = Array.FindIndex(lines, l => l.TrimStart().StartsWith("#line 1 "));
        Assert.True(directive >= 0);
        Assert.Equal("var a = 1;", lines[directive + 1].Trim());
    }
}
