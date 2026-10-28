using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Economy;

/// <summary>Sets up the shops module: forgets scene-bound caches when a level goes away and undoes everything on unload.</summary>
internal static class ShopsModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, Shops.Reset);
        GameEvents.MainMenu.Subscribe(framework, Shops.Reset);
        framework.Track(Disposable.Create(() =>
        {
            Shops.Reset();
            Prices.Clear();
        }));
        ShopStock.Init(framework);
    }
}
