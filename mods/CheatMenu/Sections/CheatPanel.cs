using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>The content shared by the Cheats window and the Cheats page of the MODS menu.</summary>
internal static class CheatPanel
{
    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Label($"Press {ctx.Config.Value.Key} or Esc to close the window.", muted: true);
        ui.Live(() => ctx.Status, muted: true);

        PlayerSection.Build(ui, ctx);
        MoneySection.Build(ui, ctx);
        ItemsSection.Build(ui, ctx);
        TimeSection.Build(ui, ctx);
        CrimeSection.Build(ui, ctx);
    }
}
