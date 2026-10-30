using Oben.Reports;

namespace ObenCore.Tests.Reports;

public class ReportSummaryTests
{
    private static SystemSnapshot Snapshot() => new SystemSnapshot
    {
        Build = "0.1.0 (a185d27)",
        Game = "0.5.13",
        GameBuild = "26179069",
        GameCodeHash = "72c44ab48d2ea50f",
        GameExeHash = "8e0953b0a66c7b62",
        Os = "Windows 10 (10.0.19045) 64bit",
        Wine = "9.0",
        Host = "Linux 6.8.0",
        Cpu = "AMD Ryzen 7 5800X",
        CpuCores = 16,
        CpuMhz = 3800,
        Gpu = "NVIDIA GeForce RTX 3070",
        GpuMemoryMb = 8192,
        GpuApi = "Direct3D 11.0 [level 11.1]",
        Display = "1600x900 Windowed, 60 Hz",
        RamMb = 32768,
        Mods = { new ModEntry { Id = "ObenFramework", Version = "0.1.0", State = "Loaded" }, new ModEntry { Id = "ObenSave", Version = "0.1.0", State = "Disabled" } },
    };

    private static string Markdown(SystemSnapshot s, ReportOptions options) =>
        ReportSummary.Markdown(s.For(options), options, new[] { "Unity: NullReferenceException", "ObenCoop: lost the host" }, 2,
            new[] { "report.md", "logs/latest.log" }, null, new DateTime(2026, 10, 30, 1, 20, 0));

    [Fact]
    public void AlwaysHasTheVersionsAndTheSystem()
    {
        string text = Markdown(Snapshot(), new ReportOptions());
        Assert.Contains("| ObenseuerExtended | 0.1.0 (a185d27) |", text);
        Assert.Contains("| Game | Obenseuer 0.5.13, Steam build 26179069 |", text);
        Assert.Contains("`72c44ab48d2ea50f`", text);
        Assert.Contains("| Operating system | Windows 10 (10.0.19045) 64bit through Wine 9.0 on Linux 6.8.0 |", text);
        Assert.Contains("- ObenSave 0.1.0 (Disabled)", text);
        Assert.Contains("- ObenFramework 0.1.0\n", text);
        Assert.Contains("2 problems:", text);
        Assert.Contains("- logs/latest.log", text);
        Assert.Contains("Personal info is hidden", text);
    }

    [Fact]
    public void ThePcOnlyWhenTicked()
    {
        string none = Markdown(Snapshot(), new ReportOptions());
        Assert.DoesNotContain("Ryzen", none);
        Assert.DoesNotContain("RTX", none);
        Assert.DoesNotContain("Memory", none);

        string all = Markdown(Snapshot(), new ReportOptions { Cpu = true, Gpu = true, Ram = true });
        Assert.Contains("| Processor | AMD Ryzen 7 5800X, 16 threads, 3800 MHz |", all);
        Assert.Contains("| Graphics | NVIDIA GeForce RTX 3070, 8 GB, Direct3D 11.0 [level 11.1] |", all);
        Assert.Contains("| Memory | 32 GB |", all);
    }

    [Fact]
    public void BepInExIsListedWhenThere()
    {
        var s = Snapshot();
        s.BepInEx = new BepInExInfo
        {
            Version = "5.4.23.2",
            Doorstop = "winhttp.dll",
            Plugins = { new PluginEntry { File = "plugins/Coop/Coop.dll", Guid = "nomads.coop", Name = "Coop", Version = "1.2.0", Sha256 = "a1b2c3d4e5f6" } },
        };
        string text = Markdown(s, new ReportOptions());
        Assert.Contains("### BepInEx 5.4.23.2 with 1 plugin", text);
        Assert.Contains("- plugins/Coop/Coop.dll: Coop 1.2.0 (nomads.coop), a1b2c3d4e5f6", text);
        Assert.DoesNotContain("BepInEx", Markdown(Snapshot(), new ReportOptions()));
    }

    [Fact]
    public void TheIssueLinkFillsTheFormFields()
    {
        string link = IssueLink.Bug(Snapshot().For(new ReportOptions { Ram = true }));
        Assert.StartsWith(IssueLink.Repository + "/issues/new?template=bug_report.yml&mod-version=0.1.0%20%28a185d27%29", link);
        Assert.Contains("&game-version=Obenseuer%200.5.13%2C%20Steam%20build%2026179069", link);
        Assert.Contains("&specs=Memory%3A%2032%20GB", link);
        Assert.DoesNotContain("Processor", link);
        Assert.DoesNotContain("bepinex=", link);
    }

    [Fact]
    public void ACrashGetsItsTimeAndTheDumpIsNamedAsNotCleaned()
    {
        string text = ReportSummary.Markdown(Snapshot(), new ReportOptions(), Array.Empty<string>(), 0, new[] { "report.md" }, null,
            new DateTime(2026, 10, 30, 1, 20, 0), "Crash_2026-10-29_162427293");
        Assert.Contains("| Last crash | 2026-10-29 16:24 UTC, its dump is in crash/ |", text);
        Assert.Contains("except inside crash.dmp", text);
    }

    [Fact]
    public void NoWineMeansWindows()
    {
        var s = Snapshot();
        s.Wine = null;
        Assert.Equal("Windows 10 (10.0.19045) 64bit", s.OsLine());
    }
}
