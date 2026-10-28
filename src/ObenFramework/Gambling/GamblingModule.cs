using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Gambling;

/// <summary>
/// Sets up the gambling module. Its hooks are applied with the framework's other patches and keep no state
/// but the payout multipliers, which are dropped on unload.
/// </summary>
internal static class GamblingModule
{
    public static void Init(ObenMod framework)
    {
        framework.Track(Disposable.Create(() =>
        {
            Payouts.Reset();
            PayoutScope.Disarm();
        }));
    }
}
