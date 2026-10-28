using Mono.Cecil;
using ObenTool.Checks;

namespace ObenCore.Tests;

/// <summary>
/// Runs the hook checker over this test assembly. The fixtures in CheckerFixtures.cs play both the
/// game (Target) and the mods that hook it, so each fixture class has a known verdict.
/// </summary>
public sealed class HookCheckerTests : IDisposable
{
    private readonly string _managed = Path.Combine(Path.GetTempPath(), "oben-check-" + Guid.NewGuid().ToString("N"));
    private readonly GameAssemblies _game;
    private readonly ModReport _report = new("Fixtures");

    public HookCheckerTests()
    {
        string self = typeof(HookCheckerTests).Assembly.Location;
        Directory.CreateDirectory(_managed);
        // The "game" is a copy of this assembly, so the fixture types can be found by name too.
        File.Copy(self, Path.Combine(_managed, "Assembly-CSharp.dll"));
        _game = new GameAssemblies(_managed);

        var module = _game.Load(self);
        PatchScanner.Scan(module, _game, _report);
        AccessToolsScanner.Scan(module, _game, _report);
    }

    public void Dispose()
    {
        _game.Dispose();
        Directory.Delete(_managed, true);
    }

    private List<Finding> For(string cls) =>
        _report.Findings.Where(f => f.Class.Contains("Fixtures." + cls)).ToList();

    private Finding Single(string cls, Severity severity, string message)
    {
        var finding = Assert.Single(For(cls));
        Assert.Equal(severity, finding.Severity);
        Assert.Contains(message, finding.Message);
        return finding;
    }

    private List<Finding> Lookup(string hookPart) =>
        _report.Findings.Where(f => f.Class.EndsWith("Lookups") && f.Hook.Contains(hookPart)).ToList();

    private bool Passed(string text) => _report.Passed.Any(p => p.Contains(text));

    [Fact]
    public void ACorrectPatchHasNoFindings()
    {
        Assert.Empty(For("GoodPatch"));
        Assert.True(Passed("GoodPatch: [HarmonyPatch] Target.Hit"));
    }

    [Fact]
    public void MissingMethodIsReported()
    {
        var finding = Single("MissingMethodPatch", Severity.Missing, "no method 'Gone'");
        Assert.Equal("Fixtures", finding.Mod);
        Assert.Contains("Target.Gone", finding.Hook);
    }

    [Fact]
    public void OverloadsWithoutArgumentTypesAreAmbiguous()
    {
        var finding = Single("AmbiguousPatch", Severity.Ambiguous, "2 overloads of 'Over'");
        Assert.Contains("Over(Int32)", finding.Message);
        Assert.Contains("Over(String)", finding.Message);
    }

    [Fact]
    public void ArgumentTypesPickTheOverload()
    {
        Assert.Empty(For("OverloadPatch"));
        Single("WrongOverloadPatch", Severity.Missing, "no overload of 'Over'");
    }

    [Fact]
    public void PatchesMustTargetTheTypeThatDeclaresTheMethod()
    {
        Single("InheritedPatch", Severity.Missing, "declared on ObenCore.Tests.Fixtures.Target");
    }

    [Fact]
    public void RenamedParametersAndFieldsAreReported()
    {
        var messages = For("RenamedParameterPatch").Select(f => f.Message).ToList();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.Contains("'amout'") && m.Contains("it has amount, who"));
        Assert.Contains(messages, m => m.Contains("___missing") && m.Contains("field 'missing'"));
    }

    [Fact]
    public void ClassAndMethodAttributesAreCombined()
    {
        Single("SplitAcrossClassAndMethod", Severity.Missing, "no method 'Nowhere'");
        Assert.True(Passed("SplitAcrossClassAndMethod: [HarmonyPatch] Target.Hit"));
    }

    [Fact]
    public void PropertyAndConstructorTargetsAreChecked()
    {
        Assert.Empty(For("GetterPatch"));
        Assert.Empty(For("ConstructorPatch"));
        Single("MissingSetterPatch", Severity.Missing, "has no setter");
    }

    [Fact]
    public void ATypeGivenByNameThatIsGoneIsReported()
    {
        Single("GoneTypePatch", Severity.Missing, "no longer exists");
    }

    [Fact]
    public void ATypeWithoutAMethodNameIsReported()
    {
        Single("NoMethodNamed", Severity.Missing, "no target method");
    }

    [Fact]
    public void ComputedTargetsAreCountedNotChecked()
    {
        Assert.Empty(For("ComputedTarget"));
        Assert.True(_report.Dynamic >= 1);
    }

    [Fact]
    public void CategoryClassesAreCheckedAndLabelled()
    {
        Assert.Empty(For("CategoryPatch"));
        Assert.True(Passed("CategoryPatch [category fixture-category]"));
    }

    [Fact]
    public void AccessToolsMethodLookups()
    {
        Assert.Single(Lookup("Method(Target, \"Gone\")"));
        Assert.Equal(Severity.Ambiguous, Assert.Single(Lookup("Method(Target, \"Over\")")).Severity);
        Assert.Empty(Lookup("Method(Target, \"Over\", [String])"));
        Assert.True(Passed("Method(Target, \"Over\", [String])"));
        Assert.Contains("no overload", Assert.Single(Lookup("[Double]")).Message);
    }

    [Fact]
    public void InheritedMembersCountForPlainLookupsButNotDeclaredOnes()
    {
        Assert.Empty(Lookup("AccessTools.Method(Derived, \"Plain\")"));
        Assert.True(Passed("AccessTools.Method(Derived, \"Plain\")"));
        Assert.Single(Lookup("DeclaredMethod(Derived, \"Plain\")"));
        Assert.Single(Lookup("DeclaredField(Derived, \"Count\")"));
    }

    [Fact]
    public void AccessToolsFieldAndPropertyLookups()
    {
        Assert.True(Passed("AccessTools.Field(Target, \"name\")"));
        Assert.Single(Lookup("Field(Target, \"nome\")"));
        Assert.True(Passed("AccessTools.FieldRefAccess(Target, \"Count\")"));
        Assert.Single(Lookup("FieldRefAccess(Target, \"Cnt\")"));
        Assert.True(Passed("AccessTools.Property(Target, \"Value\")"));
        Assert.Contains("no setter", Assert.Single(Lookup("PropertySetter(Target, \"ReadOnly\")")).Message);
    }

    [Fact]
    public void TheTypeColonMemberFormIsChecked()
    {
        Assert.True(Passed("AccessTools.Method(ObenCore.Tests.Fixtures.Target, \"Plain\")"));
        Assert.Single(Lookup("Target, \"Absent\")"));
    }

    [Fact]
    public void LookupsWithANonConstantNameAreCountedNotChecked()
    {
        Assert.DoesNotContain(_report.Findings, f => f.Class.EndsWith("Lookups") && f.Hook.Contains("dynamicName"));
        Assert.True(_report.Dynamic >= 2);
    }

    [Fact]
    public void NoAmbiguityIsReportedForGoodHooks()
    {
        Assert.DoesNotContain(_report.Findings, f => f.Class.EndsWith(".GoodPatch") || f.Class.EndsWith(".OverloadPatch"));
    }
}
