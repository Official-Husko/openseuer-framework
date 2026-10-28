using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Economy;

/// <summary>
/// Sets up the money side of the economy module: the bank ledger, player-run shops and the state the
/// banking, invoice, legal, fuel and trader APIs keep. Shops, prices and stock are set up by <see cref="ShopsModule"/>.
/// </summary>
internal static class EconomyModule
{
    public static void Init(ObenMod framework)
    {
        BankLedger.Init(framework);
        PlayerShops.Init(framework);
        framework.Track(Disposable.Create(() =>
        {
            BankLedger.Shutdown();
            PlayerShops.Shutdown();
            TraderFactors.Reset();
        }));
    }
}
