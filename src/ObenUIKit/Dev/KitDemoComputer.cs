using System;
using ObenFramework.Clock;
using ObenUIKit.Computer;

namespace ObenUIKit.Dev;

/// <summary>
/// Two demo computer apps for checking the computer support by hand: "Demo Stocks" opens in a kit window over
/// the screen, "Demo Notes" in a window on the computer's own screen. The bridge's <c>computer.apps demo=true</c>
/// adds them, <c>demo=false</c> takes them away.
/// </summary>
public static class KitDemoComputer
{
    private const string Stocks = "ObenUIKit.DemoStocks";
    private const string Notes = "ObenUIKit.DemoNotes";

    private static IDisposable _stocks;
    private static IDisposable _notes;
    private static string _typed = "";
    private static int _presses;

    public static bool Shown => _stocks != null;

    public static void Show(bool on)
    {
        if (!on)
        {
            _stocks?.Dispose();
            _notes?.Dispose();
            _stocks = _notes = null;
            return;
        }
        if (_stocks != null)
            return;
        var kit = UIKitMod.Instance;
        _stocks = ComputerApps.Register(kit, Stocks, "Demo Stocks", ui =>
        {
            ui.Header("Open Sewer Exchange");
            ui.Live(() => "Rat futures: " + (1.2f + 0.1f * (float)Math.Sin(GameClock.Minute / 10f)).ToString("0.00") + " OC");
            ui.Label("A kit window over the screen. Esc closes it and you stay seated.", muted: true);
        });
        _notes = ComputerApps.RegisterOnScreen(kit, Notes, "Demo Notes", ui =>
        {
            ui.Header("Demo Notes");
            ui.Text("A window on the computer's own screen. Typing an \"e\" here doesn't make you get up.");
            ui.Live(() => "Game time " + GameClock.TimeText + ", button pressed " + _presses + " times");
            ui.Field(_typed, v => _typed = v, "Type something...");
            ui.Row(r =>
            {
                r.Button("Press me", () => _presses++);
                r.Button("Redraw", ui.Rebuild);
            });
            ui.Button("Close", ui.Window.Close);
            for (int i = 1; i <= 12; i++)
                ui.Text($"Line {i} of a list long enough to scroll with the mouse wheel.", 26f);
        });
    }
}
