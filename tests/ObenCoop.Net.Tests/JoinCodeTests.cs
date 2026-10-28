using ObenCoop.Net.Lobby;
using Xunit;

namespace ObenCoop.Net.Tests;

public class JoinCodeTests
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Theory]
    [InlineData("192.168.1.20", 27890)]
    [InlineData("127.0.0.1", 1)]
    [InlineData("255.255.255.255", 65535)]
    [InlineData("10.0.0.7", 27891)]
    public void DirectCodesRoundTrip(string ip, int port)
    {
        string code = JoinCode.ForAddress(ip, port);

        Assert.Equal(14, code.Length);
        Assert.True(JoinCode.TryDecode(code, out var address));
        Assert.Equal(LobbyKind.Direct, address.Kind);
        Assert.Equal(ip, address.Host);
        Assert.Equal(port, address.Port);
    }

    [Fact]
    public void SteamCodesRoundTrip()
    {
        const ulong lobby = 109775241234567890UL;
        string code = JoinCode.ForSteamLobby(lobby);

        Assert.Equal(19, code.Length);
        Assert.True(JoinCode.TryDecode(code, out var address));
        Assert.Equal(LobbyKind.Steam, address.Kind);
        Assert.Equal(lobby, address.LobbyId);
    }

    [Fact]
    public void CodesAreReadForgivingly()
    {
        string code = JoinCode.ForAddress("192.168.1.20", 27890);
        string sloppy = " " + code.Replace("-", "").ToLowerInvariant().Replace('0', 'o').Replace('1', 'l') + " ";

        Assert.True(JoinCode.TryDecode(sloppy, out var address));
        Assert.Equal("192.168.1.20", address.Host);
    }

    [Fact]
    public void EveryWrongCharacterIsCaught()
    {
        string code = JoinCode.ForAddress("192.168.1.20", 27890).Replace("-", "");
        for (int i = 0; i < code.Length; i++)
        {
            foreach (char c in Alphabet)
            {
                if (c == code[i])
                    continue;
                string typo = code.Substring(0, i) + c + code.Substring(i + 1);
                Assert.False(JoinCode.TryDecode(typo, out _), $"{typo} was accepted");
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("UUUU-UUUU-UUUU")]
    [InlineData("0000-0000-0000")]
    public void NonCodesAreNotRead(string text) => Assert.False(JoinCode.TryDecode(text, out _));

    [Fact]
    public void AddressesParseFromWhatPlayersType()
    {
        Assert.True(LobbyAddress.TryParse("192.168.1.20", 27890, out var plain, out _));
        Assert.Equal(LobbyAddress.Direct("192.168.1.20", 27890), plain);

        Assert.True(LobbyAddress.TryParse("example.org:4000", 27890, out var named, out _));
        Assert.Equal(LobbyAddress.Direct("example.org", 4000), named);

        Assert.True(LobbyAddress.TryParse(JoinCode.ForAddress("10.0.0.7", 27891), 27890, out var coded, out _));
        Assert.Equal(LobbyAddress.Direct("10.0.0.7", 27891), coded);

        Assert.True(LobbyAddress.TryParse("steam:109775241234567890", 27890, out var steam, out _));
        Assert.Equal(LobbyAddress.Steam(109775241234567890UL), steam);

        Assert.True(LobbyAddress.TryParse("109775241234567890", 27890, out var bare, out _));
        Assert.Equal(LobbyKind.Steam, bare.Kind);
    }

    [Fact]
    public void ATypoInACodeIsReportedAsOne()
    {
        string code = JoinCode.ForAddress("10.0.0.7", 27891);
        string typo = (code[0] == 'A' ? 'B' : 'A') + code.Substring(1);

        Assert.False(LobbyAddress.TryParse(typo, 27890, out _, out string problem));
        Assert.Contains("typo", problem);
        Assert.False(LobbyAddress.TryParse("  ", 27890, out _, out _));
        Assert.False(LobbyAddress.TryParse("steam:abc", 27890, out _, out _));
    }

    [Fact]
    public void OnlyDottedIPv4AddressesGetACode()
    {
        Assert.True(LobbyAddress.Direct("192.168.1.20", 27890).TryGetJoinCode(out _));
        Assert.False(LobbyAddress.Direct("example.org", 27890).TryGetJoinCode(out _));
        Assert.False(LobbyAddress.Direct("1234", 27890).TryGetJoinCode(out _));
        Assert.True(LobbyAddress.Steam(1).TryGetJoinCode(out _));
    }
}
