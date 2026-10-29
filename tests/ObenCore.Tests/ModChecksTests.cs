using ObenLoader.Mods;

namespace ObenCore.Tests;

public class ModIdsTests
{
    [Theory]
    [InlineData("ObenFramework")]
    [InlineData("my.mod")]
    [InlineData("my_mod-2")]
    [InlineData("a")]
    public void AcceptsPlainIds(string id) => Assert.True(ModIds.IsValid(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("My Mod")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("mod!")]
    [InlineData(".hidden")]
    [InlineData("trailing.")]
    [InlineData("a..b")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("LPT9")]
    [InlineData("café")]
    [InlineData("ObenLoader")]
    [InlineData("obenloader")]
    public void RefusesUnsafeIds(string? id)
    {
        Assert.False(ModIds.IsValid(id!));
        Assert.NotNull(ModIds.Problem(id!));
    }

    [Fact]
    public void RefusesLongIds()
    {
        Assert.True(ModIds.IsValid(new string('a', ModIds.MaxLength)));
        Assert.False(ModIds.IsValid(new string('a', ModIds.MaxLength + 1)));
    }

    [Fact]
    public void RequireNamesTheParameter()
    {
        var e = Assert.Throws<ArgumentException>(() => ModIds.Require("bad id", "modId"));
        Assert.Equal("modId", e.ParamName);
    }

    [Theory]
    [InlineData("config.json", true)]
    [InlineData("My Save 1", true)]
    [InlineData("nul.json", false)]
    [InlineData("aux", false)]
    [InlineData("a:b", false)]
    [InlineData("a?b", false)]
    [InlineData("dir/file", false)]
    [InlineData("..", false)]
    [InlineData("name.", false)]
    [InlineData("name ", false)]
    [InlineData(" name", false)]
    [InlineData("tab\tname", false)]
    [InlineData("", false)]
    public void FileNames(string name, bool safe) => Assert.Equal(safe, ModIds.IsSafeFileName(name));

    [Fact]
    public void ConsoleDevicesAreReserved()
    {
        Assert.True(ModIds.IsReservedName("CONIN$"));
        Assert.True(ModIds.IsReservedName("com¹"));
        Assert.False(ModIds.IsReservedName("COM10"));
        Assert.False(ModIds.IsReservedName("console"));
    }
}

public class VersionRangeTests
{
    private static bool Matches(string range, string version)
    {
        Assert.True(VersionRange.TryParse(range, out var parsed, out string error), error);
        return parsed.Matches(version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("  x ")]
    [InlineData("0.4 || *")]
    public void AnyMatchesEverything(string? range)
    {
        Assert.True(VersionRange.TryParse(range, out var parsed, out _));
        Assert.True(parsed.IsAny);
        Assert.True(parsed.Matches("0.5.13"));
        Assert.True(parsed.Matches("garbage"));
    }

    [Theory]
    [InlineData("0.5.13", "0.5.13", true)]
    [InlineData("0.5.13", "0.5.13.4", true)]
    [InlineData("0.5.13", "0.5.14", false)]
    [InlineData("0.5", "0.5.13", true)]
    [InlineData("0.5.x", "0.5.0", true)]
    [InlineData("0.5.*", "0.6.0", false)]
    [InlineData("0.5.08", "0.5.8", true)]
    [InlineData("0.5.13", "0.5.13f1", true)]
    public void BareVersionsMatchByPrefix(string range, string version, bool expected) =>
        Assert.Equal(expected, Matches(range, version));

    [Theory]
    [InlineData(">=0.5.10", "0.5.10", true)]
    [InlineData(">=0.5.10", "0.5.9", false)]
    [InlineData(">0.5.10", "0.5.10", false)]
    [InlineData(">0.5.10", "0.5.10.1", true)]
    [InlineData("<0.6", "0.5.99", true)]
    [InlineData("<0.6", "0.6.0", false)]
    [InlineData("<=0.5.13", "0.5.13", true)]
    [InlineData("=0.5", "0.5.0", true)]
    [InlineData("==0.5.13", "0.5.13", true)]
    [InlineData("=0.5", "0.5.1", false)]
    public void Comparisons(string range, string version, bool expected) =>
        Assert.Equal(expected, Matches(range, version));

    [Theory]
    [InlineData(">=0.5.10 <0.6", "0.5.13", true)]
    [InlineData(">=0.5.10 <0.6", "0.6.1", false)]
    [InlineData(">=0.5.10 <0.6", "0.5.9", false)]
    [InlineData("0.5.08 || >=0.5.13", "0.5.8", true)]
    [InlineData("0.5.08 || >=0.5.13", "0.5.10", false)]
    [InlineData("0.5.08 || >=0.5.13", "0.5.13", true)]
    public void CombinedRanges(string range, string version, bool expected) =>
        Assert.Equal(expected, Matches(range, version));

    [Theory]
    [InlineData("abc")]
    [InlineData(">=")]
    [InlineData(">=0.5.x")]
    [InlineData("0.5.13 ||")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1..2")]
    [InlineData("~1.2")]
    public void RefusesNonsense(string range)
    {
        Assert.False(VersionRange.TryParse(range, out _, out string error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void UnreadableGameVersionDoesNotMatchARealRange()
    {
        Assert.False(Matches(">=0.5", "unknown"));
        Assert.False(Matches(">=0.5", null!));
    }
}

public class ModChecksTests
{
    private static ModManifest Manifest(string id = "MyMod", string? game = null, bool strict = false, params string[] builds) =>
        new ModManifest { Id = id, GameVersion = game, StrictGameVersion = strict, Version = "1.0.0", GameBuilds = new List<string>(builds) };

    [Fact]
    public void NoRangeNoProblem()
    {
        Assert.Null(ModChecks.Refusal(Manifest(), "0.5.13", out string warning));
        Assert.Null(warning);
    }

    [Fact]
    public void InvalidIdIsRefused()
    {
        string refusal = ModChecks.Refusal(Manifest("my mod"), "0.5.13", out _);
        Assert.Contains("Invalid id", refusal);
    }

    [Fact]
    public void OtherGameVersionWarns()
    {
        Assert.Null(ModChecks.Refusal(Manifest(game: "0.5.08"), "0.5.13", out string warning));
        Assert.Contains("0.5.08", warning);
        Assert.Contains("0.5.13", warning);
    }

    [Fact]
    public void StrictRangeRefuses()
    {
        string refusal = ModChecks.Refusal(Manifest(game: ">=0.5.08 <0.5.10", strict: true), "0.5.13", out string warning);
        Assert.Contains("strictGameVersion", refusal);
        Assert.Null(warning);
    }

    [Fact]
    public void MatchingStrictRangeLoads()
    {
        Assert.Null(ModChecks.Refusal(Manifest(game: "0.5.x", strict: true), "0.5.13", out string warning));
        Assert.Null(warning);
    }

    [Fact]
    public void UnreadableRangeOnlyWarnsEvenWhenStrict()
    {
        Assert.Null(ModChecks.Refusal(Manifest(game: "soon", strict: true), "0.5.13", out string warning));
        Assert.Contains("can't be read", warning);
    }

    [Fact]
    public void OtherSteamBuildWarns()
    {
        Assert.Null(ModChecks.Refusal(Manifest(builds: "25969064"), "0.5.13", "26000001", false, out string warning));
        Assert.Contains("25969064", warning);
        Assert.Contains("26000001", warning);
    }

    [Fact]
    public void ListedSteamBuildLoadsQuietly()
    {
        Assert.Null(ModChecks.Refusal(Manifest(builds: new[] { "1", "26000001" }), "0.5.13", "26000001", false, out string warning));
        Assert.Null(warning);
    }

    [Fact]
    public void UnknownSteamBuildIsNotChecked()
    {
        Assert.Null(ModChecks.Refusal(Manifest(strict: true, builds: "25969064"), "0.5.13", null, true, out string warning));
        Assert.Null(warning);
    }

    [Fact]
    public void StrictModRefusesAnotherBuild()
    {
        string refusal = ModChecks.Refusal(Manifest(strict: true, builds: "25969064"), "0.5.13", "26000001", false, out _);
        Assert.Contains("strictGameVersion", refusal);
    }

    [Fact]
    public void LoaderSettingRefusesEveryMismatch()
    {
        string refusal = ModChecks.Refusal(Manifest(game: "0.5.08"), "0.5.13", null, true, out string warning);
        Assert.Contains("RefuseOtherGameVersions", refusal);
        Assert.Null(warning);
    }

    [Fact]
    public void LoaderSettingStillOnlyWarnsForAnUnreadableRange()
    {
        Assert.Null(ModChecks.Refusal(Manifest(game: "soon"), "0.5.13", null, true, out string warning));
        Assert.Contains("can't be read", warning);
    }

    [Theory]
    [InlineData("my mod", "1.0")]
    [InlineData("Other", "latest")]
    public void BadDependenciesAreRefused(string id, string minimum)
    {
        var manifest = Manifest();
        manifest.Dependencies[id] = minimum;
        Assert.NotNull(ModChecks.Refusal(manifest, "0.5.13", out _));
    }

    [Fact]
    public void UsualDependenciesPass()
    {
        var manifest = Manifest();
        manifest.Dependencies["ObenFramework"] = "0.1.0";
        manifest.Dependencies["ObenUIKit"] = "*";
        Assert.Null(ModChecks.Refusal(manifest, "0.5.13", out string warning));
        Assert.Null(warning);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "*")]
    [InlineData(false, " * ")]
    public void AnyGameVersionLoadsEverywhereWithALogNote(bool flag, string? range)
    {
        var manifest = Manifest(game: range, strict: true, builds: "25969064");
        manifest.AnyGameVersion = flag;
        Assert.Null(ModChecks.Refusal(manifest, "0.6.0", "26000001", true, out string warning, out string note));
        Assert.Null(warning);
        Assert.Contains("every game version", note);
        Assert.Contains("0.6.0", note);
    }

    [Fact]
    public void AnyGameVersionOverridesARange()
    {
        var manifest = Manifest(game: "0.5.08");
        manifest.AnyGameVersion = true;
        Assert.Null(ModChecks.Refusal(manifest, "0.5.13", null, false, out string warning, out string note));
        Assert.Null(warning);
        Assert.Contains("overrides", note);
    }

    [Fact]
    public void ANormalModHasNoNote()
    {
        Assert.Null(ModChecks.Refusal(Manifest(game: "0.5.x"), "0.5.13", null, false, out _, out string note));
        Assert.Null(note);
    }

    [Fact]
    public void UnreadableOwnVersionWarns()
    {
        var manifest = Manifest();
        manifest.Version = "first";
        Assert.Null(ModChecks.Refusal(manifest, "0.5.13", out string warning));
        Assert.Contains("'first'", warning);
    }
}

public class SteamBuildTests
{
    private const string Manifest = "\"AppState\"\n{\n\t\"appid\"\t\t\"951240\"\n\t\"name\"\t\t\"Obenseuer\"\n\t\"buildid\"\t\t\"25969064\"\n\t\"installdir\"\t\t\"Obenseuer\"\n}";

    [Fact]
    public void ReadsTheBuildId() => Assert.Equal("25969064", ObenLoader.Runtime.SteamBuild.Parse(Manifest, null));

    [Fact]
    public void MatchesTheInstallFolder()
    {
        Assert.Equal("25969064", ObenLoader.Runtime.SteamBuild.Parse(Manifest, "obenseuer"));
        Assert.Null(ObenLoader.Runtime.SteamBuild.Parse(Manifest, "OtherGame"));
    }

    [Fact]
    public void NoBuildIdNoAnswer() => Assert.Null(ObenLoader.Runtime.SteamBuild.Parse("\"AppState\" { }", null));
}
