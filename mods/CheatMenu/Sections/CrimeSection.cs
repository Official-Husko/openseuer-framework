using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>The console's <c>reset_crime</c>, which calls the same routine.</summary>
internal static class CrimeSection
{
    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Crime");
        if (Crime.instance == null)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        ui.Button("Reset crime", () => ctx.Run("Reset crime", () =>
        {
            if (Crime.instance == null)
                throw new System.InvalidOperationException(CheatContext.NeedsGame);
            Crime.instance.ClearCrime();
            ctx.Say("Crime cleared.");
        }));
    }
}
