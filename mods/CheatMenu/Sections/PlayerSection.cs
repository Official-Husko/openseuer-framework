using CheatMenu.Cheats;
using ObenFramework.Player;
using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>God mode, needs, health, noclip, move speed and the saved teleport spots.</summary>
internal static class PlayerSection
{
    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Player");
        if (!PlayerRef.Exists)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        ui.Toggle("God mode", PlayerState.Godmode, on => ctx.Run("God mode", () =>
        {
            PlayerState.Godmode = on;
            ctx.Say(on ? "God mode on: you can't die." : "God mode off.");
        }));

        ui.Row(r =>
        {
            r.Button("Refill needs", () => ctx.Run("Refill needs", () =>
            {
                PlayerVitals.ClearNeeds();
                ctx.Say("Needs refilled.");
            }));
            r.Button("Full heal", () => ctx.Run("Full heal", () =>
            {
                PlayerVitals.FullHeal();
                ctx.Say("Health restored.");
            }));
        });

        if (Noclip.Available)
        {
            ui.Toggle("Noclip", Noclip.Active, on => ctx.Run("Noclip", () =>
            {
                Noclip.Set(on);
                ctx.Say(on ? "Noclip on: fly with the movement keys." : "Noclip off.");
            }));
        }

        if (MoveSpeed.Available)
        {
            ui.Slider("Move speed", MoveSpeed.Min, MoveSpeed.Max, MoveSpeed.Factor,
                v => ctx.Run("Move speed", () => MoveSpeed.Set(v)), "0.0x");
        }

        BuildSpots(ui, ctx);
    }

    private static void BuildSpots(UiBuilder ui, CheatContext ctx)
    {
        var config = ctx.Config.Value;
        int slot = 0;
        var picker = ui.Cycle("Teleport spot", Spots.Labels(config), slot, i => slot = i);
        ui.Row(r =>
        {
            r.Button("Save here", () => ctx.Run("Save spot", () =>
            {
                ctx.Say(Spots.Save(ctx, slot));
                picker.SetOptions(Spots.Labels(config), slot);
            }));
            r.Button("Go there", () => ctx.Run("Teleport", () => ctx.Say(Spots.Go(ctx, slot))));
        });
    }
}
