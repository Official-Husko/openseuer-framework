using ObenFramework.Economy;
using ObenFramework.Game;
using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>Open Sewer Coins and Real Money through the wallet.</summary>
internal static class MoneySection
{
    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Money");
        if (!WorldState.IsReady || !Wallet.CanChange)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        ui.Live(() => $"{Wallet.Coins} coins, {Wallet.RealMoney:0.0} Real Money");
        ui.Row(r =>
        {
            r.Button("+100 coins", () => Add(ctx, Money.Currency.OC, 100f));
            r.Button("+1000 coins", () => Add(ctx, Money.Currency.OC, 1000f));
        });
        ui.Row(r =>
        {
            r.Button("+10 Real Money", () => Add(ctx, Money.Currency.RM, 10f));
            r.Button("+100 Real Money", () => Add(ctx, Money.Currency.RM, 100f));
        });
        ui.Button("Set both to zero", () => ctx.Run("Set money to zero", () =>
        {
            Wallet.TryRemove(Wallet.Coins, Wallet.RealMoney);
            ctx.Say("Money set to zero.");
        }));
    }

    private static void Add(CheatContext ctx, Money.Currency currency, float amount) =>
        ctx.Run("Add money", () =>
        {
            ctx.Say(Wallet.Add(currency, amount)
                ? $"Added {amount:0} {(currency == Money.Currency.OC ? "coins" : "Real Money")}."
                : "Couldn't add money right now.");
        });
}
