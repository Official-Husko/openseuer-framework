using Oben.Reports;

namespace ObenCore.Tests.Reports;

public class ReportRedactorTests
{
    private static string Run(params string[] texts)
    {
        var redactor = new ReportRedactor();
        foreach (string text in texts)
            redactor.Learn(text);
        return string.Join("\n", texts.Select(redactor.Apply));
    }

    [Fact]
    public void WindowsUserFolderIsHidden()
    {
        Assert.Equal(@"C:\Users\<user>\AppData\LocalLow\Loiste Interactive",
            Run(@"C:\Users\Jonna\AppData\LocalLow\Loiste Interactive"));
    }

    [Fact]
    public void LinuxMacAndWineFoldersAreHidden()
    {
        string text = Run("/home/kaarlo/.steam/steam", "/run/media/kaarlo/games/Obenseuer", @"Z:\home\kaarlo\Games", "/Users/mira/Library");
        Assert.DoesNotContain("kaarlo", text);
        Assert.DoesNotContain("mira", text);
        Assert.Contains("/home/<user>/.steam/steam", text);
        Assert.Contains("/run/media/<user>/games/Obenseuer", text);
    }

    [Fact]
    public void ANameLearnedInOneTextIsHiddenInTheOthers()
    {
        string text = Run("Signed in as kaarlo on this machine", @"Game folder: Z:\home\kaarlo\Games");
        Assert.Equal("Signed in as <user> on this machine\nGame folder: Z:\\home\\<user>\\Games", text);
    }

    [Fact]
    public void PartsOfOtherWordsStay()
    {
        Assert.Equal("/home/<user> runs maxwell", Run("/home/max runs maxwell"));
    }

    [Fact]
    public void WinesOwnUserStays()
    {
        Assert.Equal(@"C:\users\steamuser\AppData", Run(@"C:\users\steamuser\AppData"));
    }

    [Fact]
    public void SteamIdsAreHiddenButLobbyIdsStay()
    {
        Assert.Equal("Steam is up (signed in as <steam id>), lobby 109775241043398451",
            Run("Steam is up (signed in as 76561197960287930), lobby 109775241043398451"));
    }

    [Fact]
    public void AddressesAreHiddenButLoopbackAndVersionsStay()
    {
        Assert.Equal("Joined <ip>:27890 and <ip>, bridge on 127.0.0.1:27860",
            Run("Joined 203.0.113.7:27890 and 192.168.1.20, bridge on 127.0.0.1:27860"));
        Assert.Equal("0Harmony, Version=2.3.6.0 and Windows 10 (10.0.19045) 64bit and v1.2.3.4",
            Run("0Harmony, Version=2.3.6.0 and Windows 10 (10.0.19045) 64bit and v1.2.3.4"));
    }

    [Fact]
    public void AFiveNumberVersionIsNoAddress()
    {
        Assert.Equal("build 10.0.19045.1.2", Run("build 10.0.19045.1.2"));
    }

    [Fact]
    public void NamesAddedByHandAreHiddenToo()
    {
        var redactor = new ReportRedactor();
        redactor.AddName("DESKTOP-K4R1");
        Assert.Equal("machine <user>", redactor.Apply("machine DESKTOP-K4R1"));
    }
}
