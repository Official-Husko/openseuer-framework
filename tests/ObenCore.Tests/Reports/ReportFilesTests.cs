using Oben.Reports;

namespace ObenCore.Tests.Reports;

public sealed class ReportFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-files-" + Guid.NewGuid().ToString("N"));

    public ReportFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private string Touch(string relative, DateTime? written = null)
    {
        string path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        if (written != null)
            File.SetLastWriteTimeUtc(path, written.Value);
        return path;
    }

    [Fact]
    public void SettingsLeaveOutManifestsChangelogsAndIdentities()
    {
        Touch("Mods/ObenCoop/mod.json");
        Touch("Mods/ObenCoop/config.json");
        Touch("Mods/ObenCoop/identity.json");
        Touch("Mods/ObenCoop/identity-2.json");
        Touch("Mods/ObenUIKit/changelog.json");
        Touch("Mods/ObenUIKit/hotkeys.json");

        var names = ReportFiles.Settings(Path.Combine(_dir, "ObenLoader"), Path.Combine(_dir, "Mods")).Select(e => e.Name).ToList();

        Assert.Equal(new[]
        {
            "settings/ObenLoader/settings.json", "settings/ObenLoader/game-check.json",
            "settings/Mods/ObenCoop/config.json", "settings/Mods/ObenUIKit/hotkeys.json",
        }, names);
    }

    [Fact]
    public void LogsOfASecondCopyHaveItsSuffix()
    {
        var names = ReportFiles.Logs(Path.Combine(_dir, "logs"), "-2", null).Select(e => e.Source).ToList();
        Assert.Contains(Path.Combine(_dir, "logs", "latest-2.log"), names);
        Assert.Contains(Path.Combine(_dir, "logs", "errors-previous-2.log"), names);
    }

    [Fact]
    public void TheNewestSaveIsTheOneWrittenLast()
    {
        Touch("Saves/Esko/Slot1/Info.tnmt", new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc));
        Touch("Saves/Esko/Slot2/Info.tnmt", new DateTime(2026, 10, 29, 23, 0, 0, DateTimeKind.Utc));
        Touch("Saves/Anna/Autosave/Info.tnmt", new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc));
        Touch("Saves/Anna/Empty/notes.txt");

        string newest = ReportFiles.NewestSave(Path.Combine(_dir, "Saves"))!;
        var files = ReportFiles.Save(newest, out string? name);

        Assert.Equal("Esko/Slot2", name);
        Assert.Equal("save/Esko/Slot2/Info.tnmt", Assert.Single(files).Name);
        Assert.False(files[0].IsText);
        Assert.Null(ReportFiles.NewestSave(Path.Combine(_dir, "nowhere")));
    }

    [Fact]
    public void TheNewestCrashFromTheLastWeekIsAdded()
    {
        var now = new DateTime(2026, 10, 30, 1, 0, 0, DateTimeKind.Utc);
        Touch("Crashes/Crash_2026-10-28_092739105/crash.dmp");
        Touch("Crashes/Crash_2026-10-29_162427293/crash.dmp");
        Touch("Crashes/Crash_2026-10-29_162427293/Player.log");
        Directory.SetLastWriteTimeUtc(Path.Combine(_dir, "Crashes", "Crash_2026-10-28_092739105"), now.AddDays(-2));
        Directory.SetLastWriteTimeUtc(Path.Combine(_dir, "Crashes", "Crash_2026-10-29_162427293"), now.AddHours(-9));

        var files = ReportFiles.Crash(Path.Combine(_dir, "Crashes"), now, out string? name);

        Assert.Equal("Crash_2026-10-29_162427293", name);
        Assert.Equal(new[] { "crash/Crash_2026-10-29_162427293/crash.dmp", "crash/Crash_2026-10-29_162427293/Player.log" }, files.Select(f => f.Name));
        Assert.False(files[0].IsText);
        Assert.True(files[1].IsText);
        Assert.Empty(ReportFiles.Crash(Path.Combine(_dir, "Crashes"), now.AddDays(8), out string? old));
        Assert.Null(old);
    }

    [Fact]
    public void ProblemsAreReadBackFromErrorsLog()
    {
        const string log = "ObenseuerExtended error report\nWritten 2026-10-30 01:00:00\nGame Obenseuer 0.5.13\nMods: ObenFramework 0.1.0\n\n"
            + "12 problems this session.\n\n[23:21:23] ERROR Unity: NullReferenceException: Object reference not set\n  2 times.\n"
            + "  LightController.Awake ()\n\n[23:40:00] CAUGHT ObenCoop: lost the host\n  Once.\n\n...and 10 more not listed.\n";

        var titles = ReportAssembly.ProblemsFromErrorLog(log, out int count);

        Assert.Equal(12, count);
        Assert.Equal(new[] { "ObenCoop: lost the host", "Unity: NullReferenceException: Object reference not set" }, titles);
        Assert.Empty(ReportAssembly.ProblemsFromErrorLog(null, out int none));
        Assert.Equal(0, none);
    }

    [Fact]
    public void EntriesPutTheSummaryAndSnapshotFirstAndSkipMissingFiles()
    {
        string log = Touch("logs/latest.log");
        var entries = ReportAssembly.Entries(new SystemSnapshot { Build = "0.1.0 (abc1234)" }, new ReportOptions(), new List<string>(), 0,
            new[] { new ReportEntry { Name = "logs/latest.log", Source = log }, new ReportEntry { Name = "logs/gone.log", Source = Path.Combine(_dir, "gone.log") } },
            null, DateTime.Now, s => "{}", out string summary);

        Assert.Equal(new[] { "report.md", "system.json", "logs/latest.log" }, entries.Select(e => e.Name));
        Assert.Contains("- logs/latest.log", summary);
        Assert.DoesNotContain("gone.log", summary);
    }
}
